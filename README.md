# Racing Game Project 🏁🎸

A 3D **combat racing** game for **Android & iOS**, inspired by the classic *Rock n' Roll Racing*:
armed cars, missiles, mines, nitro, money, alien-planet tracks.

Built with **Unity 6 + C#**.

## Status: v0.1 playable prototype

Everything is generated from code (placeholder art from primitives), so you can open the project and press **Play** right away.

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
| Garage / shop / upgrades | ⏳ next |
| Real 3D car & track models, music, SFX, announcer | ⏳ |
| Championship / multiple tracks / planets | ⏳ |
| Multiplayer | ⏳ |

## How to open & run

1. Install **Unity Hub** and **Unity 6 LTS (6000.0.x)** with **Android Build Support** (and **iOS Build Support** on a Mac).
2. Unity Hub → **Add → Add project from disk** → select this folder.
   (If Hub asks about a different editor version, pick any Unity 6 version.)
3. If Unity asks to enable the new Input System backends, click **Yes** (the code supports both input systems).
4. Menu **Racing Game → Create Race Scene**, then press **Play**.
   *(Pressing Play in any empty scene also works — the race builds itself.)*

### Controls

| Action | Keyboard | Touch |
|---|---|---|
| Steer | A/D or ←/→ | `<` `>` buttons |
| Gas / Brake | W/S or ↑/↓ | auto-gas, `BRAKE` button |
| Fire missile | Space / J | `FIRE` |
| Drop mine | E / K | `MINE` |
| Nitro | Left Shift / L | `NITRO` |
| Switch camera | C | `CAM` button |
| Restart race | R | `RACE AGAIN` |

In the editor, press **TOUCH: ON** in the top-right to preview the phone controls (click with the mouse).

## Building for phones

- **Android:** File → Build Profiles → Android → Switch Platform → Build (APK) or Build And Run with your phone connected (USB debugging on).
- **iOS:** Requires a Mac with Xcode. File → Build Profiles → iOS → Build, then open the generated Xcode project and run it on your device.
- Recommended Player settings: Default Orientation = *Landscape Left* / Auto Rotation (landscape only).

## Code structure

```
Assets/Scripts/
  Core/       GameBootstrap (auto-start), InputBridge, MaterialFactory, Explosion
  Track/      TrackBuilder (procedural track), TrackPath (centerline/progress), Pickup
  Car/        CarController (physics), CarHealth, RacerProgress (laps),
              PlayerCarInput (keyboard+touch), AICarInput, CarBuilder
  Weapons/    WeaponSystem, Missile, Mine
  Race/       RaceManager (countdown, laps, standings, prize money)
  CameraRig/  RaceCamera (isometric / chase)
  UI/         RaceHUD
  Editor/     RacingGameMenu (scene creation tools)
```

Tuning lives in public fields (e.g. `CarController.maxSpeed`, `WeaponSystem.missilesPerLap`,
`RaceManager.totalLaps`) and track layouts in `TrackBuilder.DefaultLayout`.
