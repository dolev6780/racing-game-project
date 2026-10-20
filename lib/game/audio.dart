import 'package:flame_audio/flame_audio.dart';
import 'package:flutter/foundation.dart';

import '../data/profile.dart';

/// Engine loop (pitch follows speed) and one-shot sound effects. Fails silently where audio is unavailable.
class GameAudio {
  static bool enabled = true; // tests switch this off
  static bool _cached = false;
  AudioPlayer? _engine;
  double _rateTimer = 0, _lastRate = 1;

  double get _volume => Profile.volume;

  static Future<void> preload() async {
    if (!enabled || _cached) return;
    try {
      await FlameAudio.audioCache.loadAll(
          ['engine.ogg', 'impact.ogg', 'skid.ogg', 'beep_low.wav', 'beep_high.wav', 'lap.wav', 'blip.wav']);
      _cached = true;
    } catch (e) {
      debugPrint('audio preload failed: $e');
    }
  }

  Future<void> startEngine() async {
    if (!enabled || _volume <= 0) return;
    try {
      _engine = await FlameAudio.loop('engine.ogg', volume: _volume * 0.35);
    } catch (e) {
      debugPrint('engine audio failed: $e');
    }
  }

  void updateEngine(double speed01, bool boosting, double dt) {
    final e = _engine;
    if (e == null) return;
    _rateTimer -= dt;
    if (_rateTimer > 0) return;
    _rateTimer = 0.12;
    final rate = 0.7 + speed01 * 1.0 + (boosting ? 0.25 : 0);
    if ((rate - _lastRate).abs() < 0.03) return;
    _lastRate = rate;
    e.setPlaybackRate(rate).catchError((_) {});
  }

  void pauseEngine(bool paused) {
    final e = _engine;
    if (e == null) return;
    (paused ? e.pause() : e.resume()).catchError((_) {});
  }

  void play(String file, [double volume = 1]) {
    if (!enabled || _volume <= 0) return;
    FlameAudio.play(file, volume: (volume * _volume).clamp(0.0, 1.0)).then((_) {}, onError: (_) {});
  }

  void dispose() {
    final e = _engine;
    _engine = null;
    if (e != null) {
      e.stop().then((_) => e.dispose(), onError: (_) {});
    }
  }
}
