# Part 1 交接（代码已交接，外部验收未完成）

工作目录 `/Users/bytedance/unitygame-campaign`，分支 `codex/fullgame-campaign`。
共同基线 `15fc189c`；契约首批 `4b02c674`；实现 `a646c858`、恢复与内容 `19bf07ac`、迁移预览 `83451c5b`。保留原有源码、美术、战斗数据、测试、工具及策划快照；未加入 Library/Builds、历史临时截图、用户存档及无关网站目录。未推送远端。

## 唯一状态与接口

`GameManager.Campaign` 为唯一主流程权威；`Snapshot` 是深拷贝，写入快照不改变游戏。`Campaign.Changed` 在落盘成功后发布。`OperationResult.Success` 对成功和相同请求重放均为 true，`error == Duplicate` 表示重放；相同 actionId 不同参数返回 Conflict。UI应在一次用户意图的网络/存盘重试中复用 actionId，发起新意图才生成新ID。

```csharp
var gm = GameManager.Instance;
var result = gm.StartCampaign("slot-1"); // 已存在时拒绝覆盖；未批准规则时返回 RulesPending
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

## AI交付状态

提供 `IDialogueService`、`CampaignDialogueCoordinator`、HTTPS SSE `HttpDialogueGateway`；后者是游戏网关协议，不是直接调用服务商API。网关使用 `FINALDEFENSE_DIALOGUE_GATEWAY`，短期会话token由 `FINALDEFENSE_GATEWAY_SESSION` 注入。Provider密钥不得嵌入客户端。服务返回 `data: {"delta":"文字"}` 及最终 `data: {"result":{...DialogueTurnResult}}`，SSE事件间空行。最终文本须与流文本一致；合法topic/emotion、120字、3提示、身份校验后才结算。失败、取消、超时、晚回复不扣行程、不改好感。

人设来自包装95–129；六NPC提升/降低话题来自包装215–232；原包装93指定deepseek v4 flash，但模型可用端点、网关、凭据与预算尚未交付，**未接通真实AI，未完成六NPC真人体验验证**。测试使用明示FakeDialogue，仅在Tests目录。

## 当前验证

`python3 Tools/verify_campaign.py --output /tmp/unitygame-campaign-gateway-verification`：18场景、4238断言通过；验证初始化、预约、对话错误/迟到、每日额度、事务、重试、28天六NPC成功路径、两类失败、文件备份/设置/解锁、跨进程继续、旧v1/v2迁移预览、6种话题掉落和2种客串、39条新闻与确定性展示。28天测试使用测试战果和测试AI，**不是自然战斗通关或M1真实闭环**。

运行时与Editor程序集通过离线编译，读取原工程已导入依赖，没有启动Editor、共享Library或使用正式PlayerPrefs。存在已有未赋值等警告，详见临时日志。

## 尚未完成的验收

真实AI、规则确认、旧档迁移策略、客串概率、NPC新闻解锁条件、Part2/3一天真实闭环及Unity成品体验。已导入39条原文新闻；通用14条中用保存种子每天选1–3条，并保存dailyNewsIds，不能读档重抽；25条NPC新闻仅入目录，联动解锁条件待定。最终结局已导入包装673–704完整原文，六位成功对白在NpcDefinition.endingLine。


## 验收追踪

| 任务 | 已实现 | 未完成/外部依赖 |
|---|---|---|
| A01 | 可恢复基线、共享契约、唯一状态、稳定ID、人设/初值/地点、生成器隔离旧战斗目录 | Part3关闭旧人格/DDL/EndingSystem正式入口；正式规则发布 |
| A02 | 早中晚编辑/确认/执行锁、三次结束才编队、检查点与可用操作 | 真实页面操作验收 |
| A03 | SSE协议/有界上下文/取消超时/标签验证/确定数值/额度/8彩蛋配置机制 | 真实服务、六NPC体验、概率定稿 |
| A04 | 整数成绩、唯一结算、连胜/排名/对手成绩/Forfeit与重试配置 | 争议规则定稿、真实战斗完整闭环 |
| A05 | 19项目录、价格/库存/赠礼目标/携带消费、失败回滚 | UI实际使用位置与Figma确认 |
| A06 | 关系/成绩趋势、单位目录契约、39新闻、正式结局、全局解锁 | NPC新闻解锁条件、Part2单位目录适配 |
| A07 | v3列表/原子备份/设置/跨进程、迁移预览与原档保留 | D15批准、真实Unity原生JSON与图形恢复验收 |
| A08 | 独立测试、离线编译、经济/六结局可达性、交接与提交 | M1真实一天、自然28天、真实AI验收 |

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
