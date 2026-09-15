using System;
using System.Linq;

namespace FinalDefense.Shop
{
    public sealed class BattleShopItem
    {
        public readonly string key, name, description;
        public readonly int priceHundredths, stackLimit, saleLimit, favorGain;
        public readonly string targetNpc;
        public bool IsBattleItem => favorGain == 0;
        public float Price => priceHundredths / 100f;
        public BattleShopItem(string key, string name, string description, int priceHundredths = 0, int stackLimit = 99, int saleLimit = 99, int favorGain = 0, string targetNpc = null)
        { this.key = key; this.name = name; this.description = description; this.priceHundredths = priceHundredths; this.stackLimit = stackLimit; this.saleLimit = priceHundredths > 0 ? saleLimit : 0; this.favorGain = favorGain; this.targetNpc = targetNpc; }
    }
    public static class BattleItemDefs
    {
        public static readonly BattleShopItem[] All =
        {
            new BattleShopItem("signed_fan_art", "同人签名板", "下局直伤棋子攻速 +8%"),
            new BattleShopItem("makeup_sample", "美妆小样", "下局治疗棋子生命上限 +10%"),
            new BattleShopItem("competition_manual", "竞赛手册", "下局追击棋子攻击 +5%"),
            new BattleShopItem("balulu_figure", "盲盒手办", "下局盾反棋子防御 +10%"),
            new BattleShopItem("custom_keyboard", "客制化键盘", "下局灼烧棋子施加灼烧时额外 +1 层"),
            new BattleShopItem("coffee_coupon", "手冲咖啡券", "下局中毒棋子的持续技能和毒地块时长 +15%"),
            new BattleShopItem("leave_note", "请假条", "下局敌方首波延迟 3 秒出现"),
            new BattleShopItem("recommendation_letter", "院长推荐信", "下局战斗胜利时额外 +1 GPA"),
            new BattleShopItem("espresso_focus", "浓缩聪明水", "下局所有我方棋子攻速 +15%", 20),
            new BattleShopItem("sparkling_focus", "气泡聪明水", "下局所有我方棋子防御 +10%", 10),
            new BattleShopItem("gentle_focus", "温和聪明水", "下局所有敌人攻击 −5%", 30),
            new BattleShopItem("endurance_focus", "续航聪明水", "下局每个我方棋子首次倒地前保留生命并回复 100 HP", 80),
            new BattleShopItem("heart_card", "心意卡", "赠送任意可攻略NPC，好感 +3", 100, 99, 99, 3),
            new BattleShopItem("fan_comic", "同人漫画", "赠送刘若水，好感 +5", 99, 1, 1, 5, "liu_ruoshui"),
            new BattleShopItem("lipstick", "新品口红", "赠送万思瑞，好感 +5", 99, 1, 1, 5, "wan_sirui"),
            new BattleShopItem("theatre_ticket", "剧票", "赠送骆洋，好感 +5", 99, 1, 1, 5, "luo_yang"),
            new BattleShopItem("balulu_secret", "balulu隐藏款", "赠送项臻，好感 +5", 99, 1, 1, 5, "xiang_zhen"),
            new BattleShopItem("sports_wristband", "运动护腕", "赠送任飞，好感 +5", 99, 1, 1, 5, "ren_fei"),
            new BattleShopItem("coffee_beans", "精品咖啡豆", "赠送明杉，好感 +5", 99, 1, 1, 5, "ming_shan")
        };
        public static BattleShopItem[] BattleItems => All.Where(item => item.IsBattleItem).ToArray();
        public static BattleShopItem Find(string key) => All.FirstOrDefault(item => item.key == key);
        public static BattleShopItem[] ShopItems => All.Where(item => item.priceHundredths > 0).ToArray();
        public static string Name(string key) => Find(key)?.name ?? key;
        public static string Effect(string key) => Find(key)?.description ?? "";
    }
}
