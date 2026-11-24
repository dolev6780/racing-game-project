using System.Collections.Generic;
using UnityEngine;

namespace Cloudborne
{
    /// <summary>
    /// The climbing kit (the user: something reasonable to stand on and climb, tied to the jungle, for many stages -
    /// drawn in Canva, approved, built by Meshy): a ruin's pillar with a slab on top (RuinLedge), a platform of planks
    /// round a trunk with a rope ladder (TreePlatform). Each stands behind the path, its top the ledge he lands on; without the models the old slab and its support show.
    /// </summary>
    public partial class CbLevel
    {
        // the tallest ledge a ruin's pillar stands under (the ruin towers over the higher ones are gone: the user)
        const float TowerAt = 4.5f;
        // the model's own size, as parts of its height
        const float PillarWide = 0.49f, PlatformTop = 0.70f, PlatformWide = 0.5f;

        /// <summary>The ledge at x (top, width) built from the climbing kit; false if this stage or this ledge has none
        /// (then the old slab is built).</summary>
        bool ClimbKitLedge(Transform t, float x, float width, float top)
        {
            if (!CbStage.Jungle && !CbStage.Treetops) return false;
            // (up the gorge and over the summit the ledges jut from the cliff's rock: its own slabs, no pillars)
            if (CbStage.Jungle && x > MountainFrom - 1f && x < MountainTo + 1f) return false;
            float baseY = Mathf.Max(SupportAt(x, top - 0.3f, false), -2.5f);
            float h = top - baseY + 0.2f;
            if (h < 0.6f) return false;
            if (CbStage.Treetops) return TreePlatform(t, x, width, top, baseY);
            // (no ruin towers: the user took them all out, the stage to be laid out anew from a Canva plan - a ledge too
            // high for a pillar is the cliff's slab on its column of rock)
            if (h >= TowerAt) return false;
            // a pillar: as tall as it must be, about as wide as it is drawn for that, its slab a little wider than the ledge
            float wide = Mathf.Clamp(h * PillarWide, width + 0.2f, width + 0.6f);
            var holder = CbArt.Joint(t, 0, baseY - 0.2f, 0.25f, "ruinLedge");
            if (Model("RuinLedge", holder, new Vector3(wide, h, wide * 0.8f), 180f) == null) { Object.Destroy(holder.gameObject); return false; }
            // its slab where it is drawn: on his line, its top at the ledge's height, the ledge as wide as the slab
            AddMeasuredLedge(t, holder, x, wide / 2 + 0.3f, top, width);
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

        // ---------- a swing pole (the user: like the one in his picture, to swing round and fling off) ----------

        /// <summary>A pole sticking out of a ruin's pillar: he catches it, swings round it and lets go flying on.</summary>
        public class Pole { public float x, y, cool; public Transform t; }
        readonly List<Pole> poles = new List<Pole>();

        // the model's bar is PoleBarAt of its height up; turned PoleYaw it points mostly out of the screen, a little
        // slanted so its length shows; its middle stands PoleBack behind the path, the far end in the pillar
        const float PoleBarAt = 0.8f, PoleYaw = 115f, PoleBack = 0.75f, PoleLong = 1.5f, PoleBarRadius = 0.06f;

        // (none for now: the user takes them out of the river and will say where they go)
        void BuildPoles() { }

        /// <summary>Test hook: a swing pole at (x, y), its bar's middle there.</summary>
        public void TestAddPole(float x, float y) => AddPole(x, y);

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
            float py = y;
            if (Model("SwingPole", bar, new Vector3(PoleLong, h, PoleLong * 0.28f), PoleYaw) == null)
                CbArt.Cylinder(Bamboo, bar, new Vector3(0, PoleBarAt * h, 0), 0.07f, PoleLong, Quaternion.Euler(90, 0, 0));
            else
            {
                // his hands on the bar itself (the user: touching the pole, not in the air): the bar found where it is
                // drawn, across his line, his hands' height its middle
                var cs = Solidify(bar);
                Shift(bar, new Vector3(0, 0, -BestDepth(cs, x, 0.15f, y, 0.4f)));
                float top = float.NegativeInfinity;
                foreach (float z in ProbeZ)
                    if (TopOf(cs, x, z, y - 0.4f, y + 0.4f, out float tz)) top = Mathf.Max(top, tz);
                if (top > float.NegativeInfinity) py = top - PoleBarRadius;
            }
            poles.Add(new Pole { x = x, y = py, t = t });
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
