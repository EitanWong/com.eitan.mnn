using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;

namespace MNN.Unity.Editor
{
    [Serializable]
    internal sealed class MNNStudioConversation
    {
        public string Id, Title, ModelDirectory, Draft, ImagePath, AudioPath, FolderId, ContextSummary;
        public bool Named;
        public int SummarizedMessages;
        public List<MNNStudioHistoryMessage> Messages = new List<MNNStudioHistoryMessage>();
        internal static MNNStudioConversation Capture(string id, string model, IReadOnlyList<MNNStudioMessage> messages, string draft, string image, string audio, string contextSummary = null, int summarizedMessages = 0)
        {
            string first = messages.FirstOrDefault(item => item.IsUser)?.Text ?? draft ?? "New chat";
            string title = string.Join(" ", first.Split(new[]{'\r', '\n', '\t'}, StringSplitOptions.RemoveEmptyEntries)).Trim();
            if (title.Length > 48)
                title = title.Substring(0, 47) + "…";
            var conversation = new MNNStudioConversation{Id = string.IsNullOrEmpty(id) ? Guid.NewGuid().ToString("N") : id, Title = title.Length > 0 ? title : "New chat", ModelDirectory = model, Draft = draft ?? "", ImagePath = image, AudioPath = audio, ContextSummary = MNNStudioPrompt.BoundSummary(contextSummary), SummarizedMessages = Math.Max(0, Math.Min(summarizedMessages, messages.Count))};
            foreach (var message in messages)
                conversation.Messages.Add(new MNNStudioHistoryMessage{IsUser = message.IsUser, Text = message.Text, ImagePath = message.ImagePath, AudioPath = message.AudioPath, HasResult = message.Result != null, Tokens = message.Result?.Tokens ?? 0, Seconds = message.Result?.Seconds ?? 0, AudioSeconds = message.Result?.AudioSeconds ?? 0, BackendDescription = message.Result?.BackendDescription, Limited = message.Result?.Limited ?? false, RepetitionStopped = message.Result?.RepetitionStopped ?? false, Cancelled = message.Result?.Cancelled ?? false});
            return conversation;
        }

        internal IEnumerable<MNNStudioMessage> RestoreMessages() => Messages.Select(message =>
        {
            string text = message.IsUser ? message.Text : MNNStudioPrompt.CleanResponse(message.Text);
            return new MNNStudioMessage(message.IsUser, text, message.ImagePath, message.AudioPath, message.HasResult ? new MNNStudioResult{Text = text, Tokens = message.Tokens, Seconds = message.Seconds, AudioSeconds = message.AudioSeconds, BackendDescription = message.BackendDescription, Limited = message.Limited, RepetitionStopped = message.RepetitionStopped, Cancelled = message.Cancelled} : null);
        });
    }
}
