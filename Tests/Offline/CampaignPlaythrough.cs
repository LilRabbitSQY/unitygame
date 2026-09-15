using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using FinalDefense.Combat;

// Exercises the actual campaign factory: 8 drafted identities, 8 different enemy
// identities with skills, and the authored Level1 schedule (4 waves / 41 enemies).
// All battle actions use public, COST-paying deployment, upgrade and revival APIs.
internal static class CampaignPlaythrough
{
    internal static readonly string[] Roster =
    {
        "counter_shield", "counter_store", "pursuit_strike", "pursuit_support",
        "poison_amp", "burn_burst", "heal_aura", "heal_support"
    };

    internal static void Run()
    {
        foreach (int seed in Enumerable.Range(1,20).Concat(new[] { 731 }))
        {
            int replaySeed = seed;
            BattleLogicTests.Run("Campaign legal 41-enemy full clear, seed " + replaySeed,
                () => Replay(replaySeed));
        }
        // These are the eight distinct next-day loadouts produced by the current
        // daily reward rotation plus one legal 0.2-GPA espresso purchase per day.
        // This verifies battle compatibility; inventory and GPA persistence have their own integration tests.
        foreach (string reward in new[] { "signed_fan_art", "makeup_sample", "competition_manual", "balulu_figure",
            "custom_keyboard", "coffee_coupon", "leave_note", "recommendation_letter" })
        {
            string nextDayReward = reward;
            BattleLogicTests.Run("Campaign full clear with next-day reward " + nextDayReward + " and espresso",
                () => Replay(731,new[] { nextDayReward,"espresso_focus" }));
        }
    }

    internal static BattleSimulation Replay(int seed, string[] battleItems = null)
    {
        var map = JsonSerializer.Deserialize<BattleConfiguration>(
            File.ReadAllText("Assets/Resources/Battle/Level1.json"), CombatDataTests.JsonOptions);
        var config = BattleFactory.CreateCampaignDuel(map, CombatDataTests.LoadUnits("Towers"),
            CombatDataTests.LoadUnits("Enemies"), Roster.Select(key => "tower_" + key));
        config.battleItems = battleItems;
        BattleLogicTests.Equal(20, config.initialCost, "Campaign uses the authored initial COST");
        BattleLogicTests.Equal(10, config.protection, "Campaign uses the authored protection");
        BattleLogicTests.Equal(8, config.towers.Length, "Player deploys from the selected eight identities");
        BattleLogicTests.Equal(8, config.enemies.Length, "All eight opposing identities are present");
        BattleLogicTests.True(config.enemies.All(enemy => enemy.skill != null), "Every campaign opponent has its authored skill");
        BattleLogicTests.True(!config.towers.Select(tower => tower.skill.id)
            .Intersect(config.enemies.Select(enemy => enemy.skill.id)).Any(), "Enemy and player identities do not overlap");
        BattleLogicTests.Equal(41, config.groups.Sum(group => group.count), "The complete authored schedule is retained");
        string before = JsonSerializer.Serialize(config, CombatDataTests.JsonOptions);

        var simulation = new BattleSimulation(config,seed);
        var units = new Dictionary<string,UnitState>();
        var plan = new Queue<Func<bool>>();
        var actions = new List<string>();
        int revivals = 0;
        Action<string,string,int,int,int,int> deploy = (name,key,x,y,facingX,facingY) => plan.Enqueue(() =>
        {
            var unit = simulation.TryDeploy("tower_" + key,x,y,facingX,facingY);
            if (unit == null) return false;
            units.Add(name,unit);
            actions.Add($"{simulation.Time:F2}s deploy {key} ({x},{y}) facing ({facingX},{facingY})");
            return true;
        });
        Action<string> upgrade = name => plan.Enqueue(() =>
        {
            var unit = units[name];
            if (!simulation.TryUpgrade(unit)) return false;
            actions.Add($"{simulation.Time:F2}s upgrade {name} LV{unit.level}");
            return true;
        });

        // Execute each queued purchase as soon as the actual battle economy allows.
        // +Y means north. For UI seed 731 without items: deploy at 0,0,13,19,31,
        // 41,53.47 seconds; upgrades at 63,76,92,101.17; guard revives at 120.
        // The revival is essential: leaving the downed blocker unattended causes leaks.
        deploy("guard","counter_shield",8,7,-1,0);
        deploy("striker1","pursuit_strike",7,6,0,1);
        deploy("poison","poison_amp",6,2,0,-1);
        deploy("striker2","pursuit_strike",9,6,0,1);
        deploy("healer","heal_aura",8,6,0,1);
        deploy("guard2","counter_shield",9,7,-1,0);
        deploy("support","pursuit_support",6,6,1,0);
        upgrade("guard");
        upgrade("guard");
        upgrade("healer");
        upgrade("healer");

        for (int frame = 0; frame < 240 * 30 && !simulation.Finished; frame++)
        {
            while (plan.Count > 0 && plan.Peek()()) plan.Dequeue();
            foreach (var unit in simulation.Towers.Where(unit => unit.downed).ToArray())
            {
                float costBefore = simulation.Cost;
                if (!simulation.TryRedeploy(unit)) continue;
                BattleLogicTests.True(simulation.Time >= unit.reviveAt, "A revival waits for the authored cooldown");
                BattleLogicTests.Near(costBefore - unit.definition.deployCost, simulation.Cost,
                    "Reviving pays the authored deployment cost");
                revivals++;
                actions.Add($"{simulation.Time:F2}s revive {unit.definition.id} ({unit.x},{unit.y})");
            }
            simulation.Tick(BattleSimulation.StepSeconds);
        }

        BattleLogicTests.True(simulation.Finished && simulation.Report.won, "The full campaign battle is winnable within four minutes");
        BattleLogicTests.Equal(41, simulation.Report.spawned, "All planned enemies spawn");
        BattleLogicTests.Equal(41, simulation.Report.killed, "Every authored campaign enemy is defeated");
        BattleLogicTests.Equal(0, simulation.Report.leaked, "No enemy reaches the objective");
        BattleLogicTests.Equal(10, simulation.Protection, "The playthrough preserves all protection");
        BattleLogicTests.Equal(7, simulation.Report.deployed, "Only the seven stated deployments are used");
        BattleLogicTests.Equal(4, simulation.Report.upgrades, "Only the four stated upgrades are used");
        BattleLogicTests.Equal(0, plan.Count, "Every planned purchase completes");
        BattleLogicTests.True(before == JsonSerializer.Serialize(config, CombatDataTests.JsonOptions),
            "The complete playthrough never edits catalog stats, map, waves or item definitions");
        if (battleItems == null || battleItems.Length == 0)
        {
            BattleLogicTests.True(revivals >= 1 && revivals <= 2, "One or two legal revivals sustain this no-item strategy");
            BattleLogicTests.Equal(144 + revivals * 10, simulation.Report.costSpent,
                "The plan pays 144 COST plus ten per blocker revival");
        }
        Console.WriteLine($"CAMPAIGN seed={seed} items={string.Join(",",battleItems ?? Array.Empty<string>())} "
            + $"time={simulation.Time:F2}s killed={simulation.Report.killed} leaked={simulation.Report.leaked} "
            + $"protection={simulation.Protection} spent={simulation.Report.costSpent} revivals={revivals}");
        if (seed == 731 && (battleItems == null || battleItems.Length == 0))
            foreach (string action in actions) Console.WriteLine("CAMPAIGN INPUT " + action);
        return simulation;
    }
}
