using System.Collections.Generic;
using UnityEngine;

namespace Cloudborne
{
    /// <summary>
    /// Stage 1-5's way in, through the burning jungle (the CI's pictures: 60 m of plain path with a few crates and no
    /// fire to be seen near Raja, in a stage about the jungle going up in flames).
    /// <list type="bullet">
    /// <item>Fire on the path: low walls of flame across it, to jump. Touching one burns, and throws him back.</item>
    /// <item>The fire run (CbGame, from FireRunFrom to FireRunTo): the jungle behind him goes up and a wall of fire comes
    /// on after him, a little slower than he runs. Burning branches crash down ahead of him (a shake and a shower of
    /// sparks first), and lie burning across the path, to jump; a burning bough hangs low, to slide under. At the
    /// temple's stone threshold the fire can't follow.</item>
    /// </list>
    /// </summary>
    public partial class CbLevel
    {
        /// <summary>The fire run: where it starts (the fire rising behind him) and the temple threshold where it ends.</summary>
        public const float FireRunFrom = 50f, FireRunTo = 88f;
        /// <summary>The low walls of fire across the path (to jump).</summary>
        public static readonly float[] EmberPatches = { 12.5f, 29f, 57f, 71.5f };
        /// <summary>Where the burning branches come down in the fire run, and the low burning bough (to slide under).</summary>
        public static readonly float[] EmberBranches = { 63f, 79f };
        public const float EmberBoughX = 67f;
        // the low fires: how tall (he jumps them), how far either side of their middle they burn
        const float PatchTall = 0.75f, PatchHalf = 0.5f, PatchDamage = 8f;
        // the fire behind him: how far back it rises, how fast it comes on (he runs 7 m/s), how hard it burns
        const float ChaseFireBack = 11f, ChaseFireSpeed = 5.3f, ChaseFireDamage = 12f;
        // a branch: how near he is when it starts to go, how long it shakes, how long it falls, how hard it hits
        const float BranchWarn = 7.5f, BranchShake = 0.75f, BranchFall = 0.4f, BranchDamage = 14f, BurnBranchHigh = 5.2f;

        class EmberPatch { public float x, tall; public Transform t; public List<Transform> tongues = new List<Transform>(); public List<float> heights = new List<float>(); }
        class EmberBranch { public float x, t = -1f; public bool down; public Transform root, log; public EmberPatch fire; }

        readonly List<EmberPatch> emberPatches = new List<EmberPatch>();
        readonly List<EmberBranch> emberBranches = new List<EmberBranch>();
        EmberPatch chaseFire;
        float chaseX;
        bool chasing;

        /// <summary>The fire behind him in the fire run is coming on, and where its front is.</summary>
        public bool FireChasing => chasing;
        public float FireChaseX => chaseX;
        /// <summary>Test hook: how many of the burning branches have come down.</summary>
        public int BranchesDown { get { int n = 0; foreach (var b in emberBranches) if (b.down) n++; return n; } }

        void BuildEmberWay()
        {
            foreach (var x in EmberPatches) emberPatches.Add(LowFire(x, PatchTall, "pathFire"));
            // the wall of fire of the run, as tall as the arena's (he can't jump it)
            chaseFire = LowFire(FireRunFrom, FireTall * 1.25f, "chaseFire", 16);
            chaseFire.t.gameObject.SetActive(false);
            emberPatches.Remove(chaseFire);
            // the burning branches, up in the canopy over the path until he comes near
            foreach (var x in EmberBranches)
            {
                var b = new EmberBranch { x = x };
                b.root = CbArt.Joint(root, x, BurnBranchHigh, 0.1f, "burningBranch");
                b.log = CbArt.Joint(b.root, 0, 0, 0, "log");
                b.log.localRotation = Quaternion.Euler(0, 90, 0);
                if (CbArt.Prop("Cloudborne/Jungle/Log/Log", b.log, CbArt.PropOutline) is Transform m)
                {
                    m.localScale = new Vector3(1.2f, 0.7f, 1.5f);
                    // charred
                    foreach (var r in m.GetComponentsInChildren<Renderer>()) r.material.color = new Color(0.38f, 0.24f, 0.18f);
                }
                else CbArt.Cylinder(M("#4a3020"), b.root, Vector3.zero, 0.3f, 3.4f, Quaternion.Euler(90, 0, 0));
                b.fire = LowFire(x, PatchTall, "branchFire");
                b.fire.t.gameObject.SetActive(false);
                emberPatches.Remove(b.fire);
                emberBranches.Add(b);
            }
            // the low burning bough, to slide under (a few tongues of flame along its top)
            AddOverhang(EmberBoughX, 0.7f, 0.95f, 1.65f, false);
            var top = LowFire(EmberBoughX, 0.55f, "boughFire", 8);
            top.t.localPosition = new Vector3(EmberBoughX, 1.6f, 0);
            emberPatches.Remove(top);
            boughFire = top;
            AddLantern(FireRunFrom - 3f);
            AddLantern(FireRunTo + 2.5f);
            ResetEmberWay();
        }

        EmberPatch boughFire;

        /// <summary>A low wall of fire across the path at x: tongues of flame from the back to the front.</summary>
        EmberPatch LowFire(float x, float tall, string name, int count = 10)
        {
            var p = new EmberPatch { x = x, tall = tall };
            p.t = CbArt.Joint(root, x, 0, 0, name);
            var tex = (Texture)CbVfx.Painted("Fire") ?? FlameTexture();
            bool painted = CbVfx.Painted("Fire") != null;
            var outer = CbArt.Glow(painted ? new Color(1f, 0.85f, 0.75f, 1f) : new Color(1f, 0.42f, 0.1f, 1f), tex);
            var inner = CbArt.Glow(painted ? new Color(1f, 1f, 0.9f, 0.9f) : new Color(1f, 0.8f, 0.35f, 1f), tex);
            for (int k = 0; k < count; k++)
            {
                bool core = k % 3 == 2;
                float z = -1.5f + k * 3.5f / count + Random.Range(-0.1f, 0.1f);
                float h = tall * (core ? Random.Range(0.55f, 0.75f) : Random.Range(0.8f, 1.15f));
                var tongue = CbArt.Part(CbMesh.Quad, core ? inner : outer, p.t, new Vector3(Random.Range(-0.3f, 0.3f), h / 2, z - (core ? 0.05f : 0f)),
                    new Vector3(h * 0.95f, h, 1), shadows: false);
                tongue.name = "flame";
                p.tongues.Add(tongue);
                p.heights.Add(h);
            }
            // charred ground under it
            CbArt.Part(CbMesh.Quad, CbArt.NewToon(new Color(0.16f, 0.1f, 0.07f), 0f), p.t, new Vector3(0, 0.02f, 0.2f), new Vector3(1.4f, 3.8f, 1),
                CbArt.Rot(Mathf.PI / 2), shadows: false);
            emberPatches.Add(p);
            return p;
        }

        /// <summary>The fire run begins: the jungle behind him goes up.</summary>
        public void StartFireChase(float heroX)
        {
            chasing = true;
            chaseX = heroX - ChaseFireBack;
            chaseFire.t.gameObject.SetActive(true);
            chaseFire.t.localPosition = new Vector3(chaseX, 0, 0);
            var at = new Vector3(chaseX, 1f, -0.3f);
            CbFx.Spark(at, CbFx.Fire, 40, 8f, 0.5f, 0.8f, -3);
            CbVfx.Burst(at, CbFx.Fire, 4f, 0.3f);
            CbFx.Sfx("fire");
        }

        /// <summary>The fire run is over (the threshold reached, or he fell): the fire behind him dies down.</summary>
        public void StopFireChase()
        {
            if (!chasing) return;
            chasing = false;
            CbFx.Puff(new Vector3(chaseX, 1f, -0.3f), new Color(0.3f, 0.26f, 0.24f), 12, 2.5f, 1.4f, 0.8f);
            chaseFire.t.gameObject.SetActive(false);
        }

        void ResetEmberWay()
        {
            chasing = false;
            if (chaseFire != null) chaseFire.t.gameObject.SetActive(false);
            foreach (var b in emberBranches)
            {
                b.t = -1f;
                b.down = false;
                b.root.localPosition = new Vector3(b.x, BurnBranchHigh, 0.1f);
                b.root.localRotation = Quaternion.identity;
                b.fire.t.gameObject.SetActive(false);
            }
        }

        static void Flicker(EmberPatch p, float time)
        {
            for (int i = 0; i < p.tongues.Count; i++)
            {
                var t = p.tongues[i];
                float h0 = p.heights[i];
                float s = 1f + Mathf.Sin(time * (8f + i % 5) + i * 1.7f) * 0.16f + Mathf.Sin(time * 13f + i) * 0.06f;
                t.localScale = new Vector3(h0 * 0.95f * (1.6f - s * 0.6f), h0 * s, 1);
                var lp = t.localPosition;
                lp.y = h0 * s / 2;
                t.localPosition = lp;
                t.localRotation = Quaternion.Euler(0, 0, Mathf.Sin(time * 3.1f + i * 0.9f) * 6f);
            }
        }

        /// <summary>Burns him if he is in a low fire at x (not over it); thrown back the way he came.</summary>
        static void Burn(CbHero hero, EmberPatch p)
        {
            if (Mathf.Abs(hero.x - p.x) < PatchHalf && hero.y < p.tall * 0.8f)
                hero.TakeHit(PatchDamage, hero.vx >= 0 ? -1 : 1, unblockable: true, knock: 6f);
        }

        void UpdateEmberWay(float dt, CbHero hero)
        {
            float time = g.clock;
            foreach (var p in emberPatches)
            {
                if (Mathf.Abs(p.x - hero.x) > 18f) continue;
                Flicker(p, time);
                if (Random.value < 0.25f) CbFx.Spark(new Vector3(p.x + Random.Range(-0.3f, 0.3f), p.tall * 0.8f, Random.Range(-1.2f, 1.6f)), CbFx.Fire, 1, 1.2f, 0.22f, 0.45f, -4);
                Burn(hero, p);
            }
            if (boughFire != null && Mathf.Abs(boughFire.x - hero.x) < 18f) Flicker(boughFire, time);

            // the burning branches: a shake and sparks as he comes near, then down across the path
            foreach (var b in emberBranches)
            {
                if (b.down)
                {
                    if (Mathf.Abs(b.x - hero.x) < 18f) Flicker(b.fire, time);
                    Burn(hero, b.fire);
                    continue;
                }
                if (b.t < 0 && chasing && hero.x > b.x - BranchWarn && hero.x < b.x) b.t = 0;
                if (b.t < 0) continue;
                b.t += dt;
                if (b.t < BranchShake)
                {
                    b.root.localRotation = Quaternion.Euler(0, 0, Mathf.Sin(time * 50f) * 3f);
                    if (Random.value < 0.6f) CbFx.Spark(new Vector3(b.x + Random.Range(-1f, 1f), BurnBranchHigh - 0.3f, 0), CbFx.Fire, 2, 1.5f, 0.3f, 0.8f, 6);
                    continue;
                }
                float k = Mathf.Clamp01((b.t - BranchShake) / BranchFall);
                b.root.localPosition = new Vector3(b.x, Mathf.Lerp(BurnBranchHigh, 0.32f, k * k), 0.1f);
                b.root.localRotation = Quaternion.Euler(0, 0, (1 - k) * 12f);
                if (k >= 1f)
                {
                    b.down = true;
                    b.fire.t.gameObject.SetActive(true);
                    g.shake = Mathf.Max(g.shake, 0.35f);
                    CbFx.Spark(new Vector3(b.x, 0.4f, -0.2f), CbFx.Fire, 24, 6f, 0.4f, 0.7f, 4);
                    CbFx.Puff(new Vector3(b.x, 0.5f, -0.2f), new Color(0.35f, 0.3f, 0.28f), 8, 2f, 1f, 0.6f);
                    CbFx.Sfx("heavy", 0.7f);
                    if (Mathf.Abs(hero.x - b.x) < 1.1f && hero.y < 1.4f) hero.TakeHit(BranchDamage, hero.x >= b.x ? 1 : -1, unblockable: true, knock: 7f);
                }
            }

            // the wall of fire behind him
            if (chasing)
            {
                chaseX += ChaseFireSpeed * dt;
                // (never far behind him: it is the jungle burning down all along)
                chaseX = Mathf.Max(chaseX, hero.x - ChaseFireBack - 2f);
                chaseFire.t.localPosition = new Vector3(chaseX, 0, 0);
                chaseFire.x = chaseX;
                Flicker(chaseFire, time);
                if (Random.value < 0.9f)
                    CbFx.Spark(new Vector3(chaseX + Random.Range(-0.4f, 0.4f), Random.Range(0.3f, 2f), Random.Range(-1.2f, 2f)), CbFx.Fire, 1, 2f, 0.3f, 0.6f, -4);
                if (hero.x < chaseX + 0.5f && hero.state != CbHero.State.Dead)
                    hero.TakeHit(ChaseFireDamage, 1, unblockable: true, knock: 9f);
            }
        }
    }
}
