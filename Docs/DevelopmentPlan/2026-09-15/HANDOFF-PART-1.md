# Part 1 交接（实现中，未满足完整验收）

工作目录 `/Users/bytedance/unitygame-campaign`，分支 `codex/fullgame-campaign`。
共同基线 `15fc189c`；契约首批 `4b02c674`。保留原有源码、美术、战斗数据、测试、工具及策划快照；未加入 Library/Builds、历史临时截图、用户存档及无关网站目录。未推送远端。

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
- `GameManager.LoadSettings/SaveSettings`，`LoadUnlocks`，`RecoverCampaignBackup`；设置与存档独立。UI应用音量与分辨率。
- `GameManager.BeginNewGame/LoadProgress` 为兼容入口，正式UI请显示 OperationResult。`RecordBattleResult(bool)`、旧属性/GPA/金币直接修改、旧随机NPC回复均停用。保留签名只为可编译。

## 持久化与恢复

保存到 `Application.persistentDataPath/CampaignV3/<saveId>.json`，临时文件写入并flush后原子replace，保留 `.backup`。主文件损坏/未知版本不静默回退；用户选“读取备份”后明确恢复。读取时校验规则和内容版本，失败不会替换当前会话。全局结局在 `unlocks.json`，可由本局终局重新修复；设置在 `settings.json`。

战斗采用准备检查点，保留相同attempt/阵容/seed/物品。读档不消费、不重抽。主动离开已开始检查点走Forfeit；程序中断继续检查点。检查点恢复不是局内全现场存档。

旧PlayerPrefs `FinalDefense.Campaign.v1` 不删、不写。`InspectLegacySave()` 先留只读副本再报告v1/v2原GPA；因旧档没有NPC/三次日程/匹配历史，尚不能无损迁移。D15未确认时不伪造迁移后的历史。

## 规则待定（没有把建议冒充策划）

`CampaignRules` 默认版本 `proposal-2026-09-15.1`、`approved=false`。纯逻辑测试可显式构造proposal；正式新游戏返回 RulesPending。部署需经确认的 `Resources/Campaign/Rules.json`。

提案：D02 玩家最大100，允许购物到0、<0结算失败，并列第一成功；D03第二连胜起每胜额外2、只更新对决NPC；D04赠礼共用+5，无有效增益不消耗，彩蛋门槛取结算后、每条回复最多±2；初始同分用NPC稳定枚举顺序，后续每次好感实际变化记录事件序号；D06重试默认不开放，可配置为付固定费用、同seed和补给且当天成绩不再结算；D16好感60归中档，低/中/高轮数3/6/10。均待用户确认。

D09客串及掉落概率为−1（未定），不运行概率事件；提供确定种子抽样实现与配置接口。不能宣称真实彩蛋概率已完成。D07携带选择由Part3显式交互确定。D08沿用关卡正式道具表的3秒/+1及19个ID。商品一局总售卖量，不自动按天刷新。D15旧人格/DDL/旧结局UI入口仍需Part3移除正式路径。

## AI交付状态

提供 `IDialogueService`、`CampaignDialogueCoordinator`、HTTPS SSE `HttpDialogueGateway`；后者是游戏网关协议，不是直接调用服务商API。网关使用 `FINALDEFENSE_DIALOGUE_GATEWAY`，短期会话token由 `FINALDEFENSE_GATEWAY_SESSION` 注入。Provider密钥不得嵌入客户端。服务返回 `data: {"delta":"文字"}` 及最终 `data: {"result":{...DialogueTurnResult}}`，SSE事件间空行。最终文本须与流文本一致；合法topic/emotion、120字、3提示、身份校验后才结算。失败、取消、超时、晚回复不扣行程、不改好感。

人设来自包装95–129；六NPC提升/降低话题来自包装215–232；原包装93指定deepseek v4 flash，但模型可用端点、网关、凭据与预算尚未交付，**未接通真实AI，未完成六NPC真人体验验证**。测试使用明示FakeDialogue，仅在Tests目录。

## 当前验证

`python3 Tools/verify_campaign.py --output /tmp/unitygame-campaign-verification`：9场景、4059断言通过（第一轮）；验证初始化、预约、对话错误/迟到、每日额度、事务、重试、28天六NPC成功路径、两类失败、文件备份/设置/解锁。28天测试使用测试战果和测试AI，**不是自然战斗通关或M1真实闭环**。

运行时与Editor程序集通过离线编译，读取原工程已导入依赖，没有启动Editor、共享Library或使用正式PlayerPrefs。存在已有未赋值等警告，详见临时日志。

## 尚未完成的验收

真实AI、规则确认、旧档迁移策略、客串概率、正式新闻展示条件/内容、Part2/3一天真实闭环及Unity成品体验。`CampaignContent.news`为空，不能把它宣称正式新闻已完成。最终结局目前采用最新主文案的简述，完整包装展示由Part3整合。
