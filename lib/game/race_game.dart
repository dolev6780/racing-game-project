import 'dart:async';
import 'dart:math' as math;


import 'package:flame/components.dart';
import 'package:flame/events.dart';
import 'package:flame/game.dart';
import 'package:flutter/services.dart';
import 'package:flutter/widgets.dart' show KeyEventResult;
import 'package:vector_math/vector_math.dart' as vm;

import '../data/career.dart';
import '../data/profile.dart';
import '../sim/race.dart';
import 'audio.dart';
import 'controls.dart';
import 'effects_layer.dart';
import 'iso.dart';
import 'iso_scene.dart';

class FeedItem {
  final String text;
  final Color color;
  final double time;
  FeedItem(this.text, this.color, this.time);
}

/// Flame game that renders a [Race] simulation and handles camera, effects, audio and stats.
class RaceGame extends FlameGame with KeyboardEvents {
  final RaceSetup setup;
  final PlayerControls controls = PlayerControls();
  final GameAudio audio = GameAudio();
  final void Function(CareerResult result)? onFinished;
  final VoidCallback? onPauseKey;
  /// Attract mode: AI drives every car (menu background); no sound, stats or results.
  final bool attract;
  late final Race race;
  late final EffectsLayer _effects;

  final List<FeedItem> feed = [];
  double clock = 0;
  double _trauma = 0, _zoomFactor = 1;
  final vm.Vector2 _focus = vm.Vector2.zero(); // world point the camera looks at
  double _playerLapStart = -1, _topSpeed = 0;
  bool _recorded = false;
  CareerResult? result;


  /// [autopilot]: the AI drives the player's car (testing/demo); results and stats work normally.
  RaceGame(this.setup, {this.onFinished, this.onPauseKey, this.attract = false, bool autopilot = false, int seed = 0}) {
    race = Race(setup,
        seed: seed == 0 ? DateTime.now().millisecondsSinceEpoch : seed,
        playerDriver: attract || autopilot ? null : PlayerDriver(controls));
  }

  @override
  Color backgroundColor() => setup.track.theme.sky;

  @override
  Future<void> onLoad() async {
    _effects = EffectsLayer(race);
    world.addAll([_effects, IsoScene(race, _effects, () => _focus, _viewRadius)]);
    camera.viewfinder.anchor = Anchor.center;
    _focus.setFrom(race.player.position);
    _snapCamera();
    if (!attract) {
      // Never block the race on audio: some browsers/devices only allow sound after a tap,
      // and loading can stall. Load in the background with a timeout instead.
      unawaited(GameAudio.preload()
          .timeout(const Duration(seconds: 5))
          .then((_) => audio.startEngine())
          .catchError((Object _) {}));
    }
  }

  @override
  void onRemove() {
    audio.dispose();
    if (!attract) Profile.maxStat(Stat.topSpeedKmh, _topSpeed.round());
    super.onRemove();
  }

  void setPaused(bool paused) {
    race.paused = paused;
    audio.pauseEngine(paused);
  }

  @override
  KeyEventResult onKeyEvent(KeyEvent event, Set<LogicalKeyboardKey> keysPressed) {
    if (event is KeyDownEvent && (event.logicalKey == LogicalKeyboardKey.escape || event.logicalKey == LogicalKeyboardKey.keyP)) {
      onPauseKey?.call();
      return KeyEventResult.handled;
    }
    if (event is KeyDownEvent && event.logicalKey == LogicalKeyboardKey.keyC) { // zoom close / wide
      Profile.camera = CameraView.values[(Profile.camera.index + 1) % CameraView.values.length];
    }
    controls.onKey(event);
    return KeyEventResult.handled;
  }

  @override
  void update(double dt) {
    if (race.paused) return;
    clock += dt;
    race.update(dt);
    _handleEvents();
    super.update(dt);
    _updateCamera(dt);

    final p = race.player;
    if (!p.finished) _topSpeed = math.max(_topSpeed, p.speedKmh);
    audio.updateEngine((p.forwardSpeed.abs() / p.maxSpeed).clamp(0.0, 1.0), p.boosting, dt);
    feed.removeWhere((f) => clock - f.time > 3);
  }

  void addFeed(String text, Color color) {
    feed.insert(0, FeedItem(text, color, clock));
    if (feed.length > 4) feed.removeLast();
  }

  void _handleEvents() {
    final p = race.player;
    if (attract) {
      for (final e in race.events) {
        if (e.type == RaceEventType.explosion) _effects.explode(e.position, e.value);
        if (e.type == RaceEventType.hit) _effects.sparks(e.position, const Color(0xFFFFE08A), 8);
      }
      race.events.clear();
      return;
    }
    for (final e in race.events) {
      final mine = identical(e.car, p);
      switch (e.type) {
        case RaceEventType.explosion:
          _effects.explode(e.position, e.value);
          final dist = e.position.distanceTo(p.position);
          if (dist < 30) {
            _shake(e.value * 0.06 * (1 - dist / 30));
            audio.play('impact.ogg', 0.6 * (1 - dist / 30));
          }
        case RaceEventType.hit:
          _effects.sparks(e.position, const Color(0xFFFFE08A), 8);
          if (mine || identical(e.other, p)) {
            _shake((e.value / 40).clamp(0.1, 0.5));
            audio.play('impact.ogg', (e.value / 25).clamp(0.2, 1.0));
          }
        case RaceEventType.wreck:
          final victim = e.car!, attacker = e.other;
          if (identical(attacker, p) && !mine) {
            Profile.addStat(Stat.wrecks);
            addFeed('YOU WRECKED ${victim.name.toUpperCase()}  +\$${Race.killBonus}', const Color(0xFFFFC81A));
          } else if (mine) {
            Profile.addStat(Stat.timesWrecked);
            addFeed(attacker != null ? '${attacker.name.toUpperCase()} WRECKED YOU!' : 'WRECKED!', const Color(0xFFFF4D4D));
          } else if (attacker != null) {
            addFeed('${attacker.name.toUpperCase()} WRECKED ${victim.name.toUpperCase()}', const Color(0xFFFFFFFF));
          }
        case RaceEventType.pickup:
          if (mine) {
            addFeed(e.text, const Color(0xFFFFC81A));
            audio.play('blip.wav', 0.6);
            if (e.value == 4) Profile.addStat(Stat.moneyPickups);
          }
        case RaceEventType.lap:
          if (mine) _onPlayerLap(e.value.toInt());
        case RaceEventType.miniTurbo:
          _effects.sparks(e.car!.position - e.car!.forward * 1.5, EffectsLayer.tierColors[e.value.toInt().clamp(0, 3)], 16);
          if (mine) {
            const names = ['', 'MINI-TURBO!', 'SUPER MINI-TURBO!', 'ULTRA MINI-TURBO!'];
            addFeed(names[e.value.toInt().clamp(1, 3)], EffectsLayer.tierColors[e.value.toInt().clamp(1, 3)]);
          }
        case RaceEventType.boostPad:
          if (mine) audio.play('blip.wav', 0.4);
        case RaceEventType.missileFired:
          if (mine) Profile.addStat(Stat.missilesFired);
        case RaceEventType.mineHit:
          if (mine) Profile.addStat(Stat.mineHits);
        case RaceEventType.nitro:
          if (mine) Profile.addStat(Stat.nitrosUsed);
        case RaceEventType.mineDropped:
          break;
        case RaceEventType.finish:
          if (mine && !_recorded) {
            _recorded = true;
            Profile.maxStat(Stat.topSpeedKmh, _topSpeed.round());
            result = recordFinishedRace(race);
            onFinished?.call(result!);
          }
      }
    }
    race.events.clear();
  }

  void _onPlayerLap(int lap) {
    if (lap >= 2 && _playerLapStart >= 0) {
      final t = race.raceTime - _playerLapStart;
      final record = Profile.submitLap(setup.track.name, t);
      addFeed(record ? 'NEW LAP RECORD!  ${formatTime(t)}' : 'LAP  ${formatTime(t)}',
          record ? const Color(0xFF59F273) : const Color(0xFFFFFFFF));
      audio.play('lap.wav', 0.6);
    }
    _playerLapStart = race.raceTime;
  }

  void _shake(double amount) => _trauma = (_trauma + amount).clamp(0.0, 1.0);

  // ---------- Camera (fixed isometric angle, Rock n' Roll Racing style) ----------

  /// Metres visible across the shorter screen side.
  double get _viewMeters => Profile.camera == CameraView.close ? 34 : 46;
  double get _baseZoom => math.max(1, math.min(size.x, size.y) / _viewMeters);

  /// World distance from the focus to the farthest screen corner (the ground is squashed on screen).
  double _viewRadius() {
    final z = camera.viewfinder.zoom;
    final hw = size.x / 2 / z + 1.5, hh = size.y / 2 / z / Iso.squash + 1.5;
    return math.sqrt(hw * hw + hh * hh);
  }

  void _snapCamera() {
    final o = Iso.projectV(_focus);
    camera.viewfinder.position = Vector2(o.dx, o.dy);
    camera.viewfinder.zoom = _baseZoom;
    camera.viewfinder.angle = 0;
  }

  void _updateCamera(double dt) {
    final p = race.player;
    // Look ahead in the direction of travel.
    final target = p.position + p.velocity * 0.32;
    _focus.add((target - _focus) * (1 - math.exp(-5 * dt)));
    final o = Iso.projectV(_focus);
    camera.viewfinder.position = Vector2(o.dx, o.dy);

    final speed01 = (p.forwardSpeed.abs() / p.maxSpeed).clamp(0.0, 1.5);
    final zf = 1 / (1 + speed01 * 0.12 + (p.boosting ? 0.06 : 0));
    _zoomFactor += (zf - _zoomFactor) * (1 - math.exp(-2.5 * dt));
    camera.viewfinder.zoom = _baseZoom * _zoomFactor;

    camera.viewfinder.angle = 0;
    if (_trauma > 0) {
      final s = _trauma * _trauma;
      camera.viewfinder.position += Vector2(math.sin(clock * 47) * s * 0.8, math.cos(clock * 53) * s * 0.8);
      camera.viewfinder.angle = math.sin(clock * 37) * s * 0.02;
      _trauma = math.max(0, _trauma - dt * 1.5);
    }
  }
}

String formatTime(double t) {
  final m = t ~/ 60;
  final s = t - m * 60;
  return '$m:${s.toStringAsFixed(2).padLeft(5, '0')}';
}
