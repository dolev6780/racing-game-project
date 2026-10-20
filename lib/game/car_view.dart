import 'dart:math' as math;
import 'dart:ui';

import 'package:flame/components.dart';

import '../data/vehicles.dart';
import '../sim/car.dart';
import 'palette.dart';

/// Cartoon top-down car: shadow, outline, turning front wheels, cabin, spoiler, roof cannon, boost flames.
class CarView extends Component {
  final Car car;
  double _time = 0;
  double _steerVis = 0;

  CarView(this.car) : super(priority: 20);

  static final Paint _outline = Paint()
    ..color = Ink.outline
    ..style = PaintingStyle.stroke
    ..strokeWidth = 0.12
    ..strokeJoin = StrokeJoin.round;

  @override
  void update(double dt) {
    _time += dt;
    _steerVis += (car.input.steer - _steerVis) * math.min(1, dt * 12);
  }

  @override
  void render(Canvas canvas) {
    final c = canvas;
    if (car.wrecked) return;
    if (car.invulnerable > 0 && (_time * 10).floor().isEven) return; // blink after respawn

    final pos = Offset(car.position.x, car.position.y);

    // Ground shadow (not rotated with the car, so the light direction stays fixed).
    c.save();
    c.translate(pos.dx + 0.35, pos.dy + 0.5);
    c.rotate(car.angle);
    c.drawRRect(RRect.fromRectAndRadius(const Rect.fromLTWH(-1.05, -1.95, 2.1, 3.9), const Radius.circular(0.6)),
        Paint()..color = Ink.shadow);
    c.restore();

    c.save();
    c.translate(pos.dx, pos.dy);

    if (car.isPlayer) {
      c.drawCircle(Offset.zero, 2.6, Paint()
        ..color = Ink.gold.withValues(alpha: 0.55)
        ..style = PaintingStyle.stroke
        ..strokeWidth = 0.25);
    }

    c.rotate(car.angle);
    if (car.boosting) _drawFlames(c);
    if (car.body == BodyStyle.bike) {
      _drawBike(c);
    } else {
      _drawTruck(c);
    }
    c.restore();
  }

  void _wheel(Canvas c, double x, double y, double w, double h, double steer) {
    c.save();
    c.translate(x, y);
    c.rotate(steer);
    final r = RRect.fromRectAndRadius(Rect.fromCenter(center: Offset.zero, width: w, height: h), const Radius.circular(0.15));
    c.drawRRect(r, Paint()..color = Ink.tyre);
    c.drawRRect(r, _outline);
    c.restore();
  }

  void _drawTruck(Canvas c) {
    final steer = _steerVis * 0.45;
    _wheel(c, -0.95, -1.15, 0.45, 0.85, steer);
    _wheel(c, 0.95, -1.15, 0.45, 0.85, steer);
    _wheel(c, -0.95, 1.2, 0.5, 0.9, 0);
    _wheel(c, 0.95, 1.2, 0.5, 0.9, 0);

    final body = RRect.fromRectAndRadius(const Rect.fromLTWH(-0.9, -1.85, 1.8, 3.7), const Radius.circular(0.55));
    c.drawRRect(body, Paint()..color = car.color);
    // Side shading for volume.
    c.drawRRect(RRect.fromRectAndRadius(const Rect.fromLTWH(0.35, -1.6, 0.5, 3.2), const Radius.circular(0.3)),
        Paint()..color = Ink.shade(car.color, 0.82));
    // Hood highlight.
    c.drawRRect(RRect.fromRectAndRadius(const Rect.fromLTWH(-0.6, -1.65, 0.7, 0.8), const Radius.circular(0.25)),
        Paint()..color = Color.lerp(car.color, const Color(0xFFFFFFFF), 0.35)!);
    // Cabin glass.
    c.drawRRect(RRect.fromRectAndRadius(const Rect.fromLTWH(-0.7, -0.55, 1.4, 1.5), const Radius.circular(0.35)),
        Paint()..color = Ink.glass);
    c.drawRRect(RRect.fromRectAndRadius(const Rect.fromLTWH(-0.55, -0.45, 0.45, 0.6), const Radius.circular(0.15)),
        Paint()..color = const Color(0x55FFFFFF));
    c.drawRRect(body, _outline);
    // Spoiler.
    final spoiler = RRect.fromRectAndRadius(const Rect.fromLTWH(-1.05, 1.55, 2.1, 0.38), const Radius.circular(0.12));
    c.drawRRect(spoiler, Paint()..color = Ink.shade(car.color, 0.7));
    c.drawRRect(spoiler, _outline);
    // Roof cannon pointing forward.
    final cannon = RRect.fromRectAndRadius(const Rect.fromLTWH(-0.16, -1.05, 0.32, 1.25), const Radius.circular(0.1));
    c.drawRRect(cannon, Paint()..color = const Color(0xFF4A4F5C));
    c.drawRRect(cannon, _outline);
    // Headlights.
    final light = Paint()..color = const Color(0xFFFFF2A8);
    c.drawCircle(const Offset(-0.55, -1.72), 0.16, light);
    c.drawCircle(const Offset(0.55, -1.72), 0.16, light);
  }

  void _drawBike(Canvas c) {
    _wheel(c, 0, -1.3, 0.38, 0.95, _steerVis * 0.45);
    _wheel(c, 0, 1.25, 0.42, 1.0, 0);
    final body = RRect.fromRectAndRadius(const Rect.fromLTWH(-0.45, -1.2, 0.9, 2.5), const Radius.circular(0.4));
    c.drawRRect(body, Paint()..color = car.color);
    c.drawRRect(body, _outline);
    // Rider helmet.
    c.drawCircle(const Offset(0, 0.15), 0.42, Paint()..color = const Color(0xFF222633));
    c.drawCircle(const Offset(-0.12, 0.02), 0.13, Paint()..color = const Color(0x66FFFFFF));
    c.drawCircle(const Offset(0, 0.15), 0.42, _outline);
  }

  void _drawFlames(Canvas c) {
    final flicker = 0.7 + 0.3 * math.sin(_time * 40);
    final len = 1.4 + flicker;
    final outer = Path()
      ..moveTo(-0.55, 1.85)
      ..lineTo(0, 1.85 + len)
      ..lineTo(0.55, 1.85)
      ..close();
    c.drawPath(outer, Paint()..color = const Color(0xFFFF7A1A));
    final inner = Path()
      ..moveTo(-0.28, 1.85)
      ..lineTo(0, 1.85 + len * 0.6)
      ..lineTo(0.28, 1.85)
      ..close();
    c.drawPath(inner, Paint()..color = const Color(0xFFFFF0A0));
  }
}
