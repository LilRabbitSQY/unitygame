using System;
using System.Collections.Generic;
using System.Linq;

namespace FinalDefense.Combat
{
    // Authoritative battle state. Unity only supplies input and renders this fixed 30 Hz simulation.
    public sealed partial class BattleSimulation
    {
        public const float StepSeconds = 1f/30f;
        public readonly BattleConfiguration Config;
        public readonly Random Random;
        public readonly List<UnitState> Towers = new List<UnitState>();
        public readonly List<UnitState> Enemies = new List<UnitState>();
        public readonly List<ProjectileState> Projectiles = new List<ProjectileState>();
        public readonly List<PoisonTile> PoisonTiles = new List<PoisonTile>();
        public readonly Queue<BattleEvent> Events = new Queue<BattleEvent>();
        public readonly BattleReport Report = new BattleReport();
        public float Time { get; private set; }
        public float Cost => (float)preciseCost;
        public int Protection { get; private set; }
        public int CurrentWave { get; private set; }
        public bool Paused { get; set; }
        public float Speed { get; set; } = 1;
        public bool Finished { get; private set; }
        public int TotalEnemies => schedule.Count;
        public int TotalWaves => Config.groups.Length == 0 ? 0 : Config.groups.Max(g=>g.wave);
        public string LastError { get; private set; }
        private readonly List<ScheduledSpawn> schedule = new List<ScheduledSpawn>();
        private readonly float[] pathDistances;
        private int nextSpawn, nextId, frame;
        private double accumulator, preciseCost;
        private sealed class ScheduledSpawn { public float time; public SpawnGroup group; public int order; }

        public BattleSimulation(BattleConfiguration config, int seed=1)
        {
            Config=config ?? throw new ArgumentNullException(nameof(config));
            Random=new Random(seed);
            ValidateConfiguration(config);
            InitializeBattleItems();
            preciseCost=config.initialCost; Protection=config.protection;
            pathDistances=new float[config.path.Length];
            for(int i=1;i<pathDistances.Length;i++) pathDistances[i]=pathDistances[i-1]+Distance(config.path[i-1].x,config.path[i-1].y,config.path[i].x,config.path[i].y);
            int order=0;
            foreach(var g in config.groups)
                for(int i=0;i<g.count;i++) schedule.Add(new ScheduledSpawn { time=g.waveStart+BattleItemWaveDelay(g)+g.delay+i*g.interval,group=g,order=order++ });
            schedule.Sort((a,b)=>a.time!=b.time ? a.time.CompareTo(b.time) : a.order.CompareTo(b.order));
            SpawnDue();
        }
        public static void ValidateConfiguration(BattleConfiguration c)
        {
            if(c.path==null || c.path.Length<2) throw new ArgumentException("A battle needs at least two path cells.");
            if(c.protection<=0 || c.initialCost<0 || c.maxCost<c.initialCost || c.costPerSecond<0 || c.maxDeployed<1) throw new ArgumentException("Invalid economy or protection configuration.");
            if(c.groups==null || c.towers==null || c.enemies==null || c.ground==null || c.highGround==null) throw new ArgumentException("Battle catalog/map is incomplete.");
            foreach(var d in c.towers.Concat(c.enemies))
                if(string.IsNullOrEmpty(d.id)||d.hp==null||d.hp.Length==0||d.hp.Any(h=>h<=0)||d.attack==null||d.defense==null||d.moveSpeed<0) throw new ArgumentException("Invalid unit: "+d.id);
            if(c.towers.GroupBy(d=>d.id).Any(g=>g.Count()>1)||c.enemies.GroupBy(d=>d.id).Any(g=>g.Count()>1)) throw new ArgumentException("Duplicate unit ids.");
            foreach(var g in c.groups)
                if(g.count<0||g.interval<0||g.waveStart<0||g.delay<0||g.statMultiplier<=0||!c.enemies.Any(d=>d.id==g.enemyId)) throw new ArgumentException("Invalid spawn group: "+g.enemyId);
        }
        public void Tick(float deltaTime)
        {
            if(Paused||Finished||deltaTime<=0) return;
            accumulator += deltaTime*Math.Max(0,Speed);
            while(accumulator+1e-8 >= 1.0/30 && !Finished)
            {
                accumulator-=1.0/30;
                frame++; Time=frame/30f;
                Step();
            }
        }
        private void Step()
        {
            SpawnDue();
            AddCost(Config.costPerSecond*StepSeconds);
            foreach(var u in Towers.Concat(Enemies).Where(u=>u.alive&&!u.downed).ToArray()) TickStatus(u);
            if(Finished) return;
            TickTowerEffects(StepSeconds);
            foreach(var u in Towers.Where(Active).ToArray()) TickTower(u);
            foreach(var u in Enemies.Where(Active).ToArray())
            {
                TickEnemySkill(u,StepSeconds);
                if(!Active(u)||Time<u.frozenUntil) continue;
                var skill=u.definition.skill;
                if(skill!=null&&skill.interval>0)
                {
                    u.skillTimer=Math.Max(0,u.skillTimer-StepSeconds);
                    if(u.skillTimer<=.0001f && CanExecuteEnemySkill(u)) { ExecuteEnemySkill(u);u.TotalSkills++;u.skillTimer=skill.interval; }
                }
            }
            MoveEnemies();
            foreach(var u in Enemies.Where(Active).ToArray()) TickAttack(u);
            TickProjectiles();
            TickPoisonTiles();
            if(!Finished && nextSpawn==schedule.Count && !Enemies.Any(Active)) Finish(true);
        }
        private static bool Active(UnitState u) => u!=null&&u.alive&&!u.downed;
        public float PathLength => pathDistances[pathDistances.Length-1];
        private void SpawnDue()
        {
            foreach(var group in Config.groups)
                if(group.waveStart+BattleItemWaveDelay(group)<=Time+.0001f&&group.wave>CurrentWave)
                { CurrentWave=group.wave;Emit("wave",null,"第 "+CurrentWave+" 波"); }
            while(nextSpawn<schedule.Count && schedule[nextSpawn].time<=Time+.0001f)
            {
                var g=schedule[nextSpawn++].group;
                var d=Config.enemies.First(e=>e.id==g.enemyId);
                var u=CreateUnit(d,true); u.scale=g.statMultiplier;u.hp=MaxHP(u);
                // A group can override objective damage without mutating the shared definition.
                goalOverrides[u.id]=g.goalDamage>0?g.goalDamage:d.goalDamage;
                PositionOnPath(u,0);Enemies.Add(u);Report.spawned++;
                Emit("spawn",u,d.name);
            }
        }
        private readonly Dictionary<int,int> goalOverrides = new Dictionary<int,int>();
        private UnitState CreateUnit(UnitDefinition d,bool enemy)
        {
            var u=new UnitState { id=++nextId,definition=d,enemy=enemy,sp=d.skill==null?0:d.skill.initialSp,skillTimer=d.skill==null?0:d.skill.interval };
            u.hp=MaxHP(u); return u;
        }
        public UnitState TryDeploy(string id,int x,int y,int facingX=1,int facingY=0)
        {
            LastError=null;
            var d=Config.towers.FirstOrDefault(t=>t.id==id);
            if(Finished||d==null) return DeploymentError("无法部署");
            if(Math.Abs(facingX)+Math.Abs(facingY)!=1) return DeploymentError("朝向必须是上下左右之一");
            if(!(d.highGround?Config.highGround:Config.ground).Any(c=>c.x==x&&c.y==y)) return DeploymentError(d.highGround?"需要高台位置":"需要地面位置");
            if(Towers.Any(t=>t.alive&&t.x==x&&t.y==y)) return DeploymentError("此格已有棋子");
            if(Towers.Count(t=>t.alive)>=Config.maxDeployed) return DeploymentError("已达到部署上限");
            if(!SpendCost(d.deployCost)) return DeploymentError("COST 不足");
            var u=CreateUnit(d,false);u.x=x;u.y=y;u.facingX=facingX;u.facingY=facingY;Towers.Add(u);Report.deployed++;
            if(d.skill!=null&&d.skill.spCost<=0) {u.skillActive=true;StartTowerSkill(u);}
            Emit("deploy",u,d.name);return u;
        }
        private UnitState DeploymentError(string message) {LastError=message;return null;}
        public bool TryUpgrade(UnitState u)
        {
            if(Finished||!Active(u)||u.enemy||u.level>=3) return false;
            var costs=u.definition.upgradeCost;
            if(costs==null||costs.Length<=u.level||!SpendCost(costs[u.level])) return false;
            int before=MaxHP(u);u.level++;u.hp=Math.Min(MaxHP(u),u.hp+MaxHP(u)-before);Report.upgrades++;
            Emit("upgrade",u,"升级至 Lv."+u.level);return true;
        }
        public bool TryRedeploy(UnitState u)
        {
            if(Finished||u==null||!u.alive||!u.downed||u.enemy||Time<u.reviveAt||!SpendCost(u.definition.deployCost)) return false;
            u.downed=false;u.level=1;u.hp=MaxHP(u);u.sp=u.definition.skill==null?0:u.definition.skill.initialSp;
            if(u.definition.skill!=null&&u.definition.skill.spCost<=0){u.skillActive=true;StartTowerSkill(u);}
            Emit("revive",u,"重新部署");return true;
        }
        public bool Withdraw(UnitState u)
        {
            if(Finished||u==null||u.enemy||!u.alive||u.downed) return false;
            if(u.skillActive) EndTowerSkill(u,false);
            u.alive=false;u.skillActive=false;
            if(!u.downed) AddCost((int)(u.definition.deployCost*.5f));
            Emit("withdraw",u,"已撤回");return true;
        }
        public bool SpendCost(float amount)
        {
            if(amount<0||Cost+.0001f<amount) return false;
            preciseCost=Math.Max(0,preciseCost-amount);Report.costSpent+=(int)amount;return true;
        }
        public void AddCost(float amount)
        {
            if(amount<=0) return;
            preciseCost=Math.Min(Config.maxCost,preciseCost+amount);
        }
        public float ModifierValue(UnitState u,Stat stat) => u.modifiers.Where(m=>m.stat==stat&&m.expires>Time).Sum(m=>m.value)+BattleItemModifier(u,stat);
        public void Buff(UnitState u,string source,Stat stat,float value,float duration)
        {
            if(!Active(u)||duration<=0) return;
            var m=u.modifiers.FirstOrDefault(b=>b.source==source&&b.stat==stat);
            if(m==null) {m=new Modifier {source=source,stat=stat};u.modifiers.Add(m);}
            m.value=value;m.expires=Time+duration;
        }
        public int MaxHP(UnitState u) => Math.Max(1,Round(u.definition.AtLevel(u.definition.hp,Math.Max(1,u.level))*u.scale*(1+ModifierValue(u,Stat.MaxHP))));
        public float AttackValue(UnitState u) => Math.Max(0,(u.definition.AtLevel(u.definition.attack,Math.Max(1,u.level))*u.scale+ModifierValue(u,Stat.AttackFlat))*(1+ModifierValue(u,Stat.AttackPercent)));
        public float DefenseValue(UnitState u) => Math.Max(0,(u.definition.AtLevel(u.definition.defense,Math.Max(1,u.level))*u.scale+ModifierValue(u,Stat.DefenseFlat))*(1+ModifierValue(u,Stat.DefensePercent)-(Time<u.frozenUntil?.1f:0)));
        public int Attack(UnitState u) => Round(AttackValue(u));
        public int Defense(UnitState u) => Round(DefenseValue(u));
        public float AttackSpeedBonus(UnitState u) => ModifierValue(u,Stat.AttackSpeed)-(Time<u.poisonUntil&&!u.enemy?.2f:0);
        public static int Round(float x) => (int)Math.Round(x,MidpointRounding.AwayFromZero);
        public int BlockCapacity(UnitState u) => !Active(u)||u.definition.highGround?0:Math.Max(0,u.definition.block-(int)ModifierValue(u,Stat.BlockReduction));
        public List<UnitState> Allies(UnitState u) => (u.enemy?Enemies:Towers).Where(Active).ToList();
        public List<UnitState> Foes(UnitState u) => (u.enemy?Towers:Enemies).Where(Active).ToList();
        public bool InRange(UnitState u,UnitState v,int width=0,int depth=0,bool radial=false)
            => InRange(u,v.x,v.y,width,depth,radial);
        public bool InRange(UnitState u,float x,float y,int width=0,int depth=0,bool radial=false)
        {
            if(width==0&&depth==0&&!u.enemy&&u.skillActive&&u.SkillId=="control_retreat"){width=1;depth=5;}
            return CombatMath.InRange(u.x,u.y,u.facingX,u.facingY,x,y,width>0?width:u.definition.rangeWidth,depth>0?depth:u.definition.rangeDepth,radial||u.definition.radial);
        }
        public float Distance(UnitState a,UnitState b) => Distance(a.x,a.y,b.x,b.y);
        private static float Distance(float ax,float ay,float bx,float by) => (float)Math.Sqrt((ax-bx)*(ax-bx)+(ay-by)*(ay-by));
        public List<UnitState> Targets(UnitState u,bool allies=false) => (allies?Allies(u):Foes(u)).Where(t=>(!allies&&!u.enemy&&t.blocker==u)||InRange(u,t)).OrderByDescending(t=>!allies&&t.blocker==u?10000:allies?1-t.hp/MaxHP(t):t.enemy?t.progress:100-Distance(u,t)).ThenBy(t=>t.id).ToList();

        private void TickTower(UnitState u)
        {
            var skill=u.definition.skill;
            if(skill!=null)
            {
                if(u.skillActive)
                {
                    TickTowerSkill(u,StepSeconds);
                    if(skill.spCost>0)
                    {
                        u.skillRemaining-=StepSeconds;
                        if(u.skillRemaining<=.0001f) {u.skillActive=false;EndTowerSkill(u);}
                    }
                }
                else if(Time>=u.frozenUntil)
                {
                    // Keep 30 Hz charging exact while accepting skill-driven SP changes.
                    if(u.sp!=u.publishedSp)u.preciseSp=u.sp;
                    u.preciseSp=Math.Min(skill.spCost,u.preciseSp+1d/30);
                    u.sp=(float)u.preciseSp;u.publishedSp=u.sp;
                    if(u.sp+.0001f>=skill.spCost && Enemies.Any(Active))
                    {
                        u.sp=0;u.skillActive=true;u.skillRemaining=TowerSkillDuration(u);u.TotalSkills++;
                        StartTowerSkill(u);Emit("skill",u,skill.name);
                        if(skill.duration<=0){u.skillActive=false;EndTowerSkill(u);}
                    }
                }
            }
            TickAttack(u);
        }
        private void TickAttack(UnitState u)
        {
            if(!Active(u)||u.stopAttacking||Time<u.frozenUntil||u.definition.attackFrames<=0) return;
            u.attackTimer=Math.Max(0,u.attackTimer-StepSeconds);
            if(u.attackTimer>.0001f) return;
            List<UnitState> targets;
            if(u.enemy && u.definition.attackWhenBlocked)
            {
                targets=new List<UnitState>();
                if(Active(u.blocker))
                {targets.Add(u.blocker);targets.AddRange(Targets(u).Where(t=>t!=u.blocker));}
            }
            else targets=Targets(u,u.definition.healing);
            if(u.definition.healing) targets=targets.Where(t=>t.hp<MaxHP(t)).ToList();
            targets=targets.Take(u.definition.healAll?int.MaxValue:Math.Max(1,u.definition.targets)).ToList();
            if(targets.Count==0) return;
            AttackTargets(u,targets);
            float speed=AttackSpeedBonus(u);
            u.attackTimer=CombatMath.AttackFrames(u.definition.attackFrames,speed)/30f;
        }
        public void AttackTargets(UnitState u,List<UnitState> targets,float multiplier=1,bool followUp=false,float critBonus=0,Action<int,UnitState> onHit=null)
        {
            if(!Active(u)||Finished) return;
            targets=targets.Where(Active).ToList();
            if(targets.Count==0) return;
            u.TotalAttacks++;
            if(!u.enemy) TowerAttackEffects(u,targets,followUp);
            foreach(var target in targets)
            {
                var hit=DamagePacket.Basic(u.definition.AtLevel(u.definition.attack,u.level)*u.scale);
                hit.skillMultiplier=multiplier;hit.flat=ModifierValue(u,Stat.AttackFlat);
                hit.attackPercent=ModifierValue(u,Stat.AttackPercent)+(!u.enemy?TowerConditionalAttackBonus(u,target):0);
                hit.damageBonus=ModifierValue(u,Stat.DamageBonus);
                hit.critChance=Math.Max(0,Math.Min(1,.05+ModifierValue(u,Stat.CritChance)+critBonus));
                hit.critDamage=.5+ModifierValue(u,Stat.CritDamage);
                hit.critical=Random.NextDouble()<hit.critChance;
                // The attacking side is snapshotted now; the target side is evaluated on impact.
                Action<int> towerHit=u.enemy?null:CaptureTowerHit(u,target,followUp);
                Projectiles.Add(new ProjectileState { source=u,target=target,x=u.x,y=u.y,damage=hit,
                    speed=u.definition.projectile?u.definition.projectileSpeed*Math.Max(.1f,1+AttackSpeedBonus(u)):0,
                    delay=StepSeconds,eligibleFrame=frame+1,healing=u.definition.healing,
                    onHit=damage=> { towerHit?.Invoke(damage);onHit?.Invoke(damage,target); } });
            }
            if(!u.enemy) AfterTowerAttack(u,targets,followUp);
        }
        private void TickProjectiles()
        {
            foreach(var p in Projectiles.ToArray())
            {
                if(!Active(p.target)){Projectiles.Remove(p);continue;}
                if(frame<p.eligibleFrame)continue;
                if(p.speed>0)
                {
                    float dist=Distance(p.x,p.y,p.target.x,p.target.y),step=p.speed*StepSeconds;
                    if(dist>step){p.x+=(p.target.x-p.x)/dist*step;p.y+=(p.target.y-p.y)/dist*step;continue;}
                }
                if(p.effect!=null){p.effect();Projectiles.Remove(p);continue;}
                int actual;
                if(p.healing) actual=Heal(p.target,(float)((p.damage.attack*p.damage.skillMultiplier+p.damage.flat)*(1+p.damage.attackPercent)));
                else
                {
                    var d=DefensePacket.Basic(p.target.definition.AtLevel(p.target.definition.defense,p.target.level)*p.target.scale);
                    d.flat=ModifierValue(p.target,Stat.DefenseFlat);d.defensePercent=ModifierValue(p.target,Stat.DefensePercent)-(Time<p.target.frozenUntil?.1:0);
                    d.vulnerability=ModifierValue(p.target,Stat.Vulnerability);d.damageTaken=ModifierValue(p.target,Stat.DamageTaken);
                    d.reduction=Math.Min(1,ModifierValue(p.target,Stat.Reduction));
                    d.shelter=Math.Min(1,ModifierValue(p.target,Stat.Shelter)+(!p.target.enemy?TowerConditionalShelter(p.target,p.source):0));
                    actual=ApplyDamage(p.target,CombatMath.Damage(p.damage,d),p.source,false);
                }
                p.onHit?.Invoke(actual);Projectiles.Remove(p);
            }
        }
        public void QueueEffect(UnitState source,UnitState target,Action onHit)
        {
            if(!Active(source)||!Active(target)) return;
            Projectiles.Add(new ProjectileState {source=source,target=target,x=source.x,y=source.y,
                speed=source.definition.projectile?source.definition.projectileSpeed*Math.Max(.1f,1+AttackSpeedBonus(source)):0,
                delay=StepSeconds,eligibleFrame=frame+1,effect=onHit});
        }
        public int Heal(UnitState u,float amount)
        {
            if(!Active(u)||amount<=0) return 0;
            float before=u.hp;
            u.hp=Math.Min(MaxHP(u),u.hp+Round(amount*Math.Max(0,1+ModifierValue(u,Stat.Healing))));
            int healed=Round(u.hp-before);u.TotalHealing+=healed;if(!u.enemy)Report.healing+=healed;
            return healed;
        }
        public void AddShield(UnitState u,float amount) { if(Active(u))u.shield+=Math.Max(0,Round(amount)); }
        public void DamageDirect(UnitState u,float damage,UnitState source=null,bool reflect=false) => ApplyDamage(u,CombatMath.RoundDamage(damage),source,reflect);
        private int ApplyDamage(UnitState u,int damage,UnitState source,bool reflected)
        {
            if(!Active(u)||Finished||damage<=0) return 0;
            u.lastShieldBeforeHit=u.shield;
            float shieldDamage=Math.Min(u.shield,damage);u.shield-=shieldDamage;
            float hpDamage=Math.Min(Math.Max(0,u.hp-(u.immortality?1:0)),damage-shieldDamage);u.hp-=hpDamage;
            int actual=Round(shieldDamage+hpDamage);u.lastHitAt=Time;
            if(source!=null)source.TotalDamage+=actual;
            if(u.enemy)Report.damage+=actual;
            Emit("hit",u,"",actual);
            if(u.enemy)RecordEnemyDamage(u,damage);else RecordTowerDamage(u,damage);
            if(!reflected&&source!=null)
            {if(u.enemy)OnEnemyDamaged(u,source,actual);else OnTowerDamaged(u,source,actual);}
            if(u.hp<=0 && Active(u))
            {
                if(u.enemy)
                {
                    u.alive=false;Report.killed++;AddCost(u.definition.costReward);Report.costEarned+=u.definition.costReward;
                    Report.goldEarned+=u.definition.goldReward;
                    Emit("kill",u,u.definition.name);
                }
                else if(!TryPreventItemDefeat(u)) Down(u);
            }
            return actual;
        }
        private void Down(UnitState u)
        {
            if(u.skillActive) {u.skillActive=false;EndTowerSkill(u,false);}
            u.downed=true;u.level=0;u.hp=0;u.shield=0;u.sp=0;u.reviveAt=Time+Config.redeploySeconds;
            u.modifiers.Clear();u.burns.Clear();u.poisonUntil=0;u.frozenUntil=0;u.immortality=false;u.stopAttacking=false;
            Emit("down",u,"倒地 · 等待重新部署");
        }
        public void Burn(UnitState u,int stacks,float seconds,UnitState source=null)
        {
            if(!Active(u))return;
            stacks=BattleItemBurnStacks(source,stacks);
            u.burns.RemoveAll(b=>b.expires<=Time);
            for(int i=0;i<stacks&&u.burns.Count<50;i++)u.burns.Add(new BurnStack {expires=Time+Math.Min(10,seconds)});
        }
        public void Poison(UnitState u,float seconds) {if(Active(u))u.poisonUntil=Math.Max(u.poisonUntil,Time+seconds);}
        public void Freeze(UnitState u,float seconds) {if(Active(u))u.frozenUntil=Math.Max(u.frozenUntil,Time+Math.Min(5,seconds));}
        public void Retreat(UnitState u,float seconds)
        {
            if(!Active(u))return;
            if(u.enemy)u.retreatUntil=Math.Max(u.retreatUntil,Time+seconds);
            else {u.retreatUntil=Math.Max(u.retreatUntil,Time+seconds);Buff(u,"retreat-"+frame+"-"+u.modifiers.Count,Stat.BlockReduction,1,seconds);}
        }
        private void TickStatus(UnitState u)
        {
            u.modifiers.RemoveAll(m=>m.expires<=Time);
            u.hp=Math.Min(u.hp,MaxHP(u));
            u.dotTimer+=StepSeconds;
            if(u.dotTimer>=1-.0001f)
            {
                u.dotTimer-=1;
                int burn=u.burns.Count(b=>b.expires>=Time-.0001f);
                if(burn>0)DamageDirect(u,burn*100);
                if(!u.enemy&&u.poisonUntil>=Time-.0001f&&u.poisonUntil>0)DamageDirect(u,200);
            }
            u.burns.RemoveAll(b=>b.expires<=Time);
        }
        public void CreatePoisonTile(UnitState source,int x,int y,float duration,float damage=200,float slow=-.2f)
        {
            duration=BattleItemPoisonDuration(source,duration);
            var tile=PoisonTiles.FirstOrDefault(t=>t.x==x&&t.y==y);
            if(tile==null){tile=new PoisonTile{x=x,y=y,source=source};PoisonTiles.Add(tile);}
            tile.expires=Math.Max(tile.expires,Time+duration);tile.damage=damage;tile.slow=slow;
        }
        private void TickPoisonTiles()
        {
            PoisonTiles.RemoveAll(p=>p.expires<=Time);
            foreach(var p in PoisonTiles)
                foreach(var e in Enemies.Where(Active))
                    if(Math.Abs(e.x-p.x)<.5f&&Math.Abs(e.y-p.y)<.5f)
                    {
                        Poison(e,1.1f);Buff(e,"poison-tile",Stat.MoveSpeed,p.slow,1.1f);
                        if(!p.nextHit.TryGetValue(e.id,out float next)||Time>=next)
                        {DamageDirect(e,p.damage,p.source);p.nextHit[e.id]=Time+1;}
                    }
        }
        private void MoveEnemies()
        {
            var blockCounts=new Dictionary<int,int>();
            // Reserve existing engagements first. Later enemies may pass a full blocker,
            // but must never swap places with the enemy it is already holding.
            foreach(var e in Enemies.Where(Active).OrderBy(e=>e.id))
            {
                var t=e.blocker;
                if(t==null)continue;
                blockCounts.TryGetValue(t.id,out int count);
                if(Time<e.retreatUntil||BlockCapacity(t)<=count||Distance(e,t)>.75f)e.blocker=null;
                else blockCounts[t.id]=count+1;
            }
            foreach(var e in Enemies.Where(Active).OrderByDescending(e=>e.progress).ThenBy(e=>e.id))
            {
                if(Time<e.retreatUntil)
                {if(Time>=e.frozenUntil&&!e.stopMoving)PositionOnPath(e,Math.Max(0,e.progress-e.definition.moveSpeed*StepSeconds));continue;}
                float speed=e.definition.moveSpeed*Math.Max(0,1+ModifierValue(e,Stat.MoveSpeed));
                float next=Math.Min(PathLength,e.progress+speed*StepSeconds);
                foreach(var t in Towers.Where(t=>e.blocker==null&&BlockCapacity(t)>0).OrderBy(t=>Distance(e,t)))
                {
                    blockCounts.TryGetValue(t.id,out int used);
                    if(used>=BlockCapacity(t))continue;
                    if(Distance(e,t)<=.6f+speed*StepSeconds)
                    {e.blocker=t;blockCounts[t.id]=used+1;break;}
                }
                if(e.blocker!=null||e.stopMoving||Time<e.frozenUntil)continue;
                PositionOnPath(e,next);
                if(e.progress>=PathLength-.0001f)
                {
                    int damage=Math.Min(Protection,goalOverrides[e.id]);
                    e.alive=false;Report.leaked++;Report.protectionLost+=damage;Protection-=damage;Emit("leak",e,"保护点受损",damage);
                    if(Protection==0){Finish(false);return;}
                }
            }
        }
        private void PositionOnPath(UnitState u,float progress)
        {
            u.progress=progress;
            for(int i=1;i<Config.path.Length;i++)
                if(progress<=pathDistances[i]+.0001f)
                {
                    var a=Config.path[i-1];var b=Config.path[i];float len=pathDistances[i]-pathDistances[i-1];
                    float k=len==0?0:(progress-pathDistances[i-1])/len;
                    u.x=a.x+(b.x-a.x)*k;u.y=a.y+(b.y-a.y)*k;
                    u.facingX=Math.Sign(b.x-a.x);u.facingY=Math.Sign(b.y-a.y);return;
                }
        }
        public void Forfeit() { if(!Finished) Finish(false); }
        private void Finish(bool won)
        {
            if(Finished)return;Finished=true;Report.won=won;Report.duration=Time;Report.bonusGpa=BattleItemBonusGpa(won);
            Emit("finished",null,won?"对决胜利":"保护点失守");
        }
        public void Emit(string kind,UnitState u,string text,int amount=0)
        {
            Events.Enqueue(new BattleEvent {kind=kind,text=text,amount=amount,time=Time,x=u==null?0:u.x,y=u==null?0:u.y,unitId=u==null?0:u.id});
            while(Events.Count>2048)Events.Dequeue();
        }
    }
}
