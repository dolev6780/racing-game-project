# Claude: read me first (Raja and the Four Crystals / Cloudborne)

A 2.5D anime side-scroller for Android, Unity 6 (6000.0.58f2), URP. Namespace `Cloudborne`, code in
`Assets/Scripts/Cloudborne`, art in `Assets/Resources/Cloudborne`. Work branch: **`cloudborne-phase0`**
(GitHub `dolev6780/racing-game-project`). See README.md for how to open and run.

## Standing rules from the user (keep all of them)

- **Always answer the user in Hebrew.**
- **Quality first, never save at its cost** ("top high quality game"). Test everything: where Raja stands, hangs or
  climbs he must touch it (feet on the surface, hands on the edge), with reasonable movement.
- **Art pipeline:** every new piece of art starts as a **Canva drawing the user approves**, and only then goes to
  **Meshy**. Show the user renders of Meshy's results before they go in. Take out anything unsuitable for a kids'
  game (a rope loop that looked like a noose was removed from a drawing).
  - Canva's own images come back as 200 px previews, and Canva often "cools down" (quota). Ask the user to download
    the approved image and send it in the chat; crop the pieces from that (PIL), each on white, square.
- **Meshy credits:** report the balance after **every** Meshy action (`GET https://api.meshy.ai/openapi/v1/balance`).
  The key is injected by the proxy: always send `-H "Authorization: Bearer msy_dummy"`. Never bypass Meshy's paywall.
  Costs seen: image-to-3D 30, text-to-3D preview 20, rig 5, animation 3 per clip, remesh 5.
- **Pushing:** never push without the user's word, per round. "Push" means push with `[no apk]` in the head commit
  message (the CI's Android job is skipped). "Push and build an APK" means a head commit without `[no apk]`. A stop
  hook keeps asking to push: politely say (in Hebrew) that the commits wait for the user's word. Don't push during an
  APK build unless told.
- **APKs:** never publish them publicly. Give the GitHub artifact link (needs login; open in Chrome, not the GitHub
  app; download the ZIP, extract, install): `https://github.com/dolev6780/racing-game-project/actions/runs/<run>/artifacts/<id>`.
- **Secrets:** never ask for keys in chat. The Unity license, email and password live only in GitHub secrets. The
  user declined to give an ElevenLabs key. Keep the user's email private.
- **Commits:** no model names. End every commit message with:
  ```
  Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>
  Claude-Session: https://claude.ai/code/session_01K37uY7EN2xbFgihJKgCj46
  ```
- Videos are on hold ("focus on the game"); audio later; chapter 2 only after chapter 1 is right.

## Workflow

1. Edit, then `bash Tools/CompileCheck/check.sh` (compiles everything against stubs of Unity's API; editor APIs new to
   the project need a stub in `Tools/CompileCheck/Stubs/EditorStubs.cs`).
2. Commit locally; push only on the user's word (see above).
3. CI (`.github/workflows/ci.yml`): compile check, then Unity play mode tests (about 16 minutes), then the Android
   build (about 25 more, unless `[no apk]`). A new push cancels the running run. Schedule a check about 24 minutes
   after a push.
4. Read the tests' log: GitHub MCP `actions_list` (runs, jobs), then `get_job_logs` with the tests job's id,
   `return_content: true, tail_lines: 7000`. The log is too big and is saved to a file. Then:
   `python3 -I Tools/Dev/ci_report.py <saved log> <out dir>`. It prints the failures (re-printed at the end as
   "CI FAILURE AGAIN"), "CI TESTS DONE", the touch test's places one per line, the rock-step counts and PERF lines
   (things drawn, triangles, heaviest objects per picture), and extracts the CI pictures (`SHOT name ...` in the log)
   as JPGs.
   - The log is cut to its last ~16 MB, mostly pictures. A test whose picture must be seen should be named `Zz...`
     with a name sorting after `ZzPicturesOfTheStage`, so it runs late.
5. Make picture sheets with PIL and send them to the user with a short Hebrew report.

## Tools

- `Tools/Blender/meshy_prop.py`: a Meshy GLB to FBX + albedo for Unity (`-- in.glb OUT_DIR NAME 2 0 [--faces N]`).
  Use full detail (reduced meshes looked torn). A lighter LOD: run again as `NAME_LOD1 --faces N`, then delete the
  `NAME_LOD1_Albedo.png` (`CbArt.Prop` puts LOD1 in an LODGroup with the same material).
- `Tools/Blender/meshy_character.py`: Meshy animation GLBs to clips for Raja:
  `-- OUT_DIR Raja a.glb b.glb --clips-only --file Raja_<Set>_Clips`. `CbModel` loads `Raja_Clips`,
  `_More_Clips`, `_Dodge_Clips` and `_Climb_Clips`; add a `LoadAll` line for a new file.
- `Tools/Dev/` (Blender run with a venv that has `bpy`; in the cloud sessions it was `scratchpad/bvenv`):
  - `glb_views.py`: three views of a model.
  - `close_render.py`: a close render of a model.
  - `slices.py`, `slices_rot.py`: a model's extent per height (to measure ledge tops, openings).
  - `clip_marks.py`: per 10% of a clip, the hips, hands and feet (to find a punch's or a catch's moment).
  - `render_clip_frames.py`: six frames of a clip, the camera following.
  - `cut_y.py`: cut a mesh at a plane.
  - `tricount.py`: triangles per FBX.
  - `ci_parse.py`, `ci_report.py`: the CI log (see Workflow).
- **Meshy API.**
  - Image to 3D: `POST /openapi/v1/image-to-3d` with `{image_url: data URI, ai_model: latest,
    should_texture: true, should_remesh: true, topology: triangle, target_polycount: 12000-15000, enable_pbr: false}`.
  - Animations: `POST /openapi/v1/animations` with `{rig_task_id, action_id}`.
  - The animation library is at https://docs.meshy.ai/api/animation-library. The user picks from it.
  - Text to Motion makes a custom move from a description.
  - Raja's rig `01a207d4-e59d-74f9-a50e-0262a09272b1` expires about 2026-11-26. After that, rig again (5 credits):
    export Raja's mesh to a GLB, then `POST /openapi/v1/rigging` with `{model_url: data URI, height_meters}`.
  - `cdn.meshy.ai` previews are blocked here; `assets.meshy.ai` downloads work.

## How the game is built (the parts touched most lately)

- **No racing game left (the user):** the code assemblies are `Cloudborne`, `Cloudborne.Editor`,
  `Cloudborne.Tests.PlayMode`; namespaces `Cloudborne`, `Cloudborne.EditorTools`, `Cloudborne.Tests`; shaders
  `Cloudborne/...`; the URP asset `Assets/Settings/Cloudborne_URP.asset`. `InputBridge` (touch pointers),
  `UIKit` (panels, rings, labels) and `PerformanceGovernor` live in `Assets/Scripts/Cloudborne`. Only the GitHub
  repository's name still says racing.
- **The game:** `CbGame` (+ `CbGameRuns`), `CbHero` (+ `CbHeroWays`, `CbHeroGuard`, `CbHeroThrow`), `CbEnemy*`,
  `CbItems`, `CbHud`.
- **Levels:** `CbLevel` partials per stage and feature:
  - `CbLevelWays`: the long middle of 1-1, and `Model()` for the kit's models (yaw 180 faces the camera).
  - `CbLevelGorge`, `CbLevelBoulder`, `CbLevelTunnels` (the ambush caves).
  - `CbLevelClimb`: the climbing kit, the swing pole.
  - `CbLevelProbe`: probes surfaces from the meshes.
- **Meshy kits (approved drawings):**
  - In `Jungle/Ways`: RuinLedge (pillar under a ledge), RuinWall (towers; taken out of the stages by the user),
    TreePlatform (1-2's ledges), SwingPole, StakedLog (thorny low passages), CaveArch + CaveGate
    (the ambush tunnels; the gate sinks while raiders come out), StoneStairs (stone bridge), VineTree.
  - Leafy models get a thin outline (`Leafy` in `CbLevelWays`).
- **Touching what is drawn (`CbLevelProbe`).**
  - The kit's and the rocks' models get MeshColliders (`Solidify`). Rays find their real tops; ledges are put there
    (`FindTop`, `AddMeasuredLedge`).
  - `AddRockSteps` makes every step drawn on a rock standable.
  - `SurfaceUnder` gives his feet the drawn surface across the path's depth. His feet stand 0.36 m either side of
    his line, so look at z ±0.3.
- **IK (`CbLimbIK`, LateUpdate after the legacy Animation).**
  - Feet: each foot on the surface under it. The body goes down to the lower foot's surface, and the higher knee
    bends.
  - Hands: each palm on the grip (an edge, a pole, a bar, a vine, a lip).
- **Hangs.**
  - At a block's side: braced against it.
  - Under a ledge: a free hang, legs straight.
  - Along a ledge's lip: facing it, moving left and right (`State.Lip`, clips 446/447).
  - Pull-up: `State.Climb`.
- **The swing pole:** a pendulum from his hands (`State.Pole`). **No pole in the stages until the user says where.**
- **Performance.**
  - Textures: ASTC per texture kind (`Assets/Scripts/Editor/CharacterImport.cs`).
  - One shared material per model (`CbArt.Prop`); tint copies with `CbArt.Tint`, never a shared material.
  - The bank stones and grass are static-batched.
  - The HUD shows an FPS line (`CbHud.ShowFps`).
- **Tests:** `Assets/Tests/PlayMode/CloudborneSmokeTests.cs`.
  - `RajaTouchesWhatHeStandsOnAndHangsFrom` stands him on every ledge, block and along stages 1-1 and 1-2, and hangs
    him from every ledge's edges.
  - `ZzSwingPole...` and `ZzWhenShortOfALedge...` (the lip hang).
  - The picture tests: `ZzPicturesOfTheStage` and others.

## Where things stand (update this section as work goes on)

- **Run 289 (7ef081d, with APK): 82 of 84 pass.** The headband, the start wall and the guard pass now.
  - Touch test: the chest again, and stairs at x 450.7. The chest is skinned: `Solidify` measured only MeshFilters,
    so nothing measured it, and the test baked it without its scale. Now `Solidify` bakes skinned parts too
    (`BakeMesh(m, true)`), and so does the test. On the stairs (0.58 m steps) a foot hung halfway down a step:
    `CbLimbIK.StepDown` (0.42) - a foot doesn't step down further standing; it stays level, toes over the edge (the
    test allows one such foot at an edge). Local, not yet run.
  - `RajasFireWakesBeforeGraksThreePhases`: Grak flung no fire in 20 s (his moves are random). Now 30 s, and the
    failure lists his moves.
- **Performance (real numbers, run 286):** 1-1 draws 63k-137k triangles, the gorge 220k (its CliffRock pieces 115k),
  1-2 37k-121k (TreePlatform up to 60k). Fine for a phone; the gorge's rocks are the first to lighten if needed.
- **Hangs (the user, after run 288's APK):** hanging looked like climbing (hands going up, arms stretching, body
  rising). Every hang is now `Bar_Hang_Idle`, still, arms straight (`FreeHangReach`), at a wall too (`BracedOff`).
  The 446/447 hang-and-climb clips are loaded but unused (they lift him 0.45 m and swing his legs up).
- **Towers:** all ruin towers (RuinWall) are out (the user). The stage will be laid out anew from a Canva plan
  (a presentation, one slide per section) that the user edits and approves. A ledge too high for a pillar is the
  cliff kit's slab on its column of rock.
- **Animations.** The user chose Meshy 94, 132, 446, 447, 448, 449, 450 and 485; they are in:
  - Air fist: 94.
  - Ki-ball cast: 132.
  - Wall flip: 450.
  - Bar and pole catch: 485.
  - Lip hang left and right: 446/447.
  - 448 and 449 were already in (grabbing an edge, climbing a vine).
  The pull-up over an edge still uses the end of "Ladder Climb Finish"; I suggested making a mantle in Text to Motion.
- **Rocks.** The user said climbing on the rocks "doesn't render well". Steps on them are now standable
  (`AddRockSteps`). Still waiting for a screenshot of the render problem.
- **FPS:** the user says it is fine (run 288's APK).
- **The look of the jungle (the user's marked screenshot, local, not yet run):**
  - `PaintedCutout` got `_Wind` (the painting's top sways, gusts rolling along the stage), `_Dapple` (sunlight
    drifting over leaves) and depth shading for the ground's face (`_ShadeTop`, `_Deep`, `_DeepOver`, `_Ao`).
  - The hedge, its clumps, the row of trees and the grass lip sway; a second, hazier, mirrored row of trees stands at
    z 34 for depth; the earth face darkens with depth and lies in shadow under the lip.
  - Swaying plant cards scattered in front of the hedge, along the path's back edge and over the front edge.
  - Raja's headband tail stirs: `CbHeadband` adds a bone at its knot at runtime (Raja's mesh is now readable,
    `CharacterImport` version 9). Test: `RajasHeadbandTailStirsALittle`.
  - No new art yet; new paintings (a better ground, separate tree layers) would go through Canva first.
- **Stage 1-1.**
  - The user chose to keep the stage as it is for now (no new Canva route), and fix it piece by piece from the start.
  - Behind him at the start: no gate, an invisible wall at `CbWorld.StartWall` (-0.5; every stage starts at x 0).
    The scenery still runs on behind it. Test: `AtTheStartAnInvisibleWallIsBehindHim` (local, not yet run).
  - The start's tower and springy leaf were removed by the user, so 1-1 has one secret (the cracked boulder).
  - The swing pole was removed from the river, to be placed where the user says.
- **Next art to redo through Canva and Meshy (proposed):** the waterfall, the raft river's small footbridge, the
  thorns on the bamboo bar, the trunks holding the bars.
