import 'dart:math' as math;
import 'dart:ui';

import 'package:vector_math/vector_math.dart';

import '../data/vehicles.dart';
import '../sim/car.dart';
import 'iso.dart';
import 'palette.dart';

/// 2.5D combat car built from shaded 3D blocks (chassis, cabin, wheels, spoiler, roof cannon).
class CarPainter {
  static const Color glass = Color(0xFF2B3550);
  static const Color metal = Color(0xFF555A66);

  /// [steer] -1..1 for front wheel angle, [time] for flame flicker.
  static void paint(Canvas c, Car car, double steer, double time, {bool showShadow = true}) {
    final pos = car.position, a = car.angle;
    final f = Vector2(math.sin(a), -math.cos(a)), r = Vector2(math.cos(a), math.sin(a));
    Vector2 at(double x, double y) => pos + r * x + f * y; // car frame: x right, y forward

    if (showShadow) {
      final o = Iso.projectV(pos);
      c.save();
      c.translate(o.dx + 0.25, o.dy + 0.2);
      c.drawOval(Rect.fromCenter(center: Offset.zero, width: 4.4, height: 4.4 * Iso.squash), Paint()..color = const Color(0x55000000));
      c.restore();
    }
    if (car.isPlayer) {
      final o = Iso.projectV(pos);
      c.drawOval(Rect.fromCenter(center: o, width: 5.6, height: 5.6 * Iso.squash), Paint()
        ..color = Ink.gold.withValues(alpha: 0.7)
        ..style = PaintingStyle.stroke
        ..strokeWidth = 0.2);
    }

    final parts = <(double, void Function())>[];
    void part(Vector2 center, void Function() draw, [double bias = 0]) => parts.add((Iso.depth(center.x, center.y) + bias, draw));

    final bike = car.body == BodyStyle.bike;
    final color = car.color;
    final wheelAngle = a + steer * 0.45;

    if (bike) {
      part(at(0, 1.3), () => IsoPainter.box(c, at(0, 1.3), wheelAngle, 0.2, 0.48, 0, 0.95, Ink.tyre));
      part(at(0, -1.25), () => IsoPainter.box(c, at(0, -1.25), a, 0.22, 0.5, 0, 1.0, Ink.tyre));
      part(at(0, 0), () {
        IsoPainter.box(c, at(0, 0.05), a, 0.42, 1.25, 0.35, 0.95, color);
        IsoPainter.box(c, at(0, 0.75), a, 0.3, 0.25, 0.95, 1.25, metal);
        IsoPainter.ball(c, at(0, -0.15), 1.75, 0.48, const Color(0xFF2A2F40));
      }, 0.01);
    } else {
      // Wheels: far ones drawn before the body, near ones after (they stick out).
      for (final (x, y, front) in const [(-0.98, 1.2, true), (0.98, 1.2, true), (-0.98, -1.2, false), (0.98, -1.2, false)]) {
        final w = at(x, y);
        final ang = front ? wheelAngle : a;
        final nearSide = (w - pos).dot(Iso.toViewer) > 0;
        part(w, () {
          IsoPainter.box(c, w, ang, 0.24, 0.42, 0, 0.78, const Color(0xFF2E2E38), topColor: const Color(0xFF4A4A57));
          // Hubcap on the outer side.
          final hub = w + Vector2(math.cos(ang), math.sin(ang)) * (x.sign * 0.25);
          c.drawCircle(Iso.projectV(hub, 0.39), 0.17, Paint()..color = const Color(0xFFB8BCC8));
        },
            nearSide ? 5 : -5);
      }
      part(pos, () {
        // Chassis with a slightly lower hood, and a racing stripe.
        IsoPainter.box(c, at(0, -0.15), a, 0.9, 1.7, 0.28, 0.95, color);
        IsoPainter.box(c, at(0, 1.55), a, 0.86, 0.35, 0.28, 0.8, color);
        IsoPainter.decal(c, [at(-0.18, 1.5), at(0.18, 1.5), at(0.18, -1.8), at(-0.18, -1.8)], 0.955, const Color(0xCCFFFFFF));
        // Headlights.
        IsoPainter.box(c, at(-0.55, 1.88), a, 0.18, 0.04, 0.55, 0.72, const Color(0xFFFFF2A8), outlined: false);
        IsoPainter.box(c, at(0.55, 1.88), a, 0.18, 0.04, 0.55, 0.72, const Color(0xFFFFF2A8), outlined: false);
      });
      // Upper parts sorted among themselves.
      final upper = <(double, void Function())>[
        (Iso.depth(at(0, -0.3).x, at(0, -0.3).y), () => IsoPainter.box(c, at(0, -0.3), a, 0.72, 0.85, 0.95, 1.55, color,
            sideColor: glass, topColor: Ink.shade(color, 1.05))),
        (Iso.depth(at(0, -1.75).x, at(0, -1.75).y), () {
          IsoPainter.box(c, at(-0.7, -1.7), a, 0.06, 0.1, 0.95, 1.35, metal, outlined: false);
          IsoPainter.box(c, at(0.7, -1.7), a, 0.06, 0.1, 0.95, 1.35, metal, outlined: false);
          IsoPainter.box(c, at(0, -1.75), a, 1.02, 0.2, 1.35, 1.48, Ink.shade(color, 0.85));
        }),
        (Iso.depth(at(0, 0.45).x, at(0, 0.45).y) + 0.01, () => IsoPainter.box(c, at(0, 0.45), a, 0.13, 0.7, 1.55, 1.78, metal)),
      ]..sort((p, q) => p.$1.compareTo(q.$1));
      part(pos, () {
        for (final u in upper) {
          u.$2();
        }
      }, 0.5);
    }

    if (car.boosting) {
      part(at(0, -2.2), () => _flame(c, at(0, -1.95), f, time), -0.3);
    }

    parts.sort((p, q) => p.$1.compareTo(q.$1));
    for (final p in parts) {
      p.$2();
    }
  }

  static void _flame(Canvas c, Vector2 base, Vector2 forward, double time) {
    final len = 1.5 + 0.5 * math.sin(time * 40);
    final tip = base - forward * len;
    final side = Vector2(-forward.y, forward.x) * 0.45;
    final outer = Path()
      ..addPolygon([Iso.projectV(base + side, 0.62), Iso.projectV(tip, 0.62), Iso.projectV(base - side, 0.62)], true);
    c.drawPath(outer, Paint()
      ..color = const Color(0xFFFF7A1A)
      ..blendMode = BlendMode.plus);
    final inner = Path()
      ..addPolygon([Iso.projectV(base + side * 0.5, 0.62), Iso.projectV(base - forward * len * 0.6, 0.62), Iso.projectV(base - side * 0.5, 0.62)], true);
    c.drawPath(inner, Paint()..color = const Color(0xFFFFF0A0));
  }
}
