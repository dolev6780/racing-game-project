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

            // the net
            Transform[] rings = null;
            s.Wait(0.45f, u => { foreach (var r in rings) r.localScale = Vector3.one * 0.62f * u; }, () =>
            {
                CbFx.Sfx("swing", 0.8f);
                var rope = koda.f.M("#8a6a3a");
                rings = new Transform[3];
                for (int i = 0; i < 3; i++)
                    rings[i] = CbArt.Part(CbMesh.Torus(0.06f), rope, koda.f.root, new Vector3(0, 0.95f, 0), Vector3.zero,
                        CbArt.Rot(i * 1.05f, i * 0.6f, 0.4f + i * 0.5f));
                koda.struggle = true;
                koda.Play("Hop_with_Arms_Raised");
                r1.Once("Right_Hand_Sword_Slash", 0.1f, 0.5f, 0.4f);
            });
            s.Say("KODA", KodaCol, "...wait, why the net?!", () => koda.Play("Look_Around_Dumbfounded"));
            s.Say("SGT. SNAP", SnapCol, "The wolf comes with us. The General knows what to do with prophecy kids.",
                () => { koda.Play("Hop_with_Arms_Raised"); snap.Once("Shouting_Angrily", 0.15f, 0.85f, 1.8f); });

            // they carry him off between two raiders; Raja runs after them
            s.Wait(0.4f, null, () =>
            {
                r1.WalkTo(koda.x - 0.7f, 6f);
                r2.WalkTo(koda.x + 0.7f, 6f);
            });
            bool chasing = false;
            s.Wait(1.8f, u => { if (u > 0.35f && !chasing) { chasing = true; s.RajaRunTo(-1.8f, 5.5f); } }, () =>
            {
                koda.y = 1.15f;
                s.Always = () => { koda.x = (r1.x + r2.x) * 0.5f; koda.facing = 1; };
                r1.WalkTo(24f, 6.5f);
                r2.WalkTo(25.4f, 6.5f);
                snap.WalkTo(27f, 7f);
                s.Camera(0.5f, 1.6f, 0.9f, 2f);
            });
            s.Say("RAJA", RajaCol, "KODA!", () => s.Camera(-1.6f, 1.4f, 0.62f, 3f));

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
            return s;
        }
    }
}
