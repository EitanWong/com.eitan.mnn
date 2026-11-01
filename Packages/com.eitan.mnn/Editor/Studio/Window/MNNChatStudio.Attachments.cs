using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using MNN.Unity.Editor.UI;

namespace MNN.Unity.Editor
{
    public sealed partial class MNNChatStudio
    {
        private void PickAttachment(bool image)
        {
            string modelDirectory = Selected?.Directory;
            if (modelDirectory == null)
                return;
            var task = CurrentTask.Task;
            ShowDialogAfterGUI(() =>
            {
                if (Busy || _audio.IsRecording || Selected?.Directory != modelDirectory || CurrentTask.Task != task)
                    return;
                string path = _attachmentPicker(image ? "Attach image" : "Attach audio", "", image ? "png,jpg,jpeg,bmp" : "wav");
                if (this != null && _audio != null && !Busy && !_audio.IsRecording && Selected?.Directory == modelDirectory && CurrentTask.Task == task && !string.IsNullOrEmpty(path))
                    Attach(path, image);
            });
        }

        private void ShowDialogAfterGUI(Action action)
        {
            if (_dialogPending)
                return;
            _dialogPending = true;
            // Native dialogs can pump a nested GUI event and reset Unity's layout
            // stack. Wait until all scopes from the current OnGUI have closed.
            _dialogSchedule(() =>
            {
                try
                {
                    if (this != null && _audio != null)
                        Try(action);
                }
                finally
                {
                    _dialogPending = false;
                    if (this != null)
                        Repaint();
                }
            });
        }

        private void Attach(string path, bool image)
        {
            if (!File.Exists(path))
                throw new FileNotFoundException("The attachment does not exist.", path);
            bool supported = image ? Selected.Supports(MNNModelCapabilities.Vision) : Selected.Supports(MNNModelCapabilities.AudioInput) || CurrentTask.Task == MNNStudioTask.SpeechSynthesis && Selected.RequiresReferenceAudio;
            if (!supported)
                throw new NotSupportedException(image ? "This model does not support image attachments." : "This model does not support audio attachments.");
            if (path.IndexOfAny(new[]{'<', '>', '\n', '\r'}) >= 0)
                throw new ArgumentException("Attachment paths cannot contain media tag delimiters.");
            if (image)
                _imagePath = Path.GetFullPath(path);
            else
            {
                if (CurrentTask.Task == MNNStudioTask.SpeechSynthesis && Selected.RequiresReferenceAudio)
                    MNNReferenceAudio.FromWaveFile(path);
                _audioPath = Path.GetFullPath(path);
            }
        }

        private Texture2D Preview(string path)
        {
            if (_previews.TryGetValue(path, out var texture))
                return texture;
            if (_failedPreviews.Contains(path))
                return null;
            Texture2D candidate = null;
            try
            {
                // Decode once, and never retry corrupt/oversized previews on every repaint.
                if (new FileInfo(path).Length > 16 * 1024 * 1024)
                {
                    _failedPreviews.Add(path);
                    return null;
                }

                candidate = new Texture2D(2, 2, TextureFormat.RGBA32, false)
                {hideFlags = HideFlags.HideAndDontSave};
                if (!candidate.LoadImage(File.ReadAllBytes(path), true))
                    throw new IOException("Invalid image.");
                if (Math.Max(candidate.width, candidate.height) > 320)
                {
                    float scale = 320f / Math.Max(candidate.width, candidate.height);
                    var temporary = RenderTexture.GetTemporary(Math.Max(1, Mathf.RoundToInt(candidate.width * scale)), Math.Max(1, Mathf.RoundToInt(candidate.height * scale)), 0, RenderTextureFormat.ARGB32);
                    var previous = RenderTexture.active;
                    Texture2D thumbnail = null;
                    try
                    {
                        Graphics.Blit(candidate, temporary);
                        RenderTexture.active = temporary;
                        thumbnail = new Texture2D(temporary.width, temporary.height, TextureFormat.RGBA32, false)
                        {hideFlags = HideFlags.HideAndDontSave};
                        thumbnail.ReadPixels(new Rect(0, 0, temporary.width, temporary.height), 0, 0);
                        thumbnail.Apply(false, true);
                        DestroyImmediate(candidate);
                        candidate = thumbnail;
                        thumbnail = null;
                    }
                    finally
                    {
                        RenderTexture.active = previous;
                        RenderTexture.ReleaseTemporary(temporary);
                        if (thumbnail != null)
                            DestroyImmediate(thumbnail);
                    }
                }

                _previews[path] = candidate;
                return candidate;
            }
            catch
            {
                if (candidate != null)
                    DestroyImmediate(candidate);
                _failedPreviews.Add(path);
                return null;
            }
        }

        private void ClearPreviews()
        {
            foreach (var texture in _previews.Values)
                if (texture != null)
                    DestroyImmediate(texture);
            _previews.Clear();
            _failedPreviews.Clear();
            _markdown.Clear();
        }

        private void CleanupRecordings()
        {
            foreach (string path in _recordings)
                try
                {
                    File.Delete(path);
                }
                catch (IOException)
                {
                }
                catch (UnauthorizedAccessException)
                {
                }

            _recordings.Clear();
        }
    }
}
