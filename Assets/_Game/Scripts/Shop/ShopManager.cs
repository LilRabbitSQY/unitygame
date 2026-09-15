using UnityEngine;
using FinalDefense.Core;

namespace FinalDefense.Shop
{
    public class ShopManager : MonoBehaviour
    {
        public BattleShopItem[] AvailableItems => BattleItemDefs.ShopItems;
        private bool leaving;
        public bool BuyItem(BattleShopItem item)
            => !leaving && item != null && GameManager.Instance != null && GameManager.Instance.BuyBattleItem(item.key);
        public void LeaveShop()
        {
            if (leaving) return;
            var gm = GameManager.Instance;
            if (gm == null) return;
            if (gm.IsGameComplete)
            {
                leaving = true;
                SceneLoader.LoadResult();
                return;
            }
            if (!gm.FinishShopping()) return;
            leaving = true;
            if (gm.IsGameComplete) SceneLoader.LoadResult();
            else SceneLoader.LoadSchedule();
        }
    }
}
