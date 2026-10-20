import 'dart:ui';

/// Shared colours for the cartoon look.
class Ink {
  static const outline = Color(0xFF1A1424);
  static const shadow = Color(0x40000000);
  static const glass = Color(0xFF2B3047);
  static const tyre = Color(0xFF16161C);
  static const curbRed = Color(0xFFE5452E);
  static const curbWhite = Color(0xFFF7F7F7);
  static const edgeYellow = Color(0xFFFFCC33);
  static const gold = Color(0xFFFFCC33);

  static Color shade(Color c, double f) => Color.from(
        alpha: c.a,
        red: (c.r * f).clamp(0.0, 1.0),
        green: (c.g * f).clamp(0.0, 1.0),
        blue: (c.b * f).clamp(0.0, 1.0),
      );

  static Color hsv(double h, double s, double v) {
    h = (h % 1.0) * 6;
    final i = h.floor();
    final f = h - i;
    final p = v * (1 - s), q = v * (1 - s * f), t = v * (1 - s * (1 - f));
    final (r, g, b) = switch (i) { 0 => (v, t, p), 1 => (q, v, p), 2 => (p, v, t), 3 => (p, q, v), 4 => (t, p, v), _ => (v, p, q) };
    return Color.from(alpha: 1, red: r, green: g, blue: b);
  }
}
