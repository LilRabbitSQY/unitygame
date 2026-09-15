using System;
using System.Collections.Generic;

namespace FinalDefense.Combat
{
    [Serializable] public class Cell { public int x, y; public Cell() {} public Cell(int x, int y) { this.x=x; this.y=y; } }
    [Serializable] public class SkillDefinition
    {
        public string id, name, description;
        public float initialSp, spCost, duration, interval;
    }
    [Serializable] public class UnitDefinition
    {
        public string id, name, description, tag;
        public bool highGround, healing, healAll, projectile, radial, attackWhenBlocked = true;
        public int targets = 1, block = 1, attackFrames = 30, rangeWidth = 1, rangeDepth = 1, deployCost = 10;
        public int[] hp, attack, defense, upgradeCost;
        public float moveSpeed = 1, projectileSpeed = 8;
        public int goalDamage = 1, costReward = 1, goldReward = 1;
        public SkillDefinition skill;
        public int AtLevel(int[] values, int level) => values == null || values.Length == 0 ? 0 : values[Math.Min(values.Length-1, Math.Max(0, level-1))];
    }
    [Serializable] public class SpawnGroup
    {
        public int wave, count;
        public string enemyId;
        public float waveStart, delay, interval;
        public float statMultiplier = 1;
        public int goalDamage;
    }
    [Serializable] public class BattleConfiguration
    {
        public string title, source, notes;
        public int protection = 10, initialCost = 20, maxCost = 99, maxDeployed = 10;
        public float costPerSecond = 1, redeploySeconds = 15;
        public Cell[] path, ground, highGround;
        public UnitDefinition[] towers, enemies;
        public SpawnGroup[] groups;
        public string[] battleItems;
    }
    [Serializable] public class UnitCatalog { public UnitDefinition[] units; }
    public enum Stat
    {
        AttackPercent, AttackFlat, DamageBonus, Vulnerability, DamageTaken,
        Reduction, Shelter, DefensePercent, DefenseFlat, CritChance, CritDamage,
        AttackSpeed, Healing, MoveSpeed, MaxHP, BlockReduction
    }
    public sealed class Modifier
    {
        public string source;
        public Stat stat;
        public float value, expires;
    }
    public sealed class BurnStack { public float expires; }
    public sealed class UnitState
    {
        public int id, level = 1;
        public UnitDefinition definition;
        public bool enemy, alive = true, downed;
        public float x, y, facingX = 1, facingY, hp, shield, progress, scale = 1;
        public float attackTimer, skillTimer, sp, skillRemaining, reviveAt, poisonUntil, frozenUntil, retreatUntil;
        internal double preciseSp;
        internal float publishedSp;
        public float dotTimer, recordedDamage, pursuitSpeed, lastHitAt, lastShieldBeforeHit;
        public bool skillActive, immortality, stopAttacking, stopMoving;
        public UnitState blocker;
        public readonly List<Modifier> modifiers = new List<Modifier>();
        public readonly List<BurnStack> burns = new List<BurnStack>();
        public int TotalAttacks, TotalSkills;
        public float TotalDamage, TotalHealing;
        public string SkillId => definition.skill == null ? "" : definition.skill.id;
        public string Tag => definition.tag;
    }
    public sealed class ProjectileState
    {
        public UnitState source, target;
        public float x, y, speed, delay;
        public int eligibleFrame;
        public DamagePacket damage;
        public bool healing;
        public Action<int> onHit;
        public Action effect;
    }
    public sealed class PoisonTile
    {
        public int x, y;
        public float expires, damage = 200, slow = -.2f;
        public UnitState source;
        public readonly Dictionary<int, float> nextHit = new Dictionary<int, float>();
    }
    public sealed class BattleReport
    {
        public bool won;
        public int spawned, killed, leaked, protectionLost, costSpent, costEarned, goldEarned, upgrades, deployed;
        public int bonusGpa;
        public float duration, damage, healing;
    }
    public sealed class BattleEvent
    {
        public string kind, text;
        public float time, x, y;
        public int unitId, amount;
    }
}
