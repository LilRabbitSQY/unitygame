using UnityEngine;

namespace FinalDefense.Data
{
    public enum EnemyType
    {
        Walker,     // 行走型 - 基础地面
        Flyer,      // 飞行型 - 空中单位
        Heavy,      // 重装型 - 高HP高DEF
        Rusher,     // 突袭型 - 极高移速
        Ranger,     // 远程型 - 移动中攻击塔台
        Swarm,      // 群体型 - 大量低属性
        Stealth,    // 隐身型 - 对特定塔台隐身
        Shielded,   // 护盾型 - 额外护盾值
        Splitter,   // 分裂型 - 击杀后分裂
        HealerMob,  // 治疗型 - 恢复周围怪物HP
        BossPhased, // 阶段型Boss
        BossSummoner // 召唤型Boss
    }

    public enum MoveType
    {
        Ground, // 地面
        Air     // 空中
    }

    [CreateAssetMenu(menuName = "FinalDefense/Enemy Data")]
    public class EnemyData : ScriptableObject
    {
        [Header("基础信息")]
        public string enemyName;
        public EnemyType enemyType = EnemyType.Walker;
        public MoveType moveType = MoveType.Ground;

        [Header("属性")]
        public int maxHP = 20;
        public int defense = 0;
        public float moveSpeed = 2f;
        public int attackDamage = 5;
        public float attackSpeed = 1.5f;
        public float attackRange = 1.2f;

        [Header("奖励")]
        public int gpaDamage = 2;
        public int goldReward = 10;
        public int costReward = 2;

        [Header("特殊属性")]
        public int shieldHP = 0;
        public int splitCount = 0;
        public EnemyData splitInto;
        public float healAmount = 0;
        public float healRange = 2f;
        public float stealthDuration = 3f;
        public bool canAttackWhileMoving = false;

        [Header("Boss属性")]
        public int bossPhaseCount = 1;
        public float[] phaseHPThresholds;
        public float bossRageMultiplier = 1.5f;

        [Header("视觉")]
        public GameObject prefab;
        public Color enemyColor = Color.red;
    }
}
