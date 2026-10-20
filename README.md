# Riot Racers (Flutter + Flame) 🏁🎸

Top-down **combat racing** for **Android, iOS and the web**, inspired by *Rock n' Roll Racing*
with kart-racer touches. Built with **Flutter 3.47 + Flame 1.38**.

This is the Flutter/Flame version of the Unity prototype in
[racing-game-project](https://github.com/dolev6780/racing-game-project) — same tracks, cars, weapons,
garage, championship and achievements, but in 2D (Flame is a 2D engine), drawn in a cartoon style.

## Features
| | |
|---|---|
| 4 tracks | Green Valley, Desert Canyon, Frost Peak, Alien Wastes (own colours & scenery) |
| Arcade physics | grip, drifting, **drift sparks + mini-turbo** (blue → orange → purple), boost |
| Combat | missiles (aim assist), mines, nitro, armor, wrecks & respawn, ramming damage |
| Track items | rainbow **"?" item boxes** (random missile/mine/nitro/armor/cash), **boost pads** |
| 5 AI opponents | racing line, corner braking, weapons, stuck recovery, rubber-banding |
| HUD | position (gold/silver/bronze), laps, time, best lap, speedometer, armor, **minimap**, standings, kill feed, start lights, speed lines, damage flash |
| Camera | top-down, **turns with your car** (or north-up), zooms out with speed, screen shake |
| Garage | 5 vehicles (incl. Hellcat bike) × 5 upgrades × 4 levels |
| Championship | 4 leagues × 4 races, points 10-7-5-3-2-1, unlock the next league |
| Trophies | 26 achievements with cash rewards, league cups, career stats, best laps |
| Controls | keyboard, or touch: floating joystick / buttons + FIRE, MINE, NITRO, BRAKE |
| Sound | engine pitch follows speed, impacts, start beeps, lap chime |

## Run it
1. Install Flutter (3.47+): https://docs.flutter.dev/get-started/install
2. `flutter pub get`
3. Run:
   - **Phone (Android):** plug it in with USB debugging on → `flutter run`
   - **Browser:** `flutter run -d chrome`
   - **iPhone:** needs a Mac with Xcode → `flutter run`

### Controls (keyboard)
| Action | Keys |
|---|---|
| Drive / brake | W/S or ↑/↓ |
| Steer | A/D or ←/→ |
| Fire missile | Space / J |
| Drop mine | E / K |
| Nitro | Shift / L |
| Camera (turning / north-up) | C |
| Pause | Esc / P |

Touch controls appear automatically on phones and tablets (toggle in Settings).

## Tests
```
flutter test        # simulates full races on all 4 tracks, combat, upgrades
flutter analyze
```
The race logic lives in `lib/sim/` (pure Dart, no rendering), so whole races are simulated in tests.

## Get the APK without installing anything
Every push runs GitHub Actions (`.github/workflows/ci.yml`): analyze + tests, then builds an
**Android APK** and a **web build**. Open the run under **Actions** → **Artifacts** →
`RiotRacers-android-apk` → unzip → install on your phone (allow "install unknown apps").
No secrets or licenses needed.

## Code map
```
lib/
  sim/    track.dart (layouts, path math)  car.dart (physics, drift)  ai.dart  race.dart (weapons, items, laps, standings)
  game/   race_game.dart (Flame game, camera, events, audio)  track_layer / car_view / entities_layer / effects_layer
  data/   vehicles, championship, achievements, profile (saved progress), career (post-race bookkeeping)
  ui/     menu (live AI race background), quick race, garage, championship, trophies, settings,
          race_screen (HUD, pause, results), touch_controls, painters (minimap, gauge, speed lines)
```

Dev shortcut (web): `?screen=race&track=1&laps=1&autopilot=1` opens a race directly (also `garage`, `champ`, `trophies`).

## Credits
- Sound effects: Kenney Starter Kit Racing (CC0) — https://kenney.nl
- Fonts: Russo One, Rubik (SIL Open Font License) — see `licenses/`
