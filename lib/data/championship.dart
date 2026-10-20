import '../sim/race.dart';
import '../sim/track.dart';
import 'profile.dart';

class LeagueDef {
  final String name;
  final List<int> tracks;
  final int laps, opponents, winBonus;
  final double prizeMultiplier, aiTopSpeed, aiArmor;
  final List<double> aiSpeed, aiSkill, aiAggression;

  const LeagueDef(this.name, this.tracks, this.laps, this.prizeMultiplier, this.winBonus, this.aiTopSpeed, this.aiArmor,
      this.aiSpeed, this.aiSkill, this.aiAggression,
      {this.opponents = 5});
}

class Standing {
  final String name;
  final int points;
  bool get isPlayer => name == Championship.playerName;
  const Standing(this.name, this.points);
}

/// Four leagues of increasing difficulty; each season is 4 races scored 10-7-5-3-2-1.
/// Win a league to unlock the next one. Progress is saved in [Profile].
class Championship {
  static const String playerName = 'You';
  static const List<int> pointsTable = [10, 7, 5, 3, 2, 1];

  static const List<LeagueDef> leagues = [
    LeagueDef('ROOKIE LEAGUE', [0, 1, 2, 3], 3, 1, 3000, 30, 90, [0.86, 0.94], [0.3, 0.6], [0.2, 0.5]),
    LeagueDef('PRO LEAGUE', [1, 3, 0, 2], 4, 2, 6000, 33, 110, [0.92, 1.0], [0.5, 0.8], [0.4, 0.7]),
    LeagueDef('ELITE LEAGUE', [2, 0, 3, 1], 4, 3.5, 10000, 36, 130, [0.95, 1.03], [0.65, 0.95], [0.55, 0.9]),
    LeagueDef('RIOT MASTERS', [3, 2, 1, 0], 5, 5, 20000, 39, 150, [0.98, 1.06], [0.8, 1.0], [0.7, 1.0]),
  ];

  static int get unlocked => Profile.champUnlocked.clamp(0, leagues.length - 1);
  static int get activeLeague => Profile.champActive;
  static bool get hasActiveSeason => activeLeague >= 0;
  static LeagueDef? get active => hasActiveSeason ? leagues[activeLeague] : null;
  static int get nextRace => Profile.champNextRace;
  static bool get seasonComplete => hasActiveSeason && nextRace >= active!.tracks.length;

  static int pointsFor(int place) => place >= 1 && place <= pointsTable.length ? pointsTable[place - 1] : 0;

  static void startSeason(int league) {
    if (league > unlocked) return;
    Profile.clearSeason();
    Profile.champActive = league;
    Profile.champNextRace = 0;
    Profile.champPoints = {
      playerName: 0,
      for (int i = 0; i < leagues[league].opponents; i++) Race.opponentRoster[i % Race.opponentRoster.length].$1: 0,
    };
  }

  static void abandon() => Profile.clearSeason();

  static List<Standing> standings() {
    final list = Profile.champPoints.entries.map((e) => Standing(e.key, e.value)).toList()
      ..sort((a, b) {
        final c = b.points.compareTo(a.points);
        if (c != 0) return c;
        return (b.isPlayer ? 1 : 0).compareTo(a.isPlayer ? 1 : 0); // ties go to the player
      });
    return list;
  }

  static int playerSeasonPosition() {
    final s = standings();
    final i = s.indexWhere((e) => e.isPlayer);
    return i < 0 ? s.length + 1 : i + 1;
  }

  /// Records a finished race (names in finishing order). Returns the player's points.
  static int recordRace(int raceIndex, List<String> order) {
    if (!hasActiveSeason || raceIndex != nextRace) return 0;
    final points = Profile.champPoints;
    int playerPoints = 0;
    for (int i = 0; i < order.length; i++) {
      final pts = pointsFor(i + 1);
      points[order[i]] = (points[order[i]] ?? 0) + pts;
      if (order[i] == playerName) {
        playerPoints = pts;
        Profile.setChampResult(raceIndex, i + 1, pts);
      }
    }
    Profile.champPoints = points;
    Profile.champNextRace = raceIndex + 1;
    return playerPoints;
  }

  static int seasonBonus(int league, int position) {
    final bonus = leagues[league].winBonus;
    return position == 1 ? bonus : position == 2 ? bonus * 2 ~/ 5 : position == 3 ? bonus ~/ 5 : 0;
  }

  /// Pays the bonus, records the best finish, unlocks the next league on a win, closes the season.
  static ({int position, int bonus, bool unlockedNext}) finishSeason() {
    if (!seasonComplete) return (position: 0, bonus: 0, unlockedNext: false);
    final league = activeLeague;
    final position = playerSeasonPosition();
    final bonus = seasonBonus(league, position);
    Profile.money += bonus;
    final best = Profile.champBest(league);
    if (best == 0 || position < best) Profile.setChampBest(league, position);
    var unlockedNext = false;
    if (position == 1) {
      Profile.setChampWon(league);
      if (league + 1 < leagues.length && unlocked < league + 1) {
        Profile.champUnlocked = league + 1;
        unlockedNext = true;
      }
    }
    Profile.clearSeason();
    Profile.onAchievementCheck?.call();
    return (position: position, bonus: bonus, unlockedNext: unlockedNext);
  }

  static RaceSetup? nextRaceSetup() {
    final l = active;
    if (l == null || seasonComplete) return null;
    final v = Profile.selected;
    return RaceSetup(
      track: tracks[l.tracks[nextRace]],
      playerVehicle: v,
      playerStats: Profile.statsFor(v),
      laps: l.laps,
      opponents: l.opponents,
      aiTopSpeed: l.aiTopSpeed,
      aiArmor: l.aiArmor,
      aiSpeedMultiplier: l.aiSpeed,
      aiSkill: l.aiSkill,
      aiAggression: l.aiAggression,
      prizeMultiplier: l.prizeMultiplier,
      label: l.name,
      championshipRace: nextRace,
    );
  }
}

/// Quick race setup from the menu choices.
RaceSetup quickRaceSetup() {
  final v = Profile.selected;
  final diff = Profile.quickDifficulty.clamp(0, 2);
  return RaceSetup(
    track: tracks[Profile.quickTrack.clamp(0, tracks.length - 1)],
    playerVehicle: v,
    playerStats: Profile.statsFor(v),
    laps: Profile.quickLaps,
    opponents: Profile.quickOpponents,
    aiArmor: diff == 2 ? 120 : 100,
    aiSpeedMultiplier: const [[0.82, 0.9], [0.9, 0.98], [0.97, 1.04]][diff],
    aiSkill: const [[0.25, 0.55], [0.45, 0.95], [0.75, 1.0]][diff],
    aiAggression: const [[0.15, 0.45], [0.3, 0.9], [0.6, 1.0]][diff],
    prizeMultiplier: const [0.75, 1.0, 1.5][diff],
    label: const ['EASY', 'NORMAL', 'HARD'][diff],
  );
}
