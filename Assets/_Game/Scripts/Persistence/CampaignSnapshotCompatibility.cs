using System;
using FinalDefense.Contracts;

namespace FinalDefense.Persistence
{
    public static class CampaignSnapshotCompatibility
    {
        // Unity serializes inline reference fields by value; null returns as an empty object.
        // Only wholly absent identities are canonical null. Partial/mismatched identities remain
        // intact for CampaignService.Validate to reject rather than hiding corrupt save data.
        public static void NormalizeEmptyCheckpoints(CampaignSnapshot s)
        {
            if (s == null) return;
            if (s.dialogue != null && Empty(s.dialogue.conversationId, s.dialogue.npcId, s.dialogue.locationId)) s.dialogue = null;
            if (s.draftRequest != null && Empty(s.draftRequest.runId, s.draftRequest.battleId)) s.draftRequest = null;
            if (s.draft != null && Empty(s.draft.battleId, s.draft.opponentId)) s.draft = null;
            if (s.battle != null && Empty(s.battle.runId, s.battle.battleId, s.battle.attemptId)) s.battle = null;
            if (s.outcome != null && Empty(s.outcome.runId, s.outcome.battleId, s.outcome.attemptId)) s.outcome = null;
            if (s.appointments != null)
                for (int i = 0; i < s.appointments.Length; i++)
                    if (s.appointments[i] != null && Empty(s.appointments[i].npcId, s.appointments[i].locationId)) s.appointments[i] = null;
        }
        private static bool Empty(params string[] values) => Array.TrueForAll(values, string.IsNullOrEmpty);
    }
}
