using System.Collections.Generic;
using UnityEngine;

namespace Cloudborne
{
    /// <summary>
    /// The short runs: a change of pace in the middle of a stage (the user: short runs, like a chase).
    /// <list type="bullet">
    /// <item>Stage 1-1, RUN!: on the flat stretch after the gorge a great boulder comes down off the mountain behind
    /// him and rolls after him (the user: a boulder to run from, not raiders to fight), smashing the fallen trunks he
    /// slides under, till it plunges into the river (CbLevelBoulder). Never caught by it: ESCAPED!</item>
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
        const int RunBonus = 500, SackCoins = 8;

        bool chaseStarted, chaseDone, thiefStarted, thiefDone, fireRunStarted, fireRunDone;
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
            thiefFoe = null;
            if (sack != null) Destroy(sack.gameObject);
            sack = null;
        }

        /// <summary>Beaten in a run that wasn't over: it starts over when he comes back past its start.</summary>
        void RunsRespawned()
        {
            if (chaseStarted && !chaseDone) { chaseStarted = false; level?.StopBoulder(); }
            if (thiefStarted && !thiefDone) { thiefStarted = false; thiefFoe = null; if (sack != null) Destroy(sack.gameObject); sack = null; }
            if (fireRunStarted && !fireRunDone) { fireRunStarted = false; level?.StopFireChase(); }
        }

        void UpdateRuns()
        {
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
            // a great boulder off the mountain behind him (the user: a boulder to run from, not raiders to fight)
            if (!chaseStarted && hero.x > ChaseFrom && hero.x < ChaseTo - 4f)
            {
                chaseStarted = true;
                level.StartBoulder(CbLevel.MountainTo - CbLevel.BoulderR, CbLevel.SummitTop);
                hud.Banner("RUN! A BOULDER!");
                hud.SetTip("A boulder is rolling down after you - run! <color=#ffb03a>Slide</color> under the trunks, and over the river!");
                shake = Mathf.Max(shake, 0.5f);
            }
            if (!chaseStarted || chaseDone) return;
            // into the river: the run is over - escaped, if it never caught him
            if (level.BoulderSank)
            {
                chaseDone = true;
                if (level.BoulderHits == 0)
                {
                    ChaseEscaped = true;
                    score += RunBonus;
                    hud.Banner("ESCAPED! +" + RunBonus, true);
                    hud.SpeedLines(0.6f, 0.4f);
                    CbFx.Sfx("perfect", 0.7f);
                }
                else hud.Banner("PHEW!", true);
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

    }
}
