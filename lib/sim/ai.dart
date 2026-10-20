import 'dart:math' as math;

import 'package:vector_math/vector_math.dart';

import 'car.dart';
import 'race.dart';

/// Computer driver: follows the racing line, slows for corners, fires at cars ahead,
/// drops mines on chasers, uses nitro on straights and recovers when stuck.
class AiDriver implements Driver {
  final Race race;
  final double laneSeed;
  double _stuck = 0, _reverseUntil = -1, _nextFire = 0, _nextMine = 0, _nextBoost = 0;
  int _recover = 0;

  AiDriver(this.race, this.laneSeed);

  @override
  void drive(Car car, CarInput out, double dt) {
    final path = race.path;
    final t = race.time;
    final speed = car.forwardSpeed;
    final idx = car.index;

    // Steering toward a look-ahead point with a slowly wandering lane.
    final look = 3 + (speed.abs() * 0.2).round();
    final lane = math.sin(t * 0.35 + laneSeed) * 0.25 * 14;
    final target = path.point(idx + look) + path.right(idx + look) * lane;
    final to = target - car.position;
    final f = car.forward;
    final angle = math.atan2(f.x * to.y - f.y * to.x, f.dot(to)) * 180 / math.pi;
    out.steer = (angle / 25).clamp(-1.0, 1.0);

    // Speed: ease off before sharp corners.
    final corner = _angleBetween(path.direction(idx + look), path.direction(idx + look + 10));
    var cornerSpeed = _lerp(car.maxSpeed, car.maxSpeed * 0.45, ((corner - 20) / 60).clamp(0.0, 1.0));
    cornerSpeed *= _lerp(0.9, 1.05, car.skill);
    out.throttle = speed > cornerSpeed ? -0.4 : 1;
    if (angle.abs() > 70 && speed > 10) out.throttle = -0.6;

    // Stuck recovery.
    if (t < _reverseUntil) {
      out.throttle = -1;
      out.steer = -out.steer;
    } else {
      _stuck = speed.abs() < 1.5 ? _stuck + dt : 0;
      if (_stuck > 1.5) {
        _stuck = 0;
        _recover++;
        if (_recover > 2) {
          race.respawn(car);
          _recover = 0;
        } else {
          _reverseUntil = t + 1.2;
        }
      } else if (speed.abs() > 5) {
        _recover = 0;
      }
    }

    // Weapons (not in the first seconds after the start, so the grid isn't a massacre).
    if (race.raceTime < 4) return;
    if (t > _nextFire && _findCar(car, 40, 0, 12)) {
      out.fire = true;
      _nextFire = t + _lerp(3, 0.8, car.aggression) + race.rng.nextDouble();
    }
    if (t > _nextMine && _findCar(car, 25, 150, 180)) {
      out.mine = true;
      _nextMine = t + _lerp(6, 2, car.aggression) + race.rng.nextDouble() * 2;
    }
    if (t > _nextBoost && corner < 8 && speed > 15 && _angleBetween(path.direction(idx), path.direction(idx + 20)) < 10) {
      out.boost = true;
      _nextBoost = t + 4 + race.rng.nextDouble() * 6;
    }
  }

  bool _findCar(Car me, double range, double minAngle, double maxAngle) {
    for (final other in race.cars) {
      if (identical(other, me) || other.wrecked) continue;
      final d = other.position - me.position;
      if (d.length2 > range * range) continue;
      final a = _angleBetween(me.forward, d);
      if (a >= minAngle && a <= maxAngle) return true;
    }
    return false;
  }

  static double _angleBetween(Vector2 a, Vector2 b) {
    final la = a.length, lb = b.length;
    if (la < 1e-6 || lb < 1e-6) return 0;
    return math.acos((a.dot(b) / (la * lb)).clamp(-1.0, 1.0)) * 180 / math.pi;
  }

  static double _lerp(double a, double b, double t) => a + (b - a) * t;
}
