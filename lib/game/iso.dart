import 'dart:math' as math;
import 'dart:typed_data';
import 'dart:ui';

import 'package:vector_math/vector_math.dart';

import 'palette.dart';

/// Isometric 2.5D projection (Rock n' Roll Racing style): the ground is rotated 45 degrees and
/// squashed vertically; height lifts things straight up the screen.
class Iso {
  static const double squash = 0.56; // vertical squash of the ground plane
  static const double heightScale = 0.95; // screen units per metre of height
  static final double _c = math.cos(math.pi / 4), _s = math.sin(math.pi / 4);

  /// World direction that faces the camera (screen-down on the ground plane).
  static final Vector2 toViewer = Vector2(_s, _c);

  /// World direction the light comes from (upper-left of the screen).
  static final Vector2 light = Vector2(-0.95, 0.3)..normalize();

  static Offset project(double x, double y, [double z = 0]) =>
      Offset(x * _c - y * _s, (x * _s + y * _c) * squash - z * heightScale);

  static Offset projectV(Vector2 p, [double z = 0]) => project(p.x, p.y, z);

  /// Larger = closer to the camera (drawn later).
  static double depth(double x, double y) => x * _s + y * _c;

  /// Canvas transform that maps ground-plane drawing (world metres) into iso space.
  static final Float64List groundMatrix = Float64List.fromList([
    _c, _s * squash, 0, 0, //
    -_s, _c * squash, 0, 0, //
    0, 0, 1, 0, //
    0, 0, 0, 1,
  ]);
}

/// Draws extruded shapes (prisms) in the iso projection with per-face shading and cartoon outlines.
class IsoPainter {
  static final Paint _fill = Paint()..isAntiAlias = true;
  static final Paint outline = Paint()
    ..color = Ink.outline
    ..style = PaintingStyle.stroke
    ..strokeWidth = 0.05
    ..strokeJoin = StrokeJoin.round;

  /// Shade a side face by how much its outward normal faces the light.
  static Color shadeSide(Color base, Vector2 normal) {
    final l = normal.dot(Iso.light);
    return Ink.shade(base, 0.7 + 0.28 * math.max(0, l) + 0.06 * normal.dot(Iso.toViewer));
  }

  static Color shadeTop(Color base) => Color.lerp(base, const Color(0xFFFFFFFF), 0.12)!;

  /// Extrudes a convex ground polygon (world metres, any winding) from z0 to z1.
  /// Only faces that face the camera are drawn, then the top.
  static void prism(Canvas c, List<Vector2> footprint, double z0, double z1, Color color,
      {Color? topColor, Color? sideColor, bool outlined = true}) {
    final n = footprint.length;
    var cx = 0.0, cy = 0.0;
    for (final p in footprint) {
      cx += p.x;
      cy += p.y;
    }
    cx /= n;
    cy /= n;
    for (int i = 0; i < n; i++) {
      final a = footprint[i], b = footprint[(i + 1) % n];
      final edge = b - a;
      var normal = Vector2(edge.y, -edge.x);
      if (normal.dot(Vector2((a.x + b.x) / 2 - cx, (a.y + b.y) / 2 - cy)) < 0) normal = -normal;
      normal.normalize();
      if (normal.dot(Iso.toViewer) <= 0.02) continue;
      final face = Path()
        ..addPolygon([
          Iso.projectV(a, z0),
          Iso.projectV(b, z0),
          Iso.projectV(b, z1),
          Iso.projectV(a, z1),
        ], true);
      _fill.color = shadeSide(sideColor ?? color, normal);
      c.drawPath(face, _fill);
      if (outlined) c.drawPath(face, outline);
    }
    final top = Path()..addPolygon([for (final p in footprint) Iso.projectV(p, z1)], true);
    _fill.color = topColor ?? shadeTop(color);
    c.drawPath(top, _fill);
    if (outlined) c.drawPath(top, outline);
  }

  /// Oriented box: centre (world), heading angle (0 = up the world, clockwise), half width/length.
  static void box(Canvas c, Vector2 center, double angle, double hw, double hl, double z0, double z1, Color color,
      {Color? topColor, Color? sideColor, bool outlined = true}) {
    prism(c, rect(center, angle, hw, hl), z0, z1, color, topColor: topColor, sideColor: sideColor, outlined: outlined);
  }

  static List<Vector2> rect(Vector2 center, double angle, double hw, double hl) {
    final f = Vector2(math.sin(angle), -math.cos(angle)), r = Vector2(math.cos(angle), math.sin(angle));
    return [
      center + r * -hw + f * hl,
      center + r * hw + f * hl,
      center + r * hw - f * hl,
      center + r * -hw - f * hl,
    ];
  }

  /// Flat quad lying on top of a surface at height z (stripes, decals).
  static void decal(Canvas c, List<Vector2> corners, double z, Color color) {
    c.drawPath(Path()..addPolygon([for (final p in corners) Iso.projectV(p, z)], true), Paint()..color = color);
  }

  /// Soft shadow blob on the ground.
  static void shadow(Canvas c, Vector2 at, double rx, double ry, {double alpha = 0.28}) {
    final o = Iso.projectV(at);
    c.drawOval(Rect.fromCenter(center: o, width: rx * 2, height: ry * 2 * Iso.squash),
        Paint()..color = Color.fromRGBO(0, 0, 0, alpha));
  }

  /// Shaded ball (tree canopy, rider helmet) at a world position and height.
  static void ball(Canvas c, Vector2 at, double z, double r, Color color) {
    final o = Iso.projectV(at, z);
    c.drawCircle(o, r, Paint()..color = Ink.shade(color, 0.78));
    c.drawCircle(o + Offset(-r * 0.12, -r * 0.14), r * 0.82, Paint()..color = color);
    c.drawCircle(o + Offset(-r * 0.36, -r * 0.4), r * 0.32, Paint()..color = Color.lerp(color, const Color(0xFFFFFFFF), 0.35)!);
    c.drawCircle(o, r, outline);
  }
}
