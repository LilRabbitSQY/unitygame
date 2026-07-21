# 期末保卫战 - Unity Demo 实现计划

## Context

基于飞书文档中的策划案，为 "期末保卫战"（AI策略塔防+模拟经营）制作一个 Unity 可运行 Demo。当前项目是全新的 Unity 6 (6000.5.1f1) URP 2D 项目，无任何自定义代码。Demo 聚焦**塔防核心战斗循环**，验证玩法可行性。

---

## 一、Demo 范围

实现以下核心功能：
1. **塔防战斗场景** — 敌人沿路径行进，玩家放置"学习工具"塔攻击"考试怪物"
2. **基础数值系统** — GPA（生命值）、属性对战斗的影响
3. **完整场景流程** — 主菜单 → 战斗 → 结算
4. **占位符美术** — 彩色几何图形 Sprite

---

## 二、项目文件夹结构

```
Assets/_Game/
├── Scripts/
│   ├── Core/           # Singleton, GameManager, EventBus, SceneLoader
│   ├── Data/           # ScriptableObject 定义: TowerData, EnemyData, WaveData, PlayerStats
│   ├── Tower/          # Tower, TowerPlacement, Projectile
│   ├── Enemy/          # Enemy, EnemyHealth, EnemySpawner
│   ├── Path/           # PathManager
│   ├── Battle/         # BattleManager, GoldSystem
│   └── UI/             # MainMenuUI, BattleHUD, TowerSelectionUI, ResultScreenUI
├── ScriptableObjects/  # SO 配置实例 (Towers/, Enemies/, Waves/)
├── Prefabs/            # 预制体 (Towers/, Enemies/, Projectiles/)
├── Scenes/             # MainMenu, Battle, Result
├── Sprites/            # 占位符色块图
└── Tilemaps/           # Tilemap Palette 和 Tile 资产
```

---

## 三、核心脚本职责

| 脚本 | 职责 |
|------|------|
| Singleton\<T\> | 泛型单例基类，DontDestroyOnLoad |
| GameManager | 持有 PlayerStats，管理全局游戏状态 |
| EventBus | 静态事件：EnemyReachedEnd, EnemyKilled, WaveCompleted, BattleLost/Won, GoldChanged |
| SceneLoader | 异步场景切换封装 |
| TowerData/EnemyData/WaveData | ScriptableObject 数据定义 |
| PlayerStats | 玩家属性 SO：GPA, Grade, Emotion, Strength, EduPower, Determination |
| PathManager | 管理路径点列表，提供路径查询 |
| Enemy | 沿路径点移动，到达终点扣 GPA |
| EnemyHealth | 血量管理，死亡时发事件 |
| EnemySpawner | 按 WaveData 配置定时生成敌人 |
| Tower | 范围检测 + 定时攻击最前方敌人 |
| TowerPlacement | 鼠标交互，Tilemap 格子合法性检测，放置塔 |
| Projectile | 追踪飞向目标，命中造成伤害 |
| BattleManager | 战斗流程控制，胜负判定 |
| GoldSystem | 金币收支管理 |

---

## 四、关键系统设计

### 4.1 塔放置
- 用户点击 UI 选塔 → TowerPlacement 进入放置模式
- 鼠标位置转 Tilemap 格子坐标：`placableTilemap.WorldToCell(mouseWorldPos)`
- 用 `HasTile()` + `HashSet<Vector3Int> occupiedCells` 判定合法性
- 确认放置：扣金币 → 实例化塔到格子中心

### 4.2 敌人寻路
- 路径点系统（非 NavMesh），用 `Vector3.MoveTowards` 逐点移动
- 到达终点：触发 GPA 扣除事件，敌人自毁

### 4.3 战斗数学
- 塔伤害 = `baseDamage * (1 + eduPower * 0.01)`
- 敌人 HP = `baseHP * (1 + determination * 0.015)`
- 初始金币 = 100，击杀奖励 = EnemyData.goldReward

### 4.4 目标选择
- Physics2D.OverlapCircleAll 检测范围内敌人
- 选择路径进度最远的（CurrentWaypointIndex 最大）

---

## 五、Demo 数据配置

**3 种塔：**

| 名称 | 花费 | 伤害 | 间隔 | 范围 | 特色 |
|------|------|------|------|------|------|
| 笔记本 | 50 | 5 | 0.8s | 2.5 | 快攻 |
| 计算器 | 80 | 12 | 1.2s | 3.0 | 均衡 |
| 咖啡杯 | 120 | 8 | 1.5s | 2.0 | 减速（AOE预留） |

**3 种敌人：**

| 名称 | HP | 速度 | GPA伤害 | 金币 |
|------|-----|------|---------|------|
| 选择题小怪 | 20 | 2.5 | 2 | 10 |
| 填空题中怪 | 50 | 1.8 | 5 | 25 |
| 论述题大怪 | 120 | 1.2 | 10 | 50 |

---

## 六、实现顺序

### 阶段 1：基础框架
1. 创建文件夹结构
2. Singleton\<T\>, EventBus, GameManager, SceneLoader
3. 创建 3 个场景，配置 Build Settings

### 阶段 2：数据层
4. TowerData, EnemyData, WaveData, PlayerStats SO 脚本
5. 创建占位符 Sprite 资产（色块）
6. 创建 SO 配置实例和 Prefab

### 阶段 3：路径和敌人
7. Battle 场景 Tilemap 搭建（S 形路径）
8. PathManager + 路径节点
9. Enemy + EnemyHealth + EnemySpawner
10. 验证：敌人沿路径移动

### 阶段 4：塔和战斗
11. Tower + Projectile
12. TowerPlacement（Tilemap 格子放置）
13. GoldSystem + BattleManager
14. 验证：完整战斗循环

### 阶段 5：UI 和流程
15. MainMenuUI → BattleHUD + TowerSelectionUI → ResultScreenUI
16. 连通全流程
17. 接入属性影响（eduPower/determination）

### 阶段 6：打磨
18. 敌人血条、塔范围可视化
19. 数值平衡调整

---

## 七、验证方式

1. Unity Editor 中 Play Mode 运行 MainMenu 场景
2. 点击"开始战斗" → 进入 Battle 场景
3. 选塔 → 点击可放置格子放下塔 → 观察塔自动攻击经过的敌人
4. 敌人到达终点扣 GPA，GPA 归零判定失败
5. 所有波次清完判定胜利 → 进入 Result 场景
6. 返回主菜单，验证 GPA 数值持久

---

## 八、架构决策

- **事件驱动通信**：各系统通过 EventBus 解耦
- **ScriptableObject 配置**：数值独立于代码，方便调参
- **Tilemap 判定放置**：利用 Unity 内置系统，无需自建网格
- **占位符美术**：彩色几何 Sprite，后续可直接替换
- **不用对象池**：Demo 规模小，简化实现
