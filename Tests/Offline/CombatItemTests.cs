using System;
using System.Linq;
using System.Text.Json;
using FinalDefense.Combat;
using static BattleLogicTests;

internal static class CombatItemTests
{
    private static BattleConfiguration Config(params string[] items)
    {
        var towers = CombatDataTests.LoadUnits("Towers");
        // Cheap fixtures isolate effects; full campaign replays use original costs.
        foreach (var d in towers) { d.deployCost = 0; d.upgradeCost = new[] {0,0,0}; }
        var enemy = new UnitDefinition { id="item-target", tag="灼烧", hp=new[]{1000000},
            attack=new[]{100}, defense=new[]{0}, moveSpeed=0, attackFrames=0 };
        return new BattleConfiguration { battleItems=items, initialCost=99, maxDeployed=20,
            path=new[]{new Cell(0,0),new Cell(100,0)},
            ground=Enumerable.Range(0,20).Select(x=>new Cell(x,0)).ToArray(),
            highGround=Enumerable.Range(0,20).Select(x=>new Cell(x,1)).ToArray(),
            towers=towers, enemies=new[]{enemy}, groups=new[]{new SpawnGroup{enemyId=enemy.id,wave=1,count=1}} };
    }
    private static UnitState Deploy(BattleSimulation s,string id,int x=0)
    {
        var def=s.Config.towers.First(t=>t.id=="tower_"+id);
        var unit=s.TryDeploy(def.id,x,def.highGround?1:0);
        True(unit!=null,"Legal fixture deployment: "+id); return unit;
    }
    private static void Frames(BattleSimulation s,int n)=>CombatSimulationTests.Frames(s,n);
    internal static void RunAll()
    {
        Run("item stats: tag filters, additive categories, duplicates and construction snapshot",()=>{
            var config=Config("signed_fan_art","signed_fan_art","makeup_sample","competition_manual",
                "balulu_figure","espresso_focus","sparkling_focus","gentle_focus");
            string before=JsonSerializer.Serialize(config.towers,CombatDataTests.JsonOptions);
            var s=new BattleSimulation(config); config.battleItems[0]="unknown";
            UnitState direct=Deploy(s,"direct_crit"), healer=Deploy(s,"heal_aura",1), pursuit=Deploy(s,"pursuit_strike",2), shield=Deploy(s,"counter_shield",3);
            Near(.23,s.AttackSpeedBonus(direct),"8% direct and 15% all-friendly add once");
            Near(.15,s.AttackSpeedBonus(healer),"Direct-only bonus excludes healer");
            Equal(BattleSimulation.Round(healer.definition.hp[0]*1.1f),s.MaxHP(healer),"Healer gains 10% maximum HP");
            Near(s.MaxHP(healer),healer.hp,"Deployment fills boosted HP");
            Near(pursuit.definition.attack[0]*1.05f,s.AttackValue(pursuit),"Pursuit gets 5% ATK");
            Near(shield.definition.attack[0],s.AttackValue(shield),"Other tags do not get pursuit ATK");
            Near(shield.definition.defense[0]*1.2f,s.DefenseValue(shield),"Two defense bonuses add");
            Near(direct.definition.defense[0]*1.1f,s.DefenseValue(direct),"Nonshield gets only global defense");
            Near(95,s.AttackValue(s.Enemies[0]),"Enemy attack reduction is 5%");
            Near(0,s.AttackSpeedBonus(s.Enemies[0]),"Friendly speed items exclude enemy");
            True(before==JsonSerializer.Serialize(config.towers,CombatDataTests.JsonOptions),"Shared definitions remain unchanged");
        });
        Run("keyboard: real burn skill adds one stack per application from friendly burn source",()=>{
            var plain=new BattleSimulation(Config()); var carried=new BattleSimulation(Config("custom_keyboard","custom_keyboard"));
            UnitState a=Deploy(plain,"burn_stack"), b=Deploy(carried,"burn_stack");
            a.sp=a.definition.skill.spCost; b.sp=b.definition.skill.spCost;
            a.attackTimer=b.attackTimer=10000;
            Frames(plain,16);Frames(carried,16);
            plain.AttackTargets(a,new System.Collections.Generic.List<UnitState>{plain.Enemies[0]});
            carried.AttackTargets(b,new System.Collections.Generic.List<UnitState>{carried.Enemies[0]});
            Frames(plain,16);Frames(carried,16);
            True(plain.Enemies[0].burns.Count>0,"Production active skill actually applied burn");
            Equal(plain.Enemies[0].burns.Count+1,carried.Enemies[0].burns.Count,"One extra stack, not one per duplicate inventory key");
            int count=carried.Enemies[0].burns.Count;var pursuit=Deploy(carried,"pursuit_strike",2);
            carried.Burn(carried.Enemies[0],2,10,pursuit);Equal(count+2,carried.Enemies[0].burns.Count,"Nonburn source excluded");
            carried.Burn(b,2,10,carried.Enemies[0]);Equal(2,b.burns.Count,"Enemy burn source excluded");
        });
        Run("coffee: real poison skill and generated tiles extend by 15 percent exactly once",()=>{
            var s=new BattleSimulation(Config("coffee_coupon","coffee_coupon"));var poison=Deploy(s,"poison_amp");
            poison.sp=poison.definition.skill.spCost;poison.attackTimer=10000;Frames(s,1);
            True(poison.skillActive,"Real poison skill activated");Near(23,poison.skillRemaining,"20-second poison skill becomes 23");
            Frames(s,15);True(s.PoisonTiles.Count>0,"Real skill generated poison zone");
            True(s.PoisonTiles.All(t=>t.expires<=23.6f),"Zone is extended once, never to 26.45 seconds");
            s.CreatePoisonTile(poison,10,0,20);Near(s.Time+23,s.PoisonTiles.Last().expires,"New poison zone receives its own single extension",.001);
            var burn=Deploy(s,"burn_stack",2);burn.sp=burn.definition.skill.spCost;burn.attackTimer=10000;Frames(s,1);
            Near(burn.definition.skill.duration,burn.skillRemaining,"Other-tag skill duration unchanged");
        });
        Run("leave note: every first-wave group delayed, later wave unchanged and shared schedule immutable",()=>{
            var c=Config("leave_note","leave_note");c.groups=new[]{
                new SpawnGroup{enemyId="item-target",wave=1,count=1},
                new SpawnGroup{enemyId="item-target",wave=1,count=1,delay=1},
                new SpawnGroup{enemyId="item-target",wave=2,count=1,waveStart=8}};
            string before=JsonSerializer.Serialize(c.groups,CombatDataTests.JsonOptions);var s=new BattleSimulation(c);
            Equal(0,s.Enemies.Count,"Nothing appears immediately");Frames(s,89);Equal(0,s.Enemies.Count,"Nothing before three seconds");
            Frames(s,1);Equal(1,s.Enemies.Count,"First group at exact three seconds");Frames(s,30);Equal(2,s.Enemies.Count,"Second first-wave group at four seconds");
            Frames(s,120);Equal(3,s.Enemies.Count,"Later wave still at eight seconds");
            True(before==JsonSerializer.Serialize(c.groups,CombatDataTests.JsonOptions),"Authored groups unmodified");
        });
        Run("recommendation: victory bonus once, no GPA on defeat",()=>{
            var win=new BattleSimulation(Config("recommendation_letter","recommendation_letter"));win.DamageDirect(win.Enemies[0],10000000);Frames(win,1);
            True(win.Finished&&win.Report.won,"Actual kill finishes battle");Equal(1,win.Report.bonusGpa,"Victory-only bonus deduplicated");
            var c=Config("recommendation_letter");c.protection=1;c.enemies[0].moveSpeed=10000;var loss=new BattleSimulation(c);Frames(loss,1);
            True(loss.Finished&&!loss.Report.won,"Actual goal crossing loses");Equal(0,loss.Report.bonusGpa,"Loss grants no bonus");
        });
        Run("endurance: fixed 100 restore ignores heal reduction, consumed across down and redeploy",()=>{
            var s=new BattleSimulation(Config("endurance_focus","endurance_focus"));var u=Deploy(s,"direct_crit");
            s.Buff(u,"test-heal-debuff",Stat.Healing,-1,100);
            s.DamageDirect(u,100000);True(!u.downed,"First lethal prevented");Near(100,u.hp,"Exactly 100 HP despite healing debuff");
            s.DamageDirect(u,100000);True(u.downed,"Second lethal really downs unit");Frames(s,451);
            True(s.TryRedeploy(u),"Normal cooldown permits redeployment");s.DamageDirect(u,100000);True(u.downed,"Redeployment cannot renew consumed item");
        });
        Run("forfeit: preserves earned statistics and freezes battle exactly once",()=>{
            var c=Config("recommendation_letter");c.groups[0].count=2;c.groups[0].interval=10;
            var s=new BattleSimulation(c);s.DamageDirect(s.Enemies[0],10000000);Frames(s,1);
            True(!s.Finished,"Later scheduled enemy still outstanding");int kills=s.Report.killed,gold=s.Report.goldEarned;float time=s.Time;
            s.Forfeit();True(s.Finished&&!s.Report.won,"Forfeit records a real defeat");
            Equal(kills,s.Report.killed,"Existing kills retained");Equal(gold,s.Report.goldEarned,"Existing kill gold retained");
            Equal(0,s.Report.bonusGpa,"Forfeit cannot award victory item GPA");s.Forfeit();Frames(s,900);
            Near(time,s.Time,"Finished battle does not advance");Equal(kills,s.Report.killed,"Repeat forfeit cannot change statistics");
        });
        Run("no items: original stats and first lethal remain unchanged",()=>{
            var s=new BattleSimulation(Config());var u=Deploy(s,"direct_crit");
            Near(0,s.AttackSpeedBonus(u),"No item AS bonus");Near(u.definition.attack[0],s.AttackValue(u),"Original attack");
            Near(u.definition.hp[0],s.MaxHP(u),"Original maximum HP");s.DamageDirect(u,100000);True(u.downed,"No free resurrection without item");
        });
    }
}
