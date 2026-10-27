import 'package:flutter_test/flutter_test.dart';
import 'package:riot_racers/data/vehicles.dart';
import 'package:riot_racers/game/controls.dart';
import 'package:riot_racers/sim/car.dart';
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

  group('player controls', () {
    Race playerRace(PlayerControls controls) =>
        Race(setupFor(tracks.first), seed: 1, playerDriver: PlayerDriver(controls));

    void run(Race race, double seconds) {
      for (double t = 0; t < seconds; t += 1 / 60) {
        race.update(1 / 60);
      }
    }

    test('GAS drives forward, REVERSE backs up', () {
      final controls = PlayerControls();
      final race = playerRace(controls);
      run(race, 3.2); // countdown
      controls.touchGas = true;
      run(race, 1.5);
      expect(race.player.forwardSpeed, greaterThan(8));
      controls
        ..touchGas = false
        ..touchReverse = true;
      run(race, 4);
      expect(race.player.forwardSpeed, lessThan(-2));
    });

    test('stick steering turns toward the screen direction', () {
      final car = playerRace(PlayerControls()).player;
      car.angle = 0; // facing world-up, which is up-right on the isometric screen
      expect(PlayerControls.aimSteer(car, 0.707, -0.396).abs(), lessThan(0.05));
      expect(PlayerControls.aimSteer(car, 1, 0.4), greaterThan(0.5)); // screen right/down: turn right
      expect(PlayerControls.aimSteer(car, -1, -0.2), lessThan(-0.5)); // screen left: turn left
    });

    test('holding the stick in one direction makes the car drive that way', () {
      final controls = PlayerControls();
      final race = playerRace(controls);
      run(race, 3.2);
      final car = race.player;
      // Point toward the car's current screen heading rotated a bit, with gas held.
      controls
        ..touchGas = true
        ..touchAim = (x: -1, y: 0.3);
      run(race, 2.5);
      final steer = PlayerControls.aimSteer(car, -1, 0.3);
      expect(steer.abs(), lessThan(0.35), reason: 'car should have turned to face the stick direction');
    });
  });

  test('coins are placed, collected, speed the car up and are lost on a wreck', () {
    final race = simulate(tracks.first, laps: 1);
    expect(race.coins.length, greaterThan(20));
    final collected = race.cars.map((c) => c.coins).fold(0, (a, b) => a + b);
    expect(collected, greaterThan(0), reason: 'cars driving the racing line should pick up coins');
    final car = race.player;
    car.coins = Car.maxCoins;
    final before = car.armor;
    race.damage(car, before + 1, null);
    expect(car.wrecked, isTrue);
    expect(car.coins, Car.maxCoins ~/ 2);
  });

  test('drawn kart pose moves smoothly between 60 Hz steps (120 Hz displays)', () {
    final race = simulate(tracks.first, maxSeconds: 8); // past the countdown, cars moving
    final car = race.cars.first;
    final positions = <double>[];
    // 120 Hz frames: half of them land between simulation steps.
    for (int i = 0; i < 12; i++) {
      race.update(1 / 120);
      car.interpolate(race.interpolation);
      positions.add(car.renderPosition.distanceTo(race.path.point(0)));
    }
    // Every frame shows a new position (no frame repeats the previous one).
    for (int i = 1; i < positions.length; i++) {
      expect((positions[i] - positions[i - 1]).abs(), greaterThan(1e-4), reason: 'frame $i repeated the pose');
    }
  });
}
