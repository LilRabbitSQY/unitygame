using System;
using System.Collections.Generic;
using System.Linq;
using FinalDefense.Contracts;
using FinalDefense.Shop;
using FinalDefense.Persistence;

namespace FinalDefense.Campaign
{
    public sealed class CampaignService
    {
        private readonly object gate = new object();
        private readonly IDataCodec codec; private readonly ICampaignStore store;
        private CampaignSnapshot state; private readonly CampaignContent content; private readonly CampaignRules rules;
        public event Action<CampaignSnapshot> Changed;
        public CampaignSnapshot Snapshot { get { lock (gate) return Copy(state); } }
        public CampaignContent Content => Copy(content);
        public CampaignRules Rules => Copy(rules);
        public string[] AvailableActions
        {
            get
            {
                var s = Snapshot;
                switch (s.phase)
                {
                    case CampaignStage.Introduction: return new[] { "CompleteIntroduction" };
                    case CampaignStage.Booking: return ValidAppointments(s.appointments) ? new[] { "EditAppointments", "ConfirmAppointments", "Gift" } : new[] { "EditAppointments", "Gift" };
                    case CampaignStage.Dialogue: return !s.dialogue.opened ? new[] { "OpenDialogue" } : s.dialogue.turns < 1 ? new[] { "SendDialogue" } : s.dialogue.finished ? new[] { "FinishDialogue" } : new[] { "SendDialogue", "FinishDialogue" };
                    case CampaignStage.Matching: return new[] { "SaveDraft" };
                    case CampaignStage.Preparation: return new[] { "PrepareBattle" };
                    case CampaignStage.Battle: return new[] { "ResumeBattle", "CommitBattleOutcome" };
                    case CampaignStage.Result: return rules.retriesAllowed && s.outcome.reason != BattleEndReason.Victory ? new[] { "EnterShop", "RetryBattle" } : new[] { "EnterShop" };
                    case CampaignStage.Shop: return new[] { "Buy", "Gift", "NextDay" };
                    default: return Array.Empty<string>();
                }
            }
        }
        public CampaignService(CampaignContent content, CampaignRules rules, ICampaignStore store, IDataCodec codec, CampaignSnapshot saved)
        {
            this.codec = codec ?? throw new ArgumentNullException(nameof(codec)); this.store = store ?? throw new ArgumentNullException(nameof(store));
            this.content = Copy(content); this.rules = Copy(rules); this.content.Validate(); this.rules.Validate();
            Validate(saved); state = Copy(saved);
        }
        public static CampaignSnapshot NewState(string saveId, CampaignContent content, CampaignRules rules, int seed)
        {
            content.Validate(); rules.Validate(); if (!AtomicCampaignStore.ValidId(saveId) || saveId == "settings" || saveId == "unlocks") throw new ArgumentException("Invalid save ID");
            return new CampaignSnapshot { saveId = saveId, runId = Guid.NewGuid().ToString("N"), rulesVersion = rules.version,
                contentVersion = content.version, seed = seed, phase = CampaignStage.Introduction,
                npcs = content.npcs.Where(n => n.romance).Select(n => new NpcSnapshot { npcId = n.id, favor = n.initialFavor, gpa = n.initialGpa, reachedSequence = n.legacyId + 1 }).ToArray(),
                dailyNewsIds = SelectNews(content, seed, 1), sequence = 6, inventory = BattleItemDefs.All.Select(i => new InventoryEntry { itemId = i.key }).ToArray() };
        }
        private T Copy<T>(T value) => value == null ? default(T) : codec.Decode<T>(codec.Encode(value));
        private OperationResult Error(OperationError error, string text) => OperationResult.Fail(error, text);
        private OperationResult Ok(string message = "操作完成") => new OperationResult { message = message };
        private static bool Id(string id) => !string.IsNullOrWhiteSpace(id) && id.Length <= 160;
        private OperationResult Apply(string actionId, Payload payload, Func<CampaignSnapshot, OperationResult> mutate)
        {
            CampaignSnapshot published = null; OperationResult result;
            lock (gate)
            {
                if (!Id(actionId)) return Error(OperationError.InvalidInput, "操作ID不能为空或超长");
                string fingerprint = codec.Encode(payload);
                var prior = state.receipts.FirstOrDefault(r => r.actionId == actionId);
                if (prior != null)
                {
                    if (prior.fingerprint != fingerprint) return Error(OperationError.Conflict, "操作ID已用于不同请求");
                    result = Copy(prior.result); result.error = OperationError.Duplicate; return result;
                }
                var next = Copy(state); result = mutate(next); if (!result.Success) return result;
                next.receipts = next.receipts.Concat(new[] { new OperationReceipt { actionId = actionId, fingerprint = fingerprint, result = Copy(result) } }).ToArray();
                try { Validate(next); store.Write(next); }
                catch { return Error(OperationError.PersistenceFailed, "保存失败，状态未改变；请重试"); }
                state = next; published = Copy(next);
            }
            // Subscriber failures cannot turn an already persisted transaction into a failed operation.
            if (Changed != null) foreach (Action<CampaignSnapshot> subscriber in Changed.GetInvocationList())
                try { subscriber(Copy(published)); } catch { }
            return result;
        }
        [Serializable] private sealed class Payload { public string op, a, b, c; public string[] items; public Appointment[] appointments; public DraftSnapshot draft; public BattleOutcome outcome; public DialogueTurnResult dialogue; }
        public OperationResult CompleteIntroduction(string actionId) => Apply(actionId, new Payload { op = "intro" }, s =>
        { if (s.phase != CampaignStage.Introduction) return Error(OperationError.WrongPhase, "当前不是引导阶段"); s.phase = CampaignStage.Booking; return Ok(); });
        public OperationResult EditAppointments(string actionId, Appointment[] appointments) => Apply(actionId, new Payload { op = "book", appointments = appointments }, s =>
        {
            if (s.phase != CampaignStage.Booking) return Error(OperationError.WrongPhase, "预约已锁定");
            if (!ValidAppointments(appointments)) return Error(OperationError.InvalidInput, "请选择早中晚三个地点及可攻略同伴");
            s.appointments = Copy(appointments); return Ok();
        });
        private bool ValidAppointments(Appointment[] values) => values != null && values.Length == 3 && values.All(a => a != null && content.Npc(a.npcId)?.romance == true && content.locations.Any(l => l.id == a.locationId)) && (rules.repeatCompanion || values.Select(a => a.npcId).Distinct().Count() == 3);
        public OperationResult ConfirmAppointments(string actionId) => Apply(actionId, new Payload { op = "confirm" }, s =>
        {
            if (s.phase != CampaignStage.Booking || !ValidAppointments(s.appointments)) return Error(OperationError.WrongPhase, "请先完成三次预约");
            s.slot = 0; OpenSlot(s); return Ok("预约已锁定");
        });
        private void OpenSlot(CampaignSnapshot s)
        {
            var a = s.appointments[s.slot]; s.phase = CampaignStage.Dialogue;
            s.dialogue = new DialogueCheckpoint { conversationId = s.runId + ":" + s.day + ":" + s.slot, npcId = a.npcId, locationId = a.locationId };
            if (rules.cameoChancePerTenThousand >= 0 && Roll(s.seed, s.day * 31 + s.slot * 7) < rules.cameoChancePerTenThousand)
                s.dialogue.cameoId = Roll(s.seed, s.day * 37 + s.slot) < 5000 ? "wang_yijun" : "ning_qishan";
        }
        private static int Roll(int seed, int salt) { unchecked { uint x = (uint)(seed ^ salt * 16777619); x ^= x << 13; x ^= x >> 17; x ^= x << 5; return (int)(x % 10000); } }
        public int MaxTurns(int favor, string topic, NpcDefinition npc)
        {
            int band = favor < 30 ? 0 : favor == 60 ? rules.favorSixtyBand : favor > 60 ? 2 : 1;
            bool negative = npc.negativeTopics.Contains(topic); bool positive = npc.positiveTopics.Contains(topic);
            int will = negative ? (band == 2 ? 1 : 0) : positive ? (band == 0 ? 1 : 2) : (band == 2 ? 2 : 1);
            return will == 0 ? rules.lowTurns : will == 1 ? rules.mediumTurns : rules.highTurns;
        }
        public DialogueTurnRequest CreateDialogueRequest(string turnId, string input)
        {
            var s = Snapshot; if (s.phase != CampaignStage.Dialogue || s.dialogue == null || s.dialogue.finished || !Id(turnId)) throw new InvalidOperationException("对话阶段无效");
            bool opening = !s.dialogue.opened;
            if ((!opening && string.IsNullOrWhiteSpace(input)) || (input?.Length ?? 0) > 1000 || (opening && !string.IsNullOrEmpty(input))) throw new ArgumentException("开场无需输入，回复需1–1000字");
            var npc = content.Npc(s.dialogue.npcId); var n = s.npcs.First(v => v.npcId == npc.id);
            return new DialogueTurnRequest { runId = s.runId, conversationId = s.dialogue.conversationId, turnId = turnId, npcId = npc.id,
                cameoId = opening ? s.dialogue.cameoId : null, cameoPersona = opening ? content.Npc(s.dialogue.cameoId)?.persona : null,
                locationId = s.dialogue.locationId, input = input ?? "", persona = npc.persona, favor = n.favor, maxTurns = 10,
                lastBattleSummary = s.lastBattleSummary, opening = opening, allowedTopics = new[] { "neutral" }.Concat(npc.positiveTopics).Concat(npc.negativeTopics).ToArray(),
                hints = s.dialogue.hints, history = s.dialogue.history.Select(l => Copy(l)).ToArray() };
        }
        // This endpoint is for the service coordinator, never for direct model/UI numerical changes.
        internal OperationResult CommitDialogue(DialogueTurnRequest request, DialogueTurnResult reply) => Apply(request.turnId,
            new Payload { op = "dialogue", a = request.conversationId, b = request.input, dialogue = reply }, s =>
        {
            var d = s.dialogue;
            if (s.phase != CampaignStage.Dialogue || d == null || d.finished || request.runId != s.runId || request.conversationId != d.conversationId || request.npcId != d.npcId || request.locationId != d.locationId || request.opening == d.opened
                || request.history == null || request.history.Length != d.history.Length) return Error(OperationError.Conflict, "对话已变化，旧回复已丢弃");
            var npc = content.Npc(d.npcId);
            if (reply == null || reply.status != DialogueStatus.Completed || reply.conversationId != d.conversationId || reply.turnId != request.turnId
                || string.IsNullOrWhiteSpace(reply.text) || new System.Globalization.StringInfo(reply.text).LengthInTextElements > 120
                || !content.emotionTags.Contains(reply.emotion) || (reply.topic != "neutral" && !npc.positiveTopics.Contains(reply.topic) && !npc.negativeTopics.Contains(reply.topic))
                || reply.hints == null || reply.hints.Length != 3 || reply.hints.Any(h => string.IsNullOrWhiteSpace(h) || h.Length > 120)) return Error(OperationError.InvalidResponse, "AI回复格式不合法，可重试");
            bool positive = npc.positiveTopics.Contains(reply.topic), negative = npc.negativeTopics.Contains(reply.topic);
            if (!request.opening && ((positive && reply.emotion != "happy" && reply.emotion != "touched") || (negative && reply.emotion != "angry" && reply.emotion != "sad" && reply.emotion != "speechless"))) return Error(OperationError.InvalidResponse, "话题与情感标签不一致");
            var n = s.npcs.First(v => v.npcId == npc.id); int before = n.favor;
            var result = Ok();
            if (!request.opening)
            {
                d.turns++; result.favorDelta = Favor(s, n, positive ? 2 : negative ? -2 : 0, true);
                d.history = d.history.Concat(new[] { new DialogueLine { role = "user", text = request.input } }).ToArray();
                if (positive && result.favorDelta == 0) result.message = "今日好感额度或好感上限已满";
                string item = TopicItem(npc.id, reply.topic, rules.thresholdAfterTurn ? n.favor : before);
                if (item != null && rules.topicDropChancePerTenThousand >= 0 && Roll(s.seed, s.day * 997 + s.slot * 31 + d.turns) < rules.topicDropChancePerTenThousand)
                    if (GrantConversationItem(s, item)) result.itemId = item;
            }
            else if (!string.IsNullOrEmpty(d.cameoId))
            {
                string item = d.cameoId == "wang_yijun" ? "leave_note" : "recommendation_letter";
                if (GrantConversationItem(s, item)) result.itemId = item;
            }
            d.opened = true; d.emotion = reply.emotion; d.hints = Copy(reply.hints);
            d.history = d.history.Concat(new[] { new DialogueLine { role = "assistant", text = reply.text } }).ToArray();
            d.finished = !request.opening && (reply.endConversation || d.turns >= MaxTurns(n.favor, reply.topic, npc) || d.turns >= 10);
            return result;
        });
        private bool GrantConversationItem(CampaignSnapshot s, string id)
        {
            if (s.dialogue.grantedItems.Contains(id)) return false;
            var item = s.inventory.First(i => i.itemId == id); if (item.count >= BattleItemDefs.Find(id).stackLimit) return false;
            item.count++; s.dialogue.grantedItems = s.dialogue.grantedItems.Concat(new[] { id }).ToArray(); return true;
        }
        private static string TopicItem(string npc, string topic, int favor)
        {
            switch (npc)
            {
                case "liu_ruoshui": return topic == "ACGN同人" && favor >= 40 ? "signed_fan_art" : null;
                case "wan_sirui": return topic == "美妆" || topic == "穿搭" ? "makeup_sample" : null;
                case "luo_yang": return topic == "学术" || topic == "竞赛" ? "competition_manual" : null;
                case "xiang_zhen": return topic == "盲盒" || topic == "童年回忆" ? "balulu_figure" : null;
                case "ren_fei": return topic == "电竞" || topic == "手工" ? "custom_keyboard" : null;
                case "ming_shan": return topic == "咖啡" || topic == "音乐" ? "coffee_coupon" : null;
                default: return null;
            }
        }
        private int Favor(CampaignSnapshot s, NpcSnapshot n, int delta, bool cap)
        {
            int actual = delta > 0 ? Math.Min(delta, Math.Min(100 - n.favor, cap ? 5 - n.dailyGain : 100)) : Math.Max(delta, -n.favor);
            n.favor += actual; if (actual > 0 && cap) n.dailyGain += actual;
            if (actual != 0) n.reachedSequence = ++s.sequence; return actual;
        }
        public OperationResult FinishDialogue(string actionId) => Apply(actionId, new Payload { op = "finishDialogue" }, s =>
        {
            if (s.phase != CampaignStage.Dialogue || s.dialogue == null || !s.dialogue.opened || s.dialogue.turns < 1) return Error(OperationError.WrongPhase, "请先完成NPC开场及一次回复");
            s.dialogue.finished = true;
            if (++s.slot < 3) OpenSlot(s);
            else
            {
                s.phase = CampaignStage.Matching;
                s.draftRequest = new DraftRequest { runId = s.runId, battleId = s.runId + ":battle:" + s.day, day = s.day,
                    rulesVersion = rules.version, seed = unchecked(s.seed + s.day * 104729), cycleLength = rules.matchCycleLength,
                    playerGpa = s.gpa, candidates = Copy(s.npcs), history = s.matchHistory.Skip(((s.day - 1) / rules.matchCycleLength) * rules.matchCycleLength).ToArray() };
            }
            return Ok();
        });
        public OperationResult SaveDraft(string actionId, DraftSnapshot draft) => Apply(actionId, new Payload { op = "draft", draft = draft }, s =>
        {
            if (s.phase != CampaignStage.Matching || s.draftRequest == null) return Error(OperationError.WrongPhase, "三次行程尚未完成或编队已锁定");
            if (draft == null || draft.battleId != s.draftRequest.battleId || draft.seed != s.draftRequest.seed || draft.rulesVersion != rules.version
                || !s.npcs.Any(n => n.npcId == draft.opponentId) || draft.playerUnits == null || draft.enemyUnits == null || draft.availableUnits == null
                || draft.playerUnits.Concat(draft.enemyUnits).Concat(draft.availableUnits).Any(u => !Id(u))
                || draft.playerUnits.Concat(draft.enemyUnits).Concat(draft.availableUnits).Distinct().Count() != draft.playerUnits.Length + draft.enemyUnits.Length + draft.availableUnits.Length)
                return Error(OperationError.InvalidInput, "编队身份、种子或棋子互斥校验失败");
            var opponent = s.npcs.First(n => n.npcId == draft.opponentId);
            if (s.gpa != opponent.gpa && draft.playerFirst != (s.gpa < opponent.gpa)) return Error(OperationError.InvalidInput, "低GPA方应先手");
            if (s.draft != null && !string.IsNullOrEmpty(s.draft.battleId) && (s.draft.opponentId != draft.opponentId || s.draft.playerFirst != draft.playerFirst)) return Error(OperationError.Conflict, "不能重抽对手或先手");
            if (draft.locked && (draft.playerUnits.Length == 0 || draft.playerUnits.Length != draft.enemyUnits.Length || draft.availableUnits.Length != 0)) return Error(OperationError.InvalidInput, "锁定阵容必须双方各半且无剩余棋子");
            s.draft = Copy(draft); if (draft.locked) s.phase = CampaignStage.Preparation; return Ok();
        });
        public OperationResult PrepareBattle(string actionId, string levelId, string contentVersion, string[] items, int initialCost, int protection) => Apply(actionId,
            new Payload { op = "prepare", a = levelId, b = contentVersion, c = initialCost + ":" + protection, items = items }, s =>
        {
            if (s.phase != CampaignStage.Preparation || s.draft == null || !s.draft.locked) return Error(OperationError.WrongPhase, "请先锁定编队");
            if (!Id(levelId) || !Id(contentVersion) || initialCost < 0 || protection <= 0 || items == null || items.Distinct().Count() != items.Length
                || items.Any(id => BattleItemDefs.Find(id)?.IsBattleItem != true || s.inventory.First(i => i.itemId == id).count < 1)) return Error(OperationError.InvalidInput, "补给、关卡或初始参数无效");
            foreach (string id in items) s.inventory.First(i => i.itemId == id).count--;
            s.battle = new BattleStartContext { runId = s.runId, battleId = s.draft.battleId, attemptId = s.draft.battleId + ":1", levelId = levelId,
                contentVersion = contentVersion, seed = s.draft.seed, playerUnits = Copy(s.draft.playerUnits), enemyUnits = Copy(s.draft.enemyUnits), battleItems = Copy(items), initialCost = initialCost, protection = protection };
            s.phase = CampaignStage.Battle; return Ok("补给已扣除，战斗检查点已保存");
        });
        public OperationResult CommitBattleOutcome(BattleOutcome outcome)
        {
            if (outcome == null) return Error(OperationError.InvalidInput, "缺少战果");
            return Apply("outcome:" + outcome.attemptId, new Payload { op = "outcome", outcome = outcome }, s =>
            {
                if (s.phase != CampaignStage.Battle || s.battle == null || outcome.runId != s.runId || outcome.battleId != s.battle.battleId || outcome.attemptId != s.battle.attemptId)
                    return Error(OperationError.Conflict, "战果不属于当前战斗");
                if (!Enum.IsDefined(typeof(BattleEndReason), outcome.reason) || outcome.protectionLost < 0 || (outcome.report?.Length ?? 0) > 2000) return Error(OperationError.InvalidInput, "战果无效");
                bool won = outcome.reason == BattleEndReason.Victory; int before = s.gpa;
                if (!s.daySettled)
                {
                    s.streak = won ? s.streak + 1 : 0;
                    s.gpa = Math.Min(rules.playerMaxGpa, s.gpa + (won ? 200 + (s.streak >= rules.streakBonusFrom ? 200 : 0) + (s.battle.battleItems.Contains("recommendation_letter") ? 100 : 0) : -300));
                    var opponent = s.npcs.First(n => n.npcId == s.draft.opponentId); opponent.gpa = Math.Max(rules.npcMinGpa, Math.Min(rules.npcMaxGpa, opponent.gpa + (won ? -50 : 50)));
                    if (won) s.wins++; else s.losses++;
                    s.matchHistory = s.matchHistory.Concat(new[] { opponent.npcId }).ToArray(); s.daySettled = true;
                    s.trends = s.trends.Concat(new[] { new DailyRecord { day = s.day, gpa = s.gpa, streak = s.streak, ranking = Rank(s), opponentId = opponent.npcId, reason = outcome.reason.ToString() } }).ToArray();
                }
                s.outcome = Copy(outcome); s.lastBattleSummary = "第" + s.day + "天，" + (won ? "胜利" : "失败") + "，GPA " + (s.gpa / 100m).ToString("0.00");
                s.phase = CampaignStage.Result;
                if (s.gpa < 0 || s.day == 28) End(s);
                return new OperationResult { gpaDelta = s.gpa - before, message = "战果已结算" };
            });
        }
        public OperationResult RetryBattle(string actionId) => Apply(actionId, new Payload { op = "retry" }, s =>
        {
            if (!rules.retriesAllowed) return Error(OperationError.RulesPending, "本规则版本未开放重试");
            if (s.phase != CampaignStage.Result || s.outcome == null || s.outcome.reason == BattleEndReason.Victory || s.battle == null) return Error(OperationError.WrongPhase, "当前无法重试");
            if (s.gpa < rules.retryPrice) return Error(OperationError.InsufficientFunds, "GPA不足");
            s.gpa -= rules.retryPrice; s.battle.attemptId = s.battle.battleId + ":retry:" + (s.sequence + 1); s.sequence++;
            s.outcome = null; s.phase = CampaignStage.Battle; return Ok("沿用相同阵容、种子及补给，当天成绩不再次结算");
        });
        public OperationResult EnterShop(string actionId) => Apply(actionId, new Payload { op = "shop" }, s =>
        { if (s.phase != CampaignStage.Result || !s.daySettled) return Error(OperationError.WrongPhase, "请先完成战斗结算"); s.phase = CampaignStage.Shop; return Ok(); });
        public OperationResult Buy(string actionId, string itemId) => Apply(actionId, new Payload { op = "buy", a = itemId }, s =>
        {
            if (s.phase != CampaignStage.Shop) return Error(OperationError.WrongPhase, "仅可在商店购买");
            var def = BattleItemDefs.Find(itemId); if (def == null || def.priceHundredths <= 0) return Error(OperationError.NotFound, "此道具不可购买");
            var item = s.inventory.First(i => i.itemId == itemId);
            if (item.purchased >= def.saleLimit) return Error(OperationError.OutOfStock, "商品售罄");
            if (item.count >= def.stackLimit) return Error(OperationError.InventoryFull, "库存达到上限");
            if (s.gpa < def.priceHundredths) return Error(OperationError.InsufficientFunds, "GPA不足");
            s.gpa -= def.priceHundredths; item.count++; item.purchased++; return Ok("购买成功");
        });
        public OperationResult Gift(string actionId, string itemId, string npcId) => Apply(actionId, new Payload { op = "gift", a = itemId, b = npcId }, s =>
        {
            if (s.phase != CampaignStage.Shop && s.phase != CampaignStage.Booking) return Error(OperationError.WrongPhase, "请在预约或商店阶段赠礼");
            var def = BattleItemDefs.Find(itemId); var n = s.npcs.FirstOrDefault(v => v.npcId == npcId);
            if (def == null || def.IsBattleItem || n == null || (def.targetNpc != null && def.targetNpc != npcId)) return Error(OperationError.InvalidInput, "礼物或赠送对象不符");
            var item = s.inventory.First(i => i.itemId == itemId); if (item.count < 1) return Error(OperationError.OutOfStock, "没有此礼物");
            int change = Favor(s, n, def.favorGain, rules.giftsShareDailyCap); if (change == 0) return Error(OperationError.NoEffect, "好感或每日额度已满，未消耗礼物");
            item.count--; return new OperationResult { favorDelta = change, message = "赠送成功" };
        });
        public OperationResult NextDay(string actionId) => Apply(actionId, new Payload { op = "next" }, s =>
        {
            if (s.phase != CampaignStage.Shop || !s.daySettled || s.day >= 28) return Error(OperationError.WrongPhase, "当前不能进入下一天");
            s.day++; s.dailyNewsIds = SelectNews(content, s.seed, s.day); s.phase = CampaignStage.Booking; s.slot = 0; s.appointments = new Appointment[3]; s.dialogue = null;
            s.draftRequest = null; s.draft = null; s.battle = null; s.outcome = null; s.daySettled = false;
            foreach (var n in s.npcs) n.dailyGain = 0; return Ok();
        });
        public RankingEntry[] Ranking => Rank(Snapshot);
        private static RankingEntry[] Rank(CampaignSnapshot s)
        {
            var all = s.npcs.Select(n => new RankingEntry { id = n.npcId, gpa = n.gpa }).Concat(new[] { new RankingEntry { id = "player", gpa = s.gpa } }).ToArray();
            foreach (var e in all) e.rank = 1 + all.Count(other => other.gpa > e.gpa);
            return all.OrderBy(e => e.rank).ThenBy(e => e.id, StringComparer.Ordinal).ToArray();
        }
        private void End(CampaignSnapshot s)
        {
            bool first = s.gpa >= 0 && !s.npcs.Any(n => n.gpa > s.gpa) && (rules.tiedFirstWins || !s.npcs.Any(n => n.gpa == s.gpa));
            s.companionId = first ? s.npcs.OrderByDescending(n => n.favor).ThenBy(n => n.reachedSequence).ThenBy(n => n.npcId, StringComparer.Ordinal).First().npcId : null;
            s.endingId = first ? "success_" + s.companionId : s.gpa < 0 ? "failure_gpa" : "failure_rank"; s.phase = CampaignStage.Ending;
        }
        internal static string[] SelectNews(CampaignContent content, int seed, int day)
        {
            // General authored news is available to everyone. NPC unlock conditions remain a content decision.
            var candidates = content.news.Where(n => string.IsNullOrEmpty(n.npcId) && day >= n.firstDay && day <= n.lastDay).ToArray();
            return candidates.OrderBy(n => Roll(seed, day * 541 + Array.IndexOf(candidates, n) * 3571)).ThenBy(n => n.id, StringComparer.Ordinal)
                .Take(1 + Roll(seed, day * 73) % 3).Select(n => n.id).ToArray();
        }
        public NewsDefinition[] News { get { var ids = Snapshot.dailyNewsIds; return content.news.Where(n => ids.Contains(n.id)).Select(Copy).ToArray(); } }
        public OperationResult ReadNews(string actionId, string newsId) => Apply(actionId, new Payload { op = "news", a = newsId }, s =>
        { if (!s.dailyNewsIds.Contains(newsId)) return Error(OperationError.NotFound, "新闻不可用"); s.readNews = s.readNews.Union(new[] { newsId }).ToArray(); return Ok(); });
        public void Validate(CampaignSnapshot s)
        {
            if (s == null || s.version != 3 || !AtomicCampaignStore.ValidId(s.saveId) || !Id(s.runId) || s.rulesVersion != rules.version || s.contentVersion != content.version
                || s.day < 1 || s.day > 28 || s.gpa > rules.playerMaxGpa || s.gpa < -300 || s.streak < 0 || s.streak > 28 || s.sequence < 6 || !Enum.IsDefined(typeof(CampaignStage), s.phase)
                || s.npcs == null || s.npcs.Length != 6 || s.npcs.Any(n => n == null || content.Npc(n.npcId)?.romance != true || n.gpa < rules.npcMinGpa || n.gpa > rules.npcMaxGpa || n.favor < 0 || n.favor > 100 || n.dailyGain < 0 || n.dailyGain > 5 || n.reachedSequence < 1 || n.reachedSequence > s.sequence)
                || s.npcs.Select(n => n.npcId).Distinct().Count() != 6 || s.inventory == null || s.inventory.Length != BattleItemDefs.All.Length
                || s.inventory.Any(i => i == null || BattleItemDefs.Find(i.itemId) == null || i.count < 0 || i.count > BattleItemDefs.Find(i.itemId).stackLimit || i.purchased < 0 || i.purchased > BattleItemDefs.Find(i.itemId).saleLimit)
                || s.inventory.Select(i => i.itemId).Distinct().Count() != s.inventory.Length || s.receipts == null || s.receipts.Any(r => r == null || !Id(r.actionId) || r.result == null || r.fingerprint == null)
                || s.receipts.Select(r => r.actionId).Distinct().Count() != s.receipts.Length || s.trends == null || s.matchHistory == null || s.readNews == null || s.dailyNewsIds == null || s.dailyNewsIds.Length > 3 || s.dailyNewsIds.Any(id => !content.news.Any(n => n.id == id)) || s.slot < 0 || s.slot > 3 || s.appointments == null || s.appointments.Length != 3)
                throw new ArgumentException("Invalid campaign save");
            if (s.phase >= CampaignStage.Dialogue && s.phase <= CampaignStage.Shop && !ValidAppointments(s.appointments)) throw new ArgumentException("Invalid appointment checkpoint");
            if (s.phase == CampaignStage.Dialogue && (s.slot > 2 || s.dialogue == null || s.dialogue.npcId != s.appointments[s.slot].npcId || s.dialogue.locationId != s.appointments[s.slot].locationId || !Id(s.dialogue.conversationId) || s.dialogue.turns < 0 || s.dialogue.turns > 10 || s.dialogue.history == null || s.dialogue.history.Length > 21 || s.dialogue.grantedItems == null || s.dialogue.hints == null)) throw new ArgumentException("Invalid dialogue checkpoint");
            if (s.phase >= CampaignStage.Matching && s.phase <= CampaignStage.Shop && (s.slot != 3 || s.draftRequest == null || !Id(s.draftRequest.battleId))) throw new ArgumentException("Invalid match checkpoint");
            if (s.phase >= CampaignStage.Preparation && s.phase <= CampaignStage.Shop && (s.draft == null || !s.draft.locked)) throw new ArgumentException("Invalid draft checkpoint");
            if (s.phase >= CampaignStage.Battle && s.phase <= CampaignStage.Shop && (s.battle == null || s.battle.runId != s.runId || s.battle.battleId != s.draft.battleId || !Id(s.battle.attemptId) || s.battle.battleItems == null || s.battle.battleItems.Any(i => BattleItemDefs.Find(i)?.IsBattleItem != true))) throw new ArgumentException("Invalid battle checkpoint");
            if ((s.phase == CampaignStage.Result || s.phase == CampaignStage.Shop) && (!s.daySettled || s.outcome == null)) throw new ArgumentException("Invalid settlement checkpoint");
            if (s.phase == CampaignStage.Ending && (string.IsNullOrEmpty(s.endingId) || !s.daySettled)) throw new ArgumentException("Invalid ending checkpoint");
            if (s.trends.Length > 28 || s.matchHistory.Length > 28 || s.matchHistory.Any(id => content.Npc(id)?.romance != true)
                || s.trends.Any(t => t == null || t.day < 1 || t.day > s.day || t.ranking == null || t.ranking.Length != 7)
                || s.trends.Select(t => t.day).Distinct().Count() != s.trends.Length) throw new ArgumentException("Invalid history");
            if (s.phase == CampaignStage.Dialogue)
            {
                var d = s.dialogue;
                if (d.conversationId != s.runId + ":" + s.day + ":" + s.slot || d.history.Any(l => l == null || string.IsNullOrWhiteSpace(l.text) || (l.role != "user" && l.role != "assistant"))
                    || d.history.Length != (d.opened ? 1 + d.turns * 2 : 0) || (d.finished && (!d.opened || d.turns < 1))
                    || !content.emotionTags.Contains(d.emotion) || (d.opened && (d.hints.Length != 3 || d.hints.Any(string.IsNullOrWhiteSpace)))
                    || d.grantedItems.Any(id => BattleItemDefs.Find(id)?.IsBattleItem != true) || d.grantedItems.Distinct().Count() != d.grantedItems.Length
                    || (!string.IsNullOrEmpty(d.cameoId) && d.cameoId != "wang_yijun" && d.cameoId != "ning_qishan")) throw new ArgumentException("Invalid dialogue history");
            }
            if (s.phase >= CampaignStage.Matching && s.phase <= CampaignStage.Shop)
            {
                var r = s.draftRequest;
                if (r.runId != s.runId || r.day != s.day || r.battleId != s.runId + ":battle:" + s.day || r.rulesVersion != rules.version
                    || r.seed != unchecked(s.seed + s.day * 104729) || r.candidates == null || r.candidates.Length != 6 || r.history == null) throw new ArgumentException("Invalid draft request");
                if (s.draft != null && !string.IsNullOrEmpty(s.draft.battleId))
                {
                    var d = s.draft;
                    if (d.battleId != r.battleId || d.seed != r.seed || d.rulesVersion != rules.version || content.Npc(d.opponentId)?.romance != true
                        || d.playerUnits == null || d.enemyUnits == null || d.availableUnits == null) throw new ArgumentException("Invalid saved draft");
                    var all = d.playerUnits.Concat(d.enemyUnits).Concat(d.availableUnits).ToArray();
                    if (all.Any(u => !Id(u)) || all.Distinct().Count() != all.Length || (d.locked && (d.playerUnits.Length == 0 || d.playerUnits.Length != d.enemyUnits.Length || d.availableUnits.Length != 0))) throw new ArgumentException("Invalid saved roster");
                }
            }
            if (s.phase >= CampaignStage.Battle && s.phase <= CampaignStage.Shop)
            {
                var b = s.battle;
                if (b.seed != s.draft.seed || b.playerUnits == null || b.enemyUnits == null || !b.playerUnits.SequenceEqual(s.draft.playerUnits) || !b.enemyUnits.SequenceEqual(s.draft.enemyUnits)
                    || !Id(b.levelId) || !Id(b.contentVersion) || b.initialCost < 0 || b.protection < 1 || b.battleItems.Distinct().Count() != b.battleItems.Length) throw new ArgumentException("Invalid battle recovery");
                if (s.daySettled && s.outcome != null && (s.outcome.runId != s.runId || s.outcome.battleId != b.battleId)) throw new ArgumentException("Invalid outcome identity");
            }
            if (s.gpa < 0 && s.phase != CampaignStage.Ending) throw new ArgumentException("Negative GPA must terminate campaign");
        }
    }
}
