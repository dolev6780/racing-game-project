import 'dart:math' as math;

import 'package:flutter/services.dart';

import '../sim/car.dart';

/// Player input, written by the keyboard handler and the touch controls, read by [PlayerDriver].
class PlayerControls {
  // Keyboard
  final Set<LogicalKeyboardKey> keys = {};

  // Touch (set by the TouchControls widget)
  double touchSteer = 0; // arrow buttons, -1..1
  bool touchGas = false, touchReverse = false;
  /// Stick steering: the screen direction the player points to (null = stick released).
  /// Screen x right, y down.
  ({double x, double y})? touchAim;

  // One-shot buttons (consumed by the driver)
  bool firePressed = false, minePressed = false, boostPressed = false;

  bool _down(List<LogicalKeyboardKey> any) => any.any(keys.contains);

  void onKey(KeyEvent event) {
    if (event is KeyDownEvent) {
      keys.add(event.logicalKey);
      final k = event.logicalKey;
      if (k == LogicalKeyboardKey.space || k == LogicalKeyboardKey.keyJ) firePressed = true;
      if (k == LogicalKeyboardKey.keyE || k == LogicalKeyboardKey.keyK) minePressed = true;
      if (k == LogicalKeyboardKey.shiftLeft || k == LogicalKeyboardKey.shiftRight || k == LogicalKeyboardKey.keyL) {
        boostPressed = true;
      }
    } else if (event is KeyUpEvent) {
      keys.remove(event.logicalKey);
    }
  }

  void read(CarInput out, Car car) {
    double steer = 0, throttle = 0;
    if (_down([LogicalKeyboardKey.keyA, LogicalKeyboardKey.arrowLeft])) steer -= 1;
    if (_down([LogicalKeyboardKey.keyD, LogicalKeyboardKey.arrowRight])) steer += 1;
    if (_down([LogicalKeyboardKey.keyW, LogicalKeyboardKey.arrowUp])) throttle += 1;
    if (_down([LogicalKeyboardKey.keyS, LogicalKeyboardKey.arrowDown])) throttle -= 1;

    if (touchGas) throttle += 1;
    if (touchReverse) throttle -= 1;
    steer += touchSteer;
    final aim = touchAim;
    if (aim != null) steer += aimSteer(car, aim.x, aim.y, reversing: car.forwardSpeed < -0.1);

    out.steer = steer.clamp(-1.0, 1.0);
    out.throttle = throttle.clamp(-1.0, 1.0);
    out.fire = firePressed;
    out.mine = minePressed;
    out.boost = boostPressed;
    firePressed = minePressed = boostPressed = false;
  }

  /// Steering that turns the car so it moves toward a screen direction. The view is isometric
  /// (ground rotated 45 degrees and squashed), so the screen direction is first mapped back to
  /// the ground. When reversing, the tail is pointed that way instead (you back toward it).
  static double aimSteer(Car car, double sx, double sy, {bool reversing = false}) {
    const squash = 0.56;
    final a = sx / math.cos(math.pi / 4), b = sy / (math.sin(math.pi / 4) * squash);
    final wx = (a + b) / 2, wy = (b - a) / 2;
    var target = math.atan2(wx, -wy); // car angle convention: 0 = up, clockwise
    if (reversing) target += math.pi;
    var diff = (target - car.angle) % (2 * math.pi);
    if (diff > math.pi) diff -= 2 * math.pi;
    if (reversing) diff = -diff; // steering is mirrored when moving backwards
    return (diff * 2.2).clamp(-1.0, 1.0);
  }
}

class PlayerDriver implements Driver {
  final PlayerControls controls;
  PlayerDriver(this.controls);

  @override
  void drive(Car car, CarInput out, double dt) => controls.read(out, car);
}
