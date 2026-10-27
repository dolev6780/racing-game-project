import 'dart:math' as math;
import 'dart:ui';

import 'package:vector_math/vector_math.dart';

import '../data/vehicles.dart';
import '../sim/car.dart';
import 'iso.dart';
import 'palette.dart';

/// 2.5D go-kart (kart-racer style) built from shaded 3D blocks: chunky wheels, low chassis with a
/// nose cone and bumper, side pods, rear engine with exhaust pipes, and a big-helmeted driver.
class CarPainter {
  static const Color metal = Color(0xFF555A66);

  /// [steer] -1..1 for front wheel angle, [time] for flame flicker.
  static void paint(Canvas c, Car car, double steer, double time, {bool showShadow = true}) {
    final pos = car.renderPosition, a = car.renderAngle;
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
        _helmet(c, at(0, -0.15), 1.8, 0.56, color, f);
      }, 0.01);
    } else {
      _kart(c, car, pos, a, wheelAngle, at, part);
    }

    if (car.boosting) {
      if (bike) {
        part(at(0, -2.2), () => _flame(c, at(0, -1.95), f, time, 0.62), -0.3);
      } else {
        for (final x in const [-0.28, 0.28]) {
          part(at(x, -2.1), () => _flame(c, at(x, -1.85), f, time + x, 0.85, 0.7), -0.3);
        }
      }
    }

    parts.sort((p, q) => p.$1.compareTo(q.$1));
    for (final p in parts) {
      p.$2();
    }
  }

  static void _kart(Canvas c, Car car, Vector2 pos, double a, double wheelAngle, Vector2 Function(double, double) at,
      void Function(Vector2, void Function(), [double]) part) {
    final color = car.color;
    final dark = Ink.shade(color, 0.62), light = Color.lerp(color, const Color(0xFFFFFFFF), 0.45)!;
    final f = Vector2(math.sin(a), -math.cos(a));

    // Chunky round tyres (bigger at the back). Far ones go behind the body, near ones in front.
    for (final (x, y, front) in const [(-0.92, 1.15, true), (0.92, 1.15, true), (-0.98, -1.05, false), (0.98, -1.05, false)]) {
      final w = at(x, y);
      final ang = front ? wheelAngle : a;
      final radius = front ? 0.38 : 0.48, width = front ? 0.42 : 0.56;
      final nearSide = (w - pos).dot(Iso.toViewer) > 0;
      part(w, () => _tyre(c, w, ang, radius, width, color), nearSide ? 5 : -5);
    }

    // Low chassis, side pods, nose cone, bumper.
    part(pos, () {
      IsoPainter.box(c, at(0, 0.1), a, 0.72, 1.5, 0.22, 0.42, color);
      IsoPainter.box(c, at(-0.74, -0.1), a, 0.14, 0.75, 0.24, 0.62, const Color(0xFFF5F5FA));
      IsoPainter.box(c, at(0.74, -0.1), a, 0.14, 0.75, 0.24, 0.62, const Color(0xFFF5F5FA));
      IsoPainter.prism(c, [at(-0.62, 0.55), at(0.62, 0.55), at(0.38, 1.75), at(-0.38, 1.75)], 0.3, 0.72, color);
      IsoPainter.box(c, at(0, 1.92), a, 0.78, 0.13, 0.18, 0.42, const Color(0xFFF5F5FA));
      IsoPainter.box(c, at(0, -1.25), a, 0.46, 0.3, 0.38, 0.8, const Color(0xFF7A8090)); // engine
    });

    // Upper parts (seat, driver, steering wheel, exhausts) sorted among themselves.
    final upper = <(Vector2, void Function())>[
      (at(0, -0.62), () => IsoPainter.box(c, at(0, -0.62), a, 0.4, 0.12, 0.42, 1.1, dark)),
      (at(0, -1.6), () {
        for (final x in const [-0.24, 0.24]) {
          IsoPainter.box(c, at(x, -1.58), a, 0.07, 0.12, 0.6, 0.98, const Color(0xFF9AA0AE));
        }
      }),
      (at(0, -0.2), () {
        IsoPainter.box(c, at(0, -0.22), a, 0.38, 0.3, 0.42, 1.2, Ink.shade(color, 0.85)); // body in overalls
        IsoPainter.box(c, at(-0.32, 0.18), a, 0.1, 0.32, 0.95, 1.12, light); // arms
        IsoPainter.box(c, at(0.32, 0.18), a, 0.1, 0.32, 0.95, 1.12, light);
      }),
      (at(0, 0.5), () => IsoPainter.box(c, at(0, 0.52), a, 0.3, 0.06, 0.9, 1.1, const Color(0xFF22232C))),
      (at(0, -0.15), () => _helmet(c, at(0, -0.15), 1.72, 0.58, color, f)),
    ]..sort((p, q) => Iso.depth(p.$1.x, p.$1.y).compareTo(Iso.depth(q.$1.x, q.$1.y)));
    part(pos, () {
      for (final u in upper) {
        u.$2();
      }
    }, 0.5);
  }

  /// Round tyre seen in 3D: inner disc, tread joining both discs, outer disc with a white rim and
  /// a hub in the team colour. Drawn from whichever side faces the camera.
  static void _tyre(Canvas c, Vector2 center, double ang, double radius, double width, Color hubColor) {
    final f = Vector2(math.sin(ang), -math.cos(ang)), r = Vector2(math.cos(ang), math.sin(ang));
    final facing = r.dot(Iso.toViewer) >= 0 ? r : -r; // the side we see
    List<Offset> disc(Vector2 base, double rad) => [
          for (int k = 0; k < 14; k++)
            Iso.projectV(base + f * (math.cos(k * math.pi / 7) * rad), radius + math.sin(k * math.pi / 7) * rad),
        ];
    final back = disc(center - facing * (width / 2), radius), front = disc(center + facing * (width / 2), radius);
    final tread = Paint()..color = Ink.tyre;
    final hull = _convexHull([...back, ...front]);
    final hullPath = Path()..addPolygon(hull, true);
    c.drawPath(hullPath, tread);
    c.drawPath(hullPath, IsoPainter.outline);
    final face = Path()..addPolygon(front, true);
    c.drawPath(face, Paint()..color = const Color(0xFF2C2C36));
    c.drawPath(face, IsoPainter.outline);
    final rimBase = center + facing * (width / 2 + 0.01);
    c.drawPath(Path()..addPolygon(disc(rimBase, radius * 0.62), true), Paint()..color = const Color(0xFFF2F2F7));
    c.drawPath(Path()..addPolygon(disc(rimBase, radius * 0.3), true), Paint()..color = hubColor);
  }

  static List<Offset> _convexHull(List<Offset> pts) {
    final p = List.of(pts)..sort((a, b) => a.dx != b.dx ? a.dx.compareTo(b.dx) : a.dy.compareTo(b.dy));
    double cross(Offset o, Offset a, Offset b) => (a.dx - o.dx) * (b.dy - o.dy) - (a.dy - o.dy) * (b.dx - o.dx);
    final lower = <Offset>[], upper = <Offset>[];
    for (final q in p) {
      while (lower.length >= 2 && cross(lower[lower.length - 2], lower.last, q) <= 0) {
        lower.removeLast();
      }
      lower.add(q);
    }
    for (final q in p.reversed) {
      while (upper.length >= 2 && cross(upper[upper.length - 2], upper.last, q) <= 0) {
        upper.removeLast();
      }
      upper.add(q);
    }
    return [...lower..removeLast(), ...upper..removeLast()];
  }

  /// Big round helmet in the team colour, with a white stripe and a dark visor facing forward.
  static void _helmet(Canvas c, Vector2 at, double z, double r, Color color, Vector2 forward) {
    IsoPainter.ball(c, at, z, r, color);
    final o = Iso.projectV(at, z);
    final fwd = Iso.projectV(at + forward, z) - o; // screen direction the driver looks
    final len = fwd.distance;
    if (len < 1e-6) return;
    final dir = fwd / len;
    // Stripe over the top, from front to back.
    c.drawLine(o - dir * r * 0.7 + const Offset(0, -0.12), o + dir * r * 0.55 + const Offset(0, -0.12), Paint()
      ..color = const Color(0xEEFFFFFF)
      ..strokeWidth = r * 0.22
      ..strokeCap = StrokeCap.round);
    // Visor: only when the face is turned toward the camera.
    if (forward.dot(Iso.toViewer) > -0.2) {
      final v = o + dir * r * 0.55 + Offset(0, r * 0.12);
      c.save();
      c.translate(v.dx, v.dy);
      c.rotate(math.atan2(dir.dy, dir.dx) + math.pi / 2);
      c.drawOval(Rect.fromCenter(center: Offset.zero, width: r * 1.05, height: r * 0.5), Paint()..color = const Color(0xFF1C2333));
      c.drawOval(Rect.fromCenter(center: Offset(-r * 0.18, -r * 0.06), width: r * 0.32, height: r * 0.14),
          Paint()..color = const Color(0x99BFE6FF));
      c.restore();
    }
  }

  static void _flame(Canvas c, Vector2 base, Vector2 forward, double time, double z, [double scale = 1]) {
    final len = (1.5 + 0.5 * math.sin(time * 40)) * scale;
    final tip = base - forward * len;
    final side = Vector2(-forward.y, forward.x) * 0.45 * scale;
    final outer = Path()
      ..addPolygon([Iso.projectV(base + side, z), Iso.projectV(tip, z), Iso.projectV(base - side, z)], true);
    c.drawPath(outer, Paint()
      ..color = const Color(0xFFFF7A1A)
      ..blendMode = BlendMode.plus);
    final inner = Path()
      ..addPolygon([Iso.projectV(base + side * 0.5, z), Iso.projectV(base - forward * len * 0.6, z), Iso.projectV(base - side * 0.5, z)], true);
    c.drawPath(inner, Paint()..color = const Color(0xFFFFF0A0));
  }
}
