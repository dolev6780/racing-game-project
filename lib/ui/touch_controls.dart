import 'dart:math' as math;

import 'package:flutter/gestures.dart' show PointerDeviceKind;
import 'package:flutter/material.dart';

import '../data/profile.dart';
import '../game/controls.dart';
import '../sim/car.dart';
import 'theme.dart';

enum _Btn { left, right, brake, fire, mine, nitro }

/// Phone controls: floating joystick (or < > buttons) on the left, action buttons on the right.
/// Auto-accelerates; BRAKE (or pulling the joystick down) reverses.
class TouchControls extends StatefulWidget {
  final PlayerControls controls;
  final Car car;
  const TouchControls({super.key, required this.controls, required this.car});

  @override
  State<TouchControls> createState() => _TouchControlsState();
}

class _TouchControlsState extends State<TouchControls> {
  final Map<int, Offset> _pointers = {};
  final Set<_Btn> _held = {};
  int? _joyPointer;
  Offset _joyOrigin = Offset.zero, _joyKnob = Offset.zero;
  Size _size = Size.zero;

  bool get _buttonsMode => Profile.steering == SteeringMode.buttons;

  Map<_Btn, Rect> _layout(Size s) {
    final u = s.height * 0.17, m = s.height * 0.035, big = u * 1.3;
    return {
      if (_buttonsMode) _Btn.left: Rect.fromLTWH(m, s.height - m - u, u * 1.1, u),
      if (_buttonsMode) _Btn.right: Rect.fromLTWH(m * 2 + u * 1.1, s.height - m - u, u * 1.1, u),
      _Btn.fire: Rect.fromLTWH(s.width - m - big, s.height - m - big, big, big),
      _Btn.mine: Rect.fromLTWH(s.width - m * 2 - big - u, s.height - m - u, u, u),
      _Btn.brake: Rect.fromLTWH(s.width - m * 3 - big - u * 2, s.height - m - u, u, u),
      _Btn.nitro: Rect.fromLTWH(s.width - m - big + (big - u) / 2, s.height - m * 2 - big - u, u, u),
    };
  }

  double get _joyRadius => _size.height * 0.11;

  void _recompute() {
    final c = widget.controls;
    final layout = _layout(_size);
    final previously = Set.of(_held);
    _held.clear();
    bool joyStillDown = false;
    for (final e in _pointers.entries) {
      if (e.key == _joyPointer) {
        joyStillDown = true;
        final delta = e.value - _joyOrigin;
        if (delta.distance > _joyRadius) _joyOrigin = e.value - delta / delta.distance * _joyRadius;
        _joyKnob = e.value;
        continue;
      }
      bool onButton = false;
      for (final b in layout.entries) {
        if (b.value.contains(e.value)) {
          _held.add(b.key);
          onButton = true;
        }
      }
      if (!onButton && !_buttonsMode && _joyPointer == null && e.value.dx < _size.width * 0.45 && e.value.dy > _size.height * 0.3) {
        _joyPointer = e.key;
        _joyOrigin = e.value;
        _joyKnob = e.value;
        joyStillDown = true;
      }
    }
    if (!joyStillDown) _joyPointer = null;

    double steer = 0;
    bool pullBrake = false;
    if (_buttonsMode) {
      if (_held.contains(_Btn.left)) steer -= 1;
      if (_held.contains(_Btn.right)) steer += 1;
    } else if (_joyPointer != null) {
      final d = (_joyKnob - _joyOrigin) / _joyRadius;
      steer = d.dx.abs() < 0.12 ? 0 : d.dx.sign * ((d.dx.abs() - 0.12) / 0.88).clamp(0.0, 1.0);
      pullBrake = d.dy > 0.7;
    }
    c.touchSteer = steer;
    c.touchBrake = _held.contains(_Btn.brake) || pullBrake;
    if (_held.contains(_Btn.fire) && !previously.contains(_Btn.fire)) c.firePressed = true;
    if (_held.contains(_Btn.mine) && !previously.contains(_Btn.mine)) c.minePressed = true;
    if (_held.contains(_Btn.nitro) && !previously.contains(_Btn.nitro)) c.boostPressed = true;
    setState(() {});
  }

  @override
  void dispose() {
    widget.controls
      ..touchActive = false
      ..touchSteer = 0
      ..touchBrake = false;
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    return LayoutBuilder(builder: (context, constraints) {
      _size = constraints.biggest;
      final layout = _layout(_size);
      final car = widget.car;
      return Listener(
        behavior: HitTestBehavior.translucent,
        onPointerDown: (e) {
          // Real touches switch to phone driving (auto-accelerate); a mouse can still press the buttons.
          if (e.kind == PointerDeviceKind.touch) widget.controls.touchActive = true;
          _pointers[e.pointer] = e.localPosition;
          _recompute();
        },
        onPointerMove: (e) {
          _pointers[e.pointer] = e.localPosition;
          _recompute();
        },
        onPointerUp: (e) {
          _pointers.remove(e.pointer);
          _recompute();
        },
        onPointerCancel: (e) {
          _pointers.remove(e.pointer);
          _recompute();
        },
        child: Stack(children: [
          for (final b in layout.entries)
            Positioned.fromRect(
              rect: b.value,
              child: _ButtonView(
                label: switch (b.key) {
                  _Btn.left => '<',
                  _Btn.right => '>',
                  _Btn.brake => 'BRAKE',
                  _Btn.fire => 'FIRE',
                  _Btn.mine => 'MINE',
                  _Btn.nitro => 'NITRO',
                },
                color: switch (b.key) {
                  _Btn.fire => AppColors.hot,
                  _Btn.nitro => AppColors.nitro,
                  _Btn.mine => const Color(0xFF5A5C6B),
                  _ => const Color(0xFF3A3F50),
                },
                round: b.key == _Btn.fire || b.key == _Btn.mine || b.key == _Btn.nitro,
                held: _held.contains(b.key),
                badge: switch (b.key) {
                  _Btn.fire => car.missiles,
                  _Btn.mine => car.mines,
                  _Btn.nitro => car.boosts,
                  _ => null,
                },
              ),
            ),
          if (!_buttonsMode) _joystick(),
        ]),
      );
    });
  }

  Widget _joystick() {
    final active = _joyPointer != null;
    final origin = active ? _joyOrigin : Offset(_size.width * 0.14, _size.height * 0.75);
    final knob = active ? _joyKnob : origin;
    final r = _joyRadius;
    return Stack(children: [
      Positioned(
        left: origin.dx - r,
        top: origin.dy - r,
        child: IgnorePointer(
          child: Container(
            width: r * 2,
            height: r * 2,
            decoration: BoxDecoration(
              shape: BoxShape.circle,
              color: Colors.white.withValues(alpha: active ? 0.18 : 0.08),
              border: Border.all(color: Colors.white.withValues(alpha: 0.45), width: 3),
            ),
          ),
        ),
      ),
      Positioned(
        left: knob.dx - r * 0.45,
        top: knob.dy - r * 0.45,
        child: IgnorePointer(
          child: Container(
            width: r * 0.9,
            height: r * 0.9,
            decoration: BoxDecoration(
              shape: BoxShape.circle,
              color: AppColors.accent.withValues(alpha: active ? 0.95 : 0.4),
              border: Border.all(color: AppColors.dark, width: 2),
            ),
          ),
        ),
      ),
    ]);
  }
}

class _ButtonView extends StatelessWidget {
  final String label;
  final Color color;
  final bool round, held;
  final int? badge;
  const _ButtonView({required this.label, required this.color, required this.round, required this.held, this.badge});

  @override
  Widget build(BuildContext context) {
    final empty = badge == 0;
    return IgnorePointer(
      child: AnimatedScale(
        scale: held ? 0.9 : 1,
        duration: const Duration(milliseconds: 60),
        child: Stack(clipBehavior: Clip.none, children: [
          Container(
            decoration: BoxDecoration(
              color: color.withValues(alpha: held ? 1 : (empty ? 0.3 : 0.75)),
              shape: round ? BoxShape.circle : BoxShape.rectangle,
              borderRadius: round ? null : BorderRadius.circular(18),
              border: Border.all(color: Colors.white.withValues(alpha: 0.5), width: 3),
            ),
            alignment: Alignment.center,
            child: FittedBox(
              child: Padding(padding: const EdgeInsets.all(8), child: Text(label, style: title(20))),
            ),
          ),
          if (badge != null)
            Positioned(
              right: -4,
              top: -4,
              child: Container(
                width: 30,
                height: 30,
                alignment: Alignment.center,
                decoration: const BoxDecoration(color: AppColors.dark, shape: BoxShape.circle),
                child: Text('${math.max(0, badge!)}', style: title(16)),
              ),
            ),
        ]),
      ),
    );
  }
}
