using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;

namespace MNN.Unity.Editor
{
    /// <summary>Bounded, project-local conversation history. Stores managed text and metadata;
    /// native sessions, generated waveforms and image bytes never enter the history file.</summary>
    [Serializable]
    internal sealed class MNNStudioHistory
    {
        public List<MNNStudioConversation> Conversations = new List<MNNStudioConversation>();
        public List<MNNStudioFolder> Folders = new List<MNNStudioFolder>();
        internal void Remember(MNNStudioConversation conversation)
        {
            int index = Conversations.FindIndex(item => item.Id == conversation.Id);
            if (index >= 0)
            {
                var previous = Conversations[index];
                conversation.FolderId = previous.FolderId;
                conversation.Named = previous.Named;
                if (previous.Named)
                    conversation.Title = previous.Title;
                Conversations[index] = conversation;
            }
            else
                Conversations.Insert(0, conversation);
        }

        internal MNNStudioFolder CreateFolder(string name)
        {
            name = CleanName(name);
            if (name.Length == 0)
                throw new ArgumentException("Enter a folder name.");
            var folder = new MNNStudioFolder{Id = Guid.NewGuid().ToString("N"), Name = name};
            Folders.Add(folder);
            return folder;
        }

        internal void Rename(string id, string name, bool folder = false)
        {
            name = CleanName(name);
            if (name.Length == 0)
                throw new ArgumentException("Enter a name.");
            if (folder)
            {
                var target = Folders.Find(item => item.Id == id);
                if (target != null)
                    target.Name = name;
            }
            else
            {
                var target = Conversations.Find(item => item.Id == id);
                if (target != null)
                {
                    target.Title = name;
                    target.Named = true;
                }
            }
        }

        internal void MoveChat(string id, string folder, string before = null)
        {
            var chat = Conversations.Find(item => item.Id == id);
            if (chat == null || before == id)
                return;
            if (!string.IsNullOrEmpty(folder) && !Folders.Exists(item => item.Id == folder))
                throw new ArgumentException("Folder not found.");
            chat.FolderId = folder;
            Conversations.Remove(chat);
            int index = Conversations.FindIndex(item => item.Id == before && item.FolderId == folder);
            if (index < 0)
                Conversations.Add(chat);
            else
                Conversations.Insert(index, chat);
        }

        internal void MoveFolder(string id, string before)
        {
            var folder = Folders.Find(item => item.Id == id);
            if (folder == null || before == id)
                return;
            Folders.Remove(folder);
            int index = Folders.FindIndex(item => item.Id == before);
            if (index < 0)
                Folders.Add(folder);
            else
                Folders.Insert(index, folder);
        }

        internal void Delete(string id, bool folder = false)
        {
            if (folder)
            {
                Folders.RemoveAll(item => item.Id == id);
                foreach (var chat in Conversations.Where(item => item.FolderId == id))
                    chat.FolderId = null;
            }
            else
                Conversations.RemoveAll(item => item.Id == id);
        }

        internal static string CleanName(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return "";
            string name = string.Join(" ", text.Split(new[]{'\r', '\n', '\t'}, StringSplitOptions.RemoveEmptyEntries)).Trim(' ', '"', '\'', '“', '”', '#', '`');
            if (name.Length > 60)
                name = name.Substring(0, 60).TrimEnd();
            return name;
        }

        internal static MNNStudioHistory Load(string path)
        {
            if (!File.Exists(path))
                return new MNNStudioHistory();
            // Do not parse an unexpectedly large or damaged local file on the editor thread.
            if (new FileInfo(path).Length > 32 * 1024 * 1024)
                throw new IOException("The Studio history file is too large.");
            var history = JsonUtility.FromJson<MNNStudioHistory>(File.ReadAllText(path));
            if (history?.Conversations == null)
                throw new IOException("The Studio history file is invalid.");
            history.Conversations = history.Conversations.Where(item => item != null && !string.IsNullOrEmpty(item.Id)).GroupBy(item => item.Id).Select(group => group.First()).ToList();
            history.Folders = (history.Folders ?? new List<MNNStudioFolder>()).Where(item => item != null && !string.IsNullOrEmpty(item.Id)).GroupBy(item => item.Id).Select(group => group.First()).ToList();
            foreach (var conversation in history.Conversations)
            {
                conversation.Messages = (conversation.Messages ?? new List<MNNStudioHistoryMessage>()).Where(item => item != null).ToList();
                conversation.Draft = conversation.Draft ?? "";
                conversation.ContextSummary = MNNStudioPrompt.BoundSummary(conversation.ContextSummary);
                conversation.SummarizedMessages = string.IsNullOrEmpty(conversation.ContextSummary) ? 0 : Math.Max(0, Math.Min(conversation.SummarizedMessages, conversation.Messages.Count));
                if (!history.Folders.Exists(item => item.Id == conversation.FolderId))
                    conversation.FolderId = null;
            }

            return history;
        }

        internal void Save(string path)
        {
            string json = JsonUtility.ToJson(this);
            if (Encoding.UTF8.GetByteCount(json) > 32 * 1024 * 1024)
                throw new IOException("The Studio history file is too large. Export or delete older conversations.");
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)));
            string temporary = path + ".tmp";
            try
            {
                File.WriteAllText(temporary, json);
                if (File.Exists(path))
                    File.Replace(temporary, path, null);
                else
                    File.Move(temporary, path);
            }
            finally
            {
                if (File.Exists(temporary))
                    File.Delete(temporary);
            }
        }
    }
}
