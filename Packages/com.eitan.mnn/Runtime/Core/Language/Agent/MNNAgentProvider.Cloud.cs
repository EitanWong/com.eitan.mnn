using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MNN.Unity;

namespace MNN.Unity
{
    public sealed partial class MNNAgentProvider
    {
        private async Task GenerateCloudAsync(IReadOnlyList<MNNOpenAIMessage> history, string instructions, Func<string, Task> publish, CancellationToken token)
        {
            if (!_config.AllowCloud) { await DeniedAsync(publish).ConfigureAwait(false); return; }
            var context = CloudContext(history, instructions);
            if (context == null)
            { SetRoute("Cloud context exceeded"); await publish("This request exceeds the cloud context budget. Please shorten it or split the document.").ConfigureAwait(false); return; }
            if (_createCloud == null)
            { SetRoute("Cloud unavailable"); await publish("A cloud answer provider is not configured. Please configure it before retrying.").ConfigureAwait(false); return; }
            SetRoute("Cloud");
            bool published = false;
            try
            {
                if (_cloud == null) _cloud = _createCloud() ?? throw new InvalidOperationException("Cloud provider is unavailable.");
                if (!_cloudPrepared)
                {
                    await _cloud.PrepareAsync(token).ConfigureAwait(false);
                    _cloudPrepared = true;
                }
                await _cloud.GenerateAsync(context, null, 16000, async value =>
                {
                    token.ThrowIfCancellationRequested();
                    if (!string.IsNullOrEmpty(value)) published = true;
                    await publish(value).ConfigureAwait(false);
                }, token).ConfigureAwait(false);
                token.ThrowIfCancellationRequested();
            }
            catch (Exception) when (!token.IsCancellationRequested && !published)
            { SetRoute("Cloud unavailable"); await publish("The cloud service is unavailable. Please try again; I cannot reliably answer this request locally.").ConfigureAwait(false); }
        }

        private IReadOnlyList<MNNOpenAIMessage> CloudContext(IReadOnlyList<MNNOpenAIMessage> history, string instructions)
        {
            string current = CurrentRequest(history);
            int remaining = _config.CloudContextCharacters - instructions.Length - current.Length;
            if (remaining < 0) return null;
            var context = new List<MNNOpenAIMessage>();
            int count = 0;
            for (int i = history.Count - 2; i >= 0 && count < _config.CloudHistoryMessages; --i)
            {
                var item = history[i];
                if (item.Audio != null || item.Role == MNNChatRole.System) continue;
                if (item.Text.Length > remaining) break;
                remaining -= item.Text.Length;
                context.Add(item); ++count;
            }
            context.Reverse();
            if (!string.IsNullOrEmpty(instructions)) context.Insert(0, new MNNOpenAIMessage(MNNChatRole.System, instructions));
            context.Add(new MNNOpenAIMessage(MNNChatRole.User, current));
            return context;
        }
    }
}
