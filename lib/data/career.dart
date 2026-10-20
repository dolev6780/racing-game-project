import '../sim/race.dart';
import '../sim/track.dart';
import 'achievements.dart';
import 'championship.dart';
import 'profile.dart';

/// Result of recording a finished race, shown on the results screen.
class CareerResult {
  final int championshipPoints; // -1 = not a championship race
  const CareerResult(this.championshipPoints);
}

/// Saves everything a finished race changes: money, stats, records, achievements, championship points.
CareerResult recordFinishedRace(Race race) {
  final p = race.player;
  final place = p.finishPlace;
  Profile.money += race.earnedTotal;
  Profile.addStat(Stat.races);
  Profile.addStat(Stat.moneyEarned, race.earnedTotal);
  Profile.setStat(Stat.tracksRaced, Profile.stat(Stat.tracksRaced) | (1 << tracks.indexOf(race.setup.track)));
  if (place <= 3) Profile.addStat(Stat.podiums);
  if (place == 1) {
    Profile.addStat(Stat.wins);
    Profile.addStat(Stat.winStreak);
    Profile.maxStat(Stat.bestWinStreak, Profile.stat(Stat.winStreak));
  } else {
    Profile.setStat(Stat.winStreak, 0);
  }

  if (p.kills >= 3) Achievements.unlock('rampage');
  if (place == 1 && p.wasLast) Achievements.unlock('comeback');
  if (place == 1 && p.timesWrecked == 0) Achievements.unlock('flawless');

  var points = -1;
  if (race.setup.isChampionship) {
    points = Championship.recordRace(race.setup.championshipRace, race.finishOrder.map((c) => c.name).toList());
  }
  Achievements.checkAll();
  return CareerResult(points);
}
