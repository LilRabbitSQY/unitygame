using System;
using System.IO;
using System.Linq;
using UnityEngine;
using FinalDefense.Data;
using FinalDefense.Combat;
using FinalDefense.Shop;
using FinalDefense.Campaign;
using FinalDefense.Contracts;
using FinalDefense.Persistence;
using FinalDefense.Dialogue;
using BattleEndReason = FinalDefense.Contracts.BattleEndReason;

namespace FinalDefense.Core
{
    // Compatibility projection for existing scenes. New pages consume Campaign.Snapshot.phase.
    public enum CampaignPhase { Personality, Schedule, Battle, Result, Shop, Complete }
    public sealed class UnityCampaignCodec : IDataCodec
    {
        [Serializable] private sealed class ArrayBox<T> { public T value; }
        public string Encode<T>(T value) => typeof(T).IsArray ? JsonUtility.ToJson(new ArrayBox<T> { value = value }) : JsonUtility.ToJson(value);
        public T Decode<T>(string value)
        {
            T result = typeof(T).IsArray ? JsonUtility.FromJson<ArrayBox<T>>(value).value : JsonUtility.FromJson<T>(value);
            if (result is CampaignSnapshot snapshot) CampaignSnapshotCompatibility.NormalizeEmptyCheckpoints(snapshot);
            return result;
        }
    }
    public class GameManager : Singleton<GameManager>
    {
        [SerializeField] private PlayerStats playerStats;
        [SerializeField] private PersonalityConfig personalityConfig;
        public PlayerStats Stats => playerStats;
        public PersonalityConfig PersonalityConfigData => personalityConfig;
        public CampaignService Campaign { get; private set; }
        public CampaignDialogueCoordinator DialogueSession { get; private set; }
        public OperationResult LastOperation { get; private set; }
        public string PersistenceWarning { get; private set; }
        private AtomicCampaignStore store; private UnityCampaignCodec codec; private CampaignContent content; private CampaignRules rules;
        private IDialogueService dialogueService;
        public const int TotalDays = 28;
        private const string LegacySaveKey = "FinalDefense.Campaign.v1";
        private CampaignSnapshot S => Campaign?.Snapshot;
        public float CurrentGPA => CurrentGPAHundredths / 100f;
        public int CurrentGPAHundredths => S?.gpa ?? 7000;
        public int CurrentDay => S?.day ?? 1;
        public int BattlesWon => S?.wins ?? 0;
        public int BattlesLost => S?.losses ?? 0;
        public int RetryCount => S?.battle?.attemptId?.Contains(":retry:") == true ? 1 : 0;
        public bool IsGameComplete => S?.phase == CampaignStage.Ending;
        public bool HasSavedGame => ListCampaignSaves().Any(s => s.error == null);
        public bool HasLegacySave => PlayerPrefs.HasKey(LegacySaveKey);
        public int CurrentGrade => 1;
        public int CurrentEmotion => 0; public int CurrentStrength => 0; public int CurrentEduPower => 0; public int CurrentDetermination => 0;
        public int Gold => 0; public int StatCap => 20; public int MaxActionPoints => 3;
        public int ActionPoints => S == null ? 3 : Math.Max(0, 3 - S.slot);
        public int CurrentSemester => Math.Min(8, (CurrentDay - 1) / 3 + 1);
        public int CurrentRound => (CurrentDay - 1) % 3 + 1;
        public PersonalityType CurrentPersonality { get; private set; } = PersonalityType.NORM;
        public bool PersonalitySelected => S != null && S.phase != CampaignStage.Introduction;
        public BattleConfiguration CurrentBattleConfiguration { get; private set; }
        public BattleReport LastBattleReport { get; private set; }
        public string LastBattleDrop => null;
        public System.Collections.Generic.IReadOnlyList<string> NextBattleItems => S?.battle?.battleItems ?? Array.Empty<string>();
        public CampaignPhase Phase => S == null ? CampaignPhase.Personality : S.phase switch
        {
            CampaignStage.Introduction => CampaignPhase.Personality,
            CampaignStage.Battle => CampaignPhase.Battle,
            CampaignStage.Result => CampaignPhase.Result,
            CampaignStage.Shop => CampaignPhase.Shop,
            CampaignStage.Ending => CampaignPhase.Complete,
            _ => CampaignPhase.Schedule
        };
        protected override void Awake()
        {
            base.Awake(); if (Instance != this) return;
            if (personalityConfig == null) personalityConfig = ScriptableObject.CreateInstance<PersonalityConfig>();
            EnsureServices();
        }
        private void EnsureServices()
        {
            if (store != null) return;
            codec = new UnityCampaignCodec();
            content = codec.Decode<CampaignContent>(Resources.Load<TextAsset>("Campaign/Content").text);
            var ruleAsset = Resources.Load<TextAsset>("Campaign/Rules"); rules = ruleAsset == null ? new CampaignRules() : codec.Decode<CampaignRules>(ruleAsset.text);
            store = new AtomicCampaignStore(System.IO.Path.Combine(Application.persistentDataPath, "CampaignV3"), codec);
            // User-selected packaged direct connection; optional gateway remains a fallback.
            string endpoint = Environment.GetEnvironmentVariable("FINALDEFENSE_DIALOGUE_GATEWAY");
            if (!string.IsNullOrWhiteSpace(EmbeddedAiConfig.ApiKey))
                dialogueService = new DeepSeekDialogueService(EmbeddedAiConfig.ApiKey, EmbeddedAiConfig.Endpoint, EmbeddedAiConfig.Model, codec);
            else if (!string.IsNullOrEmpty(endpoint) && Uri.TryCreate(endpoint, UriKind.Absolute, out var uri) && uri.Scheme == "https")
                dialogueService = new HttpDialogueGateway(uri, codec, () => Environment.GetEnvironmentVariable("FINALDEFENSE_GATEWAY_SESSION"));
        }
        public void ConfigureDialogueService(IDialogueService service)
        {
            DialogueSession?.Dispose(); if (dialogueService is IDisposable old) old.Dispose();
            dialogueService = service; if (Campaign != null) DialogueSession = new CampaignDialogueCoordinator(Campaign, service);
        }
        private void Attach(CampaignSnapshot state)
        {
            DialogueSession?.Dispose();
            Campaign = new CampaignService(content, rules, store, codec, state);
            DialogueSession = new CampaignDialogueCoordinator(Campaign, dialogueService);
            Campaign.Changed += snapshot =>
            {
                if (snapshot.phase == CampaignStage.Ending)
                    try { store.RecordUnlock(snapshot.endingId); PersistenceWarning = null; }
                    catch { PersistenceWarning = "结局已保存在本局；全局解锁写入失败，下次继续时重试"; }
            };
            CurrentBattleConfiguration = null;
            LastBattleReport = state.phase != CampaignStage.Result && state.phase != CampaignStage.Shop && state.phase != CampaignStage.Ending ? null : new BattleReport { won = state.outcome.reason == BattleEndReason.Victory, protectionLost = state.outcome.protectionLost };
            if (state.phase == CampaignStage.Ending) try { store.RecordUnlock(state.endingId); } catch { PersistenceWarning = "全局解锁保存失败"; }
        }
        public SaveMetadata[] ListCampaignSaves() { EnsureServices(); return store.List(); }
        public OperationResult StartCampaign(string saveId, bool overwrite = false)
        {
            EnsureServices();
            if (!overwrite && store.List().Any(s => s.saveId == saveId)) return LastOperation = OperationResult.Fail(OperationError.Conflict, "存档ID已存在，请选择新档");
            try
            {
                var state = CampaignService.NewState(saveId, content, rules, Guid.NewGuid().GetHashCode());
                var service = new CampaignService(content, rules, store, codec, state);
                if (overwrite) store.ArchiveSlot(saveId);
                store.Write(state); Attach(state);
                return LastOperation = new OperationResult { message = "新游戏已保存" };
            }
            catch { return LastOperation = OperationResult.Fail(OperationError.PersistenceFailed, "无法创建存档"); }
        }
        public OperationResult ContinueCampaign(string saveId)
        {
            EnsureServices();
            try { var state = store.Read(saveId); Attach(state); return LastOperation = new OperationResult { message = state.phase == CampaignStage.Battle ? "从战斗检查点继续；阵容、种子和补给不变" : "存档已读取" }; }
            catch (NotSupportedException) { return LastOperation = OperationResult.Fail(OperationError.UnsupportedVersion, "存档版本不支持，原文件已保留"); }
            catch { return LastOperation = OperationResult.Fail(OperationError.CorruptSave, "存档损坏或配置版本不匹配，可尝试备份"); }
        }
        public OperationResult RecoverCampaignBackup(string saveId)
        {
            EnsureServices();
            try { var backup = store.ReadBackup(saveId); var check = new CampaignService(content, rules, store, codec, backup); Attach(backup); return LastOperation = new OperationResult { message = "备份已读取，下一次操作将写入恢复后的进度" }; }
            catch { return LastOperation = OperationResult.Fail(OperationError.CorruptSave, "备份不可用，原文件未改变"); }
        }
        public GameSettings LoadSettings() { EnsureServices(); return store.LoadSettings(); }
        public OperationResult SaveSettings(GameSettings settings)
        { EnsureServices(); try { store.SaveSettings(settings); return new OperationResult { message = "设置已保存" }; } catch { return OperationResult.Fail(OperationError.PersistenceFailed, "设置保存失败"); } }
        public UnlockData LoadUnlocks() { EnsureServices(); return store.LoadUnlocks(); }
        public void BeginNewGame() => StartCampaign("save_" + Guid.NewGuid().ToString("N"));
        public bool LoadProgress() { var latest = ListCampaignSaves().LastOrDefault(s => s.error == null); return latest != null && ContinueCampaign(latest.saveId).Success; }
        public void SaveProgress() { if (Campaign != null) { try { store.Write(S); } catch { LastOperation = OperationResult.Fail(OperationError.PersistenceFailed, "保存失败"); } } }
        public void InitializeStats() { DialogueSession?.Cancel(); Campaign = null; CurrentBattleConfiguration = null; LastBattleReport = null; }
        private string Action(string name) => name + ":" + Guid.NewGuid().ToString("N");
        public void SetPersonality(PersonalityType type) { CurrentPersonality = type; if (Campaign != null) LastOperation = Campaign.CompleteIntroduction(Action("intro")); }
        public bool BeginBattle(BattleConfiguration configuration = null)
        {
            if (S?.phase != CampaignStage.Battle || S.battle == null) return false;
            // Preparation, roster selection and inventory consumption must already be committed by the real services.
            if (configuration != null) { if (!IsUsableBattleConfiguration(configuration)) return false; CurrentBattleConfiguration = configuration; }
            return true;
        }
        public bool CompleteBattle(BattleReport report)
        {
            LastOperation = OperationResult.Fail(OperationError.InvalidInput, "战报缺少run/battle/attempt身份；请提交Session的BattleOutcome");
            return false;
        }

        public void RecordBattleResult(bool won) { LastOperation = OperationResult.Fail(OperationError.InvalidInput, "请由战斗服务提交真实战果"); }
        public bool TryRetryBattle() => Campaign != null && (LastOperation = Campaign.RetryBattle(Action("retry"))).Success;
        public bool EnterShop() => Campaign != null && (LastOperation = Campaign.EnterShop(Action("shop"))).Success;
        public bool FinishShopping() => Campaign != null && (LastOperation = Campaign.NextDay(Action("next"))).Success;
        public void AdvanceDay() => FinishShopping();
        public int BattleItemCount(string key) => S?.inventory.FirstOrDefault(i => i.itemId == key)?.count ?? 0;
        public int ShopStock(string key) { var def = BattleItemDefs.Find(key); return def == null ? 0 : Math.Max(0, def.saleLimit - (S?.inventory.FirstOrDefault(i => i.itemId == key)?.purchased ?? 0)); }
        public bool BuyBattleItem(string key) => Campaign != null && (LastOperation = Campaign.Buy(Action("buy"), key)).Success;
        public void PrepareBattleItems(BattleConfiguration configuration)
        { if (configuration != null && S?.phase == CampaignStage.Battle) { configuration.battleItems = (string[])S.battle.battleItems.Clone(); CurrentBattleConfiguration = configuration; } }
        // Retired mutations remain source-compatible but cannot alter the campaign ledger.
        public bool GrantBattleItem(string key, int count = 1) => false;
        public void TakeGPADamage(int amount) { }
        public void AddGPA(int amount) { }
        public void ModifyStat(string stat, int delta) { }
        public bool SpendActionPoint(int cost = 1) => false;
        public void ResetActionPoints() { }
        public void AddGold(int amount) { }
        public bool SpendGold(int amount) => false;
        public void IncrementRetry() { }
        public void GradeUp() { }
        public float GetEduPowerBonus() => 0;
        public float GetDeterminationPenalty() => 0;
        public float GetEmotionDebuffReduction() => 0;
        public float GetStrengthActionBonus() => 0;
        public static bool IsUsableBattleConfiguration(BattleConfiguration configuration)
        {
            if (configuration == null) return false;
            try { BattleSimulation.ValidateConfiguration(configuration); return true; } catch (Exception) { return false; }
        }
        public LegacyMigrationPreview PreviewLegacyMigration(string newSaveId)
        {
            EnsureServices();
            if (!HasLegacySave) throw new InvalidOperationException("未找到旧存档");
            return LegacyCampaignMigration.Preview(PlayerPrefs.GetString(LegacySaveKey), newSaveId, content, rules, codec, Guid.NewGuid().GetHashCode());
        }
        public OperationResult MigrateLegacyCampaign(string newSaveId)
        {
            EnsureServices();
            if (!rules.allowLegacyRestartDayMigration) return OperationResult.Fail(OperationError.RulesPending, "当前规则未开放旧档迁移");
            if (store.List().Any(s => s.saveId == newSaveId)) return OperationResult.Fail(OperationError.Conflict, "目标槽已有存档");
            try
            {
                var preview = PreviewLegacyMigration(newSaveId);
                if (!preview.canMigrate) return OperationResult.Fail(OperationError.Conflict, preview.explanation);
                store.PreserveLegacy(PlayerPrefs.GetString(LegacySaveKey));
                var check = new CampaignService(content, rules, store, codec, preview.proposed);
                store.Write(preview.proposed); Attach(preview.proposed);
                return new OperationResult { message = preview.explanation };
            }
            catch { return OperationResult.Fail(OperationError.CorruptSave, "迁移未完成，原存档未修改"); }
        }
        public OperationResult InspectLegacySave()
        {
            EnsureServices(); if (!HasLegacySave) return OperationResult.Fail(OperationError.NotFound, "未找到旧存档");
            try
            {
                string raw = PlayerPrefs.GetString(LegacySaveKey); store.PreserveLegacy(raw);
                var legacy = codec.Decode<LegacyProgress>(raw);
                if (legacy == null || (legacy.version != 1 && legacy.version != 2)) return OperationResult.Fail(OperationError.UnsupportedVersion, "旧存档版本不支持，已备份原文");
                return new OperationResult { message = "旧存档已保留，GPA=" + ((legacy.version == 1 ? legacy.gpa * 100 : legacy.gpaHundredths) / 100m) + "。旧档缺少NPC、预约和对手历史；D15迁移策略未定，不伪造续玩状态，请保留旧档或另开新局。" };
            }
            catch { return OperationResult.Fail(OperationError.CorruptSave, "旧存档读取失败，未修改原存档"); }
        }
        [Serializable] private sealed class LegacyProgress { public int version, gpa, gpaHundredths, day; }
        private void OnDestroy() { DialogueSession?.Dispose(); if (dialogueService is IDisposable disposable) disposable.Dispose(); }
    }
}
