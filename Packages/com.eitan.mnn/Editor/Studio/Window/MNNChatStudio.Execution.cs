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
        private void Send()
        {
            if (!CanSend)
                return;
            Try(() =>
            {
                string text = _draft.Trim();
                if (text.Length == 0)
                    text = _audioPath != null ? "Transcribe and respond to this audio." : _imagePath != null ? "Describe this image." : "";
                if (text.Length > MNNStudioPrompt.MaximumCharacters)
                    throw new ArgumentException("The message is too long (16,000 characters maximum).");
                EnsureSession();
                _discarded = false;
                _streamText = "";
                _streamMarkdown = null;
                _followOutput = true;
                _session.Send(new MNNStudioRequest{VisionImage = Selected.IsMiniMindO && !string.IsNullOrEmpty(_imagePath) ? MNNStudioImage.ReadRgb(_imagePath) : null, Prompt = text, History = _messages.ToArray(), ContextSummary = _contextSummary, SummarizedMessages = _summarizedMessages, Image = _imagePath, Audio = _audioPath, TokenLimit = _tokenLimit, ContextTokens = _contextTokens, Speech = _speech && Selected.Supports(MNNModelCapabilities.SpeechOutput)});
                _contextCompressionPending = false;
                _messages.Add(new MNNStudioMessage(true, text, _imagePath, _audioPath));
                foreach (var cached in _markdown.Keys.Where(message => !_messages.Contains(message)).ToArray())
                    _markdown.Remove(cached);
                foreach (string cached in _previews.Keys.Where(path => !_messages.Any(message => message.ImagePath == path)).ToArray())
                {
                    DestroyImmediate(_previews[cached]);
                    _previews.Remove(cached);
                }

                _draft = "";
                _imagePath = _audioPath = null;
                _error = null;
                _scroll.y = float.MaxValue;
                _focusComposer = true;
                RememberConversation();
            });
        }

        private void OnCompleted()
        {
            if (_namingChatId != null)
            {
                var chat = _history.Conversations.Find(item => item.Id == _namingChatId);
                _namingChatId = null;
                if (chat != null)
                {
                    string title = _session.Result != null && !_session.Result.Cancelled ? MNNStudioNaming.CleanTitle(_session.Result.Text) : "";
                    if (title.Length > 0)
                    {
                        chat.Title = title;
                        chat.Named = true;
                    }
                    else
                        chat.Named = true; // Keep fallback; a manual model-title action can retry.
                }

                _streamText = "";
                _streamMarkdown = null;
                _shownProgress = _session.Progress;
                SaveLibrary();
                return;
            }

            _contextCompressionPending = false;
            if (_session.MemorySummary != null)
            {
                _contextSummary = _session.MemorySummary;
                _summarizedMessages = _session.MemoryCovered;
            }

            if (_session.Error != null)
            {
                _error = _session.Error;
                StopVoiceLoop();
            }
            else if (_session.Result != null)
            {
                var result = _session.Result;
                _contextCompressionPending = false;
                if (!string.IsNullOrEmpty(result.ContextSummary))
                    _contextSummary = MNNStudioPrompt.BoundSummary(result.ContextSummary);
                if (_taskIndex == 0)
                {
                    if (!string.IsNullOrEmpty(result.Text))
                        _messages.Add(new MNNStudioMessage(false, result.Text, result: result));
                    if (_followOutput)
                        _scroll.y = float.MaxValue;
                    if ((!_voiceLoop || !_voiceMuted) && result.Waveform.Length > 0 && !result.Cancelled)
                    {
                        Try(() => PlayGeneratedSpeech(result));
                        if (_voiceLoop)
                            _nextListen = EditorApplication.timeSinceStartup + (double)result.Waveform.Length / result.SampleRate + .5;
                    }
                    else if (_voiceLoop && !_voiceMuted)
                        _nextListen = EditorApplication.timeSinceStartup + .25;
                }
                else
                {
                    _retrieval = result;
                    _taskResultMessage = new MNNStudioMessage(false, result.Text, result: result);
                    if (result.Waveform.Length > 0 && !result.Cancelled)
                        Try(() => PlayGeneratedSpeech(result));
                }
            }

            RememberConversation();
            _sessionLastUsed = EditorApplication.timeSinceStartup;
            _streamText = "";
            _streamMarkdown = null;
            Repaint();
        }

        private void Try(Action action)
        {
            try
            {
                action();
            }
            catch (ExitGUIException)
            {
                throw;
            }
            catch (Exception error)
            {
                _error = error.GetBaseException().Message;
                StopVoiceLoop();
            }
        }
    }
}
