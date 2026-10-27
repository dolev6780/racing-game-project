import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter/scheduler.dart';
import 'package:flutter/services.dart';
import 'package:shared_preferences/shared_preferences.dart';

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

/// Last unexpected error, shown on screen (a black screen tells nobody anything).
final ValueNotifier<String?> lastError = ValueNotifier(null);

void reportError(Object error) {
  final text = '$error';
  debugPrint('ERROR: $text');
  lastError.value = text.length > 400 ? '${text.substring(0, 400)}...' : text;
}

void main() {
  runZonedGuarded(() {
    WidgetsFlutterBinding.ensureInitialized();
    FlutterError.onError = (details) {
      FlutterError.presentError(details);
      reportError(details.exceptionAsString());
    };
    ErrorWidget.builder = (details) => _ErrorPanel(details.exceptionAsString());
    if (Uri.base.queryParameters['perf'] == '1') _logFrameTimes();
    // Show something immediately; load saved data behind a loading screen.
    runApp(const _Boot());
  }, (error, stack) => reportError(error));
}

/// Loading screen while saved progress loads, then the game.
class _Boot extends StatefulWidget {
  const _Boot();

  @override
  State<_Boot> createState() => _BootState();
}

class _BootState extends State<_Boot> {
  bool _ready = false;
  String? _failed;

  @override
  void initState() {
    super.initState();
    _load();
  }

  Future<void> _load() async {
    try {
      try {
        await Profile.load();
      } catch (e) {
        // Saving is broken on this device: play anyway, keeping progress in memory only.
        reportError('Progress will not be saved on this device: $e');
        // ignore: invalid_use_of_visible_for_testing_member
        SharedPreferences.setMockInitialValues({});
        await Profile.load();
      }
      Achievements.init();
      await SystemChrome.setPreferredOrientations([DeviceOrientation.landscapeLeft, DeviceOrientation.landscapeRight]);
      await SystemChrome.setEnabledSystemUIMode(SystemUiMode.immersiveSticky);
      if (mounted) setState(() => _ready = true);
    } catch (e) {
      reportError(e);
      if (mounted) setState(() => _failed = '$e');
    }
  }

  @override
  Widget build(BuildContext context) {
    if (_ready) return const RiotRacersApp();
    return Directionality(
      textDirection: TextDirection.ltr,
      child: ColoredBox(
        color: AppColors.dark,
        child: Center(
          child: Text(_failed == null ? 'LOADING...' : 'Could not start: $_failed',
              textAlign: TextAlign.center,
              style: const TextStyle(fontFamily: 'RussoOne', fontSize: 28, color: AppColors.accent, decoration: TextDecoration.none)),
        ),
      ),
    );
  }
}

/// Replaces the grey box Flutter shows for a broken widget in release builds.
class _ErrorPanel extends StatelessWidget {
  final String message;
  const _ErrorPanel(this.message);

  @override
  Widget build(BuildContext context) => ColoredBox(
        color: const Color(0xFF3A0D12),
        child: Padding(
          padding: const EdgeInsets.all(8),
          child: Text(message,
              textDirection: TextDirection.ltr,
              style: const TextStyle(fontSize: 12, color: Colors.white, decoration: TextDecoration.none)),
        ),
      );
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
      builder: (context, child) => Stack(children: [child!, const AchievementToast(), const _ErrorBanner()]),
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

/// Small banner with the last unexpected error (tap to hide), so problems on a phone can be reported.
class _ErrorBanner extends StatelessWidget {
  const _ErrorBanner();

  @override
  Widget build(BuildContext context) => ValueListenableBuilder<String?>(
        valueListenable: lastError,
        builder: (context, error, _) => error == null
            ? const SizedBox.shrink()
            : Positioned(
                left: 8,
                right: 8,
                bottom: 8,
                child: GestureDetector(
                  onTap: () => lastError.value = null,
                  child: Container(
                    padding: const EdgeInsets.all(8),
                    decoration: BoxDecoration(color: const Color(0xE63A0D12), borderRadius: BorderRadius.circular(8)),
                    child: Text('Error: $error  (tap to hide)', style: const TextStyle(fontSize: 12, color: Colors.white)),
                  ),
                ),
              ),
      );
}
