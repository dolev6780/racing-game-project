# Raja and the Four Crystals

A 2.5D anime side-scroller for **Android** (Unity 6, URP). Raja, a 13-year-old tiger cub, fights his way through
the jungle with paws, staff and (later) fire to win back the four crystals. The game builds itself from code
(`Assets/Scripts/Cloudborne`, namespace `Cloudborne`); its art is drawn in Canva and modelled in Meshy
(`Assets/Resources/Cloudborne`).

## How to open & run

1. Install **Unity Hub** and **Unity 6 LTS (6000.0.x)** with **Android Build Support**.
2. Unity Hub → **Add → Add project from disk** → select this folder.
3. If Unity asks to enable the new Input System backends, click **Yes**.
4. On first open the project switches itself to **URP** and creates the `Cloudborne` scene, then opens it.
   If anything looks pink, use **Cloudborne → Set Up URP**; if the scene is missing, **Cloudborne → Set Up Scene and Settings**.
5. Press **Play**. (Play in any new, unsaved scene also starts the game.)

**Cloudborne → Update from GitHub** pulls the latest version from inside the editor.

## Compile check (no Unity needed)

`Tools/CompileCheck/check.sh` compiles every script against Unity's API using the .NET SDK
(old Input Manager, new Input System, editor scripts and tests). Run it before committing:

```
Tools/CompileCheck/check.sh      # prints PASS/FAIL per pass, with any errors
```

## Automated tests (Unity)

`Assets/Tests/PlayMode/CloudborneSmokeTests.cs` plays the game for real in play mode: every move and technique,
climbing, swimming, the stage's traps and puzzles, the story scenes, the menu and the boss. Any exception or error
log fails a test. The last test, `ZzPicturesOfTheStage`, photographs the stage at six places as the game camera
sees it (saved to `artifacts/shots` and written to the log as base64 `SHOT` lines).

- **In the editor:** Window → General → Test Runner → PlayMode → Run All.
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

- **Android:** menu **Cloudborne → Build Android APK** (sets up URP and the scene, builds a 64-bit APK to `build/Android/`).
- The game holds the phone sideways (landscape, auto-rotating) and keeps it awake.

## Code structure

```
Assets/Scripts/
  Cloudborne/  the game: CbBoot (start-up), CbGame (flow, camera, fights), CbHero + CbHeroWays (Raja), CbEnemy,
               CbLevel + CbLevelWays (the stage's platforms, climbs, traps and puzzles), CbWorld (scenery),
               CbItems, CbHud, CbMenu, CbScene / CbStory / CbComic (story scenes), CbArt / CbMesh / CbFx / CbVfx
               (materials, meshes, effects), CbModel / CbRig (characters), CbSave
  Core/        InputBridge (keyboard, gamepad, touch), PerformanceGovernor (keeps phones near 60 fps)
  UI/          UIKit (code-built uGUI: canvases, panels, labels, buttons, fonts)
  Editor/      CloudborneSetup (scene + settings), UrpSetup, BuildScript, CiTests, CharacterImport, GitUpdate
Assets/Resources/
  Cloudborne/  the art: Raja, Koda, Baba, the hyena soldiers, items, props, the jungle's paintings and models, story panels
  Shaders/     Toon (cel lighting + outline), PaintedCutout, Backdrop, SkyGradient, Glow, Sparks, Smoke
  Fonts/       Russo One, Rubik
Tools/
  Blender/     scripts that turn Meshy's models and animations into the game's FBX files
  CompileCheck/
```

Fonts: see [CREDITS.md](CREDITS.md).
