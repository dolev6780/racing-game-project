using System.Collections.Generic;
using UnityEngine;

namespace Cloudborne
{
    /// <summary>
    /// The short runs: a change of pace in the middle of a stage (the user: short runs, like a chase).
    /// <list type="bullet">
    /// <item>Stage 1-1, RUN!: on the flat stretch after the gorge a pack of raiders bursts in from behind, faster than
    /// soldiers walk. Raja runs for the lantern ahead - over the cracked boulder, under the low branch (they can't duck
    /// under it), over the crate. Reaching it with them still after him: ESCAPED!, and they give up. Caught, it is a
    /// fight.</item>
    /// <item>Stage 1-2, THIEF!: a raider snatches a sack of loot and runs off along the bough under the saw drone,
    /// a little slower than Raja runs. Caught and struck down, the sack bursts; reaching the end of the bough, he is
    /// gone.</item>
    /// </list>
    /// </summary>
    public partial class CbGame
    {
        /// <summary>Stage 1-1's chase: where it starts, the lantern it ends at, the low branch on the way.</summary>
        public const float ChaseFrom = 168f, ChaseTo = 196f, ChaseBranchX = 187f;
        /// <summary>Stage 1-2's thief: where he snatches the sack, where he gets away.</summary>
        public const float ThiefFrom = 91f, ThiefTo = 120f;
        const int ChasePack = 4, RunBonus = 500, SackCoins = 8;
        // how much faster than a soldier walks the pack runs (he runs 7 m/s; they come on at about 6)
        const float ChaseSpeed = 2.2f;

        bool chaseStarted, chaseDone, thiefStarted, thiefDone, fireRunStarted, fireRunDone;
        readonly List<CbEnemy> chasers = new List<CbEnemy>();
        CbEnemy thiefFoe;
        Transform sack;

        /// <summary>Test hooks: the runs' progress.</summary>
        public bool ChaseStarted => chaseStarted;
        public bool ChaseEscaped { get; private set; }
        public bool ThiefStarted => thiefStarted;
        public bool ThiefCaught { get; private set; }
        public CbEnemy Thief => thiefFoe;
        public bool FireRunStarted => fireRunStarted;
        public bool FireRunEscaped { get; private set; }
        /// <summary>Test hook: after SkipFights, the fire run waits to start again.</summary>
        public void ArmFireRun() { fireRunStarted = fireRunDone = false; FireRunEscaped = false; }

        void ResetRuns()
        {
            chaseStarted = chaseDone = thiefStarted = thiefDone = fireRunStarted = fireRunDone = false;
            ChaseEscaped = ThiefCaught = FireRunEscaped = false;
            chasers.Clear();
            thiefFoe = null;
            if (sack != null) Destroy(sack.gameObject);
            sack = null;
        }

        /// <summary>Beaten in a run that wasn't over: it starts over when he comes back past its start.</summary>
        void RunsRespawned()
        {
            if (chaseStarted && !chaseDone) { chaseStarted = false; chasers.Clear(); }
            if (thiefStarted && !thiefDone) { thiefStarted = false; thiefFoe = null; if (sack != null) Destroy(sack.gameObject); sack = null; }
            if (fireRunStarted && !fireRunDone) { fireRunStarted = false; level?.StopFireChase(); }
        }

        void UpdateRuns()
        {
            ClearRetreated();
            if (lockZone != null) return;
            if (CbStage.Jungle) UpdateChase();
            else if (CbStage.Treetops) UpdateThief();
            else if (CbStage.Ember) UpdateFireRun();
        }

        /// <summary>Stage 1-5, FIRE!: the jungle behind him goes up and the fire comes on after him, to the temple's
        /// threshold (CbLevelEmberWay).</summary>
        void UpdateFireRun()
        {
            if (!fireRunStarted && hero.x > CbLevel.FireRunFrom && hero.x < CbLevel.FireRunTo - 6f)
            {
                fireRunStarted = true;
                level.StartFireChase(hero.x);
                hud.Banner("RUN! THE JUNGLE IS BURNING!");
                hud.SetTip("The fire is right behind you - run for the temple! <color=#ffb03a>Jump</color> the flames, <color=#ffb03a>slide</color> under the burning bough");
                shake = Mathf.Max(shake, 0.4f);
            }
            if (!fireRunStarted || fireRunDone) return;
            if (hero.x >= CbLevel.FireRunTo)
            {
                fireRunDone = true;
                FireRunEscaped = true;
                level.StopFireChase();
                score += RunBonus;
                hud.Banner("SAFE! +" + RunBonus, true);
                hud.SpeedLines(0.6f, 0.4f);
                CbFx.Sfx("perfect", 0.7f);
            }
        }

        void UpdateChase()
        {
            if (!chaseStarted && hero.x > ChaseFrom && hero.x < ChaseTo - 4f)
            {
                chaseStarted = true;
                for (int i = 0; i < ChasePack; i++)
                {
                    var e = SpawnAt(CbEnemy.Kind.Hyena, camX - RunInFrom - i * 1.6f);
                    e.runningIn = e.arena = true;
                    e.speedMul = ChaseSpeed;
                    e.facing = 1;
                    chasers.Add(e);
                }
                hud.Banner("RUN!");
                hud.SetTip("A pack of raiders! Run for the lantern - <color=#ffb03a>slide</color> under the low branch, they can't");
                shake = Mathf.Max(shake, 0.3f);
                CbFx.Sfx("whistle", 0.7f);
            }
            if (!chaseStarted || chaseDone) return;
            bool anyLeft = chasers.Exists(e => !e.dead && !e.gone);
            if (!anyLeft) { chaseDone = true; return; }
            if (hero.x >= ChaseTo)
            {
                // the lantern reached with them still after him: they give up and turn back
                chaseDone = true;
                ChaseEscaped = true;
                foreach (var e in chasers) if (!e.dead) { e.retreat = true; e.arena = e.runningIn = false; }
                score += RunBonus;
                hud.Banner("ESCAPED! +" + RunBonus, true);
                hud.SpeedLines(0.6f, 0.4f);
                CbFx.Sfx("perfect", 0.7f);
            }
        }

        void UpdateThief()
        {
            if (!thiefStarted && hero.x > ThiefFrom && hero.x < ThiefTo - 8f)
            {
                thiefStarted = true;
                var e = SpawnAt(CbEnemy.Kind.Hyena, hero.x + 6f);
                e.thief = true;
                e.hp = e.max = 20;
                e.facing = 1;
                thiefFoe = e;
                var bag = CbArt.Toon("#8a6a3a", CbArt.PropOutline);
                sack = CbArt.Part(CbMesh.Sphere, bag, actors, new Vector3(e.x, 1.1f, -0.2f), new Vector3(0.55f, 0.6f, 0.5f));
                sack.name = "thiefSack";
                hud.Banner("THIEF! CATCH HIM!");
                hud.SetTip("A raider is getting away with loot - catch him! <color=#ffb03a>Slide</color> under the saw drone (stick ↓)");
                CbFx.Sfx("whistle", 0.6f);
            }
            if (!thiefStarted || thiefDone || thiefFoe == null) return;
            var t = thiefFoe;
            if (sack != null) sack.position = new Vector3(t.x - t.facing * 0.38f, t.y + 1.15f + Mathf.Sin(clock * 14f) * 0.04f, -0.25f);
            if (t.dead)
            {
                // caught: the sack bursts
                thiefDone = true;
                ThiefCaught = true;
                if (sack != null) Destroy(sack.gameObject);
                sack = null;
                var at = new Vector3(t.x, t.y + 1f, 0);
                for (int k = 0; k < SackCoins; k++) items.Spawn(CbItems.Kind.Coin, at);
                items.Spawn(CbItems.Kind.Mango, at);
                score += RunBonus;
                hud.Banner("CAUGHT HIM! +" + RunBonus, true);
                CbVfx.Ring(at, new Color(1f, 0.85f, 0.3f) * 0.5f, 2f, 0.4f);
                return;
            }
            if (t.x >= ThiefTo)
            {
                thiefDone = true;
                t.gone = true;
                if (sack != null) Destroy(sack.gameObject);
                sack = null;
                hud.Popup(new Vector3(t.x - 1f, 2.4f, 0), "HE GOT AWAY!", CbHud.PopStyle.Big);
            }
        }

        /// <summary>Called each frame by the enemies' loop: chasers that gave up go once far enough off.</summary>
        void ClearRetreated()
        {
            foreach (var e in chasers)
                if (e.retreat && !e.dead && Mathf.Abs(e.x - hero.x) > 16f) e.gone = true;
        }
    }
}
