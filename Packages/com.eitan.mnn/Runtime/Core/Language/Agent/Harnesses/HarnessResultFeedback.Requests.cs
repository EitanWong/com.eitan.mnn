using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace MNN.Samples.SiriAI.Harnesses
{
    public sealed partial class HarnessResultFeedback
    {
        private HarnessResultConnection _requestConnection;
        private readonly HashSet<string> _presentedRequests = new HashSet<string>();
        private readonly Dictionary<string, DateTime> _requestRetryAfter = new Dictionary<string, DateTime>();

        private async Task<bool> PresentPendingRequestAsync(HarnessResultConnection connection, CancellationToken token)
        {
            var requests = _presenter as IHarnessRequestPresenter;
            if (requests == null) return false;
            if (_requestConnection == null || !ReferenceEquals(connection.Client, _requestConnection.Client) ||
                !ReferenceEquals(connection.Scope, _requestConnection.Scope) || connection.ConversationId != _requestConnection.ConversationId)
            {
                _requestConnection = connection;
                _presentedRequests.Clear(); _requestRetryAfter.Clear();
            }
            HarnessTask[] tasks;
            using (var request = CancellationTokenSource.CreateLinkedTokenSource(token))
            {
                request.CancelAfter(TimeSpan.FromSeconds(5));
                tasks = await connection.Client.ListAsync(request.Token);
            }
            var pending = tasks.Where(task => IsPendingRequest(connection, task)).ToArray();
            var identities = new HashSet<string>(pending.Select(RequestIdentity));
            _presentedRequests.RemoveWhere(id => !identities.Contains(id));
            foreach (string key in _requestRetryAfter.Keys.Where(key => !identities.Contains(key)).ToArray()) _requestRetryAfter.Remove(key);
            var next = pending.FirstOrDefault(task => !_presentedRequests.Contains(RequestIdentity(task)) &&
                (!_requestRetryAfter.TryGetValue(RequestIdentity(task), out var after) || after <= DateTime.UtcNow));
            if (next == null) return false;
            if (!IsCurrent(connection) || !_presenter.CanPresent) return true;
            var current = await RpcAsync(ct => connection.Client.GetAsync(next.Id, ct), token);
            if (!IsPendingRequest(connection, current) || current.RequestId != next.RequestId) return true;
            _requestRetryAfter[RequestIdentity(current)] = DateTime.UtcNow.AddSeconds(5);
            await PresentRequestAsync(connection, current, requests, token);
            return true;
        }

        private static bool IsPendingRequest(HarnessResultConnection connection, HarnessTask task) =>
            task.DeliveryConversationId == connection.ConversationId && task.State == HarnessTaskState.Running &&
            task.WaitingFor != HarnessWaitingFor.None && !string.IsNullOrEmpty(task.RequestId);
        private static string RequestIdentity(HarnessTask task) => task.Id + ":" + task.RequestId;
        private static string RequestEventId(HarnessTask task)
        {
            using (var sha = SHA256.Create())
                return "request-" + BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(RequestIdentity(task)))).Replace("-", "");
        }

        private async Task PresentRequestAsync(HarnessResultConnection connection, HarnessTask task,
            IHarnessRequestPresenter presenter, CancellationToken token)
        {
            string context = "Task: " + task.Id + "\nRequestId: " + task.RequestId + "\nWaitingFor: " + task.WaitingFor +
                "\nQuestion: " + Bounded(task.Activity ?? "This task needs your input.", 2000);
            if (task.InputForm != null || task.InputPrompt?.Mode == HarnessInputMode.Url)
                context += "\nOpen task details for the complete form or external authorization; do not ask for private credentials in voice.";
            using (var production = CancellationTokenSource.CreateLinkedTokenSource(token))
            {
                if (!IsCurrent(connection)) return;
                var reply = presenter.TryBeginRequestReply(RequestEventId(task), context, production.Token);
                if (reply == null) return;
                try
                {
                    var until = DateTime.UtcNow.AddMinutes(2);
                    var refresh = DateTime.UtcNow;
                    while (IsCurrent(connection) && DateTime.UtcNow < until)
                    {
                        token.ThrowIfCancellationRequested();
                        if (DateTime.UtcNow >= refresh)
                        {
                            var current = await RpcAsync(ct => connection.Client.GetAsync(task.Id, ct), token);
                            if (!IsPendingRequest(connection, current) || current.RequestId != task.RequestId) return;
                            refresh = DateTime.UtcNow.AddSeconds(1);
                        }
                        bool presented = _presenter.UsesSpeech ? reply.PlaybackStarted && reply.PlaybackCompleted : reply.TextDisplayed;
                        if (presented) { _presentedRequests.Add(RequestIdentity(task)); return; }
                        if (IsInterrupted(reply)) return;
                        await Task.Delay(100, token);
                    }
                }
                finally
                {
                    production.Cancel();
                    if (!reply.PlaybackCompleted && !reply.TextDisplayed) await _presenter.InterruptAsync(reply);
                    await reply.Completion;
                }
            }
        }
    }
}
