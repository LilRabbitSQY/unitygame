using UnityEngine;

namespace FinalDefense.Data
{
    [CreateAssetMenu(menuName = "FinalDefense/Shop Item Data")]
    public class ShopItemData : ScriptableObject
    {
        public string itemName;
        public string description;
        public int price = 20;
        public int emotionDelta;
        public int strengthDelta;
        public int eduPowerDelta;
        public int determinationDelta;
        public int gpaDelta;
        public int actionPointDelta;
    }
}
