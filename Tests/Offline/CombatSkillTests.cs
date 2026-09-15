using System;
using System.Collections.Generic;
using System.Linq;
using FinalDefense.Combat;
using static BattleLogicTests;

internal static class CombatSkillTests
{
    private sealed class Fixture
    {
        internal BattleSimulation sim;
        internal UnitState caster, ally, target;
    }

    private static Fixture TowerFixture(UnitDefinition definition, string allyTag = "fixture")
    {
        var config = CombatSimulationTests.Config();
        var ally = CombatSimulationTests.Dummy("ally");
        ally.hp = new[] { 1000000 }; ally.tag = allyTag;
        config.towers = new[] { CombatSimulationTests.Clone(definition), ally };
        if (definition.highGround) config.highGround = new[] { new Cell(0, 0) };
        config.groups[0].waveStart = 0;
        var sim = new BattleSimulation(config, 11);
        var caster = sim.TryDeploy(definition.id, 0, 0);
        var friend = sim.TryDeploy("ally", 1, 0);
        True(caster != null && friend != null, "Skill fixture deploys both participants");
        caster.attackTimer = 100000;
        var target = sim.Enemies[0]; target.x = 1; target.y = 0; target.progress = 1; target.stopMoving = true;
        return new Fixture { sim = sim, caster = caster, ally = friend, target = target };
    }

    private static void Activate(Fixture f)
    {
        f.caster.sp = f.caster.definition.skill.spCost;
        CombatSimulationTests.Frames(f.sim, 1);
        if (f.caster.definition.skill.spCost > 0)
            Equal(1, f.caster.TotalSkills, "Ready tower skill activates exactly once");
        else True(f.caster.skillActive, "Permanent passive skill is active");
        // Effects resolve using the same frame/projectile queue as production.
        CombatSimulationTests.Frames(f.sim, 14);
    }

    private static UnitState ExtraEnemy(Fixture f, int id, float x, float y)
    {
        var target = new UnitState { id = id, definition = f.target.definition, enemy = true,
            hp = 1000000, x = x, y = y, stopMoving = true, progress = x };
        f.sim.Enemies.Add(target);
        return target;
    }

    private static void TowerSkills()
    {
        var definitions = CombatDataTests.LoadUnits("Towers");
        foreach (var definition in definitions)
            Run("tower skill actual effect: " + definition.skill.id, () =>
            {
                string id = definition.skill.id;
                var f = TowerFixture(definition, id == "direct_speed" ? "直伤" : "fixture");
                f.caster.hp = Math.Max(1, f.caster.hp - 300);
                f.sim.Buff(f.caster, "test-no-crit", Stat.CritChance, -1, 100);
                float healthBefore = f.caster.hp;
                Activate(f);
                switch (id)
                {
                    case "direct_crit":
                        Near(.15, f.sim.ModifierValue(f.caster, Stat.AttackPercent), "Level-one direct attack aura");
                        Near(-.1, f.sim.ModifierValue(f.caster, Stat.DefensePercent), "Caster defense tradeoff");
                        Near(.5, f.sim.ModifierValue(f.caster, Stat.CritDamage), "Crit-damage aura");
                        break;
                    case "direct_speed":
                        Near(.4, f.sim.ModifierValue(f.ally, Stat.AttackSpeed), "Adjacent direct teammate receives +40% speed");
                        Near(.5, f.sim.ModifierValue(f.caster, Stat.DefensePercent), "Caster receives +50% defense");
                        CombatSimulationTests.Frames(f.sim, 30);
                        Near(healthBefore + 100, f.caster.hp, "Caster regenerates 100 per elapsed second");
                        break;
                    case "burn_stack":
                        var second = ExtraEnemy(f, 9001, 2, 1);
                        var outside = ExtraEnemy(f, 9002, 2, 4);
                        f.sim.AttackTargets(f.caster, new List<UnitState> { f.target });
                        CombatSimulationTests.Frames(f.sim, 15);
                        True(f.target.burns.Count >= 2 && second.burns.Count >= 2, "Burn applies to all enemies in skill range, not only basic target");
                        Equal(0, outside.burns.Count, "Burn respects expanded skill bounds");
                        break;
                    case "burn_burst":
                        Equal(2, f.target.burns.Count, "Instant burst applies two burn stacks");
                        Near(999800, f.target.hp, "Instant burst settles two stacks once without forced crit");
                        Near(.25, f.sim.ModifierValue(f.caster, Stat.AttackSpeed), "Burst grants five-second +25% attack speed");
                        break;
                    case "poison_spread":
                        Near(100, f.sim.ModifierValue(f.caster, Stat.AttackFlat), "Poison striker gains flat attack");
                        Near(.5, f.sim.ModifierValue(f.caster, Stat.AttackSpeed), "Poison striker gains attack speed");
                        Near(0, f.sim.TowerConditionalAttackBonus(f.caster, f.target), "Non-poisoned target gets no conditional bonus");
                        f.sim.Poison(f.target, 5);
                        Near(2.5, f.sim.TowerConditionalAttackBonus(f.caster, f.target), "Poisoned target enables level-one +250% attack");
                        Near(.2, f.sim.TowerConditionalShelter(f.caster, f.target), "Only poisoned attacker grants +20% shelter");
                        break;
                    case "poison_amp":
                        True(f.sim.PoisonTiles.Count > 0, "Skill places real poison ground tiles");
                        True(f.target.poisonUntil > f.sim.Time, "Standing on a tile causes poison");
                        Near(-.1, f.sim.ModifierValue(f.target, Stat.DefensePercent), "Poisoned enemies lose 10% defense");
                        Near(.1, f.sim.ModifierValue(f.target, Stat.DamageTaken), "Poisoned enemies take 10% more damage");
                        break;
                    case "weaken_def":
                        Near(-.15, f.sim.ModifierValue(f.target, Stat.DefensePercent), "Solo weakening reduces defense 15%");
                        Near(-.5, f.sim.ModifierValue(f.target, Stat.Healing), "Solo weakening halves received healing");
                        break;
                    case "weaken_slow":
                        True(f.target.frozenUntil > f.sim.Time, "Instant skill freezes its target");
                        CombatSimulationTests.Frames(f.sim, 20);
                        Near(-.6, f.sim.ModifierValue(f.target, Stat.MoveSpeed), "After thaw, solo skill applies 60% slow");
                        break;
                    case "control_retreat":
                        Near(1.2, f.sim.ModifierValue(f.caster, Stat.AttackPercent), "Retreat striker gains +120% attack");
                        Near(.3, f.sim.ModifierValue(f.caster, Stat.AttackSpeed), "Retreat striker gains +30% speed");
                        f.sim.AttackTargets(f.caster, new List<UnitState> { f.target });
                        CombatSimulationTests.Frames(f.sim, 3);
                        True(f.target.retreatUntil > f.sim.Time, "Successful hit inflicts retreat");
                        break;
                    case "control_frozen":
                        Near(.6, f.sim.ModifierValue(f.caster, Stat.AttackPercent), "Control support gains +60% attack");
                        f.sim.Retreat(f.target, 2); CombatSimulationTests.Frames(f.sim, 1);
                        Near(.35, f.sim.ModifierValue(f.target, Stat.Vulnerability), "Retreating enemy becomes 35% vulnerable");
                        break;
                    case "counter_shield":
                        Near(f.sim.MaxHP(f.caster), f.caster.hp, "Shield tank heals fully at activation");
                        Near(BattleSimulation.Round(f.sim.MaxHP(f.caster) * .3f), f.caster.shield, "Shield equals 30% maximum HP");
                        True(f.caster.stopAttacking, "Shield tank stops attacking");
                        float before = f.target.hp;
                        f.sim.DamageDirect(f.caster, 100, f.target);
                        CombatSimulationTests.Frames(f.sim, 3);
                        Near(before - BattleSimulation.Round(f.sim.Defense(f.caster) * .6f), f.target.hp, "Shield absorbs and defense counter still damages attacker");
                        break;
                    case "counter_store":
                        True(f.caster.immortality && f.caster.stopAttacking, "Stored-damage tank stops attacking and cannot die");
                        f.sim.DamageDirect(f.caster, 1000000);
                        Near(1, f.caster.hp, "Lethal hit leaves one HP");
                        float cap = f.sim.MaxHP(f.caster) * 3;
                        Near(cap, f.caster.recordedDamage, "Raw incoming damage is capped at triple maximum HP");
                        float enemyBefore = f.target.hp;
                        f.sim.Tick(11);
                        Near(enemyBefore - cap, f.target.hp, "End burst releases capped stored damage");
                        True(!f.caster.immortality && !f.caster.stopAttacking, "End clears immortality and stop-attacking state");
                        break;
                    case "pursuit_strike":
                        Near(.8, f.sim.ModifierValue(f.caster, Stat.AttackPercent), "Pursuit striker gains +80% attack");
                        var other = ExtraEnemy(f, 9010, 1, 1);
                        for (int index = 0; index < 30; index++)
                        {
                            int attacks = f.caster.TotalAttacks;
                            int projectiles = f.sim.Projectiles.Count;
                            f.sim.AttackTargets(f.caster, new List<UnitState> { f.target, other });
                            int added = f.caster.TotalAttacks - attacks;
                            True(added == 1 || added == 2, "Each two-target attack gets at most one self follow-up");
                            Equal(added * 2, f.sim.Projectiles.Count - projectiles, "One follow-up roll applies to both targets without recursion");
                        }
                        break;
                    case "pursuit_support":
                        Near(healthBefore * .8f, f.caster.hp, "Support sacrifices 20% current HP");
                        Near(-.2, f.sim.ModifierValue(f.caster, Stat.DefensePercent), "Support sacrifices defense");
                        f.ally.definition.tag = "追击";
                        int old = f.ally.TotalAttacks;
                        f.sim.AttackTargets(f.ally, new List<UnitState> { f.target });
                        Equal(old + 2, f.ally.TotalAttacks, "Support grants exactly one follow-up without recursion");
                        Near(.05, f.sim.ModifierValue(f.ally, Stat.AttackSpeed), "Follow-up grants one level-one speed stack");
                        break;
                    case "heal_aura":
                        Near(.2, f.sim.ModifierValue(f.caster, Stat.AttackPercent), "Permanent healing passive grants +20% attack");
                        break;
                    case "heal_support":
                        Near(.15, f.sim.ModifierValue(f.caster, Stat.AttackPercent), "Healing support gains +15% attack");
                        Near(.15, f.sim.ModifierValue(f.ally, Stat.Healing), "Nearby friend receives +15% healing");
                        Near(.1, f.sim.ModifierValue(f.ally, Stat.Shelter), "Nearby friend receives +10% shelter");
                        Near(.15, f.sim.ModifierValue(f.target, Stat.Vulnerability), "Nearby enemy receives +15% vulnerability");
                        break;
                    default: throw new Exception("No behavioral test for authored tower skill: " + id);
                }
            });

        Run("tower aura clears when ally leaves, caster falls and skill ends", () =>
        {
            var definition = definitions.Single(item => item.skill.id == "direct_crit");
            var f = TowerFixture(definition, "直伤"); Activate(f);
            Near(.15, f.sim.ModifierValue(f.ally, Stat.AttackPercent), "Ally receives active aura");
            f.ally.x = 10; CombatSimulationTests.Frames(f.sim, 1);
            Near(0, f.sim.ModifierValue(f.ally, Stat.AttackPercent), "Leaving range removes aura immediately");
            f.ally.x = 1; CombatSimulationTests.Frames(f.sim, 1);
            Near(.15, f.sim.ModifierValue(f.ally, Stat.AttackPercent), "Returning restores aura");
            f.sim.DamageDirect(f.caster, 1000000); CombatSimulationTests.Frames(f.sim, 1);
            Near(0, f.sim.ModifierValue(f.ally, Stat.AttackPercent), "Downed caster supplies no aura");
            var ended = TowerFixture(definition, "直伤"); Activate(ended); ended.sim.Tick(21);
            Near(0, ended.sim.ModifierValue(ended.ally, Stat.AttackPercent), "Expired skill supplies no aura");
        });

        Run("weaken partner is checked in expanded skill range before enhanced freeze", () =>
        {
            var definition = definitions.Single(item => item.skill.id == "weaken_slow");
            var f = TowerFixture(definition, "削弱"); f.ally.x = 4; f.target.x = 3; f.target.progress = 3;
            Activate(f);
            True(f.target.frozenUntil > f.sim.Time + 1, "Partner beyond basic range enables two-second freeze");
            CombatSimulationTests.Frames(f.sim, 50);
            Near(-.7, f.sim.ModifierValue(f.target, Stat.MoveSpeed), "Enhanced freeze ends with 70% slow");
        });

        Run("level-two shield counter keeps fractional defense until final damage settlement", () =>
        {
            var definition = definitions.Single(item => item.skill.id == "counter_shield");
            var f = TowerFixture(definition);
            True(f.sim.TryUpgrade(f.caster), "Counter tank upgrades to authored level two");
            Activate(f);
            float before = f.target.hp;
            f.sim.DamageDirect(f.caster, 100, f.target);
            CombatSimulationTests.Frames(f.sim, 1);
            Near(before - 1218, f.target.hp, "967 defense times 1.8 times 0.7 settles to 1218, not 1219");
        });
    }

    private static void EnemySkills()
    {
        foreach (var authored in CombatDataTests.LoadUnits("Enemies"))
            Run("enemy skill actual effect: " + authored.skill.id, () =>
            {
                var definition = CombatSimulationTests.Clone(authored); definition.attackFrames = 0;
                var config = CombatSimulationTests.Config();
                var tower = CombatSimulationTests.Dummy("target"); tower.hp = new[] { 1000000 }; tower.defense = new[] { 0 };
                config.towers = new[] { tower }; config.enemies = new[] { definition };
                config.groups[0].enemyId = definition.id; config.groups[0].waveStart = 0;
                var sim = new BattleSimulation(config, 11);
                var target = sim.TryDeploy("target", 1, 0);
                var enemy = sim.Enemies[0]; enemy.x = .5f; enemy.progress = .5f; enemy.stopMoving = true; enemy.blocker = target;
                enemy.skillTimer = 0; enemy.hp -= 2000;
                sim.Buff(enemy, "test-no-crit", Stat.CritChance, -.05f, 100);
                float enemyBefore = enemy.hp;
                CombatSimulationTests.Frames(sim, 1); enemy.skillTimer = 10000;
                CombatSimulationTests.Frames(sim, 14);
                Equal(1, enemy.TotalSkills, "Ready enemy skill executes exactly once");
                switch (definition.skill.id)
                {
                    case "direct_crit": Near(999100, target.hp, "180% of 500 attack"); break;
                    case "direct_speed":
                        Near(999325, target.hp, "150% of 450 attack"); Near(enemyBefore + 100, enemy.hp, "Self-heal 100"); break;
                    case "burn_stack": Equal(2, target.burns.Count, "Enemy applies two burn stacks"); break;
                    case "burn_burst":
                        Equal(1, target.burns.Count, "Enemy applies one area burn"); Near(999900, target.hp, "Area burn settles once immediately"); break;
                    case "poison_spread":
                        True(target.poisonUntil > sim.Time, "Poison applied"); Near(-.1, sim.ModifierValue(target, Stat.DefensePercent), "Poison skill defense reduction"); break;
                    case "poison_amp":
                        True(target.poisonUntil > sim.Time, "Area poison applied"); Near(-50, sim.ModifierValue(target, Stat.DefenseFlat), "Area poison flat defense loss"); break;
                    case "weaken_def":
                        Near(-.25, sim.ModifierValue(target, Stat.DefensePercent), "Enemy defense debuff");
                        Near(-.1, sim.ModifierValue(target, Stat.AttackPercent), "Enemy attack debuff");
                        Near(-.5, sim.ModifierValue(target, Stat.Healing), "Enemy healing debuff"); break;
                    case "weaken_slow": True(target.frozenUntil > sim.Time, "Enemy freezes target"); break;
                    case "control_retreat":
                        Equal(0, sim.BlockCapacity(target), "Retreat removes one block capacity");
                        Near(998920, target.hp, "Current attack receives +80%");
                        Near(0, sim.ModifierValue(enemy, Stat.AttackPercent), "Current-attack bonus does not persist"); break;
                    case "control_frozen":
                        Equal(0, sim.BlockCapacity(target), "Area retreat removes block capacity");
                        Near(.2, sim.ModifierValue(target, Stat.Vulnerability), "Retreating target becomes vulnerable"); break;
                    case "counter_shield":
                        Near(enemyBefore + 300, enemy.hp, "Shield enemy heals 300"); Near(1000, enemy.shield, "Shield equals 10% of 10000 HP");
                        sim.DamageDirect(enemy, 1100, target);
                        Near(1000000, target.hp, "Shield counter cannot hit on its creation frame");
                        CombatSimulationTests.Frames(sim, 1);
                        Near(999100, target.hp, "Shield-breaking hit still reflects current defense once"); break;
                    case "counter_store":
                        True(enemy.stopAttacking && enemy.stopMoving, "Enemy storage pauses attacks and movement");
                        sim.DamageDirect(enemy, 200); sim.Burn(enemy, 1, 1);
                        CombatSimulationTests.Frames(sim, 76);
                        Near(1000000, target.hp, "Storage release frame still waits one frame for impact");
                        CombatSimulationTests.Frames(sim, 1);
                        Near(999820, target.hp, "Three-second release includes direct plus DOT damage times 60%");
                        True(!enemy.stopAttacking && !enemy.stopMoving && !enemy.skillActive, "Enemy storage releases and restores activity"); break;
                    case "pursuit_strike": Near(999010, target.hp, "Enemy pursuit causes 150% of 660 attack"); break;
                    case "pursuit_support":
                        Near(999160, target.hp, "Enemy pursuit causes 120% of 700 attack");
                        Near(enemyBefore + 168, enemy.hp, "Lifesteal heals 20% of actual impact damage"); break;
                    case "heal_aura": Near(enemyBefore + 540, enemy.hp, "Enemy area heal is 150% of 360 attack"); break;
                    case "heal_support":
                        Near(enemyBefore + 312, enemy.hp, "Enemy heal snapshots 80% of original 390 attack");
                        Near(.1, sim.ModifierValue(enemy, Stat.AttackPercent), "Enemy support attack buff");
                        Near(.1, sim.ModifierValue(enemy, Stat.AttackSpeed), "Enemy support speed buff");
                        Near(.05, sim.ModifierValue(enemy, Stat.Shelter), "Enemy support shelter buff"); break;
                    default: throw new Exception("No behavioral test for authored enemy skill: " + definition.skill.id);
                }
            });
    }

    internal static void RunAll()
    {
        TowerSkills();
        EnemySkills();
    }
}
