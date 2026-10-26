import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:riot_racers/data/achievements.dart';
import 'package:riot_racers/data/championship.dart';
import 'package:riot_racers/data/profile.dart';
import 'package:riot_racers/main.dart';
import 'package:riot_racers/ui/championship_screen.dart';
import 'package:riot_racers/ui/garage_screen.dart';
import 'package:riot_racers/ui/menu_screen.dart';
import 'package:riot_racers/ui/race_screen.dart';
import 'package:riot_racers/ui/settings_screen.dart';
import 'package:riot_racers/ui/trophies_screen.dart';
import 'package:shared_preferences/shared_preferences.dart';

/// Phone screens in landscape (physical pixels, device pixel ratio): every screen must lay out without overflowing.
const phones = [
  (Size(2400, 1080), 2.75), // typical Android phone (873 x 393 logical)
  (Size(1600, 720), 2.0), // small Android phone (800 x 360)
  (Size(2532, 1170), 3.0), // iPhone (844 x 390)
];

Future<void> pumpScreen(WidgetTester tester, Widget screen, Size size, double dpr) async {
  tester.view.physicalSize = size;
  tester.view.devicePixelRatio = dpr;
  await tester.pumpWidget(MaterialApp(home: screen));
  for (int i = 0; i < 20; i++) {
    await tester.pump(const Duration(milliseconds: 50));
  }
}

void main() {
  setUp(() async {
    SharedPreferences.setMockInitialValues({});
    await Profile.load();
    Achievements.init();
  });

  testWidgets('app starts and shows the main menu', (tester) async {
    tester.view.physicalSize = phones.first.$1;
    tester.view.devicePixelRatio = phones.first.$2;
    addTearDown(tester.view.reset);
    await tester.pumpWidget(const RiotRacersApp());
    for (int i = 0; i < 30; i++) {
      await tester.pump(const Duration(milliseconds: 50));
    }
    expect(find.text('QUICK RACE'), findsOneWidget);
  });

  final screens = <String, Widget Function()>{
    'menu': () => const MenuScreen(),
    'quick race': () => const QuickRaceScreen(),
    'garage': () => const GarageScreen(),
    'championship': () => const ChampionshipScreen(),
    'trophies': () => const TrophiesScreen(),
    'settings': () => const SettingsScreen(),
    'race': () => RaceScreen(setup: quickRaceSetup()),
  };
  for (final e in screens.entries) {
    for (final (size, dpr) in phones) {
      testWidgets('${e.key} fits on a ${size.width ~/ dpr}x${size.height ~/ dpr} phone', (tester) async {
        addTearDown(tester.view.reset);
        await pumpScreen(tester, e.value(), size, dpr);
        expect(tester.takeException(), isNull);
        // Let background timers (audio loading timeout) finish before the test ends.
        await tester.pumpWidget(const SizedBox());
        await tester.pump(const Duration(seconds: 6));
      });
    }
  }
}
