import 'package:flutter/material.dart';

class AppColors {
  static const accent = Color(0xFFFFC81A);
  static const hot = Color(0xFFFF5C33);
  static const good = Color(0xFF59F273);
  static const bad = Color(0xFFFF4D4D);
  static const nitro = Color(0xFF4DBFFF);
  static const panel = Color(0xD9121520);
  static const panelLight = Color(0x1FFFFFFF);
  static const button = Color(0xF2293040);
  static const dark = Color(0xFF15161C);
  static const dim = Color(0x99FFFFFF);
  static const gold = Color(0xFFFFCC33), silver = Color(0xFFCCD6E6), bronze = Color(0xFFD98040);
}

TextStyle title(double size, [Color color = Colors.white]) => TextStyle(
      fontFamily: 'RussoOne',
      fontSize: size,
      color: color,
      height: 1.1,
      shadows: const [Shadow(color: Color(0x88000000), offset: Offset(2, 3), blurRadius: 0)],
    );

TextStyle body(double size, [Color color = Colors.white]) =>
    TextStyle(fontFamily: 'Rubik', fontWeight: FontWeight.w700, fontSize: size, color: color, height: 1.2);

String money(int v) {
  final s = v.abs().toString().replaceAllMapped(RegExp(r'(\d)(?=(\d{3})+$)'), (m) => '${m[1]},');
  return '${v < 0 ? '-' : ''}\$$s';
}

String ordinal(int n) {
  if (n % 100 >= 11 && n % 100 <= 13) return '${n}TH';
  return '$n${switch (n % 10) { 1 => 'ST', 2 => 'ND', 3 => 'RD', _ => 'TH' }}';
}

/// Chunky game button with a press animation.
class GameButton extends StatefulWidget {
  final String label;
  final VoidCallback? onPressed;
  final Color? color, textColor;
  final double width, height, fontSize;

  const GameButton(this.label,
      {super.key, this.onPressed, this.color, this.textColor, this.width = 300, this.height = 64, this.fontSize = 26});

  const GameButton.primary(this.label, {super.key, this.onPressed, this.width = 300, this.height = 64, this.fontSize = 28})
      : color = AppColors.accent,
        textColor = AppColors.dark;

  @override
  State<GameButton> createState() => _GameButtonState();
}

class _GameButtonState extends State<GameButton> {
  bool _down = false;

  @override
  Widget build(BuildContext context) {
    final enabled = widget.onPressed != null;
    final color = widget.color ?? AppColors.button;
    return GestureDetector(
      onTapDown: enabled ? (_) => setState(() => _down = true) : null,
      onTapCancel: () => setState(() => _down = false),
      onTapUp: enabled ? (_) => setState(() => _down = false) : null,
      onTap: widget.onPressed,
      child: AnimatedScale(
        scale: _down ? 0.93 : 1,
        duration: const Duration(milliseconds: 80),
        child: Opacity(
          opacity: enabled ? 1 : 0.45,
          child: Container(
            width: widget.width,
            height: widget.height,
            alignment: Alignment.center,
            decoration: BoxDecoration(
              color: color,
              borderRadius: BorderRadius.circular(widget.height * 0.3),
              border: Border.all(color: const Color(0x33FFFFFF), width: 2),
              boxShadow: const [BoxShadow(color: Color(0x66000000), offset: Offset(0, 4))],
            ),
            child: FittedBox(
              child: Padding(
                padding: const EdgeInsets.symmetric(horizontal: 12),
                child: Text(widget.label, style: title(widget.fontSize, widget.textColor ?? Colors.white)),
              ),
            ),
          ),
        ),
      ),
    );
  }
}

class GamePanel extends StatelessWidget {
  final Widget child;
  final EdgeInsets padding;
  final Color color;
  const GamePanel({super.key, required this.child, this.padding = const EdgeInsets.all(20), this.color = AppColors.panel});

  @override
  Widget build(BuildContext context) => Container(
        padding: padding,
        decoration: BoxDecoration(
          color: color,
          borderRadius: BorderRadius.circular(22),
          border: Border.all(color: const Color(0x22FFFFFF), width: 2),
        ),
        child: child,
      );
}

/// "LABEL      <  VALUE  >" selector row.
class OptionRow extends StatelessWidget {
  final String label;
  final List<String> options;
  final int index;
  final ValueChanged<int> onChanged;
  const OptionRow({super.key, required this.label, required this.options, required this.index, required this.onChanged});

  @override
  Widget build(BuildContext context) {
    void change(int d) => onChanged((index + d + options.length) % options.length);
    return Container(
      margin: const EdgeInsets.symmetric(vertical: 5),
      padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 6),
      decoration: BoxDecoration(color: AppColors.panelLight, borderRadius: BorderRadius.circular(14)),
      child: Row(children: [
        Expanded(child: Text(label, style: body(18))),
        GameButton('<', width: 48, height: 44, fontSize: 22, onPressed: () => change(-1)),
        SizedBox(width: 170, child: Center(child: Text(options[index], style: title(20, AppColors.accent)))),
        GameButton('>', width: 48, height: 44, fontSize: 22, onPressed: () => change(1)),
      ]),
    );
  }
}

/// Bank chip shown in menus.
class BankChip extends StatelessWidget {
  final int amount;
  const BankChip(this.amount, {super.key});

  @override
  Widget build(BuildContext context) => GamePanel(
        padding: const EdgeInsets.symmetric(horizontal: 20, vertical: 10),
        child: Text(money(amount), style: title(26, AppColors.accent)),
      );
}

/// Dark backdrop used behind menu screens that have no live background.
class MenuBackdrop extends StatelessWidget {
  final Widget child;
  const MenuBackdrop({super.key, required this.child});

  @override
  Widget build(BuildContext context) => Container(
        decoration: const BoxDecoration(
          gradient: LinearGradient(
            begin: Alignment.topLeft,
            end: Alignment.bottomRight,
            colors: [Color(0xFF1B2A4A), Color(0xFF3B1F4F), Color(0xFF14161F)],
          ),
        ),
        child: SafeArea(child: child),
      );
}
