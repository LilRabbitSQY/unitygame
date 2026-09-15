using System;
using System.Threading;
using System.Threading.Tasks;
using FinalDefense.Campaign;
using FinalDefense.Contracts;

namespace FinalDefense.Dialogue
{
    public sealed class CampaignDialogueCoordinator : IDisposable
    {
        private readonly CampaignService campaign; private readonly IDialogueService service; private readonly TimeSpan timeout;
        private CancellationTokenSource pending; private bool explicitlyCancelled; private readonly object gate = new object();
        public bool IsBusy { get { lock (gate) return pending != null; } }
        public CampaignDialogueCoordinator(CampaignService campaign, IDialogueService service, TimeSpan? timeout = null)
        { this.campaign = campaign; this.service = service; this.timeout = timeout ?? TimeSpan.FromSeconds(45); }
        public async Task<OperationResult> SendAsync(string turnId, string input, Action<string> chunk, CancellationToken cancel = default)
        {
            CancellationTokenSource requestCancellation;
            lock (gate)
            {
                if (pending != null) return OperationResult.Fail(OperationError.Conflict, "回复正在生成，请勿重复发送");
                explicitlyCancelled = false;
                pending = requestCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancel);
            }
            try
            {
                DialogueTurnRequest request;
                try { request = campaign.CreateDialogueRequest(turnId, input); }
                catch (ArgumentException) { return OperationResult.Fail(OperationError.InvalidInput, "输入不能为空或超过1000字"); }
                catch (InvalidOperationException) { return OperationResult.Fail(OperationError.WrongPhase, "当前无法发送对话"); }
                if (service == null) return OperationResult.Fail(OperationError.Unavailable, "AI服务尚未配置，当前行程已保留");
                requestCancellation.CancelAfter(timeout);
                var task = service.SendAsync(request, fragment =>
                {
                    if (requestCancellation.IsCancellationRequested || campaign.Snapshot.dialogue?.conversationId != request.conversationId) return;
                    if (fragment != null && fragment.Length <= 1024) chunk?.Invoke(fragment);
                }, requestCancellation.Token);
                // Enforce cancellation even when an adapter ignores its token. Late replies cannot commit.
                var cancellationTask = Task.Delay(Timeout.Infinite, requestCancellation.Token);
                if (await Task.WhenAny(task, cancellationTask) != task)
                {
                    _ = task.ContinueWith(t => { var ignored = t.Exception; }, TaskContinuationOptions.OnlyOnFaulted);
                    return OperationResult.Fail((cancel.IsCancellationRequested || explicitlyCancelled) ? OperationError.Cancelled : OperationError.Timeout, "请求已中止，未消耗行程，可重试");
                }
                var reply = await task;
                requestCancellation.Token.ThrowIfCancellationRequested();
                return campaign.CommitDialogue(request, reply);
            }
            catch (OperationCanceledException) { return OperationResult.Fail((cancel.IsCancellationRequested || explicitlyCancelled) ? OperationError.Cancelled : OperationError.Timeout, "请求已中止，可重试"); }
            catch { return OperationResult.Fail(OperationError.Unavailable, "AI服务暂时不可用，未结算，可重试"); }
            finally
            {
                lock (gate) { if (pending == requestCancellation) pending = null; }
                requestCancellation.Dispose();
            }
        }
        public void Cancel() { lock (gate) { explicitlyCancelled = true; pending?.Cancel(); } }
        public void Dispose() => Cancel();
    }
}
