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
  Use full detail (reduced meshes looked torn). `--smooth DEG` welds and smooth-shades (Meshy's trees came half
  flat-shaded, all facets up close). A lighter LOD: run again as `NAME_LOD1 --faces N`, then delete the
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

- **Run 296 (320ab64): all 87 pass** (the knockdown, the lip, the pull-up and the hanging platforms' tests). The
  pictures: the platforms hang on their ropes; the branches are high, under the HUD's hint box; in the canopy the bar's
  two trunks (`AddTrunk` at the bar's ends +-1.4) stand right behind the platforms at 380.6 and 388.6; at the
  waterfall the stacked platforms are behind the falling water (as the old ledges were).
- **Combat (the user: struck down, the soldiers were up again at once, straight back into the fight):** a big blow
  (`CbEnemy.KnockKb` 7 or `KnockUp` 4: the combo finisher, launchers, the slide, the sweep kick, fire finishers)
  floors a soldier: he flies (or falls over where he stands), bounces once, lies `LieTime` 0.85 s, gets up over
  `RiseTime` 0.6 s (his `dying_backwards` played back), then waits 0.6-1 s before swinging. Hit on the ground he
  bounces and stays down; getting up, a blow only pushes him. After an ordinary flinch he waits 0.4 s. (Raja's
  `Stand_Up1` can't drive the hyenas: their rig is 1.34 times taller in its own units.) Test
  `ABigBlowKnocksASoldierDownAndHeTakesHisTimeGettingUp`. Local, not yet run.
- **The user's four points (photos from the phone; local, not yet run):**
  - The gorge's water, left at the summit's height after the climb, stood a wall of dark blue: now clear like the
    streams (alpha 0.5 at the top, 0.78 deep over 6 m).
  - No hang along a ledge's front lip (his back to the camera): only from a ledge's ends, from the side. `StartLip`
    is kept, unused; the test is now `ShortOfALedgeHeDoesNotHangWithHisBackToUs`.
  - The pull-up from the side: the hang is held `HangHold` 0.35 s (hands up over his head, head below them), then
    `ClimbTime` 0.8 s in two parts: hauled up to the edge from arms straight (`PullPart`, palms kept on the edge),
    then the mantle (`Ladder_Climb_Finish` 0.25-0.85).
  - The staff 0.72 of its size (`StaffScale`): as tall as Raja, it looked far too big.
  - The ledges' rock pillars (the user: they look bad) are replaced in 1-1 (outside the gorge) by wooden platforms
    hanging on two ropes from a mossy branch (`HangingPlatform`, `HangPlatforms` in `CbLevelClimb`). Canva kit sheet
    approved (MAHcHuMkxy0), Meshy: `WoodPlatform`, `WoodPlatformSmall` (ledges up to 1.3 m), `MossBranch` in
    `Jungle/Ways`, with LOD1s. The platforms' rope stubs were cut off at the knots (the ropes are cylinders up to the
    branch); a rope with a platform or another's branch over it is tied to that (the waterfall's stacked ledges).
    RuinLedge is no longer used. Test `ZzzLedgesHangOnRopesFromBranches` (pictures hang_*). Run 296: passes.
  - The user (run 296's pictures): not what they meant - a beautiful TREE in the background whose branch comes out
    over the path and holds the platform, the background blending into the path. Canva drawing approved
    (MAHcHzvuEsE; its swing painted out with PIL), Meshy `BranchTree` (Jungle/Ways, LOD1). `BranchTree()` in
    `CbLevelClimb`: per platform with a free rope, a tree TreeZ 4 back, depth squeezed `TreeSquash` 0.35, as tall as
    puts its branch `BranchOver` 3 m over the platform, on a dry side, turned (up to 40 deg) so the branch reaches the
    platform's back; the model's branch direction is unknown, so of two yaws the one whose branch rays from both knots
    meet is kept (temporary MeshColliders, destroyed after); a branch may not pass within `HeadRoom` over another
    platform. Ropes go from the knots to where the rays meet the branch (slanted a little in z). No tree fits: the
    old MossBranch. Run 297: all 87 pass; 6 trees (one platform, at the waterfall, on a branch of its own); the
    pictures show each branch reaching over its platform from a big tree behind the path. Its crown's leaf cards drew
    black shards with the outline: the tree is now drawn without one (`Bare`) and its leaves two-sided (local).
    Canopy and waterfall pictures 312-338k triangles (about as before: the mossy branches were as heavy).
  - The user (a screenshot by the shrine, run 297's build): the tree had black shards (fixed: no outline); the
    hangers should look better (ropes are now two twisted strands, bound fatter at the branch); the platforms at a
    few heights under the branch (`HangOvers` 2.7-4.3 m, in turn); trees further back (`TreeZ` 5.5, turn up to 45).
    The tree looked faceted up close: Meshy's finer remesh (45k, 5 credits) looked the same, so it was not used;
    instead the tree is welded and smooth-shaded (`meshy_prop.py --smooth 75`): rounder trunk and branch. The side hang still looked the same:
    it now holds the start of `Hang_and_Climb_Rght/Left` (446/447, the user's choice: hang, climb on press) and the
    haul up plays its pull (0.07-0.33), then the mantle. The gorge's ledges are natural rocks (`CliffRock`,
    `CrackedRock` for crumbling ones; `GorgeRockThick`). Pictures gorge_rock*, hang_pose, hang_pull. Not yet run.
- **Performance (real numbers, run 286):** 1-1 draws 63k-137k triangles, the gorge 220k (its CliffRock pieces 115k),
  1-2 37k-121k (TreePlatform up to 60k). Fine for a phone; the gorge's rocks are the first to lighten if needed.
- **Hangs (the user, after run 288's APK):** hanging looked like climbing (hands going up, arms stretching, body
  rising). Every hang is now `Bar_Hang_Idle`, still, arms straight (`FreeHangReach`), at a wall too (`BracedOff`).
  The 446/447 hang-and-climb clips are loaded but unused (they lift him 0.45 m and swing his legs up).
- **Towers:** all ruin towers (RuinWall) are out (the user). The stage will be laid out anew from a Canva plan
  (a presentation, one slide per section) that the user edits and approves. The jungle's ledges hang from branches
  (see above).
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
  - The user (run 289's APK): the trees should not move (their wind and dapple are off; the far row stays, still);
    the ground showed a straight cut through the flowers where the lip ends over the face. The face painting is
    raised `FaceGrassShift` (0.3 m) so its own flower row lies under the lip and the lip ends on plain grass of its
    colour; the face's shading starts where its earth begins (`FaceEarthAt`); no dapple on the lip. Bank slopes
    follow the same shift.
- **The ground's edge (the user, run 293's APK: still cut):** the lip ended in a straight line of a slightly other
  green, and the face's shadow started all at once (another line across the grass's bumps). The painted lip is now
  only for stage 1-2's moss; elsewhere the face's own painting (its flower row and grass bumps, one piece) is the
  edge (`FaceGrassShift` 0), and its shading deepens gradually from where the earth begins (`_Ao` 0). Picture
  `ground_edge` in `ZzzStreamsSeenThroughTheirClearWater`. Run 294: all 85 pass; the picture shows the edge of one
  piece, no cut. Later the user disliked the face's green band of flowers (blurred, "pixel like Minecraft"): the
  face is now a new sharp painting (Canva MAHcIB3WxiQ, the user's download, cropped and made to tile with PIL): a thin
  grass fringe over layered earth, roots and pebbles (`GroundFace.png` 1840x615, `FaceEarthAt` 0.09).
- **Clear streams (the user's reference picture: see-through water, what is under it showing; local, not yet run):**
  - `WaterFront` on the jungle's streams: turquoise and see-through at the top (alpha 0.3), bluer and denser deeper
    (0.82 over 3.4 m); it ends at `CbLevel.StreamBed` (WaterLevel - 3), the earth's face below.
  - Under it (`CbWorld.StreamBedAndWeed`, `CbLevel.BedStones`): a sandy bed, the channel's back and side walls down
    to it, round stones on it, weed (tinted grass cards) swaying on it. Rocks standing in the water go on down to the
    bed (not standable: kept out of `rockSolids`), the footbridge's posts too. The stormy river (1-4) is unchanged.
  - Picture test `ZzzStreamsSeenThroughTheirClearWater` (stream_long, stream_vines, stream_raft).
  - The user (run 291's pictures): the water under the surface looks good; the top of the water (at ground level)
    needs work. New `WaterTop` shader on the clear streams' top: the painted ripples (35%) under turquoise flowing
    towards the camera, the sky mirrored when seen flat, the jungle's green mirrored near the far bank, sun glints,
    foam along the banks and at the back, see-through at the front (local, not yet run).
- **Stage 1-1.**
  - The user chose to keep the stage as it is for now (no new Canva route), and fix it piece by piece from the start.
  - Behind him at the start: no gate, an invisible wall at `CbWorld.StartWall` (every stage starts at x 0). The user
    (later): it should be at the screen's left edge - now -5, and the camera never scrolls back past it (`CbGame`,
    tx >= wall - 0.5 + half the view). Test: `AtTheStartAnInvisibleWallIsBehindHim` (checks the viewport too).
  - The start's tower and springy leaf were removed by the user, so 1-1 has one secret (the cracked boulder).
  - The swing pole was removed from the river, to be placed where the user says.
- **Next art to redo through Canva and Meshy (proposed):** the waterfall, the raft river's small footbridge, the
  thorns on the bamboo bar, the trunks holding the bars.
