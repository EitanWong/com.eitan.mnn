using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using MNN.Unity.Editor.UI;

namespace MNN.Unity.Editor
{
    public sealed partial class MNNChatStudio
    {
        private void HandleDrop(Rect rect)
        {
            Event evt = Event.current;
            if (evt.type == EventType.DragExited)
            {
                if (_mediaDragActive)
                {
                    _mediaDragActive = false;
                    _mediaDropAccepted = false;
                    _mediaDropHint = null;
                    Repaint();
                }

                return;
            }

            if (evt.type != EventType.DragUpdated && evt.type != EventType.DragPerform)
                return;
            if (!rect.Contains(evt.mousePosition))
            {
                if (_mediaDragActive)
                {
                    _mediaDragActive = false;
                    _mediaDropAccepted = false;
                    _mediaDropHint = null;
                    Repaint();
                }

                return;
            }

            string path = DroppedPath();
            bool accepted = CanAttachDrop(path, Selected, Busy, _audio != null && _audio.IsRecording, out bool image, out string mediaHint);
            _mediaDragActive = true;
            _mediaDropAccepted = accepted;
            _mediaDropHint = mediaHint;
            DragAndDrop.visualMode = accepted ? DragAndDropVisualMode.Copy : DragAndDropVisualMode.Rejected;
            if (evt.type == EventType.DragPerform)
            {
                if (accepted)
                {
                    DragAndDrop.AcceptDrag();
                    Try(() => Attach(path, image));
                }

                _mediaDragActive = false;
                _mediaDropAccepted = false;
                _mediaDropHint = null;
            }

            evt.Use();
            Repaint();
        }

        internal static bool CanAttachDrop(string path, MNNStudioModel model, bool busy, bool recording, out bool image, out string hint)
        {
            image = false;
            string extension = Path.GetExtension(path ?? string.Empty).ToLowerInvariant();
            image = extension == ".png" || extension == ".jpg" || extension == ".jpeg" || extension == ".bmp";
            bool audio = extension == ".wav";
            bool format = image || audio;
            bool capability = model != null && (image ? model.Supports(MNNModelCapabilities.Vision) : audio && model.Supports(MNNModelCapabilities.AudioInput));
            bool exists = !string.IsNullOrWhiteSpace(path) && File.Exists(path);
            bool accepted = !busy && !recording && format && capability && exists;
            hint = !format ? "Drop a PNG, JPEG, BMP, or WAV file" : !capability ? "Selected model cannot accept this file" : !exists ? "The dropped file is unavailable" : image ? "Drop image to attach" : "Drop WAV audio to attach";
            return accepted;
        }

        private string DroppedPath()
        {
            string path = DragAndDrop.paths.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
            if (string.IsNullOrWhiteSpace(path))
            {
                UnityEngine.Object asset = DragAndDrop.objectReferences.FirstOrDefault(value => value != null);
                if (asset != null)
                    path = AssetDatabase.GetAssetPath(asset);
            }

            if (string.IsNullOrWhiteSpace(path))
                return null;
            if (path.StartsWith("file://", StringComparison.OrdinalIgnoreCase))
            {
                Uri uri;
                if (!Uri.TryCreate(path, UriKind.Absolute, out uri))
                    return null;
                path = uri.LocalPath;
            }

            if (!Path.IsPathRooted(path))
                path = Path.GetFullPath(path);
            return path;
        }

        private void DrawMediaDropFeedback(Rect rect)
        {
            if (!_mediaDragActive || string.IsNullOrEmpty(_mediaDropHint))
                return;
            Color color = _mediaDropAccepted ? MNNStudioUI.Accent : new Color(.84f, .34f, .32f);
            EditorGUI.DrawRect(rect, new Color(color.r, color.g, color.b, .10f));
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width, 2), color);
            EditorGUI.DrawRect(new Rect(rect.x, rect.yMax - 2, rect.width, 2), color);
        }
    }
}
