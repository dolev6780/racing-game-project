import 'dart:math' as math;
import 'dart:ui';

import 'package:flame/components.dart';
import 'package:vector_math/vector_math.dart' as vm;

import '../sim/car.dart';
import '../sim/race.dart';

class _Particle {
  double x, y, vx, vy, life, maxLife, size, grow;
  Color color;
  bool additive;
  _Particle(this.x, this.y, this.vx, this.vy, this.life, this.size, this.color, {this.grow = 0, this.additive = true})
      : maxLife = life;
}

class _Blast {
  final double x, y, size;
  double age = 0;
  _Blast(this.x, this.y, this.size);
}

class _Skid {
  final double x1, y1, x2, y2;
  double age = 0;
  _Skid(this.x1, this.y1, this.x2, this.y2);
}

/// Explosions, particles (drift sparks, boost flames, smoke, debris) and skid marks.
class EffectsLayer extends Component {
  final Race race;
  final List<_Particle> _particles = [];
  final List<_Blast> _blasts = [];
  final List<_Skid> _skids = [];
  final Map<Car, (vm.Vector2, vm.Vector2)> _lastWheels = {};
  final math.Random _rng = math.Random(5);

  static const List<Color> tierColors = [Color(0xFFFFF5CC), Color(0xFF4DA6FF), Color(0xFFFF8C1A), Color(0xFFD94DFF)];

  EffectsLayer(this.race) : super(priority: 5);

  /// Skid marks render under the cars; everything else above (see [EffectsOverlay]).
  void explode(vm.Vector2 at, double size) {
    _blasts.add(_Blast(at.x, at.y, size));
    for (int i = 0; i < (8 + size * 2).round(); i++) {
      final a = _rng.nextDouble() * math.pi * 2, sp = 6 + _rng.nextDouble() * 12;
      _particles.add(_Particle(at.x, at.y, math.cos(a) * sp, math.sin(a) * sp, 0.3 + _rng.nextDouble() * 0.4, 0.25 + _rng.nextDouble() * 0.3,
          _rng.nextBool() ? const Color(0xFFFFB02E) : const Color(0xFFFFE680)));
    }
    for (int i = 0; i < 6; i++) {
      final a = _rng.nextDouble() * math.pi * 2;
      _particles.add(_Particle(at.x, at.y, math.cos(a) * 2, math.sin(a) * 2, 1.0 + _rng.nextDouble() * 0.6, size * 0.25,
          const Color(0xAA555560), grow: size * 0.4, additive: false));
    }
  }

  void sparks(vm.Vector2 at, Color color, int count) {
    for (int i = 0; i < count; i++) {
      final a = _rng.nextDouble() * math.pi * 2, sp = 3 + _rng.nextDouble() * 6;
      _particles.add(_Particle(at.x, at.y, math.cos(a) * sp, math.sin(a) * sp, 0.25 + _rng.nextDouble() * 0.2, 0.2, color));
    }
  }

  @override
  void update(double dt) {
    final step = dt.clamp(0.0, 0.05);
    for (final c in race.cars) {
      _emitForCar(c, step);
    }
    for (final m in race.missiles) {
      if (_rng.nextDouble() < 0.7) {
        final back = m.position - m.direction * 0.9;
        _particles.add(_Particle(back.x, back.y, 0, 0, 0.6, 0.35, const Color(0x99D9D9E0), grow: 0.9, additive: false));
      }
    }
    for (final p in _particles) {
      p.life -= step;
      p.x += p.vx * step;
      p.y += p.vy * step;
      p.vx *= 1 - 2.5 * step;
      p.vy *= 1 - 2.5 * step;
      p.size += p.grow * step;
    }
    _particles.removeWhere((p) => p.life <= 0);
    if (_particles.length > 900) _particles.removeRange(0, _particles.length - 900);
    for (final b in _blasts) {
      b.age += step;
    }
    _blasts.removeWhere((b) => b.age > 0.55);
    for (final s in _skids) {
      s.age += step;
    }
    _skids.removeWhere((s) => s.age > 5);
    if (_skids.length > 1500) _skids.removeRange(0, _skids.length - 1500);
  }

  void _emitForCar(Car c, double dt) {
    final rear = c.position - c.forward * 1.3;
    final l = rear - c.right * 0.95, r = rear + c.right * 0.95;
    final marking = !c.wrecked && (c.drifting || c.lateralSpeed.abs() > 6);
    final prev = _lastWheels[c];
    if (marking && prev != null && prev.$1.distanceTo(l) < 3) {
      _skids.add(_Skid(prev.$1.x, prev.$1.y, l.x, l.y));
      _skids.add(_Skid(prev.$2.x, prev.$2.y, r.x, r.y));
    }
    if (marking) {
      _lastWheels[c] = (l, r);
    } else {
      _lastWheels.remove(c);
    }

    if (c.wrecked) return;
    if (c.drifting) {
      final color = tierColors[c.driftTier.clamp(0, 3)];
      final rate = c.driftTier == 0 ? 0.3 : 0.8;
      for (final w in [l, r]) {
        if (_rng.nextDouble() < rate) {
          final back = -c.forward * (3 + _rng.nextDouble() * 4) + c.right * ((_rng.nextDouble() - 0.5) * 4);
          _particles.add(_Particle(w.x, w.y, back.x, back.y, 0.2 + _rng.nextDouble() * 0.2, 0.22, color));
        }
      }
    }
    if (c.boosting && _rng.nextDouble() < 0.8) {
      final ex = c.position - c.forward * 2.2;
      final back = -c.forward * 8;
      _particles.add(_Particle(ex.x, ex.y, back.x + (_rng.nextDouble() - 0.5) * 2, back.y + (_rng.nextDouble() - 0.5) * 2, 0.25,
          0.45, const Color(0xFFFF9A2E)));
    }
  }

  /// Skid marks (drawn under the cars).
  @override
  void render(Canvas canvas) {
    final c = canvas;
    final paint = Paint()
      ..strokeWidth = 0.32
      ..strokeCap = StrokeCap.round;
    for (final s in _skids) {
      paint.color = Color.fromRGBO(20, 20, 26, 0.45 * (1 - s.age / 5));
      c.drawLine(Offset(s.x1, s.y1), Offset(s.x2, s.y2), paint);
    }
  }

  /// Particles and explosions (drawn above the cars by [EffectsOverlay]).
  void renderTop(Canvas c) {
    final add = Paint()..blendMode = BlendMode.plus;
    final normal = Paint();
    for (final p in _particles) {
      final t = (p.life / p.maxLife).clamp(0.0, 1.0);
      final paint = p.additive ? add : normal;
      paint.color = p.color.withValues(alpha: p.color.a * t);
      c.drawCircle(Offset(p.x, p.y), p.size * (p.additive ? (0.5 + 0.5 * t) : 1), paint);
    }
    for (final b in _blasts) {
      final t = b.age / 0.55;
      final r = b.size * (0.4 + t * 0.9);
      final a = (1 - t).clamp(0.0, 1.0);
      c.drawCircle(Offset(b.x, b.y), r, Paint()..color = Color.fromRGBO(255, 90, 30, 0.85 * a));
      c.drawCircle(Offset(b.x, b.y), r * 0.7, Paint()..color = Color.fromRGBO(255, 170, 40, 0.9 * a));
      c.drawCircle(Offset(b.x, b.y), r * 0.4 * (1 - t), Paint()..color = Color.fromRGBO(255, 250, 200, a));
    }
  }
}

/// Draws [EffectsLayer.renderTop] above the cars.
class EffectsOverlay extends Component {
  final EffectsLayer effects;
  EffectsOverlay(this.effects) : super(priority: 30);

  @override
  void render(Canvas canvas) => effects.renderTop(canvas);
}
