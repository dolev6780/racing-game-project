import 'dart:ui' show Color;

enum UpgradeType { engine, tires, armor, weapons, nitro }

enum BodyStyle { truck, bike }

/// Base stats of a vehicle you can buy.
class VehicleDef {
  final String id, name, description;
  final Color color;
  final BodyStyle body;
  final int price;
  final double topSpeed, acceleration, turnSpeed, grip, armor;
  final int missiles, mines, boosts;

  const VehicleDef({
    required this.id,
    required this.name,
    required this.description,
    required this.color,
    this.body = BodyStyle.truck,
    required this.price,
    required this.topSpeed,
    required this.acceleration,
    required this.turnSpeed,
    required this.grip,
    required this.armor,
    required this.missiles,
    required this.mines,
    required this.boosts,
  });
}

/// Final stats after upgrades.
class CarStats {
  final double topSpeed, acceleration, turnSpeed, grip, armor, missileDamage, boostDuration;
  final int missiles, mines, boosts;

  const CarStats({
    required this.topSpeed,
    required this.acceleration,
    required this.turnSpeed,
    required this.grip,
    required this.armor,
    required this.missiles,
    required this.mines,
    required this.boosts,
    this.missileDamage = 25,
    this.boostDuration = 1.6,
  });
}

const int maxUpgradeLevel = 4;

const List<VehicleDef> vehicles = [
  VehicleDef(
    id: 'rumbler', name: 'RUMBLER', description: 'Trusty all-rounder. Every legend starts somewhere.',
    color: Color(0xFFFFCC26), price: 0,
    topSpeed: 32, acceleration: 18, turnSpeed: 130, grip: 6, armor: 100, missiles: 3, mines: 2, boosts: 2,
  ),
  VehicleDef(
    id: 'bruiser', name: 'BRUISER', description: 'Heavy armor and extra mines. Slow, but hard to wreck.',
    color: Color(0xFF4DD966), price: 2500,
    topSpeed: 30, acceleration: 16, turnSpeed: 118, grip: 7, armor: 150, missiles: 3, mines: 3, boosts: 2,
  ),
  VehicleDef(
    id: 'viper', name: 'VIPER', description: 'Fast and twitchy with an extra nitro. Thin armor.',
    color: Color(0xFFFF4D33), price: 4500,
    topSpeed: 36, acceleration: 21, turnSpeed: 140, grip: 5.5, armor: 85, missiles: 3, mines: 2, boosts: 3,
  ),
  VehicleDef(
    id: 'phantom', name: 'PHANTOM', description: 'Elite combat truck. Strong in every area, extra missile.',
    color: Color(0xFFA666FF), price: 8000,
    topSpeed: 35, acceleration: 20, turnSpeed: 136, grip: 6.5, armor: 120, missiles: 4, mines: 2, boosts: 2,
  ),
  VehicleDef(
    id: 'hellcat', name: 'HELLCAT', description: "Insane speed and handling, lots of nitro. One mistake and you're toast.",
    color: Color(0xFF4DCCFF), body: BodyStyle.bike, price: 12000,
    topSpeed: 39, acceleration: 24, turnSpeed: 160, grip: 5, armor: 65, missiles: 2, mines: 1, boosts: 4,
  ),
];

const List<List<int>> _upgradePrices = [
  [600, 1200, 2000, 3200], // engine
  [500, 1000, 1700, 2600], // tires
  [500, 1000, 1700, 2600], // armor
  [700, 1400, 2200, 3300], // weapons
  [600, 1200, 2000, 3000], // nitro
];

const List<String> upgradeNames = ['ENGINE', 'TIRES', 'ARMOR', 'WEAPONS', 'NITRO'];
const List<String> upgradeEffects = [
  '+5% top speed, +8% acceleration',
  '+7% steering, +10% grip',
  '+15% armor',
  '+1 missile, +6% damage (+1 mine at lv 2/4)',
  '+0.15s nitro (+1 nitro at lv 1/3)',
];

VehicleDef vehicleById(String id) => vehicles.firstWhere((v) => v.id == id, orElse: () => vehicles.first);

/// Price of the next level, or -1 when maxed.
int upgradePrice(UpgradeType type, int currentLevel) =>
    currentLevel >= maxUpgradeLevel ? -1 : _upgradePrices[type.index][currentLevel];

CarStats computeStats(VehicleDef v, List<int> levels) {
  final engine = levels[0], tires = levels[1], armor = levels[2], weapons = levels[3], nitro = levels[4];
  return CarStats(
    topSpeed: v.topSpeed * (1 + 0.05 * engine),
    acceleration: v.acceleration * (1 + 0.08 * engine),
    turnSpeed: v.turnSpeed * (1 + 0.07 * tires),
    grip: v.grip * (1 + 0.1 * tires),
    armor: v.armor * (1 + 0.15 * armor),
    missiles: v.missiles + weapons,
    mines: v.mines + weapons ~/ 2,
    boosts: v.boosts + (nitro + 1) ~/ 2,
    missileDamage: 25 * (1 + 0.06 * weapons),
    boostDuration: 1.6 + 0.15 * nitro,
  );
}
