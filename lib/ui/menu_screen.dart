import 'package:flame/game.dart';
import 'package:flutter/material.dart';

import '../data/achievements.dart';
import '../data/championship.dart';
import '../data/profile.dart';
import '../game/race_game.dart';
import '../sim/track.dart';
import 'championship_screen.dart';
import 'garage_screen.dart';
import 'race_screen.dart';
import 'settings_screen.dart';
import 'theme.dart';
import 'trophies_screen.dart';

const String gameTitle = 'RIOT RACERS'; // placeholder name - change any time

/// Main menu over a live AI race (attract mode).
class MenuScreen extends StatefulWidget {
  const MenuScreen({super.key});

  @override
  State<MenuScreen> createState() => _MenuScreenState();
}

class _MenuScreenState extends State<MenuScreen> {
  late RaceGame _attract;

  @override
  void initState() {
    super.initState();
    _attract = _newAttract();
  }

  RaceGame _newAttract() {
    final setup = quickRaceSetup();
    return RaceGame(
      // Show a random track behind the menu, with the AI driving everyone.
      setup.copyWithTrack(tracks[DateTime.now().second % tracks.length]),
      attract: true,
    );
  }

  Future<void> _open(Widget screen) async {
    await Navigator.of(context).push(MaterialPageRoute(builder: (_) => screen));
    if (mounted) setState(() {}); // refresh bank / labels
  }

  @override
  Widget build(BuildContext context) {
    final size = MediaQuery.sizeOf(context);
    final wide = size.width > 700;
    final short = size.height < 520; // phones in landscape
    return Scaffold(
      body: Stack(children: [
        IgnorePointer(child: GameWidget(game: _attract)),
        Container(
          decoration: const BoxDecoration(
            gradient: LinearGradient(colors: [Color(0xE6101320), Color(0x66101320), Color(0x00101320)], stops: [0, 0.55, 1]),
          ),
        ),
        SafeArea(
          child: Padding(
            padding: EdgeInsets.symmetric(horizontal: wide ? 60 : 24, vertical: short ? 12 : 20),
            child: short ? _phoneLayout() : _tallLayout(wide),
          ),
        ),
      ]),
    );
  }

  List<Widget> _buttons(double width, double height, double fontSize, double gap) => [
        GameButton.primary(Championship.hasActiveSeason ? 'CONTINUE SEASON' : 'CHAMPIONSHIP',
            width: width, height: height, fontSize: fontSize, onPressed: () => _open(const ChampionshipScreen())),
        SizedBox(height: gap),
        GameButton('QUICK RACE', width: width, height: height, fontSize: fontSize, onPressed: () => _open(const QuickRaceScreen())),
        SizedBox(height: gap),
        GameButton('GARAGE', width: width, height: height, fontSize: fontSize, onPressed: () => _open(const GarageScreen())),
        SizedBox(height: gap),
        GameButton('TROPHIES  ${Achievements.unlockedCount}/${Achievements.all.length}',
            width: width, height: height, fontSize: fontSize, onPressed: () => _open(const TrophiesScreen())),
        SizedBox(height: gap),
        GameButton('SETTINGS', width: width, height: height, fontSize: fontSize, onPressed: () => _open(const SettingsScreen())),
      ];

  Widget _tallLayout(bool wide) => Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
        Row(children: [const Spacer(), BankChip(Profile.money)]),
        const Spacer(),
        Text(gameTitle, style: title(wide ? 84 : 56, AppColors.accent)),
        Text('COMBAT RACING', style: body(wide ? 26 : 20)),
        Container(margin: const EdgeInsets.only(top: 8, bottom: 22), width: 260, height: 6, color: AppColors.hot),
        ..._buttons(340, 64, 26, 10),
        const Spacer(),
        Text('Built with Flutter + Flame  -  sounds: Kenney (CC0)', style: body(12, AppColors.dim)),
      ]);

  /// Landscape phone: title on the left, a compact column of buttons on the right.
  Widget _phoneLayout() => Row(crossAxisAlignment: CrossAxisAlignment.stretch, children: [
        Expanded(
          child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
            const Spacer(),
            FittedBox(fit: BoxFit.scaleDown, child: Text(gameTitle, style: title(60, AppColors.accent))),
            Text('COMBAT RACING', style: body(18)),
            Container(margin: const EdgeInsets.only(top: 8), width: 200, height: 5, color: AppColors.hot),
            const Spacer(),
            Text('Built with Flutter + Flame  -  sounds: Kenney (CC0)', style: body(11, AppColors.dim)),
          ]),
        ),
        const SizedBox(width: 16),
        FittedBox(
          fit: BoxFit.scaleDown,
          alignment: Alignment.centerRight,
          child: Column(mainAxisSize: MainAxisSize.min, crossAxisAlignment: CrossAxisAlignment.end, children: [
            BankChip(Profile.money),
            const SizedBox(height: 10),
            ..._buttons(300, 50, 22, 8),
          ]),
        ),
      ]);

}

/// Track, laps, opponents and difficulty for a single race.
class QuickRaceScreen extends StatefulWidget {
  const QuickRaceScreen({super.key});

  @override
  State<QuickRaceScreen> createState() => _QuickRaceScreenState();
}

class _QuickRaceScreenState extends State<QuickRaceScreen> {
  static const laps = [2, 3, 4, 5, 6];
  static const opponents = [1, 2, 3, 4, 5, 6, 7];

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      body: MenuBackdrop(
        child: Center(
          child: SingleChildScrollView(
            child: SizedBox(
              width: 620,
              child: GamePanel(
                child: Column(mainAxisSize: MainAxisSize.min, children: [
                  Text('QUICK RACE', style: title(44, AppColors.accent)),
                  const SizedBox(height: 14),
                  OptionRow(
                    label: 'TRACK',
                    options: [for (final t in tracks) t.name],
                    index: Profile.quickTrack.clamp(0, tracks.length - 1),
                    onChanged: (i) => setState(() => Profile.quickTrack = i),
                  ),
                  OptionRow(
                    label: 'LAPS',
                    options: [for (final l in laps) '$l'],
                    index: laps.indexOf(Profile.quickLaps).clamp(0, laps.length - 1),
                    onChanged: (i) => setState(() => Profile.quickLaps = laps[i]),
                  ),
                  OptionRow(
                    label: 'OPPONENTS',
                    options: [for (final o in opponents) '$o'],
                    index: opponents.indexOf(Profile.quickOpponents).clamp(0, opponents.length - 1),
                    onChanged: (i) => setState(() => Profile.quickOpponents = opponents[i]),
                  ),
                  OptionRow(
                    label: 'DIFFICULTY',
                    options: const ['EASY', 'NORMAL', 'HARD'],
                    index: Profile.quickDifficulty.clamp(0, 2),
                    onChanged: (i) => setState(() => Profile.quickDifficulty = i),
                  ),
                  const SizedBox(height: 6),
                  Text('Harder races pay more prize money', style: body(14, AppColors.dim)),
                  const SizedBox(height: 18),
                  Row(mainAxisAlignment: MainAxisAlignment.center, children: [
                    GameButton('BACK', width: 200, onPressed: () => Navigator.of(context).pop()),
                    const SizedBox(width: 16),
                    GameButton.primary('START', width: 240, onPressed: () {
                      Navigator.of(context).pushReplacement(MaterialPageRoute(builder: (_) => RaceScreen(setup: quickRaceSetup())));
                    }),
                  ]),
                ]),
              ),
            ),
          ),
        ),
      ),
    );
  }
}
