using System;
using System.Collections.Generic;
using System.Linq;

namespace FinalDefense.Combat
{
    public static class BattleFactory
    {
        // Keep the configured stage's wave times and counts while assigning its
        // eight groups to the eight identities left after the player's draft.
        public static BattleConfiguration CreateCampaignDuel(BattleConfiguration map,UnitDefinition[] towers,UnitDefinition[] enemies,IEnumerable<string> selectedIds)
        {
            var result=CreateDuel(map,towers,enemies,selectedIds);
            var opponents=result.enemies.OrderBy(e=>e.AtLevel(e.hp,1)).ThenBy(e=>e.id).ToArray();
            result.groups=map.groups.Select((group,index)=>new SpawnGroup {
                wave=group.wave,waveStart=group.waveStart,delay=group.delay,interval=group.interval,
                count=group.count,enemyId=opponents[index%opponents.Length].id,
                statMultiplier=group.statMultiplier,goalDamage=group.goalDamage
            }).ToArray();
            result.title="期末保卫战 · 对决";
            result.notes="采用关卡的波次时间与数量，玩家选择八种棋子，其余八种组成敌方阵容。";
            return result;
        }
        // The pre-battle flow can supply a roster here. This practice match uses
        // the unpicked eight; it does not pretend to call an AI drafting service.
        public static BattleConfiguration CreateDuel(BattleConfiguration map,UnitDefinition[] towers,UnitDefinition[] enemies,IEnumerable<string> selectedIds)
        {
            var selected=selectedIds.Distinct().ToArray();
            if(selected.Length!=8)throw new ArgumentException("请选择 8 种棋子");
            var roster=selected.Select(id=>towers.First(t=>t.id==id)).ToArray();
            var skills=new HashSet<string>(roster.Select(t=>t.skill.id));
            var opponent=enemies.Where(e=>!skills.Contains(e.skill.id)).ToArray();
            if(opponent.Length!=8)throw new ArgumentException("敌我阵容必须互不重复");
            var groups=new List<SpawnGroup>();
            for(int i=0;i<opponent.Length;i++)groups.Add(new SpawnGroup{wave=i/2+1,waveStart=new float[]{0,40,85,135}[i/2],delay=i%2*8+2,enemyId=opponent[i].id,count=2,interval=4,statMultiplier=1});
            return new BattleConfiguration {title="期末保卫战 · 自选对决",source=map.source,
                notes="自选对决用于体验飞书全部棋子；数量和间隔暂用练习配置，敌我身份互斥。",
                protection=map.protection,initialCost=map.initialCost,maxCost=map.maxCost,costPerSecond=map.costPerSecond,maxDeployed=map.maxDeployed,redeploySeconds=map.redeploySeconds,
                path=map.path,ground=map.ground,highGround=map.highGround,towers=roster,enemies=opponent,groups=groups.ToArray(),
                battleItems=map.battleItems==null?null:(string[])map.battleItems.Clone()};
        }
    }
}
