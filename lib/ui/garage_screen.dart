import 'package:flutter/material.dart';
import 'package:flutter/scheduler.dart';
import 'package:vector_math/vector_math.dart' show Vector2;

import '../data/profile.dart';
import '../data/vehicles.dart';
import '../game/car_view.dart';
import '../sim/car.dart';
import 'theme.dart';

class _NoDriver implements Driver {
  @override
  void drive(Car car, CarInput out, double dt) {}
}

/// Draws a vehicle with the in-game renderer, slowly turning (showroom preview).
class VehiclePreview extends StatefulWidget {
  final VehicleDef vehicle;
  const VehiclePreview(this.vehicle, {super.key});

  @override
  State<VehiclePreview> createState() => _VehiclePreviewState();
}

class _VehiclePreviewState extends State<VehiclePreview> with SingleTickerProviderStateMixin {
  late final Ticker _ticker;
  double _t = 0;

  @override
  void initState() {
    super.initState();
    _ticker = createTicker((e) => setState(() => _t = e.inMicroseconds / 1e6))..start();
  }

  @override
  void dispose() {
    _ticker.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) => CustomPaint(painter: _PreviewPainter(widget.vehicle, _t));
}

class _PreviewPainter extends CustomPainter {
  final VehicleDef v;
  final double t;
  _PreviewPainter(this.v, this.t);

  @override
  void paint(Canvas canvas, Size size) {
    final car = Car(
      name: v.name, color: v.color, isPlayer: false, driver: _NoDriver(), body: v.body,
      stats: computeStats(v, const [0, 0, 0, 0, 0]), position: Vector2.zero(), angle: t * 0.6,
    );
    canvas.save();
    canvas.translate(size.width / 2, size.height / 2);
    final scale = size.shortestSide / 6;
    canvas.scale(scale);
    canvas.drawCircle(Offset.zero, 2.7, Paint()..color = const Color(0x22FFFFFF));
    CarView(car).render(canvas);
    canvas.restore();
  }

  @override
  bool shouldRepaint(covariant _PreviewPainter old) => true;
}

/// Buy vehicles and upgrades.
class GarageScreen extends StatefulWidget {
  const GarageScreen({super.key});

  @override
  State<GarageScreen> createState() => _GarageScreenState();
}

class _GarageScreenState extends State<GarageScreen> {
  late int _index = vehicles.indexOf(Profile.selected);

  VehicleDef get v => vehicles[_index];

  void _browse(int d) => setState(() => _index = (_index + d + vehicles.length) % vehicles.length);

  @override
  Widget build(BuildContext context) {
    final owned = Profile.owns(v);
    final selected = Profile.selected == v;
    final stats = owned ? Profile.statsFor(v) : computeStats(v, const [0, 0, 0, 0, 0]);
    final wide = MediaQuery.of(context).size.width > 900;

    final preview = Column(mainAxisSize: MainAxisSize.min, children: [
      Row(mainAxisSize: MainAxisSize.min, children: [
        GameButton('<', width: 60, height: 60, onPressed: () => _browse(-1)),
        SizedBox(width: 260, child: Center(child: Text(v.name, style: title(40)))),
        GameButton('>', width: 60, height: 60, onPressed: () => _browse(1)),
      ]),
      Text('${_index + 1} / ${vehicles.length}', style: body(14, AppColors.dim)),
      SizedBox(width: 280, height: 280, child: VehiclePreview(v)),
      SizedBox(width: 420, child: Text(v.description, textAlign: TextAlign.center, style: body(16, AppColors.dim))),
    ]);

    final details = SizedBox(
      width: 520,
      child: GamePanel(
        child: Column(mainAxisSize: MainAxisSize.min, children: [
          _stat('SPEED', stats.topSpeed / 46),
          _stat('ACCEL', stats.acceleration / 33),
          _stat('HANDLING', stats.turnSpeed / 215),
          _stat('ARMOR', stats.armor / 200),
          _stat('FIREPOWER', stats.missiles * stats.missileDamage / (8 * 37)),
          const SizedBox(height: 6),
          Text('PER LAP:  ${stats.missiles} MISSILES   ${stats.mines} MINES   ${stats.boosts} NITRO', style: body(14)),
          const SizedBox(height: 10),
          if (owned)
            for (final t in UpgradeType.values) _upgradeRow(t)
          else
            Padding(
              padding: const EdgeInsets.all(16),
              child: Text('UPGRADES UNLOCK AFTER PURCHASE', style: body(16, AppColors.dim)),
            ),
          const SizedBox(height: 12),
          if (!owned)
            GameButton.primary(
              Profile.money >= v.price ? 'BUY  ${money(v.price)}' : 'NEED  ${money(v.price)}',
              width: 360,
              onPressed: Profile.money >= v.price ? () => setState(() => Profile.buy(v)) : null,
            )
          else if (!selected)
            GameButton.primary('SELECT', width: 360, onPressed: () => setState(() => Profile.select(v)))
          else
            GameButton('SELECTED FOR RACING', width: 360, color: AppColors.good, textColor: AppColors.dark, onPressed: null),
        ]),
      ),
    );

    return Scaffold(
      body: MenuBackdrop(
        child: Stack(children: [
          Positioned(left: 24, top: 16, child: Text('GARAGE', style: title(48, AppColors.accent))),
          Positioned(right: 24, top: 16, child: BankChip(Profile.money)),
          Positioned(left: 24, bottom: 16, child: GameButton('BACK', width: 180, onPressed: () => Navigator.of(context).pop())),
          Center(
            child: SingleChildScrollView(
              padding: const EdgeInsets.only(top: 80, bottom: 90),
              child: wide
                  ? Row(mainAxisSize: MainAxisSize.min, children: [preview, const SizedBox(width: 30), details])
                  : Column(mainAxisSize: MainAxisSize.min, children: [preview, details]),
            ),
          ),
        ]),
      ),
    );
  }

  Widget _stat(String label, double value) => Padding(
        padding: const EdgeInsets.symmetric(vertical: 3),
        child: Row(children: [
          SizedBox(width: 120, child: Text(label, style: body(15))),
          Expanded(
            child: Container(
              height: 14,
              decoration: BoxDecoration(color: AppColors.panelLight, borderRadius: BorderRadius.circular(7)),
              alignment: Alignment.centerLeft,
              child: FractionallySizedBox(
                widthFactor: value.clamp(0.03, 1.0),
                child: Container(
                  decoration: BoxDecoration(
                    color: Color.lerp(AppColors.accent, AppColors.hot, value.clamp(0.0, 1.0)),
                    borderRadius: BorderRadius.circular(7),
                  ),
                ),
              ),
            ),
          ),
        ]),
      );

  Widget _upgradeRow(UpgradeType t) {
    final level = Profile.level(v, t);
    final price = upgradePrice(t, level);
    final maxed = price < 0;
    final canAfford = !maxed && Profile.money >= price;
    return Container(
      margin: const EdgeInsets.symmetric(vertical: 3),
      padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 6),
      decoration: BoxDecoration(color: AppColors.panelLight, borderRadius: BorderRadius.circular(12)),
      child: Row(children: [
        Expanded(
          child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
            Text(upgradeNames[t.index], style: title(18)),
            Text(upgradeEffects[t.index], style: body(11, AppColors.dim)),
          ]),
        ),
        for (int i = 0; i < maxUpgradeLevel; i++)
          Container(
            width: 14,
            height: 14,
            margin: const EdgeInsets.all(3),
            decoration: BoxDecoration(shape: BoxShape.circle, color: i < level ? AppColors.accent : AppColors.panelLight),
          ),
        const SizedBox(width: 8),
        GameButton(
          maxed ? 'MAX' : money(price),
          width: 120,
          height: 44,
          fontSize: 18,
          color: canAfford ? AppColors.accent : AppColors.button,
          textColor: canAfford ? AppColors.dark : AppColors.dim,
          onPressed: canAfford ? () => setState(() => Profile.upgrade(v, t)) : null,
        ),
      ]),
    );
  }
}
