import 'dart:convert';

import 'package:flutter/foundation.dart' show defaultTargetPlatform, TargetPlatform;
import 'package:shared_preferences/shared_preferences.dart';

import 'vehicles.dart';

enum Stat {
  races, wins, podiums, winStreak, bestWinStreak, wrecks, timesWrecked, missilesFired, mineHits,
  nitrosUsed, moneyPickups, moneyEarned, topSpeedKmh, tracksRaced, upgradesBought, vehiclesBought,
}

/// Touch steering: [arrows] = left/right buttons that turn the car; [stick] = point the stick
/// where you want to go on screen and the car turns that way.
enum SteeringMode { arrows, stick }

/// Isometric camera zoom.
enum CameraView { close, wide }

/// Saved player data (money, garage, settings, stats, records, championship, achievements).
/// Backed by SharedPreferences; call [Profile.load] once at startup.
class Profile {
  static late SharedPreferences _prefs;
  static void Function()? onAchievementCheck;

  static Future<void> load() async => _prefs = await SharedPreferences.getInstance();

  /// For tests: use an already-created (mock) instance.
  static void useInstance(SharedPreferences prefs) => _prefs = prefs;

  static int _int(String k, [int d = 0]) => _prefs.getInt(k) ?? d;
  static void _setInt(String k, int v) => _prefs.setInt(k, v);

  // ---------- Money ----------
  static int get money => _int('money');
  static set money(int v) {
    _setInt('money', v < 0 ? 0 : v);
    onAchievementCheck?.call();
  }

  static bool trySpend(int amount) {
    if (amount < 0 || money < amount) return false;
    money -= amount;
    return true;
  }

  // ---------- Garage ----------
  static bool owns(VehicleDef v) => v.price == 0 || (_prefs.getBool('own_${v.id}') ?? false);

  static VehicleDef get selected {
    final v = vehicleById(_prefs.getString('selected') ?? vehicles.first.id);
    return owns(v) ? v : vehicles.first;
  }

  static void select(VehicleDef v) {
    if (owns(v)) _prefs.setString('selected', v.id);
  }

  static bool buy(VehicleDef v) {
    if (owns(v) || !trySpend(v.price)) return false;
    _prefs.setBool('own_${v.id}', true);
    addStat(Stat.vehiclesBought);
    select(v);
    return true;
  }

  static int level(VehicleDef v, UpgradeType t) => _int('upg_${v.id}_${t.name}');
  static List<int> levels(VehicleDef v) => [for (final t in UpgradeType.values) level(v, t)];

  static bool upgrade(VehicleDef v, UpgradeType t) {
    if (!owns(v)) return false;
    final price = upgradePrice(t, level(v, t));
    if (price < 0 || !trySpend(price)) return false;
    _setInt('upg_${v.id}_${t.name}', level(v, t) + 1);
    addStat(Stat.upgradesBought);
    return true;
  }

  static CarStats statsFor(VehicleDef v) => computeStats(v, levels(v));

  // ---------- Settings ----------
  static SteeringMode get steering => SteeringMode.values[_int('touchSteering', 0).clamp(0, SteeringMode.values.length - 1)];
  static set steering(SteeringMode m) => _setInt('touchSteering', m.index);
  static CameraView get camera => CameraView.values[_int('camera', 0).clamp(0, CameraView.values.length - 1)];
  static set camera(CameraView c) => _setInt('camera', c.index);
  static double get volume => _prefs.getDouble('volume') ?? 0.8;
  static set volume(double v) => _prefs.setDouble('volume', v);
  /// Touch buttons: on by default on phones/tablets (also mobile browsers), off on desktop.
  static bool get showTouch =>
      _prefs.getBool('showTouch') ??
      (defaultTargetPlatform == TargetPlatform.android || defaultTargetPlatform == TargetPlatform.iOS);
  static set showTouch(bool v) => _prefs.setBool('showTouch', v);
  /// Frames-per-second counter in races (to check how smoothly a device runs the game).
  static bool get showFps => _prefs.getBool('showFps') ?? false;
  static set showFps(bool v) => _prefs.setBool('showFps', v);
  static int get quickTrack => _int('q_track');
  static set quickTrack(int v) => _setInt('q_track', v);
  static int get quickLaps => _int('q_laps', 3);
  static set quickLaps(int v) => _setInt('q_laps', v);
  static int get quickOpponents => _int('q_opp', 5);
  static set quickOpponents(int v) => _setInt('q_opp', v);
  static int get quickDifficulty => _int('q_diff', 1);
  static set quickDifficulty(int v) => _setInt('q_diff', v);

  // ---------- Stats & records ----------
  static int stat(Stat s) => _int('stat_${s.name}');
  static void setStat(Stat s, int v) {
    if (stat(s) == v) return;
    _setInt('stat_${s.name}', v);
    onAchievementCheck?.call();
  }

  static void addStat(Stat s, [int n = 1]) => setStat(s, stat(s) + n);
  static void maxStat(Stat s, int v) {
    if (v > stat(s)) setStat(s, v);
  }

  static int bitCount(Stat s) {
    var v = stat(s), n = 0;
    while (v != 0) {
      n += v & 1;
      v >>= 1;
    }
    return n;
  }

  static double bestLap(String track) => _prefs.getDouble('pb_lap_$track') ?? 0;

  /// Returns true for a new record.
  static bool submitLap(String track, double time) {
    if (time <= 1) return false;
    final best = bestLap(track);
    if (best > 0 && time >= best) return false;
    _prefs.setDouble('pb_lap_$track', time);
    return true;
  }

  // ---------- Achievements ----------
  static bool achieved(String id) => _prefs.getBool('ach_$id') ?? false;
  static void setAchieved(String id) => _prefs.setBool('ach_$id', true);

  // ---------- Championship ----------
  static int get champUnlocked => _int('champ_unlocked');
  static set champUnlocked(int v) => _setInt('champ_unlocked', v);
  static bool champWon(int league) => _prefs.getBool('champ_won_$league') ?? false;
  static void setChampWon(int league) => _prefs.setBool('champ_won_$league', true);
  static int champBest(int league) => _int('champ_best_$league');
  static void setChampBest(int league, int place) => _setInt('champ_best_$league', place);
  static int get champActive => _int('champ_active', -1);
  static set champActive(int v) => _setInt('champ_active', v);
  static int get champNextRace => _int('champ_race');
  static set champNextRace(int v) => _setInt('champ_race', v);

  static Map<String, int> get champPoints {
    final raw = _prefs.getString('champ_points');
    if (raw == null) return {};
    return (jsonDecode(raw) as Map<String, dynamic>).map((k, v) => MapEntry(k, v as int));
  }

  static set champPoints(Map<String, int> p) => _prefs.setString('champ_points', jsonEncode(p));
  static int champResult(int race) => _int('champ_result_$race');
  static int champResultPoints(int race) => _int('champ_respts_$race');
  static void setChampResult(int race, int place, int points) {
    _setInt('champ_result_$race', place);
    _setInt('champ_respts_$race', points);
  }

  static void clearSeason() {
    _prefs.remove('champ_active');
    _prefs.remove('champ_race');
    _prefs.remove('champ_points');
    for (int i = 0; i < 8; i++) {
      _prefs.remove('champ_result_$i');
      _prefs.remove('champ_respts_$i');
    }
  }

  static Future<void> resetAll() => _prefs.clear();
}
