using System;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using MNN.Unity.Editor.UI;

namespace MNN.Unity.Editor
{
    public sealed partial class MNNChatStudio
    {
        private string _namingChatId, _dragChatId, _dragFolderId;
        private bool _autoTitle = true;
        private readonly Dictionary<string, Rect> _libraryRows = new Dictionary<string, Rect>();
        private readonly Dictionary<string, Rect> _folderRows = new Dictionary<string, Rect>();
        private Vector2 _dragStart;
        private const string DragKey = "MNNStudioConversation";
        // Keep the OS drag loop separate from IMGUI routing; synthetic events cannot
        // complete macOS's modal drag loop. Production uses Unity's native drag service.
        [NonSerialized]
        private Action<string> _startLibraryDrag = DragAndDrop.StartDrag;
        private sealed class LibraryDrag
        {
            internal int Window;
            internal string Id;
            internal bool Folder;
        }

        private void ResetLibraryDragOrigin()
        {
            var evt = Event.current;
            if (evt.type == EventType.MouseDown || evt.rawType == EventType.MouseUp || evt.type == EventType.DragExited)
                _dragChatId = _dragFolderId = null;
            if (evt.type == EventType.DragExited && DragAndDrop.GetGenericData(DragKey)is LibraryDrag payload && payload.Window == GetInstanceID())
                DragAndDrop.SetGenericData(DragKey, null);
        }

        private void SaveLibrary()
        {
            if (!string.IsNullOrEmpty(_historyPath))
                Try(() => _history.Save(_historyPath));
            Repaint();
        }

        private void DrawLibrary()
        {
            if (Event.current.type == EventType.Repaint)
            {
                _libraryRows.Clear();
                _folderRows.Clear();
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Space(22);
                GUILayout.Label("Conversations", MNNStudioUI.Caption);
                GUILayout.FlexibleSpace();
                if (MNNStudioUI.Button("＋", 28, quiet: true, tooltip: "Create folder"))
                    EditLibraryName(null, true, "New folder", GUILayoutUtility.GetLastRect());
                GUILayout.Space(10);
            }

            foreach (var folder in _history.Folders.ToArray())
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.Space(10);
                    if (MNNStudioUI.Navigation((folder.Collapsed ? "▸  " : "▾  ") + folder.Name, false, SidebarWidth - 54, "Drag chats here · Right-click for folder actions", rect => LibraryRow(rect, folder.Id, true, folder.Id)))
                    {
                        folder.Collapsed = !folder.Collapsed;
                        SaveLibrary();
                    }

                    if (MNNStudioUI.Button("•••", 28, quiet: true, tooltip: "Folder actions"))
                        LibraryMenu(folder.Id, true, GUILayoutUtility.GetLastRect());
                }

                if (!folder.Collapsed)
                    DrawLibraryChats(folder.Id);
            }

            GUILayout.Space(8);
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Space(22);
                GUILayout.Label("Chats", MNNStudioUI.Caption, GUILayout.Height(24));
            }

            LibraryDrop(GUILayoutUtility.GetLastRect(), null, false, null);
            DrawLibraryChats(null);
            if (_history.Conversations.Count == 0)
                SidebarCaption("Your conversations appear here.");
        }

        private void DrawLibraryChats(string folder)
        {
            foreach (var chat in _history.Conversations.Where(item => string.IsNullOrEmpty(item.FolderId) == string.IsNullOrEmpty(folder) && (string.IsNullOrEmpty(folder) || item.FolderId == folder)).ToArray())
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.Space(string.IsNullOrEmpty(folder) ? 10 : 20);
                    using (new EditorGUI.DisabledScope(Busy))
                        if (MNNStudioUI.Navigation(chat.Title, _taskIndex == 0 && chat.Id == _activeChatId, SidebarWidth - (string.IsNullOrEmpty(folder) ? 54 : 64), chat.Title, rect => LibraryRow(rect, chat.Id, false, folder)))
                            OpenConversation(chat.Id);
                    using (new EditorGUI.DisabledScope(Busy))
                        if (MNNStudioUI.Button("•••", 28, quiet: true, tooltip: "Conversation actions"))
                            LibraryMenu(chat.Id, false, GUILayoutUtility.GetLastRect());
                }
            }

            var end = GUILayoutUtility.GetRect(SidebarWidth - 20, 12, GUILayout.ExpandWidth(false));
            LibraryDrop(end, null, false, folder);
        }

        private void LibraryRow(Rect rect, string id, bool folder, string destination)
        {
            var evt = Event.current;
            if (evt.type == EventType.Repaint)
            {
                var windowRect = new Rect(GUIUtility.GUIToScreenPoint(rect.position) - position.position, rect.size);
                if (folder)
                    _folderRows[id] = windowRect;
                else
                    _libraryRows[id] = windowRect;
            }

            if (evt.type == EventType.ContextClick && rect.Contains(evt.mousePosition))
            {
                LibraryMenu(id, folder, rect);
                evt.Use();
            }

            // Runs before GUI.Button so it cannot consume drag events before the library.
            if (evt.rawType == EventType.MouseDown && evt.button == 0 && rect.Contains(evt.mousePosition))
            {
                _dragStart = evt.mousePosition;
                _dragChatId = folder ? null : id;
                _dragFolderId = folder ? id : null;
            }

            if (!Busy && evt.type == EventType.MouseDrag && ((_dragChatId == id && !folder) || (_dragFolderId == id && folder)) && Vector2.Distance(_dragStart, evt.mousePosition) > 5)
            {
                GUIUtility.hotControl = 0;
                DragAndDrop.PrepareStartDrag();
                DragAndDrop.SetGenericData(DragKey, new LibraryDrag{Window = GetInstanceID(), Id = id, Folder = folder});
                _startLibraryDrag(folder ? "Move folder" : "Move conversation");
                _dragChatId = _dragFolderId = null;
                evt.Use();
            }

            LibraryDrop(rect, id, folder, destination);
        }

        private void LibraryDrop(Rect rect, string target, bool folder, string destination)
        {
            var evt = Event.current;
            if (Busy || !rect.Contains(evt.mousePosition))
                return;
            var payload = DragAndDrop.GetGenericData(DragKey) as LibraryDrag;
            if (payload == null || payload.Window != GetInstanceID() || payload.Id == target || (payload.Folder && !folder))
                return;
            bool after = target != null && evt.mousePosition.y >= rect.center.y && (payload.Folder || !folder);
            if (evt.type == EventType.Repaint)
            {
                EditorGUI.DrawRect(new Rect(rect.x, after || (folder && !payload.Folder) ? rect.yMax - 2 : rect.y, rect.width, 2), MNNStudioUI.Accent);
                return;
            }

            if (evt.type != EventType.DragUpdated && evt.type != EventType.DragPerform)
                return;
            DragAndDrop.visualMode = DragAndDropVisualMode.Move;
            if (evt.type == EventType.DragPerform)
            {
                DragAndDrop.AcceptDrag();
                if (after)
                {
                    if (payload.Folder)
                        target = _history.Folders.SkipWhile(item => item.Id != target).Skip(1).FirstOrDefault()?.Id;
                    else
                        target = _history.Conversations.Where(item => string.IsNullOrEmpty(item.FolderId) == string.IsNullOrEmpty(destination) && (string.IsNullOrEmpty(destination) || item.FolderId == destination)).SkipWhile(item => item.Id != target).Skip(1).FirstOrDefault()?.Id;
                }

                if (payload.Folder)
                    _history.MoveFolder(payload.Id, target);
                else
                    _history.MoveChat(payload.Id, destination, folder ? null : target);
                DragAndDrop.SetGenericData(DragKey, null);
                SaveLibrary();
            }

            evt.Use();
        }

        private void LibraryMenu(string id, bool folder, Rect anchor)
        {
            var menu = new GenericMenu();
            menu.AddItem(new GUIContent("Rename…"), false, () => EditLibraryName(id, folder, folder ? _history.Folders.Find(item => item.Id == id)?.Name : _history.Conversations.Find(item => item.Id == id)?.Title, anchor));
            if (!folder)
            {
                menu.AddItem(new GUIContent("Move to/Chats"), false, () =>
                {
                    _history.MoveChat(id, null);
                    SaveLibrary();
                });
                foreach (var item in _history.Folders)
                {
                    var target = item;
                    menu.AddItem(new GUIContent("Move to/" + item.Name.Replace("/", "∕")), false, () =>
                    {
                        _history.MoveChat(id, target.Id);
                        SaveLibrary();
                    });
                }

                menu.AddItem(new GUIContent("Generate title with model"), false, () => QueueTitle(id, true));
            }

            menu.AddSeparator("");
            menu.AddItem(new GUIContent(folder ? "Delete folder (keep chats)" : "Delete conversation"), false, () =>
            {
                if (Busy && !folder)
                    return;
                if (!folder && id == _activeChatId)
                {
                    ResetWorkspace();
                    _activeChatId = null;
                }

                _history.Delete(id, folder);
                SaveLibrary();
            });
            menu.DropDown(anchor);
        }

        private void EditLibraryName(string id, bool folder, string initial, Rect anchor)
        {
            PopupWindow.Show(anchor, new MNNStudioNamePopover(initial, name =>
            {
                if (id == null)
                    _history.CreateFolder(name);
                else
                    _history.Rename(id, name, folder);
                SaveLibrary();
            }));
        }

        private void QueueTitle(string id, bool force = false)
        {
            var chat = _history.Conversations.Find(item => item.Id == id);
            if (chat == null || (chat.Named && !force) || !chat.Messages.Exists(item => !item.IsUser))
                return;
            if (Busy)
                return;
            if (id != _activeChatId)
                OpenConversation(id);
            if (Busy || Selected == null || _voiceLoop || _audio.IsRecording)
                return;
            Try(() =>
            {
                chat.Named = true; // A failed load keeps the fallback title until a manual retry.
                EnsureSession();
                _session.Send(new MNNStudioRequest{Conversation = MNNStudioNaming.Prompt(chat), TokenLimit = 32});
                _namingChatId = id;
            });
        }
    }
}
