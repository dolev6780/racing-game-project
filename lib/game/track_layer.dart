import 'dart:math' as math;
import 'dart:ui';

import 'package:flame/components.dart';
import 'package:vector_math/vector_math.dart' as vm;

import '../sim/track.dart';
import 'palette.dart';

/// Draws the static track (ground, road, curbs, walls, start line, scenery) once into a Picture.
class TrackLayer extends Component {
  final TrackPath path;
  final TrackTheme theme;
  late final Picture _picture;

  TrackLayer(this.path, this.theme) : super(priority: 0);

  static const double half = TrackPath.roadWidth / 2;

  @override
  Future<void> onLoad() async {
    final recorder = PictureRecorder();
    final c = Canvas(recorder);
    _drawGround(c);
    _drawRoad(c);
    _drawCurbs(c);
    _drawWalls(c);
    _drawStartLine(c);
    _drawScenery(c);
    _picture = recorder.endRecording();
  }

  @override
  void render(Canvas canvas) => canvas.drawPicture(_picture);

  // ---------- helpers ----------

  vm.Vector2 _smoothRight(int i) {
    final d = (path.direction(i - 1) + path.direction(i))..normalize();
    return vm.Vector2(-d.y, d.x);
  }


  vm.Vector2 _edge(int i, double offset) => path.point(i) + _smoothRight(i) * offset;

  Path _band(int i, double inner, double outer) => Path()
    ..moveTo(_edge(i, inner).x, _edge(i, inner).y)
    ..lineTo(_edge(i + 1, inner).x, _edge(i + 1, inner).y)
    ..lineTo(_edge(i + 1, outer).x, _edge(i + 1, outer).y)
    ..lineTo(_edge(i, outer).x, _edge(i, outer).y)
    ..close();

  Rect get _bounds {
    double minX = double.infinity, minY = double.infinity, maxX = -double.infinity, maxY = -double.infinity;
    for (final p in path.points) {
      minX = math.min(minX, p.x);
      minY = math.min(minY, p.y);
      maxX = math.max(maxX, p.x);
      maxY = math.max(maxY, p.y);
    }
    return Rect.fromLTRB(minX, minY, maxX, maxY).inflate(170);
  }

  // ---------- layers ----------

  void _drawGround(Canvas c) {
    final b = _bounds;
    c.drawRect(b, Paint()..color = theme.ground);
    // Mowed-lawn stripes.
    final light = theme.ground.computeLuminance() > 0.6;
    final stripe = Paint()..color = Ink.shade(theme.ground, light ? 0.97 : 0.92);
    for (double x = b.left; x < b.right; x += 16) {
      c.drawRect(Rect.fromLTWH(x, b.top, 8, b.height), stripe);
    }
  }

  void _drawRoad(Canvas c) {
    final n = path.count;
    // Draw the road as segment quads (robust on tight corners).
    // One combined path (no anti-aliasing seams between the segment quads).
    final road = Path();
    for (int i = 0; i < n; i++) {
      road.addPath(_band(i, -half - 0.05, half + 0.05), Offset.zero);
    }
    c.drawPath(road, Paint()..color = theme.road);
    // Faint lane dashes.
    final dashes = Path();
    for (int i = 0; i < n; i += 4) {
      dashes.addPath(_band(i, -0.15, 0.15), Offset.zero);
    }
    c.drawPath(dashes, Paint()..color = Ink.shade(theme.road, 1.25).withValues(alpha: 0.5));
  }

  void _drawCurbs(Canvas c) {
    final red = Path(), white = Path(), line = Path();
    for (int i = 0; i < path.count; i++) {
      final p = i.isEven ? red : white;
      // Slight overlap along the track hides joins between pieces.
      p.addPath(_band(i, half - 1.1, half), Offset.zero);
      p.addPath(_band(i, -half, -half + 1.1), Offset.zero);
      line.addPath(_band(i, half - 1.6, half - 1.35), Offset.zero);
      line.addPath(_band(i, -half + 1.35, -half + 1.6), Offset.zero);
    }
    c.drawPath(white, Paint()..color = Ink.curbWhite);
    c.drawPath(red, Paint()..color = Ink.curbRed);
    c.drawPath(line, Paint()..color = Ink.edgeYellow);
  }

  void _drawWalls(Canvas c) {
    final shadow = Path(), a = Path(), b = Path(), edges = Path();
    for (int i = 0; i < path.count; i++) {
      for (final side in const [-1.0, 1.0]) {
        final inner = side * (half + 0.4), outer = side * (half + 1.2);
        shadow.addPath(_band(i, inner, outer), const Offset(0.35, 0.45));
        ((i ~/ 2).isEven ? a : b).addPath(_band(i, inner, outer), Offset.zero);
        edges
          ..moveTo(_edge(i, inner).x, _edge(i, inner).y)
          ..lineTo(_edge(i + 1, inner).x, _edge(i + 1, inner).y)
          ..moveTo(_edge(i, outer).x, _edge(i, outer).y)
          ..lineTo(_edge(i + 1, outer).x, _edge(i + 1, outer).y);
      }
    }
    c.drawPath(shadow, Paint()..color = Ink.shadow);
    c.drawPath(a, Paint()..color = theme.wallA);
    c.drawPath(b, Paint()..color = theme.wallB);
    c.drawPath(edges, Paint()
      ..color = Ink.outline
      ..style = PaintingStyle.stroke
      ..strokeWidth = 0.12);
  }

  void _drawStartLine(Canvas c) {
    final p = path.point(0), d = path.direction(0), r = path.right(0);
    const tiles = 10;
    const tile = TrackPath.roadWidth / tiles;
    final black = Paint()..color = const Color(0xFF111111), white = Paint()..color = const Color(0xFFFFFFFF);
    for (int row = 0; row < 2; row++) {
      for (int i = 0; i < tiles; i++) {
        final center = p + r * (-half + tile * (i + 0.5)) + d * (row * tile);
        final corners = [
          for (final (sx, sy) in const [(-0.5, -0.5), (0.5, -0.5), (0.5, 0.5), (-0.5, 0.5)])
            center + r * (sx * tile) + d * (sy * tile),
        ];
        final quad = Path()..addPolygon([for (final v in corners) Offset(v.x, v.y)], true);
        c.drawPath(quad, (i + row).isEven ? white : black);
      }
    }
  }

  void _drawScenery(Canvas c) {
    final rng = math.Random(1234);
    final clearance = half + 4;
    final outline = Paint()
      ..color = Ink.outline
      ..style = PaintingStyle.stroke
      ..strokeWidth = 0.15;
    final count = theme.forest ? 260 : 170;
    int placed = 0, attempts = 0;
    final items = <(vm.Vector2, double, bool)>[];
    while (placed < count && attempts < 4000) {
      attempts++;
      final i = rng.nextInt(path.count);
      final side = rng.nextBool() ? -1.0 : 1.0;
      final size = theme.forest ? 1.6 + rng.nextDouble() * 2.2 : 1.0 + rng.nextDouble() * 2.6;
      final pos = path.point(i) + path.right(i) * side * (clearance + size + rng.nextDouble() * 55);
      final closest = path.point(path.findClosest(pos));
      if (closest.distanceTo(pos) < clearance + size) continue;
      items.add((pos, size, !theme.forest && rng.nextDouble() < theme.crystalChance));
      placed++;
    }
    // Draw back to front by y so overlaps look natural.
    items.sort((a, b) => a.$1.y.compareTo(b.$1.y));
    for (final (pos, size, crystal) in items) {
      final o = Offset(pos.x, pos.y);
      c.drawOval(Rect.fromCenter(center: o + Offset(size * 0.25, size * 0.35), width: size * (crystal ? 1.2 : 1.9), height: size * (crystal ? 0.8 : 1.5)),
          Paint()..color = Ink.shadow);
      if (theme.forest) {
        // Cartoon tree: dark rim, body, highlight.
        final leaf = Color.lerp(const Color(0xFF2E8F3A), const Color(0xFF55B840), rng.nextDouble())!;
        c.drawCircle(o, size, Paint()..color = Ink.shade(leaf, 0.75));
        c.drawCircle(o + Offset(-size * 0.12, -size * 0.12), size * 0.82, Paint()..color = leaf);
        c.drawCircle(o + Offset(-size * 0.35, -size * 0.35), size * 0.3, Paint()..color = Ink.shade(leaf, 1.25));
        c.drawCircle(o, size, outline);
      } else if (crystal) {
        final shard = Path()
          ..moveTo(o.dx, o.dy - size * 1.3)
          ..lineTo(o.dx + size * 0.55, o.dy)
          ..lineTo(o.dx, o.dy + size * 0.6)
          ..lineTo(o.dx - size * 0.55, o.dy)
          ..close();
        c.drawPath(shard, Paint()..color = theme.crystal);
        c.drawPath(
            Path()
              ..moveTo(o.dx, o.dy - size * 1.3)
              ..lineTo(o.dx - size * 0.55, o.dy)
              ..lineTo(o.dx, o.dy + size * 0.6)
              ..close(),
            Paint()..color = Ink.shade(theme.crystal, 0.75));
        c.drawPath(shard, outline);
      } else {
        // Rock: lumpy polygon with a lighter top.
        final rock = Path();
        const pts = 7;
        for (int k = 0; k < pts; k++) {
          final a = k / pts * math.pi * 2;
          final r = size * (0.8 + rng.nextDouble() * 0.35);
          final v = Offset(o.dx + math.cos(a) * r, o.dy + math.sin(a) * r * 0.8);
          k == 0 ? rock.moveTo(v.dx, v.dy) : rock.lineTo(v.dx, v.dy);
        }
        rock.close();
        c.drawPath(rock, Paint()..color = theme.rock);
        c.drawCircle(o + Offset(-size * 0.25, -size * 0.25), size * 0.35, Paint()..color = Ink.shade(theme.rock, 1.2));
        c.drawPath(rock, outline);
      }
    }
  }
}
