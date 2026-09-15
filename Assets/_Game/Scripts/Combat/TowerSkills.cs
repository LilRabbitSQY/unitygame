using System;
using System.Collections.Generic;

namespace FinalDefense.Combat
{
    // The names, level values and effects below follow Source/04-towers.json.
    // Four surrounding cells means the four orthogonal neighbours; 3*3 means a nine-cell square.
    public partial class BattleSimulation
    {
        private sealed class TowerSkillMemory
        {
            public float healingTimer;
            public readonly Dictionary<int, float> retreating = new Dictionary<int, float>();
            public readonly Dictionary<int, float> pursuitBonuses = new Dictionary<int, float>();
        }
        private sealed class TowerFreezeRecovery
        {
            public UnitState target;
            public string source;
            public float slow, duration;
        }
        private sealed class TowerRetreatDamage
        {
            public UnitState source, target;
            public float expires, nextTick;
        }
        private readonly Dictionary<int, TowerSkillMemory> towerSkillMemory = new Dictionary<int, TowerSkillMemory>();
        private readonly List<TowerFreezeRecovery> towerFreezeRecoveries = new List<TowerFreezeRecovery>();
        private readonly List<TowerRetreatDamage> towerRetreatDamage = new List<TowerRetreatDamage>();

        private TowerSkillMemory TowerMemory(UnitState u)
        {
            if (!towerSkillMemory.TryGetValue(u.id, out var memory))
                towerSkillMemory[u.id] = memory = new TowerSkillMemory();
            return memory;
        }
        private float TowerLevel(UnitState u, float one, float two, float three)
            => u.level <= 1 ? one : u.level == 2 ? two : three;
        private string TowerSource(UnitState u) => "tower:" + u.id + ":active";
        private bool TowerChance(float probability) => Random.NextDouble() < probability;
        private bool TowerAdjacent(UnitState u, UnitState v)
            => Math.Abs(u.x-v.x)+Math.Abs(u.y-v.y) <= 1.01f && u != v;
        private bool TowerNearby(UnitState u, UnitState v)
            => Math.Abs(u.x-v.x)<=1.01f && Math.Abs(u.y-v.y)<=1.01f;
        private bool TowerTaggedFriend(UnitState u, string tag, int width=0, int depth=0)
        {
            foreach (var ally in Allies(u))
                if (ally != u && ally.alive && ally.Tag == tag && InRange(u,ally,width,depth)) return true;
            return false;
        }
        private List<UnitState> TowerSkillTargets(UnitState u, int width, int depth)
        {
            var result = new List<UnitState>();
            foreach (var enemy in Foes(u))
                if (enemy.alive && InRange(u,enemy,width,depth)) result.Add(enemy);
            return result;
        }
        private void RemoveTowerSource(string source)
        {
            foreach (var unit in Towers) unit.modifiers.RemoveAll(m => m.source == source);
            foreach (var unit in Enemies) unit.modifiers.RemoveAll(m => m.source == source);
        }
        public void StartTowerSkill(UnitState u)
        {
            var memory = TowerMemory(u);
            memory.healingTimer = 0;
            string source = TowerSource(u);
            float duration = Math.Max(.1f,TowerSkillDuration(u));
            switch (u.SkillId)
            {
                case "direct_crit":
                    Buff(u,source,Stat.DefensePercent,-.1f,duration);
                    break;
                case "direct_speed":
                    Buff(u,source,Stat.DefensePercent,.5f,duration);
                    break;
                case "burn_burst":
                    foreach (var target in TowerSkillTargets(u,2,3))
                    {
                        bool critical = TowerChance(.55f+ModifierValue(u,Stat.CritChance));
                        float criticalMultiplier = 1.5f+ModifierValue(u,Stat.CritDamage);
                        QueueEffect(u,target,()=>
                        {
                            Burn(target,2,10,u);
                            int stacks = 0;
                            foreach (var burn in target.burns) if (burn.expires > Time) stacks++;
                            float damage = Math.Min(50,stacks)*100;
                            if (stacks >= 50) damage *= 2;
                            if (critical) damage *= criticalMultiplier;
                            DamageDirect(target,damage,u);
                        });
                    }
                    foreach (var ally in Allies(u))
                        if (ally.alive && (ally == u || TowerAdjacent(u,ally)) && ally.Tag == "灼烧")
                            Buff(ally,"tower:"+u.id+":burst",Stat.AttackSpeed,TowerLevel(u,.25f,.3f,.35f),5);
                    break;
                case "poison_spread":
                    Buff(u,source,Stat.AttackFlat,100,duration);
                    Buff(u,source,Stat.AttackSpeed,.5f,duration);
                    break;
                case "poison_amp":
                    var cells = new HashSet<string>();
                    if (Config.ground != null)
                        foreach (var cell in Config.ground)
                            CreateTowerPoisonTile(u,cell,cells);
                    if (Config.path != null)
                        foreach (var cell in Config.path)
                            CreateTowerPoisonTile(u,cell,cells);
                    break;
                case "weaken_def":
                    bool weakeningPartner = TowerTaggedFriend(u,"削弱",3,3);
                    bool lowerAttack = weakeningPartner || TowerChance(.5f);
                    float seconds = weakeningPartner ? 7 : 5;
                    float defensePenalty = weakeningPartner ? -TowerLevel(u,.2f,.35f,.4f) : -TowerLevel(u,.15f,.2f,.25f);
                    foreach (var target in TowerSkillTargets(u,3,3))
                    {
                        string debuff = "tower:"+u.id+":weaken";
                        QueueEffect(u,target,()=>
                        {
                            Buff(target,debuff,Stat.DefensePercent,defensePenalty,seconds);
                            Buff(target,debuff,Stat.Healing,weakeningPartner ? -.6f : -.5f,seconds);
                            if (lowerAttack) Buff(target,debuff,Stat.AttackPercent,weakeningPartner ? -.4f : -.3f,seconds);
                        });
                    }
                    break;
                case "weaken_slow":
                    bool freezePartner = TowerTaggedFriend(u,"削弱",3,4);
                    float freezeSeconds = TowerLevel(u,1,2,3)+(freezePartner ? 1 : 0);
                    foreach (var target in TowerSkillTargets(u,3,4))
                    {
                        QueueEffect(u,target,()=>
                        {
                            Freeze(target,freezeSeconds);
                            towerFreezeRecoveries.Add(new TowerFreezeRecovery { target=target,
                                source="tower:"+u.id+":thaw", slow=freezePartner ? -.7f : -.6f,
                                duration=freezePartner ? 2 : .5f });
                        });
                    }
                    break;
                case "control_retreat":
                    Buff(u,source,Stat.AttackPercent,TowerLevel(u,1.2f,1.5f,1.8f),duration);
                    Buff(u,source,Stat.AttackSpeed,.3f,duration);
                    break;
                case "control_frozen":
                    Buff(u,source,Stat.AttackPercent,TowerLevel(u,.6f,.7f,.8f),duration);
                    memory.retreating.Clear();
                    break;
                case "counter_shield":
                    u.stopAttacking = true;
                    Heal(u,MaxHP(u));
                    foreach (var ally in Allies(u))
                    {
                        if (!ally.alive || !TowerNearby(u,ally)) continue;
                        bool partner = ally != u && ally.Tag == "盾反";
                        Buff(ally,source,Stat.DefensePercent,TowerLevel(u,.6f,.8f,1)+(partner ? .2f : 0),duration);
                        AddShield(ally,MaxHP(ally)*.3f);
                        if (partner) Heal(ally,MaxHP(ally)*.3f);
                    }
                    break;
                case "counter_store":
                    u.stopAttacking = true;
                    u.immortality = true;
                    u.recordedDamage = 0;
                    Buff(u,source,Stat.DefensePercent,TowerLevel(u,.3f,.4f,.5f),duration);
                    Buff(u,source,Stat.MaxHP,.8f,duration);
                    break;
                case "pursuit_strike":
                    Buff(u,source,Stat.AttackPercent,.8f,duration);
                    break;
                case "pursuit_support":
                    Buff(u,source,Stat.DefensePercent,-.2f,duration);
                    // The sheet says current HP -20%, not maximum HP -20%.
                    u.hp = Math.Max(1,u.hp*.8f);
                    memory.pursuitBonuses.Clear();
                    break;
                case "heal_support":
                    Buff(u,source,Stat.AttackPercent,.15f,duration);
                    Buff(u,source,Stat.AttackSpeed,.2f,duration);
                    break;
            }
            ApplyTowerAuras(u);
        }

        private void CreateTowerPoisonTile(UnitState u, Cell cell, HashSet<string> cells)
        {
            if (!CombatMath.InRange(u.x,u.y,u.facingX,u.facingY,cell.x,cell.y,3,3)) return;
            if (cells.Add(cell.x+","+cell.y)) CreatePoisonTile(u,cell.x,cell.y,20);
        }

        public void TickTowerSkill(UnitState u, float dt)
        {
            if (u.SkillId != "direct_speed" && u.SkillId != "counter_store") return;
            var memory = TowerMemory(u);
            memory.healingTimer += dt;
            while (memory.healingTimer >= 1)
            {
                memory.healingTimer -= 1;
                Heal(u,u.SkillId == "direct_speed" ? 100 : 80);
            }
        }

        public void EndTowerSkill(UnitState u,bool completed=true)
        {
            if (u.SkillId == "counter_store")
            {
                bool behind = false;
                foreach (var ally in Allies(u))
                    if (ally != u && ally.alive && ally.Tag == "盾反"
                        && CombatMath.InRange(u.x,u.y,-u.facingX,-u.facingY,ally.x,ally.y,1,1)) behind = true;
                float critChance = .05f + (behind ? .5f : .2f) + ModifierValue(u,Stat.CritChance);
                foreach (var target in completed ? TowerSkillTargets(u,1,1) : new List<UnitState>())
                {
                    float damage = u.recordedDamage;
                    if (TowerChance(critChance)) damage *= 1.5f+ModifierValue(u,Stat.CritDamage);
                    QueueEffect(u,target,()=>DamageDirect(target,damage,u));
                }
                u.recordedDamage = 0;
                u.immortality = false;
            }
            if (u.SkillId == "counter_store" || u.SkillId == "counter_shield") u.stopAttacking = false;
            if (u.SkillId == "pursuit_support")
            {
                bool restore = completed && TowerChance(TowerLevel(u,.45f,.5f,.55f));
                foreach (var ally in Allies(u))
                {
                    if (ally != u && ally.alive && ally.Tag == "追击" && InRange(u,ally) && restore)
                        ally.sp = Math.Min(ally.definition.skill.spCost,ally.sp+5);
                    ally.modifiers.RemoveAll(m=>m.source == "tower:"+u.id+":pursuit");
                }
                TowerMemory(u).pursuitBonuses.Clear();
            }
            RemoveTowerSource(TowerSource(u));
            RemoveTowerSource("tower:"+u.id+":aura");
            u.hp = Math.Min(u.hp,MaxHP(u));
        }

        public float TowerConditionalAttackBonus(UnitState attacker, UnitState target)
            => attacker != null && !attacker.enemy && attacker.skillActive && attacker.SkillId == "poison_spread"
                && target.poisonUntil > Time ? TowerLevel(attacker,2.5f,2.8f,3) : 0;

        public float TowerConditionalShelter(UnitState target, UnitState attacker)
            => target != null && attacker != null && !target.enemy && target.skillActive
                && target.SkillId == "poison_spread" && attacker.poisonUntil > Time
                ? TowerLevel(target,.2f,.25f,.3f) : 0;

        // Run once per simulation step, including when a tower's instant skill is no longer active.
        public void TickTowerEffects(float dt)
        {
            foreach (var tower in Towers)
            {
                if (!tower.alive || tower.downed)
                {
                    RemoveTowerSource(TowerSource(tower));
                    RemoveTowerSource("tower:"+tower.id+":aura");
                    continue;
                }
                ApplyTowerAuras(tower);
                if (tower.skillActive && tower.SkillId == "control_frozen") TrackRetreatEnd(tower);
            }
            for (int i=towerFreezeRecoveries.Count-1;i>=0;i--)
            {
                var recovery = towerFreezeRecoveries[i];
                if (!recovery.target.alive) { towerFreezeRecoveries.RemoveAt(i); continue; }
                if (recovery.target.frozenUntil > Time) continue;
                Buff(recovery.target,recovery.source,Stat.MoveSpeed,recovery.slow,recovery.duration);
                towerFreezeRecoveries.RemoveAt(i);
            }
            for (int i=towerRetreatDamage.Count-1;i>=0;i--)
            {
                var dot = towerRetreatDamage[i];
                if (!dot.target.alive) { towerRetreatDamage.RemoveAt(i); continue; }
                while (dot.nextTick <= Time+.0001f && dot.nextTick <= dot.expires+.0001f)
                {
                    DamageDirect(dot.target,200,dot.source);
                    dot.nextTick += 1;
                }
                if (Time >= dot.expires) towerRetreatDamage.RemoveAt(i);
            }
        }

        private void ApplyTowerAuras(UnitState u)
        {
            if (!u.alive || u.downed) return;
            bool passive = u.SkillId == "heal_aura";
            if (!u.skillActive && !passive) return;
            string aura = "tower:"+u.id+":aura";
            RemoveTowerSource(aura);
            // Rebuild aura membership every simulation step, so death and moving/added allies are respected.
            const float refresh = 1;
            if (passive)
            {
                Buff(u,aura,Stat.AttackPercent,TowerLevel(u,.2f,.25f,.3f),refresh);
                if (TowerTaggedFriend(u,"治疗"))
                    foreach (var ally in Allies(u))
                        if (ally.alive && InRange(u,ally)) Buff(ally,aura,Stat.DefenseFlat,50,refresh);
                return;
            }
            foreach (var ally in Allies(u))
            {
                if (!ally.alive) continue;
                switch (u.SkillId)
                {
                    case "direct_crit":
                        if ((ally == u || InRange(u,ally,1,2)) && ally.Tag == "直伤")
                        {
                            Buff(ally,aura,Stat.AttackPercent,TowerLevel(u,.15f,.18f,.2f),refresh);
                            Buff(ally,aura,Stat.CritChance,.3f,refresh);
                            Buff(ally,aura,Stat.CritDamage,.5f,refresh);
                        }
                        break;
                    case "direct_speed":
                        if (TowerAdjacent(u,ally) && ally.Tag == "直伤")
                            Buff(ally,aura,Stat.AttackSpeed,TowerLevel(u,.4f,.45f,.5f),refresh);
                        break;
                    case "heal_support":
                        if (ally != u && InRange(u,ally))
                        {
                            Buff(ally,aura,Stat.Healing,TowerLevel(u,.15f,.18f,.2f),refresh);
                            Buff(ally,aura,Stat.Shelter,TowerLevel(u,.1f,.12f,.15f),refresh);
                            if (ally.Tag == "控制" || ally.Tag == "中毒" || ally.Tag == "灼烧" || ally.Tag == "治疗")
                                Buff(ally,aura,Stat.AttackSpeed,.2f,refresh);
                        }
                        break;
                }
            }
            foreach (var enemy in Foes(u))
            {
                if (!enemy.alive || !InRange(u,enemy)) continue;
                if (u.SkillId == "poison_amp" && enemy.poisonUntil > Time)
                {
                    Buff(enemy,aura,Stat.DefensePercent,-TowerLevel(u,.1f,.12f,.15f),refresh);
                    Buff(enemy,aura,Stat.DamageTaken,TowerLevel(u,.1f,.12f,.15f),refresh);
                    Buff(enemy,aura,Stat.AttackSpeed,-.2f,refresh);
                }
                if (u.SkillId == "heal_support") Buff(enemy,aura,Stat.Vulnerability,.15f,refresh);
            }
            if (u.SkillId == "control_frozen")
                foreach (var enemy in Foes(u))
                    if (enemy.alive && enemy.retreatUntil > Time) Buff(enemy,aura,Stat.Vulnerability,.35f,refresh);
        }

        private void TrackRetreatEnd(UnitState u)
        {
            var previous = TowerMemory(u).retreating;
            foreach (var target in Foes(u))
            {
                if (!target.alive || !InRange(u,target)) { previous.Remove(target.id); continue; }
                if (target.retreatUntil > Time) previous[target.id] = target.retreatUntil;
                else if (previous.ContainsKey(target.id))
                {
                    previous.Remove(target.id);
                    if (TowerChance(.5f))
                    {
                        Retreat(target,2);
                        previous[target.id] = target.retreatUntil;
                    }
                }
            }
        }

        // This hook runs once BEFORE each complete attack, not once per target.
        public void TowerAttackEffects(UnitState u, List<UnitState> targets, bool followUp)
        {
            // A healer's normal healing action is not an attack and must not trigger attack procs.
            if (u.definition.healing) return;
            if (u.skillActive && u.SkillId == "direct_crit" && TowerChance(.1f))
            {
                float duration = Math.Max(.01f,u.skillRemaining);
                foreach (var target in targets)
                    QueueEffect(u,target,()=>Buff(target,"direct_crit:defense",Stat.DefensePercent,-.1f,duration));
            }
            if (u.skillActive && u.SkillId == "burn_stack")
            {
                int count = (int)TowerLevel(u,2,3,4);
                foreach (var target in TowerSkillTargets(u,2,3)) QueueEffect(u,target,()=>Burn(target,count,10,u));
            }
            if (u.skillActive && u.SkillId == "pursuit_strike" && TowerTaggedFriend(u,"追击") && TowerChance(.2f))
            {
                bool burning = TowerChance(.5f);
                foreach (var target in targets)
                    QueueEffect(u,target,()=> { if (burning) Burn(target,1,2,u); else Freeze(target,2); });
            }
            foreach (var ally in Allies(u))
            {
                if (!ally.alive) continue;
                if (ally.SkillId == "heal_aura" && InRange(ally,u) && TowerChance(.2f)) Heal(u,80);
                if (!ally.skillActive) continue;
                if (ally.SkillId == "burn_stack" && u.Tag == "灼烧" && (ally == u || InRange(ally,u)) && TowerChance(.5f))
                    foreach (var target in targets) QueueEffect(u,target,()=>Burn(target,1,10,u));
                if (ally.SkillId == "direct_speed" && u.Tag == "直伤" && TowerAdjacent(ally,u) && TowerChance(.3f))
                    foreach (var target in targets) QueueEffect(u,target,()=>Buff(target,"direct_speed:slow",Stat.MoveSpeed,-.2f,2));
            }
        }

        public void AfterTowerHit(UnitState attacker, UnitState target, int damage, bool followUp)
            => CaptureTowerHit(attacker,target,followUp)(damage);

        // Capture this callback on the firing frame, not when a projectile eventually arrives.
        public Action<int> CaptureTowerHit(UnitState attacker, UnitState target, bool followUp)
        {
            string skill = attacker.skillActive ? attacker.SkillId : "";
            int leechSources = 0;
            if (followUp)
                foreach (var support in Allies(attacker))
                    if (support != attacker && support.alive && support.skillActive && support.SkillId == "pursuit_support"
                        && attacker.Tag == "追击" && InRange(support,attacker)) leechSources++;
            return damage =>
            {
                switch (skill)
                {
                    case "poison_spread":
                        Retreat(target,2);
                        break;
                    case "control_retreat":
                        Retreat(target,2);
                        var dot = towerRetreatDamage.Find(d => d.target == target && d.source == attacker);
                        if (dot == null)
                            towerRetreatDamage.Add(new TowerRetreatDamage { source=attacker,target=target,
                                nextTick=Time+1,expires=Time+2 });
                        else dot.expires = Time+2;
                        break;
                }
                for (int i=0;i<leechSources;i++) Heal(attacker,damage*.2f);
            };
        }

        public void AfterTowerAttack(UnitState u, List<UnitState> targets, bool followUp)
        {
            if (u.definition.healing) return;
            if (followUp)
            {
                foreach (var support in Allies(u))
                {
                    if (support == u || !support.alive || !support.skillActive || support.SkillId != "pursuit_support"
                        || u.Tag != "追击" || !InRange(support,u)) continue;
                    var bonuses = TowerMemory(support).pursuitBonuses;
                    bonuses.TryGetValue(u.id,out float bonus);
                    bonus = Math.Min(.5f,bonus+TowerLevel(support,.05f,.08f,.1f));
                    bonuses[u.id] = bonus;
                    Buff(u,"tower:"+support.id+":pursuit",Stat.AttackSpeed,bonus,Math.Max(.01f,support.skillRemaining));
                }
                return;
            }
            // A skill and each separate support may each grant one extra attack; none can recurse.
            if (u.skillActive && u.SkillId == "pursuit_strike" && TowerChance(TowerLevel(u,.6f,.7f,.8f)))
                AttackTargets(u,new List<UnitState>(targets),1.5f,true);
            foreach (var support in Allies(u))
                if (support != u && support.alive && support.skillActive && support.SkillId == "pursuit_support"
                    && u.Tag == "追击" && InRange(support,u)) AttackTargets(u,new List<UnitState>(targets),1,true);
        }

        public void RecordTowerDamage(UnitState u, int damage)
        {
            if (!u.skillActive) return;
            if (u.SkillId == "counter_store")
                u.recordedDamage = Math.Min(MaxHP(u)*3,u.recordedDamage+damage);
        }

        public void OnTowerDamaged(UnitState u, UnitState attacker, int damage)
        {
            if (!u.skillActive) return;
            if (u.SkillId == "counter_shield" && attacker != null && attacker.enemy && attacker.alive)
            {
                float reflected = DefenseValue(u)*TowerLevel(u,.6f,.7f,.8f);
                QueueEffect(u,attacker,()=>DamageDirect(attacker,reflected,u,true));
            }
        }
    }
}
