using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using FinalDefense.Contracts;
using FinalDefense.Campaign;
using FinalDefense.Persistence;
using FinalDefense.Dialogue;
using FinalDefense.Shop;

internal sealed class JsonCodec : IDataCodec
{
    static readonly JsonSerializerOptions Options = new JsonSerializerOptions { IncludeFields = true };
    public string Encode<T>(T value) => JsonSerializer.Serialize(value, Options);
    public T Decode<T>(string text) => JsonSerializer.Deserialize<T>(text, Options);
}
internal sealed class FakeDialogue : IDialogueService
{
    public string topic = "neutral", emotion = "neutral"; public bool invalid, fail, never, finish = true; public int calls;
    public async Task<DialogueTurnResult> SendAsync(DialogueTurnRequest r, Action<string> chunk, CancellationToken token)
    {
        calls++; if (never) { await Task.Delay(200); } else await Task.Delay(1, token);
        if (fail) throw new IOException("test network failure");
        chunk?.Invoke("测试文本");
        return new DialogueTurnResult { conversationId = r.conversationId, turnId = r.turnId, status = DialogueStatus.Completed,
            text = invalid ? new string('字', 121) : "测试文本", emotion = r.opening ? "neutral" : emotion, topic = r.opening ? "neutral" : topic,
            hints = new[] { "提示一", "提示二", "提示三" }, endConversation = !r.opening && finish };
    }
}
internal sealed class FailingStore : ICampaignStore
{
    public void Write(CampaignSnapshot s) => throw new IOException(); public CampaignSnapshot Read(string id) => null; public SaveMetadata[] List() => Array.Empty<SaveMetadata>();
}
internal sealed class GatewayTestTransport : System.Net.Http.HttpMessageHandler
{
    public string body, mediaType = "text/event-stream";
    public System.Net.HttpStatusCode status = System.Net.HttpStatusCode.OK;
    public string receivedBody, receivedToken;
    protected override async Task<System.Net.Http.HttpResponseMessage> SendAsync(System.Net.Http.HttpRequestMessage message, CancellationToken token)
    {
        receivedBody = await message.Content.ReadAsStringAsync(); receivedToken = message.Headers.Authorization?.Parameter;
        return new System.Net.Http.HttpResponseMessage(status) { Content = new System.Net.Http.StringContent(body, System.Text.Encoding.UTF8, mediaType) };
    }
}
internal static class CampaignTests
{
    static readonly JsonCodec codec = new JsonCodec(); static CampaignContent content; static string directory; static int checks, cases;
    static void Check(bool condition, string message) { checks++; if (!condition) throw new Exception(message); }
    static void Good(OperationResult r) => Check(r.Success, r.error + ": " + r.message);
    static CampaignService Create(string id = "test", CampaignRules rules = null, CampaignSnapshot state = null, ICampaignStore store = null)
    { rules = rules ?? new CampaignRules { cameoChancePerTenThousand = -1, topicDropChancePerTenThousand = -1, battleFavorChancePerTenThousand = 0 }; store = store ?? new AtomicCampaignStore(Path.Combine(directory, Guid.NewGuid().ToString("N")), codec); return new CampaignService(content, rules, store, codec, state ?? CampaignService.NewState(id, content, rules, 42)); }
    static string Id() => Guid.NewGuid().ToString("N");
    static async Task Three(CampaignService c, string npc = "liu_ruoshui", string topic = "neutral", string emotion = "neutral")
    {
        if (c.Snapshot.phase == CampaignStage.Introduction) Good(c.CompleteIntroduction(Id()));
        Good(c.EditAppointments(Id(), Enumerable.Range(0,3).Select(i => new Appointment { npcId = npc, locationId = "library" }).ToArray()));
        Good(c.ConfirmAppointments(Id()));
        for (int i=0;i<3;i++)
        {
            var fake = new FakeDialogue { topic = topic, emotion = emotion }; using var d = new CampaignDialogueCoordinator(c, fake);
            Good(await d.SendAsync(Id(), "", null)); Good(await d.SendAsync(Id(), "用户输入", null)); Good(c.FinishDialogue(Id()));
        }
        Check(c.Snapshot.phase == CampaignStage.Matching, "three actions unlock matching");
    }
    static void Prepare(CampaignService c, string opponent = "liu_ruoshui", string[] items = null)
    {
        var s=c.Snapshot;
        Good(c.SaveDraft(Id(),new DraftSnapshot { battleId=s.draftRequest.battleId,opponentId=opponent,seed=s.draftRequest.seed,rulesVersion=s.rulesVersion,
            playerFirst=s.gpa <= s.npcs.First(n=>n.npcId==opponent).gpa,locked=true,playerUnits=new[]{"tower_a"},enemyUnits=new[]{"tower_b"} }));
        Good(c.PrepareBattle(Id(), "day-"+s.day.ToString("00"),"test-level",items??Array.Empty<string>(),30,10));
    }
    static BattleOutcome Outcome(CampaignService c,bool won) { var b=c.Snapshot.battle; return new BattleOutcome{runId=b.runId,battleId=b.battleId,attemptId=b.attemptId,reason=won?BattleEndReason.Victory:BattleEndReason.Defeat,protectionLost=9}; }
    static async Task Run(string name, Func<Task> body) { await body(); cases++; Console.WriteLine("PASS " + name); }
    public static async Task<int> Main(string[] args)
    {
        if(args.Length>2 && args[2]=="--live-ai") return await LiveAiVerification.Run(args[0],args[1]);
        if(args.Length>0 && args[0]=="--child")
        {
            content=codec.Decode<CampaignContent>(File.ReadAllText(Path.Combine(args[1],"Assets/Resources/Campaign/Content.json")));
            var childStore=new AtomicCampaignStore(args[2],codec);
            var childCampaign=new CampaignService(content,new CampaignRules(),childStore,codec,childStore.Read("cross"));
            return childCampaign.CompleteIntroduction("child-intro").Success ? 0 : 1;
        }
        content=codec.Decode<CampaignContent>(File.ReadAllText(Path.Combine(args[0],"Assets/Resources/Campaign/Content.json")));directory=Path.Combine(args[1],"isolated-saves");Directory.CreateDirectory(directory);
        try
        {
            await Run("initial data and defensive copies",()=> { var c=Create();var s=c.Snapshot;Check(s.gpa==7000,"70 initial GPA");Check(s.npcs.Select(n=>n.favor).SequenceEqual(new[]{0,5,2,5,1,3}),"favors");Check(s.npcs.Select(n=>n.gpa).SequenceEqual(new[]{9200,8500,9000,8300,7600,8600}),"NPC GPA");s.gpa=1;Check(c.Snapshot.gpa==7000,"defensive copy");Check(BattleItemDefs.All.Length==19 && BattleItemDefs.BattleItems.Length==12,"19 items, 12 combat");return Task.CompletedTask;});
            await Run("booking validation, lock and restore",async()=> { var store=new AtomicCampaignStore(Path.Combine(directory,"booking"),codec);var c=Create(store:store);Check(!c.ConfirmAppointments(Id()).Success,"no bypass");Good(c.CompleteIntroduction("intro"));Good(c.CompleteIntroduction("intro"));Check(!c.EnterShop("intro").Success,"action conflict");Check(!c.EditAppointments(Id(),new[]{new Appointment{npcId="wang_yijun",locationId="library"}}).Success,"cameo not bookable");await Three(c);var restored=Create(state:store.Read("test"),store:store);Check(restored.Snapshot.slot==3,"restore progress");Check(!restored.ConfirmAppointments(Id()).Success,"no repeat");});
            await Run("dialogue errors, cancellation, daily favor cap",async()=> {var c=Create();await Three(c,"wan_sirui","美妆","happy");Check(c.Snapshot.npcs[1].favor==10,"daily +5");Check(c.Snapshot.inventory.All(i=>i.count==0),"unresolved drop probability not silently invented");var another=Create();Good(another.CompleteIntroduction(Id()));Good(another.EditAppointments(Id(),Enumerable.Range(0,3).Select(i=>new Appointment{npcId="liu_ruoshui",locationId="library"}).ToArray()));Good(another.ConfirmAppointments(Id()));var fake=new FakeDialogue{invalid=true};using var d=new CampaignDialogueCoordinator(another,fake,TimeSpan.FromMilliseconds(10));Check(!(await d.SendAsync(Id(),"",null)).Success,"invalid response rejected");Check(!another.Snapshot.dialogue.opened,"no state consumed");fake.invalid=false;fake.never=true;Check(!(await d.SendAsync(Id(),"",null)).Success,"timeout rejected");await Task.Delay(230);Check(!another.Snapshot.dialogue.opened,"late result not applied");fake.never=false;Good(await d.SendAsync(Id(),"",null));Check(!(await d.SendAsync(Id()," ",null)).Success,"empty input");});
            await Run("battle settlement, duplicates and GPA precision",async()=> {var c=Create();await Three(c);Prepare(c);var o=Outcome(c,true);Good(c.CommitBattleOutcome(o));Check(c.Snapshot.gpa==7200,"victory +2, protection ignored");Check(c.Snapshot.npcs[0].gpa==9150,"opponent -0.5");Good(c.CommitBattleOutcome(o));Check(c.Snapshot.gpa==7200,"duplicate no reward");o.reason=BattleEndReason.Defeat;Check(!c.CommitBattleOutcome(o).Success,"conflicting outcome");Good(c.EnterShop(Id()));Good(c.Buy("water","espresso_focus"));Good(c.Buy("water","espresso_focus"));Check(c.Snapshot.gpa==7180 && c.Snapshot.inventory.First(i=>i.itemId=="espresso_focus").count==1,"20 cents once");Good(c.NextDay(Id()));await Three(c);Prepare(c,items:new[]{"espresso_focus"});Check(c.Snapshot.inventory.First(i=>i.itemId=="espresso_focus").count==0,"consumed once");Good(c.CommitBattleOutcome(Outcome(c,true)));Check(c.Snapshot.gpa==7580,"second victory +4");});
            await Run("gift target, stock, cap and atomic failures",async()=> {var c=Create();await Three(c);Prepare(c);Good(c.CommitBattleOutcome(Outcome(c,true)));Good(c.EnterShop(Id()));Good(c.Buy(Id(),"fan_comic"));Check(!c.Gift(Id(),"fan_comic","wan_sirui").Success,"wrong NPC");Good(c.Gift("gift","fan_comic","liu_ruoshui"));Good(c.Gift("gift","fan_comic","liu_ruoshui"));Check(c.Snapshot.npcs[0].favor==5,"gift once");Check(!c.Buy(Id(),"fan_comic").Success,"one lifetime stock");Good(c.Buy(Id(),"heart_card"));Check(!c.Gift(Id(),"heart_card","liu_ruoshui").Success,"cap no effect");Check(c.Snapshot.inventory.First(i=>i.itemId=="heart_card").count==1,"no loss on failed gift");var broken=Create(store:new FailingStore());Check(!broken.CompleteIntroduction("x").Success && broken.Snapshot.phase==CampaignStage.Introduction,"failed write rolls back");});
            await Run("forfeit, retries and crash checkpoint",async()=> {var r=new CampaignRules{retriesAllowed=true,retryPrice=0};var store=new AtomicCampaignStore(Path.Combine(directory,"battle"),codec);var c=Create(rules:r,store:store);await Three(c);Prepare(c);var b=c.Snapshot.battle;var restored=Create(rules:r,state:store.Read("test"),store:store);Check(restored.Snapshot.battle.attemptId==b.attemptId,"crash retains attempt");var o=Outcome(restored,false);o.reason=BattleEndReason.Forfeit;Good(restored.CommitBattleOutcome(o));Check(restored.Snapshot.gpa==6700,"forfeit -3");Good(restored.RetryBattle(Id()));Good(restored.CommitBattleOutcome(Outcome(restored,true)));Check(restored.Snapshot.gpa==6700 && restored.Snapshot.streak==0,"retry does not re-settle daily score");});
            await Run("28 days with real campaign transactions, six endings",async()=> {foreach(var npc in content.npcs.Where(n=>n.romance)){var c=Create();for(int day=1;day<=28;day++){await Three(c,npc.id,npc.positiveTopics[0],"happy");Prepare(c,content.npcs[(day-1)%6].id);Good(c.CommitBattleOutcome(Outcome(c,true)));if(day<28){Good(c.EnterShop(Id()));Good(c.Buy(Id(),"sparkling_focus"));Good(c.NextDay(Id()));}}Check(c.Snapshot.endingId=="success_"+npc.id,"companion ending "+npc.id);Check(c.Ranking.First(r=>r.id=="player").rank==1,"rank first with shopping");Check(c.Snapshot.npcs.First(n=>n.npcId==npc.id).favor==100,"favor reachable");}});
            await Run("early negative GPA and final rank failures",async()=> {var c=Create();for(int day=1;day<=24;day++){await Three(c);Prepare(c);Good(c.CommitBattleOutcome(Outcome(c,false)));if(day<24){Good(c.EnterShop(Id()));Good(c.NextDay(Id()));}}Check(c.Snapshot.gpa==-200 && c.Snapshot.endingId=="failure_gpa","negative failure");var x=Create();for(int day=1;day<=28;day++){await Three(x);Prepare(x);Good(x.CommitBattleOutcome(Outcome(x,day%2==0)));if(day<28){Good(x.EnterShop(Id()));Good(x.NextDay(Id()));}}Check(x.Snapshot.endingId=="failure_rank","rank failure");});
            await Run("atomic backup, multiple slots, settings and global unlocks",()=> {var store=new AtomicCampaignStore(Path.Combine(directory,"files"),codec);var c=Create("one",store:store);store.Write(c.Snapshot);Good(c.CompleteIntroduction(Id()));var two=Create("two",store:store);store.Write(two.Snapshot);Check(store.List().Length==2,"two slots");File.WriteAllText(Path.Combine(directory,"files","one.json"),"broken");Check(store.List().Count(s=>s.error!=null)==1,"corrupt metadata");Check(store.ReadBackup("one").phase==CampaignStage.Introduction,"backup valid");store.SaveSettings(new GameSettings{music=.4f});Check(store.LoadSettings().music==.4f,"settings");store.RecordUnlock("success_liu_ruoshui");store.RecordUnlock("success_liu_ruoshui");Check(store.LoadUnlocks().endings.Length==1,"global dedupe");return Task.CompletedTask;});
            await Run("catalog news and deterministic reload",()=> { var c=Create();Check(content.news.Length==39,"39 sourced articles");Check(c.News.Length>=1 && c.News.Length<=3,"1–3 per day");var restored=Create(state:c.Snapshot);Check(c.News.Select(n=>n.id).SequenceEqual(restored.News.Select(n=>n.id)),"no reroll");Good(c.ReadNews("news",c.News[0].id));Good(c.ReadNews("news",c.News[0].id));Check(c.Snapshot.readNews.Length==1,"read once");return Task.CompletedTask;});
            await Run("all six topic drops, before/after threshold, negative floor",async()=> {
                string[] topics={"ACGN同人","美妆","学术","盲盒","电竞","咖啡"};
                string[] items={"signed_fan_art","makeup_sample","competition_manual","balulu_figure","custom_keyboard","coffee_coupon"};
                for(int i=0;i<6;i++){
                    var r=new CampaignRules{topicDropChancePerTenThousand=10000};var state=CampaignService.NewState("drop",content,r,42);state.npcs[i].favor=38;
                    var c=Create(rules:r,state:state);await Three(c,content.npcs[i].id,topics[i],"happy");
                    Check(c.Snapshot.inventory.First(v=>v.itemId==items[i]).count==3,"one per conversation "+items[i]);
                }
                var beforeRules=new CampaignRules{topicDropChancePerTenThousand=10000,thresholdAfterTurn=false};var ss=CampaignService.NewState("before",content,beforeRules,42);ss.npcs[0].favor=38;
                var before=Create(rules:beforeRules,state:ss);await Three(before,"liu_ruoshui","ACGN同人","happy");Check(before.Snapshot.inventory.First(i=>i.itemId=="signed_fan_art").count==2,"first turn threshold uses pre-value");
                var negative=Create();await Three(negative,"wan_sirui","冷漠回绝","sad");Check(negative.Snapshot.npcs[1].favor==0 && negative.Snapshot.npcs[1].dailyGain==0,"negative unrestricted, floor zero");
            });
            await Run("cameo rewards, invalid service and duplicate inflight",async()=> {
                bool teacher=false,dean=false;
                for(int seed=1;seed<=12 && !(teacher&&dean);seed++){
                    var r=new CampaignRules{cameoChancePerTenThousand=10000};var c=Create(rules:r,state:CampaignService.NewState("cameo",content,r,seed));
                    await Three(c);teacher |= c.Snapshot.inventory.First(i=>i.itemId=="leave_note").count>0;dean |= c.Snapshot.inventory.First(i=>i.itemId=="recommendation_letter").count>0;
                }
                Check(teacher&&dean,"both cameo paths");
                var c2=Create();Good(c2.CompleteIntroduction(Id()));Good(c2.EditAppointments(Id(),Enumerable.Range(0,3).Select(i=>new Appointment{npcId="liu_ruoshui",locationId="library"}).ToArray()));Good(c2.ConfirmAppointments(Id()));
                var fake=new FakeDialogue{never=true};using var coordinator=new CampaignDialogueCoordinator(c2,fake,TimeSpan.FromSeconds(1));var pending=coordinator.SendAsync("inflight","",null);Check(!(await coordinator.SendAsync("another","",null)).Success,"double send refused");coordinator.Cancel();Check(!(await pending).Success,"cancel no effect");await Task.Delay(220);Check(!c2.Snapshot.dialogue.opened,"cancelled late result ignored");
                fake.never=false;fake.fail=true;Check(!(await coordinator.SendAsync("network","",null)).Success,"network failure");Check(c2.Snapshot.npcs[0].favor==0,"failure no reward");
            });
            await Run("malformed saves, slot archive and unknown version",()=> {
                var original=Create().Snapshot;original.npcs[0].npcId=original.npcs[1].npcId;bool rejected=false;try{Create(state:original);}catch(ArgumentException){rejected=true;}Check(rejected,"duplicate NPC rejected");
                var store=new AtomicCampaignStore(Path.Combine(directory,"archive"),codec);var c=Create("slot",store:store);store.Write(c.Snapshot);store.ArchiveSlot("slot");Check(Directory.GetFiles(Path.Combine(directory,"archive"),"*.archive-*").Length==1,"permanent archive");
                var p=Path.Combine(directory,"archive","slot.json");File.WriteAllText(p,"{\"version\":99,\"saveId\":\"slot\"}");rejected=false;try{store.Read("slot");}catch(NotSupportedException){rejected=true;}Check(rejected,"unknown version not replaced");
                Check(File.ReadAllText(p).Contains("99"),"unknown file preserved");return Task.CompletedTask;
            });
            await Run("tied highest favor uses event sequence",async()=> {
                var r=new CampaignRules{battleFavorChancePerTenThousand=0};var state=CampaignService.NewState("tie",content,r,42);state.day=28;state.gpa=9900;
                foreach(var n in state.npcs){n.favor=10;n.reachedSequence=++state.sequence;}state.npcs[5].reachedSequence=1;
                var c=Create(rules:r,state:state);await Three(c);Prepare(c);Good(c.CommitBattleOutcome(Outcome(c,true)));Check(c.Snapshot.companionId=="ming_shan","earliest attained tie");
            });
            await Run("v1/v2 migration preserves GPA and rejects unsafe checkpoints",()=> {
                var r=new CampaignRules();
                var v1=LegacyCampaignMigration.Preview("{\"version\":1,\"day\":4,\"gpa\":100,\"phase\":1}","m1",content,r,codec,1);
                Check(v1.canMigrate && v1.proposed.gpa==10000 && v1.proposed.day==4,"v1 GPA preserved");Check(!string.IsNullOrEmpty(v1.proposed.migrationNotice),"missing history disclosed");
                var v2=LegacyCampaignMigration.Preview("{\"version\":2,\"day\":2,\"gpaHundredths\":9970,\"phase\":1,\"inventory\":[{\"key\":\"espresso_focus\",\"count\":2,\"purchased\":2}]}","m2",content,r,codec,1);
                Check(v2.canMigrate && v2.proposed.gpa==9970 && v2.proposed.inventory.First(i=>i.itemId=="espresso_focus").count==2,"v2 precise inventory");
                Check(!LegacyCampaignMigration.Preview("{\"version\":2,\"day\":2,\"gpaHundredths\":9000,\"phase\":2}","unsafe",content,r,codec,1).canMigrate,"battle cannot fabricate opponent");return Task.CompletedTask;
            });
            await Run("separate process save continuation",()=> {
                var path=Path.Combine(directory,"cross-process");var store=new AtomicCampaignStore(path,codec);store.Write(Create("cross",store:store).Snapshot);
                var start=new System.Diagnostics.ProcessStartInfo(Environment.ProcessPath){UseShellExecute=false};
                start.ArgumentList.Add(typeof(CampaignTests).Assembly.Location);start.ArgumentList.Add("--child");start.ArgumentList.Add(args[0]);start.ArgumentList.Add(path);
                using var process=System.Diagnostics.Process.Start(start);process.WaitForExit();Check(process.ExitCode==0,"child process result");
                Check(store.Read("cross").phase==CampaignStage.Booking,"child transaction persisted");Check(store.Read("cross").receipts.Count(r=>r.actionId=="child-intro")==1,"receipt crossed process boundary");return Task.CompletedTask;
            });
            await Run("Unity empty inline checkpoint normalization",()=> {
                var s=Create().Snapshot;s.dialogue=new DialogueCheckpoint();s.draftRequest=new DraftRequest();s.draft=new DraftSnapshot();s.battle=new BattleStartContext();s.outcome=new BattleOutcome();s.appointments[0]=new Appointment();
                CampaignSnapshotCompatibility.NormalizeEmptyCheckpoints(s);
                Check(s.dialogue==null && s.draftRequest==null && s.draft==null && s.battle==null && s.outcome==null && s.appointments[0]==null,"canonical null checkpoints");
                s.battle=new BattleStartContext{runId="present"};CampaignSnapshotCompatibility.NormalizeEmptyCheckpoints(s);Check(s.battle!=null,"partial corrupt identity not hidden");
                var migration=LegacyCampaignMigration.Preview("{\"version\":1,\"day\":4,\"gpa\":100,\"phase\":1}","news-day",content,new CampaignRules(),codec,1);
                Check(migration.proposed.dailyNewsIds.SequenceEqual(CampaignService.SelectNews(content,1,4)),"migration selects actual-day news");return Task.CompletedTask;
            });
            await Run("gateway SSE transport and malformed streams",async()=> {
                var request=new DialogueTurnRequest{conversationId="c",turnId="t",npcId="liu_ruoshui",opening=true};
                var reply=new DialogueTurnResult{conversationId="c",turnId="t",text="你好",emotion="neutral",topic="neutral",status=DialogueStatus.Completed,hints=new[]{"一","二","三"}};
                string valid="data: {\"delta\":\"你\"}\n\ndata: {\"delta\":\"好\"}\n\ndata: {\"result\":"+codec.Encode(reply)+"}\n\n";
                var transport=new GatewayTestTransport{body=valid};using(var gateway=new HttpDialogueGateway(new Uri("https://gateway.invalid/dialogue"),codec,transport,()=>"test-only-token"))
                {
                    string streamed="";var result=await gateway.SendAsync(request,s=>streamed+=s,CancellationToken.None);
                    Check(streamed=="你好" && result.text==streamed,"SSE chunks and final");Check(transport.receivedToken=="test-only-token" && transport.receivedBody.Contains("liu_ruoshui"),"request context and session authentication");
                }
                string[] bad={"data: {\"delta\":\"错\"}\n\ndata: {\"result\":"+codec.Encode(reply)+"}\n\n","data: not-json\n\n","data: {\"delta\":\"未完成\"}\n\n",new string('x',8193)+"\n", "data: {\"delta\":\""+new string('字',121)+"\"}\n\n"};
                foreach(var body in bad){using var gateway=new HttpDialogueGateway(new Uri("https://gateway.invalid/dialogue"),codec,new GatewayTestTransport{body=body});bool rejected=false;try{await gateway.SendAsync(request,null,CancellationToken.None);}catch{rejected=true;}Check(rejected,"malformed stream rejected");}
                using(var gateway=new HttpDialogueGateway(new Uri("https://gateway.invalid/dialogue"),codec,new GatewayTestTransport{body=valid,mediaType="application/json"}))
                {bool rejected=false;try{await gateway.SendAsync(request,null,CancellationToken.None);}catch(InvalidDataException){rejected=true;}Check(rejected,"wrong content type rejected");}
            });
            await Run("provisional production rules run without approval gate",async()=> {
                var rules=codec.Decode<CampaignRules>(File.ReadAllText(Path.Combine(args[0],"Assets/Resources/Campaign/Rules.json")));rules.Validate();
                Check(!rules.approved && rules.retriesAllowed && rules.retryPrice==100,"provisional metadata and paid retry");
                Check(rules.cameoChancePerTenThousand==1500 && rules.topicDropChancePerTenThousand==3500,"explicit drop defaults");
                rules.battleFavorChancePerTenThousand=10000;var c=Create(rules:rules);await Three(c,"wan_sirui");Prepare(c);var o=Outcome(c,true);Good(c.CommitBattleOutcome(o));Check(c.Snapshot.npcs[0].favor==1,"battle favor target");Good(c.CommitBattleOutcome(o));Check(c.Snapshot.npcs[0].favor==1,"battle favor dedupe");
                var loss=Create(rules:rules);await Three(loss);Prepare(loss);Good(loss.CommitBattleOutcome(Outcome(loss,false)));Good(loss.RetryBattle("paid-retry"));Good(loss.RetryBattle("paid-retry"));Check(loss.Snapshot.gpa==6600,"retry charges exactly one GPA");Good(loss.CommitBattleOutcome(Outcome(loss,true)));Check(loss.Snapshot.gpa==6600,"retry cannot farm score");
                rules.npcNewsChancePerTenThousand=10000;var npcs=c.Snapshot.npcs;npcs[0].favor=30;var news=CampaignService.SelectNews(content,42,2,npcs,rules);Check(news.Any(id=>content.news.First(n=>n.id==id).npcId=="liu_ruoshui"),"unlocked NPC news selected");
            });
            await Run("direct DeepSeek structured stream",async()=> {
                string reply="{\"text\":\"你好\\n学姐\",\"emotion\":\"neutral\",\"topic\":\"neutral\",\"hints\":[\"一\",\"二\",\"三\"],\"endConversation\":false}";
                string wire="";
                foreach(char character in reply) wire+="data: "+JsonSerializer.Serialize(new{choices=new[]{new{delta=new{content=character.ToString()},finish_reason=(string)null}}})+"\n\n";
                wire+="data: {\"choices\":[{\"delta\":{},\"finish_reason\":\"stop\"}]}\n\ndata: [DONE]\n\n";
                var transport=new GatewayTestTransport{body=wire};
                using(var service=new DeepSeekDialogueService("test-key","https://api.deepseek.com/chat/completions","deepseek-flash",codec,transport))
                {
                    string streamed="";var response=await service.SendAsync(new DialogueTurnRequest{conversationId="c",turnId="t",opening=true,persona="内敛",history=Array.Empty<DialogueLine>()},part=>streamed+=part,CancellationToken.None);
                    Check(response.text=="你好\n学姐" && streamed==response.text,"direct incremental text decoding");Check(response.conversationId=="c" && response.turnId=="t","local identity preserved");
                    using var body=JsonDocument.Parse(transport.receivedBody);Check(body.RootElement.GetProperty("stream").GetBoolean() && body.RootElement.GetProperty("response_format").GetProperty("type").GetString()=="json_object","provider wire format");
                    Check(transport.receivedToken=="test-key","embedded key authentication");
                }
                var bad=new GatewayTestTransport{body=wire.Replace("\"stop\"","\"length\"")};using(var service=new DeepSeekDialogueService("test-key","https://api.deepseek.com/chat/completions","deepseek-flash",codec,bad))
                {bool rejected=false;try{await service.SendAsync(new DialogueTurnRequest{opening=true},null,CancellationToken.None);}catch(InvalidDataException){rejected=true;}Check(rejected,"truncated generation rejected");}
                Check(DeepSeekDialogueService.PartialText("{\"emotion\":\"happy\",\"text\":\"你好")=="你好","property order independent");
                Check(DeepSeekDialogueService.PartialText("{\"nested\":{\"text\":\"wrong\"},\"text\":\"right")=="right","nested text ignored");
                Check(DeepSeekDialogueService.PartialText("{\"text\":\"a\\u4f")=="a","incomplete unicode buffered");
                Check(DeepSeekDialogueService.PartialText("{\"text\":\"a\\u4f60")=="a你","unicode decoded");
            });
            Console.WriteLine($"{cases} scenarios passed; {checks} assertions; 0 failed.");return 0;
        }
        catch(Exception ex){Console.Error.WriteLine(ex);return 1;}
    }
}
