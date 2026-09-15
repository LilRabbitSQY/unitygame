using System;

namespace FinalDefense.Combat
{
    public struct DamagePacket
    {
        public double attack, skillMultiplier, flat, attackPercent, damageBonus;
        public double critChance, critDamage;
        public bool critical;
        public static DamagePacket Basic(double attack) => new DamagePacket { attack=attack, skillMultiplier=1, critChance=.05, critDamage=.5 };
    }
    public struct DefensePacket
    {
        public double defense, skillMultiplier, flat, defensePercent;
        public double vulnerability, damageTaken, reduction, shelter;
        public static DefensePacket Basic(double defense) => new DefensePacket { defense=defense, skillMultiplier=1 };
    }
    public static class CombatMath
    {
        public static int Damage(DamagePacket hit, DefensePacket target)
        {
            double damage = (hit.attack*hit.skillMultiplier+hit.flat)*(1+hit.attackPercent)*(1+hit.damageBonus)
                *(1+target.vulnerability)*(1+target.damageTaken)*(1-target.reduction)*(1-target.shelter)
                -(target.defense*target.skillMultiplier+target.flat)*(1+target.defensePercent);
            if (damage < 0) damage = Math.Max(0,hit.attack)*.05;
            if (hit.critical) damage *= 1+hit.critDamage;
            return RoundDamage(damage);
        }
        public static int RoundDamage(double damage) => (int)Math.Max(0,Math.Round(Math.Round(damage,2,MidpointRounding.AwayFromZero),0,MidpointRounding.AwayFromZero));
        // float -> decimal restores the meaningful decimal precision before midpoint rounding.
        public static int RoundDamage(float damage) => (int)Math.Max(0m,Math.Round(Math.Round((decimal)damage,2,MidpointRounding.AwayFromZero),0,MidpointRounding.AwayFromZero));
        public static int AttackFrames(int frames, float speedBonus) => Math.Max(1,(int)Math.Floor(frames*(1.0-speedBonus)+.00001));
        public static bool InRange(float sx,float sy,float fx,float fy,float tx,float ty,int width,int depth,bool radial=false)
        {
            float dx=tx-sx,dy=ty-sy;
            double left=-Math.Floor((width-1)/2.0)-.5, right=left+width;
            if(radial)
            {
                double bottom=-Math.Floor((depth-1)/2.0)-.5;
                return dx>=left && dx<right && dy>=bottom && dy<bottom+depth;
            }
            float forward=dx*fx+dy*fy, side=dx*(-fy)+dy*fx;
            // Front ranges exclude the caster's own row. Even widths bias one cell to local right.
            return forward>=.5f && forward<depth+.5f && side>=left && side<right;
        }
    }
}
