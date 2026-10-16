# Racing Game Project 🏁🎸

A 3D **combat racing** game for **Android & iOS**, inspired by the classic *Rock n' Roll Racing*:
armed cars, missiles, mines, nitro, money, alien-planet tracks.

Built with **Unity 6 + C#**.

## Status: v0.7 — kart-racer graphics & shaders

Cartoon look inspired by kart racers: custom toon/outline shaders, cartoon sky, item boxes, boost pads, drift sparks + mini-turbo.
Trophy Room with league cups, 26 achievements (with cash rewards), career stats and lap records.
Championship mode: 4 leagues x 4 races with points, season standings and unlocks; 4 themed tracks.
Garage with 5 vehicles to buy and 5 upgrade types (4 levels each), shown in a 3D showroom.
Main menu with 3D backdrop, race setup (laps / opponents / difficulty), settings, pause menu,
and a new HUD (speedometer, standings, kill feed, results screen).
Low-poly 3D trucks, finish arch, forests and pit tents (Kenney, CC0), engine/skid/crash sounds.
The track itself is generated from code, so you can open the project and press **Play** right away.
If the 3D models can't load, the game falls back to placeholder block cars automatically.

| Feature | Status |
|---|---|
| Arcade car physics (drift, boost, no flipping) | ✅ |
| Procedural track: road, walls, start gantry, scenery | ✅ |
| 5 AI opponents (racing line, corner braking, weapons, stuck recovery) | ✅ |
| Weapons: missiles (aim-assist), mines, nitro — refill every lap | ✅ |
| Armor, ramming damage, wreck & respawn | ✅ |
| Laps, positions, wrong-way, countdown, results | ✅ |
| 4 tracks: Green Valley, Desert Canyon, Frost Peak, Alien Wastes (own colors, fog, scenery) | ✅ |
| Championship: 4 leagues, points (10-7-5-3-2-1), season standings, win bonus, unlock next league | ✅ |
| 26 achievements with cash rewards + unlock pop-ups | ✅ |
| Trophy Room: league cups (gold/silver/bronze), achievement progress, career stats, best laps | ✅ |
| Lap timing, personal best laps per track, "NEW LAP RECORD" alerts | ✅ |
| Custom shaders: toon/cel + outline, cartoon sky, boost pad, rainbow item box, spark particles | ✅ |
| Rainbow "?" item boxes (random missile / mine / nitro / armor / cash) | ✅ |
| Boost pads, drift sparks (blue → orange → purple) and mini-turbo | ✅ |
| Close chase camera with speed/boost FOV kick (isometric still available) | ✅ |
| Money: prize by place + kills + pickups (saved) | ✅ |
| Armor & money pickups on track | ✅ |
| Isometric camera (classic) + chase camera | ✅ |
| Touch controls (auto-accelerate) + keyboard | ✅ |
| Phone steering: floating joystick / tilt / buttons | ✅ |
| Main menu: race setup, settings, bank | ✅ |
| HUD: position, laps, speedometer, armor, standings, kill feed, damage flash | ✅ |
| Pause menu & results screen with prize breakdown | ✅ |
| Difficulty levels (Easy / Normal / Hard, harder pays more) | ✅ |
| Garage: 5 vehicles (Rumbler, Bruiser, Viper, Phantom, Hellcat bike) | ✅ |
| Upgrades: Engine, Tires, Armor, Weapons, Nitro (4 levels each, per vehicle) | ✅ |
| Real 3D car models, finish arch, forest scenery (Kenney) | ✅ |
| Engine / skid / crash / explosion sounds | ✅ |
| Music & announcer | ⏳ |
| Multiplayer | ⏳ |

## How to open & run

1. Install **Unity Hub** and **Unity 6 LTS (6000.0.x)** with **Android Build Support** (and **iOS Build Support** on a Mac).
2. Unity Hub → **Add → Add project from disk** → select this folder.
   (If Hub asks about a different editor version, pick any Unity 6 version.)
3. If Unity asks to enable the new Input System backends, click **Yes** (the code supports both input systems).
   The project uses the **glTFast** package (`com.unity.cloud.gltfast`) to import the `.glb` 3D models; Unity installs it automatically.
   If it reports a package version error, open **Window → Package Manager → + → Add package by name…** and enter `com.unity.cloud.gltfast`.
4. Menu **Racing Game → Create Game Scene (Main Menu)**, then press **Play**.
   *(Pressing Play in any empty scene also works — the menu builds itself.)*
   For quick testing, **Racing Game → Create Quick Race Scene (dev)** skips the menu.

### Controls

| Action | Keyboard | Touch |
|---|---|---|
| Steer | A/D or ←/→ | Joystick (drag on left side), Tilt, or `<` `>` buttons |
| Gas / Brake | W/S or ↑/↓ | auto-gas, `BRAKE` button (or pull joystick down) |
| Fire missile | Space / J | `FIRE` |
| Drop mine | E / K | `MINE` |
| Nitro | Left Shift / L | `NITRO` |
| Switch camera | C | `CAM` button |
| Pause | Esc / P | `II` button (top centre) |
| Restart race | R (after finish) | `RACE AGAIN` / pause menu |

**Phone steering modes** — choose in **Settings** (main menu or pause menu); your choice is saved:
- **STICK** (default): put your thumb anywhere on the left side of the screen and drag left/right. The joystick appears where you touch.
- **TILT**: turn the phone like a steering wheel. Tilt is centred on how you hold the phone when the race starts;
  set **INVERT TILT** in Settings if left/right feel reversed.
- **BTNS**: classic `<` `>` buttons.

In the editor, turn on **Settings → TOUCH CONTROLS** to preview the phone controls (use the mouse as your finger).

## Compile check (no Unity needed)

`Tools/CompileCheck/check.sh` compiles every script against Unity's API using the .NET SDK
(old Input Manager, new Input System, and editor scripts). Run it before committing to catch compile errors:

```
Tools/CompileCheck/check.sh      # prints PASS/FAIL per pass, with any errors
```

## Automated tests (Unity)

`Assets/Tests/PlayMode/GameSmokeTests.cs` starts the real game in play mode and checks that:
models/sounds/fonts load, all 4 tracks build, the main menu starts, a full race runs to the finish
(every car drives, results screen appears, back to menu works) and a championship race records points.
Any exception or error log fails the test.

- **In the editor:** Window → General → Test Runner → PlayMode → Run All.
  (Like playing, this changes your saved money/stats.)
- **On GitHub (automatic):** `.github/workflows/ci.yml` runs on every push:
  1. **Compile check** — always, no license needed.
  2. **Unity play mode tests** and 3. **Android APK build** — need your Unity license as repository secrets:

  | Secret | Value |
  |---|---|
  | `UNITY_EMAIL` | your Unity account email |
  | `UNITY_PASSWORD` | your Unity account password |
  | `UNITY_LICENSE` | full contents of your license file `Unity_lic.ulf` |

  Get `Unity_lic.ulf` by signing into Unity Hub on your computer and activating a free Personal license, then open:
  Windows `C:\ProgramData\Unity\Unity_lic.ulf`, macOS `/Library/Application Support/Unity/Unity_lic.ulf`,
  Linux `~/.local/share/unity3d/Unity/Unity_lic.ulf`.
  Add the secrets in GitHub: repo **Settings → Secrets and variables → Actions → New repository secret**.
  The APK appears under the workflow run's **Artifacts** (download, unzip, install on Android).

## Building for phones

- **Android:** File → Build Profiles → Android → Switch Platform → Build (APK) or Build And Run with your phone connected (USB debugging on).
- **iOS:** Requires a Mac with Xcode. File → Build Profiles → iOS → Build, then open the generated Xcode project and run it on your device.
- Recommended Player settings: Default Orientation = *Landscape Left* / Auto Rotation (landscape only).

## Code structure

```
Assets/Scripts/
  Core/       GameBootstrap (auto-start), GameFlow (menu <-> race), InputBridge, ModelLibrary (models+audio), MaterialFactory, Explosion
  Track/      TrackLibrary (layouts + themes), TrackBuilder (procedural track), TrackPath (centerline/progress), Pickup
  Car/        CarController (physics), CarHealth, RacerProgress (laps),
              PlayerCarInput (keyboard+touch), AICarInput, CarBuilder, CarAudio
  Progress/   Stats, Records (best laps), Achievements
  Garage/     VehicleCatalog, PlayerGarage (owned/selected/upgrades), Wallet
  Weapons/    WeaponSystem, Missile, Mine
  Race/       RaceManager (countdown, laps, standings, pause, prize money), RaceRequest, RaceConfig, Championship
  CameraRig/  RaceCamera (isometric / chase), MenuCamera (orbit)
  UI/         UIKit (code-built uGUI), MainMenu, ChampionshipScreen, GarageScreen, TrophyRoom, AchievementToast, SettingsPanel, RaceHUD, TouchControlsView
  Editor/     RacingGameMenu (scene creation tools)
```

3D models live in `Assets/Resources/Models` and sounds in `Assets/Resources/Audio` (see [CREDITS.md](CREDITS.md)).
To use your own car model, drop a `.glb` there with children named `body` and `wheel-front-left`, `wheel-front-right`,
`wheel-back-left`, `wheel-back-right` (pivot at each wheel's centre), and pass its name to `CarBuilder.Create`.

### Championship
**CHAMPIONSHIP** in the main menu. Each league is a season of 4 races; finishing positions earn points
(10, 7, 5, 3, 2, 1). Between races you can visit the garage. Win the season to unlock the next league:

| League | Laps | Prizes | Win bonus | Opponents |
|---|---|---|---|---|
| Rookie League | 3 | x1 | $3,000 | slow, timid |
| Pro League | 4 | x2 | $6,000 | faster, more aggressive |
| Elite League | 4 | x3.5 | $10,000 | fast, armored |
| Riot Masters | 5 | x5 | $20,000 | top speed of an upgraded car |

Leagues and AI strength: `Assets/Scripts/Race/Championship.cs`. Tracks and themes: `Assets/Scripts/Track/TrackLibrary.cs`.

### Graphics & shaders
Custom shaders live in `Assets/Resources/Shaders` (built-in render pipeline):

| Shader | Used for |
|---|---|
| `RacingGame/Toon` | Everything solid: banded cel lighting with blue-tinted shadows, crisp highlight, rim light, optional outline (`_OutlineWidth`) |
| `RacingGame/SkyGradient` | Procedural skybox: gradient, glowing sun, drifting cartoon clouds |
| `RacingGame/BoostPad` | Scrolling glowing chevrons |
| `RacingGame/ItemBox` | Rainbow see-through "?" boxes with fresnel shimmer |
| `RacingGame/Sparks` | Additive glow for drift sparks and boost flames |

`Core/ToonStyle.cs` builds the materials (and the striped-grass / asphalt textures) at runtime;
if a shader is missing the game falls back to Unity's default material.

**Drifting:** steer hard at speed to slide; keep it up and the sparks go blue → orange → purple.
Straighten out to release a mini-turbo (longer for higher tiers).

### Achievements & trophies
Defined in `Assets/Scripts/Progress/Achievements.cs` (stats in `Stats.cs`). Each pays a cash reward when unlocked.
Categories: Racing (wins, streaks, comebacks, top speed, every track), Combat (wrecks, rampage, missiles, mines, nitro),
Career (pickups, upgrades, vehicles, savings) and Championship (win each league).
League cups in the Trophy Room are gold / silver / bronze for your best season finish in that league.

### Garage & economy
Vehicles, prices and upgrade effects are defined in `Assets/Scripts/Garage/Garage.cs` (`VehicleCatalog`).
Win races to earn money (prize by place x difficulty, $100 per wreck, $100 per money pickup),
then buy vehicles and upgrades in the **GARAGE**. Upgrades belong to each vehicle.
Use **Racing Game → Reset Saved Progress** in the editor to start over.

Tuning lives in public fields (e.g. `CarController.maxSpeed`, `WeaponSystem.missilesPerLap`,
`RaceManager.totalLaps`) and track layouts in `TrackBuilder.DefaultLayout`.
