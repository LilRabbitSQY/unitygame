# 战斗模块验证

## 离线检查

在项目根目录执行：

```bash
python3 Tools/verify_battle.py
```

也可分别执行 `--mode compile` 或 `--mode logic`，用 `--output /tmp/battle-check` 指定报告目录。
工具从 `ProjectVersion.txt` 获取 Unity 版本，默认使用 macOS Unity Hub 安装位置；其他安装位置可传 `--unity /path/to/Unity`。

编译检查依赖已有的 `Library` 导入缓存和 Unity 自带的 .NET SDK，不下载依赖、不修改游戏资源，编译结果与日志写入临时目录。
工具重新枚举当前脚本，并根据**当前 asmdef 声明**解析项目及包引用，避免沿用旧 Bee 引用而隐藏程序集配置错误。
引擎 define、分析器和其他基础依赖仍来自最近一次 Unity 导入缓存，因此包配置变化之后仍需要在有效许可的 Unity 中重新导入。

纯逻辑测试位于 `Tests/Offline/`，通过 `sources.json` 直接编译当前 `CombatMath`、`CombatModel`、`BattleFactory`、`BattleSimulation`、`TowerSkills`、`EnemySkills` 生产源码。
测试运行在 Unity 附带的 .NET 中，**不加载 UnityEngine，不使用替代实现**；`--mode logic` 不需要 Library 导入缓存。

覆盖内容：

- 飞书新版伤害完整示例 1497、负伤害才触发 5% 保底、零伤害保留零、同类加算/异类乘算、30帧攻速及浮点边界。
- 直接读取 `Docs/Battle/Source/04-towers.json`、`05-enemies.json` 作为独立预期，逐项核对 16 塔和 16 敌的数据与技能参数。
- 部署地形/占位/方向/费用/上限，精确升级费用及属性，倒地冷却和再部署，暂停及 2 倍速。
- 固定时间步进、绝对波次时间轴、保护点失败、清空全部敌人后胜利、护盾和临时加成、持续阻挡、攻击快照和命中时防御。
- 自选 8 种棋子与剩余 8 种敌人身份互斥，非法选择拒绝，全部 16 种敌人可轮换出场；同格阻挡选敌、冰冻防御一致性、金币仅计击杀。
- 32 个技能的实际效果，范围灼烧、冻后减速、伙伴强化、蓄伤/不死/反击、追击不递归、毒目标条件、光环清理等联动。
- 真实关卡资源、种子 1–20 的合法两棋子通关；撤回/倒地取消技能不会触发结束收益。
- SP 和 COST 的准确 30 帧边界、外部 SP 返还、毒弹速和发射快照、真实盾反及直接伤害的两阶段取整。

旧 Demo 的 `DamageCalculator` 与旧版“最低 1 点 / 始终 5% 保底”断言已移除，不作为新版战斗证据。

测试日志与 `summary.json` 会明确标注离线范围。**离线通过不代表 Unity 导入、场景或玩家构建通过。**

## Unity 集成测试

`Assets/_Game/Tests/EditMode/` 与 `Assets/_Game/Tests/PlayMode/` 已建立独立 TestAssemblies asmdef，普通玩家构建不会包含测试。
离线 `--mode compile` / 默认 `all` 会编译这两个测试程序集，但**不执行 Unity 测试**；新测试还未导入 Unity 时，编译使用原项目的引擎依赖模板并替换为测试 asmdef 声明的依赖。

- EditMode（2 项）：Unity 原生 Resources/JsonUtility 加载全部数据；构建列表包含 Battle，场景只有一个 bootstrap 且没有丢失脚本。
- PlayMode（4 项功能测试 + 1 项截图测试）：真实 Battle 场景生成 Canvas/Input/16 张卡及 HUD 几何；开始/暂停/菜单控制真实模拟；Unity 拖拽事件经卡片、屏幕坐标和棋盘布局完成部署；选阵容遮罩拒绝 7 人并用 8 人互斥阵容重新加载场景；截图测试覆盖初始、部署、翻页后长详情、阵容、结算和 15 秒战斗。
- 按钮测试通过 EventSystem 射线确认屏幕位置最上层确实命中目标，再发送 PointerClick，避免直接调用 onClick 掩盖遮挡。截图使用 Unity 的 ScreenCapture，不重绘界面。
- 这套测试需要有效 Unity Editor 许可。即使通过，仍需人工检查视觉遮挡、动画表现、字体和布局美观。

批处理命令示例（先替换编辑器路径；不加 `-quit`，让 Test Runner 自行退出）：

```bash
"/Applications/Unity/Hub/Editor/6000.5.1f1/Unity.app/Contents/MacOS/Unity" \
  -batchmode -nographics -projectPath "$PWD" \
  -runTests -testPlatform EditMode \
  -testResults /tmp/battle-editmode.xml -logFile /tmp/battle-editmode.log
```

将 `-testPlatform EditMode` 改为 `-testPlatform PlayMode` 并更换输出文件名，可执行真实场景测试。截图测试在没有环境变量时自动跳过；要执行全部 5 项 PlayMode 测试，必须启用图形模式（不使用 `-batchmode` 或 `-nographics`）：

```bash
FINAL_DEFENSE_CAPTURE_DIR=/tmp/battle-captures \
"/Applications/Unity/Hub/Editor/6000.5.1f1/Unity.app/Contents/MacOS/Unity" \
  -projectPath "$PWD" -runTests -testPlatform PlayMode \
  -testFilter FinalDefense.Tests \
  -testResults /tmp/battle-playmode.xml -logFile /tmp/battle-playmode.log
```

Unity 6000.5.1 的 Metal 截图会在每次加载场景后的首次捕获输出两条 `memoryless` 警告；测试仅用 `LogAssert.Expect` 声明这两条精确消息和自身截图记录，其他日志仍正常检查。

2026-09-15 已在独立项目副本中使用本机合法 Unity 许可实际运行测试。最新通过计数、原始 XML、源码哈希和真实渲染 PNG 见 `Docs/Battle/Verification/`。没有进行玩家构建，也未验证全游戏流程、所有分辨率或操作系统；自动指针事件不代表真实硬件输入兼容性测试。
