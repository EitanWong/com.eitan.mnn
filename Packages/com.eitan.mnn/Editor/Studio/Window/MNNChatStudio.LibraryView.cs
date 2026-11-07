using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using MNN.Unity.Editor.UI;

namespace MNN.Unity.Editor
{
    public sealed partial class MNNChatStudio
    {
        private enum LibraryRowKind { Heading, Folder, Chat, Drop, ChatsHeading, Empty }
        private struct LibraryViewRow
        {
            internal LibraryRowKind Kind;
            internal MNNStudioFolder Folder;
            internal MNNStudioConversation Chat;
            internal string Destination;
            internal float Y, Height;
        }

        private readonly List<LibraryViewRow> _libraryView = new List<LibraryViewRow>();
        private readonly Dictionary<string, List<MNNStudioConversation>> _libraryGroups = new Dictionary<string, List<MNNStudioConversation>>();
        private readonly List<string> _emptyLibraryGroups = new List<string>();
        private Rect _sidebarViewRect;
        private float _libraryHeight;

        private void PrepareLibraryView()
        {
            if (Event.current.type != EventType.Layout)
                return;
            GroupLibraryChats();
            _libraryView.Clear();
            _libraryHeight = 0;
            AddLibraryRow(new LibraryViewRow { Kind = LibraryRowKind.Heading, Height = 28 });
            foreach (var folder in _history.Folders)
            {
                AddLibraryRow(new LibraryViewRow { Kind = LibraryRowKind.Folder, Folder = folder, Height = 34 });
                if (!folder.Collapsed)
                    AddLibraryChats(folder.Id);
            }
            AddLibraryRow(new LibraryViewRow { Kind = LibraryRowKind.ChatsHeading, Height = 32 });
            AddLibraryChats(null);
            if (_history.Conversations.Count == 0)
                AddLibraryRow(new LibraryViewRow { Kind = LibraryRowKind.Empty, Height = 36 });
        }

        private void GroupLibraryChats()
        {
            // Group once per layout, using reusable storage. Never scan the entire history
            // once for each folder or allocate a filtered array for each GUI event.
            foreach (var group in _libraryGroups.Values)
                group.Clear();
            foreach (var chat in _history.Conversations)
            {
                string key = chat.FolderId ?? "";
                if (!_libraryGroups.TryGetValue(key, out var group))
                    _libraryGroups.Add(key, group = new List<MNNStudioConversation>());
                group.Add(chat);
            }
            _emptyLibraryGroups.Clear();
            foreach (var pair in _libraryGroups)
                if (pair.Value.Count == 0)
                    _emptyLibraryGroups.Add(pair.Key);
            foreach (string key in _emptyLibraryGroups)
                _libraryGroups.Remove(key);
        }

        private void AddLibraryRow(LibraryViewRow row)
        {
            row.Y = _libraryHeight;
            _libraryView.Add(row);
            _libraryHeight += row.Height;
        }

        private void AddLibraryChats(string folder)
        {
            if (_libraryGroups.TryGetValue(folder ?? "", out var chats))
                foreach (var chat in chats)
                    AddLibraryRow(new LibraryViewRow { Kind = LibraryRowKind.Chat, Chat = chat, Destination = folder, Height = 34 });
            AddLibraryRow(new LibraryViewRow { Kind = LibraryRowKind.Drop, Destination = folder, Height = 12 });
        }

        private void DrawLibrary()
        {
            PrepareLibraryView();
            if (Event.current.type == EventType.Repaint)
            {
                _libraryRows.Clear();
                _folderRows.Clear();
            }
            var area = GUILayoutUtility.GetRect(SidebarWidth, _libraryHeight, GUILayout.ExpandWidth(false));
            float top = _sidebarScroll.y;
            float bottom = top + (_sidebarViewRect.height > 0 ? _sidebarViewRect.height : position.height);
            foreach (var row in _libraryView)
            {
                var rect = new Rect(area.x, area.y + row.Y, SidebarWidth, row.Height);
                if (rect.yMax < top)
                    continue;
                if (rect.y > bottom)
                    break;
                DrawLibraryRow(row, rect);
            }
        }

        private void DrawLibraryRow(LibraryViewRow row, Rect rect)
        {
            switch (row.Kind)
            {
                case LibraryRowKind.Heading:
                    GUI.Label(new Rect(rect.x + 22, rect.y, SidebarWidth - 66, 28), "Conversations", MNNStudioUI.Caption);
                    var add = new Rect(rect.xMax - 38, rect.y, 28, 28);
                    if (MNNStudioUI.ButtonAt(add, "＋", quiet: true, tooltip: "Create folder"))
                        EditLibraryName(null, true, "New folder", add);
                    break;
                case LibraryRowKind.Folder:
                    DrawLibraryFolder(row.Folder, rect);
                    break;
                case LibraryRowKind.Chat:
                    DrawLibraryChat(row.Chat, row.Destination, rect);
                    break;
                case LibraryRowKind.Drop:
                    LibraryDrop(rect, null, false, row.Destination);
                    break;
                case LibraryRowKind.ChatsHeading:
                    GUI.Label(new Rect(rect.x + 22, rect.y + 8, SidebarWidth - 38, 24), "Chats", MNNStudioUI.Caption);
                    LibraryDrop(rect, null, false, null);
                    break;
                case LibraryRowKind.Empty:
                    GUI.Label(new Rect(rect.x + 22, rect.y, SidebarWidth - 38, rect.height), "Your conversations appear here.", MNNStudioUI.Caption);
                    break;
            }
        }

        private void DrawLibraryFolder(MNNStudioFolder folder, Rect rect)
        {
            var navigation = new Rect(rect.x + 10, rect.y, SidebarWidth - 54, 34);
            LibraryRow(navigation, folder.Id, true, folder.Id);
            if (MNNStudioUI.NavigationAt(navigation, (folder.Collapsed ? "▸  " : "▾  ") + folder.Name,
                false, "Drag chats here · Right-click for folder actions"))
            {
                folder.Collapsed = !folder.Collapsed;
                SaveLibrary();
            }
            var actions = new Rect(rect.xMax - 38, rect.y, 28, 28);
            if (MNNStudioUI.ButtonAt(actions, "•••", quiet: true, tooltip: "Folder actions"))
                LibraryMenu(folder.Id, true, actions);
        }

        private void DrawLibraryChat(MNNStudioConversation chat, string folder, Rect rect)
        {
            bool root = string.IsNullOrEmpty(folder);
            var navigation = new Rect(rect.x + (root ? 10 : 20), rect.y, SidebarWidth - (root ? 54 : 64), 34);
            using (new EditorGUI.DisabledScope(Busy))
            {
                LibraryRow(navigation, chat.Id, false, folder);
                if (MNNStudioUI.NavigationAt(navigation, chat.Title, _taskIndex == 0 && chat.Id == _activeChatId, chat.Title))
                    OpenConversation(chat.Id);
                var actions = new Rect(rect.xMax - 38, rect.y, 28, 28);
                if (MNNStudioUI.ButtonAt(actions, "•••", quiet: true, tooltip: "Conversation actions"))
                    LibraryMenu(chat.Id, false, actions);
            }
        }
    }
}
