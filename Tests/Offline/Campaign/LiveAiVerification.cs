using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using FinalDefense.Campaign;
using FinalDefense.Contracts;
using FinalDefense.Dialogue;
using FinalDefense.Persistence;

internal static class LiveAiVerification
{
    private sealed class Capture : IDialogueService
    {
        public IDialogueService inner; public DialogueTurnResult latest; public string failure;
        public async Task<DialogueTurnResult> SendAsync(DialogueTurnRequest r,Action<string> c,System.Threading.CancellationToken t) { try { latest=await inner.SendAsync(r,c,t); return latest; } catch(Exception e) { failure=e is InvalidDataException ? e.Message : e.GetType().Name; throw; } }
    }
    public static async Task<int> Run(string root, string output)
    {
        var codec = new JsonCodec();
        var content = codec.Decode<CampaignContent>(File.ReadAllText(Path.Combine(root,"Assets/Resources/Campaign/Content.json")));
        var rules = codec.Decode<CampaignRules>(File.ReadAllText(Path.Combine(root,"Assets/Resources/Campaign/Rules.json")));
        var results = new List<object>(); bool allPassed = true;
        foreach(var npc in content.npcs.Where(n=>n.romance && (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("FINALDEFENSE_LIVE_NPC_FILTER")) || Environment.GetEnvironmentVariable("FINALDEFENSE_LIVE_NPC_FILTER").Split(',').Contains(n.id))))
        {
            var store = new AtomicCampaignStore(Path.Combine(output,"live-isolated",npc.id),codec);
            var campaign = new CampaignService(content,rules,store,codec,CampaignService.NewState("live_"+npc.id,content,rules,42));
            campaign.CompleteIntroduction("intro");
            campaign.EditAppointments("book",Enumerable.Range(0,3).Select(i=>new Appointment{npcId=npc.id,locationId="library"}).ToArray());
            campaign.ConfirmAppointments("confirm");
            using var service = new DeepSeekDialogueService(EmbeddedAiConfig.ApiKey,EmbeddedAiConfig.Endpoint,EmbeddedAiConfig.Model,codec);
            var capture = new Capture { inner = service };
            using var coordinator = new CampaignDialogueCoordinator(campaign,capture,TimeSpan.FromSeconds(45));
            int chunks=0; string streamed="";
            var opening=await coordinator.SendAsync("opening","",part=>{chunks++;streamed+=part;});
            string openingText=streamed; int openingChunks=chunks; chunks=0;streamed="";
            var reply=opening.Success ? await coordinator.SendAsync("reply","我对"+npc.positiveTopics[0]+"很感兴趣，想听听你的看法，也想和你一起聊聊。",part=>{chunks++;streamed+=part;}) : OperationResult.Fail(OperationError.Unavailable,"opening failed");
            var s=campaign.Snapshot; bool passed=opening.Success && reply.Success && chunks>0 && openingChunks>0 && s.dialogue.turns==1;
            allPassed &= passed;
            results.Add(new{npc=npc.id,passed,openingError=opening.error.ToString(),replyError=reply.error.ToString(),transportFailure=capture.failure,modelTopic=capture.latest?.topic,modelEmotion=capture.latest?.emotion,modelHints=capture.latest?.hints,openingText,replyText=streamed,openingChunks,replyChunks=chunks,emotion=s.dialogue.emotion,hints=s.dialogue.hints,favorDelta=reply.favorDelta});
            Console.WriteLine("LIVE "+npc.id+" passed="+passed+" opening="+opening.error+" reply="+reply.error+" chunks="+chunks);
            File.WriteAllText(Path.Combine(output,"live-ai-results.json"),System.Text.Json.JsonSerializer.Serialize(new{passed=allPassed,scope="Real DeepSeek adapter and Campaign coordinator; isolated synthetic conversations, not Unity player verification",results},new System.Text.Json.JsonSerializerOptions{WriteIndented=true}));
        }
        return allPassed?0:1;
    }
}
