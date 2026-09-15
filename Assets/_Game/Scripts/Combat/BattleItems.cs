using System;
using System.Collections.Generic;

namespace FinalDefense.Combat
{
    public sealed partial class BattleSimulation
    {
        // Snapshot the loadout once. Inventory counts never multiply a battle's effect.
        private readonly HashSet<string> carriedBattleItems = new HashSet<string>(StringComparer.Ordinal);
        private readonly HashSet<int> enduranceUsed = new HashSet<int>();
        private int firstBattleWave;

        private void InitializeBattleItems()
        {
            if (Config.battleItems != null)
                foreach (var key in Config.battleItems)
                    if (!string.IsNullOrEmpty(key)) carriedBattleItems.Add(key);
            firstBattleWave = int.MaxValue;
            foreach (var group in Config.groups)
                if (group.count > 0) firstBattleWave = Math.Min(firstBattleWave,group.wave);
        }

        private float BattleItemModifier(UnitState unit, Stat stat)
        {
            if (carriedBattleItems.Count == 0) return 0;
            if (unit.enemy)
                return stat == Stat.AttackPercent && carriedBattleItems.Contains("gentle_focus") ? -.05f : 0;
            float bonus = 0;
            if (stat == Stat.AttackSpeed)
            {
                if (carriedBattleItems.Contains("espresso_focus")) bonus += .15f;
                if (unit.Tag == "直伤" && carriedBattleItems.Contains("signed_fan_art")) bonus += .08f;
            }
            else if (stat == Stat.MaxHP && unit.Tag == "治疗" && carriedBattleItems.Contains("makeup_sample"))
                bonus += .10f;
            else if (stat == Stat.AttackPercent && unit.Tag == "追击" && carriedBattleItems.Contains("competition_manual"))
                bonus += .05f;
            else if (stat == Stat.DefensePercent)
            {
                if (carriedBattleItems.Contains("sparkling_focus")) bonus += .10f;
                if (unit.Tag == "盾反" && carriedBattleItems.Contains("balulu_figure")) bonus += .10f;
            }
            return bonus;
        }

        private float BattleItemWaveDelay(SpawnGroup group)
            => group.wave == firstBattleWave && carriedBattleItems.Contains("leave_note") ? 3 : 0;

        private int BattleItemBurnStacks(UnitState source, int stacks)
            => stacks > 0 && source != null && !source.enemy && source.Tag == "灼烧"
                && carriedBattleItems.Contains("custom_keyboard") ? stacks+1 : stacks;

        // "中毒棋子持续时间 +15%": extend its active skill and each generated poison tile
        // from their own base duration, once each. Contact poison, enemy skills and other statuses stay unchanged.
        private float BattleItemPoisonDuration(UnitState source, float seconds)
            => source != null && !source.enemy && source.Tag == "中毒"
                && carriedBattleItems.Contains("coffee_coupon") ? seconds*1.15f : seconds;

        private float TowerSkillDuration(UnitState unit)
            => BattleItemPoisonDuration(unit,unit.definition.skill.duration);

        private bool TryPreventItemDefeat(UnitState unit)
        {
            if (unit.enemy || unit.hp > 0 || !carriedBattleItems.Contains("endurance_focus") || !enduranceUsed.Add(unit.id))
                return false;
            // This is a fixed emergency restoration, independent of healing-rate modifiers.
            // Keep the id in enduranceUsed through Down/TryRedeploy so a revived unit cannot renew it.
            int restored = Math.Min(100,MaxHP(unit));
            unit.hp = restored;
            unit.TotalHealing += restored;
            Report.healing += restored;
            Emit("item-save",unit,"续航聪明水 · 回复生命",restored);
            return true;
        }

        private int BattleItemBonusGpa(bool won)
            => won && carriedBattleItems.Contains("recommendation_letter") ? 1 : 0;
    }
}
