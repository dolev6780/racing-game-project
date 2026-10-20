import 'package:flutter/services.dart';

import '../sim/car.dart';

/// Player input, written by the keyboard handler and the touch controls, read by [PlayerDriver].
class PlayerControls {
  // Keyboard
  final Set<LogicalKeyboardKey> keys = {};

  // Touch (set by the TouchControls widget)
  double touchSteer = 0; // joystick / buttons, -1..1
  bool touchBrake = false;
  bool touchActive = false; // touch controls in use -> auto-accelerate

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

  void read(CarInput out) {
    double steer = 0, throttle = 0;
    if (_down([LogicalKeyboardKey.keyA, LogicalKeyboardKey.arrowLeft])) steer -= 1;
    if (_down([LogicalKeyboardKey.keyD, LogicalKeyboardKey.arrowRight])) steer += 1;
    if (_down([LogicalKeyboardKey.keyW, LogicalKeyboardKey.arrowUp])) throttle += 1;
    if (_down([LogicalKeyboardKey.keyS, LogicalKeyboardKey.arrowDown])) throttle -= 1;

    if (touchActive) {
      steer += touchSteer;
      throttle = touchBrake ? -1 : 1; // phones: auto-accelerate, brake button reverses
    }
    out.steer = steer.clamp(-1.0, 1.0);
    out.throttle = throttle.clamp(-1.0, 1.0);
    out.fire = firePressed;
    out.mine = minePressed;
    out.boost = boostPressed;
    firePressed = minePressed = boostPressed = false;
  }
}

class PlayerDriver implements Driver {
  final PlayerControls controls;
  PlayerDriver(this.controls);

  @override
  void drive(Car car, CarInput out, double dt) => controls.read(out);
}
