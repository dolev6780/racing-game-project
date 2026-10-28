using System.Collections.Generic;
using UnityEngine;

namespace Cloudborne
{
    /// <summary>
    /// Procedural meshes for the placeholder art: Unity's primitives (sphere, capsule, cylinder, cube)
    /// plus lathed shapes (cones, tapered cylinders, domes), a torus, faceted blobs and a flat leaf.
    /// Every mesh is built once and shared.
    /// </summary>
    public static class CbMesh
    {
        static readonly Dictionary<string, Mesh> cache = new Dictionary<string, Mesh>();

        public static Mesh Sphere => Primitive(PrimitiveType.Sphere);     // diameter 1
        public static Mesh Capsule => Primitive(PrimitiveType.Capsule);   // height 2, diameter 1
        public static Mesh Cylinder => Primitive(PrimitiveType.Cylinder); // height 2, diameter 1
        public static Mesh Cube => Primitive(PrimitiveType.Cube);
        public static Mesh Quad => Primitive(PrimitiveType.Quad);

        static Mesh Primitive(PrimitiveType type)
        {
            string key = "prim" + type;
            if (cache.TryGetValue(key, out var m) && m != null) return m;
            var go = GameObject.CreatePrimitive(type);
            m = go.GetComponent<MeshFilter>().sharedMesh;
            go.SetActive(false);
            Object.Destroy(go);
            return cache[key] = m;
        }

        /// <summary>Cone with its base at y=0 and apex at y=1, radius 1.</summary>
        public static Mesh Cone(int segments = 12)
        {
            return Get("cone" + segments, () => Lathe(segments, new[]
            {
                Strip(new Vector2(0, 0), new Vector2(1, 0), down: true),
                new[] { new Vector2(1, 0), new Vector2(0, 1) },
            }));
        }

        /// <summary>Tapered cylinder, y from 0 to 1, bottom radius 1, top radius <paramref name="top"/>.</summary>
        public static Mesh Frustum(float top, int segments = 12, bool caps = true)
        {
            return Get($"frustum{top:0.00}_{segments}_{caps}", () => Lathe(segments, caps
                ? new[]
                {
                    Strip(new Vector2(0, 0), new Vector2(1, 0), down: true),
                    new[] { new Vector2(1, 0), new Vector2(top, 1) },
                    Strip(new Vector2(top, 1), new Vector2(0, 1), down: false),
                }
                : new[] { new[] { new Vector2(1, 0), new Vector2(top, 1) } }));
        }

        /// <summary>Cylinder of radius 1 and height 1 centred on the origin (readable, so it can be merged).</summary>
        public static Mesh Tube(int segments = 12)
        {
            return Get("tube" + segments, () => Lathe(segments, new[]
            {
                Strip(new Vector2(0, -0.5f), new Vector2(1, -0.5f), down: true),
                new[] { new Vector2(1, -0.5f), new Vector2(1, 0.5f) },
                Strip(new Vector2(1, 0.5f), new Vector2(0, 0.5f), down: false),
            }));
        }

        /// <summary>Unit cube centred on the origin (readable, so it can be merged).</summary>
        public static Mesh Box()
        {
            return Get("box", () =>
            {
                var v = new List<Vector3>(); var n = new List<Vector3>(); var uv = new List<Vector2>(); var tri = new List<int>();
                Vector3[] dirs = { Vector3.right, Vector3.left, Vector3.up, Vector3.down, Vector3.forward, Vector3.back };
                foreach (var d in dirs)
                {
                    var u = Mathf.Abs(d.y) > 0.5f ? Vector3.right : Vector3.up;
                    var w = Vector3.Cross(d, u);
                    int s = v.Count;
                    for (int i = 0; i < 4; i++)
                    {
                        float a = i == 0 || i == 3 ? -0.5f : 0.5f, b = i < 2 ? -0.5f : 0.5f;
                        v.Add(d * 0.5f + u * a + w * b); n.Add(d); uv.Add(new Vector2(a + 0.5f, b + 0.5f));
                    }
                    // keep the winding facing along d
                    if (Vector3.Dot(Vector3.Cross(v[s + 1] - v[s], v[s + 2] - v[s]), d) > 0) { tri.Add(s); tri.Add(s + 1); tri.Add(s + 2); tri.Add(s); tri.Add(s + 2); tri.Add(s + 3); }
                    else { tri.Add(s); tri.Add(s + 2); tri.Add(s + 1); tri.Add(s); tri.Add(s + 3); tri.Add(s + 2); }
                }
                return Build(v, n, uv, tri);
            });
        }

        /// <summary>Top part of a unit sphere (radius 1, centre at the origin) down to <paramref name="fraction"/> of the way to the bottom pole.</summary>
        public static Mesh Dome(float fraction = 0.5f, int segments = 16)
        {
            return Get($"dome{fraction:0.00}_{segments}", () =>
            {
                int rings = Mathf.Max(3, Mathf.RoundToInt(segments * fraction));
                var pts = new Vector2[rings + 1];
                for (int i = 0; i <= rings; i++)
                {
                    float a = Mathf.PI * fraction * (1f - i / (float)rings);   // from the cut edge up to the pole
                    pts[i] = new Vector2(Mathf.Sin(a), Mathf.Cos(a));
                }
                return Lathe(segments, new[] { pts }, sphere: true);
            });
        }

        /// <summary>Torus in the XZ plane: ring radius 1, tube radius <paramref name="tube"/>.</summary>
        public static Mesh Torus(float tube, int ringSegs = 24, int tubeSegs = 8)
        {
            return Get($"torus{tube:0.000}", () =>
            {
                var v = new List<Vector3>(); var n = new List<Vector3>(); var uv = new List<Vector2>(); var tri = new List<int>();
                for (int i = 0; i <= ringSegs; i++)
                {
                    float a = i / (float)ringSegs * Mathf.PI * 2f;
                    var c = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
                    for (int j = 0; j <= tubeSegs; j++)
                    {
                        float b = j / (float)tubeSegs * Mathf.PI * 2f;
                        var dir = c * Mathf.Cos(b) + Vector3.up * Mathf.Sin(b);
                        v.Add(c + dir * tube); n.Add(dir); uv.Add(new Vector2(i / (float)ringSegs, j / (float)tubeSegs));
                    }
                }
                int w = tubeSegs + 1;
                for (int i = 0; i < ringSegs; i++)
                    for (int j = 0; j < tubeSegs; j++)
                    {
                        int a = i * w + j, b = (i + 1) * w + j;
                        tri.Add(a); tri.Add(a + 1); tri.Add(b);
                        tri.Add(b); tri.Add(a + 1); tri.Add(b + 1);
                    }
                return Build(v, n, uv, tri);
            });
        }

        /// <summary>Low-poly faceted ball (radius 1), used for tree canopies and rocks.</summary>
        public static Mesh Blob(int lon = 8, int lat = 5)
        {
            return Get($"blob{lon}_{lat}", () =>
            {
                var pos = new Vector3[(lon + 1) * (lat + 1)];
                for (int y = 0; y <= lat; y++)
                    for (int x = 0; x <= lon; x++)
                    {
                        float a = x / (float)lon * Mathf.PI * 2f, b = y / (float)lat * Mathf.PI;
                        pos[y * (lon + 1) + x] = new Vector3(Mathf.Sin(b) * Mathf.Cos(a), Mathf.Cos(b), Mathf.Sin(b) * Mathf.Sin(a));
                    }
                var tris = new List<int>();
                for (int y = 0; y < lat; y++)
                    for (int x = 0; x < lon; x++)
                    {
                        int a = y * (lon + 1) + x, b = a + lon + 1;
                        if (y > 0) { tris.Add(a); tris.Add(a + 1); tris.Add(b); }
                        if (y < lat - 1) { tris.Add(b); tris.Add(a + 1); tris.Add(b + 1); }
                    }
                return Faceted(pos, tris);
            });
        }

        /// <summary>Flat leaf in the XY plane, base at the origin, tip at y=3, about 1.7 wide. Visible from both sides.</summary>
        public static Mesh Leaf()
        {
            return Get("leaf", () =>
            {
                var outline = new List<Vector2>();
                const int steps = 10;
                for (int i = 0; i <= steps; i++) outline.Add(Bezier(new Vector2(0, 0), new Vector2(0.9f, 0.5f), new Vector2(0.85f, 2.2f), new Vector2(0, 3), i / (float)steps));
                for (int i = 1; i < steps; i++) outline.Add(Bezier(new Vector2(0, 3), new Vector2(-0.85f, 2.2f), new Vector2(-0.9f, 0.5f), new Vector2(0, 0), i / (float)steps));
                var v = new List<Vector3>(); var n = new List<Vector3>(); var uv = new List<Vector2>(); var tri = new List<int>();
                for (int side = 0; side < 2; side++)
                {
                    int start = v.Count;
                    v.Add(new Vector3(0, 1.4f, 0)); n.Add(side == 0 ? Vector3.back : Vector3.forward); uv.Add(new Vector2(0.5f, 0.5f));
                    foreach (var p in outline) { v.Add(p); n.Add(side == 0 ? Vector3.back : Vector3.forward); uv.Add(new Vector2(p.x * 0.5f + 0.5f, p.y / 3f)); }
                    for (int i = 0; i < outline.Count; i++)
                    {
                        int a = start + 1 + i, b = start + 1 + (i + 1) % outline.Count;
                        if (side == 0) { tri.Add(start); tri.Add(b); tri.Add(a); }
                        else { tri.Add(start); tri.Add(a); tri.Add(b); }
                    }
                }
                return Build(v, n, uv, tri);
            });
        }

        // ---------- builders ----------

        static Mesh Get(string key, System.Func<Mesh> make)
        {
            if (cache.TryGetValue(key, out var m) && m != null) return m;
            m = make();
            m.name = key;
            return cache[key] = m;
        }

        static Vector2[] Strip(Vector2 a, Vector2 b, bool down) => new[] { a, b, down ? new Vector2(0, -1) : new Vector2(0, 1) };

        /// <summary>
        /// Revolves profiles (x = radius, y = height) around the Y axis. Each profile is its own smooth strip, so
        /// edges between strips stay sharp. A 3-point strip whose last point is (0,±1) is a flat cap with that normal.
        /// </summary>
        static Mesh Lathe(int segments, Vector2[][] strips, bool sphere = false)
        {
            var v = new List<Vector3>(); var n = new List<Vector3>(); var uv = new List<Vector2>(); var tri = new List<int>();
            foreach (var raw in strips)
            {
                Vector2[] pts; Vector2[] normals;
                if (raw.Length == 3 && raw[2].x == 0 && Mathf.Abs(raw[2].y) == 1)
                {
                    // flat cap: a downward cap runs centre -> rim, an upward one rim -> centre, so both face outwards
                    pts = new[] { raw[0], raw[1] };
                    normals = new[] { raw[2], raw[2] };
                }
                else
                {
                    pts = raw;
                    normals = new Vector2[pts.Length];
                    for (int i = 0; i < pts.Length; i++)
                    {
                        if (sphere) { normals[i] = pts[i].normalized; continue; }
                        var t = pts[Mathf.Min(i + 1, pts.Length - 1)] - pts[Mathf.Max(i - 1, 0)];
                        normals[i] = new Vector2(t.y, -t.x).normalized;
                    }
                }
                int start = v.Count;
                for (int s = 0; s <= segments; s++)
                {
                    float a = s / (float)segments * Mathf.PI * 2f;
                    float c = Mathf.Cos(a), sn = Mathf.Sin(a);
                    for (int i = 0; i < pts.Length; i++)
                    {
                        v.Add(new Vector3(pts[i].x * c, pts[i].y, pts[i].x * sn));
                        n.Add(new Vector3(normals[i].x * c, normals[i].y, normals[i].x * sn).normalized);
                        uv.Add(new Vector2(s / (float)segments, pts.Length > 1 ? i / (float)(pts.Length - 1) : 0));
                    }
                }
                int w = pts.Length;
                for (int s = 0; s < segments; s++)
                    for (int i = 0; i < w - 1; i++)
                    {
                        int a = start + s * w + i, b = start + (s + 1) * w + i;
                        // Cross(edge up the profile, edge around) points outwards, so this order faces outwards in Unity
                        tri.Add(a); tri.Add(a + 1); tri.Add(b);
                        tri.Add(b); tri.Add(a + 1); tri.Add(b + 1);
                    }
            }
            return Build(v, n, uv, tri);
        }

        static Mesh Faceted(Vector3[] pos, List<int> tris)
        {
            var v = new List<Vector3>(); var n = new List<Vector3>(); var uv = new List<Vector2>(); var tri = new List<int>();
            for (int i = 0; i < tris.Count; i += 3)
            {
                Vector3 a = pos[tris[i]], b = pos[tris[i + 1]], c = pos[tris[i + 2]];
                var nn = Vector3.Cross(b - a, c - a).normalized;
                // In Unity a triangle faces where Cross(b - a, c - a) points; flip it to face outwards.
                if (Vector3.Dot(nn, a + b + c) < 0) { (b, c) = (c, b); nn = -nn; }
                foreach (var p in new[] { a, b, c }) { tri.Add(v.Count); v.Add(p); n.Add(nn); uv.Add(new Vector2(p.x * 0.5f + 0.5f, p.y * 0.5f + 0.5f)); }
            }
            return Build(v, n, uv, tri);
        }

        static Mesh Build(List<Vector3> v, List<Vector3> n, List<Vector2> uv, List<int> tri)
        {
            var m = new Mesh();
            if (v.Count > 65000) m.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            m.SetVertices(v); m.SetNormals(n); m.SetUVs(0, uv); m.SetTriangles(tri, 0);
            m.RecalculateBounds();
            return m;
        }

        static Vector2 Bezier(Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3, float t)
        {
            float u = 1 - t;
            return u * u * u * p0 + 3 * u * u * t * p1 + 3 * u * t * t * p2 + t * t * t * p3;
        }
    }
}
