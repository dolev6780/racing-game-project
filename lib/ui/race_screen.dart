import 'dart:math' as math;

import 'package:flame/game.dart';
import 'package:flutter/material.dart';
import 'package:flutter/scheduler.dart';

import '../data/career.dart';
import '../data/championship.dart';
import '../data/profile.dart';
import '../game/race_game.dart';
import '../sim/car.dart';
import '../sim/race.dart';
import 'painters.dart';
import 'theme.dart';
import 'touch_controls.dart';

/// A race: the Flame game plus HUD, touch controls, pause menu and results.
class RaceScreen extends StatefulWidget {
  final RaceSetup setup;
  final bool autopilot;
  const RaceScreen({super.key, required this.setup, this.autopilot = false});

  @override
  State<RaceScreen> createState() => _RaceScreenState();
}

class _RaceScreenState extends State<RaceScreen> with SingleTickerProviderStateMixin {
  late final RaceGame game;
  late final Ticker _ticker;
  bool _paused = false;
  CareerResult? _result;

  // HUD state
  int _lastLights = -1, _lastPos = -1;
  double _goTimer = 0, _flash = 0, _lastArmor = 0, _posPop = 0, _finalLapTimer = 0;
  bool _finalLapShown = false;
  int _frame = 0;

  Race get race => game.race;
  Car get player => race.player;

  @override
  void initState() {
    super.initState();
    game = RaceGame(widget.setup,
        onFinished: (r) => setState(() => _result = r), onPauseKey: _togglePause, autopilot: widget.autopilot);
    _lastArmor = player.armor;
    _ticker = createTicker(_tick)..start();
  }

  @override
  void dispose() {
    _ticker.dispose();
    super.dispose();
  }

  void _togglePause() {
    if (race.state == RaceState.finished) return;
    setState(() => _paused = !_paused);
    game.setPaused(_paused);
  }

  Duration _lastTick = Duration.zero;
  void _tick(Duration elapsed) {
    final dt = ((elapsed - _lastTick).inMicroseconds / 1e6).clamp(0.0, 0.1);
    _lastTick = elapsed;
    if (!mounted) return;
    _frame++;
    if (!_paused) _updateHudState(dt);
    setState(() {});
  }

  void _updateHudState(double dt) {
    // Start lights: 3 -> one red, 2 -> two, 1 -> three, then green.
    if (race.state == RaceState.countdown) {
      final n = race.countdown.ceil().clamp(1, 3);
      if (n != _lastLights) {
        _lastLights = n;
        game.audio.play('beep_low.wav', 0.6);
      }
      _goTimer = 1.2;
    } else if (_goTimer > 0) {
      if (_lastLights != 0) {
        _lastLights = 0;
        game.audio.play('beep_high.wav', 0.7);
      }
      _goTimer -= dt;
    }

    if (player.armor < _lastArmor - 0.5) _flash = (_flash + (_lastArmor - player.armor) / 30 + 0.3).clamp(0.0, 1.0);
    _lastArmor = player.armor;
    _flash = math.max(0, _flash - dt * 1.8);

    final pos = race.positionOf(player);
    if (_lastPos > 0 && pos != _lastPos && race.state == RaceState.racing) {
      _posPop = 1;
      if (pos < _lastPos) game.audio.play('blip.wav', 0.5);
    }
    _lastPos = pos;
    _posPop = math.max(0, _posPop - dt * 3);

    if (!_finalLapShown && race.state == RaceState.racing && race.laps > 1 && player.lap == race.laps) {
      _finalLapShown = true;
      _finalLapTimer = 2.5;
    }
    _finalLapTimer = math.max(0, _finalLapTimer - dt);
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      backgroundColor: Colors.black,
      body: Stack(children: [
        GameWidget(game: game, autofocus: true),
        IgnorePointer(child: _damageFlash()),
        IgnorePointer(child: _speedLines()),
        if (Profile.showTouch && _result == null && !_paused) TouchControls(controls: game.controls, car: player),
        IgnorePointer(child: _hud()),
        Positioned(
          top: 12,
          left: 0,
          right: 0,
          child: Center(
            child: Row(mainAxisSize: MainAxisSize.min, children: [
              GameButton('II', width: 64, height: 64, fontSize: 26, onPressed: _togglePause),
              const SizedBox(width: 10),
              GameButton(Profile.camera == CameraView.topDown ? 'CAM: TURN' : 'CAM: NORTH', width: 150, height: 52, fontSize: 18,
                  onPressed: () => setState(() =>
                      Profile.camera = CameraView.values[(Profile.camera.index + 1) % CameraView.values.length])),
            ]),
          ),
        ),
        if (_paused) _pauseMenu(),
        if (_result != null) _results(),
      ]),
    );
  }

  // ---------- HUD ----------

  Widget _hud() {
    return Stack(children: [
      Positioned(left: 16, top: 16, child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
        _positionCard(),
        const SizedBox(height: 10),
        _speedometer(),
      ])),
      Positioned(right: 16, top: 16, child: Column(crossAxisAlignment: CrossAxisAlignment.end, children: [
        GamePanel(
          padding: const EdgeInsets.all(8),
          child: SizedBox(width: 170, height: 170, child: CustomPaint(painter: MinimapPainter(race))),
        ),
        const SizedBox(height: 8),
        _standings(),
      ])),
      if (!Profile.showTouch) Positioned(left: 16, bottom: 16, child: _ammo()),
      Positioned(left: 0, right: 0, top: 90, child: _feed()),
      Center(child: _centerMessages()),
    ]);
  }

  Widget _positionCard() {
    final pos = race.positionOf(player);
    final color = pos == 1 ? AppColors.gold : pos == 2 ? AppColors.silver : pos == 3 ? AppColors.bronze : Colors.white;
    final lap = player.lap.clamp(1, race.laps);
    final best = Profile.bestLap(widget.setup.track.name);
    return GamePanel(
      padding: const EdgeInsets.fromLTRB(18, 10, 18, 12),
      child: Row(crossAxisAlignment: CrossAxisAlignment.start, children: [
        Transform.scale(
          scale: 1 + _posPop * 0.35,
          child: RichText(
            text: TextSpan(children: [
              TextSpan(text: '$pos', style: title(64, color)),
              TextSpan(text: ordinal(pos).substring('$pos'.length), style: title(26, color)),
              TextSpan(text: ' /${race.cars.length}', style: title(20, AppColors.dim)),
            ]),
          ),
        ),
        const SizedBox(width: 22),
        Column(crossAxisAlignment: CrossAxisAlignment.end, children: [
          Text('LAP', style: body(14, AppColors.dim)),
          Text('$lap/${race.laps}', style: title(34, AppColors.accent)),
          Text(formatTime(race.raceTime), style: body(18)),
          if (best > 0) Text('BEST ${formatTime(best)}', style: body(12, AppColors.dim)),
        ]),
      ]),
    );
  }

  Widget _standings() {
    return GamePanel(
      padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 8),
      child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
        for (int i = 0; i < race.standings.length; i++)
          Builder(builder: (_) {
            final c = race.standings[i];
            return Container(
              width: 186,
              margin: const EdgeInsets.symmetric(vertical: 1.5),
              padding: const EdgeInsets.symmetric(horizontal: 6, vertical: 2),
              decoration: BoxDecoration(
                color: c.isPlayer ? AppColors.accent.withValues(alpha: 0.2) : Colors.transparent,
                borderRadius: BorderRadius.circular(6),
              ),
              child: Row(children: [
                Container(width: 6, height: 16, color: c.color),
                const SizedBox(width: 6),
                Expanded(
                  child: Text('${i + 1}. ${c.name.toUpperCase()}',
                      overflow: TextOverflow.ellipsis, style: body(13, c.isPlayer ? AppColors.accent : Colors.white)),
                ),
                if (c.finished) Text('FIN', style: body(11, AppColors.good)),
                if (c.wrecked) Text('X', style: body(11, AppColors.bad)),
              ]),
            );
          }),
      ]),
    );
  }

  Widget _speedometer() {
    final v = (player.forwardSpeed.abs() / (player.maxSpeed * player.boostMultiplier)).clamp(0.0, 1.0);
    final a = player.armor01;
    return Column(mainAxisSize: MainAxisSize.min, children: [
      SizedBox(
        width: 124,
        height: 124,
        child: CustomPaint(
          painter: GaugePainter(v, player.boosting),
          child: Center(
            child: Column(mainAxisSize: MainAxisSize.min, children: [
              Text('${player.speedKmh.round()}', style: title(38)),
              Text('KM/H', style: body(13, AppColors.dim)),
            ]),
          ),
        ),
      ),
      const SizedBox(height: 4),
      Container(
        width: 124,
        height: 16,
        padding: const EdgeInsets.all(3),
        decoration: BoxDecoration(color: const Color(0x88000000), borderRadius: BorderRadius.circular(9)),
        child: Align(
          alignment: Alignment.centerLeft,
          child: FractionallySizedBox(
            widthFactor: math.max(a, 0.001),
            child: Container(
              decoration: BoxDecoration(
                color: a > 0.5 ? AppColors.good : a > 0.25 ? AppColors.accent : AppColors.bad,
                borderRadius: BorderRadius.circular(6),
              ),
            ),
          ),
        ),
      ),
    ]);
  }

  Widget _ammo() {
    Widget row(String label, int have, int max, Color color) => Row(children: [
          SizedBox(width: 92, child: Text(label, style: body(15))),
          for (int i = 0; i < math.max(max, have); i++)
            Container(
              width: 16,
              height: 16,
              margin: const EdgeInsets.all(2),
              decoration: BoxDecoration(color: color.withValues(alpha: i < have ? 1 : 0.2), shape: BoxShape.circle),
            ),
        ]);
    return GamePanel(
      padding: const EdgeInsets.all(12),
      child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
        row('MISSILES', player.missiles, player.missilesPerLap, AppColors.hot),
        row('MINES', player.mines, player.minesPerLap, const Color(0xFFD9D9E6)),
        row('NITRO', player.boosts, player.boostsPerLap, AppColors.nitro),
      ]),
    );
  }

  Widget _feed() {
    return Column(children: [
      for (final f in game.feed)
        Opacity(
          opacity: (1 - ((game.clock - f.time) - 2.5) / 0.5).clamp(0.0, 1.0),
          child: Container(
            margin: const EdgeInsets.symmetric(vertical: 3),
            padding: const EdgeInsets.symmetric(horizontal: 18, vertical: 6),
            decoration: BoxDecoration(color: AppColors.panel, borderRadius: BorderRadius.circular(12)),
            child: Text(f.text, style: title(20, f.color)),
          ),
        ),
    ]);
  }

  Widget _centerMessages() {
    final children = <Widget>[];
    if (race.state == RaceState.countdown || _goTimer > 0) {
      if (race.state == RaceState.countdown) {
        children.add(GamePanel(
          padding: const EdgeInsets.symmetric(horizontal: 22, vertical: 10),
          child: Column(children: [
            Text(widget.setup.track.name, style: title(34, AppColors.accent)),
            Text(
                '${widget.setup.isChampionship ? '${widget.setup.label} - RACE ${widget.setup.championshipRace + 1}' : 'QUICK RACE - ${widget.setup.label}'}  -  ${race.laps} LAPS',
                style: body(15)),
          ]),
        ));
        children.add(const SizedBox(height: 14));
      }
      final lit = race.state == RaceState.countdown ? 4 - race.countdown.ceil().clamp(1, 3) : 3;
      final green = race.state != RaceState.countdown;
      children.add(GamePanel(
        padding: const EdgeInsets.all(12),
        color: const Color(0xEE0D0D12),
        child: Row(mainAxisSize: MainAxisSize.min, children: [
          for (int i = 0; i < 3; i++)
            Container(
              width: 64,
              height: 64,
              margin: const EdgeInsets.symmetric(horizontal: 8),
              decoration: BoxDecoration(
                shape: BoxShape.circle,
                color: green ? AppColors.good : (i < lit ? const Color(0xFFFF2A1A) : const Color(0xFF401414)),
                boxShadow: [if (green || i < lit) BoxShadow(color: (green ? AppColors.good : const Color(0xFFFF2A1A)).withValues(alpha: 0.7), blurRadius: 18)],
              ),
            ),
        ]),
      ));
      if (green) children.add(Opacity(opacity: (_goTimer / 0.4).clamp(0.0, 1.0), child: Text('GO!', style: title(96, AppColors.accent))));
    } else if (race.state == RaceState.racing) {
      String? msg;
      if (player.wrecked) {
        msg = 'WRECKED!';
      } else if (player.wrongWay && (_frame ~/ 15).isEven) {
        msg = 'WRONG WAY!';
      }
      if (msg != null) {
        children.add(GamePanel(color: const Color(0xDDB81414), child: Text(msg, style: title(44))));
      }
      if (_finalLapTimer > 0) {
        children.add(const SizedBox(height: 10));
        children.add(GamePanel(color: AppColors.hot, child: Text('FINAL LAP!', style: title(44))));
      }
    }
    return Column(mainAxisSize: MainAxisSize.min, children: children);
  }

  Widget _damageFlash() => Container(
        decoration: BoxDecoration(
          gradient: RadialGradient(
            radius: 1.1,
            colors: [Colors.transparent, const Color(0xFFFF1A0D).withValues(alpha: _flash * 0.6)],
            stops: const [0.55, 1],
          ),
        ),
      );

  Widget _speedLines() {
    final speed01 = (player.forwardSpeed.abs() / player.maxSpeed).clamp(0.0, 1.0);
    final alpha = race.state != RaceState.racing ? 0.0 : player.boosting ? 0.45 : ((speed01 - 0.85) / 0.15).clamp(0.0, 1.0) * 0.15;
    return CustomPaint(size: Size.infinite, painter: SpeedLinesPainter(alpha, _frame));
  }

  // ---------- Pause & results ----------

  Widget _pauseMenu() {
    return Container(
      color: const Color(0x99000000),
      alignment: Alignment.center,
      child: GamePanel(
        padding: const EdgeInsets.all(28),
        child: Column(mainAxisSize: MainAxisSize.min, children: [
          Text('PAUSED', style: title(52, AppColors.accent)),
          const SizedBox(height: 20),
          GameButton.primary('RESUME', onPressed: _togglePause),
          const SizedBox(height: 12),
          GameButton('RESTART', onPressed: _restart),
          const SizedBox(height: 12),
          GameButton('QUIT RACE', onPressed: () => Navigator.of(context).pop()),
        ]),
      ),
    );
  }

  void _restart() {
    Navigator.of(context).pushReplacement(MaterialPageRoute(builder: (_) => RaceScreen(setup: widget.setup)));
  }

  Widget _results() {
    final place = player.finishPlace;
    final champ = (_result?.championshipPoints ?? -1) >= 0;
    final order = race.standings;
    return Container(
      color: const Color(0xAA000000),
      alignment: Alignment.center,
      child: SingleChildScrollView(
        child: GamePanel(
          padding: const EdgeInsets.all(26),
          child: Column(mainAxisSize: MainAxisSize.min, children: [
            Text(place == 1 ? 'VICTORY!' : 'YOU FINISHED ${ordinal(place)}', style: title(48, place <= 3 ? AppColors.accent : Colors.white)),
            const SizedBox(height: 14),
            Row(crossAxisAlignment: CrossAxisAlignment.start, mainAxisSize: MainAxisSize.min, children: [
              Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
                for (int i = 0; i < order.length; i++)
                  SizedBox(
                    width: 330,
                    child: Row(children: [
                      SizedBox(width: 56, child: Text(ordinal(i + 1), style: title(20, order[i].isPlayer ? AppColors.accent : Colors.white))),
                      Container(width: 8, height: 18, color: order[i].color),
                      const SizedBox(width: 8),
                      Expanded(child: Text(order[i].name.toUpperCase(), style: body(18, order[i].isPlayer ? AppColors.accent : Colors.white))),
                      Text(order[i].finished ? formatTime(order[i].finishTime) : 'racing...', style: body(16, AppColors.dim)),
                    ]),
                  ),
              ]),
              const SizedBox(width: 24),
              GamePanel(
                color: AppColors.panelLight,
                child: SizedBox(
                  width: 240,
                  child: Column(children: [
                    _earn('PRIZE (${widget.setup.label})', race.earnedPrize),
                    _earn('WRECKS x${player.kills}', race.earnedKills),
                    _earn('PICKUPS', race.earnedPickups),
                    const Divider(color: Colors.white24),
                    _earn('TOTAL', race.earnedTotal, AppColors.accent),
                    _earn('BANK', Profile.money),
                  ]),
                ),
              ),
            ]),
            if (champ) ...[
              const SizedBox(height: 14),
              Text('+${_result!.championshipPoints} CHAMPIONSHIP POINTS', style: title(28, AppColors.accent)),
            ],
            const SizedBox(height: 20),
            if (champ)
              GameButton.primary('CONTINUE', onPressed: () => Navigator.of(context).pop())
            else
              Row(mainAxisSize: MainAxisSize.min, children: [
                GameButton('MAIN MENU', onPressed: () => Navigator.of(context).pop()),
                const SizedBox(width: 16),
                GameButton.primary('RACE AGAIN', onPressed: () {
                  Navigator.of(context).pushReplacement(MaterialPageRoute(builder: (_) => RaceScreen(setup: quickRaceSetup())));
                }),
              ]),
          ]),
        ),
      ),
    );
  }

  Widget _earn(String label, int value, [Color color = Colors.white]) => Padding(
        padding: const EdgeInsets.symmetric(vertical: 3),
        child: Row(children: [
          Expanded(child: Text(label, style: body(15, AppColors.dim))),
          Text(money(value), style: title(18, color)),
        ]),
      );
}
