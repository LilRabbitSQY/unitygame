using System;
using System.Collections.Generic;

namespace FinalDefense.Combat
{
    public partial class BattleSimulation
    {
        private readonly Dictionary<int, float> enemyStoreEnds = new Dictionary<int, float>();
        private readonly HashSet<int> enemyCounterShields = new HashSet<int>();

        // The authored "attack trigger" constrains basic attacks. Skills whose
        // target is the blocker need a blocker; area and self skills run on time.
        private bool CanExecuteEnemySkill(UnitState u)
        {
            if (u == null || !u.alive || u.downed || u.definition.skill == null) return false;
            switch (u.SkillId)
            {
                case "direct_crit":
                case "direct_speed":
                case "burn_stack":
                case "poison_spread":
                case "control_retreat":
                    return EnemyBlocker(u) != null;
                case "counter_shield":
                case "heal_aura":
                case "heal_support":
                    return true;
                case "counter_store":
                    return !enemyStoreEnds.ContainsKey(u.id);
                case "pursuit_strike":
                case "pursuit_support":
                    return EnemyPursuitTargets(u).Count > 0;
                case "burn_burst":
                case "poison_amp":
                case "weaken_def":
                case "weaken_slow":
                case "control_frozen":
                    return EnemySkillArea(u).Count > 0;
                default:
                    return false;
            }
        }

        private void ExecuteEnemySkill(UnitState u)
        {
            if (!CanExecuteEnemySkill(u)) return;
            UnitState blocker = EnemyBlocker(u);
            string source = "enemy-skill:" + u.id;
            switch (u.SkillId)
            {
                case "direct_crit":
                    if (blocker == null) return;
                    AttackTargets(u, new List<UnitState> { blocker }, 1.8f);
                    break;
                case "direct_speed":
                    if (blocker == null) return;
                    AttackTargets(u, new List<UnitState> { blocker }, 1.5f, false, 0,
                        (damage, target) => Heal(u, 100));
                    break;
                case "burn_stack":
                    if (blocker == null) return;
                    QueueEffect(u, blocker, () => Burn(blocker, 2, 10));
                    break;
                case "burn_burst":
                    foreach (UnitState target in EnemySkillArea(u))
                    {
                        QueueEffect(u, target, () =>
                        {
                            Burn(target, 1, 10);
                            int stacks = 0;
                            foreach (BurnStack stack in target.burns)
                                if (stack.expires > Time) stacks++;
                            DamageDirect(target, stacks * 100, u);
                        });
                    }
                    break;
                case "poison_spread":
                    if (blocker == null) return;
                    QueueEffect(u, blocker, () =>
                    {
                        Poison(blocker, 5);
                        Buff(blocker, source, Stat.DefensePercent, -.1f, 5);
                    });
                    break;
                case "poison_amp":
                    foreach (UnitState target in EnemySkillArea(u))
                    {
                        QueueEffect(u, target, () =>
                        {
                            Poison(target, 5);
                            Buff(target, source, Stat.DefenseFlat, -50, 5);
                        });
                    }
                    break;
                case "weaken_def":
                    foreach (UnitState target in EnemySkillArea(u))
                    {
                        QueueEffect(u, target, () =>
                        {
                            Buff(target, source, Stat.DefensePercent, -.25f, 3);
                            Buff(target, source, Stat.AttackPercent, -.1f, 3);
                            Buff(target, source, Stat.Healing, -.5f, 3);
                        });
                    }
                    break;
                case "weaken_slow":
                    foreach (UnitState target in EnemySkillArea(u))
                        QueueEffect(u, target, () => Freeze(target, 1));
                    break;
                case "control_retreat":
                    if (blocker == null) return;
                    // Retain the target before its block count changes. The
                    // 80% boost belongs only to this attack, never the next one.
                    string attackSource = source + ":one-attack";
                    Buff(u, attackSource, Stat.AttackPercent, .8f, 1);
                    try
                    {
                        AttackTargets(u, new List<UnitState> { blocker }, 1, false, 0,
                            (damage, target) => Retreat(target, 2));
                    }
                    finally { u.modifiers.RemoveAll(modifier => modifier.source == attackSource); }
                    break;
                case "control_frozen":
                    foreach (UnitState target in EnemySkillArea(u))
                    {
                        QueueEffect(u, target, () =>
                        {
                            Retreat(target, 2);
                            Buff(target, source, Stat.Vulnerability, .2f, 2);
                        });
                    }
                    break;
                case "counter_shield":
                    Heal(u, 300);
                    AddShield(u, MaxHP(u) * .1f);
                    enemyCounterShields.Add(u.id);
                    break;
                case "counter_store":
                    if (enemyStoreEnds.ContainsKey(u.id)) return;
                    u.skillActive = true;
                    u.skillRemaining = 3;
                    u.stopAttacking = true;
                    u.stopMoving = true;
                    u.recordedDamage = 0;
                    enemyStoreEnds[u.id] = Time + 3;
                    Buff(u, source, Stat.DefensePercent, .2f, 3);
                    Heal(u, 1000);
                    break;
                case "pursuit_strike":
                    AttackTargets(u, EnemyPursuitTargets(u), 1.5f, true, 0,
                        (damage, target) =>
                        {
                            if (!target.alive || target.downed || Random.NextDouble() >= .1) return;
                            if (Random.NextDouble() < .5) Burn(target, 1, 2);
                            else Freeze(target, 2);
                        });
                    break;
                case "pursuit_support":
                    AttackTargets(u, EnemyPursuitTargets(u), 1.2f, true, 0,
                        (damage, target) => { if (u.alive && !u.downed) Heal(u, damage * .2f); });
                    break;
                case "heal_aura":
                    float auraHealing = AttackValue(u) * 1.5f;
                    foreach (UnitState target in EnemySkillArea(u, true))
                        QueueEffect(u, target, () => Heal(target, auraHealing));
                    break;
                case "heal_support":
                    // Snapshot attack before this cast buffs the caster itself.
                    float supportHealing = AttackValue(u) * .8f;
                    foreach (UnitState target in EnemySkillArea(u, true))
                    {
                        QueueEffect(u, target, () =>
                        {
                            Heal(target, supportHealing);
                            Buff(target, source, Stat.AttackPercent, .1f, 5);
                            Buff(target, source, Stat.AttackSpeed, .1f, 5);
                            Buff(target, source, Stat.Shelter, .05f, 5);
                        });
                    }
                    break;
                default:
                    return;
            }
            Emit("enemy-skill", u, u.definition.skill.name);
        }

        private void TickEnemySkill(UnitState u, float dt)
        {
            if (u == null || !enemyStoreEnds.TryGetValue(u.id, out float end)) return;
            u.skillRemaining = Math.Max(0, end - Time);
            if (Time + .0001f < end) return;

            enemyStoreEnds.Remove(u.id);
            u.skillActive = false;
            u.stopAttacking = false;
            u.stopMoving = false;
            float burst = u.recordedDamage * .6f;
            u.recordedDamage = 0;
            if (!u.alive || u.downed) return;
            foreach (UnitState target in EnemySkillArea(u))
                QueueEffect(u, target, () => DamageDirect(target, burst, u, true));
            Emit("counter-release", u, "续命一口 · 蓄伤释放", (int)Math.Round(burst));
        }

        private void RecordEnemyDamage(UnitState u, int damage)
        {
            if (u == null) return;
            if (enemyStoreEnds.TryGetValue(u.id, out float end) && Time <= end)
                u.recordedDamage += Math.Max(0, damage);
        }

        private void OnEnemyDamaged(UnitState u, UnitState attacker, int damage)
        {
            if (u == null) return;
            // Use the shield snapshot: the hit that breaks the shield still
            // occurred while shielded. reflect=true prevents counter recursion.
            if (u.SkillId == "counter_shield" && enemyCounterShields.Contains(u.id)
                && u.lastShieldBeforeHit > 0 && attacker != null && !attacker.enemy
                && attacker.alive && !attacker.downed)
            {
                float reflected = DefenseValue(u);
                QueueEffect(u, attacker, () => DamageDirect(attacker, reflected, u, true));
            }
        }

        private UnitState EnemyBlocker(UnitState u)
        {
            UnitState blocker = u.blocker;
            return blocker != null && blocker.alive && !blocker.downed && !blocker.enemy ? blocker : null;
        }

        private List<UnitState> EnemySkillArea(UnitState u, bool allies = false)
        {
            var result = new List<UnitState>();
            foreach (UnitState target in allies ? Allies(u) : Foes(u))
                if (target.alive && !target.downed && InRange(u, target, 3, 3, true)) result.Add(target);
            return result;
        }

        private List<UnitState> EnemyPursuitTargets(UnitState u)
        {
            var result = new List<UnitState>();
            foreach (UnitState target in Foes(u))
                if (target.alive && !target.downed && InRange(u, target, 3, 2)) result.Add(target);
            result.Sort((a, b) =>
            {
                int distance = Distance(u, a).CompareTo(Distance(u, b));
                return distance != 0 ? distance : b.id.CompareTo(a.id);
            });
            if (result.Count > 2) result.RemoveRange(2, result.Count - 2);
            return result;
        }
    }
}
