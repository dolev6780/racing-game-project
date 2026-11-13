using System.Collections.Generic;
using UnityEngine;

namespace Cloudborne
{
    /// <summary>
    /// The jungle's small life (the jungle stood still): butterflies flutter about the meadow behind the path, a few to
    /// a patch, and scatter up and away when Raja comes by, then drift back; now and then a little flock of birds
    /// crosses the sky far behind. Painted in Canva (Resources/Cloudborne/Jungle/Critters, cut out of their white
    /// background); without a painting there are none of that kind. Scenery only: nothing in the game touches them.
    /// </summary>
    public class CbCritters
    {
        const string Dir = "Cloudborne/Jungle/Critters/";
        public const string ButterflyName = "Butterfly", BirdName = "Bird";
        // Butterflies: a patch every PatchGap metres or so, two or three in it, Span across the wings, from BackFrom to
        // BackTo behind the path: clear of the rocks (whose backs reach about 2.1) and in front of the painted hedge (at
        // 3, 1.75 tall: behind it they were hidden). From Low to High over the grass. Only those within Near metres of
        // the camera are drawn and moved.
        const float PatchGap = 11f, Span = 0.5f, BackFrom = 2.25f, BackTo = 2.65f, Low = 0.5f, High = 1.8f, Near = 26f;
        // A butterfly takes fright when he comes within Fright metres and flees FleeX away and FleeY up, then settles
        // back over Settle seconds once he has gone.
        const float Fright = 2.4f, FleeX = 2.2f, FleeY = 1.9f, Settle = 3.5f;
        // The wings: from Open degrees to Open + Beat folded, open most of the time and snapping shut, Rate beats
        // (radians a second), quicker in a fright. The body leans Lean degrees back from the camera, so it is seen a
        // little from above as it flies (leant far back, it was seen edge-on: a thin sliver).
        const float Open = 4f, Beat = 70f, Rate = 15f, Lean = 22f;
        // Birds: a flock every FlockFrom to FlockTo seconds, of two to five birds, BirdSpan across, BirdFrom to BirdTo
        // deep (in front of the far painted trees), BirdUp to BirdUp + BirdBand over the camera's aim (higher, they
        // crossed under the score), BirdSpeed m/s.
        const float FlockFrom = 14f, FlockTo = 26f, BirdSpan = 1.15f, BirdFrom = 16f, BirdTo = 24f, BirdUp = 2.2f,
            BirdBand = 2.6f, BirdSpeed = 6.5f, BirdReach = 46f;

        class Fly { public Transform body, left, right; public Vector3 home, at; public float ph, rate, fear, heading; public int away; public bool shown; }
        class Bird { public Transform t; public float x, y, z, ph, flap; public int dir; }

        readonly Transform root;
        readonly List<Fly> flies = new List<Fly>();
        readonly List<Bird> birds = new List<Bird>();
        readonly Material birdMat;
        readonly float birdAspect;
        float nextFlock = 6f, lastT;

        /// <summary>How many butterflies there are along the stage (0 without their painting).</summary>
        public int Butterflies => flies.Count;
        /// <summary>How many birds are crossing the sky now.</summary>
        public int BirdsFlying => birds.Count;
        /// <summary>Whether the birds' painting is in the project.</summary>
        public bool HasBirds => birdMat != null;

        public CbCritters(Transform parent)
        {
            root = new GameObject("Critters").transform;
            root.SetParent(parent, false);
            var rnd = new System.Random(11);
            float R(float a, float b) => a + (float)rnd.NextDouble() * (b - a);

            var wings = Resources.Load<Texture2D>(Dir + ButterflyName);
            var wingMat = CbWorld.Painted(wings);
            if (wingMat != null)
            {
                float aspect = wings.width / (float)wings.height;
                var left = WingMesh(-1, aspect);
                var right = WingMesh(1, aspect);
                for (float x = CbWorld.LevelMin + 6f; x < CbWorld.LevelMax; x += R(PatchGap * 0.7f, PatchGap * 1.3f))
                {
                    // none over the rivers or in the gorge and on the mountain (cliffs all round)
                    if (CbLevel.InWater(x, 1.5f) || (x > CbLevel.MountainFrom - 2f && x < CbLevel.MountainTo + 2f)) continue;
                    int n = rnd.Next(2, 4);
                    float z = R(BackFrom, BackTo);
                    // each patch its own colours: the painting's orange and turquoise, or tinted gold or pink
                    var tint = new[] { Color.white, new Color(1f, 0.9f, 0.45f), new Color(1f, 0.7f, 0.85f), new Color(0.75f, 0.9f, 1f) }[rnd.Next(4)];
                    var mat = tint == Color.white ? wingMat : Tinted(wingMat, tint);
                    for (int i = 0; i < n; i++)
                    {
                        var f = new Fly
                        {
                            home = new Vector3(x + R(-1.2f, 1.2f), R(Low, High), z + R(-0.4f, 0.4f)),
                            ph = R(0f, 20f),
                            rate = R(0.8f, 1.2f),
                        };
                        f.at = f.home;
                        f.body = new GameObject("butterfly").transform;
                        f.body.SetParent(root, false);
                        f.body.localPosition = f.home;
                        f.left = CbArt.Part(left, mat, f.body, Vector3.zero, Vector3.one, shadows: false);
                        f.right = CbArt.Part(right, mat, f.body, Vector3.zero, Vector3.one, shadows: false);
                        f.body.gameObject.SetActive(false);
                        flies.Add(f);
                    }
                }
            }

            var bird = Resources.Load<Texture2D>(Dir + BirdName);
            // a little hazed: they are far off
            birdMat = CbWorld.Painted(bird, 0.18f);
            if (birdMat != null) birdAspect = bird.width / (float)bird.height;
        }

        /// <summary>One wing: half the painting (side -1 the left half, 1 the right), hinged at the body, Span across both.</summary>
        static Mesh WingMesh(int side, float aspect)
        {
            float w = Span / 2, h = Span / aspect;
            float x0 = side < 0 ? -w : 0, x1 = side < 0 ? 0 : w, u0 = side < 0 ? 0 : 0.5f, u1 = side < 0 ? 0.5f : 1f;
            var m = new Mesh { name = side < 0 ? "wingL" : "wingR" };
            m.vertices = new[] { new Vector3(x0, -h / 2, 0), new Vector3(x1, -h / 2, 0), new Vector3(x1, h / 2, 0), new Vector3(x0, h / 2, 0) };
            m.uv = new[] { new Vector2(u0, 0), new Vector2(u1, 0), new Vector2(u1, 1), new Vector2(u0, 1) };
            m.triangles = new[] { 0, 2, 1, 0, 3, 2 };
            m.RecalculateNormals();
            m.RecalculateBounds();
            return m;
        }

        static Material Tinted(Material m, Color c)
        {
            var t = new Material(m);
            t.SetColor("_Color", c);
            return t;
        }

        /// <summary>Moves them along: t is the game's clock, (camX, camY) where the camera aims, (heroX, heroY) where Raja is.</summary>
        public void Update(float t, float camX, float camY, float heroX, float heroY)
        {
            float dt = Mathf.Clamp(t - lastT, 0f, 0.1f);
            lastT = t;
            foreach (var f in flies)
            {
                bool near = Mathf.Abs(f.home.x - camX) < Near;
                if (near != f.shown) { f.shown = near; f.body.gameObject.SetActive(near); }
                if (!near) { f.fear = 0; continue; }
                // a fright when he comes close, settling once he's gone
                // (measured from its home, so it keeps away while he stays)
                bool close = Mathf.Abs(heroX - f.home.x) < Fright && heroY < f.home.y + 2.5f && heroY > f.home.y - 3f;
                if (close)
                {
                    if (f.fear < 0.05f) f.away = heroX < f.home.x ? 1 : -1;
                    f.fear = Mathf.Min(1f, f.fear + dt * 2.5f);
                }
                else f.fear = Mathf.Max(0f, f.fear - dt / Settle);
                float s = f.ph, r = f.rate, k = f.fear * f.fear * (3 - 2 * f.fear);
                // a wandering, looping flutter about its home
                var wander = new Vector3(
                    Mathf.Sin(t * 0.53f * r + s) * 0.9f + Mathf.Sin(t * 1.31f * r + s * 2f) * 0.35f,
                    Mathf.Sin(t * 0.77f * r + s * 3f) * 0.3f + Mathf.Sin(t * 2.3f * r + s) * 0.08f,
                    Mathf.Sin(t * 0.41f * r + s * 1.7f) * 0.15f);
                float beat = t * Rate * r * (1 + f.fear * 0.7f) + s;
                var pos = f.home + wander + new Vector3(f.away * FleeX, FleeY, 0.3f) * k;
                // a little lift with each downbeat
                pos.y += Mathf.Sin(beat) * 0.03f;
                pos.y = Mathf.Max(pos.y, 0.15f);
                var v = (pos - f.at) / Mathf.Max(dt, 0.001f);
                f.at = pos;
                f.body.localPosition = pos;
                // tipped toward the way it flies, turning smoothly (turned all the way, it flew on its side)
                if (dt > 0)
                    f.heading = Mathf.Lerp(f.heading, Mathf.Clamp(-v.x * 25f, -35f, 35f), 1 - Mathf.Exp(-6f * dt));
                f.body.localRotation = Quaternion.Euler(0, 0, f.heading) * Quaternion.Euler(Lean, 0, 0);
                float shut = 0.5f + 0.5f * Mathf.Sin(beat);
                float a = Open + Beat * shut * shut;
                // the wings fold up over its back, toward the camera
                f.left.localRotation = Quaternion.Euler(0, -a, 0);
                f.right.localRotation = Quaternion.Euler(0, a, 0);
            }

            if (birdMat == null) return;
            // now and then a flock sets off from one side of the view to the other
            if (t > nextFlock)
            {
                nextFlock = t + Random.Range(FlockFrom, FlockTo);
                int dir = Random.value < 0.5f ? 1 : -1, n = Random.Range(2, 6);
                float y0 = camY + BirdUp + Random.Range(0f, BirdBand), z0 = Random.Range(BirdFrom, BirdTo);
                for (int i = 0; i < n; i++)
                {
                    var b = new Bird
                    {
                        dir = dir,
                        // a loose V: each one a little behind and to the side of the one before
                        x = camX - dir * (BirdReach * 0.5f + i * 1.6f + Random.Range(0f, 0.8f)),
                        y = y0 + (i % 2 == 0 ? 1 : -1) * ((i + 1) / 2) * 0.55f + Random.Range(-0.2f, 0.2f),
                        z = z0 + Random.Range(-1f, 1f),
                        ph = Random.Range(0f, 6f),
                        flap = Random.Range(9f, 12f),
                    };
                    b.t = CbArt.Part(CbMesh.Quad, birdMat, root, new Vector3(b.x, b.y, b.z), Vector3.one, shadows: false);
                    b.t.name = "bird";
                    birds.Add(b);
                }
            }
            for (int i = birds.Count - 1; i >= 0; i--)
            {
                var b = birds[i];
                b.x += b.dir * BirdSpeed * dt;
                // wings beating (the painting squashed and stretched) with a glide now and then, rising and dipping
                float cycle = Mathf.Sin(t * 0.9f + b.ph);
                float beat = cycle > 0.55f ? 0.85f : 0.62f + 0.38f * Mathf.Abs(Mathf.Sin(t * b.flap + b.ph));
                b.t.localPosition = new Vector3(b.x, b.y + Mathf.Sin(t * 1.3f + b.ph) * 0.25f, b.z);
                b.t.localScale = new Vector3(BirdSpan * b.dir, BirdSpan / birdAspect * beat, 1);
                if (Mathf.Abs(b.x - camX) > BirdReach)
                {
                    Object.Destroy(b.t.gameObject);
                    birds.RemoveAt(i);
                }
            }
        }

        // ---------- test hooks ----------

        /// <summary>The butterflies nearest x: their home, where they are now, and how frightened they are.</summary>
        public List<(Vector3 home, Vector3 at, float fear)> ButterfliesNear(float x, float within)
        {
            var list = new List<(Vector3, Vector3, float)>();
            foreach (var f in flies)
                if (Mathf.Abs(f.home.x - x) < within) list.Add((f.home, f.at, f.fear));
            return list;
        }

        /// <summary>Sends a flock off now.</summary>
        public void FlockNow() => nextFlock = 0f;
    }
}
