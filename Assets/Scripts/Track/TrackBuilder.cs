using System.Collections.Generic;
using UnityEngine;
using RacingGame.Core;

namespace RacingGame.Track
{
    /// <summary>
    /// Builds a complete race track at runtime from a list of control points:
    /// road surface, walls with colliders, start line, scenery and pickups.
    /// Swap the control points (or add more layouts) to create new tracks.
    /// </summary>
    public static class TrackBuilder
    {
        public const float RoadWidth = 14f;
        const float SampleSpacing = 3f;
        const float WallHeight = 1.4f;
        const float WallThickness = 0.8f;

        // Alien-planet circuit (x, z). Start line is near the first point, on the long straight.
        static readonly Vector2[] DefaultLayout =
        {
            new Vector2(0, -20), new Vector2(0, 40), new Vector2(10, 80), new Vector2(40, 100),
            new Vector2(80, 95), new Vector2(100, 70), new Vector2(95, 40), new Vector2(70, 20),
            new Vector2(60, -10), new Vector2(80, -40), new Vector2(110, -60), new Vector2(115, -100),
            new Vector2(90, -125), new Vector2(40, -125), new Vector2(8, -112), new Vector2(0, -80),
        };

        static readonly Color RoadColor = new Color(0.27f, 0.27f, 0.31f);
        static readonly Color GroundColor = new Color(0.36f, 0.66f, 0.4f);
        static readonly Color WallColorA = new Color(0.88f, 0.9f, 0.96f);
        static readonly Color WallColorB = new Color(0.55f, 0.66f, 0.88f);
        static readonly Color CurbRed = new Color(0.9f, 0.27f, 0.22f);
        static readonly Color CurbWhite = new Color(0.97f, 0.97f, 0.97f);
        static readonly Color EdgeYellow = new Color(1f, 0.8f, 0.2f);
        static readonly Color RockColor = new Color(0.5f, 0.5f, 0.55f);

        // Kenney model tiles are 10m; scale them to our wider road.
        const float DecorationScale = 1.3f;
        const float FinishArchScale = 1.4f;

        public static TrackPath Build(Transform parent)
        {
            var root = new GameObject("Track");
            root.transform.SetParent(parent, false);

            var samples = SampleClosedCatmullRom(DefaultLayout, SampleSpacing);
            var path = root.AddComponent<TrackPath>();
            path.Initialize(samples, RoadWidth);

            BuildGround(root.transform, samples);
            BuildRoad(root.transform, path);
            BuildCurbs(root.transform, path);
            BuildWalls(root.transform, path, -1);
            BuildWalls(root.transform, path, +1);
            BuildStartLine(root.transform, path);
            BuildScenery(root.transform, path);
            BuildPickups(root.transform, path);
            return path;
        }

        // ---------- Path sampling ----------

        static List<Vector3> SampleClosedCatmullRom(Vector2[] control, float spacing)
        {
            // 1) dense sampling
            var dense = new List<Vector3>();
            int n = control.Length;
            const int stepsPerSegment = 30;
            for (int i = 0; i < n; i++)
            {
                Vector2 p0 = control[(i - 1 + n) % n];
                Vector2 p1 = control[i];
                Vector2 p2 = control[(i + 1) % n];
                Vector2 p3 = control[(i + 2) % n];
                for (int s = 0; s < stepsPerSegment; s++)
                {
                    float t = s / (float)stepsPerSegment;
                    Vector2 p = CatmullRom(p0, p1, p2, p3, t);
                    dense.Add(new Vector3(p.x, 0f, p.y));
                }
            }

            // 2) resample evenly by arc length
            var result = new List<Vector3> { dense[0] };
            float carried = 0f;
            for (int i = 0; i < dense.Count; i++)
            {
                Vector3 a = dense[i];
                Vector3 b = dense[(i + 1) % dense.Count];
                float seg = Vector3.Distance(a, b);
                float pos = spacing - carried;
                while (pos <= seg)
                {
                    result.Add(Vector3.Lerp(a, b, pos / seg));
                    pos += spacing;
                }
                carried = seg - (pos - spacing);
            }
            // drop last point if it nearly overlaps the first (closed loop)
            if (Vector3.Distance(result[result.Count - 1], result[0]) < spacing * 0.5f)
                result.RemoveAt(result.Count - 1);
            return result;
        }

        static Vector2 CatmullRom(Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3, float t)
        {
            float t2 = t * t, t3 = t2 * t;
            return 0.5f * ((2f * p1) + (-p0 + p2) * t + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 +
                           (-p0 + 3f * p1 - 3f * p2 + p3) * t3);
        }

        // ---------- Geometry ----------

        static void BuildGround(Transform parent, List<Vector3> samples)
        {
            var bounds = new Bounds(samples[0], Vector3.zero);
            foreach (var p in samples) bounds.Encapsulate(p);

            var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.name = "Ground";
            ground.transform.SetParent(parent, false);
            ground.transform.position = new Vector3(bounds.center.x, -0.5f, bounds.center.z);
            ground.transform.localScale = new Vector3(bounds.size.x + 300f, 1f, bounds.size.z + 300f);
            ground.GetComponent<Renderer>().sharedMaterial = MaterialFactory.Get(GroundColor);
        }

        static void BuildRoad(Transform parent, TrackPath path)
        {
            int n = path.Count;
            var verts = new Vector3[(n + 1) * 2];
            var uvs = new Vector2[verts.Length];
            var tris = new int[n * 6];
            float half = path.RoadWidth * 0.5f;
            float v = 0f;

            for (int i = 0; i <= n; i++)
            {
                Vector3 p = path.Point(i) + Vector3.up * 0.02f;
                Vector3 right = SmoothedRight(path, i);
                verts[i * 2] = p - right * half;
                verts[i * 2 + 1] = p + right * half;
                if (i > 0) v += Vector3.Distance(path.Point(i - 1), path.Point(i)) / path.RoadWidth;
                uvs[i * 2] = new Vector2(0, v);
                uvs[i * 2 + 1] = new Vector2(1, v);
            }

            for (int i = 0; i < n; i++)
            {
                int l0 = i * 2, r0 = i * 2 + 1, l1 = i * 2 + 2, r1 = i * 2 + 3;
                int t = i * 6;
                tris[t] = l0; tris[t + 1] = l1; tris[t + 2] = r0;
                tris[t + 3] = r0; tris[t + 4] = l1; tris[t + 5] = r1;
            }

            var mesh = new Mesh { name = "Road" };
            mesh.vertices = verts;
            mesh.uv = uvs;
            mesh.triangles = tris;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            var road = new GameObject("Road");
            road.transform.SetParent(parent, false);
            road.AddComponent<MeshFilter>().sharedMesh = mesh;
            road.AddComponent<MeshRenderer>().sharedMaterial = MaterialFactory.Get(RoadColor);
        }

        /// <summary>Red/white curbs on both road edges plus a yellow edge line, like the Kenney kit.</summary>
        static void BuildCurbs(Transform parent, TrackPath path)
        {
            var red = new MeshBuilder();
            var white = new MeshBuilder();
            var yellow = new MeshBuilder();
            float half = path.RoadWidth * 0.5f;
            const float curbWidth = 1.1f, lineWidth = 0.25f;
            Vector3 up = Vector3.up * 0.035f;

            for (int i = 0; i < path.Count; i++)
            {
                Vector3 p0 = path.Point(i), p1 = path.Point(i + 1);
                Vector3 r0 = SmoothedRight(path, i), r1 = SmoothedRight(path, i + 1);
                var curb = i % 2 == 0 ? red : white;
                foreach (float side in new[] { -1f, 1f })
                {
                    float outer = half * side, inner = (half - curbWidth) * side;
                    curb.AddQuad(p0 + r0 * inner + up, p1 + r1 * inner + up, p1 + r1 * outer + up, p0 + r0 * outer + up);
                    float li = (half - curbWidth - 0.35f) * side, lo = (half - curbWidth - 0.35f - lineWidth) * side;
                    yellow.AddQuad(p0 + r0 * lo + up, p1 + r1 * lo + up, p1 + r1 * li + up, p0 + r0 * li + up);
                }
            }
            red.CreateObject("CurbsRed", parent, CurbRed);
            white.CreateObject("CurbsWhite", parent, CurbWhite);
            yellow.CreateObject("EdgeLines", parent, EdgeYellow);
        }

        /// <summary>Average of neighbouring segment directions, to avoid gaps on corners.</summary>
        static Vector3 SmoothedRight(TrackPath path, int i)
        {
            Vector3 dir = (path.Direction(i - 1) + path.Direction(i)).normalized;
            return Vector3.Cross(Vector3.up, dir).normalized;
        }

        static void BuildWalls(Transform parent, TrackPath path, int side)
        {
            var root = new GameObject(side < 0 ? "Wall_Left" : "Wall_Right");
            root.transform.SetParent(parent, false);

            float offset = path.RoadWidth * 0.5f + WallThickness * 0.5f + 0.4f;
            int n = path.Count;

            // Visual: alternating red/white blocks combined into two meshes.
            var builderA = new MeshBuilder();
            var builderB = new MeshBuilder();

            for (int i = 0; i < n; i++)
            {
                Vector3 a = path.Point(i) + SmoothedRight(path, i) * offset * side;
                Vector3 b = path.Point(i + 1) + SmoothedRight(path, i + 1) * offset * side;
                var target = (i / 2) % 2 == 0 ? builderA : builderB;
                target.AddBox(a, b, WallThickness, WallHeight);

                // Colliders: one box per segment (static, no renderer).
                var col = new GameObject("WallCollider");
                col.transform.SetParent(root.transform, false);
                Vector3 mid = (a + b) * 0.5f + Vector3.up * (WallHeight * 0.5f);
                col.transform.position = mid;
                Vector3 fwd = b - a;
                if (fwd.sqrMagnitude > 0.0001f) col.transform.rotation = Quaternion.LookRotation(fwd.normalized);
                var box = col.AddComponent<BoxCollider>();
                box.size = new Vector3(WallThickness, WallHeight, fwd.magnitude + 0.6f);
            }

            builderA.CreateObject("WallVisualA", root.transform, WallColorA);
            builderB.CreateObject("WallVisualB", root.transform, WallColorB);
        }

        static void BuildStartLine(Transform parent, TrackPath path)
        {
            var root = new GameObject("StartLine");
            root.transform.SetParent(parent, false);
            Vector3 p = path.Point(0);
            Vector3 dir = path.Direction(0);
            Vector3 right = path.Right(0);
            int tiles = 10;
            float tile = path.RoadWidth / tiles;
            for (int row = 0; row < 2; row++)
            for (int i = 0; i < tiles; i++)
            {
                Color c = (i + row) % 2 == 0 ? Color.white : Color.black;
                Vector3 pos = p + right * (-path.RoadWidth * 0.5f + tile * (i + 0.5f)) + dir * (row * tile) + Vector3.up * 0.03f;
                MaterialFactory.Visual(PrimitiveType.Cube, root.transform, pos, new Vector3(tile, 0.02f, tile), c,
                    Quaternion.LookRotation(dir));
            }

            // Finish arch (3D model), or a simple gantry as fallback.
            var arch = ModelLibrary.Instantiate("track-finish", root.transform);
            if (arch != null)
            {
                arch.transform.SetPositionAndRotation(p + dir * 2f - Vector3.up * 0.05f, Quaternion.LookRotation(dir));
                arch.transform.localScale = Vector3.one * FinishArchScale;
                return;
            }

            float h = 6f;
            float w = path.RoadWidth * 0.5f + 2f;
            var gray = new Color(0.3f, 0.3f, 0.35f);
            MaterialFactory.Visual(PrimitiveType.Cube, root.transform, p + right * -w + Vector3.up * h * 0.5f, new Vector3(0.6f, h, 0.6f), gray);
            MaterialFactory.Visual(PrimitiveType.Cube, root.transform, p + right * w + Vector3.up * h * 0.5f, new Vector3(0.6f, h, 0.6f), gray);
            MaterialFactory.Visual(PrimitiveType.Cube, root.transform, p + Vector3.up * h, new Vector3(w * 2f + 0.6f, 1.2f, 0.6f),
                new Color(1f, 0.75f, 0.1f), Quaternion.LookRotation(dir));
        }

        static void BuildScenery(Transform parent, TrackPath path)
        {
            var root = new GameObject("Scenery");
            root.transform.SetParent(parent, false);
            var rng = new System.Random(1234);

            if (BuildForest(root.transform, path, rng)) return;

            float clearance = path.RoadWidth * 0.5f + 6f;

            int placed = 0, attempts = 0;
            while (placed < 70 && attempts < 1000)
            {
                attempts++;
                int i = rng.Next(path.Count);
                float side = rng.Next(2) == 0 ? -1f : 1f;
                float dist = clearance + (float)rng.NextDouble() * 40f;
                Vector3 pos = path.Point(i) + path.Right(i) * side * dist;
                if (Vector3.Distance(path.Point(path.FindClosest(pos)), pos) < clearance) continue;

                float s = 1.5f + (float)rng.NextDouble() * 5f;
                bool crystal = rng.Next(4) == 0;
                if (crystal)
                {
                    var color = new Color(0.2f, 0.9f, 1f);
                    MaterialFactory.Visual(PrimitiveType.Cylinder, root.transform, pos + Vector3.up * s,
                        new Vector3(s * 0.3f, s, s * 0.3f), color,
                        Quaternion.Euler((float)rng.NextDouble() * 30f, (float)rng.NextDouble() * 360f, 0f));
                }
                else
                {
                    MaterialFactory.Visual(PrimitiveType.Sphere, root.transform, pos + Vector3.up * s * 0.2f,
                        new Vector3(s * 1.4f, s, s * 1.2f), RockColor,
                        Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f));
                }
                placed++;
            }
        }

        /// <summary>Fills the area around the track with Kenney forest tiles and adds pit tents.</summary>
        static bool BuildForest(Transform root, TrackPath path, System.Random rng)
        {
            float tile = 10f * DecorationScale;
            float clearance = path.RoadWidth * 0.5f + 1.5f + tile * 0.71f;   // road + wall + half tile diagonal
            const float maxDistance = 55f;

            var bounds = new Bounds(path.Point(0), Vector3.zero);
            for (int i = 0; i < path.Count; i++) bounds.Encapsulate(path.Point(i));
            bounds.Expand(maxDistance * 2f);

            // Pit tents beside the start straight.
            int placed = 0;
            for (int k = 0; k < 2; k++)
            {
                float d = -20f - k * tile;
                Vector3 pos = path.PointAtDistance(d, out Vector3 dir);
                Vector3 right = Vector3.Cross(Vector3.up, dir).normalized;
                var tents = ModelLibrary.Instantiate("decoration-tents", root);
                if (tents == null) return false;
                tents.transform.SetPositionAndRotation(pos - right * clearance + Vector3.up * 0.01f,
                    Quaternion.LookRotation(dir) * Quaternion.Euler(0f, 90f, 0f));
                tents.transform.localScale = Vector3.one * DecorationScale;
                placed++;
            }

            for (float x = bounds.min.x; x <= bounds.max.x; x += tile)
            for (float z = bounds.min.z; z <= bounds.max.z; z += tile)
            {
                var pos = new Vector3(x, 0.01f, z);
                float dist = Vector3.Distance(path.Point(path.FindClosest(pos)), pos);
                if (dist < clearance || dist > maxDistance) continue;
                if (rng.NextDouble() < 0.3) continue;   // clearings

                var forest = ModelLibrary.Instantiate("decoration-forest", root);
                if (forest == null) return placed > 0;
                forest.transform.SetPositionAndRotation(pos, Quaternion.Euler(0f, 90f * rng.Next(4), 0f));
                forest.transform.localScale = Vector3.one * DecorationScale;
                placed++;
            }
            return placed > 0;
        }

        static void BuildPickups(Transform parent, TrackPath path)
        {
            var root = new GameObject("Pickups");
            root.transform.SetParent(parent, false);
            int count = 8;
            for (int k = 0; k < count; k++)
            {
                float d = path.Length * (k + 0.5f) / count;
                Vector3 pos = path.PointAtDistance(d, out Vector3 dir);
                Vector3 right = Vector3.Cross(Vector3.up, dir).normalized;
                float lateral = ((k % 3) - 1) * path.RoadWidth * 0.28f;
                var type = k % 2 == 0 ? PickupType.Money : PickupType.Armor;
                Pickup.Create(root.transform, pos + right * lateral + Vector3.up * 1f, type);
            }
        }

        // ---------- Mesh helper ----------

        class MeshBuilder
        {
            readonly List<Vector3> verts = new List<Vector3>();
            readonly List<int> tris = new List<int>();

            /// <summary>Adds an oriented box spanning from a to b on the ground.</summary>
            public void AddBox(Vector3 a, Vector3 b, float thickness, float height)
            {
                Vector3 fwd = (b - a);
                if (fwd.sqrMagnitude < 0.0001f) return;
                Vector3 r = Vector3.Cross(Vector3.up, fwd.normalized) * thickness * 0.5f;
                Vector3 up = Vector3.up * height;

                Vector3 a0 = a - r, a1 = a + r, b0 = b - r, b1 = b + r;
                // sides
                AddQuad(a0, b0, b0 + up, a0 + up);
                AddQuad(b1, a1, a1 + up, b1 + up);
                // top
                AddQuad(a0 + up, b0 + up, b1 + up, a1 + up);
                // ends
                AddQuad(a1, a0, a0 + up, a1 + up);
                AddQuad(b0, b1, b1 + up, b0 + up);
            }

            /// <summary>Double-sided quad so winding order never hides a face.</summary>
            public void AddQuad(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3)
            {
                AddSingleQuad(p0, p1, p2, p3);
                AddSingleQuad(p3, p2, p1, p0);
            }

            void AddSingleQuad(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3)
            {
                int s = verts.Count;
                verts.Add(p0); verts.Add(p1); verts.Add(p2); verts.Add(p3);
                tris.Add(s); tris.Add(s + 1); tris.Add(s + 2);
                tris.Add(s); tris.Add(s + 2); tris.Add(s + 3);
            }

            public void CreateObject(string name, Transform parent, Color color)
            {
                var mesh = new Mesh { name = name };
                if (verts.Count > 65000) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
                mesh.SetVertices(verts);
                mesh.SetTriangles(tris, 0);
                mesh.RecalculateNormals();
                mesh.RecalculateBounds();

                var go = new GameObject(name);
                go.transform.SetParent(parent, false);
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                go.AddComponent<MeshRenderer>().sharedMaterial = MaterialFactory.Get(color);
            }
        }
    }
}
