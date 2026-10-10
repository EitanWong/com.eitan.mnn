using System;
using System.Collections.Generic;

namespace MNN.Unity.Editor
{
    internal sealed class MNNStudioSearchHit
    {
        internal string ChatId, Title, Preview;
        internal int MessageIndex = -1;
        internal bool IsUser;
    }

    internal static class MNNStudioSearch
    {
        internal static List<MNNStudioSearchHit> Find(MNNStudioHistory history, string query, int limit = 100)
        {
            var hits = new List<MNNStudioSearchHit>();
            query = (query ?? "").Trim();
            foreach (var chat in history.Conversations)
            {
                if (query.Length == 0 || (chat.Title ?? "").IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0)
                    hits.Add(new MNNStudioSearchHit{ChatId = chat.Id, Title = chat.Title, Preview = "Conversation"});
                if (hits.Count >= limit)
                    break;
                if (query.Length == 0)
                    continue;
                for (int i = 0; i < chat.Messages.Count; ++i)
                {
                    string text = chat.Messages[i].Text ?? "";
                    int match = text.IndexOf(query, StringComparison.OrdinalIgnoreCase);
                    if (match < 0)
                        continue;
                    int start = Math.Max(0, match - 40), length = Math.Min(150, text.Length - start);
                    string snippet = text.Substring(start, length).Replace('\r', ' ').Replace('\n', ' ').Replace('\t', ' ');
                    hits.Add(new MNNStudioSearchHit{ChatId = chat.Id, Title = chat.Title, MessageIndex = i, IsUser = chat.Messages[i].IsUser, Preview = (start > 0 ? "…" : "") + snippet + (start + length < text.Length ? "…" : "")});
                    if (hits.Count >= limit)
                        break;
                }

                if (hits.Count >= limit)
                    break;
            }

            return hits;
        }
    }
}
