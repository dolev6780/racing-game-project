# Racing Game Project 🏁🎸

A 3D **combat racing** game for **Android & iOS**, inspired by the classic *Rock n' Roll Racing*:
armed cars, missiles, mines, nitro, money, alien-planet tracks.

Built with **Unity 6 + C#**.

## Status: v2 Phase 1 — URP, isometric camera, new phone controls

v2 work follows the spec ("Riot Racers (Unity) — Game Specification v2"). Phase 1:
the project now runs on the **Universal Render Pipeline (URP)** (set up automatically on first open),
the default camera is a fixed **isometric 3/4 view** (Close / Wide, plus Chase), phone controls use
**◀ ▶ buttons or a point-where-to-go stick, GAS and REV pedals**, gamepads work, the HUD goes compact on phones,
and render resolution adapts to keep phones near 60 fps.

**Phase 2 (branch `v2-phase2`) — karts and look:** every four-wheeled vehicle is now a go-kart built from
toon-shaded shapes (round tyres with white rims and team-coloured hubs, side pods, nose and bumper, twin exhausts
with twin boost flames, and a driver with a big helmet and visor), in races, the menu and the garage.
Tracks get a checkered mowed lawn with flowers (pebbles and sparkles on other planets), yellow chevron boards
on the outside of sharp bends, and pennant flags along the start straight. Badly damaged karts smoke
(new `RacingGame/Smoke` shader).

**Phase 3 (branch `v2-phase3`) — gameplay:**
- **Coins** in lines on the racing line: +1% top speed each (up to 10) and +$10; a missile hit knocks 2 loose,
  a wreck costs half. Counter on the HUD.
- **Special items** from item boxes, one held at a time (`ITEM` button / Q / gamepad LB): **oil slick** (spins
  out karts that drive over it), **shield** (blocks the next hit for 6 s), **shockwave** (hits every kart
  within 8 m). Karts near the back get better odds; the leader never gets a shockwave.
- **Drift button** (optional `DRIFT` touch button in Settings, Ctrl, gamepad RB): loosens grip at speed so
  drifts and mini-turbos are easier.
- **Ramps** (one per track, in one lane): press DRIFT in the air — or tap GAS again on a phone — for a barrel
  roll and a boost on landing.
- **Off-road** is slower (60% top speed).
- **AI personalities**: aggressive (hunts the kart ahead, fires more), defensive (holds the middle, mines and
  shields early), collector (weaves for coins and item boxes).
- Play-mode tests for every item, coins, ramps, drifting, off-road and personalities.

### v0.7 — kart-racer graphics & shaders

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
| Cameras: Isometric Close (default) / Wide (fixed 3/4 view, look-ahead, speed zoom), Chase | ✅ |
| Minimap with live racer dots | ✅ |
| Start lights (red, red, red, GREEN) with synthesized beeps; lap chime; overtake blip | ✅ |
| Game feel: speed lines on boost, camera shake on hits/explosions, skid marks while drifting | ✅ |
| Closer races: AI rubber-banding (behind = up to +7% speed, far ahead = -5%) | ✅ |
| Money: prize by place + kills + pickups (saved) | ✅ |
| Armor & money pickups on track | ✅ |
| Universal Render Pipeline, adaptive render scale for 60 fps on phones | ✅ |
| Touch controls (GAS / REV pedals), keyboard, gamepad | ✅ |
| Phone steering: arrows / point-where-to-go stick / tilt (auto-gas) | ✅ |
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
4. On first open the project switches itself to **URP** (it creates `Assets/Settings/RacingGame_URP.asset`;
   commit the new files under `Assets/Settings`). If anything looks pink, use **Racing Game → Set Up URP**.
5. Menu **Racing Game → Create Game Scene (Main Menu)**, then press **Play**.
   *(Pressing Play in any empty scene also works — the menu builds itself.)*
   For quick testing, **Racing Game → Create Quick Race Scene (dev)** skips the menu.

### Controls

| Action | Keyboard | Touch |
|---|---|---|
| Steer | A/D or ←/→ (gamepad: left stick) | `<` `>` buttons, the stick, or tilt |
| Gas / Brake & reverse | W/S or ↑/↓ (gamepad: right / left trigger) | `GAS` / `REV` pedals (tilt: auto-gas) |
| Fire missile | Space / J (gamepad: A) | `FIRE` |
| Drop mine | E / K (gamepad: B) | `MINE` |
| Nitro | Left Shift / L (gamepad: X) | `NITRO` |
| Special item (oil / shield / shockwave) | Q (gamepad: LB) | `ITEM` |
| Drift / ramp trick | Ctrl (gamepad: RB) | `DRIFT` (optional) / tap `GAS` in the air |
| Switch camera (Close / Wide / Chase) | C (gamepad: Y) | `VIEW` button |
| Pause | Esc / P (gamepad: Start) | `II` button (top centre) |
| Restart race | R (after finish) | `RACE AGAIN` / pause menu |

**Phone steering modes** — choose in **Settings** (main menu or pause menu); your choice is saved:
- **ARROWS** (default): big `<` `>` buttons for the left thumb; the right thumb holds `GAS` or `REV`.
- **STICK**: touch anywhere on the left side and point where you want to go on screen; the kart turns that way
  (with REV held it backs toward it). With the chase camera the stick steers left/right.
- **TILT**: turn the phone like a steering wheel; the kart accelerates by itself, `REV` brakes and reverses.
  Tilt is centred on how you hold the phone when the race starts; set **INVERT TILT** if left/right feel reversed.

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

- **Android:** menu **Racing Game → Build Android APK** (sets up URP and the scene, builds a 64-bit APK to `build/Android/`),
  or File → Build Profiles → Android → Switch Platform → Build / Build And Run with your phone connected (USB debugging on).
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
Custom shaders live in `Assets/Resources/Shaders`. Each has a **URP** SubShader (used now) and the original
built-in-pipeline SubShader as a fallback:

| Shader | Used for |
|---|---|
| `RacingGame/Toon` | Everything solid: banded cel lighting with blue-tinted shadows, crisp highlight, rim light, optional outline (`_OutlineWidth`) |
| `RacingGame/SkyGradient` | Procedural skybox: gradient, glowing sun, drifting cartoon clouds |
| `RacingGame/BoostPad` | Scrolling glowing chevrons |
| `RacingGame/ItemBox` | Rainbow see-through "?" boxes with fresnel shimmer |
| `RacingGame/Sparks` | Additive glow for drift sparks and boost flames |
| `RacingGame/Skid` | Transparent skid marks on the road |
| `RacingGame/Smoke` | Soft alpha-blended smoke puffs from damaged karts |

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
