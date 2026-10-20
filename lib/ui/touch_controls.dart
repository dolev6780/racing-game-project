import 'dart:math' as math;

import 'package:flutter/material.dart';

import '../data/profile.dart';
import '../game/controls.dart';
import '../sim/car.dart';
import 'theme.dart';

enum _Btn { left, right, gas, reverse, fire, mine, nitro }

/// Phone controls (landscape):
/// left thumb steers ([SteeringMode.arrows]: big < > buttons, [SteeringMode.stick]: point where to go),
/// right thumb drives (GAS / REVERSE pedals) with FIRE, MINE and NITRO above them.
/// Every finger is tracked separately, so you can slide between buttons and press several at once.
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
  int? _stickPointer;
  Offset _stickOrigin = Offset.zero, _stickKnob = Offset.zero;
  Size _size = Size.zero;

  bool get _stickMode => Profile.steering == SteeringMode.stick;

  double get _unit => math.min(_size.height * 0.2, _size.width * 0.11);
  double get _margin => _unit * 0.22;
  double get _stickRadius => _unit * 0.95;
  Offset get _stickHome => Offset(_margin + _unit * 1.25, _size.height - _margin - _unit * 1.25);

  Map<_Btn, Rect> _layout() {
    final u = _unit, m = _margin, w = _size.width, h = _size.height, gap = m * 0.6;
    final gas = Rect.fromLTWH(w - m - u * 1.15, h - m - u * 1.7, u * 1.15, u * 1.7);
    final rev = Rect.fromLTWH(gas.left - gap - u * 1.05, h - m - u * 1.15, u * 1.05, u * 1.15);
    final a = u * 0.9, rowTop = gas.top - gap - a;
    return {
      if (!_stickMode) _Btn.left: Rect.fromLTWH(m, h - m - u * 1.3, u * 1.2, u * 1.3),
      if (!_stickMode) _Btn.right: Rect.fromLTWH(m + u * 1.2 + gap, h - m - u * 1.3, u * 1.2, u * 1.3),
      _Btn.gas: gas,
      _Btn.reverse: rev,
      _Btn.fire: Rect.fromLTWH(gas.center.dx - a / 2, rowTop, a, a),
      _Btn.mine: Rect.fromLTWH(rev.center.dx - a / 2, rev.top - gap - a, a, a),
      _Btn.nitro: Rect.fromLTWH(rev.center.dx - a / 2 - a - gap, rev.top - gap - a, a, a),
    };
  }

  void _recompute() {
    final c = widget.controls;
    final layout = _layout();
    final previously = Set.of(_held);
    _held.clear();
    bool stickStillDown = false;
    for (final e in _pointers.entries) {
      if (e.key == _stickPointer) {
        stickStillDown = true;
        _stickKnob = e.value;
        // Drag the base along when the finger goes past the rim, so steering never "runs out".
        final delta = _stickKnob - _stickOrigin;
        if (delta.distance > _stickRadius) _stickOrigin = _stickKnob - delta / delta.distance * _stickRadius;
        continue;
      }
      bool onButton = false;
      for (final b in layout.entries) {
        // Slightly larger hit areas than the drawn buttons: thumbs are not precise.
        if (b.value.inflate(_margin * 0.4).contains(e.value)) {
          _held.add(b.key);
          onButton = true;
          break;
        }
      }
      if (!onButton && _stickMode && _stickPointer == null && e.value.dx < _size.width * 0.45) {
        _stickPointer = e.key;
        _stickOrigin = e.value;
        _stickKnob = e.value;
        stickStillDown = true;
      }
    }
    if (!stickStillDown) _stickPointer = null;

    double steer = 0;
    if (_held.contains(_Btn.left)) steer -= 1;
    if (_held.contains(_Btn.right)) steer += 1;
    c.touchSteer = steer;
    c.touchAim = null;
    if (_stickPointer != null) {
      final d = _stickKnob - _stickOrigin;
      if (d.distance > _stickRadius * 0.25) c.touchAim = (x: d.dx, y: d.dy);
    }
    c.touchGas = _held.contains(_Btn.gas);
    c.touchReverse = _held.contains(_Btn.reverse);
    if (_held.contains(_Btn.fire) && !previously.contains(_Btn.fire)) c.firePressed = true;
    if (_held.contains(_Btn.mine) && !previously.contains(_Btn.mine)) c.minePressed = true;
    if (_held.contains(_Btn.nitro) && !previously.contains(_Btn.nitro)) c.boostPressed = true;
    setState(() {});
  }

  @override
  void dispose() {
    widget.controls
      ..touchSteer = 0
      ..touchAim = null
      ..touchGas = false
      ..touchReverse = false;
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    return LayoutBuilder(builder: (context, constraints) {
      _size = constraints.biggest;
      final layout = _layout();
      final car = widget.car;
      return Listener(
        behavior: HitTestBehavior.translucent,
        onPointerDown: (e) {
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
                kind: b.key,
                held: _held.contains(b.key),
                badge: switch (b.key) {
                  _Btn.fire => car.missiles,
                  _Btn.mine => car.mines,
                  _Btn.nitro => car.boosts,
                  _ => null,
                },
              ),
            ),
          if (_stickMode) _stick(),
        ]),
      );
    });
  }

  Widget _stick() {
    final active = _stickPointer != null;
    final origin = active ? _stickOrigin : _stickHome;
    final knob = active ? _stickKnob : origin;
    final r = _stickRadius;
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
              color: Colors.white.withValues(alpha: active ? 0.16 : 0.08),
              border: Border.all(color: Colors.white.withValues(alpha: 0.45), width: 3),
            ),
            alignment: Alignment.center,
            child: active ? null : Text('STEER', style: title(14, Colors.white.withValues(alpha: 0.6))),
          ),
        ),
      ),
      Positioned(
        left: knob.dx - r * 0.42,
        top: knob.dy - r * 0.42,
        child: IgnorePointer(
          child: Container(
            width: r * 0.84,
            height: r * 0.84,
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
  final _Btn kind;
  final bool held;
  final int? badge;
  const _ButtonView({required this.kind, required this.held, this.badge});

  @override
  Widget build(BuildContext context) {
    final empty = badge == 0;
    final round = kind == _Btn.fire || kind == _Btn.mine || kind == _Btn.nitro;
    final color = switch (kind) {
      _Btn.gas => const Color(0xFF2FB34A),
      _Btn.reverse => const Color(0xFFD9473A),
      _Btn.fire => AppColors.hot,
      _Btn.nitro => AppColors.nitro,
      _Btn.mine => const Color(0xFF5A5C6B),
      _ => const Color(0xFF3A3F50),
    };
    final Widget label = switch (kind) {
      _Btn.left => const _Arrow(direction: AxisDirection.left),
      _Btn.right => const _Arrow(direction: AxisDirection.right),
      _Btn.gas => Column(mainAxisSize: MainAxisSize.min, children: [
          const SizedBox(width: 34, height: 26, child: _Arrow(direction: AxisDirection.up)),
          Text('GAS', style: title(22)),
        ]),
      _Btn.reverse => Column(mainAxisSize: MainAxisSize.min, children: [
          Text('REV', style: title(20)),
          const SizedBox(width: 30, height: 22, child: _Arrow(direction: AxisDirection.down)),
        ]),
      _Btn.fire => Text('FIRE', style: title(18)),
      _Btn.mine => Text('MINE', style: title(16)),
      _Btn.nitro => Text('NITRO', style: title(14)),
    };
    return IgnorePointer(
      child: AnimatedScale(
        scale: held ? 0.92 : 1,
        duration: const Duration(milliseconds: 50),
        child: Stack(clipBehavior: Clip.none, children: [
          Container(
            decoration: BoxDecoration(
              color: color.withValues(alpha: held ? 1 : (empty ? 0.3 : 0.7)),
              shape: round ? BoxShape.circle : BoxShape.rectangle,
              borderRadius: round ? null : BorderRadius.circular(20),
              border: Border.all(color: Colors.white.withValues(alpha: held ? 0.9 : 0.5), width: 3),
            ),
            alignment: Alignment.center,
            child: FittedBox(child: Padding(padding: const EdgeInsets.all(10), child: label)),
          ),
          if (badge != null)
            Positioned(
              right: -4,
              top: -4,
              child: Container(
                width: 28,
                height: 28,
                alignment: Alignment.center,
                decoration: const BoxDecoration(color: AppColors.dark, shape: BoxShape.circle),
                child: Text('${math.max(0, badge!)}', style: title(15)),
              ),
            ),
        ]),
      ),
    );
  }
}

/// A solid white triangle (drawn, so it never depends on font glyphs).
class _Arrow extends StatelessWidget {
  final AxisDirection direction;
  const _Arrow({required this.direction});

  @override
  Widget build(BuildContext context) =>
      SizedBox(width: 48, height: 48, child: CustomPaint(painter: _ArrowPainter(direction)));
}

class _ArrowPainter extends CustomPainter {
  final AxisDirection direction;
  _ArrowPainter(this.direction);

  @override
  void paint(Canvas canvas, Size size) {
    final w = size.width, h = size.height;
    final pts = switch (direction) {
      AxisDirection.left => [Offset(w * 0.1, h / 2), Offset(w * 0.85, h * 0.1), Offset(w * 0.85, h * 0.9)],
      AxisDirection.right => [Offset(w * 0.9, h / 2), Offset(w * 0.15, h * 0.1), Offset(w * 0.15, h * 0.9)],
      AxisDirection.up => [Offset(w / 2, h * 0.05), Offset(w * 0.95, h * 0.95), Offset(w * 0.05, h * 0.95)],
      AxisDirection.down => [Offset(w / 2, h * 0.95), Offset(w * 0.95, h * 0.05), Offset(w * 0.05, h * 0.05)],
    };
    final path = Path()..addPolygon(pts, true);
    canvas.drawPath(path, Paint()..color = Colors.white);
    canvas.drawPath(path, Paint()
      ..color = AppColors.dark
      ..style = PaintingStyle.stroke
      ..strokeWidth = 2.5
      ..strokeJoin = StrokeJoin.round);
  }

  @override
  bool shouldRepaint(covariant _ArrowPainter old) => old.direction != direction;
}
