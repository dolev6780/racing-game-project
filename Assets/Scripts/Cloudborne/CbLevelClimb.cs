using System.Collections.Generic;
using UnityEngine;

namespace Cloudborne
{
    /// <summary>
    /// The climbing kit (the user: something reasonable to stand on and climb, tied to the jungle, for many stages -
    /// drawn in Canva, approved, built by Meshy): a ruin's pillar with a slab on top (RuinLedge), a ruin's tower with
    /// ledges up its sides (RuinWall), a platform of planks round a trunk with a rope ladder (TreePlatform). Each
    /// stands behind the path, its top the ledge he lands on; without the models the old slab and its support show.
    /// </summary>
    public partial class CbLevel
    {
        // the tower turned TowerYaw (as drawn: its ledges to either side), measured from the model: each ledge's top and
        // its span across the screen, and the top's, as parts of its height; how far back its middle stands
        const float TowerYaw = 225f, TowerAt = 4.5f, TowerTopHalf = 0.16f, TowerBack = 0.12f;
        static readonly (float top, float x0, float x1)[] TowerLedges = { (0.40f, -0.18f, 0.01f), (0.655f, -0.01f, 0.197f), (0.81f, -0.20f, 0.01f) };
        // the model's own size, as parts of its height
        const float TowerWide = 0.336f, TowerDeep = 0.341f, PillarWide = 0.49f, PlatformTop = 0.70f, PlatformWide = 0.5f;

        /// <summary>The ledge at x (top, width) built from the climbing kit; false if this stage or this ledge has none
        /// (then the old slab is built).</summary>
        bool ClimbKitLedge(Transform t, float x, float width, float top)
        {
            if (!CbStage.Jungle && !CbStage.Treetops) return false;
            float baseY = Mathf.Max(SupportAt(x, top - 0.3f), -2.5f);
            float h = top - baseY + 0.2f;
            if (h < 0.6f) return false;
            if (CbStage.Treetops) return TreePlatform(t, x, width, top, baseY);
            if (h >= TowerAt && RuinTower(t, x, width, top, baseY)) return true;
            // a pillar: as tall as it must be, about as wide as it is drawn for that, its slab a little wider than the ledge
            float wide = Mathf.Clamp(h * PillarWide, width + 0.2f, width + 0.6f);
            var holder = CbArt.Joint(t, 0, baseY - 0.2f, 0.25f, "ruinLedge");
            if (Model("RuinLedge", holder, new Vector3(wide, h, wide * 0.8f), 180f) == null) { Object.Destroy(holder.gameObject); return false; }
            // its slab where it is drawn: on his line, its top at the ledge's height, the ledge as wide as the slab
            AddMeasuredLedge(t, holder, x, wide / 2 + 0.3f, top, width);
            return true;
        }

        /// <summary>A ruin's tower up to a high ledge, three ledges up its sides (left, right, left) to climb it by.</summary>
        bool RuinTower(Transform t, float x, float width, float top, float baseY)
        {
            float H = top - baseY;
            var holder = CbArt.Joint(t, 0, baseY, TowerBack * H, "ruinTower");
            if (Model("RuinWall", holder, new Vector3(TowerWide * H, H, TowerDeep * H), TowerYaw) == null) { Object.Destroy(holder.gameObject); return false; }
            var cs = Solidify(holder);
            // its ledges on his line (the first one's depth), its top at the ledge's height
            var (a, a0, a1) = TowerLedges[0];
            Shift(holder, new Vector3(0, 0, -BestDepth(cs, x + (a0 + a1) / 2 * H, (a1 - a0) / 2 * H + 0.3f, baseY + a * H, 0.35f * H * 0.2f + 0.3f)));
            if (FindTop(cs, x, TowerTopHalf * H + 0.3f, top, 0.6f, out float tt, out float tx0, out float tx1))
                Shift(holder, new Vector3(0, top - tt, 0));
            else { tx0 = x - width / 2; tx1 = x + width / 2; }
            ledges.Add(new Ledge { x0 = tx0, x1 = tx1, top = top, t = t, home = t.localPosition });
            // the ledges up its sides, each where it is drawn
            foreach (var (lt, l0, l1) in TowerLedges)
            {
                float near = baseY + lt * H, cx = x + (l0 + l1) / 2 * H, half = (l1 - l0) / 2 * H + 0.4f;
                if (!FindTop(cs, cx, half, near, 0.5f, out float lt2, out float lx0, out float lx1)) { lt2 = near; lx0 = x + l0 * H; lx1 = x + l1 * H; }
                var side = CbArt.Joint(root, x, 0, 0, "towerLedge");
                ledges.Add(new Ledge { x0 = lx0, x1 = lx1, top = lt2, t = side, home = side.localPosition });
            }
            return true;
        }

        /// <summary>A platform of planks round a trunk, its top the ledge; the trunk behind the path.</summary>
        bool TreePlatform(Transform t, float x, float width, float top, float baseY)
        {
            float wide = width + 0.3f, h = (top - baseY) / PlatformTop;
            var holder = CbArt.Joint(t, 0, baseY, wide * 0.28f, "treePlatform");
            if (Model("TreePlatform", holder, new Vector3(wide, h, wide), 180f) == null) { Object.Destroy(holder.gameObject); return false; }
            AddMeasuredLedge(t, holder, x, wide / 2 + 0.3f, top, width);
            return true;
        }

        /// <summary>
        /// A ledge on a model's flat top: the model moved so its top lies on his line at `top`, the ledge as wide as the
        /// top is drawn there (as the stage laid it out, `width`, if the rays find none).
        /// </summary>
        void AddMeasuredLedge(Transform t, Transform holder, float x, float half, float top, float width)
        {
            var cs = Solidify(holder);
            float x0 = x - width / 2, x1 = x + width / 2;
            if (FindTop(cs, x, half, top, 0.8f, out float found, out _, out _))
            {
                Shift(holder, new Vector3(0, top - found, 0));
                FindTop(cs, x, half, top, 0.3f, out _, out x0, out x1);
            }
            else
            {
                Shift(holder, new Vector3(0, 0, -BestDepth(cs, x, half, top, 0.8f)));
                if (FindTop(cs, x, half, top, 0.8f, out found, out _, out _))
                {
                    Shift(holder, new Vector3(0, top - found, 0));
                    FindTop(cs, x, half, top, 0.3f, out _, out x0, out x1);
                }
            }
            ledges.Add(new Ledge { x0 = x0, x1 = x1, top = top, t = t, home = t.localPosition });
        }

        /// <summary>Test hook: how many ledges stand at x's tower (its top and the ones up its sides), 0 if none.</summary>
        public int TowerLedgesAt(float x)
        {
            int n = 0;
            foreach (var l in ledges) if (l.x0 < x + 2f && l.x1 > x - 2f && l.t != null && l.t.name == "towerLedge") n++;
            return n;
        }

        // ---------- a swing pole (the user: like the one in his picture, to swing round and fling off) ----------

        /// <summary>A pole sticking out of a ruin's pillar: he catches it, swings round it and lets go flying on.</summary>
        public class Pole { public float x, y, cool; public Transform t; }
        readonly List<Pole> poles = new List<Pole>();
        /// <summary>The pole over the long river, off a pillar after the rock (another way across: from the rock to it,
        /// round it, and on to the stones past the drifting log).</summary>
        public const float RiverPoleX = 91.9f, RiverPoleY = 3.6f;
        // the model's bar is PoleBarAt of its height up; turned PoleYaw it points mostly out of the screen, a little
        // slanted so its length shows; its middle stands PoleBack behind the path, the far end in the pillar
        const float PoleBarAt = 0.8f, PoleYaw = 115f, PoleBack = 0.75f, PoleLong = 1.5f;

        void BuildPoles()
        {
            if (CbStage.Jungle) AddPole(RiverPoleX, RiverPoleY);
        }

        void AddPole(float x, float y)
        {
            var t = CbArt.Joint(root, x, 0, 0, "swingPole");
            // the pillar it comes out of, from below the water up past it, behind the path
            var pil = CbArt.Joint(t, 0, -2f, PoleBack + 0.75f, "pillar");
            float ph = y + 2.9f;
            pil.localScale = new Vector3(0.9f / 1.48f, ph / 3.6f, 0.9f / 1.5f);
            if (CbArt.Prop("Cloudborne/Jungle/Pillar/Pillar", pil, CbArt.PropOutline) == null)
                CbArt.Box(StoneGrey, pil, new Vector3(0, 1.8f, 0), new Vector3(1.48f, 3.6f, 1.5f));
            float h = PoleLong / 1.18f;
            var bar = CbArt.Joint(t, 0, y - PoleBarAt * h, PoleBack, "pole");
            if (Model("SwingPole", bar, new Vector3(PoleLong, h, PoleLong * 0.28f), PoleYaw) == null)
                CbArt.Cylinder(Bamboo, bar, new Vector3(0, PoleBarAt * h, 0), 0.07f, PoleLong, Quaternion.Euler(90, 0, 0));
            poles.Add(new Pole { x = x, y = y, t = t });
        }

        /// <summary>The pole his hands reach at (hx, hy), if one does and it isn't just let go of.</summary>
        public Pole PoleNear(float hx, float hy)
        {
            foreach (var p in poles) if (p.cool <= 0 && Mathf.Abs(hx - p.x) < 0.6f && Mathf.Abs(hy - p.y) < 0.55f) return p;
            return null;
        }

        void UpdatePoles(float dt)
        {
            foreach (var p in poles) p.cool = Mathf.Max(0, p.cool - dt);
        }
    }
}
