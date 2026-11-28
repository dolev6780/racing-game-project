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
  - In `Jungle/Ways`: (RuinLedge and RuinWall, unused once the towers and pillars went, were removed from Resources -
    in git history), TreePlatform (1-2's ledges), WoodPlatform(Small) + BranchTree + MossBranch (1-1's hanging
    ledges), the gorge's rock kit (RockLedge, RockBlock, RockPlatform, RockStairs, RockShelves, RockStep), SwingPole, StakedLog (thorny low passages), CaveArch + CaveGate
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
    `CrackedRock` for crumbling ones; `GorgeRockThick`). Pictures gorge_rock*, hang_pose, hang_pull.
  - Run 299 (1299f26): 85/87. The touch test failed at the gorge's rough rock ledges (166.4) and the summit (151.5);
    the hang picture test let go (a guessed edge: it now asks `FindEdge`). The user's screenshots (twice): hanging,
    his head over the shelf and his arms folded down to it. Measured in Blender: in Hang and Climb his wrists are
    1.76 m over his soles, but he hung 1.68 under the edge, so the hand IK bent his arms. Now `FreeHangReach` 1.78,
    and he eases down into it over `CatchTime` (caught at `HangReach` 1.43). The test measures his bones: crown under
    the edge, wrists over his head, arms straight (>0.9 of their length).
  - Gorge rocks: the user rejected two Canva sheets (not climbable-looking), approved a third (MAHcIY-kKn4: a flat
    grassy platform, stone stairs, a ledge jutting from a cliff with a lip, a wall with three shelves, a stepping stone).
    Meshy: RockPlatform, RockStairs, RockLedge, RockShelves, RockStep (150 credits) in `Jungle/Ways`, plus RockBlock
    (Meshy's 3k remesh of RockPlatform, 5 credits - Blender's decimation tore its texture; 423 left). In the gorge:
    every ledge is `GorgeLedge` (CbLevelClimb): RockLedge jutting from the back wall (its root 0.6 into it, its lip
    at z -0.75, no column under it; of yaw 90/-90 the one with the deep rock at the back, by rays), crumbling ones
    darker with cracks; the walls (`RockWall`: the gate cliff, the back wall - now on to MountainTo + 1 for the far
    side's ledges - the summit cliff, the shoulder, the jut) are rows of RockBlock, the top row's grass the walkable
    top, the rows under it turned over. RockStairs, RockShelves and RockStep not used yet. Not yet run.
- **Run 300 (a03c726): 87/90.** The hang test measured the user's complaint exactly: crown 0.93 m OVER the edge,
  arms straight down to it - with Hang and Climb the body stood ~1 m too high for the clip's hands (its hips curve
  differs from Bar Hang Idle's), and the hands' IK pulled the arms down. Fix: `CbLimbIK.alignToGrip` (on while
  `State.Hang`) moves the body by its hips each frame so the clip's own palms meet the grip, before the hands' solve.
  The touch test's "legs not straight down" in every hang came from the same. The pogo test started too far on (the
  dive overshot the hedge). A rock step was found on a RockBlock's bump (150.2): the rock kit is now solid for the feet
  but kept out of `rockSolids` (no steps on its walls).
- **Run 301 (099d28a): 92/95.** The hang is right at last: `HANG pose top 6.20: crown 6.08, wrists 6.17/6.17, arm
  straight 0.94` (head under the edge, hands on it over his head; picture hang_pose). Failures, fixed locally: the
  perfect guard met a chance red heavy blow (the tests now set `CbEnemy.NoChanceHeavy`); the sinking-stone check used
  95.2, now a lotus (now 86.6); the letter at 89 was not taken (the test now holds him under it and logs `LETTER`
  lines - read them if it fails again). The rain was hard to see: wider, longer, brighter streaks.
- **Staffs' models (local):** Meshy StaffFire/StaffWind/StaffVine (90 credits, 113 left) in `Props/Staff`, 1.75 m,
  grip 0.35 like the oak staff; symmetric (decorated at both ends). `TintStaff` swaps the model in his hand.
- **The user's ideas list (inspired by The Lost Crown, Kirby and the Forgotten Land and others; the user: "let's
  do everything").** Done locally, not yet run: gold/red blows (`CbEnemy.HeavyChance` 0.25: a red "!!", longer
  wind-up, unblockable, 1.5x, knocks down - only a dodge escapes; the usual "!" now gold); the pogo (a dive kick onto a
  thorn hedge bounces him `PogoUp` 10, double jump and dash back, thorns harmless while `Pogoing`); hanging platforms
  dip on landing (`UpdateHangers`); soldiers' break gauge (`AddSoldierBreak`: 34 hp of blows, counters/breaker blows
  +0.4; full: BREAK!, dazed 2.2 s, 1.5x damage). Already in the game before: the perfect guard returning blows and
  spears (1-3), counters, Snap's break gauge, results with tokens/friends/no-hit/rank. The R-A-J-A letters (Canva
  MAHcISvQhzY approved; Meshy LetterR/A/J/A2 in `Items/Letters`, 120 credits, 293 left): `CbItems.Kind.Letter`,
  `WaysLetters` in 1-1 - R over the temple's thorns (only the pogo reaches it), A over the river rock at 89, J over the
  canopy's bar, A over the gorge's jut; `CbGame.Letters` bits, a banner with the word, "R-A-J-A n/4" on the results.
  Hard mode (`CbSave.Hard`, SETTINGS > DIFFICULTY, where LANGUAGE was): soldiers' blows and spears x1.4, heavy blows
  x1.6 as often, the gorge's water x1.25, crumbling ledges x0.75 the time; HARD on the results.
  Lotus launchers: `AddLotus` (the 1-4 lotus model on the water, a leaf with `bounce` 12.5 and `push` 5.5): landed on
  from a jump it throws him up and on (`BouncePush`, `lotusT` keeps the throw); one in the long river at 95.2 in place
  of a sinking stone.
  Time of day and rain (`CbWeather`, 1-1 only): the sun warms from morning to afternoon gold over the stage and sinks
  10 degrees; over the canopy and the waterfall (362-410, 10 m fades) it rains - dimmer, cooler light, 140 streaks
  round the camera, lightning every 4-9 s. Pictures weather_rain, weather_afternoon.
  Ink strokes (`CbVfx.Ink`, a procedural brushed crescent with drops, dark, drawn over the scene): on every big blow
  and counter by Raja, and two crossed on a BREAK. (The chase already exists: the pack after him, `CbGameRuns`.)
  Totem masks (Canva MAHcIQKzEAE approved; Meshy MaskStone/Leaf/Vine in `Items/Masks`, 90 credits, 203 left;
  `CbHeroMasks`): `Kind.Mask` pickups at `WaysMasks` (stone 70 before the cracked wall, leaf 374 on the canopy's
  terrace, vine 140.6 at the gorge's foot); worn on his head bone 12 s (`FormTime`), blinking at the end. Stone: runs
  x1.35, running into cracked stone / crates / soldiers bursts them, half harm. Leaf: holding JUMP falling, glides at
  `GlideFall` 1.8 m/s. Vine: `VineEverywhere` - every non-smooth block side climbs as vines (`VineWallAt`).
  Time trials (`CbTrials`, made with the weather per stage): 1-1's VINE RAVINE (199.5-239.5) and CANOPY (371-400.5),
  glowing stones at either end; crossing the first starts a clock under the title (`CbHud.SetTrial`), the second
  gives GOLD/SILVER/BRONZE by `WaysTrials`' times (guesses, to be tuned by playing), best kept in PlayerPrefs;
  back past the start, swimming or dying ends the try.
  Staffs (`CbSave.StaffsOwned/StaffKind/NextStaff`, a STAFF row in Baba's workshop - the upgrade rows now 52 apart):
  oak, FIRE 120 (staff blows burn), WIND 150 (the whirl draws soldiers in, other blows x1.6 knockback), VINE 150
  (staff blows pull the soldier to him) - in `CbGame.HurtEnemy` when `hero.StaffSwinging`. Their models (Canva
  MAHcIhNLLDI approved) are in his hand (`TintStaff`), the trail coloured to match.
  Raja's camp (`CbCamp`, local, not yet run): the main menu's place - a clearing at x -30, left of 1-1's start behind
  the invisible wall (seen only from the menu; `ToMenu` builds 1-1 again when back from another stage, `InCamp` puts
  him at `CbCamp.HeroX`, `StartGame` snaps the camera to him). Canva kit MAHcKOfhc3g approved (hut, campfire, staff
  rack, trophy shelf, map board, bench); Meshy so far CampHut, Campfire (its modelled flame cut off in Blender, the
  game's flickering flame tongues on it), TrophyShelf in `Resources/Cloudborne/Camp` (90 credits, 23 left). The shelf
  (floors measured at 0.21/0.59/0.98 m) shows `CbSave.LettersEver` (top), `MasksEver` (middle) and the trials' medals
  (bottom, from their best times); the staffs owned but not carried lean on the hut. Test `ZzzRajasCampIsWhereTheMenuIs`
  (pictures camp_menu, camp). Still to model when credits renew (~Dec 10): the staff rack, map board, bench.
- **Performance and challenge (the user: keep improving performance, graphics and challenge; local, not yet run):**
  the climbing vine is Meshy's 2.3k remesh (was 6k; the canopy drew 138k of vines), the coin Meshy's 376-triangle
  remesh (was 1.2k), both fed to Meshy as GLBs made from the game's FBX + albedo (scratchpad `fbx2glb.py`); RuinLedge
  and RuinWall removed. Harder: the gorge's water starts after 1.2 s (was 1.6) and rises 0.46 m/s (was 0.38);
  crumbling ledges give way after 0.38 s (was 0.5); soldiers swing again after 0.8-1.5 s (was 1-1.9) and throw
  spears every 1.8-2.7 s (was 2.2-3.2). Credits 413.
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
