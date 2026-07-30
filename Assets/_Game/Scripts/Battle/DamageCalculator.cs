using UnityEngine;

namespace FinalDefense.Battle
{
    public static class DamageCalculator
    {
        public static int Calculate(int attack, int defense)
        {
            int baseDamage = attack - defense;
            int minDamage = Mathf.Max(1, Mathf.RoundToInt(attack * 0.05f));
            return Mathf.Max(baseDamage, minDamage);
        }
    }
}
