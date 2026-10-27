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

/// A static object recorded once into a picture (replaying it is much cheaper than rebuilding paths).
class _Static {
  final double x, y;
  final void Function(Canvas c) _record;
  Picture? _picture;
  _Static(this.x, this.y, this._record);

  Picture get picture {
    if (_picture == null) {
      final recorder = PictureRecorder();
      _record(Canvas(recorder));
      _picture = recorder.endRecording();
    }
    return _picture!;
  }
}

/// A square piece of the ground plane, recorded into its own picture the first time it comes into
/// view (so loading is instant), then replayed every frame while visible.
class _GroundTile {
  final Rect rect;
  final Picture Function() _record;
  Picture? _picture;
  _GroundTile(this.rect, this._record);

  Picture get picture => _picture ??= _record();
}

/// Renders the whole race in 2.5D isometric view:
/// 1) ground plane (grass, road, curbs, pads, skid marks), 2) depth-sorted 3D objects
/// (walls, scenery, finish arch, cars, items, missiles), 3) particles and explosions on top.
class IsoScene extends Component {
  final Race race;
  final EffectsLayer effects;
  final vm.Vector2 Function() focus; // world point the camera looks at (for culling)
  final double Function() viewRadius; // world metres from the focus that can be on screen
  final List<_GroundTile> _ground = [];
  final List<_Static> _statics = [];
  final Map<Car, double> _steer = {};
  double _t = 0;
  late final TextPainter _question;

  static const double half = TrackPath.roadWidth / 2;
  static const double tileSize = 40;

  IsoScene(this.race, this.effects, this.focus, this.viewRadius) : super(priority: 0);

  TrackPath get path => race.path;
  TrackTheme get theme => race.setup.track.theme;

  @override
  Future<void> onLoad() async {
    _buildGround();
    _buildWalls();
    _buildScenery();
    _buildArch();
    _buildChevrons();
    _buildFlags();
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

  // ---------- ground (tiles recorded into pictures on first sight, in world metres) ----------

  void _buildGround() {
    double minX = double.infinity, minY = double.infinity, maxX = -double.infinity, maxY = -double.infinity;
    for (final p in path.points) {
      minX = math.min(minX, p.x);
      minY = math.min(minY, p.y);
      maxX = math.max(maxX, p.x);
      maxY = math.max(maxY, p.y);
    }
    final b = Rect.fromLTRB(minX, minY, maxX, maxY).inflate(220);
    final light = theme.ground.computeLuminance() > 0.6;
    final groundPaint = Paint()..color = theme.ground;
    final stripe = Paint()..color = Ink.shade(theme.ground, light ? 0.97 : 0.93);
    final vergePaint = Paint()..color = Ink.shade(theme.ground, 0.75);
    final roadPaint = Paint()..color = theme.road;
    final sheenPaint = Paint()..color = Ink.shade(theme.road, 1.08);
    final dashPaint = Paint()..color = const Color(0x66FFFFFF);
    final whitePaint = Paint()..color = Ink.curbWhite;
    final redPaint = Paint()..color = Ink.curbRed;
    final linePaint = Paint()..color = Ink.edgeYellow;

    // Bounds of every road segment (with verge) so each tile only records what touches it.
    final segBounds = [
      for (int i = 0; i < path.count; i++) _band(i, -half - 1.6, half + 1.6).getBounds(),
    ];

    for (double ty = (b.top / tileSize).floorToDouble() * tileSize; ty < b.bottom; ty += tileSize) {
      for (double tx = (b.left / tileSize).floorToDouble() * tileSize; tx < b.right; tx += tileSize) {
        final tile = Rect.fromLTWH(tx, ty, tileSize, tileSize);
        _ground.add(_GroundTile(tile, () {
          final recorder = PictureRecorder();
          final c = Canvas(recorder);
          // Tiles overlap a hair so anti-aliased edges never leave seams.
          c.clipRect(tile.inflate(0.08));
          c.drawRect(tile.inflate(0.08), groundPaint);
          // Mowed-lawn checkerboard, then flowers / pebbles (the road is painted over them).
          const cell = 8.0;
          for (double y = ty; y < tile.bottom; y += cell) {
            for (double x = tx; x < tile.right; x += cell) {
              if (((x / cell).round() + (y / cell).round()).isEven) c.drawRect(Rect.fromLTWH(x, y, cell, cell), stripe);
            }
          }
          _drawGroundDecor(c, tile);
          final segs = [for (int i = 0; i < path.count; i++) if (segBounds[i].overlaps(tile)) i];
          if (segs.isNotEmpty) {
            final verge = Path(), road = Path(), sheen = Path(), dashes = Path(), red = Path(), white = Path(), line = Path();
            for (final i in segs) {
              verge.addPath(_band(i, -half - 1.6, half + 1.6), Offset.zero);
              road.addPath(_band(i, -half - 0.05, half + 0.05), Offset.zero);
              sheen.addPath(_band(i, -half * 0.55, half * 0.55), Offset.zero);
              if (i % 4 == 0) dashes.addPath(_band(i, -0.15, 0.15), Offset.zero);
              final curb = i.isEven ? red : white;
              curb.addPath(_band(i, half - 1.1, half), Offset.zero);
              curb.addPath(_band(i, -half, -half + 1.1), Offset.zero);
              line.addPath(_band(i, half - 1.6, half - 1.35), Offset.zero);
              line.addPath(_band(i, -half + 1.35, -half + 1.6), Offset.zero);
            }
            c.drawPath(verge, vergePaint);
            c.drawPath(road, roadPaint);
            c.drawPath(sheen, sheenPaint);
            c.drawPath(dashes, dashPaint);
            c.drawPath(white, whitePaint);
            c.drawPath(red, redPaint);
            c.drawPath(line, linePaint);
            if (segs.contains(0) || segs.contains(path.count - 1)) _drawStartLine(c);
          }
          return recorder.endRecording();
        }));
      }
    }
  }

  static const List<Color> _petals = [Color(0xFFFFFFFF), Color(0xFFFFE14D), Color(0xFFFF6FA8), Color(0xFFFF5A4D), Color(0xFFB98CFF)];

  void _drawGroundDecor(Canvas c, Rect tile) {
    final rng = math.Random((tile.left * 7919 + tile.top * 104729).round());
    final paint = Paint();
    if (theme.forest) {
      for (int k = 0; k < 10; k++) {
        final x = tile.left + rng.nextDouble() * tileSize, y = tile.top + rng.nextDouble() * tileSize;
        final petal = _petals[rng.nextInt(_petals.length)];
        paint.color = petal;
        for (int p = 0; p < 4; p++) {
          final ang = p * math.pi / 2 + 0.4;
          c.drawCircle(Offset(x + math.cos(ang) * 0.32, y + math.sin(ang) * 0.32), 0.3, paint);
        }
        paint.color = petal == _petals[1] ? const Color(0xFFFF8A1F) : const Color(0xFFFFD21F);
        c.drawCircle(Offset(x, y), 0.2, paint);
      }
      // Darker grass tufts.
      paint.color = Ink.shade(theme.ground, 0.82);
      for (int k = 0; k < 8; k++) {
        final x = tile.left + rng.nextDouble() * tileSize, y = tile.top + rng.nextDouble() * tileSize;
        c.drawOval(Rect.fromCenter(center: Offset(x, y), width: 1.4, height: 0.9), paint);
      }
    } else {
      // Pebbles and sparkles in the theme colours.
      for (int k = 0; k < 12; k++) {
        final x = tile.left + rng.nextDouble() * tileSize, y = tile.top + rng.nextDouble() * tileSize;
        final sparkle = rng.nextDouble() < theme.crystalChance;
        paint.color = sparkle ? theme.crystal.withValues(alpha: 0.75) : Ink.shade(theme.ground, 0.84);
        final r = sparkle ? 0.22 : 0.3 + rng.nextDouble() * 0.5;
        c.drawOval(Rect.fromCenter(center: Offset(x, y), width: r * 2.2, height: r * 1.6), paint);
      }
    }
  }

  void _drawStartLine(Canvas c) {
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
  }

  // ---------- static 3D objects ----------

  void _buildWalls() {
    for (int i = 0; i < path.count; i++) {
      for (final side in const [-1.0, 1.0]) {
        final inner = side * (half + 0.4), outer = side * (half + 1.2);
        final fp = [_edge(i, inner), _edge(i + 1, inner), _edge(i + 1, outer), _edge(i, outer)];
        final mid = (fp[0] + fp[2]) * 0.5;
        final color = (i ~/ 2).isEven ? theme.wallA : theme.wallB;
        _statics.add(_Static(mid.x, mid.y, (c) => IsoPainter.prism(c, fp, 0, 1.1, color)));
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
        _statics.add(_Static(pos.x, pos.y, (c) {
          IsoPainter.shadow(c, pos + vm.Vector2(0.8, 0.4), size * 1.1, size * 1.1);
          IsoPainter.box(c, pos, 0.6, 0.22, 0.22, 0, h + size * 0.6, const Color(0xFF7A4B2A));
          IsoPainter.ball(c, pos, h + size * 0.9, size, leaf);
          IsoPainter.ball(c, pos + vm.Vector2(-0.2, -0.1), h + size * 1.7, size * 0.65, Ink.shade(leaf, 1.08));
        }));
      } else if (rng.nextDouble() < theme.crystalChance) {
        final ang = rng.nextDouble() * math.pi;
        final hgt = size * (1.6 + rng.nextDouble());
        _statics.add(_Static(pos.x, pos.y, (c) {
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
        _statics.add(_Static(pos.x, pos.y, (c) {
          IsoPainter.shadow(c, pos + vm.Vector2(0.5, 0.3), size * 1.1, size * 0.9);
          IsoPainter.box(c, pos, ang, size * 0.9, size * 0.75, 0, h1, theme.rock);
          IsoPainter.box(c, pos + vm.Vector2(-0.2, -0.15), ang + 0.4, size * 0.55, size * 0.5, h1, h1 + size * 0.4, Ink.shade(theme.rock, 1.1));
        }));
      }
    }
  }

  /// Yellow chevron boards on the outside of sharp corners, arrows pointing the way round.
  void _buildChevrons() {
    final n = path.count;
    int lastPlaced = -1000;
    for (int i = 0; i < n; i++) {
      if (i - lastPlaced < 14) continue;
      final d1 = path.direction(i - 3), d2 = path.direction(i + 3);
      final turn = math.acos(d1.dot(d2).clamp(-1.0, 1.0));
      if (turn < 0.55) continue;
      lastPlaced = i;
      final cross = d1.x * d2.y - d1.y * d2.x; // > 0: right-hand bend
      final side = cross > 0 ? -1.0 : 1.0; // outside of the bend
      final d = path.direction(i), r = path.right(i);
      final center = path.point(i) + r * (side * (half + 2.3));
      final ang = math.atan2(d.x, -d.y);
      final inward = r * -side; // the face the drivers see
      _statics.add(_Static(center.x, center.y, (c) {
        IsoPainter.shadow(c, center + vm.Vector2(0.6, 0.4), 3.2, 0.9, alpha: 0.2);
        for (final k in const [-2.4, 2.4]) {
          IsoPainter.box(c, center + d * k, ang, 0.12, 0.12, 0, 1.0, const Color(0xFF5A5F70), outlined: false);
        }
        IsoPainter.box(c, center, ang, 0.12, 3.0, 1.0, 2.6, const Color(0xFFFFD21F), sideColor: const Color(0xFFFFD21F));
        // Arrows on whichever long face the camera sees.
        final faceN = inward.dot(Iso.toViewer) >= 0 ? inward : -inward;
        final base = center + faceN * 0.13;
        Offset fp(double u, double v) => Iso.projectV(base + d * u, v);
        final arrow = Paint()..color = const Color(0xFF1C1C26);
        for (final u0 in const [-1.8, -0.4, 1.0]) {
          c.drawPath(
              Path()
                ..addPolygon([
                  fp(u0, 2.35), fp(u0 + 0.5, 2.35), fp(u0 + 1.0, 1.8), fp(u0 + 0.5, 1.25), fp(u0, 1.25), fp(u0 + 0.5, 1.8),
                ], true),
              arrow);
        }
      }));
    }
  }

  /// Festive pennant poles along the start straight.
  void _buildFlags() {
    const colors = [Color(0xFFFF3B3B), Color(0xFFFFD21F), Color(0xFF3BA8FF), Color(0xFF4CD964), Color(0xFFFF7AD9)];
    int k = 0;
    for (int i = -10; i <= 10; i += 3) {
      if (i.abs() < 2) continue; // keep the arch clear
      final idx = (i + path.count) % path.count;
      for (final side in const [-1.0, 1.0]) {
        final base = path.point(idx) + path.right(idx) * (side * (half + 2.6));
        final d = path.direction(idx);
        final color = colors[k++ % colors.length];
        _statics.add(_Static(base.x, base.y, (c) {
          IsoPainter.shadow(c, base + vm.Vector2(0.3, 0.2), 0.4, 0.4, alpha: 0.25);
          IsoPainter.box(c, base, 0, 0.07, 0.07, 0, 4.2, const Color(0xFFE8E8F0), outlined: false);
          final tip = base - d * 1.9;
          final flag = Path()..addPolygon([Iso.projectV(base, 4.2), Iso.projectV(tip, 3.8), Iso.projectV(base, 3.3)], true);
          c.drawPath(flag, Paint()..color = color);
          c.drawPath(flag, IsoPainter.outline);
          c.drawCircle(Iso.projectV(base, 4.25), 0.14, Paint()..color = Ink.gold);
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
      _statics.add(_Static(pillar.x, pillar.y, (c) => IsoPainter.box(c, pillar, ang, 0.55, 0.55, 0, 6.2, const Color(0xFF3A3F55))));
    }
    // Banner across the top, checkered.
    _statics.add(_Static(p0.x + d.x * 0.01, p0.y + d.y * 0.01, (c) {
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
    final radius = viewRadius();
    final view = Rect.fromCircle(center: Offset(f.x, f.y), radius: radius);
    c.save();
    c.transform(Iso.groundMatrix);
    for (final t in _ground) {
      if (t.rect.overlaps(view)) c.drawPicture(t.picture);
    }
    for (final pad in race.boostPads) {
      _drawPad(c, pad);
    }
    effects.renderGround(c, view);
    c.restore();
    for (final m in race.mines) {
      _drawMineBase(c, m);
    }

    // 2) Depth-sorted 3D objects near the camera.
    final sprites = <_Sprite>[];
    final r2 = (radius + 8) * (radius + 8); // + margin for tall objects poking into view
    for (final s in _statics) {
      final dx = s.x - f.x, dy = s.y - f.y;
      if (dx * dx + dy * dy < r2) sprites.add(_Sprite(s.x, s.y, (c) => c.drawPicture(s.picture)));
    }
    for (final car in race.cars) {
      if (car.wrecked) continue;
      if (car.invulnerable > 0 && (_t * 10).floor().isEven) continue;
      final steer = _steer[car] ?? 0;
      sprites.add(_Sprite(car.position.x, car.position.y, (c) => CarPainter.paint(c, car, steer, _t)));
    }
    for (final coin in race.coins) {
      if (coin.hiddenFor > 0) continue;
      final dx = coin.position.x - f.x, dy = coin.position.y - f.y;
      if (dx * dx + dy * dy < r2) sprites.add(_Sprite(coin.position.x, coin.position.y, (c) => _drawCoin(c, coin)));
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
    effects.renderTop(c, view);
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
    final z0 = 1.0 + bob, z1 = z0 + 1.6;
    IsoPainter.shadow(c, box.position, 1.2, 1.2, alpha: 0.22);
    final hue = (_t * 0.35 + box.position.x * 0.02) % 1.0;
    final color = Ink.hsv(hue, 0.55, 1);
    final spin = _t * 1.4 + box.position.y;
    final center = Iso.projectV(box.position, (z0 + z1) / 2);
    // Soft glow, translucent rainbow cube, a big "?" inside, and a glossy highlight.
    c.drawCircle(center, 1.6, Paint()
      ..color = color.withValues(alpha: 0.22)
      ..blendMode = BlendMode.plus);
    IsoPainter.box(c, box.position, spin, 0.8, 0.8, z0, z1, color.withValues(alpha: 0.55),
        topColor: Ink.hsv(hue + 0.15, 0.4, 1).withValues(alpha: 0.6));
    c.save();
    c.translate(center.dx, center.dy);
    c.scale(0.1);
    _question.paint(c, Offset(-_question.width / 2, -_question.height / 2));
    c.restore();
    final hl = Iso.projectV(box.position, z1 - 0.35) + const Offset(-0.45, 0);
    c.drawOval(Rect.fromCenter(center: hl, width: 0.5, height: 0.22), Paint()..color = const Color(0xCCFFFFFF));
  }

  /// Upright spinning gold coin.
  void _drawCoin(Canvas c, Coin coin) {
    final bob = 0.15 * math.sin(_t * 4 + coin.position.x);
    IsoPainter.shadow(c, coin.position, 0.55, 0.55, alpha: 0.25);
    final o = Iso.projectV(coin.position, 1.0 + bob);
    final spin = math.cos(_t * 5 + coin.position.y * 0.7);
    final w = 1.2 * spin.abs() + 0.12, h = 1.2;
    final rect = Rect.fromCenter(center: o, width: w, height: h);
    c.drawOval(rect.inflate(0.06), Paint()..color = const Color(0xFF8A5A00));
    c.drawOval(rect, Paint()..color = const Color(0xFFFFC21F));
    c.drawOval(Rect.fromCenter(center: o, width: w * 0.62, height: h * 0.62), Paint()..color = const Color(0xFFFFE066));
    if (spin.abs() > 0.35) {
      c.drawRect(Rect.fromCenter(center: o, width: w * 0.12, height: h * 0.36), Paint()..color = const Color(0xFFE09A00));
    }
    c.drawOval(Rect.fromCenter(center: o + Offset(-w * 0.18, -h * 0.22), width: w * 0.22, height: h * 0.16),
        Paint()..color = const Color(0xDDFFFFFF));
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
