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
            ElderCol = CbArt.Hex("#c9d2de");

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

        /// <summary>Drawn, in the opening: Koda calls out from the net, Raja's eyes catch fire, the fire bursts out of him.</summary>
        public static readonly CbComic.Shot[] FireInsert =
        {
            new CbComic.Shot { image = "fire_koda", dur = 2.6f, from = new Vector3(0.5f, 0.5f, 1.05f), to = new Vector3(0.5f, 0.55f, 1.25f), shake = true,
                who = "KODA", caption = "RAJAAA!" },
            new CbComic.Shot { image = "fire_eyes", dur = 1.9f, from = new Vector3(0.5f, 0.5f, 1f), to = new Vector3(0.5f, 0.5f, 1.35f), fx = "embers", flash = true },
            new CbComic.Shot { image = "fire_burst", dur = 2.8f, from = new Vector3(0.5f, 0.45f, 1.3f), to = new Vector3(0.5f, 0.5f, 1.05f), fx = "sparks",
                flash = true, shake = true, sfx = "roar" },
        };

        /// <summary>
        /// Stage 1-1 opens (scenes 2 and 3 of the script): a morning in the village, Koda boasting that he is the child
        /// of the prophecy; the Iron Crown raiders believe him and carry him off in a net; Raja's fire bursts out of him
        /// for the first time, and the chase begins.
        /// </summary>
        public static CbScene Opening(CbGame g)
        {
            var s = new CbScene(g, "Opening");
            CbActor koda = null, snap = null, r1 = null, r2 = null;
            var hero = g.hero;

            s.Do(() =>
            {
                hero.x = -4.5f; hero.facing = 1;
                koda = s.Cast("Koda", KodaModels, 1.72f, KodaStyle, RigBuilder.WolfCub, -1.6f, -1);
                koda.Play("Idle");
                s.Cut(-3f, 1.35f, 0.62f);
                s.Raja("Punch_Combo");
            });
            // the morning: Raja trains by the path, Koda shows off
            s.Wait(1.6f);
            s.Say("KODA", KodaCol, "The prophecy kid? That's me! Ever seen a wolf with an ear like this?",
                () => { s.Raja("Combat_Stance"); koda.Play("Show_Both_Arm_Muscles"); },
                t => { if (koda.model == null) koda.y = t < 1.2f ? Mathf.Abs(Mathf.Sin(t * 7)) * 0.18f : 0; });
            s.Say("RAJA", RajaCol, "The prophecy is about a kid with fire, Koda. Not a kid with a floppy ear.");
            // a spark jumps from his paw: Koda leaps back
            s.Wait(0.9f, u => s.rajaGlow = 1.2f * (1 - u), () =>
            {
                var hand = hero.HandPoint() + new Vector3(0, 0, -0.3f);
                CbFx.Spark(hand, CbFx.Fire, 14, 4, 0.3f, 0.45f);
                CbVfx.Burst(hand, CbFx.Fire, 1.1f, 0.14f);
                CbFx.Sfx("fire", 0.6f);
                s.RajaOnce("Hit_Reaction", 0.1f, 0.6f, 0.6f);
                koda.Once("Hit_Reaction", 0.1f, 0.7f, 0.6f);
                koda.WalkTo(-0.8f, 4f, null);
                koda.facing = -1;
            });
            s.Say("VILLAGE ELDER", ElderCol, "Keep away from that tiger. One day his spark will set us all on fire.",
                () => { s.Raja("Idle"); koda.facing = -1; koda.Play("Idle"); });
            s.Say("RAJA", RajaCol, "...I would never hurt anyone.");

            // the saws: the Iron Crown's raiders come running out of the trees, their sergeant in front
            s.Wait(1.7f, null, () =>
            {
                CbFx.Sfx("saw", 0.8f);
                g.shake = Mathf.Max(g.shake, 0.3f);
                s.Camera(1.2f, 1.6f, 0.95f, 1.8f);
                s.Raja("Combat_Stance");
                koda.facing = 1;
                snap = s.Cast("SgtSnap", SnapModels, 2.55f, HyenaStyle(1.2f, 1.15f), RigBuilder.HyenaHead, 15f, -1, Machete);
                r1 = s.Cast("Raider", RaiderModels, 2.25f, HyenaStyle(1f, 1f), RigBuilder.HyenaHead, 16.6f, -1, Machete);
                r2 = s.Cast("Raider", RaiderModels, 2.25f, HyenaStyle(1f, 1f), RigBuilder.HyenaHead, 18.2f, -1, Machete);
                foreach (var a in new[] { snap, r1, r2 }) a.Play("Combat_Stance");
                snap.WalkTo(4.4f, 6.5f);
                r1.WalkTo(1.8f, 7f);
                r2.WalkTo(3.1f, 7f);
            });
            s.Say("SGT. SNAP", SnapCol, "Search the village! The prophecy kid is here somewhere!", () =>
            {
                CbFx.Sfx("whistle", 0.7f);
                snap.Once("Shouting_Angrily", 0f, 1f, 2.2f);
            });
            s.Say("KODA", KodaCol, "Looking for the prophecy kid? Here he is!", () =>
            {
                snap.Play("Combat_Stance");
                koda.Play("Show_Both_Arm_Muscles");
                koda.WalkTo(0.6f, 3f, "Excited_Walk_M");
            });

            // the net drops over him from above and closes: a rope sack that wraps him whole, curled up inside
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
            });
            s.Say("KODA", KodaCol, "...wait, why the net?!");
            s.Say("SGT. SNAP", SnapCol, "The wolf comes with us. The General knows what to do with prophecy kids.",
                () => snap.Once("Shouting_Angrily", 0.15f, 0.85f, 1.8f));

            // two raiders take him between them on a bamboo pole, the sack hanging from it, and run; Raja runs after them
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
            bool chasing = false;
            s.Wait(1.8f, u => { if (u > 0.35f && !chasing) { chasing = true; s.RajaRunTo(-1.8f, 5.5f); } }, () =>
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
                r1.WalkTo(24f, 6.5f);
                r2.WalkTo(25.7f, 6.5f);
                snap.WalkTo(27.5f, 7f);
                s.Camera(0.5f, 1.6f, 0.9f, 2f);
            });
            s.Say("RAJA", RajaCol, "KODA!", () => s.Camera(-1.6f, 1.4f, 0.62f, 3f));
            // drawn: Koda calling out from the net, Raja's eyes, the fire bursting out of him
            s.Comic(FireInsert);

            // his fire wakes up: it gathers, then bursts out of him
            s.Wait(1.0f, u =>
            {
                s.rajaGlow = u * 2.5f;
                g.shake = Mathf.Max(g.shake, u * 0.2f);
                if (Random.value < 0.6f)
                    CbFx.Spark(new Vector3(hero.x + Random.Range(-0.6f, 0.6f), Random.Range(0.1f, 1.6f), -0.4f), CbFx.Fire, 1, 2, 0.3f, 0.5f, -4);
            }, () =>
            {
                s.RajaOnce("Power_Gather", 0f, 0.8f, 1f);
                CbFx.Sfx("charge", 0.8f);
            });
            s.Wait(0.9f, u => s.rajaGlow = 2.5f * (1 - u) + 0.4f, () =>
            {
                var chest = new Vector3(hero.x, 1.1f, -0.5f);
                CbVfx.Explosion(chest, 4f, 0.35f);
                CbVfx.Ring(chest, CbFx.Fire * 0.6f, 3f, 0.45f);
                CbVfx.Ring(new Vector3(hero.x, 0.05f, 0), CbFx.Fire * 0.6f, 3.4f, 0.5f, ground: true);
                CbFx.Spark(chest, CbFx.Fire, 30, 9, 0.5f, 0.6f, 0, 6);
                g.hud.ImpactFlash(0.45f);
                g.hud.SpeedLines(0.9f, 0.8f);
                g.shake = Mathf.Max(g.shake, 0.5f);
                CbFx.Sfx("roar");
                s.RajaOnce("Fire_Palms", 0.2f, 0.5f, 0.35f);
            });
            s.Say("RAJA", RajaCol, "Fire... from me?", () => s.Raja("Idle"));
            s.Say("RAJA", RajaCol, "Hang on, Koda. I'm coming!", () =>
            {
                s.Raja("Combat_Stance");
                s.Camera(hero.x + 1.6f, 1.5f, 1f, 2f);
            });

            s.Finally(() =>
            {
                hero.x = Mathf.Max(hero.x, -1.8f);
                hero.facing = 1;
            });
            s.Then(g.OpeningDone);
            return s;
        }

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
    }
}
