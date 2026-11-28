using UnityEngine;

namespace Cloudborne
{
    /// <summary>
    /// The totem masks (the user's ideas list, after Kirby's forms - drawn in Canva, approved, built by Meshy): found in
    /// a stage, a mask worn for FormTime seconds gives Raja a form.
    /// <list type="bullet">
    /// <item>Stone: heavy as a boulder - he runs faster, bursts the cracked wall and crates just by running into them,
    /// throws soldiers aside, takes half the harm.</item>
    /// <item>Leaf: light as a leaf - holding JUMP as he falls, he glides down slowly.</item>
    /// <item>Vine: every rock wall is a vine wall to him - pressed into one, he climbs it.</item>
    /// </list>
    /// The mask sits over his face while it lasts, and blinks before it falls away.
    /// </summary>
    public partial class CbHero
    {
        public enum Form { None, Stone, Leaf, Vine }
        /// <summary>The form a worn mask gives him (None without one).</summary>
        public Form form { get; private set; }
        /// <summary>Seconds left of the mask.</summary>
        public float formT;
        public const float FormTime = 12f, GlideFall = 1.8f, StoneRun = 1.35f, StoneHarm = 0.5f;
        public static readonly string[] MaskModels = { "MaskStone", "MaskLeaf", "MaskVine" };
        static readonly string[] MaskNames = { "STONE MASK", "LEAF MASK", "VINE MASK" };
        static readonly string[] MaskTips =
        {
            "Stone mask: run into cracked stone, crates and soldiers to smash through!",
            "Leaf mask: hold <color=#ffc94a>JUMP</color> as you fall to glide",
            "Vine mask: push into any rock wall to climb it",
        };
        /// <summary>Every rock wall climbable as vines (the vine mask: CbLevel.VineWallAt reads it).</summary>
        public static bool VineEverywhere;
        float stoneCd;
        Transform maskOn;

        /// <summary>A mask put on (0 stone, 1 leaf, 2 vine): its form for FormTime seconds.</summary>
        public void PutOnMask(int kind)
        {
            TakeOffMask();
            form = (Form)(kind + 1);
            formT = FormTime;
            CbSave.MasksEver |= 1 << kind;
            VineEverywhere = form == Form.Vine;
            g.hud.Banner(MaskNames[kind] + "!", true);
            g.hud.SetTip(MaskTips[kind]);
            CbFx.Sfx("perfect", 0.8f);
            CbVfx.Ring(new Vector3(x, y + 1.1f, -0.4f), new Color(0.6f, 1f, 0.6f) * 0.6f, 2.2f, 0.4f);
            // over his face: on his head bone, looking the way he faces
            if (model == null) return;
            Transform head = null;
            foreach (var b in model.root.GetComponentsInChildren<Transform>())
                if (b.name.EndsWith("Head")) { head = b; break; }
            if (head == null) return;
            var holder = new GameObject("mask").transform;
            holder.SetParent(head, false);
            var m = CbArt.Prop($"Cloudborne/Items/Masks/{MaskModels[kind]}", holder, CbArt.PropOutline);
            if (m == null) { Object.Destroy(holder.gameObject); return; }
            var fwd = new Vector3(facing, 0, 0);
            holder.position = head.position + fwd * 0.13f + Vector3.up * 0.06f;
            holder.rotation = Quaternion.LookRotation(-fwd, Vector3.up);
            holder.localScale = Vector3.one * (0.3f / Mathf.Max(0.01f, head.lossyScale.x));
            maskOn = holder;
        }

        // the staffs' colours (until their own models come: the drawing is with the user for approval) and trails
        static readonly Color[] StaffTints = { Color.white, new Color(1f, 0.55f, 0.35f), new Color(0.75f, 0.9f, 1f), new Color(0.6f, 0.95f, 0.55f) };
        static readonly Color[] StaffTrails = { Color.white, new Color(1f, 0.5f, 0.15f), new Color(0.6f, 0.85f, 1f), new Color(0.45f, 1f, 0.45f) };

        // the staffs' own models (Canva MAHcIhNLLDI approved, built by Meshy), beside the oak staff's
        static readonly string[] StaffModels = { "Staff", "StaffFire", "StaffWind", "StaffVine" };
        int dressedStaff = -1;

        /// <summary>
        /// The staff in his hand for the kind he carries (CbSave.StaffKind): its own model if it is in the project (the
        /// staff's tip, with its trail, kept), else the oak staff coloured; the trail glows to match.
        /// </summary>
        public void TintStaff()
        {
            if (staff == null) return;
            int k = CbSave.StaffKind;
            if (k != dressedStaff)
            {
                string path = "Cloudborne/Props/Staff/" + StaffModels[k];
                if (Resources.Load<GameObject>(path) != null)
                {
                    foreach (Transform c in staff)
                        if (c.name != "staffTip") Object.Destroy(c.gameObject);
                    CbArt.Prop(path, staff);
                }
                else CbArt.Tint(staff, StaffTints[k]);
                dressedStaff = k;
            }
            if (staffTrail != null && staffTrail.material != null) staffTrail.material.color = StaffTrails[k] * (k == 0 ? 1f : 1.6f);
        }

        void TakeOffMask()
        {
            if (maskOn != null) Object.Destroy(maskOn.gameObject);
            maskOn = null;
            form = Form.None;
            formT = 0;
            VineEverywhere = false;
        }

        /// <summary>Each frame while a mask is on: its time running out (it blinks near the end), the stone's smashing.</summary>
        void UpdateForm(float dt)
        {
            if (form == Form.None) return;
            formT -= dt;
            if (maskOn != null) maskOn.gameObject.SetActive(formT > 2f || Mathf.Repeat(formT, 0.25f) < 0.16f);
            if (formT <= 0)
            {
                CbFx.Puff(new Vector3(x, y + 1.4f, -0.4f), new Color(0.7f, 0.9f, 0.6f), 8, 1.6f, 0.6f, 0.4f);
                TakeOffMask();
                return;
            }
            if (form == Form.Leaf && air && vy < -0.5f && Random.value < 0.3f)
                CbFx.Spark(new Vector3(x, y + 1.6f, -0.4f), new Color(0.5f, 0.9f, 0.4f), 1, 1f, 0.2f, 0.5f, 1);
            if (form != Form.Stone) return;
            // the boulder: running into cracked stone, crates or soldiers, he bursts through them
            stoneCd = Mathf.Max(0, stoneCd - dt);
            if (Mathf.Abs(vx) < 3f || stoneCd > 0) return;
            int dir = vx > 0 ? 1 : -1;
            bool hit = false;
            if (g.level != null && g.level.Strike(x, y, dir, 0.55f, true)) hit = true;
            if (g.items != null && g.items.HitCrates(x, y, dir, 0.9f)) hit = true;
            foreach (var e in g.enemies)
            {
                if (e.dead || Mathf.Abs(e.y - y) > 1.2f) continue;
                float ahead = (e.x - x) * dir;
                if (ahead < -0.2f || ahead > 0.9f + e.w) continue;
                g.HurtEnemy(e, 10f, dir, 8f, 4.5f, true, true, false, true);
                hit = true;
            }
            if (hit)
            {
                stoneCd = 0.3f;
                g.shake = Mathf.Max(g.shake, 0.3f);
                CbFx.Spark(new Vector3(x + dir * 0.6f, y + 0.8f, -0.4f), CbFx.Grey, 10, 5, 0.4f, 0.4f, 4);
            }
        }
    }
}
