using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using MNN.Unity;
using MNN.Unity.Editor;

namespace MNN.Unity.Tests
{
    public class SpeechModelPackagingTests
    {
        private static string ModelsRoot => Path.GetFullPath(Path.Combine("Assets", "StreamingAssets", "MNN", "Models"));

        [TestCase("Silero-v5.1-MNN", "silero_v5_16k_fp32.mnn", "mnn_silero_vad.json")]
        [TestCase("SmartTurn-v3.2-FP32-MNN", "smart_turn_v32_fp32.mnn", "mnn_smart_turn.json")]
        [TestCase("MarbleNet-VAD-MNN", "marblenet_fp32.mnn", "mnn_marblenet_vad.json")]
        [TestCase("DTLN-aec-128-MNN", "dtln_aec_128_1.mnn", "mnn_dtln_aec.json")]
        [TestCase("DTLN-aec-256-MNN", "dtln_aec_256_1.mnn", "mnn_dtln_aec.json")]
        [TestCase("DTLN-aec-512-MNN", "dtln_aec_512_1.mnn", "mnn_dtln_aec.json")]
        [TestCase("DTLN-MNN", "dtln_1_fp32.mnn", "mnn_dtln.json")]
        [TestCase("GTCRN-MNN", "gtcrn_fp32.mnn", "mnn_gtcrn.json")]
        [TestCase("FullSubNet-MNN", "fullsubnet_fp32.mnn", "mnn_fullsubnet.json")]
        [TestCase("ERes2NetV2-MNN", "eres2netv2_fp32.mnn", "mnn_speaker.json")]
        [TestCase("FSMN-VAD-16K-MNN", "fsmn_vad_fp32.mnn", "mnn_fsmn_vad.json")]
        [TestCase("FRCRN-SE-16K-MNN", "frcrn_se_16k_fp32.mnn", "mnn_frcrn_se.json")]
        [TestCase("DeepFilterNet3-MNN", "enc_fp16.mnn", "mnn_deepfilternet3.json")]
        [TestCase("WeSpeaker-ResNet34-LM-MNN", "wespeaker_resnet34_lm_fp32.mnn", "mnn_wespeaker.json")]
        [TestCase("LiveKit-Turn-Detector-MNN", "turn_detector_fp32.mnn", "mnn_livekit_turn_detector.json")]
        [TestCase("MossFormer2-SS-16K-MNN", "mossformer2_ss_16k_fp32.mnn", "mnn_mossformer2_ss.json")]
        [TestCase("AV-MossFormer2-TSE-16K-MNN", "av_mossformer2_tse_16k_3s.mnn", "mnn_av_mossformer2_tse.json")]
        public void ConvertedAudioProfilesContainOnlyDeployableFiles(string directoryName, string graph, string profile)
        {
            string directory = Path.Combine(ModelsRoot, directoryName);
            Assert.That(Directory.Exists(directory), Is.True, directory);
            Assert.That(new FileInfo(Path.Combine(directory, graph)).Length, Is.GreaterThan(16));
            Assert.That(File.Exists(Path.Combine(directory, profile)), Is.True);
            Assert.That(File.Exists(Path.Combine(directory, "README.md")), Is.True);
            Assert.That(File.Exists(Path.Combine(directory, "LICENSE")), Is.True);
            Assert.That(File.Exists(Path.Combine(directory, "manifest.json")), Is.True);
            var forbidden = Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories)
                .Where(path => new[]{".onnx", ".pt", ".pth", ".ckpt", ".nemo", ".safetensors"}.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase))
                .ToArray();
            Assert.IsEmpty(forbidden);
            Assert.That(directoryName, Does.EndWith("-MNN"));
        }

        [Test]
        public void WeSpeakerProfileIsVerifiedAndRunnableByStudio()
        {
            string directory = Path.Combine(ModelsRoot, "WeSpeaker-ResNet34-LM-MNN");
            Assert.True(MNNWeSpeakerModel.IsComplete(directory));
            var model = MNNStudioModel.Read(directory);
            Assert.AreEqual(MNNStudioTask.SpeakerRecognition, model.Task);
            Assert.True(model.HasSpeakerRecognitionPipeline);
        }

        [Test]
        public void StudioDiscoversConvertedProfilesAsAudioProcessingResources()
        {
            var errors = new System.Collections.Generic.List<string>();
            var models = MNNStudioModel.Discover(new[]{ModelsRoot}, errors);
            Assert.That(models.Any(model => model.Name == "FSMN-VAD-16K-MNN" && model.Task == MNNStudioTask.AudioProcessing), Is.True);
            Assert.That(models.Any(model => model.Name == "FRCRN-SE-16K-MNN" && model.Task == MNNStudioTask.AudioProcessing), Is.True);
            Assert.That(models.Any(model => model.Name == "WeSpeaker-ResNet34-LM-MNN" && model.Task == MNNStudioTask.SpeakerRecognition), Is.True);
            Assert.That(models.Any(model => model.Name == "Silero-v5.1-MNN" && model.Task == MNNStudioTask.AudioProcessing), Is.True);
            Assert.That(models.Any(model => model.Name == "SmartTurn-v3.2-FP32-MNN" && model.Task == MNNStudioTask.AudioProcessing), Is.True);
            Assert.That(models.Any(model => model.Name == "MarbleNet-VAD-MNN" && model.Task == MNNStudioTask.AudioProcessing), Is.True);
            Assert.That(models.Any(model => model.Name == "DeepFilterNet3-MNN" && model.Task == MNNStudioTask.AudioProcessing), Is.True);
            Assert.That(models.Any(model => model.Name == "DTLN-MNN" && model.Task == MNNStudioTask.AudioProcessing && model.HasAudioProcessingPipeline), Is.True);
            Assert.That(models.Any(model => model.Name == "GTCRN-MNN" && model.Task == MNNStudioTask.AudioProcessing && model.HasAudioProcessingPipeline), Is.True);
            Assert.That(models.Any(model => model.Name == "FullSubNet-MNN" && model.Task == MNNStudioTask.AudioProcessing && model.HasAudioProcessingPipeline), Is.True);
            Assert.That(models.Any(model => model.Name == "DTLN-aec-256-MNN" && model.Task == MNNStudioTask.EchoCancellation), Is.True);
            Assert.That(models.Any(model => model.Name == "DTLN-aec-128-MNN" && model.Task == MNNStudioTask.EchoCancellation), Is.True);
            Assert.That(models.Any(model => model.Name == "DTLN-aec-512-MNN" && model.Task == MNNStudioTask.EchoCancellation), Is.True);
            Assert.That(models.Any(model => model.Name == "ERes2NetV2-MNN" && model.Task == MNNStudioTask.SpeakerRecognition), Is.True);
            Assert.That(models.Any(model => model.Name == "LiveKit-Turn-Detector-MNN" && model.Task == MNNStudioTask.AudioProcessing), Is.True);
            Assert.That(models.Any(model => model.Name == "MossFormer2-SS-16K-MNN" && model.Task == MNNStudioTask.AudioProcessing), Is.True);
            var av = models.Single(model => model.Name == "AV-MossFormer2-TSE-16K-MNN");
            Assert.AreEqual(MNNStudioTask.AudioProcessing, av.Task);
            Assert.True(av.HasAudioProcessingPipeline, "Studio supports AV-TSE with synchronized face-frame input.");
            Assert.IsEmpty(errors);
        }

        [Test]
        public void NewSpeechProfilesHaveVerifiedContracts()
        {
            Assert.True(MNNLiveKitTurnDetectorModel.IsComplete(Path.Combine(ModelsRoot, "LiveKit-Turn-Detector-MNN")));
            Assert.True(MNNMossFormer2Model.IsComplete(Path.Combine(ModelsRoot, "MossFormer2-SS-16K-MNN")));
            Assert.True(MNNAvMossFormer2TseModel.IsComplete(Path.Combine(ModelsRoot, "AV-MossFormer2-TSE-16K-MNN")));
        }

        [Test]
        public void DeepFilterNetProfileIsRunnableByStudio()
        {
            var model = MNNStudioModel.Read(Path.Combine(ModelsRoot, "DeepFilterNet3-MNN"));
            Assert.True(model.HasAudioProcessingPipeline, "The three DeepFilterNet graphs need their managed audio pipeline.");
            Assert.True(MNNStudioTasks.Get((int)MNNStudioTask.AudioProcessing).CanRun(model));
        }

        [Test]
        public void MarbleNetProfileIsVerifiedAndRunnableByStudio()
        {
            string directory = Path.Combine(ModelsRoot, "MarbleNet-VAD-MNN");
            Assert.True(MNNMarbleNetVadModel.IsComplete(directory));
            var model = MNNStudioModel.Read(directory);
            Assert.AreEqual(MNNStudioTask.AudioProcessing, model.Task);
            Assert.True(model.HasAudioProcessingPipeline);
        }

        [Test]
        public void MarbleNetSegmentationUsesTwentyMillisecondFrames()
        {
            var result = MNNMarbleNetVad.Segment(Enumerable.Repeat(0.9f, 10).Concat(Enumerable.Repeat(0f, 10)).ToArray(), 320 * 20, 320, .5f);
            Assert.AreEqual(1, result.Length);
            Assert.AreEqual(0, result[0].StartSeconds);
            Assert.AreEqual(4000 / 16000.0, result[0].EndSeconds);
        }

        [Test]
        public void FrcrnProfileIsVerified()
        {
            Assert.True(MNNFrcrnSeModel.IsComplete(Path.Combine(ModelsRoot, "FRCRN-SE-16K-MNN")));
        }

        [Test]
        [Explicit("Requires the native MNN plugin available to the Unity Editor; executes the fixed AV-TSE multi-input graph on CPU.")]
        public void AvMossFormer2TargetSpeakerExtractionExecutesWithFiniteOutput()
        {
            const int sampleRate = 16000;
            var audio = new float[48000];
            var visual = new float[75 * 112 * 112];
            for (int i = 0; i < audio.Length; ++i)
                audio[i] = .1f * (float)Math.Sin(2 * Math.PI * 220 * i / sampleRate);
            for (int i = 0; i < visual.Length; ++i)
                visual[i] = .5f;

            using (var model = MNNAvMossFormer2Tse.Load(Path.Combine(ModelsRoot, "AV-MossFormer2-TSE-16K-MNN"), 2, MNNBackendType.CPU))
            {
                var extracted = model.Extract(audio, visual, 75, sampleRate);
                Assert.AreEqual(audio.Length, extracted.Length);
                Assert.That(extracted.All(value => !float.IsNaN(value) && !float.IsInfinity(value)), Is.True);
                Assert.AreEqual(MNNBackendType.CPU, model.Backend);
            }
        }

        [Test]
        [Explicit("Requires a Metal-capable Unity Editor; executes the fixed AV-TSE multi-input graph on Metal.")]
        public void AvMossFormer2TargetSpeakerExtractionExecutesWithMetal()
        {
            if (!MNNAcceleration.IsBackendAvailable(MNNBackendType.Metal))
                Assert.Ignore("Metal backend unavailable on this host.");
            const int sampleRate = 16000;
            var audio = new float[48000];
            var visual = new float[75 * 112 * 112];
            for (int i = 0; i < audio.Length; ++i)
                audio[i] = .1f * (float)Math.Sin(2 * Math.PI * 220 * i / sampleRate);
            for (int i = 0; i < visual.Length; ++i)
                visual[i] = .5f;

            using (var model = MNNAvMossFormer2Tse.Load(Path.Combine(ModelsRoot, "AV-MossFormer2-TSE-16K-MNN"), 2, MNNBackendType.Metal))
            {
                var extracted = model.Extract(audio, visual, 75, sampleRate);
                Assert.AreEqual(audio.Length, extracted.Length);
                Assert.That(extracted.All(value => !float.IsNaN(value) && !float.IsInfinity(value)), Is.True);
                Assert.AreEqual(MNNBackendType.Metal, model.Backend);
            }
        }

        [Test]
        [Explicit("Requires the native MNN plugin available to the Unity Editor; executes the shipped speech graphs on CPU.")]
        public void ConvertedSpeechGraphsExecuteWithFiniteOutputs()
        {
            var pcm = new float[16000];
            for (int i = 0; i < pcm.Length; ++i)
                pcm[i] = .15f * (float)Math.Sin(2 * Math.PI * 220 * i / 16000.0);

            using (var vad = MNNFsmnVad.Load(Path.Combine(ModelsRoot, "FSMN-VAD-16K-MNN"), backendType: MNNBackendType.CPU))
            {
                var result = vad.Detect(pcm, 16000);
                Assert.That(result.Probabilities.Count, Is.GreaterThan(0));
                Assert.That(result.Probabilities.All(value => !float.IsNaN(value) && !float.IsInfinity(value)), Is.True);
                Assert.AreEqual(MNNBackendType.CPU, vad.Backend);
            }

            using (var enhancer = MNNFrcrnSe.Load(Path.Combine(ModelsRoot, "FRCRN-SE-16K-MNN"), backendType: MNNBackendType.CPU))
            {
                var enhanced = enhancer.Enhance(pcm, 16000);
                Assert.AreEqual(pcm.Length, enhanced.Length);
                Assert.That(enhanced.All(value => !float.IsNaN(value) && !float.IsInfinity(value)), Is.True);
                Assert.AreEqual(MNNBackendType.CPU, enhancer.Backend);
            }

            using (var speaker = MNNWeSpeakerEmbedding.Load(Path.Combine(ModelsRoot, "WeSpeaker-ResNet34-LM-MNN"), backendType: MNNBackendType.CPU))
            {
                var vector = speaker.Encode(pcm, 16000);
                Assert.AreEqual(256, vector.Length);
                Assert.That(vector.All(value => !float.IsNaN(value) && !float.IsInfinity(value)), Is.True);
                Assert.That(vector.Sum(value => value * value), Is.EqualTo(1).Within(1e-4));
                Assert.AreEqual(MNNBackendType.CPU, speaker.Backend);
            }
        }

        [Test]
        [Explicit("Requires the MNN plugin and DeepFilterNet graphs; executes the Studio request path.")]
        public void DeepFilterNetStudioPreservesInputWaveSampleRate()
        {
            var model = MNNStudioModel.Read(Path.Combine(ModelsRoot, "DeepFilterNet3-MNN"));
            string path = Path.GetFullPath(Path.Combine("TestArtifacts~", "deepfilter-studio-22050.wav"));
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            var pcm = new float[22061];
            for (int i = 0; i < pcm.Length; ++i) pcm[i] = .1f * (float)Math.Sin(i * .17);
            File.WriteAllBytes(path, MNNWaveAudio.EncodePcm16(pcm, 22050));
            try
            {
                using (var backend = MNNStudioNativeBackend.Load(model, 2, false))
                {
                    var result = backend.Run(new MNNStudioRequest {Audio = path});
                    Assert.AreEqual(22050, result.SampleRate);
                    Assert.AreEqual(pcm.Length, result.Waveform.Length);
                    Assert.That(result.BackendDescription, Does.StartWith("DeepFilterNet3"));
                    Assert.That(result.Waveform.All(value => !float.IsNaN(value) && !float.IsInfinity(value)), Is.True);
                }
            }
            finally { File.Delete(path); }
        }

        [Test]
        [Explicit("Requires the native MNN plugin available to the Unity Editor; verifies Studio keeps the input WAV rate after FRCRN resampling.")]
        public void FrcrnStudioPreservesInputWaveSampleRate()
        {
            string directory = Path.Combine(ModelsRoot, "FRCRN-SE-16K-MNN");
            var model = MNNStudioModel.Read(directory);
            string path = Path.Combine("TestArtifacts~", "frcrn-studio-22050.wav");
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)));
            File.WriteAllBytes(path, MNNWaveAudio.EncodePcm16(new float[22050], 22050));
            try
            {
                using (var backend = MNNStudioNativeBackend.Load(model, 2, false))
                {
                    var result = backend.Run(new MNNStudioRequest {Audio = Path.GetFullPath(path)});
                    Assert.AreEqual(22050, result.SampleRate);
                    Assert.AreEqual(22050, result.Waveform.Length);
                }
            }
            finally
            {
                if (File.Exists(path)) File.Delete(path);
            }
        }
    }
}
