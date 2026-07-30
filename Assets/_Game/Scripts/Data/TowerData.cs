using UnityEngine;

namespace FinalDefense.Data
{
    public enum TowerType
    {
        Heavy,      // 重装型 - 地面坦克
        Vanguard,   // 先锋型 - 低费回费
        Striker,    // 突击型 - 近战爆发
        Caster,     // 术师型 - 高台AoE
        Sniper,     // 狙击型 - 高台远程单体
        Trapper,    // 陷阱型 - 地面区域控制
        Healer,     // 治疗型 - 恢复友方HP
        Support,    // 辅助型 - buff/debuff
        Shifter,    // 换位型 - 位置交换
        Summoner    // 召唤型 - 召唤临时单位
    }

    public enum DeployPosition
    {
        Ground,     // 地面位
        HighGround, // 高台位
        Both        // 均可（换位型）
    }

    [CreateAssetMenu(menuName = "FinalDefense/Tower Data")]
    public class TowerData : ScriptableObject
    {
        [Header("基础信息")]
        public string towerName;
        public TowerType towerType;
        public DeployPosition deployPosition = DeployPosition.Ground;

        [Header("费用")]
        public int cost;
        public int deployCost = 5;
        public int redeployCost = 3;
        public float redeployCooldown = 15f;

        [Header("战斗属性")]
        public int maxHP = 200;
        public int damage = 10;
        public int defense = 5;
        public float attackRange = 2.5f;
        public float attackInterval = 1f;
        public float attackSpeed = 1f;
        public int blockCount = 1;

        [Header("升级")]
        public int maxLevel = 3;
        public float upgradeHPMultiplier = 1.5f;
        public float upgradeDamageMultiplier = 1.5f;
        public float upgradeDefenseMultiplier = 1.3f;

        [Header("视觉")]
        public GameObject prefab;
        public GameObject projectilePrefab;
        public Sprite icon;
        public Color towerColor = Color.green;

        [Header("技能")]
        public TowerSkillData[] skills;
    }

    [System.Serializable]
    public class TowerSkillData
    {
        public string skillName;
        public int spCost = 10;
        public float cooldown = 5f;
        public float duration = 3f;
        public float range = 3f;
        public int value = 50;
        public SkillEffectType effectType;
    }

    public enum SkillEffectType
    {
        AreaDamage,         // 范围伤害
        SingleDamage,       // 单体高伤
        Heal,               // 治疗
        HealArea,           // 群体治疗
        Shield,             // 护盾
        Taunt,              // 嘲讽
        SpeedBuff,          // 攻速增益
        DefenseBuff,        // 防御增益
        AttackBuff,         // 攻击增益
        SlowField,          // 减速力场
        WeakenField,        // 弱化光环
        CostRecovery,       // 费用回复
        Stun,               // 眩晕
        Summon,             // 召唤
        Swap,               // 换位
        PoisonField,        // 毒雾区域
        PierceShot,         // 穿透射击
        MultiStrike         // 连击
    }
}
