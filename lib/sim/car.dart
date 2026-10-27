import 'dart:math' as math;
import 'dart:ui' show Color;

import 'package:vector_math/vector_math.dart';

import '../data/vehicles.dart';

/// One frame of driving commands. Buttons are "pressed this frame".
class CarInput {
  double throttle = 0; // -1 brake/reverse .. 1 gas
  double steer = 0; // -1 left .. 1 right
  bool fire = false, mine = false, boost = false;

  void clear() {
    throttle = 0;
    steer = 0;
    fire = mine = boost = false;
  }
}

/// Anything that can drive a car: the player, the AI.
abstract class Driver {
  void drive(Car car, CarInput out, double dt);
}

/// A racer: arcade physics state, armor, ammo, drift charge and lap progress.
class Car {
  static const double radius = 1.5; // collision circle
  static const double halfWidth = 0.95;
  static const List<double> driftTierTimes = [0.6, 1.4, 2.4];
  static const List<double> miniTurboTimes = [0.4, 0.7, 1.0];

  final String name;
  final Color color;
  final BodyStyle body;
  final bool isPlayer;
  Driver driver;

  // Tuning
  double maxSpeed, acceleration, turnSpeed, grip;
  double reverseSpeed = 12, brakeForce = 40, coastDeceleration = 5, boostMultiplier = 1.5;
  double speedMultiplier = 1, catchUp = 1;

  // Physics state (metres; angle 0 = facing up the screen, clockwise positive)
  final Vector2 position;
  final Vector2 velocity = Vector2.zero();
  double angle;
  double forwardSpeed = 0, lateralSpeed = 0;

  // Smooth drawing on displays faster than the 60 Hz simulation (e.g. 120 Hz phones): the pose is
  // drawn part of the way between the previous step and the current one.
  final Vector2 _prevPosition = Vector2.zero(), _drawPosition = Vector2.zero();
  double _prevAngle = 0, _drawAngle = 0;
  bool _hasPrev = false, _interpolated = false;

  /// Position / angle to draw (the physics pose until [interpolate] is used).
  Vector2 get renderPosition => _interpolated ? _drawPosition : position;
  double get renderAngle => _interpolated ? _drawAngle : angle;

  /// Remembers the pose before a simulation step.
  void savePose() {
    _prevPosition.setFrom(position);
    _prevAngle = angle;
    _hasPrev = true;
  }

  /// Sets the drawn pose [alpha] (0..1) of the way from the previous step to the current one.
  void interpolate(double alpha) {
    _interpolated = true;
    // No sweep across the track after a respawn teleport.
    if (!_hasPrev || _prevPosition.distanceToSquared(position) > 64) {
      _drawPosition.setFrom(position);
      _drawAngle = angle;
      return;
    }
    _drawPosition
      ..setFrom(_prevPosition)
      ..add((position - _prevPosition)..scale(alpha));
    var d = (angle - _prevAngle) % (2 * math.pi);
    if (d > math.pi) d -= 2 * math.pi;
    _drawAngle = _prevAngle + d * alpha;
  }
  final CarInput input = CarInput();
  bool inputEnabled = false;

  // Combat
  double maxArmor, armor;
  bool wrecked = false;
  double respawnTimer = 0, invulnerable = 0;
  int missilesPerLap, minesPerLap, boostsPerLap;
  int missiles, mines, boosts;
  double missileDamage, boostDuration;
  double fireCooldown = 0, boostTimer = 0;
  Car? lastAttacker;

  // Drift
  double driftTime = 0, driftGrace = 0;
  int driftTier = 0;
  bool drifting = false;

  // Progress
  int lap = 0, index = 0, highestLap = 0;
  double distanceAlong = 0, wrongWayTimer = 0;
  bool wrongWay = false;
  bool finished = false;
  int finishPlace = 0;
  double finishTime = 0;
  int kills = 0, money = 0, timesWrecked = 0;
  /// Coins picked up on the track: each one adds 1% top speed (up to [maxCoins]).
  int coins = 0;
  static const int maxCoins = 10;
  bool wasLast = false;

  // AI tuning (unused for the player)
  double skill = 0.7, aggression = 0.6;

  Car({
    required this.name,
    required this.color,
    required this.isPlayer,
    required this.driver,
    required CarStats stats,
    required this.position,
    required this.angle,
    this.body = BodyStyle.truck,
  })  : maxSpeed = stats.topSpeed,
        acceleration = stats.acceleration,
        turnSpeed = stats.turnSpeed,
        grip = stats.grip,
        maxArmor = stats.armor,
        armor = stats.armor,
        missilesPerLap = stats.missiles,
        minesPerLap = stats.mines,
        boostsPerLap = stats.boosts,
        missiles = stats.missiles,
        mines = stats.mines,
        boosts = stats.boosts,
        missileDamage = stats.missileDamage,
        boostDuration = stats.boostDuration;

  Vector2 get forward => Vector2(math.sin(angle), -math.cos(angle));
  Vector2 get right => Vector2(math.cos(angle), math.sin(angle));
  double get speedKmh => forwardSpeed.abs() * 3.6;
  bool get boosting => boostTimer > 0;
  bool get active => inputEnabled && !wrecked;
  double get armor01 => (armor / maxArmor).clamp(0.0, 1.0);

  void refill() {
    missiles = missilesPerLap;
    mines = minesPerLap;
    boosts = boostsPerLap;
  }

  void addAmmo({int missiles = 0, int mines = 0, int boosts = 0}) {
    this.missiles = math.min(this.missiles + missiles, missilesPerLap + 2);
    this.mines = math.min(this.mines + mines, minesPerLap + 2);
    this.boosts = math.min(this.boosts + boosts, boostsPerLap + 2);
  }

  void startBoost(double seconds) => boostTimer = math.max(boostTimer, seconds);

  /// Arcade physics step. Returns the mini-turbo tier released this step (0 = none).
  int step(double dt) {
    if (boostTimer > 0) boostTimer -= dt;
    if (fireCooldown > 0) fireCooldown -= dt;
    if (invulnerable > 0) invulnerable -= dt;
    if (wrecked) return 0;

    final fwd = forward, rgt = right;
    forwardSpeed = velocity.dot(fwd);
    lateralSpeed = velocity.dot(rgt);

    double throttle = input.throttle;
    final boost = boosting ? boostMultiplier : 1.0;
    if (boosting) throttle = 1;
    final topSpeed = maxSpeed * speedMultiplier * catchUp * boost * (1 + coins * 0.01);

    // Longitudinal
    double dv = 0;
    if (throttle > 0.01) {
      if (forwardSpeed < -0.5) {
        dv = brakeForce * throttle * dt;
      } else if (forwardSpeed < topSpeed) {
        dv = math.min(acceleration * boost * throttle * dt, topSpeed - forwardSpeed);
      } else {
        dv = -math.min(coastDeceleration * 2 * dt, forwardSpeed - topSpeed);
      }
    } else if (throttle < -0.01) {
      if (forwardSpeed > 0.5) {
        dv = brakeForce * throttle * dt;
      } else if (forwardSpeed > -reverseSpeed) {
        dv = acceleration * 0.6 * throttle * dt;
      }
    } else {
      dv = -forwardSpeed.sign * math.min(coastDeceleration * dt, forwardSpeed.abs());
    }
    velocity.addScaled(fwd, dv);

    // Lateral grip (lower grip = more drift)
    velocity.addScaled(rgt, -lateralSpeed * (grip * dt).clamp(0.0, 1.0));

    final turbo = _updateDrift(dt);

    // Steering
    final speedFactor = (forwardSpeed.abs() / 8).clamp(0.0, 1.0);
    final dir = forwardSpeed >= -0.1 ? 1.0 : -1.0;
    angle += input.steer * turnSpeed * (math.pi / 180) * speedFactor * dir * dt;

    position.addScaled(velocity, dt);
    return turbo;
  }

  int _updateDrift(double dt) {
    int released = 0;
    final sliding = forwardSpeed > 12 && input.steer.abs() > 0.6 && lateralSpeed.abs() > 4;
    if (sliding) {
      driftGrace = 0.15;
      driftTime += dt;
    } else if (driftGrace > 0) {
      driftGrace -= dt;
    } else if (driftTime > 0) {
      if (driftTier > 0) {
        startBoost(miniTurboTimes[driftTier - 1]);
        released = driftTier;
      }
      driftTime = 0;
    }
    drifting = driftTime > 0;
    int tier = 0;
    for (int i = 0; i < driftTierTimes.length; i++) {
      if (driftTime >= driftTierTimes[i]) tier = i + 1;
    }
    driftTier = tier;
    return released;
  }

  void resetTo(Vector2 pos, double newAngle) {
    position.setFrom(pos);
    velocity.setZero();
    angle = newAngle;
    boostTimer = 0;
    driftTime = 0;
    driftTier = 0;
    drifting = false;
  }
}
