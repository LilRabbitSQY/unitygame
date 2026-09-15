using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using FinalDefense.Combat;

// Full, legal playthrough against the checked-in stage. No stat, HP, target,
// position, skill, COST or clock edits are made in the playthrough scenarios.
internal static class Level1Playthrough
{
    internal static void Run()
    {
        for (int seed = 1; seed <= 20; seed++)
        {
            int replaySeed = seed;
            BattleLogicTests.Run("Level1 legal two-unit full clear, seed " + replaySeed,
                () => Replay(replaySeed));
        }
        CancellationTests();
    }

    private static void Replay(int seed)
    {
        var config = JsonSerializer.Deserialize<BattleConfiguration>(
            File.ReadAllText("Assets/Resources/Battle/Level1.json"), CombatDataTests.JsonOptions);
        config.towers = CombatDataTests.LoadUnits("Towers");
        BattleLogicTests.Equal(20, config.initialCost, "Playthrough uses authored initial COST");
        BattleLogicTests.Equal(10, config.protection, "Playthrough uses authored protection");
        BattleLogicTests.Equal(41, config.groups.Sum(group => group.count), "All four authored waves are present");

        var sim = new BattleSimulation(config, seed);
        UnitState guard = sim.TryDeploy("tower_counter_shield", 8, 7, -1, 0);
        UnitState striker = sim.TryDeploy("tower_pursuit_strike", 7, 6, 0, 1);
        BattleLogicTests.True(guard != null && striker != null, "Two initial placements are legal");
        BattleLogicTests.Near(2, sim.Cost, "Initial placements spend exactly eighteen COST");

        // Coordinates are the JSON map's zero-based grid, with +Y north.
        // 00:00 guard (8,7) west; striker (7,6) north.
        // 00:10 striker LV2; 00:22 striker LV3; 00:40 guard LV2; 00:55 guard LV3.
        // Skill activations and targeting are entirely the production simulation's decisions.
        for (int frame = 0; frame < 180 * 30 && !sim.Finished; frame++)
        {
            switch (frame)
            {
                case 10 * 30:
                    BattleLogicTests.True(sim.TryUpgrade(striker), "Striker LV2 is affordable at ten seconds");
                    break;
                case 22 * 30:
                    BattleLogicTests.True(sim.TryUpgrade(striker), "Striker LV3 is affordable at twenty-two seconds");
                    break;
                case 40 * 30:
                    BattleLogicTests.True(sim.TryUpgrade(guard), "Guard LV2 is affordable at forty seconds");
                    break;
                case 55 * 30:
                    BattleLogicTests.True(sim.TryUpgrade(guard), "Guard LV3 is affordable at fifty-five seconds");
                    break;
            }
            sim.Tick(BattleSimulation.StepSeconds);
        }

        BattleLogicTests.True(sim.Finished && sim.Report.won, "All waves are cleared within three minutes");
        BattleLogicTests.Equal(41, sim.Report.spawned, "No scheduled enemy is skipped");
        BattleLogicTests.Equal(41, sim.Report.killed, "All enemies are defeated");
        BattleLogicTests.Equal(0, sim.Report.leaked, "No enemy reaches the protection point");
        BattleLogicTests.Equal(10, sim.Protection, "Protection remains intact");
        BattleLogicTests.Equal(2, sim.Report.deployed, "No hidden extra deployment is used");
        BattleLogicTests.Equal(4, sim.Report.upgrades, "Only the four planned upgrades are used");
        BattleLogicTests.Equal(67, sim.Report.costSpent, "All operations pay their authored prices");
        BattleLogicTests.True(sim.Towers.All(unit => unit.alive && !unit.downed), "Neither unit requires resurrection");
        BattleLogicTests.True(sim.Cost >= 0 && sim.Cost <= config.maxCost, "Economy remains valid");
        Console.WriteLine($"PLAYTHROUGH seed={seed} t={sim.Time:F2}s killed={sim.Report.killed} "
            + $"leaked={sim.Report.leaked} protection={sim.Protection} spent={sim.Report.costSpent}");
    }

    private static void CancellationTests()
    {
        BattleLogicTests.Run("withdrawing an active damage-storage tower cancels its release", () =>
        {
            // This isolated fixture suppresses normal attacks so any new damage
            // can only be a cancelled skill's forbidden end-of-duration payload.
            var definition = CombatSimulationTests.Clone(CombatDataTests.LoadUnits("Towers")
                .Single(unit => unit.skill.id == "counter_store"));
            definition.attackFrames = 0;
            var sim = CombatSimulationTests.Arena(definition);
            var tower = sim.TryDeploy(definition.id, 0, 0);
            var enemy = sim.Enemies[0];
            CombatSimulationTests.Frames(sim, 30 * 30);
            BattleLogicTests.True(tower.skillActive, "Storage skill naturally starts after charging");
            sim.DamageDirect(tower, 500, enemy);
            BattleLogicTests.Near(500, tower.recordedDamage, "Storage contains incoming damage before withdrawal");
            float targetHP = enemy.hp;

            BattleLogicTests.True(sim.Withdraw(tower), "Active unit can be withdrawn");
            CombatSimulationTests.Frames(sim, 2);
            BattleLogicTests.Near(targetHP, enemy.hp, "Withdrawal never releases stored damage on a later frame");
            BattleLogicTests.True(!tower.alive && !tower.skillActive, "Withdrawal clears the active skill state");
            BattleLogicTests.True(!tower.immortality && !tower.stopAttacking, "Cancellation clears temporary flags");
            BattleLogicTests.Near(0, tower.recordedDamage, "Cancelled damage is discarded");
        });

        BattleLogicTests.Run("downing pursuit support cancels its end-of-skill SP reward", () =>
        {
            var catalog = CombatDataTests.LoadUnits("Towers");
            var supportDefinition = CombatSimulationTests.Clone(catalog.Single(unit => unit.skill.id == "pursuit_support"));
            var strikerDefinition = CombatSimulationTests.Clone(catalog.Single(unit => unit.skill.id == "pursuit_strike"));
            supportDefinition.attackFrames = 0;
            strikerDefinition.attackFrames = 0;
            var config = CombatSimulationTests.Config();
            config.towers = new[] { supportDefinition, strikerDefinition };
            config.ground = config.ground.Concat(new[] { new Cell(1, 3) }).ToArray();
            config.groups[0].waveStart = 0;
            // No fixture action consumes RNG before cancellation. Seed one's
            // first draw is below the 45% reward chance, exposing an accidental End call.
            var sim = new BattleSimulation(config, 1);
            var support = sim.TryDeploy(supportDefinition.id, 0, 3, 1, 0);
            var striker = sim.TryDeploy(strikerDefinition.id, 1, 3, 1, 0);
            CombatSimulationTests.Frames(sim, 25 * 30);
            BattleLogicTests.True(support.skillActive, "Support skill naturally reaches its active phase");
            BattleLogicTests.True(sim.InRange(support, striker), "An eligible pursuit ally is in reward range");
            float allySP = striker.sp;

            sim.DamageDirect(support, 100000, sim.Enemies[0]);
            BattleLogicTests.True(support.downed && !support.skillActive, "Lethal damage cancels the support skill");
            BattleLogicTests.Near(allySP, striker.sp, "Cancellation cannot grant the normal-duration SP reward");
            BattleLogicTests.True(!striker.modifiers.Any(modifier => modifier.source.StartsWith("tower:" + support.id + ":")),
                "Cancelled support does not leave its aura or pursuit bonus behind");
        });
    }
}
