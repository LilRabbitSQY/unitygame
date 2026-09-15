using System;
using System.Threading;
using System.Threading.Tasks;

namespace FinalDefense.Contracts
{
    public static class ContractVersion { public const int Current = 1; }
    public enum CampaignStage { Introduction, Booking, Dialogue, Matching, Preparation, Battle, Result, Shop, Ending }
    public enum OperationError { None, Duplicate, InvalidInput, WrongPhase, NotFound, InsufficientFunds, InventoryFull, OutOfStock, NoEffect, Conflict, PersistenceFailed, InvalidResponse, Cancelled, Timeout, Unavailable, RulesPending, UnsupportedVersion, CorruptSave }
    [Serializable] public sealed class OperationResult
    {
        public OperationError error; public string message; public int favorDelta, gpaDelta; public string itemId;
        public bool Success => error == OperationError.None || error == OperationError.Duplicate;
        public static OperationResult Fail(OperationError error, string message) => new OperationResult { error = error, message = message };
    }
    [Serializable] public sealed class Appointment { public string npcId, locationId; }
    [Serializable] public sealed class NpcSnapshot { public string npcId; public int gpa, favor, dailyGain; public long reachedSequence; }
    [Serializable] public sealed class InventoryEntry { public string itemId; public int count, purchased; }
    [Serializable] public sealed class DialogueLine { public string role, text; }
    [Serializable] public sealed class DialogueCheckpoint
    {
        public string conversationId, npcId, locationId, cameoId; public int turns; public bool opened, finished;
        public string emotion = "neutral"; public string[] hints = Array.Empty<string>();
        public string[] grantedItems = Array.Empty<string>(); public DialogueLine[] history = Array.Empty<DialogueLine>();
    }
    [Serializable] public sealed class OperationReceipt { public string actionId, fingerprint; public OperationResult result; }
    [Serializable] public sealed class RankingEntry { public string id; public int gpa, rank; }
    [Serializable] public sealed class DailyRecord { public int day, gpa, streak; public RankingEntry[] ranking; public string opponentId, reason; }
    [Serializable] public sealed class DraftRequest
    {
        public string runId, battleId, rulesVersion; public int day, seed, cycleLength, playerGpa;
        public NpcSnapshot[] candidates; public string[] history;
    }
    [Serializable] public sealed class DraftSnapshot
    {
        public string battleId, opponentId, rulesVersion; public int seed; public bool playerFirst, locked;
        public string[] playerUnits = Array.Empty<string>(), enemyUnits = Array.Empty<string>(), availableUnits = Array.Empty<string>();
    }
    [Serializable] public sealed class BattleStartContext
    {
        public string runId, battleId, attemptId, levelId, contentVersion; public int seed, initialCost, protection;
        public string[] playerUnits, enemyUnits, battleItems;
    }
    public enum BattleEndReason { Victory, Defeat, Forfeit }
    [Serializable] public sealed class BattleOutcome
    {
        public string runId, battleId, attemptId; public BattleEndReason reason; public int protectionLost;
        public string report; public string[] effectEvents = Array.Empty<string>();
    }
    [Serializable] public sealed class CampaignSnapshot
    {
        public int version = 3; public string saveId, runId, rulesVersion, contentVersion, migrationNotice;
        public int day = 1, gpa = 7000, streak, seed, slot, wins, losses; public long sequence;
        public CampaignStage phase; public Appointment[] appointments = new Appointment[3];
        public NpcSnapshot[] npcs; public InventoryEntry[] inventory;
        public DialogueCheckpoint dialogue; public DraftRequest draftRequest; public DraftSnapshot draft; public BattleStartContext battle;
        public BattleOutcome outcome; public bool daySettled; public string endingId, companionId, lastBattleSummary;
        public DailyRecord[] trends = Array.Empty<DailyRecord>(); public string[] matchHistory = Array.Empty<string>();
        public string[] dailyNewsIds = Array.Empty<string>();
        public OperationReceipt[] receipts = Array.Empty<OperationReceipt>(); public string[] readNews = Array.Empty<string>();
    }
    [Serializable] public sealed class DialogueTurnRequest
    {
        public string runId, conversationId, turnId, npcId, locationId, input, persona, lastBattleSummary;
        public int favor, maxTurns, maxCharacters = 120; public bool opening;
        public string[] allowedTopics, hints; public DialogueLine[] history;
    }
    public enum DialogueStatus { Completed, Failed, Cancelled }
    [Serializable] public sealed class DialogueTurnResult
    {
        public string conversationId, turnId, text, emotion, topic; public string[] hints;
        public DialogueStatus status; public bool endConversation;
    }
    public interface IDialogueService
    {
        Task<DialogueTurnResult> SendAsync(DialogueTurnRequest request, Action<string> onChunk, CancellationToken cancellation);
    }
    public interface ICampaignStore
    {
        void Write(CampaignSnapshot snapshot);
        CampaignSnapshot Read(string saveId);
        SaveMetadata[] List();
    }
    public interface IDataCodec { string Encode<T>(T value); T Decode<T>(string value); }
    [Serializable] public sealed class SaveMetadata { public string saveId, runId, error; public int day, gpa; public CampaignStage phase; }
    [Serializable] public sealed class GameSettings { public int version = 1, width = 1920, height = 1080; public float music = 1, sound = 1; public bool fullscreen = true; }
    [Serializable] public sealed class UnlockData { public int version = 1; public string[] endings = Array.Empty<string>(); }
    public interface IUnitCatalog { UnitInfo[] GetUnits(); }
    [Serializable] public sealed class UnitInfo { public string id, name, skillDescription; public int level, hp, attack, defense; }
    // Simulation owns implementation and ticking; the UI owns exactly one host.
    public enum BattleCommandKind { Deploy, Rotate, Upgrade, Withdraw, Redeploy, Pause, Exit }
    [Serializable] public sealed class BattleCommand { public string commandId, unitId; public BattleCommandKind kind; public int x, y, direction; }
    [Serializable] public sealed class BattleUnitState { public string id; public int hp, level, x, y; }
    [Serializable] public sealed class BattleEvent { public string id, kind, sourceId, targetId; public int amount; }
    public interface IBattleSession { OperationResult Execute(BattleCommand command); BattleUnitState[] Units { get; } BattleEvent[] DrainEvents(); void Tick(float seconds); }
}
