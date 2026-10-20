import 'package:flutter/foundation.dart';

import '../sim/track.dart';
import 'championship.dart';
import 'profile.dart';
import 'vehicles.dart';

class AchievementDef {
  final String id, name, description, category;
  final int reward;
  final (int, int) Function()? progress; // (current, target) for counters
  final bool Function()? condition; // checked automatically; null + null = unlocked by race events

  const AchievementDef(this.id, this.name, this.description, this.reward, this.category, {this.progress, this.condition});
}

AchievementDef _counter(String id, String name, String desc, int reward, String cat, Stat stat, int target) =>
    AchievementDef(id, name, desc, reward, cat, progress: () => (Profile.stat(stat).clamp(0, target), target));

AchievementDef _event(String id, String name, String desc, int reward, String cat) => AchievementDef(id, name, desc, reward, cat);

AchievementDef _league(String id, String name, String desc, int reward, int league) =>
    AchievementDef(id, name, desc, reward, 'Championship', condition: () => Profile.champWon(league));

/// 26 achievements with cash rewards (same set as the Unity version).
class Achievements {
  /// The most recently unlocked achievement (the app shows a toast when it changes).
  static final ValueNotifier<AchievementDef?> lastUnlocked = ValueNotifier(null);

  static final List<AchievementDef> all = [
    _counter('first_race', 'ROLLING THUNDER', 'Finish your first race', 200, 'Racing', Stat.races, 1),
    _counter('first_win', 'WINNER', 'Win a race', 500, 'Racing', Stat.wins, 1),
    _counter('wins_10', 'SPEED KING', 'Win 10 races', 2000, 'Racing', Stat.wins, 10),
    _counter('podiums_25', 'PODIUM REGULAR', 'Finish in the top 3 twenty-five times', 2000, 'Racing', Stat.podiums, 25),
    _counter('streak_3', 'HAT TRICK', 'Win 3 races in a row', 1500, 'Racing', Stat.bestWinStreak, 3),
    _event('comeback', 'COMEBACK KID', 'Win a race after being in last place', 1500, 'Racing'),
    _event('flawless', 'UNTOUCHABLE', 'Win a race without getting wrecked', 1000, 'Racing'),
    _counter('speed_150', 'TON FIFTY', 'Reach 150 km/h', 500, 'Racing', Stat.topSpeedKmh, 150),
    _counter('speed_180', 'LUDICROUS SPEED', 'Reach 180 km/h', 1500, 'Racing', Stat.topSpeedKmh, 180),
    AchievementDef('all_tracks', 'WORLD TOUR', 'Race on every track', 800, 'Racing',
        progress: () => (Profile.bitCount(Stat.tracksRaced), tracks.length)),
    _counter('first_wreck', 'SCRAP METAL', 'Wreck an opponent', 300, 'Combat', Stat.wrecks, 1),
    _counter('wrecks_50', 'DEMOLITION CREW', 'Wreck 50 opponents', 2500, 'Combat', Stat.wrecks, 50),
    _event('rampage', 'RAMPAGE', 'Wreck 3 opponents in one race', 1000, 'Combat'),
    _counter('missiles_100', 'TRIGGER HAPPY', 'Fire 100 missiles', 800, 'Combat', Stat.missilesFired, 100),
    _counter('mine_hits_10', 'MINESWEEPER', 'Hit opponents with your mines 10 times', 800, 'Combat', Stat.mineHits, 10),
    _counter('nitro_50', 'NITRO JUNKIE', 'Use nitro 50 times', 600, 'Combat', Stat.nitrosUsed, 50),
    _counter('pickups_50', 'MONEY GRABBER', 'Collect 50 cash pickups', 800, 'Career', Stat.moneyPickups, 50),
    _counter('first_upgrade', 'GREASE MONKEY', 'Buy your first upgrade', 300, 'Career', Stat.upgradesBought, 1),
    AchievementDef('max_upgrade', 'MAXED OUT', 'Max out any upgrade', 1000, 'Career', condition: () {
      for (final v in vehicles) {
        if (!Profile.owns(v)) continue;
        if (Profile.levels(v).any((l) => l >= maxUpgradeLevel)) return true;
      }
      return false;
    }),
    _counter('new_ride', 'NEW RIDE', 'Buy a new vehicle', 500, 'Career', Stat.vehiclesBought, 1),
    AchievementDef('collector', 'COLLECTOR', 'Own every vehicle', 5000, 'Career',
        progress: () => (vehicles.where(Profile.owns).length, vehicles.length)),
    AchievementDef('rich', 'HIGH ROLLER', r'Have $25,000 in the bank', 2500, 'Career',
        progress: () => (Profile.money.clamp(0, 25000), 25000)),
    _league('champ_0', 'ROOKIE CHAMPION', 'Win the Rookie League', 1000, 0),
    _league('champ_1', 'PRO CHAMPION', 'Win the Pro League', 2000, 1),
    _league('champ_2', 'ELITE CHAMPION', 'Win the Elite League', 4000, 2),
    _league('champ_3', 'RIOT LEGEND', 'Win Riot Masters', 10000, 3),
  ];

  static int get unlockedCount => all.where((a) => Profile.achieved(a.id)).length;

  static bool isComplete(AchievementDef a) {
    if (a.condition != null) return a.condition!();
    if (a.progress != null) {
      final (cur, target) = a.progress!();
      return cur >= target;
    }
    return false;
  }

  static bool _checking = false;

  /// Unlocks every automatic achievement whose condition is now met.
  static void checkAll() {
    if (_checking) return; // rewards change money, which triggers another check
    _checking = true;
    try {
      for (final a in all) {
        if (!Profile.achieved(a.id) && isComplete(a)) unlock(a.id);
      }
    } finally {
      _checking = false;
    }
  }

  static void unlock(String id) {
    final a = all.firstWhere((x) => x.id == id);
    if (Profile.achieved(id)) return;
    Profile.setAchieved(id);
    Profile.money += a.reward;
    lastUnlocked.value = a;
  }

  static void init() => Profile.onAchievementCheck = checkAll;

  // Keep the championship import used for league conditions.
  static int get leagueCount => Championship.leagues.length;
}
