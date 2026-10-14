# Racing Game Project 🏁🎸

A 3D **combat racing** game for **Android & iOS**, inspired by the classic *Rock n' Roll Racing*:
armed cars, missiles, mines, nitro, money, alien-planet tracks.

Built with **Unity 6 + C#**.

## Status: v0.2 — real 3D models & sound

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
| Money: prize by place + kills + pickups (saved) | ✅ |
| Armor & money pickups on track | ✅ |
| Isometric camera (classic) + chase camera | ✅ |
| Touch controls (auto-accelerate) + keyboard | ✅ |
| Phone steering: floating joystick / tilt / buttons | ✅ |
| Garage / shop / upgrades | ⏳ next |
| Real 3D car models, finish arch, forest scenery (Kenney) | ✅ |
| Engine / skid / crash / explosion sounds | ✅ |
| Music & announcer | ⏳ |
| Championship / multiple tracks / planets | ⏳ |
| Multiplayer | ⏳ |

## How to open & run

1. Install **Unity Hub** and **Unity 6 LTS (6000.0.x)** with **Android Build Support** (and **iOS Build Support** on a Mac).
2. Unity Hub → **Add → Add project from disk** → select this folder.
   (If Hub asks about a different editor version, pick any Unity 6 version.)
3. If Unity asks to enable the new Input System backends, click **Yes** (the code supports both input systems).
   The project uses the **glTFast** package (`com.unity.cloud.gltfast`) to import the `.glb` 3D models; Unity installs it automatically.
   If it reports a package version error, open **Window → Package Manager → + → Add package by name…** and enter `com.unity.cloud.gltfast`.
4. Menu **Racing Game → Create Race Scene**, then press **Play**.
   *(Pressing Play in any empty scene also works — the race builds itself.)*

### Controls

| Action | Keyboard | Touch |
|---|---|---|
| Steer | A/D or ←/→ | Joystick (drag on left side), Tilt, or `<` `>` buttons |
| Gas / Brake | W/S or ↑/↓ | auto-gas, `BRAKE` button (or pull joystick down) |
| Fire missile | Space / J | `FIRE` |
| Drop mine | E / K | `MINE` |
| Nitro | Left Shift / L | `NITRO` |
| Switch camera | C | `CAM` button |
| Restart race | R | `RACE AGAIN` |

**Phone steering modes** — tap **STEER** (top-right) to switch; your choice is saved:
- **STICK** (default): put your thumb anywhere on the left side of the screen and drag left/right. The joystick appears where you touch.
- **TILT**: turn the phone like a steering wheel. Tilt is centred on how you hold the phone when the race starts;
  tap **CENTER** to re-centre, or **INVERT** if left/right feel reversed.
- **BTNS**: classic `<` `>` buttons.

In the editor, press **TOUCH: ON** in the top-right to preview the phone controls (click with the mouse).

## Building for phones

- **Android:** File → Build Profiles → Android → Switch Platform → Build (APK) or Build And Run with your phone connected (USB debugging on).
- **iOS:** Requires a Mac with Xcode. File → Build Profiles → iOS → Build, then open the generated Xcode project and run it on your device.
- Recommended Player settings: Default Orientation = *Landscape Left* / Auto Rotation (landscape only).

## Code structure

```
Assets/Scripts/
  Core/       GameBootstrap (auto-start), InputBridge, ModelLibrary (models+audio), MaterialFactory, Explosion
  Track/      TrackBuilder (procedural track), TrackPath (centerline/progress), Pickup
  Car/        CarController (physics), CarHealth, RacerProgress (laps),
              PlayerCarInput (keyboard+touch), AICarInput, CarBuilder, CarAudio
  Weapons/    WeaponSystem, Missile, Mine
  Race/       RaceManager (countdown, laps, standings, prize money)
  CameraRig/  RaceCamera (isometric / chase)
  UI/         RaceHUD
  Editor/     RacingGameMenu (scene creation tools)
```

3D models live in `Assets/Resources/Models` and sounds in `Assets/Resources/Audio` (see [CREDITS.md](CREDITS.md)).
To use your own car model, drop a `.glb` there with children named `body` and `wheel-front-left`, `wheel-front-right`,
`wheel-back-left`, `wheel-back-right` (pivot at each wheel's centre), and pass its name to `CarBuilder.Create`.

Tuning lives in public fields (e.g. `CarController.maxSpeed`, `WeaponSystem.missilesPerLap`,
`RaceManager.totalLaps`) and track layouts in `TrackBuilder.DefaultLayout`.
