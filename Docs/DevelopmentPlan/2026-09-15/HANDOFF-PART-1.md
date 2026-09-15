> 最新验收：六NPC真实DeepSeek开场/回复6/6通过，含流式/标签/三提示/好感+2；见`Part1Evidence/live-ai-results.json`及AI-SETUP.md。历史“真实AI未接通”状态由此更新；最终Player联网和自然28天体验仍由整合验收覆盖。

> AI接入更新：用户已选择将密钥编入客户端，新增DeepSeek直连；不再需要部署网关。配置和打包步骤见[AI-SETUP.md](AI-SETUP.md)。仓库模板密钥为空；本地已按用户授权配置并完成真实调用，凭据未提交。

> 更新：用户已授权按暂定规则实现。当前默认配置已升级为 `provisional-2026-09-15.2`，未最终定稿不再阻止新游戏。本文下方历史“规则未批阻断”“概率未启用”描述由 [未定规则与暂定实现](RULE-DECISIONS-PART-1.md) 取代。真实AI服务现已配置，安装包验证由Part3串行完成。

# Part 1 交接（代码已交接，外部验收未完成）

工作目录 `/Users/bytedance/unitygame-campaign`，分支 `codex/fullgame-campaign`。
共同基线 `15fc189c`；契约首批 `4b02c674`；实现 `a646c858`、恢复与内容 `19bf07ac`、迁移预览 `83451c5b`。保留原有源码、美术、战斗数据、测试、工具及策划快照；未加入 Library/Builds、历史临时截图、用户存档及无关网站目录。未推送远端。

## 唯一状态与接口

`GameManager.Campaign` 为唯一主流程权威；`Snapshot` 是深拷贝，写入快照不改变游戏。`Campaign.Changed` 在落盘成功后发布。`OperationResult.Success` 对成功和相同请求重放均为 true，`error == Duplicate` 表示重放；相同 actionId 不同参数返回 Conflict。UI应在一次用户意图的网络/存盘重试中复用 actionId，发起新意图才生成新ID。

```csharp
var gm = GameManager.Instance;
var result = gm.StartCampaign("slot-1"); // 已存在时拒绝覆盖；用户授权的暂定规则允许新游戏
var saves = gm.ListCampaignSaves();
result = gm.ContinueCampaign("slot-1");
var campaign = gm.Campaign;
campaign.Changed += snapshot => { /* 刷新页面，勿在这里再计算数值 */ };
campaign.CompleteIntroduction(actionId);
campaign.EditAppointments(actionId, new[] {
    new Appointment { npcId = "liu_ruoshui", locationId = "library" },
    new Appointment { npcId = "wan_sirui", locationId = "cafe" },
    new Appointment { npcId = "ming_shan", locationId = "gym" }
});
campaign.ConfirmAppointments(actionId);
await gm.DialogueSession.SendAsync(turnId, "", chunk => { /* 显示流 */ }, cancel); // NPC开场
await gm.DialogueSession.SendAsync(nextTurnId, playerInput, chunk => { }, cancel);
campaign.FinishDialogue(actionId); // 每段至少1次有效回复；第三段后 Matching
// Part2读取 Snapshot.draftRequest，结果由 SaveDraft(actionId, snapshot) 验证并落盘
campaign.SaveDraft(actionId, draft);
// 参数读取Part2关卡目录；仅显式选择的物品扣一次，不能自动每种各扣1
campaign.PrepareBattle(actionId, levelId, levelVersion, chosenItemIds, initialCost, protection);
// Part2模拟接 Snapshot.battle，唯一宿主启动；退出已消费检查点须提交 Forfeit
campaign.CommitBattleOutcome(outcome); // run/battle/attempt 身份和重复结算校验
campaign.EnterShop(actionId);
campaign.Buy(actionId, itemId);
campaign.Gift(actionId, itemId, npcId);
campaign.NextDay(actionId);
```

上述每一行的 `actionId` 为各自操作的稳定ID，不能照样例将同一个值用于多个不同操作。

- GPA百分位整数：初始7000，胜200、败−300、对手反向50。保护损失不入账。推荐信仅由已携带物品判定，不信任战报任意奖励值。
- `DraftRequest.playerGpa` 补充玩家成绩，seed与已抽对手持久化。Part2负责棋子目录、合法选法与生成；本服务验证互斥、双方数量、身份、先手与seed。
- `Campaign.Content`：小写字段 `npcs/locations/news/emotionTags/worldText/endingSuccess/endingFailure`，附来源。
- `Campaign.Ranking`、`Snapshot.trends/npcs`、`Campaign.News`、`ReadNews(actionId,id)` 供信息页。
- `IUnitCatalog` 为图鉴接口，Part2提供实现，不复制技能数值。
- `BattleItemDefs.All` **19项**，`BattleItems`/`IsBattleItem`过滤12种战斗物品。新礼物字段 `favorGain/targetNpc/stackLimit/saleLimit`。其余现有ID、Name/Effect/Price保留。
- `GameManager.LoadSettings/SaveSettings`，`LoadUnlocks`，`RecoverCampaignBackup`；`StartCampaign(saveId, overwrite:true)`先保留永久archive备份，再原子替换；设置与存档独立。UI应用音量与分辨率。
- `GameManager.BeginNewGame/LoadProgress` 为兼容入口，正式UI请显示 OperationResult。`CompleteBattle(BattleReport)`（没有身份）、`RecordBattleResult(bool)`、旧属性/GPA/金币直接修改、旧随机NPC回复均停用。保留签名只为可编译。

## 持久化与恢复

保存到 `Application.persistentDataPath/CampaignV3/<saveId>.json`，临时文件写入并flush后原子replace，保留 `.backup`。主文件损坏/未知版本不静默回退；用户选“读取备份”后明确恢复。读取时校验规则和内容版本，失败不会替换当前会话。全局结局在 `unlocks.json`，可由本局终局重新修复；设置在 `settings.json`。

战斗采用准备检查点，保留相同attempt/阵容/seed/物品。读档不消费、不重抽。主动离开已开始检查点走Forfeit；程序中断继续检查点。检查点恢复不是局内全现场存档。

旧PlayerPrefs `FinalDefense.Campaign.v1` 不删、不写。`InspectLegacySave()` 先留只读副本再报告v1/v2原GPA；因旧档没有NPC/三次日程/匹配历史，尚不能无损迁移。D15未确认时不伪造迁移后的历史。`PreviewLegacyMigration(newSaveId)`可展示精确保留的GPA/天数/库存及无法恢复的字段；只有`allowLegacyRestartDayMigration`与`approved`均确认，`MigrateLegacyCampaign`才允许预约边界档导入到新槽。战斗/结算中旧档仍拒绝不安全迁移。

## 规则待定（没有把建议冒充策划）

`CampaignRules` 默认版本 `proposal-2026-09-15.1`、`approved=false`。纯逻辑测试可显式构造proposal；正式新游戏返回 RulesPending。部署需经确认的 `Resources/Campaign/Rules.json`。

提案：D02 玩家最大100，允许购物到0、<0结算失败，并列第一成功；D03第二连胜起每胜额外2、只更新对决NPC；D04赠礼共用+5，无有效增益不消耗，彩蛋门槛取结算后、每条回复最多±2；初始同分用NPC稳定枚举顺序，后续每次好感实际变化记录事件序号；D06重试默认不开放，可配置为付固定费用、同seed和补给且当天成绩不再结算；D16好感60归中档，低/中/高轮数3/6/10。均待用户确认。

D09客串及掉落概率为−1（未定），不运行概率事件；提供确定种子抽样实现与配置接口。不能宣称真实彩蛋概率已完成。D07携带选择由Part3显式交互确定。D08沿用关卡正式道具表的3秒/+1及19个ID。商品一局总售卖量，不自动按天刷新。D15旧人格/DDL/旧结局UI入口仍需Part3移除正式路径。

## 当前交付与验收状态

以下为当前状态，后文按时间保留的验证记录用于追溯。

- 用户授权以暂定决策实现未定规则，配置为 `provisional-2026-09-15.2`；`approved=false`仅标记策划状态，不阻止游戏。见[RULE-DECISIONS-PART-1.md](RULE-DECISIONS-PART-1.md)。
- 用户授权将API凭据编入客户端。生产入口选择DeepSeek直连，网关仅为可选兼容路径。本地凭据已配置，仓库模板留空，日志和公开证据不含凭据。见[AI-SETUP.md](AI-SETUP.md)。
- 六NPC真实开场及回复均通过，覆盖流式片段、三提示、合法情感/话题、程序好感结算；[真实调用结果](Part1Evidence/live-ai-results.json)明确区分于Unity Player验证。
- 最新离线验证20场景、4290断言通过，包含错误恢复、重复操作、交易/存档和六结局；[摘要及源码哈希](Part1Evidence/ai-hardening-summary.json)。自动化默认不调用付费服务。
- Unity原生EditMode 12/12、图形整合4/4及布局入口1/1通过。证据位于整合工程 `Docs/FullGameVerification/2026-09-15/`；图形对话使用明示替身。
- 单一整合工程提交 `5931ced0` 的生产Campaign、Draft、BattleSession连续28日组合验证：28胜、952断言，初始GPA70，前27天各购买0.10GPA补给，最终排名1。实际Rules.json未改写，Outcome由战斗自然产生。证据位于整合工程 `Docs/FullGameVerification/2026-09-15-part2-bridge-integrated/`。此项对话为替身，不能称为图形成品自然28天。

| 任务 | 当前实现与证据 | 仍需整合验收的范围 |
|---|---|---|
| A01 | 公共基线、契约、唯一状态、稳定ID、六NPC/地点和生成器隔离 | 最终成品的旧入口关闭核对 |
| A02 | 三预约编辑/锁定/顺序、检查点及操作接口；离线与图形链路通过 | 最终Player恢复 |
| A03 | 真实DeepSeek六NPC通过；有界上下文、流式、错误恢复、数值校验、额度和8彩蛋 | Player真实联网、头像表现与完整一天体验 |
| A04 | 整数成绩、排名、连胜、重试、唯一结算；生产战果28日接桥通过 | 最终Player连续运行 |
| A05 | 19种道具、库存、目标赠礼、携带消费及失败回滚 | 最终Player交易交互 |
| A06 | 成绩关系、单位目录接口、39条新闻与解锁条件、正式结局与全局记录 | 最终Player页面核对 |
| A07 | v3原子备份、设置、迁移原档保留、跨进程恢复；原生JSON测试通过 | 最终Player冷启动与恢复 |
| A08 | 独立验证、接口示例、迁移/决策说明、生产战斗经济可达性 | 真实AI图形一天闭环及成品连续自然流程证据 |

不再将规则确认、网关或API凭据列为阻断项。尚未用安装包证据证明的项目保持未完成，由Part3串行操作Unity和Player，Part1负责修复其发现的主流程/AI/存档问题。

### 旧入口归属说明

- `Battle/EndingSystem.cs`仍按旧属性/阈值判9结局，`Battle/GameProgressManager.cs`仍含旧学期路径：不属于Part1修改范围，Part3必须令新页面只读取`Snapshot.endingId/companionId`，不得调用旧DetermineEnding。
- `UI/DDLPanel.cs`与人格问答属于Part3接线；Part1不再提供属性战斗加成或旧ActionPoints消费。
- `NPCRelationshipManager`只从Campaign投影返回副本，不再保存第二套关系；旧随机对话已停用。
- `GameDataGenerator`的旧棋子/敌人输出改到LegacyTowers/LegacyEnemies；NPC从Campaign内容生成，保留已有资产meta。
- `Campaign.AvailableActions`返回当前阶段合法操作名；具体余额/库存失败仍以事务结果为准。

未运行旧Demo断言作为新规则验收（初始100、直接跳战斗、旧假AI等断言已过时）；总验证入口与PlayMode由Part3更新。

归档证据：[离线测试摘要](Part1Evidence/offline-summary.json)、[测试输出](Part1Evidence/offline-tests.txt)、[程序集编译摘要](Part1Evidence/compile-summary.json)。待确认选项详见[RULE-DECISIONS-PART-1.md](RULE-DECISIONS-PART-1.md)。

真实Unity发现的空内联对象问题已通过`4145c47d`修复：将完全缺少身份的dialogue/draftRequest/draft/battle/outcome还原null，部分身份错误不掩盖。离线17场景已回归；Part3随后重跑真实Unity EditMode，12/12通过，包含原生数组/空对象与预约事务读档。摘要见`Part1Evidence/native-unity-summary.json`，图形全链路仍由Part3继续验证。

追加客户端HTTP/SSE传输层测试：请求上下文和短期token、分片输出、最终文本一致性、错误媒体类型、非法JSON、无最终回复、超长单行/文本；注入仅测试程序集可用的内存transport，不访问外网。18场景4238断言通过。


## Part3 图形链路证据补充

已核实 `campaign-ui-results.xml`：2/2通过（一天链路与音效）。真实EventSystem点击菜单→三次预约/对话→16棋互斥选棋→Prepare→部署/暂停/弃局→唯一结算→购买→day2读档，空draft问题修复后不再阻断。摘要及原XML/动作日志哈希见 `Part1Evidence/graphical-day-summary.json`。

这证明页面绑定与业务状态链路；规则使用显式测试配置、聊天使用AI替身，因此仍不满足A03真实AI及A08完整真实内容闭环，不能将其标为正式M1验收或自然28天通关。

## Part2经济证据边界

已核查Part2 `content-results.json` 共168场、覆盖28天自然胜利，文件SHA256 `7be4a7605a4472ec5dc7fbc2f53047336efd8d36da64490928ea748438d4d0dc`。该证据证明关卡可胜；Part1另有7000起步、胜200/连胜额外200、对手反向50、每日购买扣10后的28天成功测试。两者互补，但未在同一连续Campaign/Session中直接传递生产BattleOutcome，不能称为自然28天综合通关。结果JSON缺少run/battle/attempt/NPC身份，不得重新包装成胜利DTO冒充生产战果。组合验收需真实DraftService→PrepareBattle→BattleSession→CommitBattleOutcome连续28天，真实AI仍另待服务交付。

暂定规则实现验证：`python3 Tools/verify_campaign.py --output /tmp/unitygame-campaign-provisional-verification-2`，19场景4281断言通过；运行时/Editor离线编译通过。新增覆盖读取实际Rules.json、重试收费去重、战斗彩蛋去重、NPC新闻解锁。此次新游戏入口取消approved检查，原生UI复测交Part3。

DeepSeek直连协议回归：20场景4288断言通过，运行时/Editor离线编译通过。证据`Part1Evidence/direct-ai-summary.json`；未使用真实密钥、未发起付费请求。

## 生产战果直接接桥补充

已核查Part2 `Tests/Integration/Part2CampaignBridge.cs`及`2026-09-15-part2-bridge/bridge-results.json`（SHA256 7e9a6e8719f5a36e8a1d5feec2af5ade3b1e6eb513c6e7812de3bb0d50683666）：28场生产Session自然结束后，原样Outcome进入Campaign唯一结算，并进行每日前27天0.10 GPA购买和次日恢复。该证据补上之前“战斗与经济未直接接桥”的缺口。运行使用UI当时Campaign与Part2候选战斗源，尚未覆盖最新`.2`暂定规则和直连适配器；需整合分支重跑。AI仍为明示测试替身，非真实AI或图形成品自然28天体验。

## 最新整合桥接核对

上述旧“需整合分支重跑”已由单一整合工程 `5931ced0` 的28日、952断言结果补齐。最新AI修复 `0077d332` 已由Part3合入为 `985bde95`，包括任意JSON字段顺序的流式文本解析和正负话题/情绪约束。安装包验证须使用包含此修复的源码。
