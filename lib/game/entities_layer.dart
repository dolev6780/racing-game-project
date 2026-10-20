import 'dart:math' as math;
import 'dart:ui';

import 'package:flame/components.dart';
import 'package:flutter/painting.dart' show TextPainter, TextSpan, TextStyle, TextDirection;

import '../sim/race.dart';
import 'palette.dart';

/// Item boxes, boost pads, mines and missiles.
class EntitiesLayer extends Component {
  final Race race;
  double _t = 0;
  late final TextPainter _question;

  EntitiesLayer(this.race) : super(priority: 10);

  static final Paint _outline = Paint()
    ..color = Ink.outline
    ..style = PaintingStyle.stroke
    ..strokeWidth = 0.12;

  @override
  Future<void> onLoad() async {
    _question = TextPainter(
      text: const TextSpan(
          text: '?', style: TextStyle(fontFamily: 'RussoOne', fontSize: 14, color: Color(0xFFFFFFFF), height: 1)),
      textDirection: TextDirection.ltr,
    )..layout();
  }

  @override
  void update(double dt) => _t += dt;

  @override
  void render(Canvas canvas) {
    final c = canvas;
    for (final pad in race.boostPads) {
      _drawPad(c, pad);
    }
    for (final m in race.mines) {
      _drawMine(c, m);
    }
    for (final box in race.itemBoxes) {
      if (box.hiddenFor <= 0) _drawBox(c, box);
    }
    for (final m in race.missiles) {
      _drawMissile(c, m);
    }
  }

  void _drawPad(Canvas c, BoostPad pad) {
    c.save();
    c.translate(pad.position.x, pad.position.y);
    c.rotate(math.atan2(pad.direction.x, -pad.direction.y));
    const w = BoostPad.width, l = BoostPad.length;
    final rect = RRect.fromRectAndRadius(Rect.fromCenter(center: Offset.zero, width: w, height: l), const Radius.circular(0.5));
    c.drawRRect(rect, Paint()..color = const Color(0xFFF25A0D));
    c.save();
    c.clipRRect(rect);
    // Chevrons scrolling forward (towards -y).
    final chevron = Paint()..color = Color.lerp(const Color(0xFFFFD21F), const Color(0xFFFFFFFF), 0.3 + 0.3 * math.sin(_t * 8))!;
    const spacing = 2.2;
    final offset = (_t * 5) % spacing;
    for (double y = l / 2 + spacing - offset; y > -l / 2 - spacing; y -= spacing) {
      final p = Path()
        ..moveTo(-w * 0.38, y + 0.9)
        ..lineTo(0, y - 0.3)
        ..lineTo(w * 0.38, y + 0.9)
        ..lineTo(w * 0.38, y + 1.5)
        ..lineTo(0, y + 0.3)
        ..lineTo(-w * 0.38, y + 1.5)
        ..close();
      c.drawPath(p, chevron);
    }
    c.restore();
    c.drawRRect(rect, Paint()
      ..color = const Color(0xFFFFFFFF)
      ..style = PaintingStyle.stroke
      ..strokeWidth = 0.25);
    c.restore();
  }

  void _drawBox(Canvas c, ItemBox box) {
    final bob = 1 + 0.08 * math.sin(_t * 3 + box.position.x);
    final s = 1.7 * bob;
    c.save();
    c.translate(box.position.x, box.position.y);
    c.drawCircle(const Offset(0.4, 0.5), s * 0.75, Paint()..color = Ink.shadow);
    c.rotate(_t * 1.2 + box.position.y);
    final r = RRect.fromRectAndRadius(Rect.fromCenter(center: Offset.zero, width: s, height: s), Radius.circular(s * 0.22));
    final hue = (_t * 0.35 + box.position.x * 0.02) % 1.0;
    c.drawRRect(r, Paint()..color = Ink.hsv(hue, 0.65, 1).withValues(alpha: 0.85));
    c.drawRRect(r.deflate(s * 0.14), Paint()..color = Ink.hsv(hue + 0.15, 0.5, 1).withValues(alpha: 0.6));
    c.drawRRect(r, Paint()
      ..color = const Color(0xFFFFFFFF)
      ..style = PaintingStyle.stroke
      ..strokeWidth = 0.14);
    c.rotate(-(_t * 1.2 + box.position.y)); // keep the "?" upright-ish relative to the world
    c.scale(0.085);
    _question.paint(c, Offset(-_question.width / 2, -_question.height / 2));
    c.restore();
  }

  void _drawMine(Canvas c, Mine m) {
    final o = Offset(m.position.x, m.position.y);
    c.drawCircle(o + const Offset(0.2, 0.3), 0.75, Paint()..color = Ink.shadow);
    c.drawCircle(o, 0.75, Paint()..color = const Color(0xFF2A2A33));
    for (int k = 0; k < 6; k++) {
      final a = k / 6 * math.pi * 2;
      c.drawCircle(o + Offset(math.cos(a) * 0.75, math.sin(a) * 0.75), 0.16, Paint()..color = const Color(0xFF3D3D4A));
    }
    c.drawCircle(o, 0.75, _outline);
    final armed = m.age > 0.5;
    final blink = (m.age * 3).floor().isEven;
    c.drawCircle(o, 0.28, Paint()..color = armed && blink ? const Color(0xFFFF2A2A) : const Color(0xFF661111));
  }

  void _drawMissile(Canvas c, Missile m) {
    c.save();
    c.translate(m.position.x, m.position.y);
    c.rotate(math.atan2(m.direction.x, -m.direction.y));
    final flame = Path()
      ..moveTo(-0.22, 0.7)
      ..lineTo(0, 1.4 + 0.3 * math.sin(_t * 50))
      ..lineTo(0.22, 0.7)
      ..close();
    c.drawPath(flame, Paint()..color = const Color(0xFFFFA126));
    final body = RRect.fromRectAndRadius(const Rect.fromLTWH(-0.22, -0.8, 0.44, 1.5), const Radius.circular(0.2));
    c.drawRRect(body, Paint()..color = const Color(0xFFE8E8EE));
    c.drawRRect(RRect.fromRectAndRadius(const Rect.fromLTWH(-0.22, -0.8, 0.44, 0.4), const Radius.circular(0.2)),
        Paint()..color = m.owner.color);
    c.drawRRect(body, _outline);
    c.restore();
  }
}
