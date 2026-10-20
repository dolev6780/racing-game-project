# Riot Racers (Flutter + Flame) 🏁🎸

**2.5D isometric combat racing** for **Android, iOS and the web** — a modern take on
*Rock n' Roll Racing*, seen from above and from the side at the same time. Built with **Flutter 3.47 + Flame 1.38**.

▶️ **Play in the browser:** https://dolev6780.github.io/racing-game-project/ (republished automatically on every push to `flutter-flame`)

This is the Flutter/Flame version of the Unity prototype in
[racing-game-project](https://github.com/dolev6780/racing-game-project) — same tracks, cars, weapons,
garage, championship and achievements — rendered in **2.5D**: an isometric ground plane plus real 3D shapes
(cars, walls, trees, rocks, crystals, item boxes, finish arch) projected and depth-sorted, with per-face lighting,
cartoon outlines, soft shadows, drifting cloud shadows and 3D particles.

## Features
| | |
|---|---|
| 4 tracks | Green Valley, Desert Canyon, Frost Peak, Alien Wastes (own colours & scenery) |
| Arcade physics | grip, drifting, **drift sparks + mini-turbo** (blue → orange → purple), boost |
| Combat | missiles (aim assist), mines, nitro, armor, wrecks & respawn, ramming damage |
| Track items | rainbow **"?" item boxes** (random missile/mine/nitro/armor/cash), **boost pads** |
| 5 AI opponents | racing line, corner braking, weapons, stuck recovery, rubber-banding |
| HUD | position (gold/silver/bronze), laps, time, best lap, speedometer, armor, **minimap**, standings, kill feed, start lights, speed lines, damage flash |
| Camera | fixed **isometric** angle like the original, follows with look-ahead, zooms out with speed, screen shake (close / wide) |
| Garage | 5 vehicles (incl. Hellcat bike) × 5 upgrades × 4 levels |
| Championship | 4 leagues × 4 races, points 10-7-5-3-2-1, unlock the next league |
| Trophies | 26 achievements with cash rewards, league cups, career stats, best laps |
| Controls | keyboard, or touch: ◀ ▶ steering (or a point-where-to-go stick) + GAS, REVERSE, FIRE, MINE, NITRO |
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
| Camera zoom (close / wide) | C |
| Pause | Esc / P |

Touch controls appear automatically on phones and tablets (toggle in Settings). Left thumb steers,
right thumb holds **GAS** (or **REV** to brake and back up). Settings → STEERING switches between
**ARROWS** (◀ ▶ turn the car) and **STICK** (point the stick where you want to go on screen and the car
turns that way, which suits the angled camera).

The web build caps the pixel density at 1.5x so phones stay smooth; `?perf=1` in the URL logs frame times.

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
  game/   race_game.dart (Flame game, camera, events, audio)  iso.dart (projection + 3D prism drawing)
          iso_scene.dart (ground, walls, scenery, items, depth sorting)  car_view.dart (3D block cars)  effects_layer.dart
  data/   vehicles, championship, achievements, profile (saved progress), career (post-race bookkeeping)
  ui/     menu (live AI race background), quick race, garage, championship, trophies, settings,
          race_screen (HUD, pause, results), touch_controls, painters (minimap, gauge, speed lines)
```

Dev shortcut (web): `?screen=race&track=1&laps=1&autopilot=1` opens a race directly (also `garage`, `champ`, `trophies`).

## Credits
- Sound effects: Kenney Starter Kit Racing (CC0) — https://kenney.nl
- Fonts: Russo One, Rubik (SIL Open Font License) — see `licenses/`
