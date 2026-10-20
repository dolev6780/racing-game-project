import 'package:flutter_test/flutter_test.dart';
import 'package:riot_racers/data/vehicles.dart';
import 'package:riot_racers/sim/race.dart';
import 'package:riot_racers/sim/track.dart';

RaceSetup setupFor(TrackDef track, {int laps = 2}) {
  final v = vehicles.first;
  return RaceSetup(track: track, playerVehicle: v, playerStats: computeStats(v, const [0, 0, 0, 0, 0]), laps: laps);
}

/// Runs a race (the AI drives everyone, including the player) until the player finishes.
Race simulate(TrackDef track, {int laps = 2, int seed = 1, double maxSeconds = 400}) {
  final race = Race(setupFor(track, laps: laps), seed: seed);
  double t = 0;
  while (race.state != RaceState.finished && t < maxSeconds) {
    race.update(1 / 30);
    t += 1 / 30;
  }
  return race;
}

void main() {
  group('tracks', () {
    for (final track in tracks) {
      test('${track.name} samples into a sensible loop', () {
        final path = TrackPath.fromLayout(track.layout);
        expect(path.length, greaterThan(400));
        expect(path.count, greaterThan(100));
      });
    }
  });

  group('full races', () {
    for (final track in tracks) {
      test('a 2-lap race on ${track.name} finishes and every car drives', () {
        final race = simulate(track);
        expect(race.state, RaceState.finished, reason: 'player (AI-driven) never finished');
        expect(race.player.finishPlace, inInclusiveRange(1, race.cars.length));
        for (final c in race.cars) {
          expect(race.progressOf(c), greaterThan(race.path.length * 1.2), reason: '${c.name} barely moved');
          // Nobody escapes through the walls.
          final lat = race.path.lateralOffset(c.position, race.path.findClosest(c.position)).abs();
          expect(lat, lessThan(TrackPath.roadWidth), reason: '${c.name} is off the track');
        }
        expect(race.earnedTotal, greaterThanOrEqualTo(0));
      });
    }
  });

  test('combat happens: missiles, mines and pickups are used', () {
    final race = Race(setupFor(tracks[0], laps: 3), seed: 7);
    var fired = 0, mines = 0, pickups = 0, explosions = 0;
    for (int i = 0; i < 30 * 200 && race.state != RaceState.finished; i++) {
      race.update(1 / 30);
      for (final e in race.events) {
        switch (e.type) {
          case RaceEventType.missileFired:
            fired++;
          case RaceEventType.mineDropped:
            mines++;
          case RaceEventType.pickup:
            pickups++;
          case RaceEventType.explosion:
            explosions++;
          default:
        }
      }
      race.events.clear();
    }
    expect(fired, greaterThan(0));
    expect(mines, greaterThan(0));
    expect(pickups, greaterThan(0));
    expect(explosions, greaterThan(0));
  });

  test('upgrades improve stats', () {
    final v = vehicles.first;
    final stock = computeStats(v, const [0, 0, 0, 0, 0]);
    final maxed = computeStats(v, const [4, 4, 4, 4, 4]);
    expect(maxed.topSpeed, greaterThan(stock.topSpeed));
    expect(maxed.armor, greaterThan(stock.armor));
    expect(maxed.missiles, greaterThan(stock.missiles));
    expect(upgradePrice(UpgradeType.engine, maxUpgradeLevel), -1);
  });
}
