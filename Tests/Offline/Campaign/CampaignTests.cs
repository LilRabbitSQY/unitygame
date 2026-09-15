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
    public string topic = "neutral", emotion = "neutral"; public bool invalid, fail, never; public int calls;
    public async Task<DialogueTurnResult> SendAsync(DialogueTurnRequest r, Action<string> chunk, CancellationToken token)
    {
        calls++; if (never) { await Task.Delay(200); } else await Task.Delay(1, token);
        if (fail) throw new IOException("test network failure");
        chunk?.Invoke("测试文本");
        return new DialogueTurnResult { conversationId = r.conversationId, turnId = r.turnId, status = DialogueStatus.Completed,
            text = invalid ? new string('字', 121) : "测试文本", emotion = r.opening ? "neutral" : emotion, topic = r.opening ? "neutral" : topic,
            hints = new[] { "提示一", "提示二", "提示三" }, endConversation = !r.opening };
    }
}
internal sealed class FailingStore : ICampaignStore
{
    public void Write(CampaignSnapshot s) => throw new IOException(); public CampaignSnapshot Read(string id) => null; public SaveMetadata[] List() => Array.Empty<SaveMetadata>();
}
internal static class CampaignTests
{
    static readonly JsonCodec codec = new JsonCodec(); static CampaignContent content; static string directory; static int checks, cases;
    static void Check(bool condition, string message) { checks++; if (!condition) throw new Exception(message); }
    static void Good(OperationResult r) => Check(r.Success, r.error + ": " + r.message);
    static CampaignService Create(string id = "test", CampaignRules rules = null, CampaignSnapshot state = null, ICampaignStore store = null)
    { rules = rules ?? new CampaignRules(); store = store ?? new AtomicCampaignStore(Path.Combine(directory, Guid.NewGuid().ToString("N")), codec); return new CampaignService(content, rules, store, codec, state ?? CampaignService.NewState(id, content, rules, 42)); }
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
        content=codec.Decode<CampaignContent>(File.ReadAllText(Path.Combine(args[0],"Assets/Resources/Campaign/Content.json")));directory=Path.Combine(args[1],"isolated-saves");Directory.CreateDirectory(directory);
        try
        {
            await Run("initial data and defensive copies",()=> { var c=Create();var s=c.Snapshot;Check(s.gpa==7000,"70 initial GPA");Check(s.npcs.Select(n=>n.favor).SequenceEqual(new[]{0,5,2,5,1,3}),"favors");Check(s.npcs.Select(n=>n.gpa).SequenceEqual(new[]{9200,8500,9000,8300,7600,8600}),"NPC GPA");s.gpa=1;Check(c.Snapshot.gpa==7000,"defensive copy");Check(BattleItemDefs.All.Length==19 && BattleItemDefs.BattleItems.Length==12,"19 items, 12 combat");return Task.CompletedTask;});
            await Run("booking validation, lock and restore",async()=> { var store=new AtomicCampaignStore(Path.Combine(directory,"booking"),codec);var c=Create(store:store);Check(!c.ConfirmAppointments(Id()).Success,"no bypass");Good(c.CompleteIntroduction("intro"));Good(c.CompleteIntroduction("intro"));Check(!c.EnterShop("intro").Success,"action conflict");Check(!c.EditAppointments(Id(),new[]{new Appointment{npcId="wang_yijun",locationId="library"}}).Success,"cameo not bookable");await Three(c);var restored=Create(state:store.Read("test"),store:store);Check(restored.Snapshot.slot==3,"restore progress");Check(!restored.ConfirmAppointments(Id()).Success,"no repeat");});
            await Run("dialogue errors, cancellation, daily favor cap",async()=> {var c=Create();await Three(c,"wan_sirui","美妆","happy");Check(c.Snapshot.npcs[1].favor==10,"daily +5");Check(c.Snapshot.inventory.All(i=>i.count==0),"unresolved drop probability not silently invented");var another=Create();Good(another.CompleteIntroduction(Id()));Good(another.EditAppointments(Id(),Enumerable.Range(0,3).Select(i=>new Appointment{npcId="liu_ruoshui",locationId="library"}).ToArray()));Good(another.ConfirmAppointments(Id()));var fake=new FakeDialogue{invalid=true};using var d=new CampaignDialogueCoordinator(another,fake,TimeSpan.FromMilliseconds(10));Check(!(await d.SendAsync(Id(),"",null)).Success,"invalid response rejected");Check(!another.Snapshot.dialogue.opened,"no state consumed");fake.invalid=false;fake.never=true;Check(!(await d.SendAsync(Id(),"",null)).Success,"timeout rejected");await Task.Delay(230);Check(!another.Snapshot.dialogue.opened,"late result not applied");fake.never=false;Good(await d.SendAsync(Id(),"",null));Check(!(await d.SendAsync(Id()," ",null)).Success,"empty input");});
            await Run("battle settlement, duplicates and GPA precision",async()=> {var c=Create();await Three(c);Prepare(c);var o=Outcome(c,true);Good(c.CommitBattleOutcome(o));Check(c.Snapshot.gpa==7200,"victory +2, protection ignored");Check(c.Snapshot.npcs[0].gpa==9150,"opponent -0.5");Good(c.CommitBattleOutcome(o));Check(c.Snapshot.gpa==7200,"duplicate no reward");o.reason=BattleEndReason.Defeat;Check(!c.CommitBattleOutcome(o).Success,"conflicting outcome");Good(c.EnterShop(Id()));Good(c.Buy("water","espresso_focus"));Good(c.Buy("water","espresso_focus"));Check(c.Snapshot.gpa==7180 && c.Snapshot.inventory.First(i=>i.itemId=="espresso_focus").count==1,"20 cents once");Good(c.NextDay(Id()));await Three(c);Prepare(c,items:new[]{"espresso_focus"});Check(c.Snapshot.inventory.First(i=>i.itemId=="espresso_focus").count==0,"consumed once");Good(c.CommitBattleOutcome(Outcome(c,true)));Check(c.Snapshot.gpa==7580,"second victory +4");});
            await Run("gift target, stock, cap and atomic failures",async()=> {var c=Create();await Three(c);Prepare(c);Good(c.CommitBattleOutcome(Outcome(c,true)));Good(c.EnterShop(Id()));Good(c.Buy(Id(),"fan_comic"));Check(!c.Gift(Id(),"fan_comic","wan_sirui").Success,"wrong NPC");Good(c.Gift("gift","fan_comic","liu_ruoshui"));Good(c.Gift("gift","fan_comic","liu_ruoshui"));Check(c.Snapshot.npcs[0].favor==5,"gift once");Check(!c.Buy(Id(),"fan_comic").Success,"one lifetime stock");Good(c.Buy(Id(),"heart_card"));Check(!c.Gift(Id(),"heart_card","liu_ruoshui").Success,"cap no effect");Check(c.Snapshot.inventory.First(i=>i.itemId=="heart_card").count==1,"no loss on failed gift");var broken=Create(store:new FailingStore());Check(!broken.CompleteIntroduction("x").Success && broken.Snapshot.phase==CampaignStage.Introduction,"failed write rolls back");});
            await Run("forfeit, retries and crash checkpoint",async()=> {var r=new CampaignRules{retriesAllowed=true};var store=new AtomicCampaignStore(Path.Combine(directory,"battle"),codec);var c=Create(rules:r,store:store);await Three(c);Prepare(c);var b=c.Snapshot.battle;var restored=Create(rules:r,state:store.Read("test"),store:store);Check(restored.Snapshot.battle.attemptId==b.attemptId,"crash retains attempt");var o=Outcome(restored,false);o.reason=BattleEndReason.Forfeit;Good(restored.CommitBattleOutcome(o));Check(restored.Snapshot.gpa==6700,"forfeit -3");Good(restored.RetryBattle(Id()));Good(restored.CommitBattleOutcome(Outcome(restored,true)));Check(restored.Snapshot.gpa==6700 && restored.Snapshot.streak==0,"retry does not re-settle daily score");});
            await Run("28 days with real campaign transactions, six endings",async()=> {foreach(var npc in content.npcs.Where(n=>n.romance)){var c=Create();for(int day=1;day<=28;day++){await Three(c,npc.id,npc.positiveTopics[0],"happy");Prepare(c,content.npcs[(day-1)%6].id);Good(c.CommitBattleOutcome(Outcome(c,true)));if(day<28){Good(c.EnterShop(Id()));Good(c.Buy(Id(),"sparkling_focus"));Good(c.NextDay(Id()));}}Check(c.Snapshot.endingId=="success_"+npc.id,"companion ending "+npc.id);Check(c.Ranking.First(r=>r.id=="player").rank==1,"rank first with shopping");Check(c.Snapshot.npcs.First(n=>n.npcId==npc.id).favor==100,"favor reachable");}});
            await Run("early negative GPA and final rank failures",async()=> {var c=Create();for(int day=1;day<=24;day++){await Three(c);Prepare(c);Good(c.CommitBattleOutcome(Outcome(c,false)));if(day<24){Good(c.EnterShop(Id()));Good(c.NextDay(Id()));}}Check(c.Snapshot.gpa==-200 && c.Snapshot.endingId=="failure_gpa","negative failure");var x=Create();for(int day=1;day<=28;day++){await Three(x);Prepare(x);Good(x.CommitBattleOutcome(Outcome(x,day%2==0)));if(day<28){Good(x.EnterShop(Id()));Good(x.NextDay(Id()));}}Check(x.Snapshot.endingId=="failure_rank","rank failure");});
            await Run("atomic backup, multiple slots, settings and global unlocks",()=> {var store=new AtomicCampaignStore(Path.Combine(directory,"files"),codec);var c=Create("one",store:store);store.Write(c.Snapshot);Good(c.CompleteIntroduction(Id()));var two=Create("two",store:store);store.Write(two.Snapshot);Check(store.List().Length==2,"two slots");File.WriteAllText(Path.Combine(directory,"files","one.json"),"broken");Check(store.List().Count(s=>s.error!=null)==1,"corrupt metadata");Check(store.ReadBackup("one").phase==CampaignStage.Introduction,"backup valid");store.SaveSettings(new GameSettings{music=.4f});Check(store.LoadSettings().music==.4f,"settings");store.RecordUnlock("success_liu_ruoshui");store.RecordUnlock("success_liu_ruoshui");Check(store.LoadUnlocks().endings.Length==1,"global dedupe");return Task.CompletedTask;});
            Console.WriteLine($"{cases} scenarios passed; {checks} assertions; 0 failed.");return 0;
        }
        catch(Exception ex){Console.Error.WriteLine(ex);return 1;}
    }
}
