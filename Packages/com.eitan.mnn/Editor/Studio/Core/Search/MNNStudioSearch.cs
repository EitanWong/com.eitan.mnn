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
            var operation = new Operation(history, query, limit);
            while (!operation.Step(256)) { }
            return operation.Hits;
        }

        // Each step examines bounded text chunks on the editor thread. The popup can
        // abandon an old operation immediately, without copying history or sharing it
        // with a worker. Message indices and ordinal case-insensitive matching stay exact.
        internal sealed class Operation
        {
            private const int ChunkLength = 16384;
            private readonly MNNStudioHistory _history;
            private readonly string _query;
            private readonly int _limit;
            private int _chat, _message = -1, _offset;
            internal readonly List<MNNStudioSearchHit> Hits = new List<MNNStudioSearchHit>();
            internal bool Complete => Hits.Count >= _limit || _chat >= _history.Conversations.Count;

            internal Operation(MNNStudioHistory history, string query, int limit = 100)
            {
                _history = history ?? throw new ArgumentNullException(nameof(history));
                _query = (query ?? "").Trim();
                _limit = Math.Max(0, limit);
            }

            internal bool Step(int workLimit)
            {
                if (workLimit <= 0) throw new ArgumentOutOfRangeException(nameof(workLimit));
                while (!Complete && workLimit-- > 0)
                    ExamineNext();
                return Complete;
            }

            private void ExamineNext()
            {
                var chat = _history.Conversations[_chat];
                if (_message < 0)
                {
                    if (_query.Length == 0 || (chat.Title ?? "").IndexOf(_query, StringComparison.OrdinalIgnoreCase) >= 0)
                        Hits.Add(new MNNStudioSearchHit { ChatId = chat.Id, Title = chat.Title, Preview = "Conversation" });
                    _message = 0;
                    if (_query.Length == 0) NextChat();
                    return;
                }
                if (_message >= chat.Messages.Count)
                {
                    NextChat();
                    return;
                }
                string text = chat.Messages[_message].Text ?? "";
                if (_query.Length > text.Length)
                {
                    NextMessage();
                    return;
                }
                int length = Math.Min(text.Length - _offset, Math.Max(ChunkLength, _query.Length));
                int match = text.IndexOf(_query, _offset, length, StringComparison.OrdinalIgnoreCase);
                if (match >= 0)
                {
                    AddMessageHit(chat, text, match);
                    NextMessage();
                }
                else if (_offset + length >= text.Length)
                    NextMessage();
                else
                    _offset += length - _query.Length + 1; // overlap catches matches across chunk boundaries
            }

            private void AddMessageHit(MNNStudioConversation chat, string text, int match)
            {
                int start = Math.Max(0, match - 40), length = Math.Min(150, text.Length - start);
                string snippet = text.Substring(start, length).Replace('\r', ' ').Replace('\n', ' ').Replace('\t', ' ');
                Hits.Add(new MNNStudioSearchHit { ChatId = chat.Id, Title = chat.Title, MessageIndex = _message,
                    IsUser = chat.Messages[_message].IsUser,
                    Preview = (start > 0 ? "…" : "") + snippet + (start + length < text.Length ? "…" : "") });
            }

            private void NextMessage() { ++_message; _offset = 0; }
            private void NextChat() { ++_chat; _message = -1; _offset = 0; }
        }
    }
}
