using System;
using System.Linq;

namespace FinalDefense.Campaign
{
    [Serializable] public sealed class NpcDefinition
    {
        public string id, name, portraitKey, style, persona, source, endingLine; public int legacyId, initialFavor, initialGpa; public bool romance;
        public string[] positiveTopics, negativeTopics;
    }
    [Serializable] public sealed class LocationDefinition { public string id, name, source; }
    [Serializable] public sealed class NewsDefinition { public string id, text, source, npcId; public int firstDay, lastDay; }
    [Serializable] public sealed class CampaignContent
    {
        public string version, worldText, worldSource, endingSuccess, endingFailure, endingSource;
        public NpcDefinition[] npcs; public LocationDefinition[] locations; public NewsDefinition[] news; public string[] emotionTags;
        public NpcDefinition Npc(string id) => npcs.FirstOrDefault(n => n.id == id);
        public void Validate()
        {
            if (string.IsNullOrWhiteSpace(version) || npcs == null || npcs.Length != 8 || npcs.Count(n => n != null && n.romance) != 6
                || npcs.Any(n => n == null || string.IsNullOrWhiteSpace(n.id) || n.positiveTopics == null || n.negativeTopics == null)
                || npcs.Select(n => n.id).Distinct().Count() != 8 || locations == null || locations.Length != 6
                || locations.Any(l => l == null || string.IsNullOrWhiteSpace(l.id)) || locations.Select(l => l.id).Distinct().Count() != 6
                || emotionTags == null || emotionTags.Length != 8) throw new ArgumentException("Invalid campaign content");
        }
    }
    // These choices are explicitly versioned proposals until the design owner confirms them.
    [Serializable] public sealed class CampaignRules
    {
        public string version = "provisional-2026-09-15.2"; public bool approved;
        public bool tiedFirstWins = true, giftsShareDailyCap = true, thresholdAfterTurn = true, repeatCompanion = true;
        public int streakBonusFrom = 2, playerMaxGpa = 10000, npcMinGpa = 6000, npcMaxGpa = 10000;
        public int favorSixtyBand = 1, lowTurns = 3, mediumTurns = 6, highTurns = 10;
        public int matchCycleLength = 7;
        public bool allowLegacyRestartDayMigration = true;
        public bool retriesAllowed = true; public int retryPrice = 100;
        // -1 means unresolved, not a zero-percent claim. No random rewards run until configured.
        public int cameoChancePerTenThousand = 1500, topicDropChancePerTenThousand = 3500;
        public int battleFavorChancePerTenThousand = 2000, npcNewsChancePerTenThousand = 2500, npcNewsFavorThreshold = 30;
        public string probabilitySource = "User authorized provisional implementation; RULE-DECISIONS-PART-1.md";
        public void Validate()
        {
            if (string.IsNullOrWhiteSpace(version) || streakBonusFrom < 2 || playerMaxGpa < 7000 || npcMinGpa > npcMaxGpa
                || lowTurns < 1 || mediumTurns < lowTurns || highTurns < mediumTurns || highTurns > 10
                || favorSixtyBand < 0 || favorSixtyBand > 2 || matchCycleLength < 1 || retryPrice < 0
                || cameoChancePerTenThousand < -1 || cameoChancePerTenThousand > 10000
                || battleFavorChancePerTenThousand < 0 || battleFavorChancePerTenThousand > 10000
                || npcNewsChancePerTenThousand < 0 || npcNewsChancePerTenThousand > 10000 || npcNewsFavorThreshold < 0 || npcNewsFavorThreshold > 100
                || topicDropChancePerTenThousand < -1 || topicDropChancePerTenThousand > 10000) throw new ArgumentException("Invalid campaign rules");
        }
    }
}
