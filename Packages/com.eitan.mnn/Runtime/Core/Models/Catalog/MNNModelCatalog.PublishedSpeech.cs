using System;
using System.Linq;

namespace MNN.Unity
{
    public partial class MNNModelCatalog
    {
        private sealed class PublishedSpeechProfile
        {
            internal readonly string Name, Task;
            internal readonly long Bytes;
            internal readonly string[] Graphs;
            internal PublishedSpeechProfile(string name, string task, long bytes, params string[] graphs)
            {
                Name = name; Task = task; Bytes = bytes; Graphs = graphs;
            }
        }

        // Published repositories only. The existing DTLN AEC entries have their own pinned plans.
        private static readonly PublishedSpeechProfile[] PublishedSpeechProfiles =
        {
            new PublishedSpeechProfile("Silero-v5.1-MNN", "voice-activity-detection", 1266761L, "silero_v5_16k_fp32.mnn"),
            new PublishedSpeechProfile("SmartTurn-v3.2-FP32-MNN", "audio-classification", 32073447L, "smart_turn_v32_fp32.mnn"),
            new PublishedSpeechProfile("MarbleNet-VAD-MNN", "voice-activity-detection", 380052L, "marblenet_fp32.mnn"),
            new PublishedSpeechProfile("DTLN-MNN", "acoustic-noise-suppression", 6040554L, "dtln_1_fp16.mnn", "dtln_1_fp32.mnn", "dtln_2_fp16.mnn", "dtln_2_fp32.mnn"),
            new PublishedSpeechProfile("GTCRN-MNN", "acoustic-noise-suppression", 460391L, "gtcrn_fp32.mnn"),
            new PublishedSpeechProfile("FullSubNet-MNN", "acoustic-noise-suppression", 45978609L, "fullsubnet_fp32.mnn", "fullsubnet_fullband_32_fp32.mnn", "fullsubnet_fullband_32_fp32.mnn.weight", "fullsubnet_subband_32_fp32.mnn", "fullsubnet_subband_32_fp32.mnn.weight"),
            new PublishedSpeechProfile("FullSubNetPlus-MNN", "acoustic-noise-suppression", 70352138L, "fullsubnetplus_fp32.mnn", "fullsubnetplus_front_fp32.mnn", "fullsubnetplus_subband_32_fp32.mnn", "fullsubnetplus_subband_32_fp32.mnn.weight"),
            new PublishedSpeechProfile("MossFormerGAN-SE-16K-MNN", "acoustic-noise-suppression", 15816995L, "mossformergan_fp32.mnn"),
            new PublishedSpeechProfile("MossFormer2-SE-48K-MNN", "acoustic-noise-suppression", 227676143L, "mossformer2_se_fp32.mnn"),
            new PublishedSpeechProfile("ERes2NetV2-MNN", "speaker-verification", 71469492L, "eres2netv2_fp32.mnn"),
            new PublishedSpeechProfile("CAMPlus-MNN", "speaker-verification", 27992915L, "campplus_fp32.mnn"),
            new PublishedSpeechProfile("ECAPA-TDNN-MNN", "speaker-verification", 83165186L, "ecapa_fp32.mnn"),
            new PublishedSpeechProfile("TitaNet-MNN", "speaker-verification", 88922695L, "titanet_fp32.mnn"),
            new PublishedSpeechProfile("FSMN-VAD-16K-MNN", "voice-activity-detection", 1782520L, "fsmn_vad_fp32.mnn"),
            new PublishedSpeechProfile("FRCRN-SE-16K-MNN", "acoustic-noise-suppression", 103997176L, "frcrn_se_16k_fp32.mnn"),
            new PublishedSpeechProfile("DeepFilterNet3-MNN", "acoustic-noise-suppression", 12987802L, "df_dec_fp16.mnn", "df_dec_fp32.mnn", "enc_fp16.mnn", "enc_fp32.mnn", "erb_dec_fp16.mnn", "erb_dec_fp32.mnn"),
            new PublishedSpeechProfile("WeSpeaker-ResNet34-LM-MNN", "speaker-verification", 26555244L, "wespeaker_resnet34_lm_fp32.mnn"),
            new PublishedSpeechProfile("MossFormer2-SS-16K-MNN", "speech-separation", 229566643L, "mossformer2_ss_16k_fp32.mnn"),
            new PublishedSpeechProfile("SepFormer-WSJ02Mix-MNN", "speech-separation", 106269343L, "sepformer_fp32.mnn"),
            new PublishedSpeechProfile("ConvTasNet-Libri2Mix-8K-MNN", "speech-separation", 20228085L, "convtasnet_fp32.mnn"),
            new PublishedSpeechProfile("DPRNN-WHAM-8K-MNN", "speech-separation", 14839492L, "dprnn_native_fp32.mnn"),
            new PublishedSpeechProfile("DPTNet-Libri1Mix-16K-MNN", "acoustic-noise-suppression", 11592972L, "dptnet_native_fp32.mnn"),
            new PublishedSpeechProfile("Sortformer-4spk-v2.1-MNN", "speaker-diarization", 492810991L, "core_fp32.mnn", "preencode_fp32.mnn"),
            new PublishedSpeechProfile("NAMO-Turn-Detector-v1-Multilingual-MNN", "text-classification", 629958802L, "namo_tiled_fp16.mnn"),
            new PublishedSpeechProfile("SudoRMRF-U16-WSJ02Mix-8K-MNN", "speech-separation", 20525295L, "sudormrf_fp32.mnn"),
            new PublishedSpeechProfile("DCCRNet-Libri1Mix-16K-MNN", "acoustic-noise-suppression", 54830855L, "dccrnet_decoder_fp32.mnn", "dccrnet_encoder_fp32.mnn", "dccrnet_native_fp32.mnn", "dccrnet_recurrent_fp32.mnn"),
            new PublishedSpeechProfile("DCUNet-Libri1Mix-16K-MNN", "acoustic-noise-suppression", 39186785L, "dcunet_fp32.mnn", "dcunet_fp32.mnn.weight"),
            new PublishedSpeechProfile("SepReformer-B-WSJ02Mix-8K-MNN", "speech-separation", 62469343L, "sepreformer_fp32.mnn"),
            new PublishedSpeechProfile("SpExPlus-WSJ02Mix-8K-MNN", "speech-separation", 44245201L, "spexplus_audio_fp32.mnn", "spexplus_reference_fp32.mnn"),
            new PublishedSpeechProfile("SEG-YGD-16K-MNN", "speech-separation", 17966047L, "seg_fp32.mnn"),
            new PublishedSpeechProfile("Resemble-Denoiser-44K-MNN", "acoustic-noise-suppression", 45942089L, "denoiser_fp32.mnn"),
            new PublishedSpeechProfile("TIGER-Speech-16K-MNN", "speech-separation", 9163394L, "tiger_fp32.mnn", "tiger_fp32.mnn.weight"),
            new PublishedSpeechProfile("DCCRN-Mayavoz-VCTK-16K-MNN", "acoustic-noise-suppression", 17497465L, "analysis.mnn", "dccrn.mnn", "dccrn.mnn.weight", "decoder.mnn", "encoder.mnn", "recurrent.mnn"),
            new PublishedSpeechProfile("AV-ConvTasNet-LRS2-16K-MNN", "speech-separation", 86188627L, "av_convtasnet_audio_fp32.mnn", "av_convtasnet_visual_fp32.mnn"),
            new PublishedSpeechProfile("AV-DPRNN-VoxCeleb2-16K-MNN", "speech-separation", 61594853L, "av_dprnn_audio_fp32.mnn", "av_dprnn_visual_fp32.mnn"),
            new PublishedSpeechProfile("AV-MossFormer2-TSE-16K-MNN", "speech-separation", 447287799L, "av_mossformer2_tse_16k_3s.mnn", "av_mossformer2_tse_16k_3s_fp32.mnn"),
            new PublishedSpeechProfile("VoxTurn-v1-MNN", "audio-classification", 2872190749L, "audio_encoder.mnn", "decoder_0.mnn", "decoder_1.mnn"),
            new PublishedSpeechProfile("Resemble-Enhance-44K-MNN", "acoustic-noise-suppression", 1483074246L, "decoder_cpu.mnn", "decoder_metal.mnn", "denoiser.mnn", "denoiser.mnn.weight", "encoder_cpu.mnn", "encoder_metal.mnn", "velocity.mnn", "vocoder.mnn"),
            new PublishedSpeechProfile("LS-EEND-Simu-8K-MNN", "speaker-diarization", 44596923L, "decoder_fp32.mnn", "encoder_fp32.mnn"),
            new PublishedSpeechProfile("CueLight-Nano-MNN", "voice-activity-detection", 4093947L, "step_accurate_fp32.mnn", "step_fp32.mnn"),
            new PublishedSpeechProfile("CueLight-Nano-SSM-MNN", "voice-activity-detection", 2107202L, "step_fp32.mnn"),
            new PublishedSpeechProfile("FS-EEND-Simu-8K-MNN", "speaker-diarization", 39706355L, "model_fp32.mnn"),
            new PublishedSpeechProfile("FS-EEND-CALLHOME-8K-MNN", "speaker-diarization", 39706991L, "model_fp32.mnn"),
            new PublishedSpeechProfile("VBx-ResNet101-16K-MNN", "speaker-verification", 59462900L, "model_fp32.mnn"),
            new PublishedSpeechProfile("VBx-ResNet101-8K-MNN", "speaker-verification", 59398377L, "model_fp32.mnn"),
            new PublishedSpeechProfile("TurnSense-SmolLM2-135M-MNN", "text-classification", 543693449L, "model_fp32.mnn"),
        };

        internal static bool IsPublishedSpeechRepository(string repositoryId) =>
            PublishedSpeechProfiles.Any(profile => string.Equals("EitanWang/" + profile.Name, repositoryId, StringComparison.OrdinalIgnoreCase));

        internal void IncludePublishedSpeechProfiles()
        {
            foreach (var profile in PublishedSpeechProfiles)
            {
                string repository = "EitanWang/" + profile.Name;
                var model = models.FirstOrDefault(item => item != null && item.provider == MNNModelSource.ModelScope &&
                    string.Equals(item.repositoryId, repository, StringComparison.OrdinalIgnoreCase));
                if (model == null)
                {
                    model = new MNNRemoteModel {id = repository, provider = MNNModelSource.ModelScope,
                        repositoryId = repository, modelScopeId = repository, name = profile.Name,
                        displayName = profile.Name, author = "EitanWang", revision = "master", isRepository = true};
                    models.Add(model);
                }
                model.category = profile.Task == "speaker-verification" ? MNNModelCatalogCategory.SpeakerRecognition.ToString() : MNNModelCatalogCategory.AudioProcessing.ToString();
                model.tasks = new[] {profile.Task};
                model.tags = new[] {"MNN", profile.Task};
                model.fileSize = profile.Bytes;
                model.mnnFiles = (string[])profile.Graphs.Clone();
                model.supportSummary = (profile.Name == "VoxTurn-v1-MNN" ? "Noncommercial license. " : string.Empty) +
                    "Published MNN deployment files. Consult the model card for input, license and validation limits; catalog availability does not validate inference.";
                model.runtimeAvailable = false;
            }
        }
    }
}
