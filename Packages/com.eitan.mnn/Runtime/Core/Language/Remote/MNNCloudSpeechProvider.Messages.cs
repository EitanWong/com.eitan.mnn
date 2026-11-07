using System;
namespace MNN.Unity
{
    public sealed partial class MNNCloudSpeechProvider
    {
        [Serializable] private sealed class RecognitionRequest { public string model; public AudioMessage[] messages; public bool stream; }
        [Serializable] private sealed class AudioMessage { public string role = "user"; public AudioContent[] content; }
        [Serializable] private sealed class AudioContent { public string type = "input_audio"; public AudioInput input_audio; }
        [Serializable] private sealed class AudioInput { public string data; }
        [Serializable] private sealed class ChatResult { public Choice[] choices; }
        [Serializable] private sealed class Choice { public Message message; }
        [Serializable] private sealed class Message { public string content; }
        [Serializable] private sealed class VolcRecognitionRequest { public User user = new User(); public VolcAudio audio; public VolcRecognitionOptions request; }
        [Serializable] private sealed class User { public string uid = "siriai"; }
        [Serializable] private sealed class VolcAudio { public string data; }
        [Serializable] private sealed class VolcRecognitionOptions { public string model_name; }
        [Serializable] private sealed class VolcRecognitionResult { public Transcript result; }
        [Serializable] private sealed class Transcript { public string text; }
        [Serializable] private sealed class CompatibleSpeech { public string model, voice, input; public string response_format = "pcm"; public int sample_rate = 24000; public bool stream; }
        [Serializable] private sealed class AudioOptions { public int sample_rate = 24000; public string format = "pcm"; public int channel = 1; }
        [Serializable] private sealed class MiniSpeech { public string model, text; public bool stream; public MiniVoice voice_setting; public AudioOptions audio_setting = new AudioOptions(); }
        [Serializable] private sealed class MiniVoice { public string voice_id; public float speed = 1, vol = 1; public int pitch; }
        [Serializable] private sealed class MiniResult { public MiniStatus base_resp; public MiniData data; }
        [Serializable] private sealed class MiniStatus { public int status_code = -1; }
        [Serializable] private sealed class MiniData { public string audio; }
        [Serializable] private sealed class VolcSpeech { public VolcSpeechOptions req_params; }
        [Serializable] private sealed class VolcSpeechOptions { public string text, speaker; public VolcAudioOptions audio_params = new VolcAudioOptions(); }
        [Serializable] private sealed class VolcAudioOptions { public string format = "pcm"; public int sample_rate = 24000; }
        [Serializable] private sealed class QwenSpeech { public string model; public QwenSpeechInput input; }
        [Serializable] private sealed class QwenSpeechInput { public string text, voice, language_type; }
        [Serializable] private sealed class QwenResult { public QwenOutput output; }
        [Serializable] private sealed class QwenOutput { public QwenAudio audio; }
        [Serializable] private sealed class QwenAudio { public string url, data; }
    }
}
