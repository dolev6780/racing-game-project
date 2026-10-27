import 'dart:math' as math;
import 'dart:ui' show Color;

import 'package:vector_math/vector_math.dart';

import '../data/vehicles.dart';
import 'ai.dart';
import 'car.dart';
import 'track.dart';

enum RaceState { countdown, racing, finished }

enum RaceEventType { explosion, hit, wreck, pickup, lap, miniTurbo, boostPad, missileFired, mineDropped, mineHit, nitro, finish, coin }

/// Something that happened this frame, for effects, sounds and the HUD.
class RaceEvent {
  final RaceEventType type;
  final Vector2 position;
  final Car? car, other;
  final String text;
  final double value;

  RaceEvent(this.type, this.position, {this.car, this.other, this.text = '', this.value = 0});
}

/// Everything needed to set up one race (quick race or championship).
class RaceSetup {
  final TrackDef track;
  final int laps, opponents;
  final VehicleDef playerVehicle;
  final CarStats playerStats;
  final double aiTopSpeed, aiArmor, prizeMultiplier;
  final List<double> aiSpeedMultiplier, aiSkill, aiAggression; // [min, max]
  final String label;
  final int championshipRace; // -1 = not a championship race

  const RaceSetup({
    required this.track,
    required this.playerVehicle,
    required this.playerStats,
    this.laps = 3,
    this.opponents = 5,
    this.aiTopSpeed = 32,
    this.aiArmor = 100,
    this.aiSpeedMultiplier = const [0.9, 0.98],
    this.aiSkill = const [0.45, 0.95],
    this.aiAggression = const [0.3, 0.9],
    this.prizeMultiplier = 1,
    this.label = 'QUICK RACE',
    this.championshipRace = -1,
  });

  bool get isChampionship => championshipRace >= 0;

  RaceSetup copyWithLaps(int n) => RaceSetup(
        track: track,
        playerVehicle: playerVehicle,
        playerStats: playerStats,
        laps: n,
        opponents: opponents,
        aiTopSpeed: aiTopSpeed,
        aiArmor: aiArmor,
        aiSpeedMultiplier: aiSpeedMultiplier,
        aiSkill: aiSkill,
        aiAggression: aiAggression,
        prizeMultiplier: prizeMultiplier,
        label: label,
        championshipRace: championshipRace,
      );

  RaceSetup copyWithTrack(TrackDef t) => RaceSetup(
        track: t,
        playerVehicle: playerVehicle,
        playerStats: playerStats,
        laps: laps,
        opponents: opponents,
        aiTopSpeed: aiTopSpeed,
        aiArmor: aiArmor,
        aiSpeedMultiplier: aiSpeedMultiplier,
        aiSkill: aiSkill,
        aiAggression: aiAggression,
        prizeMultiplier: prizeMultiplier,
        label: label,
        championshipRace: championshipRace,
      );
}

class Missile {
  final Vector2 position, direction;
  final double speed, damage;
  final Car owner;
  double age = 0;
  bool dead = false;
  Missile(this.position, this.direction, this.speed, this.damage, this.owner);
}

class Mine {
  final Vector2 position;
  final Car owner;
  final double damage;
  double age = 0;
  bool dead = false;
  Mine(this.position, this.owner, this.damage);
}

class ItemBox {
  final Vector2 position;
  double hiddenFor = 0;
  ItemBox(this.position);
}

/// Spinning gold coin on the racing line; respawns a while after being collected.
class Coin {
  static const double respawn = 10, radius = 1.9;
  final Vector2 position;
  double hiddenFor = 0;
  Coin(this.position);
}

class BoostPad {
  static const double length = 7, width = 4.5;
  final Vector2 position, direction;
  BoostPad(this.position, this.direction);
}

/// The whole race simulation (pure Dart, no rendering): fixed 60 Hz steps.
class Race {
  static const double step = 1 / 60;
  static const List<int> prizeMoney = [1000, 600, 350, 150, 75, 0];
  static const int killBonus = 100, pickupMoney = 100, coinMoney = 10;
  static const List<(String, Color)> opponentRoster = [
    ('Rip Torque', Color(0xFFE6332B)),
    ('Vixen', Color(0xFF9959E6)),
    ('Grim Gaskett', Color(0xFF40BF59)),
    ('Nova Blaze', Color(0xFFF24D33)),
    ('Krusher', Color(0xFF8C4DE6)),
    ('Dust Devil', Color(0xFF4DCC66)),
    ('Hex', Color(0xFFE64040)),
  ];
  static const double wallLimit = TrackPath.roadWidth / 2 + 0.4 - Car.halfWidth;

  final RaceSetup setup;
  final TrackPath path;
  final math.Random rng;
  final List<Car> cars = [];
  late final Car player;
  final List<Missile> missiles = [];
  final List<Mine> mines = [];
  final List<ItemBox> itemBoxes = [];
  final List<Coin> coins = [];
  final List<BoostPad> boostPads = [];
  final List<RaceEvent> events = [];

  RaceState state = RaceState.countdown;
  double countdown = 3, time = 0, raceTime = 0;
  bool paused = false;
  List<Car> standings = [];
  int _finishedCount = 0;
  double _accumulator = 0;

  // Payout (filled when the player finishes)
  int earnedPrize = 0, earnedKills = 0, earnedPickups = 0;
  int get earnedTotal => earnedPrize + earnedKills + earnedPickups;

  Race(this.setup, {int seed = 0, Driver? playerDriver})
      : path = TrackPath.fromLayout(setup.track.layout),
        rng = math.Random(seed) {
    _spawnCars(playerDriver);
    _placeItems();
    standings = List.of(cars);
  }

  int get laps => setup.laps;
  int positionOf(Car c) => standings.indexOf(c) + 1;
  double progressOf(Car c) => c.lap * path.length + c.distanceAlong;

  // ---------- Setup ----------

  void _spawnCars(Driver? playerDriver) {
    final total = setup.opponents + 1;
    final playerSlot = math.min(3, total - 1);
    int opp = 0;
    for (int slot = 0; slot < total; slot++) {
      final row = slot ~/ 2;
      final lateral = (slot.isEven ? -1 : 1) * TrackPath.roadWidth * 0.22;
      final behind = 8.0 + row * 8 + (slot % 2) * 3;
      final dir = Vector2.zero();
      final pos = path.pointAtDistance(-behind, dir) + Vector2(-dir.y, dir.x) * lateral;
      final angle = math.atan2(dir.x, -dir.y);

      final isPlayer = slot == playerSlot;
      late Car car;
      if (isPlayer) {
        car = Car(
          name: 'You', color: setup.playerVehicle.color, isPlayer: true, body: setup.playerVehicle.body,
          driver: playerDriver ?? AiDriver(this, rng.nextDouble() * 10), stats: setup.playerStats,
          position: pos, angle: angle,
        );
        player = car;
      } else {
        final (name, color) = opponentRoster[opp % opponentRoster.length];
        opp++;
        car = Car(
          name: name, color: color, isPlayer: false, driver: AiDriver(this, rng.nextDouble() * 10),
          stats: CarStats(
            topSpeed: setup.aiTopSpeed, acceleration: 18, turnSpeed: 130, grip: 6, armor: setup.aiArmor,
            missiles: 3, mines: 2, boosts: 2,
          ),
          position: pos, angle: angle,
        );
        car.skill = _range(setup.aiSkill);
        car.aggression = _range(setup.aiAggression);
        car.speedMultiplier = _range(setup.aiSpeedMultiplier);
      }
      car.index = path.findClosest(car.position);
      car.distanceAlong = path.distanceAlong(car.position, car.index);
      cars.add(car);
    }
  }

  double _range(List<double> r) => r[0] + (r[1] - r[0]) * rng.nextDouble();

  void _placeItems() {
    // Rows of four item boxes across the road at three points of the lap.
    for (final f in const [0.22, 0.55, 0.82]) {
      final dir = Vector2.zero();
      final c = path.pointAtDistance(path.length * f, dir);
      final right = Vector2(-dir.y, dir.x);
      for (int i = 0; i < 4; i++) {
        itemBoxes.add(ItemBox(c + right * ((i - 1.5) * TrackPath.roadWidth * 0.22)));
      }
    }
    // Boost pads on the straightest sections, spread around the lap.
    final n = path.count;
    final chosen = <int>[];
    for (int pick = 0; pick < 3; pick++) {
      int best = -1;
      double bestScore = double.infinity;
      for (int i = 0; i < n; i++) {
        final along = path.distanceAtIndex(i);
        if (along < 40 || along > path.length - 25) continue;
        if (chosen.any((c) => math.min((c - i).abs(), n - (c - i).abs()) < n ~/ 4)) continue;
        final d1 = path.direction(i), d2 = path.direction(i + 14);
        final score = math.acos(d1.dot(d2).clamp(-1.0, 1.0)) * 180 / math.pi;
        if (score < bestScore) {
          bestScore = score;
          best = i;
        }
      }
      if (best < 0 || bestScore > 12) break;
      chosen.add(best);
    }
    for (int k = 0; k < chosen.length; k++) {
      final i = chosen[k];
      boostPads.add(BoostPad(path.point(i) + path.right(i) * ((k % 3 - 1) * TrackPath.roadWidth * 0.25), path.direction(i)));
    }
    // Lines of five coins, alternating sides of the road, away from the item box rows and the grid.
    int line = 0;
    for (double f = 0.1; f < 0.97; f += 0.11) {
      if (const [0.22, 0.55, 0.82].any((b) => (f - b).abs() < 0.04)) continue;
      final lateral = (line++ % 3 - 1) * TrackPath.roadWidth * 0.25;
      for (int k = 0; k < 5; k++) {
        final dir = Vector2.zero();
        final c = path.pointAtDistance(path.length * f + k * 2.6, dir);
        final p = c + Vector2(-dir.y, dir.x) * lateral;
        if (boostPads.any((b) => b.position.distanceTo(p) < BoostPad.length)) continue;
        coins.add(Coin(p));
      }
    }
  }

  // ---------- Update ----------

  void update(double dt) {
    if (paused) return;
    _accumulator += math.min(dt, 0.1);
    while (_accumulator >= step) {
      _accumulator -= step;
      for (final c in cars) {
        c.savePose();
      }
      _fixedStep(step);
    }
  }

  /// How far (0..1) the current frame is between the last simulation step and the next one.
  double get interpolation => (_accumulator / step).clamp(0.0, 1.0);

  void _fixedStep(double dt) {
    time += dt;
    if (state == RaceState.countdown) {
      countdown -= dt;
      if (countdown <= 0) {
        state = RaceState.racing;
        for (final c in cars) {
          c.inputEnabled = true;
        }
      }
    } else {
      raceTime += dt;
    }

    for (final c in cars) {
      c.input.clear();
      if (c.active) c.driver.drive(c, c.input, dt);
      _useWeapons(c);
      final turbo = c.step(dt);
      if (turbo > 0) events.add(RaceEvent(RaceEventType.miniTurbo, c.position.clone(), car: c, value: turbo.toDouble()));
      _collideWalls(c);
      _updateProgress(c, dt);
      _updateWreck(c, dt);
    }
    _collideCars();
    _updateMissiles(dt);
    _updateMines(dt);
    _updateItems(dt);
    _checkFinish();
    _updateStandings();
    _updateCatchUp();
  }

  void _useWeapons(Car c) {
    if (!c.active) return;
    if (c.input.fire && c.missiles > 0 && c.fireCooldown <= 0) {
      c.missiles--;
      c.fireCooldown = 0.35;
      // Light aim assist: lock onto the closest car in a 20-degree cone ahead.
      var dir = c.forward;
      double best = 70;
      for (final o in cars) {
        if (identical(o, c) || o.wrecked) continue;
        final to = o.position - c.position;
        final dist = to.length;
        if (dist < best && dist > 0.1 && math.acos((c.forward.dot(to) / dist).clamp(-1.0, 1.0)) < 20 * math.pi / 180) {
          best = dist;
          dir = to / dist;
        }
      }
      final spawn = c.position + c.forward * 2.6;
      missiles.add(Missile(spawn, dir, math.max(c.forwardSpeed, 0) + 45, c.missileDamage, c));
      events.add(RaceEvent(RaceEventType.missileFired, spawn.clone(), car: c));
    }
    if (c.input.mine && c.mines > 0) {
      c.mines--;
      final pos = c.position - c.forward * 2.8;
      mines.add(Mine(pos, c, 35));
      if (mines.length > 30) mines.removeAt(0);
      events.add(RaceEvent(RaceEventType.mineDropped, pos.clone(), car: c));
    }
    if (c.input.boost && c.boosts > 0 && !c.boosting) {
      c.boosts--;
      c.startBoost(c.boostDuration);
      events.add(RaceEvent(RaceEventType.nitro, c.position.clone(), car: c));
    }
  }

  void _collideWalls(Car c) {
    if (c.wrecked) return;
    // Local lookup only: _updateProgress owns c.index (it needs the previous value to count laps).
    final idx = path.findClosestLocal(c.position, c.index);
    final lat = path.lateralOffset(c.position, idx);
    if (lat.abs() <= wallLimit) return;
    final normal = path.right(idx) * lat.sign; // pointing out of the track
    c.position.addScaled(normal, -(lat.abs() - wallLimit));
    final vn = c.velocity.dot(normal);
    if (vn > 0) {
      c.velocity.addScaled(normal, -vn * 1.3); // bounce
      c.velocity.scale(0.97);
      if (vn > 20) damage(c, (vn - 20) * 0.5, null);
      if (vn > 6) events.add(RaceEvent(RaceEventType.hit, c.position.clone(), car: c, value: vn));
    }
  }

  void _collideCars() {
    for (int i = 0; i < cars.length; i++) {
      final a = cars[i];
      if (a.wrecked) continue;
      for (int j = i + 1; j < cars.length; j++) {
        final b = cars[j];
        if (b.wrecked) continue;
        final d = b.position - a.position;
        final dist = d.length;
        const minDist = Car.radius * 2;
        if (dist >= minDist || dist < 1e-4) continue;
        final n = d / dist;
        final overlap = minDist - dist;
        a.position.addScaled(n, -overlap / 2);
        b.position.addScaled(n, overlap / 2);
        final rv = (b.velocity - a.velocity).dot(n);
        if (rv < 0) {
          final j2 = -rv * 0.65; // restitution ~0.3, equal masses
          a.velocity.addScaled(n, -j2);
          b.velocity.addScaled(n, j2);
          final impact = -rv;
          if (impact > 8) {
            damage(a, (impact - 8) * 1.2, b);
            damage(b, (impact - 8) * 1.2, a);
          }
          if (impact > 4) events.add(RaceEvent(RaceEventType.hit, (a.position + b.position) * 0.5, car: a, other: b, value: impact));
        }
      }
    }
  }

  void _updateProgress(Car c, double dt) {
    if (c.wrecked) return;
    final n = path.count;
    final newIndex = path.findClosestLocal(c.position, c.index);
    final delta = newIndex - c.index;
    if (delta < -n ~/ 2) c.lap++;
    if (delta > n ~/ 2) c.lap--;
    c.index = newIndex;
    c.distanceAlong = path.distanceAlong(c.position, c.index);
    if (c.lap > c.highestLap) {
      c.highestLap = c.lap;
      c.refill();
      events.add(RaceEvent(RaceEventType.lap, c.position.clone(), car: c, value: c.lap.toDouble()));
    }
    final along = c.velocity.dot(path.direction(c.index));
    c.wrongWayTimer = along < -3 ? c.wrongWayTimer + dt : 0;
    c.wrongWay = c.wrongWayTimer > 1.2;
  }

  void damage(Car c, double amount, Car? attacker) {
    if (c.wrecked || c.invulnerable > 0 || amount <= 0) return;
    c.armor -= amount;
    if (attacker != null) c.lastAttacker = attacker;
    if (c.armor > 0) return;
    c.armor = 0;
    c.wrecked = true;
    c.respawnTimer = 2;
    c.timesWrecked++;
    c.coins ~/= 2; // wrecks cost half your coins
    c.velocity.setZero();
    if (attacker != null && !identical(attacker, c) && !attacker.finished) attacker.kills++;
    events.add(RaceEvent(RaceEventType.explosion, c.position.clone(), car: c, value: 6));
    events.add(RaceEvent(RaceEventType.wreck, c.position.clone(), car: c, other: attacker));
  }

  void _updateWreck(Car c, double dt) {
    if (!c.wrecked) return;
    c.respawnTimer -= dt;
    if (c.respawnTimer <= 0) {
      respawn(c);
      c.wrecked = false;
      c.armor = c.maxArmor;
      c.invulnerable = 2;
    }
  }

  /// Put a car back on the centre of the track at its current progress point.
  void respawn(Car c) {
    final dir = path.direction(c.index);
    c.resetTo(path.point(c.index), math.atan2(dir.x, -dir.y));
  }

  void _updateMissiles(double dt) {
    for (final m in missiles) {
      m.age += dt;
      m.position.addScaled(m.direction, m.speed * dt);
      if (m.age > 2.5) {
        _explode(m.position, 2);
        m.dead = true;
        continue;
      }
      for (final c in cars) {
        if (identical(c, m.owner) || c.wrecked) continue;
        if (c.position.distanceTo(m.position) < Car.radius + 0.4) {
          damage(c, m.damage, m.owner);
          c.velocity.addScaled(m.direction, 6);
          _explode(m.position, 3.5);
          m.dead = true;
          break;
        }
      }
      if (!m.dead) {
        final idx = path.findClosest(m.position);
        if (path.lateralOffset(m.position, idx).abs() > TrackPath.roadWidth / 2 + 1.2 &&
            path.point(idx).distanceTo(m.position) < TrackPath.roadWidth) {
          _explode(m.position, 2);
          m.dead = true;
        }
      }
    }
    missiles.removeWhere((m) => m.dead);
  }

  void _updateMines(double dt) {
    for (final m in mines) {
      m.age += dt;
      if (m.age > 45) {
        m.dead = true;
        continue;
      }
      if (m.age < 0.5) continue;
      for (final c in cars) {
        if (c.wrecked) continue;
        if (identical(c, m.owner) && m.age < 2) continue;
        if (c.position.distanceTo(m.position) < 1.8 + Car.halfWidth) {
          damage(c, m.damage, m.owner);
          if (!identical(c, m.owner)) events.add(RaceEvent(RaceEventType.mineHit, m.position.clone(), car: m.owner, other: c));
          c.velocity.scale(0.35);
          c.angle += (rng.nextDouble() - 0.5) * 1.2; // spin out
          _explode(m.position, 4);
          m.dead = true;
          break;
        }
      }
    }
    mines.removeWhere((m) => m.dead);
  }

  void _explode(Vector2 pos, double size) => events.add(RaceEvent(RaceEventType.explosion, pos.clone(), value: size));

  void _updateCoins(double dt) {
    for (final coin in coins) {
      if (coin.hiddenFor > 0) {
        coin.hiddenFor -= dt;
        continue;
      }
      for (final c in cars) {
        if (c.wrecked || c.position.distanceTo(coin.position) > Coin.radius) continue;
        coin.hiddenFor = Coin.respawn;
        if (c.coins < Car.maxCoins) c.coins++;
        c.money += coinMoney;
        events.add(RaceEvent(RaceEventType.coin, coin.position.clone(), car: c, value: c.coins.toDouble()));
        break;
      }
    }
  }

  void _updateItems(double dt) {
    _updateCoins(dt);
    for (final box in itemBoxes) {
      if (box.hiddenFor > 0) {
        box.hiddenFor -= dt;
        continue;
      }
      for (final c in cars) {
        if (c.wrecked || c.position.distanceTo(box.position) > 2.2) continue;
        box.hiddenFor = 6;
        final roll = rng.nextInt(5);
        String label;
        switch (roll) {
          case 0:
            c.addAmmo(missiles: 1);
            label = 'MISSILE';
          case 1:
            c.addAmmo(mines: 1);
            label = 'MINE';
          case 2:
            c.addAmmo(boosts: 1);
            label = 'NITRO';
          case 3:
            c.armor = math.min(c.maxArmor, c.armor + 40);
            label = 'ARMOR REPAIR';
          default:
            c.money += pickupMoney;
            label = '+\$$pickupMoney';
        }
        events.add(RaceEvent(RaceEventType.pickup, box.position.clone(), car: c, text: label, value: roll.toDouble()));
        break;
      }
    }
    for (final pad in boostPads) {
      final right = Vector2(-pad.direction.y, pad.direction.x);
      for (final c in cars) {
        if (c.wrecked) continue;
        final d = c.position - pad.position;
        if (d.dot(pad.direction).abs() < BoostPad.length / 2 && d.dot(right).abs() < BoostPad.width / 2 && c.boostTimer < 0.9) {
          c.startBoost(1.1);
          events.add(RaceEvent(RaceEventType.boostPad, c.position.clone(), car: c));
        }
      }
    }
  }

  void _checkFinish() {
    for (final c in cars) {
      if (c.finished || c.lap <= laps) continue;
      c.finished = true;
      c.finishPlace = ++_finishedCount;
      c.finishTime = raceTime;
      if (c.isPlayer) {
        // Hand the car to the AI for a victory lap and pay out.
        c.driver = AiDriver(this, rng.nextDouble() * 10);
        state = RaceState.finished;
        final prize = c.finishPlace - 1 < prizeMoney.length ? prizeMoney[c.finishPlace - 1] : 0;
        earnedPrize = (prize * setup.prizeMultiplier).round();
        earnedKills = c.kills * killBonus;
        earnedPickups = c.money;
        events.add(RaceEvent(RaceEventType.finish, c.position.clone(), car: c, value: c.finishPlace.toDouble()));
      }
    }
  }

  void _updateStandings() {
    standings = List.of(cars)
      ..sort((a, b) {
        if (a.finished != b.finished) return a.finished ? -1 : 1;
        if (a.finished) return a.finishPlace.compareTo(b.finishPlace);
        return progressOf(b).compareTo(progressOf(a));
      });
    if (raceTime > 3 && cars.length > 1 && positionOf(player) == cars.length && !player.finished) player.wasLast = true;
  }

  /// Rubber-banding: AI far behind the player gets up to +7% speed, AI far ahead up to -5%.
  void _updateCatchUp() {
    final pp = progressOf(player);
    for (final c in cars) {
      if (c.isPlayer || c.finished || player.finished) {
        c.catchUp = 1;
        continue;
      }
      final f = ((pp - progressOf(c)) / 150).clamp(-1.0, 1.0);
      c.catchUp = 1 + (f > 0 ? 0.07 * f : 0.05 * f);
    }
  }

  /// Final finishing order (finished cars first, then by current progress).
  List<Car> get finishOrder => standings;
}
