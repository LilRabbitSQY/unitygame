using UnityEngine;
using FinalDefense.Core;
using FinalDefense.Data;

namespace FinalDefense.Shop
{
    public class ShopManager : MonoBehaviour
    {
        [SerializeField] private ShopItemData[] availableItems;

        public ShopItemData[] AvailableItems => availableItems;

        public bool BuyItem(ShopItemData item)
        {
            var gm = GameManager.Instance;
            if (gm == null) return false;
            if (!gm.SpendGold(item.price)) return false;

            if (item.emotionDelta != 0) gm.ModifyStat("emotion", item.emotionDelta);
            if (item.strengthDelta != 0) gm.ModifyStat("strength", item.strengthDelta);
            if (item.eduPowerDelta != 0) gm.ModifyStat("eduPower", item.eduPowerDelta);
            if (item.determinationDelta != 0) gm.ModifyStat("determination", item.determinationDelta);
            if (item.gpaDelta != 0) gm.AddGPA(item.gpaDelta);

            return true;
        }

        public void LeaveShop()
        {
            GameManager.Instance?.AdvanceDay();
            GameManager.Instance?.ResetActionPoints();
            SceneLoader.LoadSchedule();
        }
    }
}
