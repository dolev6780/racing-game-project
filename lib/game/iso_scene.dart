import 'dart:math' as math;
import 'dart:ui';

import 'package:flame/components.dart';
import 'package:flutter/painting.dart' show TextPainter, TextSpan, TextStyle, TextDirection;
import 'package:vector_math/vector_math.dart' as vm;

import '../sim/car.dart';
import '../sim/race.dart';
import '../sim/track.dart';
import 'car_view.dart';
import 'effects_layer.dart';
import 'iso.dart';
import 'palette.dart';

class _Sprite {
  final double x, y; // world position used for depth sorting and culling
  final void Function(Canvas c) draw;
  _Sprite(this.x, this.y, this.draw);
}

/// Renders the whole race in 2.5D isometric view:
/// 1) ground plane (grass, road, curbs, pads, skid marks), 2) depth-sorted 3D objects
/// (walls, scenery, finish arch, cars, items, missiles), 3) particles and explosions on top.
class IsoScene extends Component {
  final Race race;
  final EffectsLayer effects;
  final vm.Vector2 Function() focus; // world point the camera looks at (for culling)
  late final Picture _ground;
  final List<_Sprite> _statics = [];
  final Map<Car, double> _steer = {};
  double _t = 0;
  late final TextPainter _question;

  static const double half = TrackPath.roadWidth / 2;
  static const double cullRadius = 78;

  IsoScene(this.race, this.effects, this.focus) : super(priority: 0);

  TrackPath get path => race.path;
  TrackTheme get theme => race.setup.track.theme;

  @override
  Future<void> onLoad() async {
    _ground = _buildGround();
    _buildWalls();
    _buildScenery();
    _buildArch();
    _question = TextPainter(
      text: const TextSpan(text: '?', style: TextStyle(fontFamily: 'RussoOne', fontSize: 14, color: Color(0xFFFFFFFF), height: 1)),
      textDirection: TextDirection.ltr,
    )..layout();
  }

  @override
  void update(double dt) {
    _t += dt;
    for (final c in race.cars) {
      final s = _steer[c] ?? 0;
      _steer[c] = s + (c.input.steer - s) * math.min(1, dt * 12);
    }
  }

  // ---------- geometry helpers ----------

  vm.Vector2 _smoothRight(int i) {
    final d = (path.direction(i - 1) + path.direction(i))..normalize();
    return vm.Vector2(-d.y, d.x);
  }

  vm.Vector2 _edge(int i, double offset) => path.point(i) + _smoothRight(i) * offset;

  Path _band(int i, double inner, double outer) => Path()
    ..addPolygon([
      Offset(_edge(i, inner).x, _edge(i, inner).y),
      Offset(_edge(i + 1, inner).x, _edge(i + 1, inner).y),
      Offset(_edge(i + 1, outer).x, _edge(i + 1, outer).y),
      Offset(_edge(i, outer).x, _edge(i, outer).y),
    ], true);

  // ---------- ground (drawn once into a picture, in world metres) ----------

  Picture _buildGround() {
    final recorder = PictureRecorder();
    final c = Canvas(recorder);
    double minX = double.infinity, minY = double.infinity, maxX = -double.infinity, maxY = -double.infinity;
    for (final p in path.points) {
      minX = math.min(minX, p.x);
      minY = math.min(minY, p.y);
      maxX = math.max(maxX, p.x);
      maxY = math.max(maxY, p.y);
    }
    final b = Rect.fromLTRB(minX, minY, maxX, maxY).inflate(220);
    c.drawRect(b, Paint()..color = theme.ground);
    final light = theme.ground.computeLuminance() > 0.6;
    final stripe = Paint()..color = Ink.shade(theme.ground, light ? 0.97 : 0.93);
    for (double x = b.left; x < b.right; x += 16) {
      c.drawRect(Rect.fromLTWH(x, b.top, 8, b.height), stripe);
    }

    // Dark verge under the road edges for depth.
    final verge = Path(), road = Path(), dashes = Path(), red = Path(), white = Path(), line = Path();
    for (int i = 0; i < path.count; i++) {
      verge.addPath(_band(i, -half - 1.6, half + 1.6), Offset.zero);
      road.addPath(_band(i, -half - 0.05, half + 0.05), Offset.zero);
      if (i % 4 == 0) dashes.addPath(_band(i, -0.15, 0.15), Offset.zero);
      final curb = i.isEven ? red : white;
      curb.addPath(_band(i, half - 1.1, half), Offset.zero);
      curb.addPath(_band(i, -half, -half + 1.1), Offset.zero);
      line.addPath(_band(i, half - 1.6, half - 1.35), Offset.zero);
      line.addPath(_band(i, -half + 1.35, -half + 1.6), Offset.zero);
    }
    c.drawPath(verge, Paint()..color = Ink.shade(theme.ground, 0.75));
    c.drawPath(road, Paint()..color = theme.road);
    // Subtle asphalt sheen down the middle.
    final sheen = Path();
    for (int i = 0; i < path.count; i++) {
      sheen.addPath(_band(i, -half * 0.55, half * 0.55), Offset.zero);
    }
    c.drawPath(sheen, Paint()..color = Ink.shade(theme.road, 1.08));
    c.drawPath(dashes, Paint()..color = const Color(0x66FFFFFF));
    c.drawPath(white, Paint()..color = Ink.curbWhite);
    c.drawPath(red, Paint()..color = Ink.curbRed);
    c.drawPath(line, Paint()..color = Ink.edgeYellow);

    // Checkered start line.
    final p0 = path.point(0), d = path.direction(0), r = path.right(0);
    const tiles = 10, tile = TrackPath.roadWidth / tiles;
    for (int row = 0; row < 2; row++) {
      for (int i = 0; i < tiles; i++) {
        final ctr = p0 + r * (-half + tile * (i + 0.5)) + d * (row * tile);
        final corners = [
          for (final (sx, sy) in const [(-0.5, -0.5), (0.5, -0.5), (0.5, 0.5), (-0.5, 0.5)]) ctr + r * (sx * tile) + d * (sy * tile),
        ];
        c.drawPath(Path()..addPolygon([for (final v in corners) Offset(v.x, v.y)], true),
            Paint()..color = (i + row).isEven ? const Color(0xFFFFFFFF) : const Color(0xFF111111));
      }
    }
    return recorder.endRecording();
  }

  // ---------- static 3D objects ----------

  void _buildWalls() {
    for (int i = 0; i < path.count; i++) {
      for (final side in const [-1.0, 1.0]) {
        final inner = side * (half + 0.4), outer = side * (half + 1.2);
        final fp = [_edge(i, inner), _edge(i + 1, inner), _edge(i + 1, outer), _edge(i, outer)];
        final mid = (fp[0] + fp[2]) * 0.5;
        final color = (i ~/ 2).isEven ? theme.wallA : theme.wallB;
        _statics.add(_Sprite(mid.x, mid.y, (c) => IsoPainter.prism(c, fp, 0, 1.1, color)));
      }
    }
  }

  void _buildScenery() {
    final rng = math.Random(1234);
    final clearance = half + 4;
    final count = theme.forest ? 230 : 160;
    int placed = 0, attempts = 0;
    while (placed < count && attempts < 4000) {
      attempts++;
      final i = rng.nextInt(path.count);
      final side = rng.nextBool() ? -1.0 : 1.0;
      final size = theme.forest ? 1.4 + rng.nextDouble() * 1.6 : 0.9 + rng.nextDouble() * 2.2;
      final pos = path.point(i) + path.right(i) * side * (clearance + size + rng.nextDouble() * 60);
      if (path.point(path.findClosest(pos)).distanceTo(pos) < clearance + size) continue;
      placed++;
      if (theme.forest) {
        final leaf = Color.lerp(const Color(0xFF2E8F3A), const Color(0xFF5CC447), rng.nextDouble())!;
        final h = 1.0 + rng.nextDouble() * 0.8;
        _statics.add(_Sprite(pos.x, pos.y, (c) {
          IsoPainter.shadow(c, pos + vm.Vector2(0.8, 0.4), size * 1.1, size * 1.1);
          IsoPainter.box(c, pos, 0.6, 0.22, 0.22, 0, h + size * 0.6, const Color(0xFF7A4B2A));
          IsoPainter.ball(c, pos, h + size * 0.9, size, leaf);
          IsoPainter.ball(c, pos + vm.Vector2(-0.2, -0.1), h + size * 1.7, size * 0.65, Ink.shade(leaf, 1.08));
        }));
      } else if (rng.nextDouble() < theme.crystalChance) {
        final ang = rng.nextDouble() * math.pi;
        final hgt = size * (1.6 + rng.nextDouble());
        _statics.add(_Sprite(pos.x, pos.y, (c) {
          final o = Iso.projectV(pos);
          c.drawOval(Rect.fromCenter(center: o, width: size * 3, height: size * 3 * Iso.squash),
              Paint()..color = theme.crystal.withValues(alpha: 0.25));
          final f = vm.Vector2(math.sin(ang), -math.cos(ang)), r = vm.Vector2(math.cos(ang), math.sin(ang));
          final fp = [pos + f * size * 0.5, pos + r * size * 0.3, pos - f * size * 0.5, pos - r * size * 0.3];
          IsoPainter.prism(c, fp, 0, hgt, theme.crystal, topColor: Color.lerp(theme.crystal, const Color(0xFFFFFFFF), 0.5));
        }));
      } else {
        final ang = rng.nextDouble() * math.pi;
        final h1 = size * (0.5 + rng.nextDouble() * 0.4);
        _statics.add(_Sprite(pos.x, pos.y, (c) {
          IsoPainter.shadow(c, pos + vm.Vector2(0.5, 0.3), size * 1.1, size * 0.9);
          IsoPainter.box(c, pos, ang, size * 0.9, size * 0.75, 0, h1, theme.rock);
          IsoPainter.box(c, pos + vm.Vector2(-0.2, -0.15), ang + 0.4, size * 0.55, size * 0.5, h1, h1 + size * 0.4, Ink.shade(theme.rock, 1.1));
        }));
      }
    }
  }

  void _buildArch() {
    final p0 = path.point(0), d = path.direction(0), r = path.right(0);
    final ang = math.atan2(d.x, -d.y);
    const w = half + 2.0;
    for (final s in const [-1.0, 1.0]) {
      final pillar = p0 + r * (s * w);
      _statics.add(_Sprite(pillar.x, pillar.y, (c) => IsoPainter.box(c, pillar, ang, 0.55, 0.55, 0, 6.2, const Color(0xFF3A3F55))));
    }
    // Banner across the top, checkered.
    _statics.add(_Sprite(p0.x + d.x * 0.01, p0.y + d.y * 0.01, (c) {
      const segs = 8;
      for (int k = 0; k < segs; k++) {
        final t0 = -w + (2 * w) * k / segs, t1 = -w + (2 * w) * (k + 1) / segs;
        final fp = [p0 + r * t0 + d * 0.4, p0 + r * t1 + d * 0.4, p0 + r * t1 - d * 0.4, p0 + r * t0 - d * 0.4];
        IsoPainter.prism(c, fp, 5.0, 6.4, k.isEven ? const Color(0xFFFFC81A) : const Color(0xFF1A1A22));
      }
    }));
  }

  // ---------- frame ----------

  @override
  void render(Canvas canvas) {
    final c = canvas;
    final f = focus();

    // 1) Ground plane.
    c.save();
    c.transform(Iso.groundMatrix);
    c.drawPicture(_ground);
    for (final pad in race.boostPads) {
      _drawPad(c, pad);
    }
    effects.renderGround(c);
    _drawCloudShadows(c, f);
    c.restore();
    for (final m in race.mines) {
      _drawMineBase(c, m);
    }

    // 2) Depth-sorted 3D objects near the camera.
    final sprites = <_Sprite>[];
    const r2 = cullRadius * cullRadius;
    for (final s in _statics) {
      final dx = s.x - f.x, dy = s.y - f.y;
      if (dx * dx + dy * dy < r2) sprites.add(s);
    }
    for (final car in race.cars) {
      if (car.wrecked) continue;
      if (car.invulnerable > 0 && (_t * 10).floor().isEven) continue;
      final steer = _steer[car] ?? 0;
      sprites.add(_Sprite(car.position.x, car.position.y, (c) => CarPainter.paint(c, car, steer, _t)));
    }
    for (final box in race.itemBoxes) {
      if (box.hiddenFor <= 0) sprites.add(_Sprite(box.position.x, box.position.y, (c) => _drawItemBox(c, box)));
    }
    for (final m in race.missiles) {
      sprites.add(_Sprite(m.position.x, m.position.y, (c) => _drawMissile(c, m)));
    }
    for (final m in race.mines) {
      sprites.add(_Sprite(m.position.x, m.position.y, (c) => _drawMineTop(c, m)));
    }
    sprites.sort((a, b) => Iso.depth(a.x, a.y).compareTo(Iso.depth(b.x, b.y)));
    for (final s in sprites) {
      s.draw(c);
    }

    // 3) Particles and explosions.
    effects.renderTop(c);
  }

  /// Big soft shadows of (unseen) clouds drifting across the ground.
  void _drawCloudShadows(Canvas c, vm.Vector2 f) {
    final rng = math.Random(42);
    for (int k = 0; k < 7; k++) {
      final baseX = rng.nextDouble() * 400 - 200, baseY = rng.nextDouble() * 400 - 200;
      final r = 22 + rng.nextDouble() * 20;
      // Drift diagonally and wrap around a 400m tile centred on the camera.
      var x = (baseX + _t * 3.2) % 400, y = (baseY + _t * 1.6) % 400;
      x = f.x + ((x - f.x + 200) % 400) - 200;
      y = f.y + ((y - f.y + 200) % 400) - 200;
      c.drawCircle(Offset(x, y), r, Paint()
        ..shader = Gradient.radial(Offset(x, y), r, const [Color(0x22000020), Color(0x00000020)]));
    }
  }

  void _drawPad(Canvas c, BoostPad pad) {
    c.save();
    c.translate(pad.position.x, pad.position.y);
    c.rotate(math.atan2(pad.direction.x, -pad.direction.y));
    const w = BoostPad.width, l = BoostPad.length;
    final rect = RRect.fromRectAndRadius(Rect.fromCenter(center: Offset.zero, width: w, height: l), const Radius.circular(0.5));
    c.drawRRect(rect.inflate(0.5), Paint()
      ..color = const Color(0x66FF8C1A)
      ..blendMode = BlendMode.plus);
    c.drawRRect(rect, Paint()..color = const Color(0xFFF25A0D));
    c.save();
    c.clipRRect(rect);
    final chevron = Paint()..color = Color.lerp(const Color(0xFFFFD21F), const Color(0xFFFFFFFF), 0.3 + 0.3 * math.sin(_t * 8))!;
    const spacing = 2.2;
    final offset = (_t * 5) % spacing;
    for (double y = l / 2 + spacing - offset; y > -l / 2 - spacing; y -= spacing) {
      c.drawPath(
          Path()
            ..addPolygon([
              Offset(-w * 0.38, y + 0.9),
              Offset(0, y - 0.3),
              Offset(w * 0.38, y + 0.9),
              Offset(w * 0.38, y + 1.5),
              Offset(0, y + 0.3),
              Offset(-w * 0.38, y + 1.5),
            ], true),
          chevron);
    }
    c.restore();
    c.drawRRect(rect, Paint()
      ..color = const Color(0xFFFFFFFF)
      ..style = PaintingStyle.stroke
      ..strokeWidth = 0.25);
    c.restore();
  }

  void _drawItemBox(Canvas c, ItemBox box) {
    final bob = 0.35 * math.sin(_t * 3 + box.position.x);
    final z0 = 0.9 + bob, z1 = z0 + 1.5;
    IsoPainter.shadow(c, box.position, 1.2, 1.2, alpha: 0.22);
    final hue = (_t * 0.35 + box.position.x * 0.02) % 1.0;
    final color = Ink.hsv(hue, 0.6, 1);
    IsoPainter.box(c, box.position, _t * 1.4 + box.position.y, 0.75, 0.75, z0, z1, color,
        topColor: Ink.hsv(hue + 0.12, 0.45, 1));
    // Glow + "?" floating above the top face.
    final top = Iso.projectV(box.position, z1 + 0.05);
    c.drawCircle(top, 1.3, Paint()
      ..color = color.withValues(alpha: 0.25)
      ..blendMode = BlendMode.plus);
    c.save();
    c.translate(top.dx, top.dy - 0.2);
    c.scale(0.08);
    _question.paint(c, Offset(-_question.width / 2, -_question.height / 2));
    c.restore();
  }

  void _drawMissile(Canvas c, Missile m) {
    final ang = math.atan2(m.direction.x, -m.direction.y);
    IsoPainter.shadow(c, m.position, 0.5, 0.5, alpha: 0.2);
    final tail = m.position - m.direction * 0.9;
    final flame = Iso.projectV(tail, 0.75);
    c.drawCircle(flame, 0.45 + 0.15 * math.sin(_t * 50), Paint()
      ..color = const Color(0xFFFFA126)
      ..blendMode = BlendMode.plus);
    IsoPainter.box(c, m.position, ang, 0.2, 0.65, 0.55, 0.95, const Color(0xFFE8E8EE));
    IsoPainter.box(c, m.position + m.direction * 0.55, ang, 0.2, 0.12, 0.55, 0.95, m.owner.color);
  }

  void _drawMineBase(Canvas c, Mine m) => IsoPainter.shadow(c, m.position, 0.9, 0.9, alpha: 0.3);

  void _drawMineTop(Canvas c, Mine m) {
    IsoPainter.box(c, m.position, 0.4, 0.7, 0.7, 0, 0.3, const Color(0xFF2A2A33));
    final armed = m.age > 0.5 && (m.age * 3).floor().isEven;
    final o = Iso.projectV(m.position, 0.42);
    c.drawCircle(o, 0.28, Paint()..color = armed ? const Color(0xFFFF2A2A) : const Color(0xFF661111));
    if (armed) {
      c.drawCircle(o, 0.7, Paint()
        ..color = const Color(0x55FF2A2A)
        ..blendMode = BlendMode.plus);
    }
  }
}
