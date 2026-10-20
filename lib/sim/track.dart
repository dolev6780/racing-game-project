import 'dart:math' as math;
import 'dart:ui' show Color;

import 'package:vector_math/vector_math.dart';

/// Look of a track.
class TrackTheme {
  final Color ground, road, wallA, wallB, rock, crystal, sky;
  final bool forest; // trees vs rocks & crystals
  final double crystalChance;

  const TrackTheme({
    required this.ground,
    required this.road,
    required this.wallA,
    required this.wallB,
    required this.rock,
    required this.crystal,
    required this.sky,
    this.forest = false,
    this.crystalChance = 0.25,
  });
}

class TrackDef {
  final String name;
  final String subtitle;
  final List<Vector2> layout; // closed loop control points; start line at the first point
  final TrackTheme theme;

  const TrackDef(this.name, this.subtitle, this.layout, this.theme);
}

Vector2 _v(double x, double y) => Vector2(x, -y); // layouts are authored with +y = north

/// The four tracks (same layouts as the Unity version: no overlaps, drivable corners, straight grid).
final List<TrackDef> tracks = [
  TrackDef('GREEN VALLEY', 'Forest circuit', [
    _v(0, -20), _v(0, 40), _v(10, 80), _v(40, 100), _v(80, 95), _v(100, 70), _v(95, 40), _v(70, 20),
    _v(60, -10), _v(80, -40), _v(110, -60), _v(115, -100), _v(90, -125), _v(40, -125), _v(8, -112), _v(0, -80),
  ], const TrackTheme(
    ground: Color(0xFF63C74D), road: Color(0xFF6B6E80), wallA: Color(0xFFFAFAFF), wallB: Color(0xFF3380FF),
    rock: Color(0xFF9E9994), crystal: Color(0xFF33E6FF), sky: Color(0xFF8FD3FF), forest: true,
  )),
  TrackDef('DESERT CANYON', 'Long straights, fast sweepers', [
    _v(0, -20), _v(0, 60), _v(15, 95), _v(50, 108), _v(80, 90), _v(75, 55), _v(50, 35), _v(50, 5),
    _v(75, -15), _v(115, -10), _v(140, 15), _v(160, 50), _v(185, 50), _v(200, 15), _v(195, -45),
    _v(165, -85), _v(115, -105), _v(55, -105), _v(15, -95), _v(0, -65),
  ], const TrackTheme(
    ground: Color(0xFFF9C873), road: Color(0xFF806B61), wallA: Color(0xFFFFF2D9), wallB: Color(0xFFFF7333),
    rock: Color(0xFFE07A47), crystal: Color(0xFFFFBF4D), sky: Color(0xFFFFE0B3), crystalChance: 0.08,
  )),
  TrackDef('FROST PEAK', 'Icy hairpins', [
    _v(0, -20), _v(0, 50), _v(-10, 90), _v(15, 125), _v(70, 130), _v(115, 115), _v(130, 75), _v(108, 45),
    _v(125, 12), _v(155, -10), _v(160, -60), _v(130, -100), _v(80, -110), _v(30, -115), _v(5, -95), _v(0, -65),
  ], const TrackTheme(
    ground: Color(0xFFF2F7FF), road: Color(0xFF6B7894), wallA: Color(0xFF4D99FF), wallB: Color(0xFFFFFFFF),
    rock: Color(0xFFB3C7E6), crystal: Color(0xFF80D9FF), sky: Color(0xFFE0F0FF), crystalChance: 0.35,
  )),
  TrackDef('ALIEN WASTES', 'Twisty purple planet', [
    _v(0, -20), _v(0, 40), _v(20, 75), _v(60, 72), _v(80, 40), _v(115, 40), _v(135, 75), _v(175, 80),
    _v(200, 45), _v(185, 0), _v(145, -20), _v(145, -65), _v(115, -100), _v(65, -112), _v(25, -110),
    _v(4, -88), _v(0, -60),
  ], const TrackTheme(
    ground: Color(0xFF8C4DBF), road: Color(0xFF4D4761), wallA: Color(0xFF33FFCC), wallB: Color(0xFFFF4DCC),
    rock: Color(0xFF66338C), crystal: Color(0xFF4DFFD9), sky: Color(0xFF261A4D), crystalChance: 0.45,
  )),
];

/// Closed-loop centerline, evenly sampled. Used for lap counting, positions, AI, respawn and walls.
class TrackPath {
  static const double roadWidth = 14;
  static const double spacing = 3;

  final List<Vector2> points;
  late final List<double> _cumulative;
  late final double length;

  TrackPath(this.points) {
    _cumulative = List.filled(points.length, 0);
    double total = 0;
    for (int i = 0; i < points.length; i++) {
      _cumulative[i] = total;
      total += points[i].distanceTo(points[(i + 1) % points.length]);
    }
    length = total;
  }

  factory TrackPath.fromLayout(List<Vector2> control) => TrackPath(_sampleCatmullRom(control, spacing));

  int get count => points.length;
  int wrap(int i) => ((i % count) + count) % count;
  Vector2 point(int i) => points[wrap(i)];
  Vector2 direction(int i) => (point(i + 1) - point(i))..normalize();

  /// Right-hand side of the direction (screen space, y down).
  Vector2 right(int i) {
    final d = direction(i);
    return Vector2(-d.y, d.x);
  }

  double distanceAtIndex(int i) => _cumulative[wrap(i)];

  int findClosest(Vector2 pos) {
    int best = 0;
    double bestSq = double.infinity;
    for (int i = 0; i < count; i++) {
      final d = points[i].distanceToSquared(pos);
      if (d < bestSq) {
        bestSq = d;
        best = i;
      }
    }
    return best;
  }

  int findClosestLocal(Vector2 pos, int hint, [int window = 15]) {
    int best = wrap(hint);
    double bestSq = double.infinity;
    for (int o = -window; o <= window; o++) {
      final i = wrap(hint + o);
      final d = points[i].distanceToSquared(pos);
      if (d < bestSq) {
        bestSq = d;
        best = i;
      }
    }
    return best;
  }

  /// Distance along the track from the start line, refined by projecting onto the segment.
  double distanceAlong(Vector2 pos, int index) {
    final a = point(index), b = point(index + 1);
    final ab = b - a;
    final t = ((pos - a).dot(ab) / math.max(ab.length2, 1e-4)).clamp(0.0, 1.0);
    return _cumulative[wrap(index)] + t * ab.length;
  }

  /// Signed sideways offset from the centerline (positive = right of the driving direction).
  double lateralOffset(Vector2 pos, int index) => (pos - point(index)).dot(right(index));

  /// World point and direction at a distance along the track (wraps).
  Vector2 pointAtDistance(double distance, Vector2 outDirection) {
    distance = distance % length;
    if (distance < 0) distance += length;
    int i = 0;
    while (i < count - 1 && _cumulative[i + 1] <= distance) {
      i++;
    }
    final a = point(i), b = point(i + 1);
    final seg = a.distanceTo(b);
    final t = seg > 1e-4 ? (distance - _cumulative[i]) / seg : 0.0;
    outDirection.setFrom((b - a)..normalize());
    return a + (b - a) * t;
  }

  static List<Vector2> _sampleCatmullRom(List<Vector2> control, double spacing) {
    final dense = <Vector2>[];
    final n = control.length;
    const steps = 30;
    for (int i = 0; i < n; i++) {
      final p0 = control[(i - 1 + n) % n], p1 = control[i], p2 = control[(i + 1) % n], p3 = control[(i + 2) % n];
      for (int s = 0; s < steps; s++) {
        dense.add(_catmullRom(p0, p1, p2, p3, s / steps));
      }
    }
    final result = <Vector2>[dense[0].clone()];
    double carried = 0;
    for (int i = 0; i < dense.length; i++) {
      final a = dense[i], b = dense[(i + 1) % dense.length];
      final seg = a.distanceTo(b);
      double pos = spacing - carried;
      while (pos <= seg) {
        result.add(a + (b - a) * (pos / seg));
        pos += spacing;
      }
      carried = seg - (pos - spacing);
    }
    if (result.last.distanceTo(result.first) < spacing * 0.5) result.removeLast();
    return result;
  }

  static Vector2 _catmullRom(Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3, double t) {
    final t2 = t * t, t3 = t2 * t;
    return (p1 * 2.0 +
            (p2 - p0) * t +
            (p0 * 2.0 - p1 * 5.0 + p2 * 4.0 - p3) * t2 +
            (p1 * 3.0 - p0 - p2 * 3.0 + p3) * t3) *
        0.5;
  }
}
