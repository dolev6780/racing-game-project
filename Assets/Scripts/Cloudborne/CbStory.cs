using UnityEngine;

namespace Cloudborne
{
    /// <summary>
    /// The story's scenes, written as in the script (the story doc, "Raja and the Four Crystals"). Lines are in
    /// English, like the rest of the game's text for now.
    /// </summary>
    public static class CbStory
    {
        public static readonly Color RajaCol = CbArt.Hex("#ffb03a"), KodaCol = CbArt.Hex("#9fd0ff"), SnapCol = CbArt.Hex("#ff6a4d"),
            ElderCol = CbArt.Hex("#c9d2de");   // the village elder (in later scenes)

        // the models each character will use once built in Meshy; until then the stand-ins below
        static readonly string[] KodaModels = { "Cloudborne/Koda/Koda" };
        static readonly string[] SnapModels = { "Cloudborne/HyenaSergeant/HyenaSergeant", "Cloudborne/HyenaRaider/HyenaRaider" };
        static readonly string[] RaiderModels = { "Cloudborne/HyenaRaider/HyenaRaider" };
        const string Machete = "Cloudborne/Props/Machete/Machete";

        static RigStyle KodaStyle => new RigStyle
        {
            fur = CbArt.Hex("#8d9aa8"), paw = CbArt.Hex("#6f7b88"), top = CbArt.Hex("#2c3e66"), pants = CbArt.Hex("#7a5a3a"),
            belt = CbArt.Hex("#c9a46a"), wrap = CbArt.Hex("#efe6d4"), scale = 0.92f,
        };

        static RigStyle HyenaStyle(float bulk, float scale) => new RigStyle
        {
            fur = CbArt.Hex("#b8925a"), paw = CbArt.Hex("#8a6a40"), top = CbArt.Hex("#6b727d"), armor = CbArt.Hex("#7d8590"),
            pants = CbArt.Hex("#4a3a2e"), belt = CbArt.Hex("#2a1f1a"), wrap = CbArt.Hex("#4a3a2e"), bulk = bulk, scale = scale,
        };

        // the net: a sack of rope (meridians and rings) around Koda, centred NetCentre above his feet; the pole it
        // hangs from, carried by two raiders
        const float NetCentre = 0.92f, NetRx = 0.7f, NetRy = 1.02f, PoleHeight = 2.2f;

        /// <summary>A rope net sack wrapping a character whole, knotted at the top; a child of parent.</summary>
        static Transform NetSack(Transform parent, Material rope)
        {
            var net = CbArt.Joint(parent, 0, NetCentre, 0, "net");
            // eight loops round the up axis, crossing at the top and the bottom
            for (int k = 0; k < 8; k++)
                CbArt.Part(CbMesh.Torus(0.024f / NetRx), rope, net, Vector3.zero, new Vector3(NetRx, NetRx, NetRy),
                    Quaternion.Euler(0, k * 22.5f, 0) * Quaternion.Euler(90, 0, 0), shadows: false);
            // and rings round it at six heights
            foreach (float deg in new[] { -62f, -38f, -13f, 12f, 37f, 60f })
            {
                float a = deg * Mathf.Deg2Rad, r = NetRx * Mathf.Cos(a);
                CbArt.Part(CbMesh.Torus(0.024f / r), rope, net, new Vector3(0, NetRy * Mathf.Sin(a), 0), Vector3.one * r, shadows: false);
            }
            CbArt.Sphere(rope, net, new Vector3(0, NetRy, 0), 0.08f);
            return net;
        }

        /// <summary>
        /// The prologue, drawn (scene 1 of the script): the cloud temple and its Source, the iron crown's attack, the
        /// Source shattering, Baba leaping from the falling temple with the newborn cub, the star falling to the
        /// jungle; then the title. Plays before the first stage.
        /// </summary>
        public static readonly CbComic.Shot[] Prologue =
        {
            new CbComic.Shot { image = "prologue_temple", dur = 5f, from = new Vector3(0.5f, 0.42f, 1f), to = new Vector3(0.5f, 0.5f, 1.18f), fx = "stars",
                caption = "Long ago, above the clouds, a temple kept the Source of all the elements." },
            new CbComic.Shot { image = "prologue_crown", dur = 4f, from = new Vector3(0.45f, 0.5f, 1.3f), to = new Vector3(0.5f, 0.5f, 1.05f), fx = "purple",
                shake = true, sfx = "roar", caption = "Then a crown of black iron came for it." },
            new CbComic.Shot { image = "prologue_shatter", dur = 3.8f, from = new Vector3(0.5f, 0.55f, 1f), to = new Vector3(0.5f, 0.45f, 1.25f), fx = "sparks",
                flash = true, sfx = "heavy", caption = "The Source broke. Its four crystals flew to the four corners of the world..." },
            new CbComic.Shot { image = "prologue_leap", dur = 4.4f, from = new Vector3(0.35f, 0.55f, 1.2f), to = new Vector3(0.62f, 0.45f, 1.2f), fx = "embers",
                shake = true, caption = "...and its last spark fell into the night, with a newborn cub." },
            new CbComic.Shot { image = "prologue_jungle", dur = 5f, from = new Vector3(0.3f, 0.65f, 1.3f), to = new Vector3(0.45f, 0.5f, 1f), fx = "fireflies",
                who = "BABA ORANG", caption = "There are things that fall from the sky. And there are things that are sent." },
            new CbComic.Shot { title = "RAJA", subtitle = "AND THE FOUR CRYSTALS", dur = 3.2f, fx = "embers" },
        };

        /// <summary>Drawn, as the opening begins: Koda on a tree stump, puffed up with pride, and Raja not impressed.</summary>
        public static readonly CbComic.Shot[] KodaBoast =
        {
            new CbComic.Shot { image = "koda_boast", dur = 3.4f, from = new Vector3(0.62f, 0.45f, 1.3f), to = new Vector3(0.5f, 0.5f, 1.05f),
                who = "KODA", caption = "Bow down, everyone! The child of the prophecy is here!" },
        };

        /// <summary>
        /// Drawn, when Koda is carried off: Koda calls out from the net; Raja's anger, only a faint flicker of fire in his
        /// eyes (he has no powers yet, and no one but Baba knows what he is).
        /// </summary>
        public static readonly CbComic.Shot[] KodaTaken =
        {
            new CbComic.Shot { image = "koda_taken", dur = 2.6f, from = new Vector3(0.5f, 0.5f, 1.05f), to = new Vector3(0.5f, 0.55f, 1.25f), shake = true,
                who = "KODA", caption = "RAJAAA!" },
            new CbComic.Shot { image = "raja_angry", dur = 2.2f, from = new Vector3(0.5f, 0.5f, 1f), to = new Vector3(0.5f, 0.48f, 1.3f) },
        };

        /// <summary>
        /// Drawn, as the opening ends: at the village gate Raja ties his headband tight, staff in hand and a bundle on
        /// his back, ready for the road and the fight. The view slowly closes in on his face.
        /// </summary>
        public static readonly CbComic.Shot[] Journey =
        {
            new CbComic.Shot { image = "raja_journey", dur = 4.4f, from = new Vector3(0.5f, 0.45f, 1f), to = new Vector3(0.47f, 0.7f, 1.4f),
                caption = "No powers. Just a staff, and a friend to bring home." },
        };

        /// <summary>
        /// Drawn, as stage 1-3 begins (the story doc, "Baba's lesson"): Baba asleep in his hammock wakes when a spark lands
        /// on his beard. "Fire held by the heart doesn't burn. It protects." Raja asks who the child of the prophecy is;
        /// Baba, yawning, won't say.
        /// </summary>
        public static readonly CbComic.Shot[] BabaLesson =
        {
            new CbComic.Shot { image = "baba_lesson1", dur = 3.8f, from = new Vector3(0.5f, 0.5f, 1f), to = new Vector3(0.42f, 0.55f, 1.2f), fx = "embers",
                caption = "Baba's grove. Baba, as ever, was asleep..." },
            new CbComic.Shot { image = "baba_lesson2", dur = 4.4f, from = new Vector3(0.42f, 0.5f, 1.1f), to = new Vector3(0.44f, 0.5f, 1.22f),
                who = "BABA ORANG", caption = "Fire held by the heart doesn't burn. It protects." },
            new CbComic.Shot { image = "baba_lesson2", dur = 3.4f, from = new Vector3(0.62f, 0.45f, 1.22f), to = new Vector3(0.6f, 0.45f, 1.28f),
                who = "RAJA", caption = "Baba, who is the child of the prophecy?" },
            new CbComic.Shot { image = "baba_lesson2", dur = 4.6f, from = new Vector3(0.38f, 0.58f, 1.28f), to = new Vector3(0.42f, 0.52f, 1.12f),
                who = "BABA ORANG", caption = "(yawning) Whoever passes the lesson. Now show me - and don't burn my beard." },
        };

        /// <summary>
        /// The end of stage 1-4 (the story doc): the saw barge sunk, Koda's barge strikes the rocks and breaks up - but a
        /// rope comes down from an airship in the storm and takes his cage up into the sky.
        /// </summary>
        public static CbScene RiverEnd(CbGame g)
        {
            var s = new CbScene(g, "RiverEnd");
            var hero = g.hero;
            float bx = 0;
            s.Do(() =>
            {
                hero.facing = 1;
                s.Raja("Combat_Stance");
                bx = g.level.KodaBargeX;
                s.Camera((hero.x + bx) * 0.5f, CbLevel.RiverFloor(hero.x) + 2.2f, 1.1f, 1.6f);
            });
            s.Wait(1.2f);
            s.Do(() => g.level.KodaBargeBreaks());
            s.Wait(1.0f);
            s.Say("RAJA", RajaCol, "Koda! Hold on - I'm coming!", () => s.Raja("Combat_Stance"));
            s.Wait(2.6f, k => g.level.LiftKodaCage(k), () =>
            {
                CbFx.Sfx("whistle", 0.6f);
                s.Camera(bx, CbLevel.RiverFloor(bx) + 6f, 1.25f, 1.2f);
            });
            s.Say("KODA", KodaCol, "RAJAAA! ...Since when do hyenas have AIRSHIPS?!");
            s.Say("RAJA", RajaCol, "The Ember Temple... that's where it's headed. I'm not letting you go, Koda.");
            s.Finally(() => { hero.facing = 1; g.level.LiftKodaCage(1f); });
            s.Then(g.StageCleared);
            return s;
        }

        /// <summary>
        /// Stage 1-3's secret (the story doc): behind a scroll by Baba's hut, an old painting of a cub asleep on a cloud,
        /// a spark on its chest, the cloud temple behind. Raja doesn't know what it means. (Only Baba does.)
        /// </summary>
        public static readonly CbComic.Shot[] CubInTheClouds =
        {
            new CbComic.Shot { image = "cub_clouds", dur = 4.6f, from = new Vector3(0.5f, 0.5f, 1f), to = new Vector3(0.5f, 0.47f, 1.18f), fx = "embers",
                who = "RAJA", caption = "Hidden behind Baba's scroll... a cub, asleep on a cloud?" },
        };

        /// <summary>
        /// Drawn, as stage 1-5 begins (the story doc, "the crystal torn out"): Grak tears the fire crystal from its altar
        /// in the Ember Temple, and a line of fire runs out between the trees.
        /// </summary>
        public static readonly CbComic.Shot[] CrystalTorn =
        {
            new CbComic.Shot { image = "crystal_torn", dur = 3.8f, from = new Vector3(0.45f, 0.5f, 1f), to = new Vector3(0.36f, 0.48f, 1.2f), fx = "embers",
                caption = "The Ember Temple. On its altar, for a thousand years, the fire crystal..." },
            new CbComic.Shot { image = "crystal_torn", dur = 3.2f, from = new Vector3(0.3f, 0.45f, 1.2f), to = new Vector3(0.28f, 0.45f, 1.3f), fx = "embers",
                flash = true, shake = true, sfx = "heavy", caption = "...until Commander Grak tore it out." },
            new CbComic.Shot { image = "crystal_torn", dur = 4.2f, from = new Vector3(0.5f, 0.62f, 1.18f), to = new Vector3(0.5f, 0.6f, 1.3f), fx = "embers",
                who = "COMMANDER GRAK", caption = "This jungle will burn anyway. I'm just getting ahead." },
        };

        /// <summary>
        /// Drawn, before Grak's fight (the story doc, "the stripes light up"; CbStory.GrakIntro): the crystal knocked out of
        /// Grak's fist rolls to Raja; at his touch a flash like a falling star, and his stripes light up gold. Until then
        /// he had no powers.
        /// </summary>
        public static readonly CbComic.Shot[] FireWakes =
        {
            new CbComic.Shot { image = "stripes_ignite", dur = 2.6f, from = new Vector3(0.62f, 0.4f, 1.25f), to = new Vector3(0.58f, 0.42f, 1.1f), fx = "embers",
                caption = "The crystal rolled to his feet. He touched it..." },
            new CbComic.Shot { image = "stripes_ignite", dur = 3f, from = new Vector3(0.45f, 0.5f, 1.05f), to = new Vector3(0.42f, 0.52f, 1.25f), fx = "sparks",
                flash = true, shake = true, sfx = "roar", caption = "...and his stripes caught fire. Gold." },
            new CbComic.Shot { image = "stripes_ignite", dur = 3.6f, from = new Vector3(0.36f, 0.55f, 1.25f), to = new Vector3(0.36f, 0.55f, 1.32f), fx = "sparks",
                who = "RAJA", caption = "(to his fire) I'm not afraid of you anymore." },
        };

        /// <summary>
        /// Drawn, the end of chapter 1 (the story doc, "the ship in the sky"): an airship over the burnt jungle, Koda caged
        /// on its deck; the Crown's purple eye opens as it sees Raja's stripes.
        /// </summary>
        public static readonly CbComic.Shot[] ShipInTheSky =
        {
            new CbComic.Shot { image = "ship_in_sky", dur = 3.6f, from = new Vector3(0.6f, 0.4f, 1f), to = new Vector3(0.55f, 0.42f, 1.15f), fx = "embers",
                caption = "High over the burnt jungle, an airship of the Iron Crown..." },
            new CbComic.Shot { image = "ship_in_sky", dur = 3.4f, from = new Vector3(0.4f, 0.55f, 1.2f), to = new Vector3(0.42f, 0.55f, 1.3f),
                who = "GENERAL VRASK", caption = "We took the wrong cub." },
            new CbComic.Shot { image = "ship_in_sky", dur = 4.6f, from = new Vector3(0.75f, 0.75f, 1.2f), to = new Vector3(0.78f, 0.78f, 1.35f), fx = "purple",
                who = "THE CROWN", caption = "(whispering) Leave the wolf. The tiger will come after him... and bring us all of them." },
            new CbComic.Shot { title = "END OF CHAPTER 1", subtitle = "The fire crystal is Raja's. Koda is still a prisoner.", dur = 3.4f, fx = "embers" },
        };

        /// <summary>
        /// The opening cinematic (the story doc, "The opening cinematic", the user's dialogue): played in the engine
        /// like a short film, its lines going on by themselves (a tap hurries them). Raja is shy, hesitant and a
        /// worrier, his little rituals shown but never named (the knot tied again and again, counting before a jump,
        /// stones set in a row). Shot 1, the prologue, is the drawn one before it (CbGame.PlayPrologue).
        /// 2. Raja's morning at the village gate: the knot, the stones, Koda bursting in. 3. By the river's stepping
        /// stones: Koda jumps, Raja counts and doesn't. 4. Koda's boast to the village kids, Raja hiding behind him.
        /// 5. The wrong cub: the raiders net Koda while Raja freezes behind a log; Koda's scarf falls. 6. Counting to
        /// three: Baba's word, the scarf tied to the staff, and this time he runs.
        /// </summary>
        public static CbScene Opening(CbGame g)
        {
            var s = new CbScene(g, "Opening") { AutoLines = true };
            CbActor koda = null, snap = null, r1 = null, r2 = null, kid1 = null, kid2 = null, baba = null;
            var hero = g.hero;
            var stones = new Transform[3];
            Transform scarf = null, hideLog = null;
            // counting over his head, as he says it
            void Count(string n, bool big = false) => g.hud.Popup(new Vector3(hero.x, 2.5f, 0), n, big ? CbHud.PopStyle.Big : CbHud.PopStyle.Normal);
            // a cut to somewhere else, through a quick fade
            // (SKIPped while the screen is dark, the scene is over: nobody is moved to the next place)
            void CutTo(System.Action place) => g.fade.FadeThrough(() => { if (!s.Done) place(); }, 0.25f, 0.35f);

            // ---- 2. Raja's morning: at the village gate he ties his red headband, unties it, ties it again; sets three
            // stones in a row before he goes; Koda bursts in
            s.Do(() =>
            {
                hero.x = GateX; hero.facing = 1;
                s.Cut(GateX + 0.5f, 1.25f, 0.42f);
                s.RajaOnce("Power_Gather", 0.05f, 0.3f, 1.1f);
                hideLog = CbArt.Joint(s.stage, LogX, 0, 0.35f, "hideLog");
                if (CbArt.Prop("Cloudborne/Jungle/Log/Log", hideLog, CbArt.PropOutline) != null) hideLog.localScale = new Vector3(0.75f, 0.87f, 0.87f);
            });
            s.Wait(1.3f);
            s.Wait(1.2f, null, () => { s.RajaOnce("Power_Gather", 0.05f, 0.3f, 1.1f); CbFx.Sfx("swing", 0.3f); });
            s.Wait(1.8f, u =>
            {
                for (int k = 0; k < 3; k++)
                    if (stones[k] == null && u > 0.2f + k * 0.28f)
                    {
                        stones[k] = CbArt.Joint(s.stage, GateX + 1.1f + k * 0.32f, 0, -0.45f, "stone");
                        if (CbArt.Prop("Cloudborne/Jungle/RiverStone/RiverStone", stones[k], CbArt.PropOutline) != null) stones[k].localScale = Vector3.one * 0.16f;
                        else CbArt.Part(CbMesh.Sphere, CbArt.Toon("#8a8a84"), stones[k], new Vector3(0, 0.07f, 0), new Vector3(0.2f, 0.14f, 0.18f));
                        CbFx.Sfx("pickup", 0.25f);
                    }
            }, () => { s.Raja("Motion_Crouch"); s.Camera(GateX + 1.2f, 0.9f, 0.4f, 2f); });
            s.Do(() =>
            {
                s.Raja("Idle");
                koda = s.Cast("Koda", KodaModels, 1.72f, KodaStyle, RigBuilder.WolfCub, GateX + 9f, -1);
                koda.WalkTo(GateX + 2.6f, 6.5f, "RunFast");
                s.Camera(GateX + 1.6f, 1.35f, 0.6f, 2f);
            });
            s.Wait(1.0f);
            s.Say("KODA", KodaCol, "Raja! Get up! Today I show everyone who the prophecy kid is!",
                () => koda.Play("Show_Both_Arm_Muscles"));
            s.Say("RAJA", RajaCol, "Wait... one more time. The knot doesn't sit right.",
                () => s.RajaOnce("Power_Gather", 0.05f, 0.3f, 1.1f));
            s.Say("KODA", KodaCol, "That's your third knot this morning!", () => koda.Once("Look_Around_Dumbfounded", 0.1f, 0.6f, 1.2f));
            s.Say("RAJA", RajaCol, "(quietly) Fourth.", () => { s.Raja("Idle"); s.Camera(hero.x + 0.2f, 1.4f, 0.38f, 3f); });

            // ---- 3. By the river's stepping stones: Koda jumps without a thought, Raja stands at the edge counting
            s.Wait(0.6f, null, () => CutTo(() =>
            {
                hero.x = RiverBank; hero.facing = 1; s.Raja("Idle");
                koda.x = RiverBank + 1f; koda.facing = 1; koda.Play("Idle");
                s.Cut(RiverBank + 2.6f, 1.3f, 0.78f);
            }));
            float hopFrom = 0, hopTo = 0;
            foreach (var to in RiverHops)
            {
                float target = to;
                s.Wait(0.55f, u =>
                {
                    koda.x = Mathf.Lerp(hopFrom, hopTo, u);
                    koda.y = 1.4f * 4f * u * (1 - u);
                }, () =>
                {
                    hopFrom = koda.x; hopTo = target;
                    koda.Once("Hop_with_Arms_Raised", 0.1f, 0.6f, 0.55f);
                    CbFx.Sfx("jump", 0.4f);
                });
                s.Wait(0.25f, null, () => { koda.y = 0; CbFx.Spark(new Vector3(koda.x, 0.1f, -0.3f), CbFx.Water, 6, 2f, 0.25f, 0.3f, 6); });
            }
            s.Do(() => { koda.facing = -1; koda.Play("Idle"); s.Camera(RiverBank + 1.4f, 1.4f, 0.55f, 2f); });
            s.Say("RAJA", RajaCol, "(whispering) One... two... two and a half...", null, t =>
            {
                if (t > 0.3f && t - Time.deltaTime <= 0.3f) Count("1...");
                if (t > 1.0f && t - Time.deltaTime <= 1.0f) Count("2...");
                if (t > 1.7f && t - Time.deltaTime <= 1.7f) Count("2½...");
            });
            s.Say("KODA", KodaCol, "You always count, and you never jump.");
            s.Say("RAJA", RajaCol, "What if the rock is slippery? What if I fall? What if...", () => s.RajaOnce("Block1", 0.1f, 0.4f, 0.6f));
            s.Say("KODA", KodaCol, "(grinning) What if you just jump?", () => koda.Once("Show_Both_Arm_Muscles", 0.1f, 0.7f, 1.2f));

            // ---- 4. Morning in the village: Koda boasts to the kids from the log; when they look at Raja, he hides
            // behind Koda, blushing
            s.Wait(0.6f, null, () => CutTo(() =>
            {
                s.Raja("Idle");
                hero.x = GateX + 0.4f; hero.facing = 1;
                koda.x = LogX; koda.y = 0.62f; koda.facing = -1; koda.Play("Show_Both_Arm_Muscles");
                kid1 = s.Cast("Kid", KidModels, 0.85f, KodaStyle, null, LogX - 2.4f, 1);
                kid2 = s.Cast("Kid", KidModels, 0.8f, KodaStyle, null, LogX - 3.2f, 1);
                kid1.Play("Hop_with_Arms_Raised"); kid2.Play("Hop_with_Arms_Raised");
                s.Cut(LogX - 1.2f, 1.4f, 0.75f);
            }));
            s.Say("KODA", KodaCol, "The prophecy kid? That's me! Ever seen a wolf with an ear like this?");
            s.Say("RAJA", RajaCol, "The prophecy kid is supposed to save the whole jungle, Koda. You fell out of a tree yesterday.", () =>
            {
                // the kids turn to look at him: he slips behind Koda, red in the face
                kid1.facing = kid2.facing = 1;
                s.RajaRunTo(LogX + 0.9f, 2.2f);
                CbFx.Spark(new Vector3(hero.x + 0.15f, 1.5f, -0.5f), new Color(1f, 0.45f, 0.5f) * 1.6f, 4, 0.4f, 0.1f, 0.6f, 0);
            });
            s.Say("KODA", KodaCol, "That tree attacked me first!", () => koda.Once("Hit_Reaction", 0.1f, 0.7f, 0.6f));

            // ---- 5. The wrong cub: the raiders come out of the trees; Raja freezes and hides behind the log; the net
            // takes Koda, and his yellow scarf falls by the log
            s.Wait(1.6f, null, () =>
            {
                CbFx.Sfx("saw", 0.8f);
                g.shake = Mathf.Max(g.shake, 0.3f);
                kid1.WalkTo(LogX - 14f, 7f, "RunFast"); kid2.WalkTo(LogX - 14f, 7f, "RunFast");
                koda.y = 0; koda.x = LogX + 1.6f; koda.facing = 1; koda.Play("Idle");
                s.RajaRunTo(LogX - 0.8f, 4f);
                s.Camera(LogX + 3f, 1.6f, 0.95f, 1.8f);
                snap = s.Cast("SgtSnap", SnapModels, 2.55f, HyenaStyle(1.2f, 1.15f), RigBuilder.HyenaHead, LogX + 17f, -1, Machete);
                r1 = s.Cast("Raider", RaiderModels, 2.25f, HyenaStyle(1f, 1f), RigBuilder.HyenaHead, LogX + 18.6f, -1, Machete);
                r2 = s.Cast("Raider", RaiderModels, 2.25f, HyenaStyle(1f, 1f), RigBuilder.HyenaHead, LogX + 20.2f, -1, Machete);
                foreach (var a in new[] { snap, r1, r2 }) a.Play("Combat_Stance");
                snap.WalkTo(LogX + 6.4f, 6.5f);
                r1.WalkTo(LogX + 3.8f, 7f);
                r2.WalkTo(LogX + 5.1f, 7f);
            });
            s.Do(() => { s.Raja("Motion_Crouch"); hero.facing = 1; });
            s.Say("SGT. SNAP", SnapCol, "Search the village! The prophecy kid is here somewhere!", () =>
            {
                CbFx.Sfx("whistle", 0.7f);
                snap.Once("Shouting_Angrily", 0f, 1f, 2.2f);
            });
            s.Say("KODA", KodaCol, "Looking for the prophecy kid? Here he is!", () =>
            {
                snap.Play("Combat_Stance");
                koda.Play("Show_Both_Arm_Muscles");
                koda.WalkTo(LogX + 2.6f, 3f, "Excited_Walk_M");
            });
            Transform net = null;
            s.Wait(0.5f, u =>
            {
                net.localPosition = new Vector3(0, NetCentre + 2.6f * (1 - u) * (1 - u), 0);
                net.localScale = Vector3.one * Mathf.Lerp(1.3f, 1f, u);
            }, () =>
            {
                CbFx.Sfx("swing", 0.8f);
                net = NetSack(koda.f.root, koda.f.M(CbArt.Hex("#7a5a30"), 0f));
                koda.struggle = true;
                koda.Play("Head_Hold_in_Pain");
                r1.Once("Right_Hand_Sword_Slash", 0.1f, 0.5f, 0.4f);
                // his scarf comes loose and falls by the log
                scarf = CbArt.Joint(s.stage, koda.x - 0.4f, 1.2f, -0.3f, "kodaScarf");
                CbArt.Part(CbMesh.Box(), CbArt.Toon("#f2c230", CbArt.PropOutline), scarf, Vector3.zero, new Vector3(0.42f, 0.07f, 0.16f), Quaternion.Euler(0, 0, 18f));
            });
            s.Wait(0.6f, u => { if (scarf != null) scarf.localPosition = new Vector3(Mathf.Lerp(koda.x - 0.4f, LogX + ScarfDrop, u), Mathf.Lerp(1.2f, 0.05f, u * u), -0.3f); });
            s.Say("KODA", KodaCol, "...wait, why the net?! RAJA!", () => s.Camera(LogX + 0.6f, 1.3f, 0.7f, 2.5f));
            s.Say("RAJA", RajaCol, "(to himself) I can't. I'm not brave like Koda.", () => s.Camera(hero.x + 0.3f, 1.0f, 0.42f, 3f));
            s.Say("SGT. SNAP", SnapCol, "The wolf comes with us. The General knows what to do with the prophecy kid.", () =>
            {
                snap.Once("Shouting_Angrily", 0.15f, 0.85f, 1.8f);
                s.Camera(LogX + 3.4f, 1.6f, 0.95f, 2f);
            });
            // two raiders carry him off between them on a bamboo pole; Raja stays hidden
            Transform pole = null;
            s.Wait(0.45f, u =>
            {
                koda.y = Mathf.Lerp(0, PoleHeight - NetCentre - NetRy - 0.02f, u);
                pole.localScale = new Vector3(Mathf.Abs(r2.x - r1.x) + 0.7f, 1, 1) * Mathf.Min(1, u * 3);
            }, () =>
            {
                r1.WalkTo(koda.x - 0.85f, 6f);
                r2.WalkTo(koda.x + 0.85f, 6f);
                pole = CbArt.Joint(s.stage, koda.x, PoleHeight, 0.05f, "pole");
                CbArt.Cylinder(koda.f.M(CbArt.Hex("#c9a24e")), pole, Vector3.zero, 0.05f, 1f, CbArt.Rot(0, 0, Mathf.PI / 2));
                pole.localScale = Vector3.zero;
            });
            s.Wait(2.2f, null, () =>
            {
                s.Always = () =>
                {
                    float mid = (r1.x + r2.x) * 0.5f;
                    koda.x = mid;
                    koda.facing = 1;
                    koda.y = PoleHeight - NetCentre - NetRy - 0.02f + Mathf.Abs(Mathf.Sin(g.clock * 9)) * 0.05f;
                    pole.localPosition = new Vector3(mid, PoleHeight, 0.05f);
                    pole.localScale = new Vector3(Mathf.Abs(r2.x - r1.x) + 0.7f, 1, 1);
                };
                r1.WalkTo(LogX + 26f, 6.5f);
                r2.WalkTo(LogX + 27.7f, 6.5f);
                snap.WalkTo(LogX + 29.5f, 7f);
            });

            // ---- 6. Counting to three: the forest quiet; Raja picks up the scarf; Baba, yawning, has a word; Raja ties
            // the scarf to his staff, counts - and this time he runs
            s.Wait(1.2f, null, () =>
            {
                s.Always = null;
                s.Raja("Idle");
                // round the end of the log to the scarf, not over the log
                s.RajaRunTo(LogX + ScarfDrop, 1.6f);
                s.Camera(LogX + ScarfDrop, 1.1f, 0.5f, 2f);
            });
            s.Wait(1.0f, null, () =>
            {
                s.RajaOnce("Motion_Crouch", 0f, 1f, 0.8f);
                if (scarf != null) scarf.gameObject.SetActive(false);
                baba = s.Cast("Baba", BabaModels, 2.0f, BabaStyle, null, LogX - 3.2f, 1);
                baba.WalkTo(LogX - 1.4f, 1.4f, "Casual_Walk");
            });
            s.Wait(1.2f, null, () => { baba.Play("Idle_02"); s.Camera(LogX - 0.2f, 1.4f, 0.65f, 2f); });
            s.Say("BABA ORANG", BabaCol, "(yawning) Courage isn't when you're not afraid. It's when you're afraid, and you go anyway.",
                () => { s.Raja("Idle"); hero.facing = -1; baba.Once("Dozing_Elderly", 0.1f, 0.3f, 1.2f); });
            s.Wait(1.4f, null, () =>
            {
                hero.facing = 1;
                s.RajaOnce("Power_Gather", 0.05f, 0.35f, 1.3f);
                // Koda's scarf tied to his staff
                // (once: played again from the menu, the scarf is already there)
                var staff = hero.Staff;
                if (staff != null && staff.Find("kodaScarf") == null)
                {
                    // near the top, a knot and two long tails, big enough to read on a phone
                    var yellow = CbArt.Toon("#f2c230", CbArt.PropOutline);
                    var tie = CbArt.Joint(staff, 0, 0.82f, 0, "kodaScarf");
                    CbArt.Part(CbMesh.Box(), yellow, tie, Vector3.zero, new Vector3(0.17f, 0.12f, 0.17f));
                    CbArt.Part(CbMesh.Box(), yellow, tie, new Vector3(0.16f, -0.24f, 0), new Vector3(0.11f, 0.5f, 0.04f), Quaternion.Euler(0, 0, -24f));
                    CbArt.Part(CbMesh.Box(), yellow, tie, new Vector3(0.05f, -0.2f, 0.02f), new Vector3(0.09f, 0.4f, 0.04f), Quaternion.Euler(0, 0, -8f));
                }
                s.Camera(hero.x + 0.2f, 1.35f, 0.4f, 3f);
            });
            s.Say("RAJA", RajaCol, "One... two... three.", null, t =>
            {
                if (t > 0.3f && t - Time.deltaTime <= 0.3f) Count("1", true);
                if (t > 0.9f && t - Time.deltaTime <= 0.9f) Count("2", true);
                if (t > 1.5f && t - Time.deltaTime <= 1.5f) Count("3!", true);
            });
            s.Wait(1.6f, null, () =>
            {
                s.Raja("Combat_Stance");
                s.RajaRunTo(RunEnd, 6.5f);
                s.Camera(RunEnd - 2f, 1.5f, 1f, 1.6f);
                g.hud.Banner("1-1 THE JUNGLE GATE");
            });

            s.Finally(() =>
            {
                hero.x = Mathf.Clamp(hero.x, GateX, RunEnd);
                if (hero.x > 60f) hero.x = GateX;
                hero.facing = 1;
            });
            s.Then(g.OpeningDone);
            return s;
        }

        // the opening's places: Raja's spot at the village gate, the log he hides behind, the river bank by the
        // stepping stones (stage 1-1's long river) and Koda's hops across them
        const float GateX = -4.6f, LogX = -1.2f, RiverBank = 78.3f;
        // where Koda's scarf lands: on the path past the log's right end
        const float ScarfDrop = 1.5f;
        // where his run at the end stops: well short of the stage's first crate (at 9 m), which he ran into, and
        // through, before
        const float RunEnd = 5.5f;
        static readonly float[] RiverHops = { 80.6f, 82.2f };
        static readonly string[] KidModels = { "Cloudborne/Monkey/Monkey" };
        static readonly string[] BabaModels = { "Cloudborne/Baba/Baba" };
        public static readonly Color BabaCol = CbArt.Hex("#f0a050");
        static RigStyle BabaStyle => new RigStyle
        {
            fur = CbArt.Hex("#c8642a"), paw = CbArt.Hex("#9a4a20"), top = CbArt.Hex("#c8642a"), pants = CbArt.Hex("#6a4a2a"),
            belt = CbArt.Hex("#3a2a1a"), wrap = CbArt.Hex("#efe6d4"), scale = 1.05f,
        };

        /// <summary>
        /// Before Grak's fight (stage 1-5; the user: Raja gets his fire before the boss, and fights him with it): in the
        /// temple's courtyard Grak holds the fire crystal up, about to fix it to his armour, and laughs at the cub. Raja
        /// counts to three, as in the opening, and this time he goes: a blow knocks the crystal out of Grak's fist, it
        /// rolls to him, and at his touch his stripes light up (drawn). Then the fight.
        /// </summary>
        public static CbScene GrakIntro(CbGame g, float arenaX)
        {
            var s = new CbScene(g, "GrakIntro");
            CbActor grak = null;
            Transform crystal = null;
            var hero = g.hero;
            float stand = arenaX + 2.5f;
            bool awake = false;
            void Awake()
            {
                if (awake) return;
                awake = true;
                g.level?.CrystalTaken();
                g.AwakenFire();
            }
            s.Do(() =>
            {
                hero.facing = 1;
                s.Raja("Idle");
                grak = s.Cast("Grak", GrakModels, CbEnemy.GrakHeight, GrakStyle, null, stand, -1);
                grak.Once("Charged_Spell_Cast", 0f, 0.45f, 1.4f);
                // the crystal held up over his head, flaring
                crystal = CbArt.Joint(s.stage, stand - 0.3f, CbEnemy.GrakHeight + 0.5f, -0.3f, "heldCrystal");
                if (CbArt.Prop(CrystalModel, crystal, CbArt.PropOutline) is Transform m)
                {
                    foreach (var c in m.GetComponentsInChildren<Transform>(true))
                        if (c.name != "Crystal" && c != m && c.GetComponent<Renderer>() != null) c.gameObject.SetActive(false);
                    m.localScale = Vector3.one * 0.7f;
                }
                else CbArt.Part(CbMesh.Sphere, CbArt.Glow(new Color(1f, 0.4f, 0.1f), CbArt.Shaft), crystal, new Vector3(0, 0.3f, 0), new Vector3(0.3f, 0.6f, 0.3f));
                s.Camera((hero.x + stand) * 0.5f, 2.2f, 1f, 2f);
            });
            s.Wait(1.4f, u =>
            {
                if (crystal != null && Random.value < 0.5f) CbFx.Spark(crystal.position, CbFx.Fire, 1, 1.5f, 0.3f, 0.5f, -3);
            });
            s.Say("COMMANDER GRAK", GrakCol, "The heart of the Ember Temple. On my armour, it burns the whole jungle down.",
                () => grak.Once("Chest_Pound_Taunt", 0f, 1f, 1.7f));
            s.Say("COMMANDER GRAK", GrakCol, "And here comes a little cub to watch. Your wolf friend is already in a cage.");
            s.Say("RAJA", RajaCol, "(quietly) One... two...", () => { s.Raja("Combat_Stance"); s.Camera(hero.x + 0.6f, 1.4f, 0.55f, 3f); });
            s.Say("RAJA", RajaCol, "THREE!", () =>
            {
                s.RajaRunTo(stand - CbEnemy.GrakHalf - 0.7f, 10f);
                s.Camera(stand - 2f, 2f, 0.9f, 3f);
            });
            // the blow: the crystal knocked out of his fist, rolling across the stones to Raja
            s.Wait(0.5f, null, () =>
            {
                s.RajaOnce("Punch_Combo", 0.15f, 0.5f, 0.5f);
                grak.Once("Hit_Reaction", 0.1f, 0.6f, 0.6f);
                CbFx.Sfx("heavy");
                g.shake = Mathf.Max(g.shake, 0.6f);
                var at = crystal != null ? crystal.position : new Vector3(stand, 4f, -0.3f);
                CbVfx.Burst(at, CbFx.Hit, 2.4f, 0.2f);
                if (crystal != null) crystal.gameObject.SetActive(false);
                g.level?.RollCrystal(new Vector3(at.x, at.y, -0.2f), hero.x - 1.6f);
                g.hud.Popup(new Vector3(stand, CbEnemy.GrakHeight + 1f, 0), "THE CRYSTAL!", CbHud.PopStyle.Big);
            });
            s.Wait(CbLevel.CrystalRoll + 0.2f, null, () => s.Camera(hero.x - 1f, 1.2f, 0.55f, 2.5f));
            s.Do(() => { hero.facing = -1; s.RajaOnce("Motion_Crouch", 0f, 1f, 0.6f); });
            s.Wait(0.6f);
            if (CbComic.Available(FireWakes)) s.Comic(FireWakes);
            s.Do(Awake);
            s.Wait(1.2f, null, () => { hero.facing = 1; s.Raja("Combat_Stance"); s.Camera((hero.x + stand) * 0.5f, 2f, 1f, 2f); });
            s.Say("COMMANDER GRAK", GrakCol, "That fire is MINE! Give it back, cub!", () => grak.Once("Chest_Pound_Taunt", 0f, 1f, 1.7f));
            s.Say("RAJA", RajaCol, "Come and take it.");
            // (SKIPped, his fire wakes all the same)
            s.Finally(() => { Awake(); hero.facing = 1; });
            s.Then(() => g.SpawnGrak(stand));
            return s;
        }

        static readonly string[] GrakModels = { CbEnemy.GrakPath };
        const string CrystalModel = "Cloudborne/Ember/FireCrystal/FireCrystal";
        public static readonly Color GrakCol = CbArt.Hex("#c9a07a");
        static RigStyle GrakStyle => new RigStyle
        {
            fur = CbArt.Hex("#7a7d80"), paw = CbArt.Hex("#5a5d60"), armor = CbArt.Hex("#3d434b"), pants = CbArt.Hex("#3a2e26"),
            belt = CbArt.Hex("#2a1f1a"), wrap = CbArt.Hex("#4a3a2e"), bulk = 1.5f, scale = 1.3f,
        };

        /// <summary>
        /// Before the fight at the end of 1-1 (scene 4 of the script): Sergeant Snap steps out in front of the shrine
        /// gate and laughs at the kid with a stick. The fight begins where he stands.
        /// </summary>
        public static CbScene SnapIntro(CbGame g, float arenaX)
        {
            var s = new CbScene(g, "SnapIntro");
            CbActor snap = null;
            var hero = g.hero;
            float stand = arenaX + 4f;
            s.Do(() =>
            {
                hero.facing = 1;
                s.Raja("Combat_Stance");
                snap = s.Cast("SgtSnap", SnapModels, 2.55f, HyenaStyle(1.2f, 1.15f), RigBuilder.HyenaHead, arenaX + 11f, -1, Machete);
                snap.Play("Combat_Stance");
                snap.WalkTo(stand, 4.5f);
                s.Camera((hero.x + stand) * 0.5f, 1.7f, 0.85f, 2f);
            });
            s.Wait(1.5f);
            s.Say("SGT. SNAP", SnapCol, "A kid with a stick? I thought they'd send someone interesting.",
                () => { snap.facing = -1; snap.Once("Shouting_Angrily", 0f, 1f, 2.2f); });
            s.Say("RAJA", RajaCol, "Where is Koda?!", () => s.Raja("Combat_Stance"));
            s.Say("SGT. SNAP", SnapCol, "Halfway to the General by now. And you're not going anywhere!", () =>
            {
                CbFx.Sfx("whistle", 0.6f);
                snap.Play("Combat_Stance");
            });
            s.Finally(() => hero.facing = 1);
            s.Then(() => g.SpawnBoss(stand));
            return s;
        }

        /// <summary>
        /// The end of stage 1-2 (the story doc, "The empty cage": after Tusk, Raja breaks the cage, but it is empty; on
        /// the ground lies a scroll with the prophecy and the seal of the Iron Crown. Raja, reading: "'A child born
        /// outside the jungle, with fire in his stripes...' But Koda was born right here."). He runs to the cage, smashes
        /// it with the staff, finds it empty, picks up the scroll and reads it; then the stage is clear.
        /// </summary>
        public static CbScene EmptyCage(CbGame g, float cageX)
        {
            var s = new CbScene(g, "EmptyCage");
            var hero = g.hero;
            float stand = cageX - 1.7f;
            Vector3 from = Vector3.zero;
            // the scroll lifted from the cage's floor up in front of him, opening as it comes
            Vector3 Read() => new Vector3(stand + 0.48f, 1.22f, -0.34f);
            s.Do(() =>
            {
                hero.facing = 1;
                s.RajaRunTo(stand, 5f);
                s.Camera(cageX - 0.9f, 1.5f, 0.82f, 2f);
            });
            s.Wait(1.1f);
            s.Say("RAJA", RajaCol, "Koda! Hold on - I'll get you out!", () => s.Raja("Combat_Stance"));
            s.Wait(0.42f, null, () => s.RajaOnce("Heavy_Hammer_Swing", 0.05f, 0.62f, 0.42f));
            s.Do(() => g.level?.BreakKodaCage());
            s.Wait(1.3f, null, () => { s.RajaOnce("Heavy_Hammer_Swing", 0.62f, 1f, 0.5f); s.Camera(cageX - 0.5f, 1.15f, 0.62f, 1.6f); });
            s.Say("RAJA", RajaCol, "...Empty?", () => s.Raja("Idle"));
            s.Wait(0.6f, null, () => s.RajaOnce("Motion_Crouch", 0f, 0.4f, 0.6f));
            s.Wait(1.2f, k =>
            {
                var sc = g.level?.ProphecyScroll;
                if (sc == null) return;
                float e = Mathf.SmoothStep(0, 1, k);
                sc.localPosition = Vector3.Lerp(from, Read(), e);
                sc.localRotation = Quaternion.Slerp(Quaternion.Euler(0, 25f, 0), Quaternion.identity, e);
                g.level.Unroll(Mathf.InverseLerp(0.4f, 1f, k));
            }, () =>
            {
                s.RajaOnce("Motion_Crouch", 0.4f, 0f, 0.7f);
                if (g.level?.ProphecyScroll != null) from = g.level.ProphecyScroll.localPosition;
                CbFx.Sfx("pickup", 0.6f);
            });
            s.Say("RAJA", RajaCol, "The seal of the Iron Crown...", () => { s.Raja("Idle"); s.Camera(stand + 0.4f, 1.3f, 0.5f, 1.4f); });
            s.Say("RAJA", RajaCol, "\"A child born outside the jungle, with fire in his stripes...\"");
            s.Say("RAJA", RajaCol, "But Koda was born right here.", () => s.Raja("Combat_Stance"));
            // (skipped, it ends the same: the cage broken, the scroll taken)
            s.Finally(() =>
            {
                hero.facing = 1;
                g.level?.BreakKodaCage();
                if (g.level?.ProphecyScroll != null) g.level.ProphecyScroll.gameObject.SetActive(false);
            });
            s.Then(g.StageCleared);
            return s;
        }
    }
}
