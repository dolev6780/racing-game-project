import 'dart:math' as math;

import 'package:flutter/material.dart';

import '../sim/race.dart';
import 'theme.dart';

/// North-up track map with a dot for every racer (player = larger gold dot).
class MinimapPainter extends CustomPainter {
  final Race race;
  late final Rect _bounds;

  MinimapPainter(this.race, {super.repaint}) {
    double minX = double.infinity, minY = double.infinity, maxX = -double.infinity, maxY = -double.infinity;
    for (final p in race.path.points) {
      minX = math.min(minX, p.x);
      minY = math.min(minY, p.y);
      maxX = math.max(maxX, p.x);
      maxY = math.max(maxY, p.y);
    }
    final span = math.max(maxX - minX, maxY - minY) + 20;
    final c = Offset((minX + maxX) / 2, (minY + maxY) / 2);
    _bounds = Rect.fromCenter(center: c, width: span, height: span);
  }

  Offset _map(double x, double y, Size s) =>
      Offset((x - _bounds.left) / _bounds.width * s.width, (y - _bounds.top) / _bounds.height * s.height);

  @override
  void paint(Canvas canvas, Size size) {
    final pts = race.path.points;
    final path = Path()..moveTo(_map(pts[0].x, pts[0].y, size).dx, _map(pts[0].x, pts[0].y, size).dy);
    for (final p in pts.skip(1)) {
      final o = _map(p.x, p.y, size);
      path.lineTo(o.dx, o.dy);
    }
    path.close();
    canvas.drawPath(path, Paint()
      ..color = const Color(0xCC000000)
      ..style = PaintingStyle.stroke
      ..strokeWidth = 9
      ..strokeJoin = StrokeJoin.round);
    canvas.drawPath(path, Paint()
      ..color = const Color(0xEEFFFFFF)
      ..style = PaintingStyle.stroke
      ..strokeWidth = 5
      ..strokeJoin = StrokeJoin.round);
    final s0 = _map(pts[0].x, pts[0].y, size);
    canvas.drawCircle(s0, 4, Paint()..color = AppColors.accent);

    for (final c in [...race.cars.where((c) => !c.isPlayer), race.player]) {
      if (c.wrecked && (race.time * 4).floor().isEven) continue;
      final o = _map(c.position.x, c.position.y, size);
      final r = c.isPlayer ? 8.0 : 5.5;
      canvas.drawCircle(o, r + 2, Paint()..color = const Color(0xFF111111));
      canvas.drawCircle(o, r, Paint()..color = c.isPlayer ? AppColors.gold : c.color);
    }
  }

  @override
  bool shouldRepaint(covariant CustomPainter oldDelegate) => true;
}

/// 270-degree speed gauge with a nitro glow.
class GaugePainter extends CustomPainter {
  final double value01;
  final bool boosting;
  GaugePainter(this.value01, this.boosting);

  @override
  void paint(Canvas canvas, Size size) {
    final c = size.center(Offset.zero);
    final r = size.shortestSide / 2;
    canvas.drawCircle(c, r, Paint()..color = const Color(0x990D0F1A));
    if (boosting) {
      canvas.drawCircle(c, r * 1.15, Paint()
        ..color = AppColors.nitro.withValues(alpha: 0.35)
        ..maskFilter = const MaskFilter.blur(BlurStyle.normal, 18));
    }
    final rect = Rect.fromCircle(center: c, radius: r * 0.82);
    const start = math.pi * 0.75, sweep = math.pi * 1.5;
    canvas.drawArc(rect, start, sweep, false, Paint()
      ..color = const Color(0x26FFFFFF)
      ..style = PaintingStyle.stroke
      ..strokeWidth = r * 0.16
      ..strokeCap = StrokeCap.round);
    final color = boosting ? AppColors.nitro : Color.lerp(AppColors.accent, AppColors.hot, value01)!;
    canvas.drawArc(rect, start, sweep * value01.clamp(0.0, 1.0), false, Paint()
      ..color = color
      ..style = PaintingStyle.stroke
      ..strokeWidth = r * 0.16
      ..strokeCap = StrokeCap.round);
  }

  @override
  bool shouldRepaint(covariant GaugePainter old) => old.value01 != value01 || old.boosting != boosting;
}

/// Radial "going fast" streaks; [seed] changes every frame for flicker.
class SpeedLinesPainter extends CustomPainter {
  final double alpha;
  final int seed;
  SpeedLinesPainter(this.alpha, this.seed);

  @override
  void paint(Canvas canvas, Size size) {
    if (alpha <= 0.01) return;
    final rng = math.Random(seed);
    final c = size.center(Offset.zero);
    final maxR = size.longestSide * 0.75;
    final paint = Paint()..strokeCap = StrokeCap.round;
    for (int i = 0; i < 70; i++) {
      final a = rng.nextDouble() * math.pi * 2;
      final r0 = maxR * (0.42 + rng.nextDouble() * 0.25);
      final dir = Offset(math.cos(a), math.sin(a));
      paint
        ..color = Colors.white.withValues(alpha: alpha * (0.4 + rng.nextDouble() * 0.6))
        ..strokeWidth = 1.5 + rng.nextDouble() * 3;
      canvas.drawLine(c + dir * r0, c + dir * maxR, paint);
    }
  }

  @override
  bool shouldRepaint(covariant SpeedLinesPainter old) => true;
}
