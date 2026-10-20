import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter/scheduler.dart';
import 'package:flutter/services.dart';

import 'data/achievements.dart';
import 'data/championship.dart';
import 'data/profile.dart';
import 'sim/track.dart';
import 'ui/championship_screen.dart';
import 'ui/garage_screen.dart';
import 'ui/menu_screen.dart';
import 'ui/race_screen.dart';
import 'ui/settings_screen.dart';
import 'ui/theme.dart';
import 'ui/trophies_screen.dart';

Future<void> main() async {
  WidgetsFlutterBinding.ensureInitialized();
  await Profile.load();
  Achievements.init();
  await SystemChrome.setPreferredOrientations([DeviceOrientation.landscapeLeft, DeviceOrientation.landscapeRight]);
  await SystemChrome.setEnabledSystemUIMode(SystemUiMode.immersiveSticky);
  if (Uri.base.queryParameters['perf'] == '1') _logFrameTimes();
  runApp(const RiotRacersApp());
}

/// Dev aid (?perf=1): prints the average build and raster time per frame every 120 frames.
void _logFrameTimes() {
  int n = 0, build = 0, raster = 0;
  SchedulerBinding.instance.addTimingsCallback((timings) {
    for (final t in timings) {
      n++;
      build += t.buildDuration.inMicroseconds;
      raster += t.rasterDuration.inMicroseconds;
    }
    if (n >= 120) {
      debugPrint('PERF build ${(build / n / 1000).toStringAsFixed(2)} ms  raster ${(raster / n / 1000).toStringAsFixed(2)} ms');
      n = build = raster = 0;
    }
  });
}

class RiotRacersApp extends StatelessWidget {
  const RiotRacersApp({super.key});

  @override
  Widget build(BuildContext context) {
    return MaterialApp(
      title: gameTitle,
      debugShowCheckedModeBanner: false,
      theme: ThemeData(brightness: Brightness.dark, fontFamily: 'Rubik', colorSchemeSeed: AppColors.accent),
      home: _startScreen(),
      builder: (context, child) => Stack(children: [child!, const AchievementToast()]),
    );
  }

  /// Web dev shortcut: ?screen=race|garage|champ|trophies|settings (&track=N&laps=N&autopilot=1) opens a screen directly.
  Widget _startScreen() {
    final q = Uri.base.queryParameters;
    switch (q['screen']) {
      case 'race':
        final t = int.tryParse(q['track'] ?? '') ?? 0;
        var setup = quickRaceSetup().copyWithTrack(tracks[t.clamp(0, tracks.length - 1)]);
        final laps = int.tryParse(q['laps'] ?? '');
        if (laps != null) setup = setup.copyWithLaps(laps);
        return RaceScreen(setup: setup, autopilot: q['autopilot'] == '1');
      case 'garage':
        return const GarageScreen();
      case 'champ':
        return const ChampionshipScreen();
      case 'trophies':
        return const TrophiesScreen();
      case 'settings':
        return const SettingsScreen();
      default:
        return const MenuScreen();
    }
  }
}

/// Gold banner that slides in whenever an achievement unlocks (any screen).
class AchievementToast extends StatefulWidget {
  const AchievementToast({super.key});

  @override
  State<AchievementToast> createState() => _AchievementToastState();
}

class _AchievementToastState extends State<AchievementToast> {
  final List<AchievementDef> _queue = [];
  AchievementDef? _current;
  bool _visible = false;

  @override
  void initState() {
    super.initState();
    Achievements.lastUnlocked.addListener(_onUnlock);
  }

  @override
  void dispose() {
    Achievements.lastUnlocked.removeListener(_onUnlock);
    super.dispose();
  }

  void _onUnlock() {
    final a = Achievements.lastUnlocked.value;
    if (a == null) return;
    _queue.add(a);
    if (_current == null) _next();
  }

  Future<void> _next() async {
    if (_queue.isEmpty) {
      _current = null;
      return;
    }
    setState(() {
      _current = _queue.removeAt(0);
      _visible = true;
    });
    await Future<void>.delayed(const Duration(milliseconds: 3200));
    if (!mounted) return;
    setState(() => _visible = false);
    await Future<void>.delayed(const Duration(milliseconds: 400));
    if (mounted) _next();
  }

  @override
  Widget build(BuildContext context) {
    final a = _current;
    return IgnorePointer(
      child: AnimatedSlide(
        offset: _visible ? Offset.zero : const Offset(0, -1.5),
        duration: const Duration(milliseconds: 350),
        curve: Curves.easeOutBack,
        child: Align(
          alignment: Alignment.topCenter,
          child: a == null
              ? const SizedBox.shrink()
              : Padding(
                  padding: const EdgeInsets.only(top: 16),
                  child: Material(
                    color: Colors.transparent,
                    child: Container(
                      width: 520,
                      padding: const EdgeInsets.all(14),
                      decoration: BoxDecoration(
                        color: const Color(0xF2141720),
                        borderRadius: BorderRadius.circular(18),
                        border: Border.all(color: AppColors.accent, width: 2),
                        boxShadow: [BoxShadow(color: AppColors.accent.withValues(alpha: 0.4), blurRadius: 24)],
                      ),
                      child: Row(children: [
                        const Icon(Icons.emoji_events, size: 52, color: AppColors.gold),
                        const SizedBox(width: 12),
                        Expanded(
                          child: Column(crossAxisAlignment: CrossAxisAlignment.start, mainAxisSize: MainAxisSize.min, children: [
                            Text('ACHIEVEMENT UNLOCKED', style: title(14, AppColors.accent)),
                            Text(a.name, style: title(26)),
                            Text(a.description, style: body(13, AppColors.dim)),
                          ]),
                        ),
                        if (a.reward > 0) Text('+${money(a.reward)}', style: title(24, AppColors.good)),
                      ]),
                    ),
                  ),
                ),
        ),
      ),
    );
  }
}
