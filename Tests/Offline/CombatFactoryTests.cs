using System;
using System.Collections.Generic;
using System.Linq;
using FinalDefense.Combat;
using static BattleLogicTests;

internal static class CombatFactoryTests
{
    private static void Rejected(Action action, string message)
    {
        bool rejected = false;
        try { action(); }
        catch (ArgumentException) { rejected = true; }
        catch (InvalidOperationException) { rejected = true; }
        True(rejected, message);
    }

    internal static void RunAll()
    {
        var towers = CombatDataTests.LoadUnits("Towers");
        var enemies = CombatDataTests.LoadUnits("Enemies");
        Run("duel factory selects eight unique towers and excludes their enemy identities", () =>
        {
            var selected = towers.Where((unit, index) => index % 2 == 0).Select(unit => unit.id).ToArray();
            var map = CombatSimulationTests.Config();
            var duel = BattleFactory.CreateDuel(map, towers, enemies, selected);
            Equal(8, duel.towers.Length, "Selected roster has eight units");
            Equal(8, duel.towers.Select(unit => unit.id).Distinct().Count(), "Selected IDs are unique");
            True(new HashSet<string>(selected).SetEquals(duel.towers.Select(unit => unit.id)), "Factory preserves the exact selected identities");
            Equal(8, duel.enemies.Length, "Opponent receives remaining eight identities");
            var friendlySkills = new HashSet<string>(duel.towers.Select(unit => unit.skill.id));
            True(duel.enemies.All(unit => !friendlySkills.Contains(unit.skill.id)), "Enemy identity never duplicates a selected friendly identity");
            True(duel.groups.All(group => duel.enemies.Any(unit => unit.id == group.enemyId)), "Every spawn group refers to the opposing roster");
            Near(map.initialCost, duel.initialCost, "Map economy is retained");
            Equal(map.protection, duel.protection, "Map protection is retained");
            BattleSimulation.ValidateConfiguration(duel);
        });

        Run("duel factory rejects incomplete, duplicate, oversized and unknown selections", () =>
        {
            var map = CombatSimulationTests.Config();
            var valid = towers.Take(8).Select(unit => unit.id).ToArray();
            Rejected(() => BattleFactory.CreateDuel(map, towers, enemies, valid.Take(7)), "Seven identities are incomplete");
            Rejected(() => BattleFactory.CreateDuel(map, towers, enemies, towers.Take(9).Select(unit => unit.id)), "Nine identities exceed roster size");
            Rejected(() => BattleFactory.CreateDuel(map, towers, enemies, valid.Take(7).Concat(new[] { valid[0] })), "Duplicate selection cannot fill the eighth slot");
            Rejected(() => BattleFactory.CreateDuel(map, towers, enemies, valid.Take(7).Concat(new[] { "missing-tower" })), "Unknown tower cannot enter a roster");
            Rejected(() => BattleFactory.CreateDuel(map, towers, enemies, null), "Null selection is rejected");
        });

        Run("complementary duel rosters allow all sixteen enemy identities to actually spawn", () =>
        {
            var seen = new HashSet<string>();
            for (int offset = 0; offset < 16; offset += 8)
            {
                var selected = towers.Skip(offset).Take(8).Select(unit => unit.id).ToArray();
                var opponents = enemies.Select(CombatSimulationTests.Clone).ToArray();
                foreach (var enemy in opponents) enemy.moveSpeed = 0;
                var duel = BattleFactory.CreateDuel(CombatSimulationTests.Config(), towers, opponents, selected);
                var sim = new BattleSimulation(duel, 73); sim.Tick(160);
                Equal(16, sim.Report.spawned, "Two copies of each opposing identity actually spawn");
                Equal(8, sim.Enemies.Select(unit => unit.definition.id).Distinct().Count(), "All eight current opposing identities appear");
                foreach (var unit in sim.Enemies) seen.Add(unit.definition.id);
            }
            Equal(16, seen.Count, "Across complementary selections, all sixteen authored enemy identities are playable");
            True(seen.SetEquals(enemies.Select(unit => unit.id)), "No authored enemy identity is omitted");
        });
    }
}
