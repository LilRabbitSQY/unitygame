using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text.Json;
using FinalDefense.Combat;

internal static class CombatSimulationTests
{
    internal static UnitDefinition Clone(UnitDefinition unit)
        => JsonSerializer.Deserialize<UnitDefinition>(JsonSerializer.Serialize(unit, CombatDataTests.JsonOptions), CombatDataTests.JsonOptions);

    internal static UnitDefinition Dummy(string id = "dummy", bool highGround = false)
        => new UnitDefinition { id = id, name = id, highGround = highGround, hp = new[] { 100, 150, 210 },
            attack = new[] { 20, 40, 70 }, defense = new[] { 5, 9, 15 }, upgradeCost = new[] { 0, 7, 9 },
            deployCost = 10, moveSpeed = 0, attackFrames = 0, block = 1, rangeWidth = 3, rangeDepth = 5 };

    internal static BattleConfiguration Config()
    {
        var cells = new List<Cell>();
        for (int x = -2; x <= 12; x++) for (int y = -2; y <= 2; y++) cells.Add(new Cell(x, y));
        var enemy = Dummy("enemy");
        enemy.hp = new[] { 1000000 };
        enemy.goalDamage = 1;
        return new BattleConfiguration { title = "Offline fixture", initialCost = 99, maxCost = 99,
            costPerSecond = 0, protection = 100, redeploySeconds = 2, maxDeployed = 10,
            path = new[] { new Cell(0, 0), new Cell(20, 0) }, ground = cells.ToArray(),
            highGround = new[] { new Cell(0, 3), new Cell(1, 3), new Cell(2, 3) },
            towers = new[] { Dummy("ground"), Dummy("high", true) }, enemies = new[] { enemy },
            groups = new[] { new SpawnGroup { enemyId = "enemy", wave = 1, count = 1, waveStart = 10000 } } };
    }

    internal static BattleSimulation Arena(UnitDefinition tower = null, UnitDefinition enemy = null)
    {
        var config = Config();
        if (tower != null) config.towers = new[] { tower };
        if (enemy != null) config.enemies = new[] { enemy };
        config.groups[0].enemyId = config.enemies[0].id;
        config.groups[0].waveStart = 0;
        var simulation = new BattleSimulation(config, 11);
        var foe = simulation.Enemies[0];
        foe.x = 1; foe.y = 0; foe.progress = 1; foe.stopMoving = true;
        return simulation;
    }

    internal static void Frames(BattleSimulation simulation, int frames)
    {
        for (int frame = 0; frame < frames; frame++) simulation.Tick(BattleSimulation.StepSeconds);
    }

    internal static void Run()
    {
        BattleLogicTests.Run("deployment validates terrain, facing, occupancy, limit and COST atomically", () =>
        {
            var config = Config(); config.initialCost = 20; config.maxDeployed = 2;
            var sim = new BattleSimulation(config);
            BattleLogicTests.True(sim.TryDeploy("high", 0, 0) == null, "High-ground tower cannot deploy on ground");
            BattleLogicTests.True(sim.TryDeploy("ground", 0, 3) == null, "Ground tower cannot deploy on high ground");
            BattleLogicTests.True(sim.TryDeploy("ground", 0, 0, 1, 1) == null, "Diagonal facing is rejected");
            BattleLogicTests.True(sim.TryDeploy("ground", 100, 100) == null, "Out-of-map position is rejected");
            BattleLogicTests.Near(20, sim.Cost, "Rejected placements spend nothing");
            var first = sim.TryDeploy("ground", 0, 0);
            BattleLogicTests.True(first != null, "Legal ground placement succeeds");
            BattleLogicTests.Near(10, sim.Cost, "One deployment spends once");
            BattleLogicTests.True(sim.TryDeploy("ground", 0, 0) == null, "Occupied position is rejected");
            BattleLogicTests.True(sim.TryDeploy("high", 0, 3) != null, "Legal high-ground placement succeeds");
            BattleLogicTests.Near(0, sim.Cost, "Second deployment uses remaining cost");
            BattleLogicTests.True(sim.TryDeploy("ground", 1, 0) == null, "Insufficient COST cannot deploy");
            sim.AddCost(99);
            BattleLogicTests.True(sim.TryDeploy("ground", 1, 0) == null, "Deployment cap applies even with sufficient COST");
            BattleLogicTests.Equal(2, sim.Report.deployed, "Only successful placements count");
        });

        BattleLogicTests.Run("upgrade consumes exact level costs and preserves missing HP", () =>
        {
            var sim = new BattleSimulation(Config());
            var tower = sim.TryDeploy("ground", 0, 0); tower.hp = 40;
            float before = sim.Cost;
            BattleLogicTests.True(sim.TryUpgrade(tower), "Upgrade to level two succeeds");
            BattleLogicTests.Near(before - 7, sim.Cost, "Level-two upgrade costs seven, not array index zero");
            BattleLogicTests.Equal(2, tower.level, "Level two selected");
            BattleLogicTests.Equal(150, sim.MaxHP(tower), "Authored level-two HP");
            BattleLogicTests.Near(90, tower.hp, "Sixty missing HP is preserved");
            BattleLogicTests.Equal(40, sim.Attack(tower), "Authored level-two attack");
            BattleLogicTests.Equal(9, sim.Defense(tower), "Authored level-two defense");
            BattleLogicTests.True(sim.TryUpgrade(tower), "Upgrade to level three succeeds");
            BattleLogicTests.Near(before - 16, sim.Cost, "Level-three upgrade costs nine");
            BattleLogicTests.Equal(210, sim.MaxHP(tower), "Authored level-three HP");
            BattleLogicTests.True(!sim.TryUpgrade(tower), "Cannot upgrade past level three");
            BattleLogicTests.Near(before - 16, sim.Cost, "Rejected extra upgrade spends nothing");
        });

        BattleLogicTests.Run("unaffordable upgrades leave both level and wallet unchanged", () =>
        {
            var config = Config(); config.initialCost = 16;
            var sim = new BattleSimulation(config); var tower = sim.TryDeploy("ground", 0, 0);
            BattleLogicTests.True(!sim.TryUpgrade(tower), "Six COST cannot buy a seven COST upgrade");
            BattleLogicTests.Equal(1, tower.level, "Rejected upgrade preserves level");
            BattleLogicTests.Near(6, sim.Cost, "Rejected upgrade preserves COST");
        });

        BattleLogicTests.Run("downed towers stop operating and redeploy after the exact cooldown", () =>
        {
            var sim = new BattleSimulation(Config()); var tower = sim.TryDeploy("ground", 0, 0);
            sim.TryUpgrade(tower); sim.DamageDirect(tower, 10000);
            BattleLogicTests.True(tower.downed, "Lethal damage downs the tower");
            BattleLogicTests.Equal(0, tower.level, "Downed level resets to zero");
            BattleLogicTests.Equal(0, sim.BlockCapacity(tower), "Downed tower no longer blocks");
            float before = sim.Cost;
            BattleLogicTests.True(!sim.TryRedeploy(tower), "Redeployment is unavailable before cooldown");
            Frames(sim, 59);
            BattleLogicTests.True(!sim.TryRedeploy(tower), "Cooldown is not rounded down one frame");
            Frames(sim, 1);
            BattleLogicTests.True(sim.TryRedeploy(tower), "Redeployment becomes available at two seconds");
            BattleLogicTests.Near(before - 10, sim.Cost, "Redeploy charges deployment cost once");
            BattleLogicTests.Equal(1, tower.level, "Redeploy returns to level one");
            BattleLogicTests.Near(100, tower.hp, "Redeploy restores authored level-one HP");
        });

        BattleLogicTests.Run("pause and 2x speed share the same fixed simulation clock", () =>
        {
            var config = Config(); config.initialCost = 20; config.costPerSecond = 1;
            var sim = new BattleSimulation(config); sim.Paused = true; sim.Tick(10);
            BattleLogicTests.Near(0, sim.Time, "Pause freezes game time");
            BattleLogicTests.Near(20, sim.Cost, "Pause freezes COST regeneration");
            sim.Paused = false; sim.Speed = 2; sim.Tick(1);
            BattleLogicTests.Near(2, sim.Time, "One real second at 2x advances two game seconds");
            BattleLogicTests.Near(22, sim.Cost, "Regeneration follows game seconds");
            sim.AddCost(1000); BattleLogicTests.Near(99, sim.Cost, "COST is capped");
        });

        BattleLogicTests.Run("spawn timeline is absolute and preserves coincident spawn order", () =>
        {
            var config = Config();
            config.groups = new[] {
                new SpawnGroup { enemyId = "enemy", wave = 1, waveStart = 0, delay = 0, count = 2, interval = 1 },
                new SpawnGroup { enemyId = "enemy", wave = 2, waveStart = 2, delay = 0, count = 1 },
                new SpawnGroup { enemyId = "enemy", wave = 2, waveStart = 2, delay = 0, count = 1 }
            };
            var sim = new BattleSimulation(config);
            BattleLogicTests.Equal(1, sim.Report.spawned, "Zero-time spawn occurs at battle start");
            Frames(sim, 29); BattleLogicTests.Equal(1, sim.Report.spawned, "One-second spawn is not early");
            Frames(sim, 1); BattleLogicTests.Equal(2, sim.Report.spawned, "Interval spawn occurs at one second");
            Frames(sim, 30);
            BattleLogicTests.Equal(4, sim.Report.spawned, "Both second-wave spawns happen while prior enemies remain alive");
            BattleLogicTests.Equal(2, sim.CurrentWave, "Wave advances without waiting for a clear");
            BattleLogicTests.True(sim.Enemies.Select(unit => unit.id).SequenceEqual(new[] { 1, 2, 3, 4 }), "Coincident spawns retain source order");
        });

        BattleLogicTests.Run("fixed step produces equal results across render delta partitions", () =>
        {
            var config = Config(); config.costPerSecond = 1; config.initialCost = 10;
            config.enemies[0].moveSpeed = 1;
            config.groups[0].waveStart = .5f; config.groups[0].count = 3; config.groups[0].interval = .5f;
            var coarse = new BattleSimulation(config, 42); var fine = new BattleSimulation(config, 42);
            coarse.Tick(3); Frames(fine, 90);
            BattleLogicTests.Near(coarse.Time, fine.Time, "Time agrees");
            BattleLogicTests.Near(coarse.Cost, fine.Cost, "Economy agrees");
            BattleLogicTests.Equal(coarse.Report.spawned, fine.Report.spawned, "Spawn counts agree");
            for (int index = 0; index < coarse.Enemies.Count; index++)
                BattleLogicTests.Near(coarse.Enemies[index].progress, fine.Enemies[index].progress, "Path progress agrees");
        });

        BattleLogicTests.Run("protection reaching zero loses once and stops the simulation", () =>
        {
            var config = Config(); config.path = new[] { new Cell(0, 0), new Cell(1, 0) };
            config.protection = 1; config.enemies[0].moveSpeed = 1; config.groups[0].waveStart = 0;
            var sim = new BattleSimulation(config); sim.Tick(2);
            BattleLogicTests.True(sim.Finished && !sim.Report.won, "Leaking final protection point loses");
            BattleLogicTests.Equal(0, sim.Protection, "Protection does not go negative");
            BattleLogicTests.Equal(1, sim.Report.leaked, "Leak is counted once");
            float end = sim.Time; sim.Tick(10);
            BattleLogicTests.Near(end, sim.Time, "Finished battle stops advancing");
            BattleLogicTests.Equal(1, sim.Events.Count(item => item.kind == "finished"), "One finish event");
        });

        BattleLogicTests.Run("victory requires every scheduled spawn and all live enemies to resolve", () =>
        {
            var config = Config(); config.groups[0].waveStart = 0; config.groups[0].count = 2; config.groups[0].interval = 2;
            var sim = new BattleSimulation(config); sim.DamageDirect(sim.Enemies[0], 2000000);
            Frames(sim, 1); BattleLogicTests.True(!sim.Finished, "Clearing first enemy cannot skip future spawns");
            Frames(sim, 59); BattleLogicTests.Equal(2, sim.Report.spawned, "Last scheduled enemy spawned");
            BattleLogicTests.True(!sim.Finished, "Spawning the last enemy does not immediately win");
            sim.DamageDirect(sim.Enemies[1], 2000000); sim.DamageDirect(sim.Enemies[1], 2000000);
            Frames(sim, 1);
            BattleLogicTests.True(sim.Finished && sim.Report.won, "Clearing final enemy wins");
            BattleLogicTests.Equal(2, sim.Report.killed, "Repeated lethal damage does not double count");
        });

        BattleLogicTests.Run("damage consumes shields before HP and modifiers follow source stacking", () =>
        {
            var sim = new BattleSimulation(Config()); var tower = sim.TryDeploy("ground", 0, 0);
            sim.AddShield(tower, 30); sim.DamageDirect(tower, 40);
            BattleLogicTests.Near(0, tower.shield, "Shield absorbs first thirty damage");
            BattleLogicTests.Near(90, tower.hp, "Only ten damage reaches HP");
            sim.Buff(tower, "a", Stat.AttackPercent, .2f, 5);
            sim.Buff(tower, "a", Stat.AttackPercent, .3f, 5);
            sim.Buff(tower, "b", Stat.AttackPercent, .4f, 5);
            BattleLogicTests.Near(.7, sim.ModifierValue(tower, Stat.AttackPercent), "Same source refreshes; independent sources add");
            Frames(sim, 150);
            BattleLogicTests.Near(0, sim.ModifierValue(tower, Stat.AttackPercent), "Temporary modifiers expire");
        });

        BattleLogicTests.Run("projectiles snapshot attacker on cast and defender on impact", () =>
        {
            var definition = Dummy("shooter"); definition.attack = new[] { 100 }; definition.projectile = true; definition.projectileSpeed = 1;
            var sim = Arena(definition); var tower = sim.TryDeploy("shooter", 0, 0); var enemy = sim.Enemies[0];
            tower.stopAttacking = true; enemy.x = 3; enemy.progress = 3; enemy.definition.defense = new[] { 0 };
            sim.Buff(tower, "no-crit", Stat.CritChance, -.05f, 100);
            sim.AttackTargets(tower, new List<UnitState> { enemy });
            sim.Buff(tower, "after-cast", Stat.AttackPercent, 1, 100);
            sim.Buff(enemy, "before-impact", Stat.DefenseFlat, 40, 100);
            float before = enemy.hp; Frames(sim, 30);
            BattleLogicTests.Near(before, enemy.hp, "Traveling projectile has not hit yet");
            Frames(sim, 65);
            BattleLogicTests.Near(before - 60, enemy.hp, "Impact uses cast attack 100 and new defense 40");
        });

        BattleLogicTests.Run("non-projectile attack impacts exactly one frame after casting", () =>
        {
            var definition = Dummy("fighter"); definition.attack = new[] { 100 }; definition.attackFrames = 30;
            var sim = Arena(definition); var tower = sim.TryDeploy("fighter", 0, 0); var enemy = sim.Enemies[0];
            enemy.definition.defense = new[] { 0 }; sim.Buff(tower, "no-crit", Stat.CritChance, -.05f, 100);
            float before = enemy.hp; Frames(sim, 1);
            BattleLogicTests.Near(before, enemy.hp, "Cast frame has no immediate damage");
            Frames(sim, 1); BattleLogicTests.Near(before - 100, enemy.hp, "Following frame settles damage");
        });

        BattleLogicTests.Run("later enemies cannot steal a living enemy's occupied blocking slot", () =>
        {
            var config = Config(); config.enemies[0].moveSpeed = 1;
            config.groups[0].waveStart = 0; config.groups[0].count = 2; config.groups[0].interval = 1;
            var sim = new BattleSimulation(config); var tower = sim.TryDeploy("ground", 2, 0);
            Frames(sim, 48);
            var first = sim.Enemies[0]; var second = sim.Enemies[1];
            BattleLogicTests.True(first.blocker == tower, "First enemy occupies the only blocking slot");
            float blockedAt = first.progress;
            Frames(sim, 150);
            BattleLogicTests.True(first.blocker == tower, "The same living enemy retains its blocker");
            BattleLogicTests.Near(blockedAt, first.progress, "Blocked enemy does not inch forward as later enemies pass");
            BattleLogicTests.True(second.progress > tower.x + 1, "Unblocked second enemy can pass without stealing the slot");
        });

        BattleLogicTests.Run("queued effects resolve on the next frame including effects added from callbacks", () =>
        {
            var sim = Arena(); var tower = sim.TryDeploy("ground", 0, 0); var enemy = sim.Enemies[0];
            int first = 0, chained = 0;
            sim.QueueEffect(tower, enemy, () =>
            {
                first++;
                sim.QueueEffect(tower, enemy, () => chained++);
            });
            BattleLogicTests.Equal(0, first, "Externally queued effect waits for a frame");
            Frames(sim, 1);
            BattleLogicTests.Equal(1, first, "Initial queued effect resolves on frame one");
            BattleLogicTests.Equal(0, chained, "Callback-created effect cannot resolve on its creation frame");
            Frames(sim, 1);
            BattleLogicTests.Equal(1, chained, "Callback-created effect resolves exactly on the next frame");
            Frames(sim, 1);
            BattleLogicTests.Equal(1, first, "Initial effect never runs twice");
            BattleLogicTests.Equal(1, chained, "Chained effect never runs twice");
        });

        BattleLogicTests.Run("authored Level1 has four timed waves, 41 enemies and a connected 32-cell path", () =>
        {
            var config = JsonSerializer.Deserialize<BattleConfiguration>(File.ReadAllText("Assets/Resources/Battle/Level1.json"), CombatDataTests.JsonOptions);
            config.towers = CombatDataTests.LoadUnits("Towers");
            BattleLogicTests.Equal(33, config.path.Length, "Path has 33 nodes");
            for (int index = 1; index < config.path.Length; index++)
                BattleLogicTests.Equal(1, Math.Abs(config.path[index].x - config.path[index - 1].x) + Math.Abs(config.path[index].y - config.path[index - 1].y), "Each path edge is one orthogonal cell");
            var waves = config.groups.GroupBy(group => group.wave).OrderBy(group => group.Key).ToArray();
            var counts = new[] { 6, 8, 14, 13 }; var starts = new[] { 0, 40, 85, 135 };
            BattleLogicTests.Equal(4, waves.Length, "Four waves");
            for (int index = 0; index < waves.Length; index++)
            {
                BattleLogicTests.Equal(counts[index], waves[index].Sum(group => group.count), "Authored wave count");
                BattleLogicTests.True(waves[index].All(group => group.waveStart == starts[index]), "Authored absolute wave start");
            }
            // Hold enemies stationary only for the timeline test, so early leaks
            // cannot end the fixture before all four authored waves have spawned.
            foreach (var enemy in config.enemies) { enemy.moveSpeed = 0; enemy.attackFrames = 0; }
            var sim = new BattleSimulation(config); sim.Tick(160);
            BattleLogicTests.Equal(41, sim.TotalEnemies, "Total scheduled enemies");
            BattleLogicTests.Equal(41, sim.Report.spawned, "All 41 enemies actually spawn at their due times");
            BattleLogicTests.True(!sim.Finished, "All remaining enemies prevent an early victory");
            BattleLogicTests.Equal(4, sim.CurrentWave, "Fourth wave becomes active");
            BattleLogicTests.Near(32, sim.PathLength, "Connected path is 32 cells long");
            var elite = sim.Enemies.Single(unit => unit.definition.id == "stage_elite");
            BattleLogicTests.Equal(4500, sim.MaxHP(elite), "Elite HP multiplier is 1.5");
            BattleLogicTests.Equal(750, sim.Attack(elite), "Elite attack multiplier is 1.5");
            BattleLogicTests.Equal(450, sim.Defense(elite), "Elite defense multiplier is 1.5");
            BattleLogicTests.Near(155, sim.Events.Last(item => item.kind == "spawn").time, "Final elite spawns at 155 seconds");
        });

        BattleLogicTests.Run("a tower prioritizes and attacks its same-cell blocked enemy", () =>
        {
            var definition = Dummy("guard"); definition.attack = new[] { 100 }; definition.attackFrames = 30;
            var sim = Arena(definition); var tower = sim.TryDeploy("guard", 0, 0);
            var blocked = sim.Enemies[0]; blocked.x = 0; blocked.y = 0; blocked.progress = 0; blocked.definition.defense = new[] { 0 };
            var farther = new UnitState { id = 5000, definition = blocked.definition, hp = 1000000,
                enemy = true, x = 2, y = 0, progress = 2, stopMoving = true };
            sim.Enemies.Add(farther); sim.Buff(tower, "no-crit", Stat.CritChance, -.05f, 100);
            Frames(sim, 1);
            BattleLogicTests.True(blocked.blocker == tower, "Same-cell enemy is blocked");
            BattleLogicTests.True(sim.Targets(tower)[0] == blocked, "Blocked enemy outranks a farther-progress enemy");
            // The first frame may already have fired at the forward enemy before
            // movement assigned the blocker. Reset the timer and inspect the next cast.
            sim.Projectiles.Clear(); tower.attackTimer = 0;
            Frames(sim, 2);
            BattleLogicTests.Near(999900, blocked.hp, "Blocked same-cell enemy takes the next ordinary attack");
            BattleLogicTests.Near(1000000, farther.hp, "Unblocked forward enemy is not selected instead");
        });

        BattleLogicTests.Run("freeze defense shown by the model matches damage settlement and expires", () =>
        {
            var definition = Dummy("attacker"); definition.attack = new[] { 200 };
            var sim = Arena(definition); var tower = sim.TryDeploy("attacker", 0, 0); var target = sim.Enemies[0];
            target.definition.defense = new[] { 100 };
            sim.Buff(tower, "no-crit", Stat.CritChance, -.05f, 100);
            sim.Buff(target, "armor", Stat.DefensePercent, .2f, 5);
            BattleLogicTests.Equal(120, sim.Defense(target), "Unfrozen +20% armor");
            sim.Freeze(target, 1);
            BattleLogicTests.Equal(110, sim.Defense(target), "Freeze subtracts ten percentage points from defense");
            sim.AttackTargets(tower, new List<UnitState> { target }); Frames(sim, 1);
            BattleLogicTests.Near(999910, target.hp, "Actual impact subtracts the same 110 defense");
            Frames(sim, 29);
            BattleLogicTests.Equal(120, sim.Defense(target), "Defense recovers exactly when freeze expires");
        });

        BattleLogicTests.Run("only killed enemies award report gold while leaks award none", () =>
        {
            var config = Config(); config.path = new[] { new Cell(0, 0), new Cell(1, 0) };
            config.enemies[0].goldReward = 17; config.enemies[0].moveSpeed = 1;
            config.groups[0].waveStart = 0; config.groups[0].count = 2; config.groups[0].interval = 0;
            var sim = new BattleSimulation(config);
            sim.DamageDirect(sim.Enemies[0], 2000000); sim.DamageDirect(sim.Enemies[0], 2000000);
            sim.Tick(2);
            BattleLogicTests.Equal(1, sim.Report.killed, "One enemy killed");
            BattleLogicTests.Equal(1, sim.Report.leaked, "One enemy leaked");
            BattleLogicTests.Equal(17, sim.Report.goldEarned, "Only the killed enemy contributes gold, exactly once");
            BattleLogicTests.True(sim.Report.won, "Surviving protection still permits victory after final leak");
        });

        BattleLogicTests.Run("direct damage performs two-decimal then integer settlement at impact", () =>
        {
            var sim = Arena(); var target = sim.Enemies[0];
            float before = target.hp; sim.DamageDirect(target, 12.495f);
            BattleLogicTests.Near(before - 13, target.hp, "12.495 settles through 12.50 to thirteen actual HP damage");
        });

        BattleLogicTests.Run("poison slows both attack and effect projectiles and preserves their cast speed", () =>
        {
            var definition = Dummy("poisoned-shooter"); definition.projectile = true; definition.projectileSpeed = 8;
            var sim = Arena(definition); var tower = sim.TryDeploy(definition.id, 0, 0); var target = sim.Enemies[0];
            target.x = 10; target.progress = 10; tower.stopAttacking = true;
            sim.Poison(tower, .1f);
            sim.AttackTargets(tower, new List<UnitState> { target });
            sim.QueueEffect(tower, target, () => { });
            BattleLogicTests.Equal(2, sim.Projectiles.Count, "An actual attack and skill effect each launch a projectile");
            var attack = sim.Projectiles[0]; var effect = sim.Projectiles[1];
            BattleLogicTests.Near(6.4, attack.speed, "Poison applies its minus twenty percent speed to ordinary projectiles");
            BattleLogicTests.Near(6.4, effect.speed, "Poison also applies to queued skill projectiles");
            Frames(sim, 6);
            BattleLogicTests.True(sim.Time > tower.poisonUntil, "Poison expires during flight");
            BattleLogicTests.True(sim.Projectiles.Contains(attack) && sim.Projectiles.Contains(effect), "Both projectiles are still in flight");
            BattleLogicTests.Near(6.4, attack.speed, "Existing attack keeps its poisoned cast speed after poison expires");
            BattleLogicTests.Near(6.4, effect.speed, "Existing effect keeps its poisoned cast speed after poison expires");
            sim.AttackTargets(tower, new List<UnitState> { target });
            BattleLogicTests.Near(8, sim.Projectiles.Last().speed, "A fresh attack after poison expires regains base speed");
        });

        foreach (string skillId in new[] { "counter_store", "pursuit_support" })
            BattleLogicTests.Run("authored SP charge activates on its exact first due frame: " + skillId, () =>
            {
                var definition = Clone(CombatDataTests.LoadUnits("Towers").Single(unit => unit.skill.id == skillId));
                definition.attackFrames = 0;
                var sim = Arena(definition); var tower = sim.TryDeploy(definition.id, 0, definition.highGround ? 3 : 0);
                int frames = skillId == "counter_store" ? 900 : 750;
                Frames(sim, frames - 1);
                BattleLogicTests.True(!tower.skillActive && tower.TotalSkills == 0, "The preceding frame cannot activate early");
                BattleLogicTests.Near(definition.skill.spCost - 1.0 / 30, tower.sp, "The preceding frame is exactly one charge step short");
                Frames(sim, 1);
                BattleLogicTests.True(tower.skillActive, "The authored charging duration activates without a one-frame drift");
                BattleLogicTests.Equal(1, tower.TotalSkills, "The skill starts once");
                BattleLogicTests.Near(0, tower.sp, "Activation consumes the charged SP");
            });

        BattleLogicTests.Run("precise SP charging accepts a skill's external SP refund", () =>
        {
            var definition = Clone(CombatDataTests.LoadUnits("Towers").Single(unit => unit.skill.id == "counter_store"));
            definition.attackFrames = 0;
            var sim = Arena(definition); var tower = sim.TryDeploy(definition.id, 0, 0);
            Frames(sim, 150); BattleLogicTests.Near(5, tower.sp, "Five seconds of ordinary charging");
            tower.sp += 4; // The production support skill writes its refund through this public field.
            Frames(sim, 629);
            BattleLogicTests.True(!tower.skillActive, "Refund does not activate the skill one frame early");
            Frames(sim, 1);
            BattleLogicTests.True(tower.skillActive, "The refunded four SP shorten charging by exactly four seconds");
        });

        BattleLogicTests.Run("COST regeneration unlocks a thirty-cost deployment on frame nine hundred", () =>
        {
            var config = Config(); config.initialCost = 0; config.costPerSecond = 1;
            config.towers[0].deployCost = 30;
            var sim = new BattleSimulation(config);
            Frames(sim, 899);
            BattleLogicTests.True(sim.TryDeploy("ground", 0, 0) == null, "The preceding frame cannot afford thirty COST");
            BattleLogicTests.Near(30 - 1.0 / 30, sim.Cost, "Rejected deployment preserves the exact charge balance");
            Frames(sim, 1);
            BattleLogicTests.True(sim.TryDeploy("ground", 0, 0) != null, "Thirty seconds can pay exactly thirty COST");
            BattleLogicTests.Near(0, sim.Cost, "The affordable deployment spends the balance once");
        });

        BattleLogicTests.Run("a downed tower cannot bypass its fifteen-second cooldown by withdrawing", () =>
        {
            var config = Config(); config.redeploySeconds = 15;
            var sim = new BattleSimulation(config); var tower = sim.TryDeploy("ground", 0, 0);
            sim.DamageDirect(tower, 10000);
            float cost = sim.Cost;
            BattleLogicTests.True(tower.downed, "The tower is actually downed");
            BattleLogicTests.True(!sim.Withdraw(tower), "Withdrawing a downed tower is rejected");
            BattleLogicTests.True(sim.TryDeploy("ground", 0, 0) == null, "The downed tower keeps its occupied cell");
            BattleLogicTests.Near(cost, sim.Cost, "Rejected withdrawal and deployment neither refund nor charge");
            Frames(sim, 449);
            BattleLogicTests.True(!sim.TryRedeploy(tower), "Redeployment still waits the full fifteen seconds");
            Frames(sim, 1);
            BattleLogicTests.True(sim.TryRedeploy(tower), "The normal paid redeployment remains available at fifteen seconds");
            BattleLogicTests.Near(cost - tower.definition.deployCost, sim.Cost, "The one legal redeployment pays once");
        });

        BattleLogicTests.Run("elite leaks record actual protection loss and clamp to remaining protection", () =>
        {
            var config = Config(); config.protection = 3;
            config.path = new[] { new Cell(0, 0), new Cell(1, 0) };
            config.enemies[0].moveSpeed = 1;
            config.groups = new[] { new SpawnGroup { enemyId = "enemy", count = 2, waveStart = 0, interval = 2, goalDamage = 2 } };
            var sim = new BattleSimulation(config); sim.Tick(1.5f);
            BattleLogicTests.Equal(1, sim.Protection, "First elite consumes two protection points");
            BattleLogicTests.Equal(2, sim.Report.protectionLost, "First elite reports its actual two-point loss");
            sim.Tick(2);
            BattleLogicTests.True(sim.Finished && !sim.Report.won, "The second elite exhausts the objective");
            BattleLogicTests.Equal(0, sim.Protection, "Protection never becomes negative");
            BattleLogicTests.Equal(3, sim.Report.protectionLost, "The final two-point elite can consume only the one remaining point");
            BattleLogicTests.Equal(2, sim.Report.leaked, "Both leaking enemies are recorded separately");
        });
    }
}
