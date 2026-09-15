using UnityEngine;
using UnityEditor;
using FinalDefense.Data;

namespace FinalDefense.Editor
{
    public static class GameDataGenerator
    {
        [MenuItem("FinalDefense/Legacy Demo/生成塔台数据")]
        public static void GenerateTowerData()
        {
            string path = "Assets/_Game/ScriptableObjects/LegacyTowers/";
            if (!AssetDatabase.IsValidFolder(path.TrimEnd('/')))
                AssetDatabase.CreateFolder("Assets/_Game/ScriptableObjects", "LegacyTowers");

            CreateTower(path, "重装型-铁壁", TowerType.Heavy, DeployPosition.Ground,
                maxHP: 500, damage: 15, defense: 20, attackRange: 1.5f, attackInterval: 2f, deployCost: 8, blockCount: 3,
                color: new Color(0.3f, 0.4f, 0.8f));

            CreateTower(path, "先锋型-侦察兵", TowerType.Vanguard, DeployPosition.Ground,
                maxHP: 200, damage: 20, defense: 5, attackRange: 1.5f, attackInterval: 1.2f, deployCost: 3, blockCount: 1,
                color: new Color(0.9f, 0.7f, 0.2f));

            CreateTower(path, "突击型-利刃", TowerType.Striker, DeployPosition.Ground,
                maxHP: 180, damage: 45, defense: 5, attackRange: 1.5f, attackInterval: 0.8f, deployCost: 7, blockCount: 1,
                color: new Color(0.9f, 0.2f, 0.2f));

            CreateTower(path, "术师型-雷暴", TowerType.Caster, DeployPosition.HighGround,
                maxHP: 120, damage: 35, defense: 3, attackRange: 3.5f, attackInterval: 2.5f, deployCost: 9, blockCount: 0,
                color: new Color(0.6f, 0.2f, 0.9f));

            CreateTower(path, "狙击型-鹰眼", TowerType.Sniper, DeployPosition.HighGround,
                maxHP: 100, damage: 50, defense: 2, attackRange: 5f, attackInterval: 2f, deployCost: 8, blockCount: 0,
                color: new Color(0.2f, 0.8f, 0.2f));

            CreateTower(path, "陷阱型-荆棘", TowerType.Trapper, DeployPosition.Ground,
                maxHP: 80, damage: 10, defense: 0, attackRange: 2f, attackInterval: 1f, deployCost: 4, blockCount: 0,
                color: new Color(0.5f, 0.3f, 0.1f));

            CreateTower(path, "治疗型-春风", TowerType.Healer, DeployPosition.HighGround,
                maxHP: 150, damage: 0, defense: 5, attackRange: 3f, attackInterval: 2f, deployCost: 7, blockCount: 0,
                color: new Color(0.2f, 0.9f, 0.6f));

            CreateTower(path, "辅助型-军师", TowerType.Support, DeployPosition.HighGround,
                maxHP: 130, damage: 5, defense: 3, attackRange: 3.5f, attackInterval: 3f, deployCost: 6, blockCount: 0,
                color: new Color(0.9f, 0.9f, 0.2f));

            CreateTower(path, "换位型-时空", TowerType.Shifter, DeployPosition.Both,
                maxHP: 160, damage: 15, defense: 8, attackRange: 2f, attackInterval: 1.5f, deployCost: 5, blockCount: 1,
                color: new Color(0.8f, 0.5f, 0.9f));

            CreateTower(path, "召唤型-统帅", TowerType.Summoner, DeployPosition.Ground,
                maxHP: 140, damage: 10, defense: 5, attackRange: 2.5f, attackInterval: 2f, deployCost: 6, blockCount: 1,
                color: new Color(0.4f, 0.9f, 0.4f));

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("已生成10种塔台数据");
        }

        [MenuItem("FinalDefense/Legacy Demo/生成怪物数据")]
        public static void GenerateEnemyData()
        {
            string path = "Assets/_Game/ScriptableObjects/LegacyEnemies/";
            if (!AssetDatabase.IsValidFolder(path.TrimEnd('/')))
                AssetDatabase.CreateFolder("Assets/_Game/ScriptableObjects", "LegacyEnemies");

            CreateEnemy(path, "行走型-选择题怪", EnemyType.Walker, MoveType.Ground,
                maxHP: 30, defense: 2, moveSpeed: 2f, attackDamage: 5, gpaDamage: 2, costReward: 2,
                color: new Color(0.9f, 0.3f, 0.3f));

            CreateEnemy(path, "飞行型-概念题怪", EnemyType.Flyer, MoveType.Air,
                maxHP: 20, defense: 0, moveSpeed: 2.5f, attackDamage: 3, gpaDamage: 3, costReward: 3,
                color: new Color(0.7f, 0.7f, 1f));

            CreateEnemy(path, "重装型-论文怪", EnemyType.Heavy, MoveType.Ground,
                maxHP: 100, defense: 15, moveSpeed: 1f, attackDamage: 10, gpaDamage: 5, costReward: 5,
                color: new Color(0.4f, 0.2f, 0.2f));

            CreateEnemy(path, "突袭型-DDL怪", EnemyType.Rusher, MoveType.Ground,
                maxHP: 15, defense: 0, moveSpeed: 5f, attackDamage: 2, gpaDamage: 4, costReward: 2,
                color: new Color(1f, 0.5f, 0f));

            CreateEnemy(path, "远程型-提问怪", EnemyType.Ranger, MoveType.Ground,
                maxHP: 25, defense: 3, moveSpeed: 1.5f, attackDamage: 12, gpaDamage: 2, costReward: 3,
                color: new Color(0.8f, 0.2f, 0.6f), canAttackWhileMoving: true);

            CreateEnemy(path, "群体型-作业怪", EnemyType.Swarm, MoveType.Ground,
                maxHP: 8, defense: 0, moveSpeed: 2.5f, attackDamage: 2, gpaDamage: 1, costReward: 1,
                color: new Color(0.6f, 0.6f, 0.3f));

            CreateEnemy(path, "隐身型-挂科怪", EnemyType.Stealth, MoveType.Ground,
                maxHP: 20, defense: 2, moveSpeed: 2f, attackDamage: 5, gpaDamage: 4, costReward: 4,
                color: new Color(0.3f, 0.3f, 0.3f), stealthDuration: 5f);

            CreateEnemy(path, "护盾型-论辩怪", EnemyType.Shielded, MoveType.Ground,
                maxHP: 40, defense: 5, moveSpeed: 1.8f, attackDamage: 7, gpaDamage: 3, costReward: 4,
                color: new Color(0.2f, 0.5f, 0.9f), shieldHP: 30);

            CreateEnemy(path, "分裂型-Bug怪", EnemyType.Splitter, MoveType.Ground,
                maxHP: 35, defense: 3, moveSpeed: 1.8f, attackDamage: 5, gpaDamage: 2, costReward: 3,
                color: new Color(0.5f, 0.9f, 0.2f), splitCount: 3);

            CreateEnemy(path, "治疗型-抄袭怪", EnemyType.HealerMob, MoveType.Ground,
                maxHP: 25, defense: 5, moveSpeed: 1.5f, attackDamage: 0, gpaDamage: 2, costReward: 4,
                color: new Color(0.2f, 0.8f, 0.5f), healAmount: 5, healRange: 3f);

            CreateEnemy(path, "Boss-期末考试", EnemyType.BossPhased, MoveType.Ground,
                maxHP: 500, defense: 20, moveSpeed: 0.8f, attackDamage: 25, gpaDamage: 10, costReward: 15,
                color: new Color(0.1f, 0.1f, 0.1f), bossPhaseCount: 3);

            CreateEnemy(path, "Boss-毕业答辩", EnemyType.BossSummoner, MoveType.Ground,
                maxHP: 400, defense: 15, moveSpeed: 1f, attackDamage: 20, gpaDamage: 8, costReward: 12,
                color: new Color(0.5f, 0f, 0.5f), bossPhaseCount: 2);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("已生成12种怪物数据");
        }

        [MenuItem("FinalDefense/生成NPC数据")]
        public static void GenerateNPCData()
        {
            string path = "Assets/_Game/ScriptableObjects/NPCs/";
            if (!AssetDatabase.IsValidFolder("Assets/_Game/ScriptableObjects/NPCs"))
                AssetDatabase.CreateFolder("Assets/_Game/ScriptableObjects", "NPCs");

            var content = JsonUtility.FromJson<FinalDefense.Campaign.CampaignContent>(Resources.Load<TextAsset>("Campaign/Content").text);
            foreach (var npc in content.npcs)
                CreateNPC(path, (NPCId)npc.legacyId, npc.name, npc.legacyId < 2 || npc.legacyId == 7 ? "女" : "男", npc.persona,
                    (NPCBattleInfluence)npc.legacyId, npc.style, npc.initialFavor, npc.initialGpa / 100, 0, 0);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("已生成8个NPC数据");
        }

        private static void CreateTower(string path, string name, TowerType type, DeployPosition pos,
            int maxHP, int damage, int defense, float attackRange, float attackInterval, int deployCost, int blockCount, Color color)
        {
            var data = ScriptableObject.CreateInstance<TowerData>();
            data.towerName = name;
            data.towerType = type;
            data.deployPosition = pos;
            data.maxHP = maxHP;
            data.damage = damage;
            data.defense = defense;
            data.attackRange = attackRange;
            data.attackInterval = attackInterval;
            data.deployCost = deployCost;
            data.blockCount = blockCount;
            data.towerColor = color;
            data.attackSpeed = 1f;
            data.redeployCost = deployCost / 2 + 1;
            data.redeployCooldown = 15f;
            data.maxLevel = 3;
            data.upgradeHPMultiplier = 1.5f;
            data.upgradeDamageMultiplier = 1.5f;
            data.upgradeDefenseMultiplier = 1.3f;

            string assetName = name.Replace("-", "_").Replace("/", "_");
            AssetDatabase.CreateAsset(data, path + assetName + ".asset");
        }

        private static void CreateEnemy(string path, string name, EnemyType type, MoveType moveType,
            int maxHP, int defense, float moveSpeed, int attackDamage, int gpaDamage, int costReward, Color color,
            bool canAttackWhileMoving = false, float stealthDuration = 3f, int shieldHP = 0,
            int splitCount = 0, float healAmount = 0, float healRange = 2f, int bossPhaseCount = 1)
        {
            var data = ScriptableObject.CreateInstance<EnemyData>();
            data.enemyName = name;
            data.enemyType = type;
            data.moveType = moveType;
            data.maxHP = maxHP;
            data.defense = defense;
            data.moveSpeed = moveSpeed;
            data.attackDamage = attackDamage;
            data.gpaDamage = gpaDamage;
            data.costReward = costReward;
            data.goldReward = costReward * 5;
            data.attackSpeed = 1.5f;
            data.attackRange = 1.2f;
            data.enemyColor = color;
            data.canAttackWhileMoving = canAttackWhileMoving;
            data.stealthDuration = stealthDuration;
            data.shieldHP = shieldHP;
            data.splitCount = splitCount;
            data.healAmount = healAmount;
            data.healRange = healRange;
            data.bossPhaseCount = bossPhaseCount;

            string assetName = name.Replace("-", "_").Replace("/", "_");
            AssetDatabase.CreateAsset(data, path + assetName + ".asset");
        }

        private static void CreateNPC(string path, NPCId id, string name, string gender, string keywords,
            NPCBattleInfluence influence, string battleStyle, int favor, int merit, int patience, int academic)
        {
            string assetPath = path + id + ".asset";
            var data = AssetDatabase.LoadAssetAtPath<NPCData>(assetPath);
            bool create = data == null;
            if (create) data = ScriptableObject.CreateInstance<NPCData>();
            data.npcId = id;
            data.npcName = name;
            data.gender = gender;
            data.personalityKeywords = keywords;
            data.battleInfluence = influence;
            data.battleStyle = battleStyle;
            data.initialFavorability = favor;
            data.initialMeritScore = merit;
            data.initialPatience = patience;
            data.initialAcademicPower = academic;

            if (create) AssetDatabase.CreateAsset(data, assetPath); else EditorUtility.SetDirty(data);
        }
    }
}
