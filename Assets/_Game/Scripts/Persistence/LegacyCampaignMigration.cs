using System;
using System.Linq;
using FinalDefense.Campaign;
using FinalDefense.Contracts;
using FinalDefense.Shop;

namespace FinalDefense.Persistence
{
    [Serializable] public sealed class LegacyMigrationPreview
    { public bool canMigrate; public string explanation; public int sourceVersion, sourceDay, sourceGpa; public CampaignSnapshot proposed; }
    public static class LegacyCampaignMigration
    {
        [Serializable] private sealed class LegacyItem { public string key; public int count, purchased; }
        [Serializable] private sealed class LegacySave
        {
            public int version, gpa, gpaHundredths, day, phase;
            public bool itemsPrepared;
            public LegacyItem[] inventory;
        }
        // Proposal only. Caller must confirm the migration policy; no existing file is ever changed here.
        public static LegacyMigrationPreview Preview(string raw, string newSaveId, CampaignContent content, CampaignRules rules, IDataCodec codec, int seed)
        {
            var old = codec.Decode<LegacySave>(raw);
            if (old == null || (old.version != 1 && old.version != 2)) throw new NotSupportedException("Unknown legacy version");
            long gpa = old.version == 1 ? (long)old.gpa * 100 : old.gpaHundredths;
            if (old.day < 1 || old.day > 28 || gpa < 0 || gpa > 10000 || old.phase < 0 || old.phase > 5) throw new ArgumentException("Invalid legacy data");
            var result = new LegacyMigrationPreview { sourceVersion = old.version, sourceDay = old.day, sourceGpa = (int)gpa };
            if (old.phase != 0 && old.phase != 1 || old.itemsPrepared)
            {
                result.explanation = "旧档处于战斗/结算/商店/终局或已消费补给，缺少对手、行动及结算身份，不能无损迁移。原档保留，请另开新局。";
                return result;
            }
            var state = CampaignService.NewState(newSaveId, content, rules, seed);
            state.gpa = (int)gpa; state.day = old.day; state.dailyNewsIds = CampaignService.SelectNews(content, seed, old.day);
            // Explicitly retain a notice instead of fabricating past dialogue, ranks or battle identities.
            state.migrationNotice = "由v" + old.version + "迁移：保留第" + old.day + "天、原GPA与库存；当日从引导/重新预约开始。NPC按新策划初值初始化，连胜和关系/排名历史无法恢复。旧档与其备份仍保留。";
            var entries = old.inventory ?? Array.Empty<LegacyItem>();
            if (entries.Any(i => i == null || i.key == null) || entries.Select(i => i.key).Distinct().Count() != entries.Length) throw new ArgumentException("Invalid legacy inventory");
            foreach (var item in entries)
            {
                var def = BattleItemDefs.Find(item.key);
                if (def == null || !def.IsBattleItem || item.count < 0 || item.count > def.stackLimit || item.purchased < 0 || item.purchased > def.saleLimit) throw new ArgumentException("Unknown or invalid legacy item");
                var target = state.inventory.First(i => i.itemId == item.key); target.count = item.count; target.purchased = item.purchased;
            }
            result.canMigrate = true; result.explanation = state.migrationNotice; result.proposed = state; return result;
        }
    }
}
