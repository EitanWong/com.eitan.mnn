# 公共 API 参考

此文件由源码与 Roslyn 语义模型生成。请在对应 C# XML 注释中修改说明，再运行 `validate_public_api.py --write`。

范围：Unity 2021.3 Editor macOS Mono 的 Runtime 与独立 Harness 契约；可选 Burst 未包含。类型/方法的存在不代表模型、平台或设备已验证。使用步骤与线程、所有权约定见 [统一 API 层](PublicApiLayer.md)。

## AudioProcessing

```csharp
MNN.Unity.MNNAcousticCheckSeverity.Error = 2
```

```csharp
MNN.Unity.MNNAcousticCheckSeverity.Information = 0
```

```csharp
MNN.Unity.MNNAcousticCheckSeverity.Warning = 1
```

```csharp
MNN.Unity.MNNAcousticProfile.AutomaticSpeaker = 1
```

```csharp
MNN.Unity.MNNAcousticProfile.Realtime = 0
```

```csharp
public bool MNN.Unity.MNNAcousticSettings.denoise
```

```csharp
public string MNN.Unity.MNNAcousticSettings.denoiserDirectory
```

```csharp
public bool MNN.Unity.MNNAcousticSettings.enabled
```

```csharp
public string MNN.Unity.MNNAcousticSettings.englishKeywordDirectory
```

```csharp
public bool MNN.Unity.MNNAcousticSettings.enhanceKeywords
```

```csharp
public bool MNN.Unity.MNNAcousticSettings.enhanceRecognition
```

```csharp
public bool MNN.Unity.MNNAcousticSettings.enhanceVoiceActivity
```

```csharp
public string MNN.Unity.MNNAcousticSettings.keywordDirectory
```

```csharp
public MNN.Unity.MNNKeywordLanguage MNN.Unity.MNNAcousticSettings.keywordLanguage
```

```csharp
public float MNN.Unity.MNNAcousticSettings.keywordThreshold
```

```csharp
public string MNN.Unity.MNNAcousticSettings.keywords
```

```csharp
public int MNN.Unity.MNNAcousticSettings.maximumCaptureAgeMilliseconds
```

```csharp
public MNN.Unity.MNNAcousticProfile MNN.Unity.MNNAcousticSettings.profile
```

```csharp
public string MNN.Unity.MNNAcousticSettings.separationDirectory
```

```csharp
public string MNN.Unity.MNNAcousticSettings.speakerDirectory
```

```csharp
public float MNN.Unity.MNNAcousticSettings.speakerMargin
```

```csharp
public float MNN.Unity.MNNAcousticSettings.speakerMinimum
```

```csharp
public string MNN.Unity.MNNAcousticSettings.vadDirectory
```

```csharp
public bool MNN.Unity.MNNAcousticSettings.wakeWord
```

```csharp
public const int MNN.Unity.MNNAdaptiveEchoFilter.TapCount = 2048
```

```csharp
public readonly float MNN.Unity.MNNAudioFeatures.HighBand
```

```csharp
public readonly float MNN.Unity.MNNAudioFeatures.LowBand
```

```csharp
public readonly float MNN.Unity.MNNAudioFeatures.Onset
```

```csharp
public readonly float MNN.Unity.MNNAudioFeatures.Peak
```

```csharp
public readonly float MNN.Unity.MNNAudioFeatures.PitchConfidence
```

```csharp
public readonly float MNN.Unity.MNNAudioFeatures.PitchHz
```

```csharp
public readonly float MNN.Unity.MNNAudioFeatures.Rms
```

```csharp
public const string MNN.Unity.MNNAvConvTasNetConfig.FileName = "mnn_av_convtasnet.json"
```

```csharp
public string MNN.Unity.MNNAvConvTasNetConfig.audio_graph
```

```csharp
public string MNN.Unity.MNNAvConvTasNetConfig.audio_input
```

```csharp
public string MNN.Unity.MNNAvConvTasNetConfig.audio_output
```

```csharp
public bool MNN.Unity.MNNAvConvTasNetConfig.causal
```

```csharp
public int MNN.Unity.MNNAvConvTasNetConfig.face_size
```

```csharp
public int MNN.Unity.MNNAvConvTasNetConfig.frames_per_second
```

```csharp
public int MNN.Unity.MNNAvConvTasNetConfig.maximum_samples
```

```csharp
public int MNN.Unity.MNNAvConvTasNetConfig.minimum_samples
```

```csharp
public string MNN.Unity.MNNAvConvTasNetConfig.normalization
```

```csharp
public string MNN.Unity.MNNAvConvTasNetConfig.repository
```

```csharp
public string MNN.Unity.MNNAvConvTasNetConfig.revision
```

```csharp
public int MNN.Unity.MNNAvConvTasNetConfig.sample_rate
```

```csharp
public string MNN.Unity.MNNAvConvTasNetConfig.source_revision
```

```csharp
public int MNN.Unity.MNNAvConvTasNetConfig.visual_features
```

```csharp
public string MNN.Unity.MNNAvConvTasNetConfig.visual_graph
```

```csharp
public string MNN.Unity.MNNAvConvTasNetConfig.visual_input
```

```csharp
public string MNN.Unity.MNNAvConvTasNetConfig.visual_output
```

```csharp
public const int MNN.Unity.MNNAvConvTasNetExtraction.FaceSize = 112
```

```csharp
public const int MNN.Unity.MNNAvConvTasNetExtraction.FramesPerSecond = 25
```

```csharp
public const int MNN.Unity.MNNAvConvTasNetExtraction.SampleRate = 16000
```

```csharp
public const string MNN.Unity.MNNAvDprnnConfig.FileName = "mnn_av_dprnn.json"
```

```csharp
public string MNN.Unity.MNNAvDprnnConfig.audio_graph
```

```csharp
public string MNN.Unity.MNNAvDprnnConfig.audio_input
```

```csharp
public string MNN.Unity.MNNAvDprnnConfig.audio_output
```

```csharp
public bool MNN.Unity.MNNAvDprnnConfig.causal
```

```csharp
public int MNN.Unity.MNNAvDprnnConfig.face_size
```

```csharp
public int MNN.Unity.MNNAvDprnnConfig.frames_per_second
```

```csharp
public int MNN.Unity.MNNAvDprnnConfig.maximum_samples
```

```csharp
public int MNN.Unity.MNNAvDprnnConfig.minimum_samples
```

```csharp
public string MNN.Unity.MNNAvDprnnConfig.normalization
```

```csharp
public string MNN.Unity.MNNAvDprnnConfig.repository
```

```csharp
public string MNN.Unity.MNNAvDprnnConfig.revision
```

```csharp
public int MNN.Unity.MNNAvDprnnConfig.sample_rate
```

```csharp
public string MNN.Unity.MNNAvDprnnConfig.source_revision
```

```csharp
public int MNN.Unity.MNNAvDprnnConfig.visual_features
```

```csharp
public string MNN.Unity.MNNAvDprnnConfig.visual_graph
```

```csharp
public string MNN.Unity.MNNAvDprnnConfig.visual_input
```

```csharp
public string MNN.Unity.MNNAvDprnnConfig.visual_output
```

```csharp
public const int MNN.Unity.MNNAvDprnnExtraction.FaceSize = 112
```

```csharp
public const int MNN.Unity.MNNAvDprnnExtraction.FramesPerSecond = 25
```

```csharp
public const int MNN.Unity.MNNAvDprnnExtraction.SampleRate = 16000
```

```csharp
public const string MNN.Unity.MNNAvMossFormer2TseConfig.FileName = "mnn_av_mossformer2_tse.json"
```

```csharp
public int MNN.Unity.MNNAvMossFormer2TseConfig.chunk_frames
```

```csharp
public int MNN.Unity.MNNAvMossFormer2TseConfig.chunk_samples
```

```csharp
public int MNN.Unity.MNNAvMossFormer2TseConfig.decoder_frame_count
```

```csharp
public int MNN.Unity.MNNAvMossFormer2TseConfig.decoder_frame_length
```

```csharp
public int MNN.Unity.MNNAvMossFormer2TseConfig.decoder_frame_step
```

```csharp
public int MNN.Unity.MNNAvMossFormer2TseConfig.face_size
```

```csharp
public string MNN.Unity.MNNAvMossFormer2TseConfig.graph
```

```csharp
public long MNN.Unity.MNNAvMossFormer2TseConfig.graph_bytes
```

```csharp
public string MNN.Unity.MNNAvMossFormer2TseConfig.graph_fp32
```

```csharp
public long MNN.Unity.MNNAvMossFormer2TseConfig.graph_fp32_bytes
```

```csharp
public string MNN.Unity.MNNAvMossFormer2TseConfig.graph_fp32_sha256
```

```csharp
public string MNN.Unity.MNNAvMossFormer2TseConfig.graph_sha256
```

```csharp
public string MNN.Unity.MNNAvMossFormer2TseConfig.input_audio
```

```csharp
public string MNN.Unity.MNNAvMossFormer2TseConfig.input_visual
```

```csharp
public string MNN.Unity.MNNAvMossFormer2TseConfig.output
```

```csharp
public string MNN.Unity.MNNAvMossFormer2TseConfig.output_mode
```

```csharp
public string MNN.Unity.MNNAvMossFormer2TseConfig.repository
```

```csharp
public int MNN.Unity.MNNAvMossFormer2TseConfig.sample_rate
```

```csharp
public int MNN.Unity.MNNAvMossFormer2TseConfig.video_fps
```

```csharp
MNN.Unity.MNNBargeInAction.Cancelled = 3
```

```csharp
MNN.Unity.MNNBargeInAction.Candidate = 1
```

```csharp
MNN.Unity.MNNBargeInAction.Confirmed = 2
```

```csharp
MNN.Unity.MNNBargeInAction.None = 0
```

```csharp
public const string MNN.Unity.MNNConvTasNetConfig.FileName = "mnn_convtasnet.json"
```

```csharp
public string MNN.Unity.MNNConvTasNetConfig.asteroid_version
```

```csharp
public string MNN.Unity.MNNConvTasNetConfig.filterbanks_version
```

```csharp
public string MNN.Unity.MNNConvTasNetConfig.graph
```

```csharp
public string MNN.Unity.MNNConvTasNetConfig.input
```

```csharp
public int MNN.Unity.MNNConvTasNetConfig.maximum_samples
```

```csharp
public int MNN.Unity.MNNConvTasNetConfig.minimum_samples
```

```csharp
public string MNN.Unity.MNNConvTasNetConfig.normalization
```

```csharp
public string MNN.Unity.MNNConvTasNetConfig.output
```

```csharp
public string MNN.Unity.MNNConvTasNetConfig.repository
```

```csharp
public string MNN.Unity.MNNConvTasNetConfig.revision
```

```csharp
public int MNN.Unity.MNNConvTasNetConfig.sample_rate
```

```csharp
public int MNN.Unity.MNNConvTasNetConfig.speakers
```

```csharp
public const int MNN.Unity.MNNConvTasNetSeparation.SampleRate = 8000
```

```csharp
public const string MNN.Unity.MNNCueLightConfig.CoefficientsFile = "cuelight_coefficients.bin"
```

```csharp
public const string MNN.Unity.MNNCueLightConfig.FileName = "mnn_cuelight.json"
```

```csharp
public int MNN.Unity.MNNCueLightConfig.blocks
```

```csharp
public string MNN.Unity.MNNCueLightConfig.checkpoint_sha256
```

```csharp
public string MNN.Unity.MNNCueLightConfig.coefficient_sha256
```

```csharp
public string MNN.Unity.MNNCueLightConfig.cpu_graph
```

```csharp
public int MNN.Unity.MNNCueLightConfig.fft_size
```

```csharp
public int MNN.Unity.MNNCueLightConfig.frame_samples
```

```csharp
public int MNN.Unity.MNNCueLightConfig.hidden_dimension
```

```csharp
public int MNN.Unity.MNNCueLightConfig.hop_samples
```

```csharp
public int MNN.Unity.MNNCueLightConfig.maximum_samples
```

```csharp
public int MNN.Unity.MNNCueLightConfig.mel_bins
```

```csharp
public string MNN.Unity.MNNCueLightConfig.metal_graph
```

```csharp
public int MNN.Unity.MNNCueLightConfig.outputs
```

```csharp
public string MNN.Unity.MNNCueLightConfig.repository
```

```csharp
public int MNN.Unity.MNNCueLightConfig.reset_onset_frames
```

```csharp
public int MNN.Unity.MNNCueLightConfig.reset_replay_frames
```

```csharp
public int MNN.Unity.MNNCueLightConfig.reset_silence_frames
```

```csharp
public string MNN.Unity.MNNCueLightConfig.revision
```

```csharp
public int MNN.Unity.MNNCueLightConfig.sample_rate
```

```csharp
public int MNN.Unity.MNNCueLightConfig.window_samples
```

```csharp
public const int MNN.Unity.MNNCueLightTurnDetector.DefaultThreadCount = 1
```

```csharp
public const int MNN.Unity.MNNCueLightTurnDetector.FrameSamples = 320
```

```csharp
public const int MNN.Unity.MNNCueLightTurnDetector.SampleRate = 16000
```

```csharp
public const string MNN.Unity.MNNDccrnConfig.FileName = "mnn_dccrn.json"
```

```csharp
public string MNN.Unity.MNNDccrnConfig.analysis_graph
```

```csharp
public string MNN.Unity.MNNDccrnConfig.checkpoint_loading
```

```csharp
public string MNN.Unity.MNNDccrnConfig.decoder_graph
```

```csharp
public string MNN.Unity.MNNDccrnConfig.encoder_graph
```

```csharp
public string MNN.Unity.MNNDccrnConfig.external_weights
```

```csharp
public int MNN.Unity.MNNDccrnConfig.fft_size
```

```csharp
public string MNN.Unity.MNNDccrnConfig.graph
```

```csharp
public int MNN.Unity.MNNDccrnConfig.hop_size
```

```csharp
public int MNN.Unity.MNNDccrnConfig.maximum_samples
```

```csharp
public int MNN.Unity.MNNDccrnConfig.minimum_samples
```

```csharp
public string MNN.Unity.MNNDccrnConfig.normalization
```

```csharp
public string MNN.Unity.MNNDccrnConfig.recurrent_graph
```

```csharp
public string MNN.Unity.MNNDccrnConfig.repository
```

```csharp
public string MNN.Unity.MNNDccrnConfig.revision
```

```csharp
public int MNN.Unity.MNNDccrnConfig.sample_rate
```

```csharp
public string MNN.Unity.MNNDccrnConfig.source_revision
```

```csharp
public int MNN.Unity.MNNDccrnConfig.window_size
```

```csharp
public const int MNN.Unity.MNNDccrnEnhancement.SampleRate = 16000
```

```csharp
public const string MNN.Unity.MNNDccrnetConfig.FileName = "mnn_dccrnet.json"
```

```csharp
public string MNN.Unity.MNNDccrnetConfig.asteroid_version
```

```csharp
public string MNN.Unity.MNNDccrnetConfig.checkpoint_asteroid_version
```

```csharp
public string MNN.Unity.MNNDccrnetConfig.decoder_graph
```

```csharp
public string MNN.Unity.MNNDccrnetConfig.encoder_graph
```

```csharp
public string MNN.Unity.MNNDccrnetConfig.filterbanks_version
```

```csharp
public string MNN.Unity.MNNDccrnetConfig.graph
```

```csharp
public string MNN.Unity.MNNDccrnetConfig.input
```

```csharp
public int MNN.Unity.MNNDccrnetConfig.maximum_samples
```

```csharp
public int MNN.Unity.MNNDccrnetConfig.minimum_samples
```

```csharp
public string MNN.Unity.MNNDccrnetConfig.normalization
```

```csharp
public string MNN.Unity.MNNDccrnetConfig.output
```

```csharp
public string MNN.Unity.MNNDccrnetConfig.recurrent_graph
```

```csharp
public string MNN.Unity.MNNDccrnetConfig.repository
```

```csharp
public string MNN.Unity.MNNDccrnetConfig.revision
```

```csharp
public int MNN.Unity.MNNDccrnetConfig.sample_rate
```

```csharp
public int MNN.Unity.MNNDccrnetConfig.speakers
```

```csharp
public const int MNN.Unity.MNNDccrnetEnhancement.SampleRate = 16000
```

```csharp
public const string MNN.Unity.MNNDcunetConfig.FileName = "mnn_dcunet.json"
```

```csharp
public string MNN.Unity.MNNDcunetConfig.asteroid_version
```

```csharp
public string MNN.Unity.MNNDcunetConfig.checkpoint_asteroid_version
```

```csharp
public string MNN.Unity.MNNDcunetConfig.filterbanks_version
```

```csharp
public string MNN.Unity.MNNDcunetConfig.graph
```

```csharp
public string MNN.Unity.MNNDcunetConfig.input
```

```csharp
public int MNN.Unity.MNNDcunetConfig.maximum_samples
```

```csharp
public int MNN.Unity.MNNDcunetConfig.minimum_samples
```

```csharp
public string MNN.Unity.MNNDcunetConfig.normalization
```

```csharp
public string MNN.Unity.MNNDcunetConfig.output
```

```csharp
public string MNN.Unity.MNNDcunetConfig.repository
```

```csharp
public string MNN.Unity.MNNDcunetConfig.revision
```

```csharp
public int MNN.Unity.MNNDcunetConfig.sample_rate
```

```csharp
public int MNN.Unity.MNNDcunetConfig.speakers
```

```csharp
public string MNN.Unity.MNNDcunetConfig.weights
```

```csharp
public const int MNN.Unity.MNNDcunetEnhancement.SampleRate = 16000
```

```csharp
public const int MNN.Unity.MNNDeepFilterNet3.SampleRate = 48000
```

```csharp
public const string MNN.Unity.MNNDeepFilterNet3Config.FileName = "mnn_deepfilternet3.json"
```

```csharp
public int MNN.Unity.MNNDeepFilterNet3Config.conv_lookahead
```

```csharp
public int MNN.Unity.MNNDeepFilterNet3Config.df_bins
```

```csharp
public string MNN.Unity.MNNDeepFilterNet3Config.df_decoder
```

```csharp
public string MNN.Unity.MNNDeepFilterNet3Config.df_decoder_fp32
```

```csharp
public int MNN.Unity.MNNDeepFilterNet3Config.df_lookahead
```

```csharp
public int MNN.Unity.MNNDeepFilterNet3Config.df_order
```

```csharp
public string MNN.Unity.MNNDeepFilterNet3Config.encoder
```

```csharp
public string MNN.Unity.MNNDeepFilterNet3Config.encoder_fp32
```

```csharp
public int MNN.Unity.MNNDeepFilterNet3Config.erb_bins
```

```csharp
public string MNN.Unity.MNNDeepFilterNet3Config.erb_decoder
```

```csharp
public string MNN.Unity.MNNDeepFilterNet3Config.erb_decoder_fp32
```

```csharp
public string MNN.Unity.MNNDeepFilterNet3Config.execution_mode
```

```csharp
public int MNN.Unity.MNNDeepFilterNet3Config.fft_size
```

```csharp
public int MNN.Unity.MNNDeepFilterNet3Config.hop_size
```

```csharp
public int MNN.Unity.MNNDeepFilterNet3Config.max_audio_seconds
```

```csharp
public float MNN.Unity.MNNDeepFilterNet3Config.norm_alpha
```

```csharp
public string MNN.Unity.MNNDeepFilterNet3Config.repository
```

```csharp
public int MNN.Unity.MNNDeepFilterNet3Config.sample_rate
```

```csharp
public const string MNN.Unity.MNNDprnnConfig.FileName = "mnn_dprnn.json"
```

```csharp
public string MNN.Unity.MNNDprnnConfig.asteroid_version
```

```csharp
public string MNN.Unity.MNNDprnnConfig.filterbanks_version
```

```csharp
public string MNN.Unity.MNNDprnnConfig.graph
```

```csharp
public string MNN.Unity.MNNDprnnConfig.input
```

```csharp
public int MNN.Unity.MNNDprnnConfig.maximum_samples
```

```csharp
public int MNN.Unity.MNNDprnnConfig.minimum_samples
```

```csharp
public string MNN.Unity.MNNDprnnConfig.normalization
```

```csharp
public string MNN.Unity.MNNDprnnConfig.output
```

```csharp
public string MNN.Unity.MNNDprnnConfig.repository
```

```csharp
public string MNN.Unity.MNNDprnnConfig.revision
```

```csharp
public int MNN.Unity.MNNDprnnConfig.sample_rate
```

```csharp
public int MNN.Unity.MNNDprnnConfig.speakers
```

```csharp
public const int MNN.Unity.MNNDprnnSeparation.SampleRate = 8000
```

```csharp
public const string MNN.Unity.MNNDptnetConfig.FileName = "mnn_dptnet.json"
```

```csharp
public string MNN.Unity.MNNDptnetConfig.asteroid_version
```

```csharp
public string MNN.Unity.MNNDptnetConfig.checkpoint_asteroid_version
```

```csharp
public string MNN.Unity.MNNDptnetConfig.filterbanks_version
```

```csharp
public string MNN.Unity.MNNDptnetConfig.graph
```

```csharp
public string MNN.Unity.MNNDptnetConfig.input
```

```csharp
public int MNN.Unity.MNNDptnetConfig.maximum_samples
```

```csharp
public int MNN.Unity.MNNDptnetConfig.minimum_samples
```

```csharp
public string MNN.Unity.MNNDptnetConfig.normalization
```

```csharp
public string MNN.Unity.MNNDptnetConfig.output
```

```csharp
public string MNN.Unity.MNNDptnetConfig.repository
```

```csharp
public string MNN.Unity.MNNDptnetConfig.revision
```

```csharp
public int MNN.Unity.MNNDptnetConfig.sample_rate
```

```csharp
public int MNN.Unity.MNNDptnetConfig.speakers
```

```csharp
public const int MNN.Unity.MNNDptnetEnhancement.SampleRate = 16000
```

```csharp
public const int MNN.Unity.MNNDtln.FrameLength = 512
```

```csharp
public const int MNN.Unity.MNNDtln.HopLength = 128
```

```csharp
public const int MNN.Unity.MNNDtln.OutputDelaySamples = 384
```

```csharp
public const int MNN.Unity.MNNDtln.SampleRate = 16000
```

```csharp
public const int MNN.Unity.MNNDtlnAec.BlockLength = 512
```

```csharp
public const int MNN.Unity.MNNDtlnAec.HopLength = 128
```

```csharp
public const int MNN.Unity.MNNDtlnAec.OutputDelaySamples = 384
```

```csharp
public const int MNN.Unity.MNNDtlnAec.SampleRate = 16000
```

```csharp
public const string MNN.Unity.MNNDtlnConfig.FileName = "mnn_dtln.json"
```

```csharp
public int MNN.Unity.MNNDtlnConfig.frame_length
```

```csharp
public int MNN.Unity.MNNDtlnConfig.hop_length
```

```csharp
public string MNN.Unity.MNNDtlnConfig.offline_alignment
```

```csharp
public int MNN.Unity.MNNDtlnConfig.output_delay_samples
```

```csharp
public string MNN.Unity.MNNDtlnConfig.repository
```

```csharp
public int MNN.Unity.MNNDtlnConfig.sample_rate
```

```csharp
public string MNN.Unity.MNNDtlnConfig.stage1_fp16
```

```csharp
public string MNN.Unity.MNNDtlnConfig.stage1_fp32
```

```csharp
public string MNN.Unity.MNNDtlnConfig.stage2_fp16
```

```csharp
public string MNN.Unity.MNNDtlnConfig.stage2_fp32
```

```csharp
public int MNN.Unity.MNNDtlnConfig.units
```

```csharp
public const int MNN.Unity.MNNDualTurnEndpointing.DefaultThreadCount = 4
```

```csharp
public const int MNN.Unity.MNNDualTurnEndpointing.SampleRate = 24000
```

```csharp
public const int MNN.Unity.MNNDualTurnEndpointing.TickSamples = 1920
```

```csharp
public const string MNN.Unity.MNNDualTurnEndpointingConfig.FileName = "mnn_dualturn_endpointing.json"
```

```csharp
public int MNN.Unity.MNNDualTurnEndpointingConfig.audio_context
```

```csharp
public string MNN.Unity.MNNDualTurnEndpointingConfig.graph
```

```csharp
public int MNN.Unity.MNNDualTurnEndpointingConfig.kv_frames
```

```csharp
public string MNN.Unity.MNNDualTurnEndpointingConfig.repository
```

```csharp
public string MNN.Unity.MNNDualTurnEndpointingConfig.revision
```

```csharp
public int MNN.Unity.MNNDualTurnEndpointingConfig.sample_rate
```

```csharp
public string MNN.Unity.MNNDualTurnEndpointingConfig.source_sha256
```

```csharp
public int MNN.Unity.MNNDualTurnEndpointingConfig.tick_samples
```

```csharp
public int MNN.Unity.MNNDualTurnEndpointingConfig.transformer_history
```

```csharp
public string MNN.Unity.MNNDualTurnEndpointingConfig.weights
```

```csharp
public bool MNN.Unity.MNNEchoCancellationOptions.automaticDelay
```

```csharp
public int MNN.Unity.MNNEchoCancellationOptions.referenceDelayMilliseconds
```

```csharp
public const int MNN.Unity.MNNEchoDelayEstimator.AlignmentHeadroomSamples = 136
```

```csharp
public const int MNN.Unity.MNNEchoDelayEstimator.AlignmentToleranceSamples = 64
```

```csharp
public const int MNN.Unity.MNNEchoDelayEstimator.SampleRate = 16000
```

```csharp
public const int MNN.Unity.MNNFrcrnSe.FftSize = 640
```

```csharp
public const int MNN.Unity.MNNFrcrnSe.FrequencyBins = 321
```

```csharp
public const int MNN.Unity.MNNFrcrnSe.HopSize = 320
```

```csharp
public const int MNN.Unity.MNNFrcrnSe.SampleRate = 16000
```

```csharp
public const string MNN.Unity.MNNFrcrnSeConfig.FileName = "mnn_frcrn_se.json"
```

```csharp
public int MNN.Unity.MNNFrcrnSeConfig.fft_size
```

```csharp
public int MNN.Unity.MNNFrcrnSeConfig.frequency_bins
```

```csharp
public string MNN.Unity.MNNFrcrnSeConfig.graph_fp16
```

```csharp
public string MNN.Unity.MNNFrcrnSeConfig.graph_fp32
```

```csharp
public int MNN.Unity.MNNFrcrnSeConfig.hop_size
```

```csharp
public string MNN.Unity.MNNFrcrnSeConfig.input
```

```csharp
public string MNN.Unity.MNNFrcrnSeConfig.output
```

```csharp
public string MNN.Unity.MNNFrcrnSeConfig.repository
```

```csharp
public int MNN.Unity.MNNFrcrnSeConfig.sample_rate
```

```csharp
public const int MNN.Unity.MNNFsEendActivity.Columns = 5
```

```csharp
public const double MNN.Unity.MNNFsEendActivity.FrameSeconds = 0.1
```

```csharp
public const string MNN.Unity.MNNFsEendConfig.CoefficientsFile = "fs_eend_coefficients.bin"
```

```csharp
public const string MNN.Unity.MNNFsEendConfig.FileName = "mnn_fs_eend.json"
```

```csharp
public double[] MNN.Unity.MNNFsEendConfig.analysis_window
```

```csharp
public string MNN.Unity.MNNFsEendConfig.checkpoint_sha256
```

```csharp
public string MNN.Unity.MNNFsEendConfig.coefficient_sha256
```

```csharp
public int MNN.Unity.MNNFsEendConfig.context_size
```

```csharp
public int MNN.Unity.MNNFsEendConfig.delay_frames
```

```csharp
public string MNN.Unity.MNNFsEendConfig.execution
```

```csharp
public int MNN.Unity.MNNFsEendConfig.fft_size
```

```csharp
public int MNN.Unity.MNNFsEendConfig.frame_shift
```

```csharp
public int MNN.Unity.MNNFsEendConfig.frame_size
```

```csharp
public string MNN.Unity.MNNFsEendConfig.graph
```

```csharp
public string MNN.Unity.MNNFsEendConfig.input_transform
```

```csharp
public int MNN.Unity.MNNFsEendConfig.maximum_samples
```

```csharp
public int MNN.Unity.MNNFsEendConfig.median_frames
```

```csharp
public int MNN.Unity.MNNFsEendConfig.mel_bins
```

```csharp
public float[] MNN.Unity.MNNFsEendConfig.mel_filters
```

```csharp
public int MNN.Unity.MNNFsEendConfig.output_columns
```

```csharp
public string MNN.Unity.MNNFsEendConfig.repository
```

```csharp
public string MNN.Unity.MNNFsEendConfig.revision
```

```csharp
public int MNN.Unity.MNNFsEendConfig.sample_rate
```

```csharp
public int MNN.Unity.MNNFsEendConfig.subsampling
```

```csharp
public float MNN.Unity.MNNFsEendConfig.threshold
```

```csharp
public int MNN.Unity.MNNFsEendConfig.trained_maximum_speakers
```

```csharp
public const int MNN.Unity.MNNFsEendDiarization.DefaultThreadCount = 8
```

```csharp
public const int MNN.Unity.MNNFsEendDiarization.SampleRate = 8000
```

```csharp
public const string MNN.Unity.MNNFsmnVadConfig.FileName = "mnn_fsmn_vad.json"
```

```csharp
public string[] MNN.Unity.MNNFsmnVadConfig.cache_inputs
```

```csharp
public int[] MNN.Unity.MNNFsmnVadConfig.cache_shape
```

```csharp
public double[] MNN.Unity.MNNFsmnVadConfig.cmvn_scale
```

```csharp
public double[] MNN.Unity.MNNFsmnVadConfig.cmvn_shift
```

```csharp
public int MNN.Unity.MNNFsmnVadConfig.feature_bins
```

```csharp
public string MNN.Unity.MNNFsmnVadConfig.graph
```

```csharp
public string MNN.Unity.MNNFsmnVadConfig.input
```

```csharp
public int MNN.Unity.MNNFsmnVadConfig.lfr_m
```

```csharp
public int MNN.Unity.MNNFsmnVadConfig.lfr_n
```

```csharp
public string[] MNN.Unity.MNNFsmnVadConfig.outputs
```

```csharp
public string MNN.Unity.MNNFsmnVadConfig.repository
```

```csharp
public int MNN.Unity.MNNFsmnVadConfig.sample_rate
```

```csharp
public const int MNN.Unity.MNNFullSubNet.FftSize = 512
```

```csharp
public const int MNN.Unity.MNNFullSubNet.FrequencyBins = 257
```

```csharp
public const int MNN.Unity.MNNFullSubNet.HopSize = 256
```

```csharp
public const int MNN.Unity.MNNFullSubNet.SampleRate = 16000
```

```csharp
public const string MNN.Unity.MNNFullSubNetConfig.FileName = "mnn_fullsubnet.json"
```

```csharp
public int MNN.Unity.MNNFullSubNetConfig.fft_size
```

```csharp
public string MNN.Unity.MNNFullSubNetConfig.fullband_graph
```

```csharp
public string MNN.Unity.MNNFullSubNetConfig.graph
```

```csharp
public int MNN.Unity.MNNFullSubNetConfig.hop_size
```

```csharp
public int MNN.Unity.MNNFullSubNetConfig.look_ahead
```

```csharp
public string MNN.Unity.MNNFullSubNetConfig.normalization
```

```csharp
public int MNN.Unity.MNNFullSubNetConfig.recurrent_block_frames
```

```csharp
public string MNN.Unity.MNNFullSubNetConfig.repository
```

```csharp
public int MNN.Unity.MNNFullSubNetConfig.sample_rate
```

```csharp
public string MNN.Unity.MNNFullSubNetConfig.source_revision
```

```csharp
public string MNN.Unity.MNNFullSubNetConfig.subband_graph
```

```csharp
public const int MNN.Unity.MNNGtcrn.FrameLength = 512
```

```csharp
public const int MNN.Unity.MNNGtcrn.FrequencyBins = 257
```

```csharp
public const int MNN.Unity.MNNGtcrn.HopLength = 256
```

```csharp
public const int MNN.Unity.MNNGtcrn.SampleRate = 16000
```

```csharp
public const string MNN.Unity.MNNGtcrnConfig.FileName = "mnn_gtcrn.json"
```

```csharp
public int MNN.Unity.MNNGtcrnConfig.fft_size
```

```csharp
public int MNN.Unity.MNNGtcrnConfig.frequency_bins
```

```csharp
public string MNN.Unity.MNNGtcrnConfig.graph_accelerator
```

```csharp
public string MNN.Unity.MNNGtcrnConfig.graph_cpu
```

```csharp
public int MNN.Unity.MNNGtcrnConfig.hop_size
```

```csharp
public string MNN.Unity.MNNGtcrnConfig.repository
```

```csharp
public int MNN.Unity.MNNGtcrnConfig.sample_rate
```

```csharp
MNN.Unity.MNNKeywordLanguage.Automatic = 0
```

```csharp
MNN.Unity.MNNKeywordLanguage.Chinese = 1
```

```csharp
MNN.Unity.MNNKeywordLanguage.English = 2
```

```csharp
public const int MNN.Unity.MNNKeywordSpotter.DefaultMaxActivePaths = 0
```

```csharp
public const float MNN.Unity.MNNKeywordSpotter.DefaultThreshold = 0.15
```

Shared wake defaults for file, streaming, Studio and application callers.

```csharp
public const int MNN.Unity.MNNKeywordSpotter.DefaultTrailingBlanks = 0
```

```csharp
public const string MNN.Unity.MNNKeywordSpotterConfig.EnglishDefaultWords = "Siri\nHey Siri"
```

```csharp
public const string MNN.Unity.MNNKeywordSpotterConfig.EnglishDirectoryName = "English-KWS-GigaSpeech"
```

```csharp
public const string MNN.Unity.MNNKeywordSpotterConfig.EnglishModelScopeRepository = "EitanWang/English-KWS-GigaSpeech-MNN"
```

```csharp
public const string MNN.Unity.MNNKeywordSpotterConfig.FileName = "mnn_kws.json"
```

```csharp
public string MNN.Unity.MNNKeywordSpotterConfig.repository
```

```csharp
public const string MNN.Unity.MNNKeywordText.LanguageHint = "This wake model supports Chinese. Enter Chinese phrases, one per line. English or mixed-language wake words require a compatible model."
```

```csharp
public const string MNN.Unity.MNNLiveKitTurnDetectorConfig.FileName = "mnn_livekit_turn_detector.json"
```

```csharp
public string MNN.Unity.MNNLiveKitTurnDetectorConfig.graph_accelerator
```

```csharp
public string MNN.Unity.MNNLiveKitTurnDetectorConfig.graph_cpu
```

```csharp
public string MNN.Unity.MNNLiveKitTurnDetectorConfig.input
```

```csharp
public int MNN.Unity.MNNLiveKitTurnDetectorConfig.max_position_embeddings
```

```csharp
public string MNN.Unity.MNNLiveKitTurnDetectorConfig.output
```

```csharp
public string MNN.Unity.MNNLiveKitTurnDetectorConfig.repository
```

```csharp
public string MNN.Unity.MNNLiveKitTurnDetectorConfig.revision
```

```csharp
public string MNN.Unity.MNNLiveKitTurnDetectorConfig.tokenizer
```

```csharp
public readonly string MNN.Unity.MNNLiveKitTurnMessage.Content
```

```csharp
public readonly string MNN.Unity.MNNLiveKitTurnMessage.Role
```

```csharp
public const int MNN.Unity.MNNLsEendActivity.Columns = 9
```

```csharp
public const double MNN.Unity.MNNLsEendActivity.FrameSeconds = 0.1
```

```csharp
public const string MNN.Unity.MNNLsEendConfig.CoefficientsFile = "ls_eend_coefficients.bin"
```

```csharp
public const string MNN.Unity.MNNLsEendConfig.FileName = "mnn_ls_eend.json"
```

```csharp
public double[] MNN.Unity.MNNLsEendConfig.analysis_window
```

```csharp
public string MNN.Unity.MNNLsEendConfig.checkpoint_sha256
```

```csharp
public string MNN.Unity.MNNLsEendConfig.coefficient_sha256
```

```csharp
public int MNN.Unity.MNNLsEendConfig.context_size
```

```csharp
public string MNN.Unity.MNNLsEendConfig.decoder_graph
```

```csharp
public int MNN.Unity.MNNLsEendConfig.delay_frames
```

```csharp
public string MNN.Unity.MNNLsEendConfig.encoder_graph
```

```csharp
public int MNN.Unity.MNNLsEendConfig.fft_size
```

```csharp
public int MNN.Unity.MNNLsEendConfig.frame_shift
```

```csharp
public int MNN.Unity.MNNLsEendConfig.frame_size
```

```csharp
public int MNN.Unity.MNNLsEendConfig.maximum_samples
```

```csharp
public int MNN.Unity.MNNLsEendConfig.median_frames
```

```csharp
public int MNN.Unity.MNNLsEendConfig.mel_bins
```

```csharp
public float[] MNN.Unity.MNNLsEendConfig.mel_filters
```

```csharp
public int MNN.Unity.MNNLsEendConfig.output_columns
```

```csharp
public string MNN.Unity.MNNLsEendConfig.repository
```

```csharp
public string MNN.Unity.MNNLsEendConfig.revision
```

```csharp
public int MNN.Unity.MNNLsEendConfig.sample_rate
```

```csharp
public int MNN.Unity.MNNLsEendConfig.subsampling
```

```csharp
public float MNN.Unity.MNNLsEendConfig.threshold
```

```csharp
public int MNN.Unity.MNNLsEendConfig.trained_maximum_speakers
```

```csharp
public const int MNN.Unity.MNNLsEendDiarization.DefaultThreadCount = 2
```

```csharp
public const int MNN.Unity.MNNLsEendDiarization.SampleRate = 8000
```

```csharp
public const string MNN.Unity.MNNMarbleNetVadConfig.FileName = "mnn_marblenet_vad.json"
```

```csharp
public int MNN.Unity.MNNMarbleNetVadConfig.feature_bins
```

```csharp
public int MNN.Unity.MNNMarbleNetVadConfig.frame_shift_samples
```

```csharp
public string MNN.Unity.MNNMarbleNetVadConfig.graph
```

```csharp
public string MNN.Unity.MNNMarbleNetVadConfig.input
```

```csharp
public string MNN.Unity.MNNMarbleNetVadConfig.output
```

```csharp
public int MNN.Unity.MNNMarbleNetVadConfig.output_frame_samples
```

```csharp
public string MNN.Unity.MNNMarbleNetVadConfig.repository
```

```csharp
public int MNN.Unity.MNNMarbleNetVadConfig.sample_rate
```

```csharp
public int MNN.Unity.MNNMarbleNetVadConfig.speech_class
```

```csharp
public const string MNN.Unity.MNNMossFormer2Config.FileName = "mnn_mossformer2_ss.json"
```

```csharp
public int MNN.Unity.MNNMossFormer2Config.conversion_optimize_level
```

```csharp
public string MNN.Unity.MNNMossFormer2Config.graph
```

```csharp
public string MNN.Unity.MNNMossFormer2Config.input
```

```csharp
public string[] MNN.Unity.MNNMossFormer2Config.outputs
```

```csharp
public string MNN.Unity.MNNMossFormer2Config.repository
```

```csharp
public int MNN.Unity.MNNMossFormer2Config.sample_rate
```

```csharp
public int MNN.Unity.MNNMossFormer2Config.speakers
```

```csharp
public const int MNN.Unity.MNNMossFormer2Se.FftSize = 1920
```

```csharp
public const int MNN.Unity.MNNMossFormer2Se.FrequencyBins = 961
```

```csharp
public const int MNN.Unity.MNNMossFormer2Se.HopSize = 384
```

```csharp
public const int MNN.Unity.MNNMossFormer2Se.SampleRate = 48000
```

```csharp
public const string MNN.Unity.MNNMossFormer2SeConfig.FileName = "mnn_mossformer2_se.json"
```

```csharp
public float[] MNN.Unity.MNNMossFormer2SeConfig.analysis_window
```

```csharp
public string MNN.Unity.MNNMossFormer2SeConfig.coefficient_sha256
```

```csharp
public float MNN.Unity.MNNMossFormer2SeConfig.dither
```

```csharp
public int MNN.Unity.MNNMossFormer2SeConfig.dither_seed
```

```csharp
public int MNN.Unity.MNNMossFormer2SeConfig.fft_size
```

```csharp
public string MNN.Unity.MNNMossFormer2SeConfig.graph
```

```csharp
public int MNN.Unity.MNNMossFormer2SeConfig.hop_size
```

```csharp
public int MNN.Unity.MNNMossFormer2SeConfig.mel_bins
```

```csharp
public int MNN.Unity.MNNMossFormer2SeConfig.mel_fft_size
```

```csharp
public float[] MNN.Unity.MNNMossFormer2SeConfig.mel_filters
```

```csharp
public string MNN.Unity.MNNMossFormer2SeConfig.repository
```

```csharp
public string MNN.Unity.MNNMossFormer2SeConfig.revision
```

```csharp
public int MNN.Unity.MNNMossFormer2SeConfig.sample_rate
```

```csharp
public string MNN.Unity.MNNMossFormer2SeConfig.source_revision
```

```csharp
public const int MNN.Unity.MNNMossFormer2Separation.SampleRate = 16000
```

```csharp
public const int MNN.Unity.MNNMossFormerGan.FftSize = 400
```

```csharp
public const int MNN.Unity.MNNMossFormerGan.FrequencyBins = 201
```

```csharp
public const int MNN.Unity.MNNMossFormerGan.HopSize = 100
```

```csharp
public const int MNN.Unity.MNNMossFormerGan.SampleRate = 16000
```

```csharp
public const string MNN.Unity.MNNMossFormerGanConfig.FileName = "mnn_mossformergan.json"
```

```csharp
public float[] MNN.Unity.MNNMossFormerGanConfig.analysis_window
```

```csharp
public int MNN.Unity.MNNMossFormerGanConfig.fft_size
```

```csharp
public string MNN.Unity.MNNMossFormerGanConfig.graph
```

```csharp
public int MNN.Unity.MNNMossFormerGanConfig.hop_size
```

```csharp
public string MNN.Unity.MNNMossFormerGanConfig.normalization
```

```csharp
public float MNN.Unity.MNNMossFormerGanConfig.power_compression
```

```csharp
public string MNN.Unity.MNNMossFormerGanConfig.repository
```

```csharp
public string MNN.Unity.MNNMossFormerGanConfig.revision
```

```csharp
public int MNN.Unity.MNNMossFormerGanConfig.sample_rate
```

```csharp
public string MNN.Unity.MNNMossFormerGanConfig.source_revision
```

```csharp
public const int MNN.Unity.MNNNamoTurnDetector.MaximumTokens = 8192
```

```csharp
public const int MNN.Unity.MNNNamoTurnDetector.MetalMaximumTokens = 512
```

```csharp
public const string MNN.Unity.MNNNamoTurnDetectorConfig.FileName = "mnn_namo_turn_detector.json"
```

```csharp
public string MNN.Unity.MNNNamoTurnDetectorConfig.attention_mask
```

```csharp
public int MNN.Unity.MNNNamoTurnDetectorConfig.end_of_turn_class
```

```csharp
public string MNN.Unity.MNNNamoTurnDetectorConfig.graph
```

```csharp
public string MNN.Unity.MNNNamoTurnDetectorConfig.input_ids
```

```csharp
public int MNN.Unity.MNNNamoTurnDetectorConfig.maximum_tokens
```

```csharp
public string MNN.Unity.MNNNamoTurnDetectorConfig.output
```

```csharp
public string MNN.Unity.MNNNamoTurnDetectorConfig.repository
```

```csharp
public string MNN.Unity.MNNNamoTurnDetectorConfig.revision
```

```csharp
public string MNN.Unity.MNNNamoTurnDetectorConfig.source
```

```csharp
public string MNN.Unity.MNNNamoTurnDetectorConfig.tokenizer
```

```csharp
public const int MNN.Unity.MNNPlaybackWaveform.PointCount = 64
```

```csharp
public double MNN.Unity.MNNRealtimeDeviceSessionOptions.CaptureWarmupSeconds
```

```csharp
public MNN.Unity.MNNEchoCancellationOptions MNN.Unity.MNNRealtimeDeviceSessionOptions.Echo
```

```csharp
public bool MNN.Unity.MNNRealtimeDeviceSessionOptions.EchoEnabled
```

```csharp
public string MNN.Unity.MNNRealtimeDeviceSessionOptions.EchoModelDirectory
```

```csharp
public bool MNN.Unity.MNNRealtimeDeviceSessionOptions.IncludeMeasuredEchoDelayInPresentation
```

```csharp
public int MNN.Unity.MNNRealtimeDeviceSessionOptions.OutputBlockCapacity
```

```csharp
public int MNN.Unity.MNNRealtimeDeviceSessionOptions.OutputSegmentCapacity
```

```csharp
public double MNN.Unity.MNNRealtimeDeviceSessionOptions.OutputWarmupSeconds
```

```csharp
public readonly bool MNN.Unity.MNNRealtimeDeviceSnapshot.Active
```

```csharp
public readonly bool MNN.Unity.MNNRealtimeDeviceSnapshot.Audible
```

```csharp
public readonly bool MNN.Unity.MNNRealtimeDeviceSnapshot.CanRestartPlayback
```

```csharp
public readonly bool MNN.Unity.MNNRealtimeDeviceSnapshot.CaptureStarting
```

```csharp
public readonly bool MNN.Unity.MNNRealtimeDeviceSnapshot.Capturing
```

```csharp
public readonly System.Exception MNN.Unity.MNNRealtimeDeviceSnapshot.Fault
```

```csharp
public readonly bool MNN.Unity.MNNRealtimeDeviceSnapshot.InputActive
```

```csharp
public readonly bool MNN.Unity.MNNRealtimeDeviceSnapshot.OutputActive
```

```csharp
public readonly MNN.Unity.MNNPcmFormat MNN.Unity.MNNRealtimeDeviceSnapshot.OutputFormat
```

```csharp
public readonly bool MNN.Unity.MNNRealtimeDeviceSnapshot.OutputPaused
```

```csharp
public readonly bool MNN.Unity.MNNRealtimeDeviceSnapshot.OutputRunning
```

```csharp
public readonly bool MNN.Unity.MNNRealtimeDeviceSnapshot.Playing
```

```csharp
public readonly double MNN.Unity.MNNRealtimeDeviceSnapshot.PresentationTailMilliseconds
```

```csharp
public readonly long MNN.Unity.MNNRealtimeDeviceSnapshot.RenderedFrames
```

```csharp
public readonly bool MNN.Unity.MNNRealtimeDeviceSnapshot.RouteMatches
```

```csharp
public readonly float MNN.Unity.MNNRealtimeDeviceSnapshot.Volume
```

```csharp
public const int MNN.Unity.MNNRealtimeModelInputBuffer.FrameSamples = 512
```

```csharp
public const int MNN.Unity.MNNRealtimeModelInputBuffer.MaximumCapacity = 64
```

```csharp
public const int MNN.Unity.MNNRealtimeModelInputBuffer.SampleRate = 16000
```

```csharp
public const int MNN.Unity.MNNRealtimeVoiceActivity.ContextSamples = 64
```

```csharp
public const int MNN.Unity.MNNRealtimeVoiceActivity.SampleRate = 16000
```

```csharp
public const int MNN.Unity.MNNRealtimeVoiceActivity.SamplesPerInference = 512
```

```csharp
public const int MNN.Unity.MNNRealtimeVoiceActivity.StateSamples = 256
```

```csharp
public const int MNN.Unity.MNNResembleDenoiser.MaximumSamples = 132300
```

```csharp
public const int MNN.Unity.MNNResembleDenoiser.MinimumSamples = 400
```

```csharp
public const int MNN.Unity.MNNResembleDenoiser.SampleRate = 44100
```

```csharp
public const string MNN.Unity.MNNResembleDenoiserConfig.FileName = "mnn_resemble_denoiser.json"
```

```csharp
public float[] MNN.Unity.MNNResembleDenoiserConfig.analysis_window
```

```csharp
public bool MNN.Unity.MNNResembleDenoiserConfig.causal
```

```csharp
public string MNN.Unity.MNNResembleDenoiserConfig.coefficient_sha256
```

```csharp
public int MNN.Unity.MNNResembleDenoiserConfig.fft_size
```

```csharp
public string MNN.Unity.MNNResembleDenoiserConfig.graph
```

```csharp
public int MNN.Unity.MNNResembleDenoiserConfig.hop_size
```

```csharp
public string MNN.Unity.MNNResembleDenoiserConfig.input
```

```csharp
public int MNN.Unity.MNNResembleDenoiserConfig.maximum_samples
```

```csharp
public int MNN.Unity.MNNResembleDenoiserConfig.minimum_samples
```

```csharp
public string MNN.Unity.MNNResembleDenoiserConfig.output
```

```csharp
public string MNN.Unity.MNNResembleDenoiserConfig.repository
```

```csharp
public string MNN.Unity.MNNResembleDenoiserConfig.revision
```

```csharp
public int MNN.Unity.MNNResembleDenoiserConfig.right_padding
```

```csharp
public int MNN.Unity.MNNResembleDenoiserConfig.sample_rate
```

```csharp
public string MNN.Unity.MNNResembleDenoiserConfig.source_revision
```

```csharp
public const int MNN.Unity.MNNResembleEnhancer.MaximumSamples = 132300
```

```csharp
public const int MNN.Unity.MNNResembleEnhancer.MinimumSamples = 400
```

```csharp
public const int MNN.Unity.MNNResembleEnhancer.SampleRate = 44100
```

```csharp
public const string MNN.Unity.MNNSegConfig.FileName = "mnn_seg.json"
```

```csharp
public string MNN.Unity.MNNSegConfig.audio_input
```

```csharp
public string MNN.Unity.MNNSegConfig.audio_output
```

```csharp
public bool MNN.Unity.MNNSegConfig.causal
```

```csharp
public int MNN.Unity.MNNSegConfig.gesture_features
```

```csharp
public int MNN.Unity.MNNSegConfig.gesture_fps
```

```csharp
public string MNN.Unity.MNNSegConfig.gesture_input
```

```csharp
public string MNN.Unity.MNNSegConfig.graph
```

```csharp
public int MNN.Unity.MNNSegConfig.maximum_samples
```

```csharp
public int MNN.Unity.MNNSegConfig.minimum_samples
```

```csharp
public string MNN.Unity.MNNSegConfig.normalization
```

```csharp
public string MNN.Unity.MNNSegConfig.repository
```

```csharp
public string MNN.Unity.MNNSegConfig.revision
```

```csharp
public int MNN.Unity.MNNSegConfig.sample_rate
```

```csharp
public string MNN.Unity.MNNSegConfig.source_revision
```

```csharp
public const int MNN.Unity.MNNSegExtraction.SampleRate = 16000
```

```csharp
public const int MNN.Unity.MNNSegGestureFeatures.FeaturesPerFrame = 30
```

```csharp
public const int MNN.Unity.MNNSegGestureFeatures.FramesPerSecond = 15
```

```csharp
public const string MNN.Unity.MNNSepFormerConfig.FileName = "mnn_sepformer.json"
```

```csharp
public string MNN.Unity.MNNSepFormerConfig.graph
```

```csharp
public string MNN.Unity.MNNSepFormerConfig.input
```

```csharp
public int MNN.Unity.MNNSepFormerConfig.maximum_samples
```

```csharp
public int MNN.Unity.MNNSepFormerConfig.minimum_samples
```

```csharp
public string MNN.Unity.MNNSepFormerConfig.normalization
```

```csharp
public string MNN.Unity.MNNSepFormerConfig.output
```

```csharp
public string MNN.Unity.MNNSepFormerConfig.repository
```

```csharp
public string MNN.Unity.MNNSepFormerConfig.revision
```

```csharp
public int MNN.Unity.MNNSepFormerConfig.sample_rate
```

```csharp
public int MNN.Unity.MNNSepFormerConfig.speakers
```

```csharp
public string MNN.Unity.MNNSepFormerConfig.speechbrain_version
```

```csharp
public const int MNN.Unity.MNNSepFormerSeparation.SampleRate = 8000
```

```csharp
public const string MNN.Unity.MNNSepReformerConfig.FileName = "mnn_sepreformer.json"
```

```csharp
public string MNN.Unity.MNNSepReformerConfig.graph
```

```csharp
public string MNN.Unity.MNNSepReformerConfig.input
```

```csharp
public int MNN.Unity.MNNSepReformerConfig.maximum_samples
```

```csharp
public int MNN.Unity.MNNSepReformerConfig.minimum_samples
```

```csharp
public string MNN.Unity.MNNSepReformerConfig.normalization
```

```csharp
public string MNN.Unity.MNNSepReformerConfig.output
```

```csharp
public int MNN.Unity.MNNSepReformerConfig.padding_multiple
```

```csharp
public string MNN.Unity.MNNSepReformerConfig.repository
```

```csharp
public string MNN.Unity.MNNSepReformerConfig.revision
```

```csharp
public int MNN.Unity.MNNSepReformerConfig.sample_rate
```

```csharp
public int MNN.Unity.MNNSepReformerConfig.speakers
```

```csharp
public string MNN.Unity.MNNSepReformerConfig.weights
```

```csharp
public const int MNN.Unity.MNNSepReformerSeparation.SampleRate = 8000
```

```csharp
public readonly int MNN.Unity.MNNSeparatedAudio.SampleRate
```

```csharp
public readonly float[] MNN.Unity.MNNSeparatedAudio.Speaker0
```

```csharp
public readonly float[] MNN.Unity.MNNSeparatedAudio.Speaker1
```

```csharp
public const string MNN.Unity.MNNSileroVadConfig.FileName = "mnn_silero_vad.json"
```

```csharp
public string MNN.Unity.MNNSileroVadConfig.graph
```

```csharp
public string MNN.Unity.MNNSileroVadConfig.repository
```

```csharp
public string MNN.Unity.MNNSileroVadConfig.source_revision
```

```csharp
public const string MNN.Unity.MNNSmartTurnConfig.FileName = "mnn_smart_turn.json"
```

```csharp
public string MNN.Unity.MNNSmartTurnConfig.graph
```

```csharp
public string MNN.Unity.MNNSmartTurnConfig.repository
```

```csharp
public string MNN.Unity.MNNSmartTurnConfig.source_revision
```

```csharp
public const string MNN.Unity.MNNSortformerConfig.FileName = "mnn_sortformer.json"
```

```csharp
public float[] MNN.Unity.MNNSortformerConfig.analysis_window
```

```csharp
public int MNN.Unity.MNNSortformerConfig.cache_frames
```

```csharp
public string MNN.Unity.MNNSortformerConfig.checkpoint_nemo_version
```

```csharp
public int MNN.Unity.MNNSortformerConfig.chunk_frames
```

```csharp
public string MNN.Unity.MNNSortformerConfig.core_graph
```

```csharp
public int MNN.Unity.MNNSortformerConfig.maximum_samples
```

```csharp
public int MNN.Unity.MNNSortformerConfig.mel_bins
```

```csharp
public float[] MNN.Unity.MNNSortformerConfig.mel_filters
```

```csharp
public string MNN.Unity.MNNSortformerConfig.nemo_version
```

```csharp
public string MNN.Unity.MNNSortformerConfig.preencode_graph
```

```csharp
public string MNN.Unity.MNNSortformerConfig.repository
```

```csharp
public string MNN.Unity.MNNSortformerConfig.revision
```

```csharp
public int MNN.Unity.MNNSortformerConfig.sample_rate
```

```csharp
public int MNN.Unity.MNNSortformerConfig.speakers
```

```csharp
public const int MNN.Unity.MNNSortformerDiarization.SampleRate = 16000
```

```csharp
public const string MNN.Unity.MNNSpExPlusConfig.FileName = "mnn_spexplus.json"
```

```csharp
public string MNN.Unity.MNNSpExPlusConfig.audio_graph
```

```csharp
public string MNN.Unity.MNNSpExPlusConfig.audio_input
```

```csharp
public string MNN.Unity.MNNSpExPlusConfig.audio_output
```

```csharp
public bool MNN.Unity.MNNSpExPlusConfig.causal
```

```csharp
public int MNN.Unity.MNNSpExPlusConfig.embedding_size
```

```csharp
public int MNN.Unity.MNNSpExPlusConfig.maximum_reference_samples
```

```csharp
public int MNN.Unity.MNNSpExPlusConfig.maximum_samples
```

```csharp
public int MNN.Unity.MNNSpExPlusConfig.minimum_reference_samples
```

```csharp
public int MNN.Unity.MNNSpExPlusConfig.minimum_samples
```

```csharp
public string MNN.Unity.MNNSpExPlusConfig.normalization
```

```csharp
public string MNN.Unity.MNNSpExPlusConfig.reference_graph
```

```csharp
public string MNN.Unity.MNNSpExPlusConfig.reference_input
```

```csharp
public string MNN.Unity.MNNSpExPlusConfig.reference_output
```

```csharp
public string MNN.Unity.MNNSpExPlusConfig.repository
```

```csharp
public string MNN.Unity.MNNSpExPlusConfig.revision
```

```csharp
public int MNN.Unity.MNNSpExPlusConfig.sample_rate
```

```csharp
public string MNN.Unity.MNNSpExPlusConfig.source_revision
```

```csharp
public const int MNN.Unity.MNNSpExPlusExtraction.SampleRate = 8000
```

```csharp
public const double MNN.Unity.MNNSpeakerActivity.FrameSeconds = 0.08
```

```csharp
public const int MNN.Unity.MNNSpeakerActivity.Speakers = 4
```

```csharp
public const string MNN.Unity.MNNSpeakerModelConfig.FileName = "mnn_speaker.json"
```

```csharp
public float[] MNN.Unity.MNNSpeakerModelConfig.analysis_window
```

```csharp
public string MNN.Unity.MNNSpeakerModelConfig.frontend
```

```csharp
public string MNN.Unity.MNNSpeakerModelConfig.graph
```

```csharp
public string MNN.Unity.MNNSpeakerModelConfig.graph_fp16
```

```csharp
public float[] MNN.Unity.MNNSpeakerModelConfig.mel_bands
```

```csharp
public float[] MNN.Unity.MNNSpeakerModelConfig.mel_centers
```

```csharp
public float[] MNN.Unity.MNNSpeakerModelConfig.mel_filters
```

```csharp
public string MNN.Unity.MNNSpeakerModelConfig.repository
```

```csharp
public string MNN.Unity.MNNSpeakerModelConfig.source_revision
```

```csharp
public const int MNN.Unity.MNNSpeechFade.DurationMilliseconds = 24
```

```csharp
public const string MNN.Unity.MNNSudoRmRfConfig.FileName = "mnn_sudormrf.json"
```

```csharp
public string MNN.Unity.MNNSudoRmRfConfig.graph
```

```csharp
public string MNN.Unity.MNNSudoRmRfConfig.input
```

```csharp
public int MNN.Unity.MNNSudoRmRfConfig.maximum_samples
```

```csharp
public int MNN.Unity.MNNSudoRmRfConfig.minimum_samples
```

```csharp
public string MNN.Unity.MNNSudoRmRfConfig.normalization
```

```csharp
public string MNN.Unity.MNNSudoRmRfConfig.output
```

```csharp
public int MNN.Unity.MNNSudoRmRfConfig.padding_multiple
```

```csharp
public string MNN.Unity.MNNSudoRmRfConfig.repository
```

```csharp
public string MNN.Unity.MNNSudoRmRfConfig.revision
```

```csharp
public int MNN.Unity.MNNSudoRmRfConfig.sample_rate
```

```csharp
public int MNN.Unity.MNNSudoRmRfConfig.speakers
```

```csharp
public string MNN.Unity.MNNSudoRmRfConfig.weights
```

```csharp
public const int MNN.Unity.MNNSudoRmRfSeparation.SampleRate = 8000
```

```csharp
public const string MNN.Unity.MNNTigerConfig.FileName = "mnn_tiger.json"
```

```csharp
public float[] MNN.Unity.MNNTigerConfig.analysis_window
```

```csharp
public bool MNN.Unity.MNNTigerConfig.causal
```

```csharp
public string MNN.Unity.MNNTigerConfig.coefficient_sha256
```

```csharp
public int MNN.Unity.MNNTigerConfig.fft_size
```

```csharp
public string MNN.Unity.MNNTigerConfig.graph
```

```csharp
public int MNN.Unity.MNNTigerConfig.hop_size
```

```csharp
public string MNN.Unity.MNNTigerConfig.input
```

```csharp
public int MNN.Unity.MNNTigerConfig.maximum_samples
```

```csharp
public int MNN.Unity.MNNTigerConfig.minimum_samples
```

```csharp
public string MNN.Unity.MNNTigerConfig.output
```

```csharp
public string MNN.Unity.MNNTigerConfig.repository
```

```csharp
public string MNN.Unity.MNNTigerConfig.revision
```

```csharp
public int MNN.Unity.MNNTigerConfig.sample_rate
```

```csharp
public string MNN.Unity.MNNTigerConfig.source_revision
```

```csharp
public int MNN.Unity.MNNTigerConfig.speakers
```

```csharp
public const int MNN.Unity.MNNTigerSeparation.SampleRate = 16000
```

```csharp
public const int MNN.Unity.MNNTurnSense.MaximumTokens = 256
```

```csharp
public const string MNN.Unity.MNNTurnSenseConfig.FileName = "mnn_turnsense.json"
```

```csharp
public int MNN.Unity.MNNTurnSenseConfig.end_of_utterance_class
```

```csharp
public string MNN.Unity.MNNTurnSenseConfig.graph
```

```csharp
public int MNN.Unity.MNNTurnSenseConfig.maximum_tokens
```

```csharp
public string MNN.Unity.MNNTurnSenseConfig.output
```

```csharp
public int MNN.Unity.MNNTurnSenseConfig.pad_token_id
```

```csharp
public string MNN.Unity.MNNTurnSenseConfig.prefix
```

```csharp
public string MNN.Unity.MNNTurnSenseConfig.repository
```

```csharp
public string MNN.Unity.MNNTurnSenseConfig.revision
```

```csharp
public string MNN.Unity.MNNTurnSenseConfig.source_sha256
```

```csharp
public string MNN.Unity.MNNTurnSenseConfig.tokenizer
```

```csharp
public const string MNN.Unity.MNNVbxConfig.CoefficientsFile = "vbx_coefficients.bin"
```

```csharp
public const string MNN.Unity.MNNVbxConfig.FileName = "mnn_vbx.json"
```

```csharp
public string MNN.Unity.MNNVbxConfig.checkpoint_sha256
```

```csharp
public int MNN.Unity.MNNVbxConfig.cmn_left
```

```csharp
public int MNN.Unity.MNNVbxConfig.cmn_right
```

```csharp
public string MNN.Unity.MNNVbxConfig.coefficient_sha256
```

```csharp
public int MNN.Unity.MNNVbxConfig.dither_level
```

```csharp
public int MNN.Unity.MNNVbxConfig.dither_seed
```

```csharp
public int MNN.Unity.MNNVbxConfig.embedding_dimension
```

```csharp
public string MNN.Unity.MNNVbxConfig.execution
```

```csharp
public int MNN.Unity.MNNVbxConfig.fft_size
```

```csharp
public int MNN.Unity.MNNVbxConfig.frame_shift
```

```csharp
public int MNN.Unity.MNNVbxConfig.frame_size
```

```csharp
public string MNN.Unity.MNNVbxConfig.graph
```

```csharp
public string MNN.Unity.MNNVbxConfig.input
```

```csharp
public int MNN.Unity.MNNVbxConfig.maximum_samples
```

```csharp
public int MNN.Unity.MNNVbxConfig.mel_bins
```

```csharp
public int MNN.Unity.MNNVbxConfig.minimum_samples
```

```csharp
public string MNN.Unity.MNNVbxConfig.output
```

```csharp
public string MNN.Unity.MNNVbxConfig.repository
```

```csharp
public string MNN.Unity.MNNVbxConfig.revision
```

```csharp
public int MNN.Unity.MNNVbxConfig.sample_rate
```

```csharp
public const int MNN.Unity.MNNVbxEmbedding.DefaultThreadCount = 8
```

```csharp
public const int MNN.Unity.MNNVbxEmbedding.SampleRate = 16000
```

```csharp
public const string MNN.Unity.MNNWeSpeakerModelConfig.FileName = "mnn_wespeaker.json"
```

```csharp
public int MNN.Unity.MNNWeSpeakerModelConfig.embedding_dimension
```

```csharp
public string MNN.Unity.MNNWeSpeakerModelConfig.graph
```

```csharp
public string MNN.Unity.MNNWeSpeakerModelConfig.graph_fp16
```

```csharp
public string MNN.Unity.MNNWeSpeakerModelConfig.input
```

```csharp
public string MNN.Unity.MNNWeSpeakerModelConfig.output
```

```csharp
public string MNN.Unity.MNNWeSpeakerModelConfig.repository
```

```csharp
void MNN.Unity.IMNNAcousticEnhancer.EnhanceFrame(float[] samples, int offset)
```

```csharp
float[] MNN.Unity.IMNNAcousticEnhancer.EnhanceUtterance(float[] samples, System.Threading.CancellationToken token)
```

```csharp
void MNN.Unity.IMNNAcousticEnhancer.Reset()
```

```csharp
double MNN.Unity.IMNNAcousticKeywordModel.AcceptWaveform(float[] samples)
```

Consumes one 512-sample 16 kHz frame; returns the latest keyword end time, or -1.

```csharp
void MNN.Unity.IMNNAcousticKeywordModel.Reset(string keywords, float threshold)
```

```csharp
MNN.Unity.IMNNAcousticEnhancer MNN.Unity.IMNNAcousticModelFactory.CreateEnhancer(string directory)
```

```csharp
MNN.Unity.IMNNAcousticKeywordModel MNN.Unity.IMNNAcousticModelFactory.CreateKeyword(string directory)
```

```csharp
MNN.Unity.IMNNAcousticSeparator MNN.Unity.IMNNAcousticModelFactory.CreateSeparator(string directory)
```

```csharp
MNN.Unity.IMNNAcousticSpeakerEncoder MNN.Unity.IMNNAcousticModelFactory.CreateSpeaker(string directory)
```

```csharp
float[][] MNN.Unity.IMNNAcousticSeparator.Separate(float[] samples, System.Threading.CancellationToken token)
```

Returns two mono 16 kHz candidates with the same length as samples.

```csharp
float[] MNN.Unity.IMNNAcousticSpeakerEncoder.Encode(float[] samples, System.Threading.CancellationToken token)
```

```csharp
bool MNN.Unity.IMNNAudioCaptureSource.TryReadCapture(float[] destination, int offset, out MNN.Unity.MNNAudioFrameInfo info, out int count)
```

Copies one complete interleaved block. Live metadata timestamps the first sample frame in current-process Stopwatch ticks; historical replay requires an explicit offline age policy.

```csharp
void MNN.Unity.IMNNEchoHopProcessor.ProcessHop(float[] microphone, float[] reference, float[] output)
```

```csharp
void MNN.Unity.IMNNEchoHopProcessor.Reset()
```

```csharp
void MNN.Unity.IMNNRealtimeDemandSink.Enqueue(System.ReadOnlySpan<float> samples)
```

```csharp
System.Threading.Tasks.Task MNN.Unity.IMNNRealtimeDeviceClock.DelayAsync(int milliseconds, System.Threading.CancellationToken token)
```

```csharp
MNN.Unity.MNNRealtimeVadUpdate MNN.Unity.IMNNRealtimeVoiceActivity.ProcessFrame(float[] samples, int offset)
```

```csharp
void MNN.Unity.IMNNRealtimeVoiceActivity.Reset()
```

```csharp
bool MNN.Unity.IMNNRealtimeVoiceInputProcessor.Complete(System.Action<MNN.Unity.MNNRealtimeVadUpdate> onUpdate, System.Action<float[], int, MNN.Unity.MNNRealtimeVadUpdate> onFrame = null)
```

```csharp
void MNN.Unity.IMNNRealtimeVoiceInputProcessor.Process(float[] monoPcm, int offset, int inputFrames, System.Action<MNN.Unity.MNNRealtimeVadUpdate> onUpdate, bool discontinuity = false, System.Action<float[], int, MNN.Unity.MNNRealtimeVadUpdate> onFrame = null)
```

```csharp
void MNN.Unity.IMNNRealtimeVoiceInputProcessor.Reset()
```

```csharp
public string MNN.Unity.MNNAcousticCheckReport.Format(System.Func<string, string> translate = null)
```

Optional translator receives complete display strings. Codes and filesystem paths stay unchanged.

```csharp
public override string MNN.Unity.MNNAcousticCheckReport.ToString()
```

```csharp
public MNN.Unity.MNNAcousticCheckSeverity.MNNAcousticCheckSeverity()
```

```csharp
public MNN.Unity.MNNAcousticFrontend.MNNAcousticFrontend(MNN.Unity.MNNAcousticSettings settings, MNN.Unity.IMNNAcousticModelFactory factory = null)
```

```csharp
public bool MNN.Unity.MNNAcousticFrontend.CanReloadWake(MNN.Unity.MNNAcousticSettings settings)
```

```csharp
public MNN.Unity.IMNNRealtimeVoiceInputProcessor MNN.Unity.MNNAcousticFrontend.CreateInput(MNN.Unity.MNNPcmFormat format, int capacity, MNN.Unity.IMNNRealtimeVoiceActivity vad)
```

```csharp
public void MNN.Unity.MNNAcousticFrontend.DiscardWake()
```

```csharp
public void MNN.Unity.MNNAcousticFrontend.Dispose()
```

```csharp
public void MNN.Unity.MNNAcousticFrontend.Load(string root, System.Threading.CancellationToken token)
```

```csharp
public static float[] MNN.Unity.MNNAcousticFrontend.Normalize(float[] samples)
```

```csharp
public System.Threading.Tasks.Task<float[]> MNN.Unity.MNNAcousticFrontend.PrepareVoiceAsync(float[] samples, System.Threading.CancellationToken token)
```

```csharp
public System.Threading.Tasks.Task MNN.Unity.MNNAcousticFrontend.ReleaseAsync()
```

Waits off the calling thread for utterance work, then releases model owners. Capture producers and workers must already have stopped.

```csharp
public System.Threading.Tasks.Task MNN.Unity.MNNAcousticFrontend.ReloadWakeAsync(string root, MNN.Unity.MNNAcousticSettings settings, System.Threading.CancellationToken token)
```

```csharp
public void MNN.Unity.MNNAcousticFrontend.ResetIdentity()
```

```csharp
public float[] MNN.Unity.MNNAcousticFrontend.TakeWakeAudio()
```

```csharp
public MNN.Unity.MNNConversationWakeHandoff MNN.Unity.MNNAcousticFrontend.TakeWakeHandoff()
```

```csharp
public bool MNN.Unity.MNNAcousticFrontend.TakeWakeRequest()
```

```csharp
public MNN.Unity.MNNAcousticInputProcessor.MNNAcousticInputProcessor(MNN.Unity.MNNPcmFormat format, int capacity, MNN.Unity.IMNNRealtimeVoiceActivity vad, System.Action<float[], int> enhance, System.Action reset, System.Action<float[], int, float[]> observe, bool enhanceRecognition = true, bool enhanceVoiceActivity = true)
```

```csharp
public bool MNN.Unity.MNNAcousticInputProcessor.Complete(System.Action<MNN.Unity.MNNRealtimeVadUpdate> update, System.Action<float[], int, MNN.Unity.MNNRealtimeVadUpdate> onFrame = null)
```

Drains resampling and partial input, followed by one enhancement frame for its delayed tail. Completion is idempotent on success; failure requires Reset.

```csharp
public void MNN.Unity.MNNAcousticInputProcessor.Process(float[] pcm, int offset, int frames, System.Action<MNN.Unity.MNNRealtimeVadUpdate> update, bool discontinuity = false, System.Action<float[], int, MNN.Unity.MNNRealtimeVadUpdate> onFrame = null)
```

```csharp
public void MNN.Unity.MNNAcousticInputProcessor.Reset()
```

```csharp
public MNN.Unity.MNNAcousticMnnModelFactory.MNNAcousticMnnModelFactory()
```

```csharp
public MNN.Unity.IMNNAcousticEnhancer MNN.Unity.MNNAcousticMnnModelFactory.CreateEnhancer(string directory)
```

```csharp
public MNN.Unity.IMNNAcousticKeywordModel MNN.Unity.MNNAcousticMnnModelFactory.CreateKeyword(string directory)
```

```csharp
public MNN.Unity.IMNNAcousticSeparator MNN.Unity.MNNAcousticMnnModelFactory.CreateSeparator(string directory)
```

```csharp
public MNN.Unity.IMNNAcousticSpeakerEncoder MNN.Unity.MNNAcousticMnnModelFactory.CreateSpeaker(string directory)
```

```csharp
public static string[] MNN.Unity.MNNAcousticModelFiles.Denoiser(string directory)
```

```csharp
public static string[] MNN.Unity.MNNAcousticModelFiles.Separation(string directory)
```

```csharp
public static string[] MNN.Unity.MNNAcousticModelFiles.Speaker(string directory)
```

```csharp
public static string[] MNN.Unity.MNNAcousticModelFiles.Vad(string directory)
```

```csharp
public static string[] MNN.Unity.MNNAcousticModelFiles.Wake(string directory)
```

```csharp
public static void MNN.Unity.MNNAcousticModelResources.DisposeAll(params System.IDisposable[] models)
```

```csharp
public static string MNN.Unity.MNNAcousticModelResources.Resolve(string root, string path)
```

```csharp
public static string MNN.Unity.MNNAcousticModelResources.ResolvePath(string root, string path)
```

```csharp
public MNN.Unity.MNNAcousticNoiseReduction.MNNAcousticNoiseReduction(MNN.Unity.IMNNAcousticModelFactory factory = null)
```

```csharp
public void MNN.Unity.MNNAcousticNoiseReduction.Dispose()
```

```csharp
public void MNN.Unity.MNNAcousticNoiseReduction.Enhance(float[] samples, int offset)
```

```csharp
public float[] MNN.Unity.MNNAcousticNoiseReduction.EnhanceUtterance(float[] samples, System.Threading.CancellationToken token)
```

```csharp
public void MNN.Unity.MNNAcousticNoiseReduction.Load(string root, string directory)
```

```csharp
public void MNN.Unity.MNNAcousticNoiseReduction.Observe(int samples)
```

```csharp
public void MNN.Unity.MNNAcousticNoiseReduction.Reset()
```

```csharp
public static MNN.Unity.MNNAcousticCheckReport MNN.Unity.MNNAcousticPreflight.Check(MNN.Unity.MNNAcousticSettings settings, string modelsDirectory)
```

Returns all active-stage issues. No downloads, preference changes, graph loading or file writes.

```csharp
public MNN.Unity.MNNAcousticProfile.MNNAcousticProfile()
```

```csharp
public MNN.Unity.MNNAcousticSettings.MNNAcousticSettings()
```

```csharp
public MNN.Unity.MNNAcousticSettings MNN.Unity.MNNAcousticSettings.Snapshot()
```

```csharp
public void MNN.Unity.MNNAcousticSettings.Validate()
```

```csharp
public MNN.Unity.MNNAcousticSpeakerSelection.MNNAcousticSpeakerSelection(MNN.Unity.MNNAcousticSettings settings, MNN.Unity.IMNNAcousticModelFactory factory = null)
```

```csharp
public void MNN.Unity.MNNAcousticSpeakerSelection.Dispose()
```

```csharp
public void MNN.Unity.MNNAcousticSpeakerSelection.Load(string root, System.Threading.CancellationToken token)
```

```csharp
public static float[] MNN.Unity.MNNAcousticSpeakerSelection.Normalize(float[] samples)
```

```csharp
public System.Threading.Tasks.Task<float[]> MNN.Unity.MNNAcousticSpeakerSelection.PrepareVoiceAsync(float[] samples, System.Threading.CancellationToken token)
```

Noncausal separation belongs to the utterance worker, never the capture worker. Null means that no candidate was sufficiently clear and unambiguous.

```csharp
public void MNN.Unity.MNNAcousticSpeakerSelection.ResetIdentity()
```

```csharp
public MNN.Unity.MNNAdaptiveEchoFilter.MNNAdaptiveEchoFilter()
```

```csharp
public float MNN.Unity.MNNAdaptiveEchoFilter.EstimateCoherence(float[] microphone, float[] reference, int maximumLag)
```

Measures squared coherence across causal lags from zero through maximumLag (at most TapCount).

```csharp
public void MNN.Unity.MNNAdaptiveEchoFilter.Process(float[] microphone, float[] reference, float[] residual, float adaptation = 1)
```

Writes one residual per input sample with adaptation in [0,1]. Output must not alias either input. Validate before advancing filter state; processing reuses all storage.

```csharp
public void MNN.Unity.MNNAdaptiveEchoFilter.Reset()
```

```csharp
public MNN.Unity.MNNAecDelayCalibration.MNNAecDelayCalibration()
```

```csharp
public bool MNN.Unity.MNNAecDelayCalibration.Add(int milliseconds, long captureFrame, int generation, double now, out int delay)
```

```csharp
public void MNN.Unity.MNNAecDelayCalibration.Reset()
```

```csharp
public MNN.Unity.MNNAudioAnalysis.MNNAudioAnalysis()
```

```csharp
public MNN.Unity.MNNAudioFeatures MNN.Unity.MNNAudioAnalysis.Analyze(float[] samples, int count, int channels, int sampleRate)
```

Measure interleaved normalized float PCM, downmixed to mono for analysis.

```csharp
public MNN.Unity.MNNAudioFeatures MNN.Unity.MNNAudioAnalysis.Analyze(float[] samples, int offset, int count, int channels, int sampleRate)
```

Measure a sample range. Offset/count are scalar samples, not channel frames.

```csharp
public void MNN.Unity.MNNAudioAnalysis.Clear()
```

```csharp
public MNN.Unity.MNNAudioCaptureBuffer.MNNAudioCaptureBuffer(int capacity, int maximumSamples)
```

```csharp
public bool MNN.Unity.MNNAudioCaptureBuffer.TryRead(float[] destination, int offset, out MNN.Unity.MNNAudioFrameInfo info, out int count)
```

The processing consumer supplies reusable storage; never returns internal pooled arrays.

```csharp
public bool MNN.Unity.MNNAudioCaptureBuffer.TryWrite(float[] samples, int offset, int count, MNN.Unity.MNNAudioFrameInfo info)
```

Copies input; the caller retains its array. No allocation or waiting for capacity.

```csharp
public MNN.Unity.MNNAudioEnvelope.MNNAudioEnvelope(float attackSeconds = 0.065, float releaseSeconds = 0.18)
```

```csharp
public static float MNN.Unity.MNNAudioEnvelope.Measure(float rms, float peak, float sensitivity = 2)
```

Map normalized RMS/peak to speech loudness. Values at or below RMS .002 are gated.

```csharp
public void MNN.Unity.MNNAudioEnvelope.Reset()
```

```csharp
public static void MNN.Unity.MNNAudioEnvelope.Smooth(ref float value, ref float velocity, float target, float duration, float deltaTime)
```

Advance an externally owned value/velocity pair with the same exact damped response.

```csharp
public float MNN.Unity.MNNAudioEnvelope.Update(float target, float deltaTime)
```

```csharp
public MNN.Unity.MNNAudioFeatures.MNNAudioFeatures()
```

```csharp
public MNN.Unity.MNNAudioFeatures.MNNAudioFeatures(float rms, float peak, float lowBand = 0, float highBand = 0, float pitchHz = 0, float pitchConfidence = 0, float onset = 0)
```

```csharp
public float MNN.Unity.MNNAudioFeatures.GetEnvelope(float sensitivity = 2)
```

Perceptual speech level in [0,1], retaining consonant peaks and loud-syllable headroom.

```csharp
public MNN.Unity.MNNAudioFeatures MNN.Unity.MNNAudioFeatures.WithGain(float gain)
```

```csharp
public MNN.Unity.MNNAudioFrameInfo.MNNAudioFrameInfo()
```

```csharp
public MNN.Unity.MNNAudioFrameInfo.MNNAudioFrameInfo(long streamId, long formatEpoch, long sequence, long startFrame, long monotonicTicks, MNN.Unity.MNNPcmFormat format, bool discontinuity = false)
```

```csharp
public static bool MNN.Unity.MNNAudioMixtureContribution.TryEstimate(float[] mixture, float[] first, float[] second, out float firstContribution, out float secondContribution)
```

Measures two source energy fractions without allocating. Inputs use the same sample clock. Returns false and zero fractions for silence, correlated candidates or insufficient explained energy. These fractions are evidence for source selection, not calibrated speech probabilities.

```csharp
public static string[] MNN.Unity.MNNAudioProfileFiles.Graphs(string directory, string profile)
```

```csharp
public MNN.Unity.MNNAudioReferenceTimeline.MNNAudioReferenceTimeline(int capacitySamples = 32768)
```

```csharp
public bool MNN.Unity.MNNAudioReferenceTimeline.Read(long firstSample, float[] destination)
```

Copies the complete requested window, or clears destination and returns false if unavailable.

```csharp
public void MNN.Unity.MNNAudioReferenceTimeline.Reset()
```

```csharp
public void MNN.Unity.MNNAudioReferenceTimeline.Write(long firstSample, float[] samples, int count)
```

Copies samples already validated by the capture/resampling boundary. Zero count is a no-op.

```csharp
public MNN.Unity.MNNAutomaticSpeakerSelector.MNNAutomaticSpeakerSelector(float minimum, float margin)
```

```csharp
public void MNN.Unity.MNNAutomaticSpeakerSelector.Reset()
```

```csharp
public int MNN.Unity.MNNAutomaticSpeakerSelector.Select(float[] contribution, float[] similarity, double seconds)
```

```csharp
public MNN.Unity.MNNAvConvTasNetConfig.MNNAvConvTasNetConfig()
```

```csharp
public static MNN.Unity.MNNAvConvTasNetConfig MNN.Unity.MNNAvConvTasNetConfig.Read(string directory)
```

```csharp
public void MNN.Unity.MNNAvConvTasNetExtraction.Dispose()
```

```csharp
public float[] MNN.Unity.MNNAvConvTasNetExtraction.Extract(float[] samples, float[] visualFrames, int videoFrameCount, int sampleRate = 16000, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken))
```

Extract the visible target from mono PCM and normalized grayscale 112x112 target crops at 25 FPS. Frames start at the beginning of the recording. Accepts 40 ms–6 s at 8–96 kHz and restores the input sample count.

```csharp
public float[] MNN.Unity.MNNAvConvTasNetExtraction.ExtractWaveFile(string path, float[] visualFrames, int videoFrameCount, out int sampleRate, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken))
```

```csharp
public static MNN.Unity.MNNAvConvTasNetExtraction MNN.Unity.MNNAvConvTasNetExtraction.Load(string directory, int threadCount = 4, MNN.Unity.MNNBackendType backendType = MNN.Unity.MNNBackendType.Auto)
```

```csharp
public MNN.Unity.MNNAvDprnnConfig.MNNAvDprnnConfig()
```

```csharp
public static MNN.Unity.MNNAvDprnnConfig MNN.Unity.MNNAvDprnnConfig.Read(string directory)
```

```csharp
public void MNN.Unity.MNNAvDprnnExtraction.Dispose()
```

```csharp
public float[] MNN.Unity.MNNAvDprnnExtraction.Extract(float[] samples, float[] visualFrames, int videoFrameCount, int sampleRate = 16000, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken))
```

Extract the visible target from mono PCM and normalized grayscale 112x112 target crops at 25 FPS. Frames start at the beginning of the recording. Accepts 40 ms–6 s at 8–96 kHz and restores the input sample count.

```csharp
public float[] MNN.Unity.MNNAvDprnnExtraction.ExtractWaveFile(string path, float[] visualFrames, int videoFrameCount, out int sampleRate, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken))
```

```csharp
public static MNN.Unity.MNNAvDprnnExtraction MNN.Unity.MNNAvDprnnExtraction.Load(string directory, int threadCount = 4, MNN.Unity.MNNBackendType backendType = MNN.Unity.MNNBackendType.Auto)
```

```csharp
public void MNN.Unity.MNNAvMossFormer2Tse.Dispose()
```

```csharp
public float[] MNN.Unity.MNNAvMossFormer2Tse.Extract(float[] audio, float[] visualFrames, int videoFrameCount, int sampleRate = 16000, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken))
```

Extracts the visible speaker. Visual frames are grayscale, normalized to the ClearerVoice range, and flattened as frame-major [frame,112,112] samples.

```csharp
public System.Threading.Tasks.Task<float[]> MNN.Unity.MNNAvMossFormer2Tse.ExtractAsync(float[] audio, float[] visualFrames, int videoFrameCount, int sampleRate = 16000, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken))
```

```csharp
public static MNN.Unity.MNNAvMossFormer2Tse MNN.Unity.MNNAvMossFormer2Tse.Load(string directory, int threadCount = 4, MNN.Unity.MNNBackendType backendType = MNN.Unity.MNNBackendType.Auto)
```

```csharp
public MNN.Unity.MNNAvMossFormer2TseConfig.MNNAvMossFormer2TseConfig()
```

```csharp
public static MNN.Unity.MNNAvMossFormer2TseConfig MNN.Unity.MNNAvMossFormer2TseConfig.Read(string directory)
```

```csharp
public static float[] MNN.Unity.MNNAvMossFormer2TseVisual.PrepareFrame(MNN.Unity.MNNRgbImage crop)
```

```csharp
public MNN.Unity.MNNBargeInAction.MNNBargeInAction()
```

```csharp
public MNN.Unity.MNNBargeInPolicy.MNNBargeInPolicy(int confirmationMilliseconds = 96, int releaseMilliseconds = 64, float minimumConfidence = 0.65)
```

```csharp
public MNN.Unity.MNNBargeInAction MNN.Unity.MNNBargeInPolicy.Process(MNN.Unity.MNNRealtimeVadUpdate vad, bool postAecNearEnd, float nearEndConfidence, bool assistantAudible, int frameMilliseconds, bool explicitInterrupt = false)
```

```csharp
public void MNN.Unity.MNNBargeInPolicy.Reset()
```

```csharp
public static float[] MNN.Unity.MNNClearerVoiceFaceFrames.PrepareFrame(MNN.Unity.MNNRgbImage crop)
```

Accepts a 112x112 model crop or a 224x224 aligned face (center 112x112). RGB rows run from top to bottom; output uses OpenCV grayscale and (gray/255-0.4161)/0.1688.

```csharp
public MNN.Unity.MNNConvTasNetConfig.MNNConvTasNetConfig()
```

```csharp
public static MNN.Unity.MNNConvTasNetConfig MNN.Unity.MNNConvTasNetConfig.Read(string directory)
```

```csharp
public void MNN.Unity.MNNConvTasNetSeparation.Dispose()
```

```csharp
public static MNN.Unity.MNNConvTasNetSeparation MNN.Unity.MNNConvTasNetSeparation.Load(string directory, int threadCount = 4, MNN.Unity.MNNBackendType backendType = MNN.Unity.MNNBackendType.Auto)
```

```csharp
public MNN.Unity.MNNSeparatedAudio MNN.Unity.MNNConvTasNetSeparation.Separate(float[] samples, int sampleRate = 8000, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken))
```

Separate 16–80000 samples at 8 kHz, or equivalent recordings at 8–96 kHz. Output amplitude/order follows the original forward_wav; speaker slots have no persistent identity.

```csharp
public MNN.Unity.MNNSeparatedAudio MNN.Unity.MNNConvTasNetSeparation.SeparateWaveFile(string path, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken))
```

```csharp
public string MNN.Unity.MNNCueLightAnalysis.ToTsv()
```

```csharp
public MNN.Unity.MNNCueLightConfig.MNNCueLightConfig()
```

```csharp
public MNN.Unity.MNNCueLightAnalysis MNN.Unity.MNNCueLightTurnDetector.Analyze(float[] samples, int sampleRate = 16000, bool resetOnTurnStart = true, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken))
```

Analyze 1–960000 finite 16 kHz mono samples with fresh state. The last frame is zero padded.

```csharp
public MNN.Unity.MNNCueLightAnalysis MNN.Unity.MNNCueLightTurnDetector.AnalyzeWaveFile(string path, bool resetOnTurnStart = true, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken))
```

```csharp
public void MNN.Unity.MNNCueLightTurnDetector.Dispose()
```

```csharp
public static MNN.Unity.MNNCueLightTurnDetector MNN.Unity.MNNCueLightTurnDetector.Load(string directory, int threadCount = 1, MNN.Unity.MNNBackendType backendType = MNN.Unity.MNNBackendType.Auto)
```

```csharp
public MNN.Unity.MNNCueLightFrame MNN.Unity.MNNCueLightTurnDetector.Push(float[] chunk, bool resetOnTurnStart = true, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken))
```

Consume exactly 320 finite mono samples at 16 kHz. State persists until Reset or Analyze.

```csharp
public void MNN.Unity.MNNCueLightTurnDetector.Reset()
```

```csharp
public MNN.Unity.MNNDccrnConfig.MNNDccrnConfig()
```

```csharp
public static MNN.Unity.MNNDccrnConfig MNN.Unity.MNNDccrnConfig.Read(string directory)
```

```csharp
public void MNN.Unity.MNNDccrnEnhancement.Dispose()
```

```csharp
public float[] MNN.Unity.MNNDccrnEnhancement.Enhance(float[] samples, int sampleRate = 16000, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken))
```

Enhance up to three seconds of finite mono PCM at 8–96 kHz. Preserve amplitude, length and sample rate.

```csharp
public float[] MNN.Unity.MNNDccrnEnhancement.EnhanceWaveFile(string path, out int sampleRate, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken))
```

```csharp
public static MNN.Unity.MNNDccrnEnhancement MNN.Unity.MNNDccrnEnhancement.Load(string directory, int threadCount = 4, MNN.Unity.MNNBackendType backendType = MNN.Unity.MNNBackendType.Auto)
```

```csharp
public MNN.Unity.MNNDccrnetConfig.MNNDccrnetConfig()
```

```csharp
public static MNN.Unity.MNNDccrnetConfig MNN.Unity.MNNDccrnetConfig.Read(string directory)
```

```csharp
public void MNN.Unity.MNNDccrnetEnhancement.Dispose()
```

```csharp
public float[] MNN.Unity.MNNDccrnetEnhancement.Enhance(float[] samples, int sampleRate = 16000, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken))
```

Enhance 1000–48000 samples at 16 kHz. Preserve original forward_wav amplitude.

```csharp
public float[] MNN.Unity.MNNDccrnetEnhancement.EnhanceWaveFile(string path, out int sampleRate, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken))
```

```csharp
public static MNN.Unity.MNNDccrnetEnhancement MNN.Unity.MNNDccrnetEnhancement.Load(string directory, int threadCount = 4, MNN.Unity.MNNBackendType backendType = MNN.Unity.MNNBackendType.Auto)
```

```csharp
public MNN.Unity.MNNDcunetConfig.MNNDcunetConfig()
```

```csharp
public static MNN.Unity.MNNDcunetConfig MNN.Unity.MNNDcunetConfig.Read(string directory)
```

```csharp
public void MNN.Unity.MNNDcunetEnhancement.Dispose()
```

```csharp
public float[] MNN.Unity.MNNDcunetEnhancement.Enhance(float[] samples, int sampleRate = 16000, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken))
```

Enhance 1024–48000 samples at 16 kHz. Preserve original forward_wav amplitude.

```csharp
public float[] MNN.Unity.MNNDcunetEnhancement.EnhanceWaveFile(string path, out int sampleRate, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken))
```

```csharp
public static MNN.Unity.MNNDcunetEnhancement MNN.Unity.MNNDcunetEnhancement.Load(string directory, int threadCount = 4, MNN.Unity.MNNBackendType backendType = MNN.Unity.MNNBackendType.Auto)
```

```csharp
public void MNN.Unity.MNNDeepFilterNet3.Dispose()
```

```csharp
public float[] MNN.Unity.MNNDeepFilterNet3.Enhance(float[] samples, int sampleRate = 48000, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken))
```

```csharp
public System.Threading.Tasks.Task<float[]> MNN.Unity.MNNDeepFilterNet3.EnhanceAsync(float[] samples, int sampleRate = 48000, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken))
```

```csharp
public float[] MNN.Unity.MNNDeepFilterNet3.EnhanceWaveFile(string path, out int sampleRate, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken))
```

```csharp
public static MNN.Unity.MNNDeepFilterNet3 MNN.Unity.MNNDeepFilterNet3.Load(string directory, int threadCount = 4, MNN.Unity.MNNBackendType backendType = MNN.Unity.MNNBackendType.CPU)
```

```csharp
public MNN.Unity.MNNDeepFilterNet3Config.MNNDeepFilterNet3Config()
```

```csharp
public static MNN.Unity.MNNDeepFilterNet3Config MNN.Unity.MNNDeepFilterNet3Config.Read(string directory)
```

```csharp
public MNN.Unity.MNNDprnnConfig.MNNDprnnConfig()
```

```csharp
public static MNN.Unity.MNNDprnnConfig MNN.Unity.MNNDprnnConfig.Read(string directory)
```

```csharp
public void MNN.Unity.MNNDprnnSeparation.Dispose()
```

```csharp
public static MNN.Unity.MNNDprnnSeparation MNN.Unity.MNNDprnnSeparation.Load(string directory, int threadCount = 4, MNN.Unity.MNNBackendType backendType = MNN.Unity.MNNBackendType.Auto)
```

```csharp
public MNN.Unity.MNNSeparatedAudio MNN.Unity.MNNDprnnSeparation.Separate(float[] samples, int sampleRate = 8000, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken))
```

Separate 2–32000 samples at 8 kHz, or equivalent recordings at 8–96 kHz. Output amplitude/order follows the original forward_wav; speaker slots have no persistent identity.

```csharp
public MNN.Unity.MNNSeparatedAudio MNN.Unity.MNNDprnnSeparation.SeparateWaveFile(string path, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken))
```

```csharp
public MNN.Unity.MNNDptnetConfig.MNNDptnetConfig()
```

```csharp
public static MNN.Unity.MNNDptnetConfig MNN.Unity.MNNDptnetConfig.Read(string directory)
```

```csharp
public void MNN.Unity.MNNDptnetEnhancement.Dispose()
```

```csharp
public float[] MNN.Unity.MNNDptnetEnhancement.Enhance(float[] samples, int sampleRate = 16000, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken))
```

Enhance 16–48000 samples at 16 kHz. Preserve original forward_wav amplitude.

```csharp
public float[] MNN.Unity.MNNDptnetEnhancement.EnhanceWaveFile(string path, out int sampleRate, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken))
```

```csharp
public static MNN.Unity.MNNDptnetEnhancement MNN.Unity.MNNDptnetEnhancement.Load(string directory, int threadCount = 4, MNN.Unity.MNNBackendType backendType = MNN.Unity.MNNBackendType.Auto)
```

```csharp
public void MNN.Unity.MNNDtln.Dispose()
```

```csharp
public float[] MNN.Unity.MNNDtln.Enhance(float[] samples, int sampleRate = 16000, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken))
```

Enhance up to ten minutes of mono PCM; resets before and after each call. Flushes three hops and removes the streaming delay, preserving input rate and length.

```csharp
public System.Threading.Tasks.Task<float[]> MNN.Unity.MNNDtln.EnhanceAsync(float[] samples, int sampleRate = 16000, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken))
```

```csharp
public float[] MNN.Unity.MNNDtln.EnhanceWaveFile(string path, out int sampleRate, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken))
```

```csharp
public static MNN.Unity.MNNDtln MNN.Unity.MNNDtln.Load(string directory, int threadCount = 1, MNN.Unity.MNNBackendType backendType = MNN.Unity.MNNBackendType.Auto)
```

Auto uses CPU/High for short recurrent hops. Explicit GPU uses compact weights/High. Backend properties report each session; individual operators may use MNN's CPU fallback.

```csharp
public void MNN.Unity.MNNDtln.ProcessHop(float[] input, float[] output, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken))
```

Consume exactly 128 normalized mono samples. Output retains the 384-sample delay. Feed three zero hops to drain the synthesis tail. Invoke on a worker, not an audio callback.

```csharp
public void MNN.Unity.MNNDtln.Reset()
```

Clear both recurrent states and DSP history between unrelated streams.

```csharp
public void MNN.Unity.MNNDtlnAec.Dispose()
```

```csharp
public static MNN.Unity.MNNDtlnAec MNN.Unity.MNNDtlnAec.Load(string directory, int threadCount = 0, MNN.Unity.MNNBackendType backendType = MNN.Unity.MNNBackendType.Auto)
```

Auto uses CPU/High. Thread count0 selects one thread for128/256 and two for512. Verified GPU profiles retain source normalization on CPU and run recurrent/decoder work on GPU.

```csharp
public float[] MNN.Unity.MNNDtlnAec.Process(float[] microphone, float[] playbackReference, int sampleRate = 16000, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken))
```

Process a complete pair of aligned mono recordings, at exactly 16 kHz and up to ten minutes. Resets before/after processing; keeps input length and the upstream 24 ms output delay. No resampling, reference-delay estimation, gain normalization or clipping is applied.

```csharp
public System.Threading.Tasks.Task<float[]> MNN.Unity.MNNDtlnAec.ProcessAsync(float[] microphone, float[] playbackReference, int sampleRate = 16000, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken))
```

```csharp
public void MNN.Unity.MNNDtlnAec.ProcessHop(float[] microphone, float[] playbackReference, float[] output, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken))
```

Process exactly 128 normalized samples per input into a caller-owned 128-sample buffer. Output retains the upstream 384-sample (24 ms) delay. Calls are serialized; invoke on a worker, not an audio callback.

```csharp
public float[] MNN.Unity.MNNDtlnAec.ProcessWaveFiles(string microphonePath, string playbackReferencePath, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken))
```

```csharp
public void MNN.Unity.MNNDtlnAec.Reset()
```

Clear buffers and both recurrent states between unrelated streams.

```csharp
public static bool MNN.Unity.MNNDtlnAec.SupportsRepository(string repository)
```

```csharp
public MNN.Unity.MNNDtlnConfig.MNNDtlnConfig()
```

```csharp
public static MNN.Unity.MNNDtlnConfig MNN.Unity.MNNDtlnConfig.Read(string directory)
```

```csharp
public MNN.Unity.MNNDtlnEchoHopProcessor.MNNDtlnEchoHopProcessor(MNN.Unity.MNNDtlnAec model)
```

```csharp
public void MNN.Unity.MNNDtlnEchoHopProcessor.Dispose()
```

```csharp
public void MNN.Unity.MNNDtlnEchoHopProcessor.ProcessHop(float[] microphone, float[] reference, float[] output)
```

```csharp
public void MNN.Unity.MNNDtlnEchoHopProcessor.Reset()
```

```csharp
public void MNN.Unity.MNNDualTurnEndpointing.Dispose()
```

```csharp
public static MNN.Unity.MNNDualTurnEndpointing MNN.Unity.MNNDualTurnEndpointing.Load(string directory, int threadCount = 4, MNN.Unity.MNNBackendType backendType = MNN.Unity.MNNBackendType.Auto)
```

```csharp
public MNN.Unity.MNNDualTurnEndpointingResult MNN.Unity.MNNDualTurnEndpointing.Push(float[] user, float[] agent = null, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken))
```

CH0 is user; omit agent to feed silence on CH1. PCM gain and signs are preserved.

```csharp
public void MNN.Unity.MNNDualTurnEndpointing.Reset()
```

```csharp
public MNN.Unity.MNNDualTurnEndpointingConfig.MNNDualTurnEndpointingConfig()
```

```csharp
public static MNN.Unity.MNNDualTurnEndpointingConfig MNN.Unity.MNNDualTurnEndpointingConfig.Read(string directory)
```

```csharp
public MNN.Unity.MNNEchoCancellationOptions.MNNEchoCancellationOptions()
```

```csharp
public void MNN.Unity.MNNEchoCancellationOptions.Validate()
```

```csharp
public MNN.Unity.MNNEchoCancellationWorker.MNNEchoCancellationWorker(MNN.Unity.IMNNAudioCaptureSource input, MNN.Unity.IMNNEchoHopProcessor model, int rate, MNN.Unity.MNNEchoCancellationOptions settings, int referenceBlockFrames = 0)
```

```csharp
public void MNN.Unity.MNNEchoCancellationWorker.Dispose()
```

Blocking compatibility entry point for non-UI control threads. Prefer StopAsync in application lifecycles.

```csharp
public System.Threading.Tasks.Task MNN.Unity.MNNEchoCancellationWorker.PausePublicationAsync(System.Threading.CancellationToken token)
```

```csharp
public void MNN.Unity.MNNEchoCancellationWorker.SetPublicationEnabled(bool enabled)
```

```csharp
public System.Threading.Tasks.Task MNN.Unity.MNNEchoCancellationWorker.StopAsync()
```

Gate capture/reference producers before calling. Never waits synchronously for inference. A failed model release retains ownership; the next StopAsync retries it.

```csharp
public void MNN.Unity.MNNEchoCancellationWorker.SubmitReference(System.ReadOnlySpan<float> samples, int channels, long ticks, bool discontinuity = false)
```

```csharp
public void MNN.Unity.MNNEchoCancellationWorker.SubmitReference(float[] samples, int channels, long ticks, bool discontinuity = false)
```

```csharp
public bool MNN.Unity.MNNEchoCancellationWorker.TryReadCapture(float[] destination, int offset, out MNN.Unity.MNNAudioFrameInfo info, out int count)
```

```csharp
public MNN.Unity.MNNEchoDelayEstimator.MNNEchoDelayEstimator()
```

```csharp
public void MNN.Unity.MNNEchoDelayEstimator.Process(float[] microphone, float[] reference)
```

Consumes equally sized finite normalized mono PCM blocks. Invalid input leaves state unchanged.

```csharp
public void MNN.Unity.MNNEchoDelayEstimator.Reset()
```

```csharp
public void MNN.Unity.MNNFrcrnSe.Dispose()
```

```csharp
public float[] MNN.Unity.MNNFrcrnSe.Enhance(float[] samples, int sampleRate = 16000, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken))
```

```csharp
public System.Threading.Tasks.Task<float[]> MNN.Unity.MNNFrcrnSe.EnhanceAsync(float[] samples, int sampleRate = 16000, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken))
```

```csharp
public float[] MNN.Unity.MNNFrcrnSe.EnhanceWaveFile(string path, out int sampleRate, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken))
```

```csharp
public float[] MNN.Unity.MNNFrcrnSe.EnhanceWaveFile(string path, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken))
```

```csharp
public static MNN.Unity.MNNFrcrnSe MNN.Unity.MNNFrcrnSe.Load(string directory, int threadCount = 4, MNN.Unity.MNNBackendType backendType = MNN.Unity.MNNBackendType.Auto)
```

```csharp
public MNN.Unity.MNNFrcrnSeConfig.MNNFrcrnSeConfig()
```

```csharp
public static MNN.Unity.MNNFrcrnSeConfig MNN.Unity.MNNFrcrnSeConfig.Read(string directory)
```

```csharp
public string MNN.Unity.MNNFsEendActivity.ToRttm(string recordingId)
```

Original strict threshold 0.5, zero-padded 11-frame median and 100 ms RTTM grid.

```csharp
public MNN.Unity.MNNFsEendConfig.MNNFsEendConfig()
```

```csharp
public static MNN.Unity.MNNFsEendConfig MNN.Unity.MNNFsEendConfig.Read(string directory)
```

```csharp
public MNN.Unity.MNNFsEendActivity MNN.Unity.MNNFsEendDiarization.Analyze(float[] samples, int sampleRate = 8000, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken))
```

Analyze 1–480000 finite mono PCM samples at exactly 8 kHz. Each complete recording has independent attention context.

```csharp
public MNN.Unity.MNNFsEendActivity MNN.Unity.MNNFsEendDiarization.AnalyzeWaveFile(string path, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken))
```

```csharp
public void MNN.Unity.MNNFsEendDiarization.Dispose()
```

```csharp
public static MNN.Unity.MNNFsEendDiarization MNN.Unity.MNNFsEendDiarization.Load(string directory, int threadCount = 8, MNN.Unity.MNNBackendType backendType = MNN.Unity.MNNBackendType.Auto)
```

```csharp
public MNN.Unity.MNNVoiceActivityResult MNN.Unity.MNNFsmnVad.Detect(float[] samples, int sampleRate, float threshold = 0.6, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken))
```

```csharp
public System.Threading.Tasks.Task<MNN.Unity.MNNVoiceActivityResult> MNN.Unity.MNNFsmnVad.DetectAsync(float[] samples, int sampleRate, float threshold = 0.6, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken))
```

```csharp
public MNN.Unity.MNNVoiceActivityResult MNN.Unity.MNNFsmnVad.DetectWaveFile(string path, float threshold = 0.6, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken))
```

```csharp
public void MNN.Unity.MNNFsmnVad.Dispose()
```

```csharp
public static MNN.Unity.MNNFsmnVad MNN.Unity.MNNFsmnVad.Load(string directory, int threadCount = 1, MNN.Unity.MNNBackendType backendType = MNN.Unity.MNNBackendType.CPU)
```

```csharp
public void MNN.Unity.MNNFsmnVad.Reset()
```

```csharp
public MNN.Unity.MNNFsmnVadConfig.MNNFsmnVadConfig()
```

```csharp
public static MNN.Unity.MNNFsmnVadConfig MNN.Unity.MNNFsmnVadConfig.Read(string directory)
```

```csharp
public void MNN.Unity.MNNFullSubNet.Dispose()
```

```csharp
public float[] MNN.Unity.MNNFullSubNet.Enhance(float[] samples, int sampleRate = 16000, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken))
```

Enhance up to ten seconds using utterance-level normalization and centered periodic-Hann STFT. Each call is an independent recording; output retains the original rate and sample count.

```csharp
public float[] MNN.Unity.MNNFullSubNet.EnhanceWaveFile(string path, out int sampleRate, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken))
```

```csharp
public static MNN.Unity.MNNFullSubNet MNN.Unity.MNNFullSubNet.Load(string directory, int threadCount = 1, MNN.Unity.MNNBackendType backendType = MNN.Unity.MNNBackendType.Auto)
```

```csharp
public float[] MNN.Unity.MNNFullSubNet.PredictMask(float[] magnitude, int frames, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken))
```

Frequency-major magnitude [257,frames] to complex mask [2,257,frames].

```csharp
public MNN.Unity.MNNFullSubNetConfig.MNNFullSubNetConfig()
```

```csharp
public static MNN.Unity.MNNFullSubNetConfig MNN.Unity.MNNFullSubNetConfig.Read(string directory)
```

```csharp
public void MNN.Unity.MNNGtcrn.Dispose()
```

```csharp
public float[] MNN.Unity.MNNGtcrn.Enhance(float[] samples, int sampleRate = 16000, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken))
```

Enhance an entire recording with the official centered sqrt-Hann STFT convention. Resets recurrent caches before/after the recording and restores the original rate/count.

```csharp
public float[] MNN.Unity.MNNGtcrn.EnhanceWaveFile(string path, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken))
```

```csharp
public static MNN.Unity.MNNGtcrn MNN.Unity.MNNGtcrn.Load(string directory, int threadCount = 1, MNN.Unity.MNNBackendType backendType = MNN.Unity.MNNBackendType.Auto)
```

Auto uses CPU for 16 ms recurrent hops; explicit GPUs retain High precision.

```csharp
public void MNN.Unity.MNNGtcrn.ProcessSpectrum(float[] spectrum, float[] enhanced, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken))
```

Consume 257 complex bins, real/imaginary interleaved. Caller owns STFT scheduling. Reuses fixed native tensors and managed state; invoke on a worker, not the audio callback.

```csharp
public void MNN.Unity.MNNGtcrn.Reset()
```

Clear caches between unrelated streams. One instance owns one stream.

```csharp
public MNN.Unity.MNNGtcrnConfig.MNNGtcrnConfig()
```

```csharp
public static MNN.Unity.MNNGtcrnConfig MNN.Unity.MNNGtcrnConfig.Read(string directory)
```

```csharp
public MNN.Unity.MNNKeywordLanguage.MNNKeywordLanguage()
```

```csharp
public MNN.Unity.MNNKeywordDetection[] MNN.Unity.MNNKeywordSpotter.AcceptWaveform(float[] samples, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken))
```

Consume normalized mono PCM at exactly 16 kHz (at most 30 seconds per call). Retains bounded audio/state between calls; matches can span arbitrary chunk boundaries.

```csharp
public static string MNN.Unity.MNNKeywordSpotter.AssetDirectory(string directory)
```

Resolve the installed keyword assets without opening native models.

```csharp
public MNN.Unity.MNNKeywordDetection[] MNN.Unity.MNNKeywordSpotter.Detect(float[] samples, int sampleRate, string keywords, float threshold = 0.15, float score = 1, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken))
```

Detect all keyword occurrences in finite 8–96 kHz mono PCM, up to ten minutes. Resets this instance's stream; threshold and boost apply unless a keyword overrides them.

```csharp
public System.Threading.Tasks.Task<MNN.Unity.MNNKeywordDetection[]> MNN.Unity.MNNKeywordSpotter.DetectAsync(float[] samples, int sampleRate, string keywords, float threshold = 0.15, float score = 1, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken))
```

```csharp
public MNN.Unity.MNNKeywordDetection[] MNN.Unity.MNNKeywordSpotter.DetectWaveFile(string path, string keywords, float threshold = 0.15, float score = 1, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken))
```

```csharp
public void MNN.Unity.MNNKeywordSpotter.Dispose()
```

```csharp
public MNN.Unity.MNNKeywordDetection[] MNN.Unity.MNNKeywordSpotter.InputFinished(System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken))
```

Flush delayed final matches once; Reset starts the next recording.

```csharp
public static MNN.Unity.MNNKeywordSpotter MNN.Unity.MNNKeywordSpotter.Load(string directory, int threadCount = 2)
```

```csharp
public void MNN.Unity.MNNKeywordSpotter.Reset(string keywords, float threshold = 0.15, float score = 1, int trailingBlanks = 0, int maxActivePaths = 0)
```

Set ordinary wake words in the loaded model language, one phrase per line (e.g. Hey Siri or 小爱同学). Automatically prepares model input and resets the stream. Legacy token specifications remain accepted. trailingBlanks=0 and maxActivePaths=0 select language-specific confirmation and beam width; positive values explicitly override them.

```csharp
public MNN.Unity.MNNKeywordSpotterConfig.MNNKeywordSpotterConfig()
```

```csharp
public static MNN.Unity.MNNKeywordSpotterConfig MNN.Unity.MNNKeywordSpotterConfig.Read(string directory)
```

```csharp
public static MNN.Unity.MNNKeywordLanguage MNN.Unity.MNNKeywordText.DetectLanguage(string wakeWords)
```

Find the compatible model language. A phrase must be Chinese or English; lists with both languages should route each line to its corresponding model.

```csharp
public static string MNN.Unity.MNNKeywordText.GetDisplayText(string wakeWords)
```

Plain display text for a saved configuration. Editors can retain the original configuration until this displayed text changes, preserving legacy pronunciation overrides.

```csharp
public static string MNN.Unity.MNNKeywordText.Prepare(string wakeWords, MNN.Unity.MNNKeywordLanguage language = MNN.Unity.MNNKeywordLanguage.Automatic)
```

Prepare ordinary Chinese or English phrases for one model, one per line, with model vocabulary and language checks. Legacy token lines with @labels remain accepted for saved configurations.

```csharp
public void MNN.Unity.MNNLiveKitTurnDetector.Dispose()
```

```csharp
public bool MNN.Unity.MNNLiveKitTurnDetector.IsEndOfTurn(string currentText, float threshold = 0.5, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken))
```

```csharp
public static MNN.Unity.MNNLiveKitTurnDetector MNN.Unity.MNNLiveKitTurnDetector.Load(string directory, int threadCount = 2, MNN.Unity.MNNBackendType backendType = MNN.Unity.MNNBackendType.CPU)
```

```csharp
public float MNN.Unity.MNNLiveKitTurnDetector.Predict(System.Collections.Generic.IReadOnlyList<MNN.Unity.MNNLiveKitTurnMessage> messages, float threshold = 0.5, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken))
```

```csharp
public float MNN.Unity.MNNLiveKitTurnDetector.Predict(string currentText, float threshold = 0.5, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken))
```

```csharp
public MNN.Unity.MNNLiveKitTurnDetectorConfig.MNNLiveKitTurnDetectorConfig()
```

```csharp
public static MNN.Unity.MNNLiveKitTurnDetectorConfig MNN.Unity.MNNLiveKitTurnDetectorConfig.Read(string directory)
```

```csharp
public MNN.Unity.MNNLiveKitTurnMessage.MNNLiveKitTurnMessage(string role, string content)
```

```csharp
public string MNN.Unity.MNNLsEendActivity.ToRttm(string recordingId)
```

Original strict threshold 0.5, zero-padded 11-frame median and 100 ms RTTM grid.

```csharp
public MNN.Unity.MNNLsEendConfig.MNNLsEendConfig()
```

```csharp
public static MNN.Unity.MNNLsEendConfig MNN.Unity.MNNLsEendConfig.Read(string directory)
```

```csharp
public MNN.Unity.MNNLsEendActivity MNN.Unity.MNNLsEendDiarization.Analyze(float[] samples, int sampleRate = 8000, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken))
```

Analyze 1–480000 finite mono PCM samples at exactly 8 kHz, with fresh state per call.

```csharp
public MNN.Unity.MNNLsEendActivity MNN.Unity.MNNLsEendDiarization.AnalyzeWaveFile(string path, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken))
```

```csharp
public void MNN.Unity.MNNLsEendDiarization.Dispose()
```

```csharp
public static MNN.Unity.MNNLsEendDiarization MNN.Unity.MNNLsEendDiarization.Load(string directory, int threadCount = 2, MNN.Unity.MNNBackendType backendType = MNN.Unity.MNNBackendType.Auto)
```

```csharp
public MNN.Unity.MNNVoiceActivityResult MNN.Unity.MNNMarbleNetVad.Detect(float[] samples, int sampleRate, float threshold = 0.5, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken))
```

```csharp
public System.Threading.Tasks.Task<MNN.Unity.MNNVoiceActivityResult> MNN.Unity.MNNMarbleNetVad.DetectAsync(float[] samples, int sampleRate, float threshold = 0.5, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken))
```

```csharp
public MNN.Unity.MNNVoiceActivityResult MNN.Unity.MNNMarbleNetVad.DetectWaveFile(string path, float threshold = 0.5, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken))
```

```csharp
public void MNN.Unity.MNNMarbleNetVad.Dispose()
```

```csharp
public static MNN.Unity.MNNMarbleNetVad MNN.Unity.MNNMarbleNetVad.Load(string directory, int threadCount = 1, MNN.Unity.MNNBackendType backendType = MNN.Unity.MNNBackendType.Auto)
```

```csharp
public MNN.Unity.MNNMarbleNetVadConfig.MNNMarbleNetVadConfig()
```

```csharp
public static MNN.Unity.MNNMarbleNetVadConfig MNN.Unity.MNNMarbleNetVadConfig.Read(string directory)
```

```csharp
public MNN.Unity.MNNMossFormer2Config.MNNMossFormer2Config()
```

```csharp
public static MNN.Unity.MNNMossFormer2Config MNN.Unity.MNNMossFormer2Config.Read(string directory)
```

```csharp
public void MNN.Unity.MNNMossFormer2Se.Dispose()
```

```csharp
public float[] MNN.Unity.MNNMossFormer2Se.Enhance(float[] samples, int sampleRate = 48000, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken))
```

Enhance 40 ms to ten seconds of PCM. Dither uses a private seed reset for each recording.

```csharp
public float[] MNN.Unity.MNNMossFormer2Se.EnhanceWaveFile(string path, out int sampleRate, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken))
```

```csharp
public static MNN.Unity.MNNMossFormer2Se MNN.Unity.MNNMossFormer2Se.Load(string directory, int threadCount = 4, MNN.Unity.MNNBackendType backendType = MNN.Unity.MNNBackendType.Auto)
```

```csharp
public MNN.Unity.MNNMossFormer2SeConfig.MNNMossFormer2SeConfig()
```

```csharp
public static MNN.Unity.MNNMossFormer2SeConfig MNN.Unity.MNNMossFormer2SeConfig.Read(string directory)
```

```csharp
public void MNN.Unity.MNNMossFormer2Separation.Dispose()
```

```csharp
public static MNN.Unity.MNNMossFormer2Separation MNN.Unity.MNNMossFormer2Separation.Load(string directory, int threadCount = 2, MNN.Unity.MNNBackendType backendType = MNN.Unity.MNNBackendType.Auto)
```

```csharp
public MNN.Unity.MNNSeparatedAudio MNN.Unity.MNNMossFormer2Separation.Separate(float[] samples, int sampleRate = 16000, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken))
```

```csharp
public System.Threading.Tasks.Task<MNN.Unity.MNNSeparatedAudio> MNN.Unity.MNNMossFormer2Separation.SeparateAsync(float[] samples, int sampleRate = 16000, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken))
```

```csharp
public MNN.Unity.MNNSeparatedAudio MNN.Unity.MNNMossFormer2Separation.SeparateWaveFile(string path, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken))
```

```csharp
public void MNN.Unity.MNNMossFormerGan.Dispose()
```

```csharp
public float[] MNN.Unity.MNNMossFormerGan.Enhance(float[] samples, int sampleRate = 16000, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken))
```

Enhance an independent recording of 201–160000 samples at 16kHz. Zero-energy PCM returns silence; the upstream normalization is undefined there.

```csharp
public float[] MNN.Unity.MNNMossFormerGan.EnhanceWaveFile(string path, out int sampleRate, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken))
```

```csharp
public static MNN.Unity.MNNMossFormerGan MNN.Unity.MNNMossFormerGan.Load(string directory, int threadCount = 4, MNN.Unity.MNNBackendType backendType = MNN.Unity.MNNBackendType.Auto)
```

```csharp
public MNN.Unity.MNNMossFormerGanConfig.MNNMossFormerGanConfig()
```

```csharp
public static MNN.Unity.MNNMossFormerGanConfig MNN.Unity.MNNMossFormerGanConfig.Read(string directory)
```

```csharp
public void MNN.Unity.MNNNamoTurnDetector.Dispose()
```

```csharp
public bool MNN.Unity.MNNNamoTurnDetector.IsEndOfTurn(string transcript, float threshold = 0.5, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken))
```

```csharp
public static MNN.Unity.MNNNamoTurnDetector MNN.Unity.MNNNamoTurnDetector.Load(string directory, int threadCount = 4, MNN.Unity.MNNBackendType backendType = MNN.Unity.MNNBackendType.Auto)
```

```csharp
public float MNN.Unity.MNNNamoTurnDetector.Predict(string transcript, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken))
```

Returns class-1 softmax probability; truncates content on the right while preserving BOS and EOS.

```csharp
public MNN.Unity.MNNNamoTurnDetectorConfig.MNNNamoTurnDetectorConfig()
```

```csharp
public static MNN.Unity.MNNNamoTurnDetectorConfig MNN.Unity.MNNNamoTurnDetectorConfig.Read(string directory)
```

```csharp
public void MNN.Unity.MNNNoiseSuppressor.Dispose()
```

```csharp
public float[] MNN.Unity.MNNNoiseSuppressor.Enhance(float[] audio, int sampleRate = 16000, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken))
```

Processes bounded three-second blocks, preserving rate and sample count. Each block is an independent utterance; block boundaries can affect enhancement quality. No realtime-throughput claim.

```csharp
public static MNN.Unity.MNNNoiseSuppressor MNN.Unity.MNNNoiseSuppressor.Load(string directory, int threadCount = 2, MNN.Unity.MNNBackendType backendType = MNN.Unity.MNNBackendType.Auto)
```

```csharp
public static string MNN.Unity.MNNNoiseSuppressor.ProfileFile(string directory)
```

```csharp
public MNN.Unity.MNNPcmFormat.MNNPcmFormat()
```

```csharp
public MNN.Unity.MNNPcmFormat.MNNPcmFormat(int sampleRate, int channels)
```

```csharp
public bool MNN.Unity.MNNPcmFormat.Equals(MNN.Unity.MNNPcmFormat other)
```

```csharp
public override bool MNN.Unity.MNNPcmFormat.Equals(object obj)
```

```csharp
public override int MNN.Unity.MNNPcmFormat.GetHashCode()
```

```csharp
public void MNN.Unity.MNNPcmFormat.RequireValid()
```

Reject an uninitialized default PCM format before binding a device or allocating buffers.

```csharp
public int MNN.Unity.MNNPcmFormat.SamplesForFrames(int frames)
```

```csharp
public MNN.Unity.MNNPcmFrameAccumulator.MNNPcmFrameAccumulator(int frameSamples)
```

```csharp
public void MNN.Unity.MNNPcmFrameAccumulator.Append(float[] input, int inputOffset, int inputSamples, float[] output, int outputOffset, int outputCapacityFrames, out int consumedSamples, out int producedFrames)
```

Appends samples and writes complete frames into caller-owned storage. Unconsumed input remains caller-owned. The output array must have room for outputCapacityFrames * FrameSamples samples.

```csharp
public void MNN.Unity.MNNPcmFrameAccumulator.Reset()
```

```csharp
public bool MNN.Unity.MNNPcmFrameAccumulator.TryComplete(float[] output, int outputOffset, out int validSamples)
```

Emits one zero-padded final frame when partial samples remain. Repeated calls return false.

```csharp
public MNN.Unity.MNNPlaybackReceipt.MNNPlaybackReceipt()
```

```csharp
public MNN.Unity.MNNPlaybackWaveform.MNNPlaybackWaveform()
```

```csharp
public void MNN.Unity.MNNPlaybackWaveform.Publish(float[] samples, int count, int channels, MNN.Unity.MNNResponseId response, int sampleRate = 48000)
```

```csharp
public static float MNN.Unity.MNNPlaybackWaveform.Reduce(float[] samples, int count, int channels, float[] destination)
```

Preserve signed peaks in each bin so consonants survive reduction; energy uses all samples.

```csharp
public static float MNN.Unity.MNNPlaybackWaveform.SpeechEnvelope(float rms, float peak, float sensitivity)
```

Perceptual loudness with a silence gate and bounded consonant-peak contribution.

```csharp
public bool MNN.Unity.MNNPlaybackWaveform.TryRead(float[] destination, MNN.Unity.MNNResponseId response, out float rms)
```

```csharp
public bool MNN.Unity.MNNPlaybackWaveform.TryRead(float[] destination, MNN.Unity.MNNResponseId response, out float rms, out MNN.Unity.MNNAudioFeatures features)
```

```csharp
public bool MNN.Unity.MNNPlaybackWaveform.TryReadFeatures(MNN.Unity.MNNResponseId response, out MNN.Unity.MNNAudioFeatures features)
```

```csharp
public void MNN.Unity.MNNProfiledVoiceActivity.Dispose()
```

```csharp
public static MNN.Unity.MNNProfiledVoiceActivity MNN.Unity.MNNProfiledVoiceActivity.Load(string directory, int threadCount = 1, MNN.Unity.MNNBackendType backendType = MNN.Unity.MNNBackendType.Auto)
```

```csharp
public MNN.Unity.MNNRealtimeVadUpdate MNN.Unity.MNNProfiledVoiceActivity.ProcessFrame(float[] samples, int offset)
```

```csharp
public void MNN.Unity.MNNProfiledVoiceActivity.Reset()
```

```csharp
public MNN.Unity.MNNRealtimeAudioCaptureWorker.MNNRealtimeAudioCaptureWorker(MNN.Unity.IMNNAudioCaptureSource source, MNN.Unity.IMNNRealtimeVoiceInputProcessor voiceProcessor, MNN.Unity.MNNRealtimeModelInputBuffer modelInput, System.Action<MNN.Unity.MNNRealtimeVadUpdate> onVadUpdate = null, string threadName = "MNN Audio Processing", System.TimeSpan? maximumCaptureAge = null)
```

Creates the standard capture → resample/VAD → fixed model-frame pipeline.

```csharp
public MNN.Unity.MNNRealtimeAudioCaptureWorker.MNNRealtimeAudioCaptureWorker(MNN.Unity.IMNNAudioCaptureSource source, System.Action<float[], int, MNN.Unity.MNNAudioFrameInfo> process, string threadName = "MNN Audio Processing", System.TimeSpan? maximumCaptureAge = null)
```

Discards blocks whose first-sample timestamp is older than the budget (300 ms by default). Use Timeout.InfiniteTimeSpan only when intentionally draining historical/offline audio.

```csharp
public void MNN.Unity.MNNRealtimeAudioCaptureWorker.Dispose()
```

```csharp
public System.Threading.Tasks.Task MNN.Unity.MNNRealtimeAudioCaptureWorker.FlushAsync()
```

After the producer has paused, drains queued capture and flushes the resampler and partial VAD frame on their owning thread, then resets for the next stream. Completion means all final model frames have been published, not consumed.

```csharp
public void MNN.Unity.MNNRealtimeAudioCaptureWorker.Start()
```

Starts the worker once. Thread creation occurs on the control thread.

```csharp
public bool MNN.Unity.MNNRealtimeAudioCaptureWorker.Stop(System.TimeSpan timeout)
```

Requests a stop and waits only for the caller-supplied bound. Call on a control thread.

```csharp
public MNN.Unity.MNNRealtimeCapturePolicy.MNNRealtimeCapturePolicy()
```

```csharp
public void MNN.Unity.MNNRealtimeCapturePolicy.MarkCueAudible(int tailMilliseconds)
```

```csharp
public void MNN.Unity.MNNRealtimeCapturePolicy.ProtectCaptureFromSounds(bool enabled)
```

```csharp
public void MNN.Unity.MNNRealtimeCapturePolicy.RequestCapture(bool enabled)
```

```csharp
public void MNN.Unity.MNNRealtimeCapturePolicy.Reset()
```

```csharp
public bool MNN.Unity.MNNRealtimeCapturePolicy.ShouldStopCue(bool echoQualified)
```

```csharp
public bool MNN.Unity.MNNRealtimeCapturePolicy.SpeakerAudible(bool renderedSpeechAudible, long queuedSpeechFrames)
```

```csharp
public bool MNN.Unity.MNNRealtimeCapturePolicy.TryBeginCue(bool echoQualified, int tailMilliseconds)
```

```csharp
public MNN.Unity.MNNRealtimeCapturePort.MNNRealtimeCapturePort(int capacity, System.Func<bool> safetyGate = null, int maximumBlockSamples = 1920)
```

```csharp
public MNN.Unity.MNNRealtimeCapturePort.Producer MNN.Unity.MNNRealtimeCapturePort.CreateProducer(System.Action beforeCapture = null)
```

Stop the old producer before creating its replacement. A port has one active producer.

```csharp
public System.Threading.Tasks.Task MNN.Unity.MNNRealtimeCapturePort.PauseCaptureAsync(System.Threading.CancellationToken token)
```

```csharp
public void MNN.Unity.MNNRealtimeCapturePort.Producer.Dispose()
```

```csharp
public void MNN.Unity.MNNRealtimeCapturePort.Producer.Initialize(MNN.Unity.MNNPcmFormat format)
```

```csharp
public void MNN.Unity.MNNRealtimeCapturePort.Producer.Submit(System.ReadOnlySpan<float> buffer, MNN.Unity.MNNPcmFormat format, double estimatedCaptureDelayMilliseconds = 0)
```

```csharp
public bool MNN.Unity.MNNRealtimeCapturePort.TryReadCapture(float[] destination, int offset, out MNN.Unity.MNNAudioFrameInfo info, out int count)
```

```csharp
public MNN.Unity.MNNRealtimeClockSynchronizer.MNNRealtimeClockSynchronizer(MNN.Unity.MNNPcmFormat captureFormat, MNN.Unity.MNNPcmFormat presentationFormat)
```

```csharp
public bool MNN.Unity.MNNRealtimeClockSynchronizer.ObserveCapture(long framePosition, long monotonicTicks)
```

Audio-processing worker only. False rejects a duplicate, regression, or implausible clock jump.

```csharp
public bool MNN.Unity.MNNRealtimeClockSynchronizer.ObservePresentation(long framePosition, long monotonicTicks)
```

Audio-processing worker only. False rejects a duplicate, regression, or implausible clock jump.

```csharp
public void MNN.Unity.MNNRealtimeClockSynchronizer.Reset()
```

```csharp
public void MNN.Unity.MNNRealtimeClockSynchronizer.ResetCapture()
```

Reset after a capture discontinuity without discarding the independent output clock estimate.

```csharp
public void MNN.Unity.MNNRealtimeClockSynchronizer.ResetPresentation()
```

Reset after a presentation device or sample-rate change.

```csharp
public bool MNN.Unity.MNNRealtimeClockSynchronizer.TryMapCaptureToPresentation(long captureFrame, out long presentationFrame)
```

Maps a capture sample position to the corresponding device presentation sample position.

```csharp
public MNN.Unity.MNNRealtimeDemandFeeder.MNNRealtimeDemandFeeder(MNN.Unity.IMNNRealtimeDemandSink sink, int milliseconds)
```

```csharp
public void MNN.Unity.MNNRealtimeDemandFeeder.Dispose()
```

```csharp
public System.Threading.Tasks.Task MNN.Unity.MNNRealtimeDemandFeeder.StopAsync()
```

```csharp
protected MNN.Unity.MNNRealtimeDeviceBinding.MNNRealtimeDeviceBinding()
```

```csharp
public virtual System.IDisposable MNN.Unity.MNNRealtimeDeviceBinding.BindEchoReference(MNN.Unity.MNNEchoCancellationWorker worker)
```

```csharp
public abstract void MNN.Unity.MNNRealtimeDeviceBinding.BindOutput(MNN.Unity.MNNRealtimeOutputPipeline output, MNN.Unity.MNNPcmFormat captureFormat, int capacity)
```

```csharp
public virtual System.Threading.Tasks.Task MNN.Unity.MNNRealtimeDeviceBinding.FadeOutAsync()
```

```csharp
public virtual System.Threading.Tasks.Task MNN.Unity.MNNRealtimeDeviceBinding.FlushCaptureAsync(System.Threading.CancellationToken token)
```

```csharp
public virtual void MNN.Unity.MNNRealtimeDeviceBinding.OpenPlayback()
```

```csharp
public abstract MNN.Unity.MNNRealtimeDeviceSnapshot MNN.Unity.MNNRealtimeDeviceBinding.ReadState()
```

```csharp
public virtual System.Threading.Tasks.Task MNN.Unity.MNNRealtimeDeviceBinding.RequestPermissionAsync(System.Threading.CancellationToken token)
```

```csharp
public abstract void MNN.Unity.MNNRealtimeDeviceBinding.ResetForRetry()
```

```csharp
public abstract void MNN.Unity.MNNRealtimeDeviceBinding.SetCaptureEnabled(bool enabled)
```

```csharp
public virtual void MNN.Unity.MNNRealtimeDeviceBinding.SetPresentationTail(double milliseconds)
```

```csharp
public virtual void MNN.Unity.MNNRealtimeDeviceBinding.SetSpeechVolume(float volume)
```

```csharp
public abstract void MNN.Unity.MNNRealtimeDeviceBinding.StartCapture(System.Action<MNN.Unity.MNNPcmFormat, int> negotiated)
```

```csharp
public abstract void MNN.Unity.MNNRealtimeDeviceBinding.StartPlayback()
```

```csharp
public abstract void MNN.Unity.MNNRealtimeDeviceBinding.StopCapture()
```

```csharp
public abstract void MNN.Unity.MNNRealtimeDeviceBinding.StopPlayback()
```

```csharp
public virtual void MNN.Unity.MNNRealtimeDeviceBinding.Supervise()
```

Control-thread backend diagnostics only. Never called by PCM callbacks.

```csharp
public abstract void MNN.Unity.MNNRealtimeDeviceBinding.ValidateFresh()
```

```csharp
public MNN.Unity.MNNRealtimeDeviceSession.MNNRealtimeDeviceSession(MNN.Unity.MNNRealtimeDeviceBinding binding, MNN.Unity.MNNRealtimeDeviceSessionOptions options = null, MNN.Unity.IMNNRealtimeDeviceClock clock = null, System.Func<MNN.Unity.IMNNAudioCaptureSource, int, int, MNN.Unity.MNNEchoCancellationWorker> echoFactory = null)
```

```csharp
public void MNN.Unity.MNNRealtimeDeviceSession.Close()
```

Gate callbacks and stop both physical endpoints immediately. Model work joins asynchronously. Every owner is attempted; failed owners remain available for a later CloseAsync retry.

```csharp
public System.Threading.Tasks.Task MNN.Unity.MNNRealtimeDeviceSession.CloseAsync()
```

```csharp
public void MNN.Unity.MNNRealtimeDeviceSession.Configure(MNN.Unity.MNNRealtimeDeviceSessionOptions options)
```

Reconfigure only after ResetForRetry. Existing device/model ownership is never overwritten.

```csharp
public System.Threading.Tasks.Task MNN.Unity.MNNRealtimeDeviceSession.FadeOutAsync()
```

```csharp
public string MNN.Unity.MNNRealtimeDeviceSession.GetRouteFailure()
```

```csharp
public System.Threading.Tasks.Task<MNN.Unity.MNNRealtimeOutputPipeline> MNN.Unity.MNNRealtimeDeviceSession.OpenAsync(System.Threading.CancellationToken token, System.Func<System.Threading.CancellationToken, System.Threading.Tasks.Task> prepareModels = null)
```

```csharp
public System.Threading.Tasks.Task MNN.Unity.MNNRealtimeDeviceSession.PauseCaptureAsync(System.Threading.CancellationToken token)
```

```csharp
public void MNN.Unity.MNNRealtimeDeviceSession.RecordFault(System.Exception error)
```

```csharp
public void MNN.Unity.MNNRealtimeDeviceSession.ResetForRetry()
```

```csharp
public void MNN.Unity.MNNRealtimeDeviceSession.SetCaptureEnabled(bool enabled)
```

```csharp
public void MNN.Unity.MNNRealtimeDeviceSession.SetSpeechVolume(float volume)
```

```csharp
public void MNN.Unity.MNNRealtimeDeviceSession.StartEchoProcessing()
```

Starts optional worker-owned echo inference after capture and output formats are negotiated.

```csharp
public void MNN.Unity.MNNRealtimeDeviceSession.Tick()
```

Control-thread update. Invalid/inaudible routes close before speaker presentation can be acknowledged.

```csharp
public bool MNN.Unity.MNNRealtimeDeviceSession.TryReadPlaybackFeatures(float[] waveform, out MNN.Unity.MNNAudioFeatures features)
```

```csharp
public MNN.Unity.MNNRealtimeDeviceSessionOptions.MNNRealtimeDeviceSessionOptions()
```

```csharp
public MNN.Unity.MNNRealtimeDeviceSnapshot.MNNRealtimeDeviceSnapshot()
```

```csharp
public MNN.Unity.MNNRealtimeDeviceSnapshot.MNNRealtimeDeviceSnapshot(bool active, bool inputActive, bool capturing, bool captureStarting, bool outputActive, bool outputRunning, bool playing, bool audible, bool routeMatches, MNN.Unity.MNNPcmFormat outputFormat, long renderedFrames = 0, double presentationTailMilliseconds = 0, float volume = 1, bool canRestartPlayback = false, bool outputPaused = false, System.Exception fault = null)
```

```csharp
public static bool MNN.Unity.MNNRealtimeEchoDiagnostics.IsQualified(bool recording, bool requested, object diagnostics)
```

```csharp
public static bool MNN.Unity.MNNRealtimeEchoDiagnostics.PublicBool(object value, string name)
```

```csharp
public static bool MNN.Unity.MNNRealtimeEchoDiagnostics.PublicFalse(object value, string name)
```

```csharp
public static object MNN.Unity.MNNRealtimeEchoDiagnostics.PublicValue(object value, string name)
```

```csharp
public MNN.Unity.MNNRealtimeEchoHealth.MNNRealtimeEchoHealth()
```

```csharp
public bool MNN.Unity.MNNRealtimeEchoHealth.IsHealthyAt(long ticks)
```

```csharp
public void MNN.Unity.MNNRealtimeEchoHealth.Publish(bool qualified, int validMilliseconds = 50)
```

```csharp
public void MNN.Unity.MNNRealtimeEchoHealth.PublishAt(bool qualified, long ticks, int validMilliseconds = 50)
```

```csharp
public void MNN.Unity.MNNRealtimeEchoHealth.Reset()
```

```csharp
public MNN.Unity.MNNRealtimeFrameVoiceActivity.MNNRealtimeFrameVoiceActivity(System.Func<float[], int, MNN.Unity.MNNRealtimeVadUpdate> detect, System.Action reset = null)
```

Preserves detector probability and hysteresis separately. The adapter owns sample positions so a detector-only reset does not reset the continuous PCM timeline.

```csharp
public MNN.Unity.MNNRealtimeFrameVoiceActivity.MNNRealtimeFrameVoiceActivity(System.Func<float[], int, float> detect, float threshold = 0.5, System.Action reset = null)
```

```csharp
public static MNN.Unity.MNNRealtimeFrameVoiceActivity MNN.Unity.MNNRealtimeFrameVoiceActivity.Energy(float rmsThreshold)
```

```csharp
public MNN.Unity.MNNRealtimeVadUpdate MNN.Unity.MNNRealtimeFrameVoiceActivity.ProcessFrame(float[] samples, int offset)
```

```csharp
public void MNN.Unity.MNNRealtimeFrameVoiceActivity.Reset()
```

```csharp
public MNN.Unity.MNNRealtimeModelInputBuffer.MNNRealtimeModelInputBuffer(int capacity, System.TimeSpan? maximumQueueAge = null)
```

Limits queue residence to 300 ms by default, measured from enqueue to the start of a read. This does not measure capture age or model execution time. Use Timeout.InfiniteTimeSpan for offline replay.

```csharp
public void MNN.Unity.MNNRealtimeModelInputBuffer.Dispose()
```

Call after the producer and consumer have both stopped.

```csharp
public bool MNN.Unity.MNNRealtimeModelInputBuffer.TryRead(float[] destination, int offset, out MNN.Unity.MNNRealtimeModelInputInfo info)
```

Model owner only. Discards expired frames from the current write snapshot before copying one fresh frame. A too-small destination leaves all frames queued. False may mean that all snapshot frames expired; call WaitForData or inspect cancellation before retrying. Gaps require model reset/recovery before feeding PCM.

```csharp
public bool MNN.Unity.MNNRealtimeModelInputBuffer.TryWrite(float[] frame, int offset, MNN.Unity.MNNRealtimeVadUpdate vad, bool discontinuity = false)
```

Processing worker only. The supplied frame is copied; the processor retains its scratch array.

```csharp
public bool MNN.Unity.MNNRealtimeModelInputBuffer.WaitForData(System.TimeSpan timeout)
```

Waits for the single model consumer to receive available input. Writes coalesce into one wake signal; always drain with TryRead until it returns false. Availability does not imply freshness; only TryRead evaluates age. Stop producers and consumers before disposing.

```csharp
public void MNN.Unity.MNNRealtimeModelInputBuffer.WakeWaiter()
```

Wakes the model owner so it can observe session cancellation or shutdown.

```csharp
public MNN.Unity.MNNRealtimeModelInputInfo.MNNRealtimeModelInputInfo()
```

```csharp
public MNN.Unity.MNNRealtimeOutputPull.MNNRealtimeOutputPull(MNN.Unity.MNNRealtimeOutputPipeline output, int captureTailMilliseconds = 500)
```

```csharp
public void MNN.Unity.MNNRealtimeOutputPull.Disable()
```

```csharp
public void MNN.Unity.MNNRealtimeOutputPull.Dispose()
```

```csharp
public void MNN.Unity.MNNRealtimeOutputPull.Initialize(MNN.Unity.MNNPcmFormat format)
```

```csharp
public void MNN.Unity.MNNRealtimeOutputPull.Render(System.Span<float> buffer, MNN.Unity.MNNPcmFormat format)
```

```csharp
public MNN.Unity.MNNRealtimePlayback.MNNRealtimePlayback(MNN.Unity.MNNPcmFormat format, int framesPerBlock, int blockCapacity = 6, int receiptCapacity = 256)
```

```csharp
public bool MNN.Unity.MNNRealtimePlayback.Accepts(MNN.Unity.MNNResponseId response)
```

```csharp
public MNN.Unity.MNNResponseId MNN.Unity.MNNRealtimePlayback.BeginResponse()
```

Control executor only. Caller must drain/cancel the old producer before starting a new one.

```csharp
public bool MNN.Unity.MNNRealtimePlayback.CompleteResponse(MNN.Unity.MNNResponseId response)
```

Closes production after its worker has emitted final EOS; queued audio remains renderable.

```csharp
public void MNN.Unity.MNNRealtimePlayback.Interrupt()
```

Immediate generation invalidation. Already submitted hardware audio cannot be recalled here.

```csharp
public bool MNN.Unity.MNNRealtimePlayback.IsComplete(MNN.Unity.MNNResponseId response)
```

```csharp
public void MNN.Unity.MNNRealtimePlayback.Render(float[] destination, int offset, int count)
```

Device callback only. Fills every requested sample, including silence. No allocation or locks. The device must submit this buffer unchanged and acknowledge actual presentation separately.

```csharp
public bool MNN.Unity.MNNRealtimePlayback.TryEnqueue(float[] samples, int offset, int count, MNN.Unity.MNNSpeechChunkInfo info)
```

Producer copies bounded finite normalized PCM. False means stale output or backpressure.

```csharp
public bool MNN.Unity.MNNRealtimePlayback.TryReadReceipt(out MNN.Unity.MNNPlaybackReceipt receipt)
```

```csharp
public MNN.Unity.MNNRealtimeUtteranceBuffer.MNNRealtimeUtteranceBuffer(int minimumSpeechMilliseconds = 140, int silenceMilliseconds = 550, int maximumSeconds = 30, int preRollMilliseconds = 96)
```

```csharp
public void MNN.Unity.MNNRealtimeUtteranceBuffer.Append(float[] frame, bool speech, bool discontinuity = false)
```

```csharp
public void MNN.Unity.MNNRealtimeUtteranceBuffer.Reset()
```

```csharp
public float[] MNN.Unity.MNNRealtimeUtteranceBuffer.Snapshot()
```

```csharp
public MNN.Unity.MNNRealtimeVadUpdate.MNNRealtimeVadUpdate()
```

```csharp
public void MNN.Unity.MNNRealtimeVoiceActivity.Dispose()
```

```csharp
public static MNN.Unity.MNNRealtimeVoiceActivity MNN.Unity.MNNRealtimeVoiceActivity.Load(string modelDirectory, int threadCount = 1, MNN.Unity.MNNBackendType backendType = MNN.Unity.MNNBackendType.Auto, float threshold = 0.5, float releaseThreshold = 0.35, int hangoverMilliseconds = 120)
```

```csharp
public MNN.Unity.MNNRealtimeVadUpdate MNN.Unity.MNNRealtimeVoiceActivity.ProcessFrame(float[] samples, int offset)
```

Processes one consecutive 32 ms frame of normalized mono 16 kHz PCM on a worker thread.

```csharp
public void MNN.Unity.MNNRealtimeVoiceActivity.Reset()
```

Reset on stream start, explicit gap, format change or model recovery.

```csharp
public MNN.Unity.MNNRealtimeVoiceInputProcessor.MNNRealtimeVoiceInputProcessor(MNN.Unity.MNNPcmFormat inputFormat, MNN.Unity.IMNNRealtimeVoiceActivity voiceActivity, int maximumInputBlockFrames)
```

```csharp
public MNN.Unity.MNNRealtimeVoiceInputProcessor.MNNRealtimeVoiceInputProcessor(MNN.Unity.MNNPcmFormat inputFormat, MNN.Unity.MNNRealtimeVoiceActivity voiceActivity, int maximumInputBlockFrames)
```

```csharp
public bool MNN.Unity.MNNRealtimeVoiceInputProcessor.Complete(System.Action<MNN.Unity.MNNRealtimeVadUpdate> onUpdate, System.Action<float[], int, MNN.Unity.MNNRealtimeVadUpdate> onFrame = null)
```

Flushes filter output and emits one zero-padded VAD frame when a partial frame remains.

```csharp
public void MNN.Unity.MNNRealtimeVoiceInputProcessor.Process(float[] monoPcm, int offset, int inputFrames, System.Action<MNN.Unity.MNNRealtimeVadUpdate> onUpdate, bool discontinuity = false, System.Action<float[], int, MNN.Unity.MNNRealtimeVadUpdate> onFrame = null)
```

Consumes one bounded device block and emits zero or more complete VAD updates.

```csharp
public void MNN.Unity.MNNRealtimeVoiceInputProcessor.Reset()
```

Resets filter, frame and recurrent VAD state after a discontinuity or for a new stream.

```csharp
public void MNN.Unity.MNNResembleDenoiser.Dispose()
```

```csharp
public float[] MNN.Unity.MNNResembleDenoiser.Enhance(float[] samples, int sampleRate = 44100, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken))
```

Denoise 400–132300 resampled samples; restore the caller's sample rate and output length.

```csharp
public float[] MNN.Unity.MNNResembleDenoiser.EnhanceWaveFile(string path, out int sampleRate, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken))
```

```csharp
public static MNN.Unity.MNNResembleDenoiser MNN.Unity.MNNResembleDenoiser.Load(string directory, int threadCount = 8, MNN.Unity.MNNBackendType backendType = MNN.Unity.MNNBackendType.Auto)
```

```csharp
public MNN.Unity.MNNResembleDenoiserConfig.MNNResembleDenoiserConfig()
```

```csharp
public static MNN.Unity.MNNResembleDenoiserConfig MNN.Unity.MNNResembleDenoiserConfig.Read(string directory)
```

```csharp
public void MNN.Unity.MNNResembleEnhancer.Dispose()
```

```csharp
public float[] MNN.Unity.MNNResembleEnhancer.Enhance(float[] samples, int sampleRate = 44100, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken))
```

Enhance 400–132300 finite mono samples at 44.1 kHz, preserving their count and amplitude scale.

```csharp
public float[] MNN.Unity.MNNResembleEnhancer.EnhanceWaveFile(string path, out int sampleRate, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken))
```

```csharp
public static MNN.Unity.MNNResembleEnhancer MNN.Unity.MNNResembleEnhancer.Load(string directory, int threadCount = 4, MNN.Unity.MNNBackendType backendType = MNN.Unity.MNNBackendType.Auto)
```

CPU4/High or Metal for every neural stage. Auto prefers available Metal, otherwise CPU.

```csharp
public MNN.Unity.MNNSegConfig.MNNSegConfig()
```

```csharp
public static MNN.Unity.MNNSegConfig MNN.Unity.MNNSegConfig.Read(string directory)
```

```csharp
public void MNN.Unity.MNNSegExtraction.Dispose()
```

```csharp
public float[] MNN.Unity.MNNSegExtraction.Extract(float[] samples, MNN.Unity.MNNSegGestureFeatures gestures, int sampleRate = 16000, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken))
```

Extract 1067–160000 samples at 16 kHz. Features must span floor(duration × 15) synchronized frames.

```csharp
public float[] MNN.Unity.MNNSegExtraction.ExtractWaveFile(string path, MNN.Unity.MNNSegGestureFeatures gestures, out int sampleRate, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken))
```

```csharp
public static MNN.Unity.MNNSegExtraction MNN.Unity.MNNSegExtraction.Load(string directory, int threadCount = 4, MNN.Unity.MNNBackendType backendType = MNN.Unity.MNNBackendType.Auto)
```

```csharp
public MNN.Unity.MNNSegGestureFeatures.MNNSegGestureFeatures(float[] values, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken))
```

```csharp
public static MNN.Unity.MNNSegGestureFeatures MNN.Unity.MNNSegGestureFeatures.ReadJson(string path, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken))
```

Read the frame-major JSON feature format described in the model README.

```csharp
public float[] MNN.Unity.MNNSegGestureFeatures.ToArray()
```

```csharp
public MNN.Unity.MNNSepFormerConfig.MNNSepFormerConfig()
```

```csharp
public static MNN.Unity.MNNSepFormerConfig MNN.Unity.MNNSepFormerConfig.Read(string directory)
```

```csharp
public void MNN.Unity.MNNSepFormerSeparation.Dispose()
```

```csharp
public static MNN.Unity.MNNSepFormerSeparation MNN.Unity.MNNSepFormerSeparation.Load(string directory, int threadCount = 4, MNN.Unity.MNNBackendType backendType = MNN.Unity.MNNBackendType.Auto)
```

```csharp
public MNN.Unity.MNNSeparatedAudio MNN.Unity.MNNSepFormerSeparation.Separate(float[] samples, int sampleRate = 8000, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken))
```

Separate 16–80000 samples at 8 kHz, or equivalent recordings at 8–96 kHz. Output amplitude/order follows the original model; speaker slots have no persistent identity.

```csharp
public MNN.Unity.MNNSeparatedAudio MNN.Unity.MNNSepFormerSeparation.SeparateWaveFile(string path, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken))
```

```csharp
public MNN.Unity.MNNSepReformerConfig.MNNSepReformerConfig()
```

```csharp
public static MNN.Unity.MNNSepReformerConfig MNN.Unity.MNNSepReformerConfig.Read(string directory)
```

```csharp
public void MNN.Unity.MNNSepReformerSeparation.Dispose()
```

```csharp
public static MNN.Unity.MNNSepReformerSeparation MNN.Unity.MNNSepReformerSeparation.Load(string directory, int threadCount = 4, MNN.Unity.MNNBackendType backendType = MNN.Unity.MNNBackendType.Auto)
```

```csharp
public MNN.Unity.MNNSeparatedAudio MNN.Unity.MNNSepReformerSeparation.Separate(float[] samples, int sampleRate = 8000, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken))
```

Separate 16–32000 mono samples at 8 kHz, or equivalent recordings at 8–96 kHz. Speaker slots and amplitude follow the author; slots have no persistent speaker identity.

```csharp
public MNN.Unity.MNNSeparatedAudio MNN.Unity.MNNSepReformerSeparation.SeparateWaveFile(string path, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken))
```

```csharp
public MNN.Unity.MNNVoiceActivityResult MNN.Unity.MNNSileroVad.Detect(float[] samples, int sampleRate, float threshold = 0.5, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken))
```

Detect speech in normalized mono PCM at 8–96 kHz, up to ten minutes. Fixed 250 ms minimum speech, 100 ms silence and 30 ms boundary padding.

```csharp
public System.Threading.Tasks.Task<MNN.Unity.MNNVoiceActivityResult> MNN.Unity.MNNSileroVad.DetectAsync(float[] samples, int sampleRate, float threshold = 0.5, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken))
```

```csharp
public MNN.Unity.MNNVoiceActivityResult MNN.Unity.MNNSileroVad.DetectWaveFile(string path, float threshold = 0.5, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken))
```

```csharp
public void MNN.Unity.MNNSileroVad.Dispose()
```

```csharp
public static MNN.Unity.MNNSileroVad MNN.Unity.MNNSileroVad.Load(string directory, int threadCount = 1, MNN.Unity.MNNBackendType backendType = MNN.Unity.MNNBackendType.Auto)
```

```csharp
public MNN.Unity.MNNSileroVadConfig.MNNSileroVadConfig()
```

```csharp
public static MNN.Unity.MNNSileroVadConfig MNN.Unity.MNNSileroVadConfig.Read(string directory)
```

```csharp
public void MNN.Unity.MNNSmartTurn.Dispose()
```

```csharp
public static MNN.Unity.MNNSmartTurn MNN.Unity.MNNSmartTurn.Load(string directory, int threadCount = 4, MNN.Unity.MNNBackendType backendType = MNN.Unity.MNNBackendType.Auto)
```

```csharp
public MNN.Unity.MNNTurnDetectionResult MNN.Unity.MNNSmartTurn.Predict(float[] samples, int sampleRate, float threshold = 0.5, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken))
```

Evaluate normalized mono PCM at 8–96 kHz, up to ten minutes. Only the last eight seconds are used. Short recordings are padded at the beginning.

```csharp
public System.Threading.Tasks.Task<MNN.Unity.MNNTurnDetectionResult> MNN.Unity.MNNSmartTurn.PredictAsync(float[] samples, int sampleRate, float threshold = 0.5, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken))
```

```csharp
public MNN.Unity.MNNTurnDetectionResult MNN.Unity.MNNSmartTurn.PredictWaveFile(string path, float threshold = 0.5, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken))
```

```csharp
public MNN.Unity.MNNSmartTurnConfig.MNNSmartTurnConfig()
```

```csharp
public static MNN.Unity.MNNSmartTurnConfig MNN.Unity.MNNSmartTurnConfig.Read(string directory)
```

```csharp
public MNN.Unity.MNNSortformerConfig.MNNSortformerConfig()
```

```csharp
public static MNN.Unity.MNNSortformerConfig MNN.Unity.MNNSortformerConfig.Read(string directory)
```

```csharp
public MNN.Unity.MNNSpeakerActivity MNN.Unity.MNNSortformerDiarization.Analyze(float[] samples, int sampleRate = 16000, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken))
```

Analyze 10 ms–60 s mono PCM. Four overlapping speaker slots are local to this recording.

```csharp
public MNN.Unity.MNNSpeakerActivity MNN.Unity.MNNSortformerDiarization.AnalyzeWaveFile(string path, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken))
```

```csharp
public void MNN.Unity.MNNSortformerDiarization.Dispose()
```

```csharp
public static MNN.Unity.MNNSortformerDiarization MNN.Unity.MNNSortformerDiarization.Load(string directory, int threadCount = 4, MNN.Unity.MNNBackendType backendType = MNN.Unity.MNNBackendType.Auto)
```

```csharp
public MNN.Unity.MNNSpExPlusConfig.MNNSpExPlusConfig()
```

```csharp
public static MNN.Unity.MNNSpExPlusConfig MNN.Unity.MNNSpExPlusConfig.Read(string directory)
```

```csharp
public void MNN.Unity.MNNSpExPlusExtraction.Dispose()
```

```csharp
public MNN.Unity.MNNSpExPlusReference MNN.Unity.MNNSpExPlusExtraction.EncodeReference(float[] samples, int sampleRate = 8000, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken))
```

Encode 35 ms–8 s of unpadded, mono target speech. The immutable result can be reused.

```csharp
public MNN.Unity.MNNSpExPlusReference MNN.Unity.MNNSpExPlusExtraction.EncodeReferenceWaveFile(string path, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken))
```

```csharp
public float[] MNN.Unity.MNNSpExPlusExtraction.Extract(float[] samples, MNN.Unity.MNNSpExPlusReference reference, int sampleRate = 8000, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken))
```

Extract 2.5 ms–4 s of mono mixture. Preserves source amplitude and restores the original sample count.

```csharp
public float[] MNN.Unity.MNNSpExPlusExtraction.ExtractWaveFile(string path, MNN.Unity.MNNSpExPlusReference reference, out int sampleRate, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken))
```

```csharp
public static MNN.Unity.MNNSpExPlusExtraction MNN.Unity.MNNSpExPlusExtraction.Load(string directory, int threadCount = 4, MNN.Unity.MNNBackendType backendType = MNN.Unity.MNNBackendType.Auto)
```

```csharp
public float[] MNN.Unity.MNNSpExPlusReference.ToArray()
```

```csharp
public void MNN.Unity.MNNSpeakerEmbedding.Dispose()
```

```csharp
public float[] MNN.Unity.MNNSpeakerEmbedding.Encode(float[] samples, int sampleRate = 16000, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken))
```

```csharp
public System.Threading.Tasks.Task<float[]> MNN.Unity.MNNSpeakerEmbedding.EncodeAsync(float[] samples, int sampleRate = 16000, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken))
```

```csharp
public float[] MNN.Unity.MNNSpeakerEmbedding.EncodeWaveFile(string path, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken))
```

```csharp
public static MNN.Unity.MNNSpeakerEmbedding MNN.Unity.MNNSpeakerEmbedding.Load(string directory, int threadCount = 4, MNN.Unity.MNNBackendType backendType = MNN.Unity.MNNBackendType.Auto)
```

```csharp
public static float MNN.Unity.MNNSpeakerEmbedding.Similarity(float[] left, float[] right)
```

```csharp
public void MNN.Unity.MNNSpeakerEncoder.Dispose()
```

```csharp
public float[] MNN.Unity.MNNSpeakerEncoder.Encode(float[] audio, int sampleRate = 16000, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken))
```

```csharp
public static MNN.Unity.MNNSpeakerEncoder MNN.Unity.MNNSpeakerEncoder.Load(string directory, int threadCount = 2, MNN.Unity.MNNBackendType backendType = MNN.Unity.MNNBackendType.Auto)
```

```csharp
public static string MNN.Unity.MNNSpeakerEncoder.ProfileFile(string directory)
```

```csharp
public MNN.Unity.MNNSpeakerModelConfig.MNNSpeakerModelConfig()
```

```csharp
public MNN.Unity.MNNSpeechChunkInfo.MNNSpeechChunkInfo()
```

```csharp
public MNN.Unity.MNNSpeechChunkInfo.MNNSpeechChunkInfo(MNN.Unity.MNNResponseId response, long segmentId, int chunkSequence, bool endsSegment)
```

```csharp
public MNN.Unity.MNNSpeechFade.MNNSpeechFade()
```

```csharp
public System.Threading.Tasks.Task MNN.Unity.MNNSpeechFade.FadeOutAsync(MNN.Unity.MNNResponseId response)
```

```csharp
public void MNN.Unity.MNNSpeechFade.Process(float[] samples, int count, int channels, int sampleRate, MNN.Unity.MNNResponseId response)
```

```csharp
public MNN.Unity.MNNSpeechOutputProcessor.MNNSpeechOutputProcessor(MNN.Unity.MNNRealtimeOutputPipeline output, int sampleRate, double presentationTailMilliseconds = 0)
```

```csharp
public System.Threading.Tasks.Task MNN.Unity.MNNSpeechOutputProcessor.FadeOutAsync()
```

```csharp
public void MNN.Unity.MNNSpeechOutputProcessor.Process(float[] samples, int channels)
```

```csharp
public void MNN.Unity.MNNSpeechOutputProcessor.Process(float[] samples, int count, int channels)
```

```csharp
public void MNN.Unity.MNNSpeechOutputProcessor.SetPresentationTail(double milliseconds)
```

```csharp
public void MNN.Unity.MNNSpeechSeparator.Dispose()
```

```csharp
public static MNN.Unity.MNNSpeechSeparator MNN.Unity.MNNSpeechSeparator.Load(string directory, int threadCount = 2, MNN.Unity.MNNBackendType backendType = MNN.Unity.MNNBackendType.Auto)
```

```csharp
public static string MNN.Unity.MNNSpeechSeparator.ProfileFile(string directory)
```

```csharp
public MNN.Unity.MNNSeparatedAudio MNN.Unity.MNNSpeechSeparator.Separate(float[] audio, int sampleRate = 16000, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken))
```

Separates bounded three-second windows with 250 ms overlap. Matches source slots across audible overlap and crossfades boundaries. Silence cannot establish speaker identity.

```csharp
public MNN.Unity.MNNStreamingResampler.MNNStreamingResampler(MNN.Unity.MNNPcmFormat input, MNN.Unity.MNNPcmFormat output)
```

```csharp
public void MNN.Unity.MNNStreamingResampler.Complete(float[] output, int outputOffsetSamples, int outputCapacityFrames, out int producedFrames)
```

Marks end-of-stream and emits the filter tail using zero padding.

```csharp
public void MNN.Unity.MNNStreamingResampler.Process(float[] input, int inputOffsetSamples, int inputFrames, float[] output, int outputOffsetSamples, int outputCapacityFrames, out int consumedFrames, out int producedFrames)
```

Consumes as many complete interleaved frames as fit the output buffer. Unconsumed input remains caller-owned. Returns immediately if outputCapacityFrames is zero. No allocations occur after construction.

```csharp
public void MNN.Unity.MNNStreamingResampler.Reset()
```

Clears phase and filter history for a new recording.

```csharp
public MNN.Unity.MNNSudoRmRfConfig.MNNSudoRmRfConfig()
```

```csharp
public static MNN.Unity.MNNSudoRmRfConfig MNN.Unity.MNNSudoRmRfConfig.Read(string directory)
```

```csharp
public void MNN.Unity.MNNSudoRmRfSeparation.Dispose()
```

```csharp
public static MNN.Unity.MNNSudoRmRfSeparation MNN.Unity.MNNSudoRmRfSeparation.Load(string directory, int threadCount = 4, MNN.Unity.MNNBackendType backendType = MNN.Unity.MNNBackendType.Auto)
```

```csharp
public MNN.Unity.MNNSeparatedAudio MNN.Unity.MNNSudoRmRfSeparation.Separate(float[] samples, int sampleRate = 8000, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken))
```

Separate 2–32000 mono samples at 8 kHz, or equivalent recordings at 8–96 kHz. Speaker slots and amplitude follow the author; slots have no persistent speaker identity.

```csharp
public MNN.Unity.MNNSeparatedAudio MNN.Unity.MNNSudoRmRfSeparation.SeparateWaveFile(string path, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken))
```

```csharp
public MNN.Unity.MNNTigerConfig.MNNTigerConfig()
```

```csharp
public static MNN.Unity.MNNTigerConfig MNN.Unity.MNNTigerConfig.Read(string directory)
```

```csharp
public void MNN.Unity.MNNTigerSeparation.Dispose()
```

```csharp
public static MNN.Unity.MNNTigerSeparation MNN.Unity.MNNTigerSeparation.Load(string directory, int threadCount = 4, MNN.Unity.MNNBackendType backendType = MNN.Unity.MNNBackendType.Auto)
```

```csharp
public MNN.Unity.MNNSeparatedAudio MNN.Unity.MNNTigerSeparation.Separate(float[] samples, int sampleRate = 16000, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken))
```

Separate 321–48000 samples at 16 kHz, or equivalent mono PCM at 8–96 kHz. Slots have no persistent speaker identity. Output preserves the input sample rate and length.

```csharp
public MNN.Unity.MNNSeparatedAudio MNN.Unity.MNNTigerSeparation.SeparateWaveFile(string path, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken))
```

```csharp
public MNN.Unity.MNNTurnDetectionResult.MNNTurnDetectionResult(float probability, float threshold, double duration)
```

```csharp
public void MNN.Unity.MNNTurnDetector.Dispose()
```

```csharp
public bool MNN.Unity.MNNTurnDetector.IsComplete(float[] audio, int sampleRate, string transcript = null, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken))
```

```csharp
public static MNN.Unity.MNNTurnDetector MNN.Unity.MNNTurnDetector.Load(string directory, int threadCount = 4, MNN.Unity.MNNBackendType backendType = MNN.Unity.MNNBackendType.Auto)
```

```csharp
public static string MNN.Unity.MNNTurnDetector.ProfileFile(string directory)
```

```csharp
public void MNN.Unity.MNNTurnSense.Dispose()
```

```csharp
public static MNN.Unity.MNNTurnSense MNN.Unity.MNNTurnSense.Load(string directory, int threadCount = 4, MNN.Unity.MNNBackendType backendType = MNN.Unity.MNNBackendType.Auto)
```

```csharp
public MNN.Unity.MNNTurnSenseResult MNN.Unity.MNNTurnSense.Predict(string transcript, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken))
```

Preserves transcript whitespace and Unicode; rejects overlong input instead of truncating it.

```csharp
public MNN.Unity.MNNTurnSenseConfig.MNNTurnSenseConfig()
```

```csharp
public static MNN.Unity.MNNTurnSenseConfig MNN.Unity.MNNTurnSenseConfig.Read(string directory)
```

```csharp
public MNN.Unity.MNNVbxConfig.MNNVbxConfig()
```

```csharp
public static MNN.Unity.MNNVbxConfig MNN.Unity.MNNVbxConfig.Read(string directory)
```

```csharp
public void MNN.Unity.MNNVbxEmbedding.Dispose()
```

```csharp
public float[] MNN.Unity.MNNVbxEmbedding.Encode(float[] samples, int sampleRate = 0, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken))
```

Encode 95 ms–10 seconds of finite mono PCM at the profile rate. Zero selects InputSampleRate. Retain the original vector scale.

```csharp
public System.Threading.Tasks.Task<float[]> MNN.Unity.MNNVbxEmbedding.EncodeAsync(float[] samples, int sampleRate = 0, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken))
```

```csharp
public float[] MNN.Unity.MNNVbxEmbedding.EncodeWaveFile(string path, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken))
```

```csharp
public static MNN.Unity.MNNVbxEmbedding MNN.Unity.MNNVbxEmbedding.Load(string directory, int threadCount = -1, MNN.Unity.MNNBackendType backendType = MNN.Unity.MNNBackendType.Auto)
```

Load a pinned profile. Omitted threadCount (-1) selects its measured CPU fallback count.

```csharp
public static float MNN.Unity.MNNVbxEmbedding.Similarity(float[] left, float[] right)
```

```csharp
public MNN.Unity.MNNVoiceActivityResult MNN.Unity.MNNVoiceActivityDetector.Detect(float[] audio, int sampleRate, float threshold = 0.5, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken))
```

```csharp
public void MNN.Unity.MNNVoiceActivityDetector.Dispose()
```

```csharp
public static bool MNN.Unity.MNNVoiceActivityDetector.HasProfile(string directory)
```

```csharp
public static MNN.Unity.MNNVoiceActivityDetector MNN.Unity.MNNVoiceActivityDetector.Load(string directory, int threadCount = 1, MNN.Unity.MNNBackendType backendType = MNN.Unity.MNNBackendType.Auto)
```

```csharp
public static string MNN.Unity.MNNVoiceActivityDetector.ProfileFile(string directory)
```

```csharp
public void MNN.Unity.MNNVoxTurn.Dispose()
```

```csharp
public float MNN.Unity.MNNVoxTurn.EndOfTurn(float[] samples = null, string text = null, string language = "en", string speaker = "user")
```

```csharp
public static MNN.Unity.MNNVoxTurn MNN.Unity.MNNVoxTurn.Load(string directory, int threadCount = 4, MNN.Unity.MNNBackendType backendType = MNN.Unity.MNNBackendType.Auto)
```

Auto uses CPU for text and available Metal language stages for audio requests. Backend properties report the latest sessions.

```csharp
public MNN.Unity.MNNVoxTurnResult MNN.Unity.MNNVoxTurn.Predict(MNN.Unity.MNNVoxTurnRequest request, string[] questions = null, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken))
```

```csharp
public System.Collections.Generic.IReadOnlyList<MNN.Unity.MNNVoxTurnResult> MNN.Unity.MNNVoxTurn.PredictBatch(System.Collections.Generic.IReadOnlyList<MNN.Unity.MNNVoxTurnRequest> requests, string[] questions = null, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken))
```

Evaluate one or two immutable requests, retaining block-isolating attention across multiple questions.

```csharp
public float MNN.Unity.MNNVoxTurnAnswer.Probability(string label)
```

```csharp
public MNN.Unity.MNNVoxTurnRequest.MNNVoxTurnRequest(float[] samples = null, string text = null, string language = "en", string speaker = "user")
```

```csharp
public MNN.Unity.MNNWakeAudioBuffer.MNNWakeAudioBuffer()
```

```csharp
public void MNN.Unity.MNNWakeAudioBuffer.Append(float[] frame)
```

```csharp
public void MNN.Unity.MNNWakeAudioBuffer.Append(float[] frame, int offset, int count)
```

```csharp
public void MNN.Unity.MNNWakeAudioBuffer.Discard()
```

```csharp
public void MNN.Unity.MNNWakeAudioBuffer.Mark(double endSeconds)
```

```csharp
public void MNN.Unity.MNNWakeAudioBuffer.Reset()
```

```csharp
public float[] MNN.Unity.MNNWakeAudioBuffer.Take()
```

```csharp
public MNN.Unity.MNNConversationWakeHandoff MNN.Unity.MNNWakeAudioBuffer.TakeHandoff()
```

```csharp
public MNN.Unity.MNNWakeDetector.MNNWakeDetector(MNN.Unity.MNNAcousticSettings settings, MNN.Unity.IMNNAcousticModelFactory factory = null, MNN.Unity.MNNWakeAudioBuffer audio = null)
```

```csharp
public void MNN.Unity.MNNWakeDetector.Discard()
```

```csharp
public void MNN.Unity.MNNWakeDetector.Dispose()
```

```csharp
public void MNN.Unity.MNNWakeDetector.Load(string root)
```

```csharp
public void MNN.Unity.MNNWakeDetector.Observe(float[] original, int offset, float[] enhanced)
```

```csharp
public System.Threading.Tasks.Task MNN.Unity.MNNWakeDetector.ReleaseAsync()
```

```csharp
public System.Threading.Tasks.Task MNN.Unity.MNNWakeDetector.ReloadAsync(string root, MNN.Unity.MNNAcousticSettings settings, System.Threading.CancellationToken token)
```

```csharp
public void MNN.Unity.MNNWakeDetector.Reset()
```

```csharp
public float[] MNN.Unity.MNNWakeDetector.TakeAudio()
```

```csharp
public MNN.Unity.MNNConversationWakeHandoff MNN.Unity.MNNWakeDetector.TakeHandoff()
```

```csharp
public bool MNN.Unity.MNNWakeDetector.TakeRequest()
```

```csharp
public static void MNN.Unity.MNNWakeWordSettings.CopyWake(MNN.Unity.MNNAcousticSettings source, MNN.Unity.MNNAcousticSettings target)
```

```csharp
public static System.Collections.Generic.IReadOnlyDictionary<MNN.Unity.MNNKeywordLanguage, string> MNN.Unity.MNNWakeWordSettings.Group(MNN.Unity.MNNAcousticSettings settings)
```

```csharp
public static string MNN.Unity.MNNWakeWordSettings.MergeDisplayWords(string original, System.Collections.Generic.IEnumerable<string> words)
```

```csharp
public static string MNN.Unity.MNNWakeWordSettings.ModelDirectory(MNN.Unity.MNNAcousticSettings settings, MNN.Unity.MNNKeywordLanguage language)
```

```csharp
public static bool MNN.Unity.MNNWakeWordSettings.OnlyWakeChanged(MNN.Unity.MNNAcousticSettings before, MNN.Unity.MNNAcousticSettings after)
```

```csharp
public static bool MNN.Unity.MNNWakeWordSettings.SameWake(MNN.Unity.MNNAcousticSettings before, MNN.Unity.MNNAcousticSettings after)
```

```csharp
public void MNN.Unity.MNNWeSpeakerEmbedding.Dispose()
```

```csharp
public float[] MNN.Unity.MNNWeSpeakerEmbedding.Encode(float[] samples, int sampleRate = 16000, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken))
```

```csharp
public System.Threading.Tasks.Task<float[]> MNN.Unity.MNNWeSpeakerEmbedding.EncodeAsync(float[] samples, int sampleRate = 16000, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken))
```

```csharp
public float[] MNN.Unity.MNNWeSpeakerEmbedding.EncodeWaveFile(string path, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken))
```

```csharp
public static MNN.Unity.MNNWeSpeakerEmbedding MNN.Unity.MNNWeSpeakerEmbedding.Load(string directory, int threadCount = 4, MNN.Unity.MNNBackendType backendType = MNN.Unity.MNNBackendType.Auto)
```

```csharp
public static float MNN.Unity.MNNWeSpeakerEmbedding.Similarity(float[] left, float[] right)
```

```csharp
public MNN.Unity.MNNWeSpeakerModelConfig.MNNWeSpeakerModelConfig()
```

```csharp
public static MNN.Unity.MNNWeSpeakerModelConfig MNN.Unity.MNNWeSpeakerModelConfig.Read(string directory)
```

```csharp
int MNN.Unity.IMNNAcousticEnhancer.DelaySamples { get; }
```

```csharp
string MNN.Unity.IMNNAcousticEnhancer.Diagnostics { get; }
```

```csharp
bool MNN.Unity.IMNNAcousticEnhancer.Streaming { get; }
```

```csharp
MNN.Unity.MNNKeywordLanguage MNN.Unity.IMNNAcousticKeywordModel.Language { get; }
```

```csharp
MNN.Unity.MNNPcmFormat MNN.Unity.IMNNAudioCaptureSource.CaptureFormat { get; }
```

```csharp
long MNN.Unity.IMNNAudioCaptureSource.DroppedCaptureBlocks { get; }
```

```csharp
bool MNN.Unity.IMNNAudioCaptureSource.HasPendingCaptureFrames { get; }
```

```csharp
int MNN.Unity.IMNNAudioCaptureSource.MaximumCaptureSamples { get; }
```

```csharp
MNN.Unity.MNNPcmFormat MNN.Unity.IMNNRealtimeDemandSink.Format { get; }
```

```csharp
int MNN.Unity.IMNNRealtimeDemandSink.QueuedSamples { get; }
```

```csharp
double MNN.Unity.IMNNRealtimeDeviceClock.Seconds { get; }
```

```csharp
MNN.Unity.MNNPcmFormat MNN.Unity.IMNNRealtimeVoiceInputProcessor.InputFormat { get; }
```

```csharp
int MNN.Unity.IMNNRealtimeVoiceInputProcessor.MaximumInputBlockFrames { get; }
```

```csharp
public string MNN.Unity.MNNAcousticCheckEntry.Action { get; }
```

```csharp
public string MNN.Unity.MNNAcousticCheckEntry.Code { get; }
```

```csharp
public string MNN.Unity.MNNAcousticCheckEntry.Message { get; }
```

```csharp
public string MNN.Unity.MNNAcousticCheckEntry.Path { get; }
```

```csharp
public MNN.Unity.MNNAcousticCheckSeverity MNN.Unity.MNNAcousticCheckEntry.Severity { get; }
```

```csharp
public string MNN.Unity.MNNAcousticCheckEntry.Stage { get; }
```

```csharp
public bool MNN.Unity.MNNAcousticCheckReport.CanAttemptPreparation { get; }
```

```csharp
public System.Collections.Generic.IReadOnlyList<MNN.Unity.MNNAcousticCheckEntry> MNN.Unity.MNNAcousticCheckReport.Entries { get; }
```

```csharp
public bool MNN.Unity.MNNAcousticFrontend.AutomaticSpeaker { get; }
```

```csharp
public long MNN.Unity.MNNAcousticFrontend.CaptureRevision { get; }
```

```csharp
public string MNN.Unity.MNNAcousticFrontend.Diagnostics { get; }
```

```csharp
public System.TimeSpan MNN.Unity.MNNAcousticFrontend.MaximumCaptureAge { get; }
```

```csharp
public int MNN.Unity.MNNAcousticFrontend.RecognitionDelaySamples { get; }
```

```csharp
public System.Func<bool> MNN.Unity.MNNAcousticFrontend.WakeAllowed { get; set; }
```

```csharp
public MNN.Unity.MNNWakeDetector MNN.Unity.MNNAcousticFrontend.WakeDetector { get; }
```

```csharp
public bool MNN.Unity.MNNAcousticFrontend.WakeEnabled { get; }
```

```csharp
public MNN.Unity.MNNPcmFormat MNN.Unity.MNNAcousticInputProcessor.InputFormat { get; }
```

```csharp
public bool MNN.Unity.MNNAcousticInputProcessor.IsCompleted { get; }
```

```csharp
public bool MNN.Unity.MNNAcousticInputProcessor.IsFaulted { get; }
```

```csharp
public int MNN.Unity.MNNAcousticInputProcessor.MaximumInputBlockFrames { get; }
```

```csharp
public int MNN.Unity.MNNAcousticNoiseReduction.DelaySamples { get; }
```

```csharp
public string MNN.Unity.MNNAcousticNoiseReduction.Diagnostics { get; }
```

```csharp
public bool MNN.Unity.MNNAcousticNoiseReduction.Enabled { get; }
```

```csharp
public string[] MNN.Unity.MNNAcousticSettings.WakeWords { get; set; }
```

```csharp
public string MNN.Unity.MNNAcousticSpeakerSelection.Diagnostics { get; }
```

```csharp
public float MNN.Unity.MNNAdaptiveEchoFilter.ResidualPowerRatio { get; private set; }
```

```csharp
public int MNN.Unity.MNNAudioCaptureBuffer.AvailableBlocks { get; }
```

```csharp
public bool MNN.Unity.MNNAudioCaptureBuffer.CanWrite { get; }
```

```csharp
public long MNN.Unity.MNNAudioCaptureBuffer.DroppedBlocks { get; }
```

```csharp
public bool MNN.Unity.MNNAudioCaptureBuffer.HasPendingFrames { get; }
```

```csharp
public int MNN.Unity.MNNAudioCaptureBuffer.MaximumSamples { get; }
```

```csharp
public float MNN.Unity.MNNAudioEnvelope.AttackSeconds { get; }
```

```csharp
public float MNN.Unity.MNNAudioEnvelope.ReleaseSeconds { get; }
```

```csharp
public float MNN.Unity.MNNAudioEnvelope.Value { get; private set; }
```

```csharp
public bool MNN.Unity.MNNAudioFrameInfo.Discontinuity { get; }
```

```csharp
public MNN.Unity.MNNPcmFormat MNN.Unity.MNNAudioFrameInfo.Format { get; }
```

```csharp
public long MNN.Unity.MNNAudioFrameInfo.FormatEpoch { get; }
```

```csharp
public long MNN.Unity.MNNAudioFrameInfo.MonotonicTicks { get; }
```

First sample frame's capture time in current-process Stopwatch ticks, not enqueue time. Map hardware timestamps into this clock before publishing live audio.

```csharp
public long MNN.Unity.MNNAudioFrameInfo.Sequence { get; }
```

```csharp
public long MNN.Unity.MNNAudioFrameInfo.StartFrame { get; }
```

```csharp
public long MNN.Unity.MNNAudioFrameInfo.StreamId { get; }
```

```csharp
public int MNN.Unity.MNNAudioReferenceTimeline.CapacitySamples { get; }
```

```csharp
public long MNN.Unity.MNNAudioReferenceTimeline.EndSample { get; }
```

```csharp
public MNN.Unity.MNNBackendType MNN.Unity.MNNAvConvTasNetExtraction.AudioBackend { get; }
```

```csharp
public MNN.Unity.MNNBackendType MNN.Unity.MNNAvConvTasNetExtraction.Backend { get; }
```

```csharp
public MNN.Unity.MNNBackendType MNN.Unity.MNNAvDprnnExtraction.AudioBackend { get; }
```

```csharp
public MNN.Unity.MNNBackendType MNN.Unity.MNNAvDprnnExtraction.Backend { get; }
```

```csharp
public MNN.Unity.MNNBackendType MNN.Unity.MNNAvMossFormer2Tse.Backend { get; }
```

```csharp
public bool MNN.Unity.MNNBargeInPolicy.HasCandidate { get; }
```

```csharp
public MNN.Unity.MNNBackendType MNN.Unity.MNNConvTasNetSeparation.Backend { get; }
```

```csharp
public double MNN.Unity.MNNCueLightAnalysis.DurationSeconds { get; }
```

```csharp
public int MNN.Unity.MNNCueLightAnalysis.FrameCount { get; }
```

```csharp
public MNN.Unity.MNNCueLightFrame[] MNN.Unity.MNNCueLightAnalysis.Frames { get; }
```

```csharp
public float[] MNN.Unity.MNNCueLightAnalysis.Probabilities { get; }
```

```csharp
public bool MNN.Unity.MNNCueLightFrame.CarefulEndpoint { get; }
```

```csharp
public float MNN.Unity.MNNCueLightFrame.EndOfTurnProbability { get; }
```

```csharp
public float[] MNN.Unity.MNNCueLightFrame.Probabilities { get; }
```

```csharp
public int MNN.Unity.MNNCueLightFrame.ResetCount { get; }
```

```csharp
public bool MNN.Unity.MNNCueLightFrame.SnappyEndpoint { get; }
```

```csharp
public float MNN.Unity.MNNCueLightFrame.SpeechProbability { get; }
```

```csharp
public MNN.Unity.MNNBackendType MNN.Unity.MNNCueLightTurnDetector.Backend { get; }
```

```csharp
public string MNN.Unity.MNNCueLightTurnDetector.ModelName { get; }
```

```csharp
public MNN.Unity.MNNBackendType MNN.Unity.MNNDccrnEnhancement.AnalysisBackend { get; }
```

```csharp
public MNN.Unity.MNNBackendType MNN.Unity.MNNDccrnEnhancement.Backend { get; }
```

```csharp
public MNN.Unity.MNNBackendType MNN.Unity.MNNDccrnEnhancement.DecoderBackend { get; }
```

```csharp
public MNN.Unity.MNNBackendType MNN.Unity.MNNDccrnEnhancement.RecurrentBackend { get; }
```

```csharp
public MNN.Unity.MNNBackendType MNN.Unity.MNNDccrnetEnhancement.Backend { get; }
```

```csharp
public MNN.Unity.MNNBackendType MNN.Unity.MNNDccrnetEnhancement.DecoderBackend { get; }
```

```csharp
public MNN.Unity.MNNBackendType MNN.Unity.MNNDccrnetEnhancement.RecurrentBackend { get; }
```

```csharp
public MNN.Unity.MNNBackendType MNN.Unity.MNNDcunetEnhancement.Backend { get; }
```

```csharp
public MNN.Unity.MNNBackendType MNN.Unity.MNNDeepFilterNet3.DfDecoderBackend { get; }
```

```csharp
public MNN.Unity.MNNBackendType MNN.Unity.MNNDeepFilterNet3.EncoderBackend { get; }
```

```csharp
public MNN.Unity.MNNBackendType MNN.Unity.MNNDeepFilterNet3.ErbDecoderBackend { get; }
```

```csharp
public MNN.Unity.MNNBackendType MNN.Unity.MNNDprnnSeparation.Backend { get; }
```

```csharp
public MNN.Unity.MNNBackendType MNN.Unity.MNNDptnetEnhancement.Backend { get; }
```

```csharp
public MNN.Unity.MNNBackendType MNN.Unity.MNNDtln.Stage1Backend { get; }
```

```csharp
public MNN.Unity.MNNBackendType MNN.Unity.MNNDtln.Stage2Backend { get; }
```

```csharp
public MNN.Unity.MNNBackendType MNN.Unity.MNNDtlnAec.Backend { get; }
```

```csharp
public static string MNN.Unity.MNNDtlnAec.ConfigFileName { get; }
```

```csharp
public static int[] MNN.Unity.MNNDtlnAec.SupportedUnits { get; }
```

```csharp
public int MNN.Unity.MNNDtlnAec.Units { get; }
```

```csharp
public MNN.Unity.MNNBackendType MNN.Unity.MNNDualTurnEndpointing.Backend { get; }
```

```csharp
public long MNN.Unity.MNNDualTurnEndpointing.TickCount { get; }
```

```csharp
public System.Collections.Generic.IReadOnlyList<float> MNN.Unity.MNNDualTurnEndpointingResult.AgentFutureVoiceProbabilities { get; }
```

```csharp
public float MNN.Unity.MNNDualTurnEndpointingResult.AgentVoiceProbability { get; }
```

```csharp
public float MNN.Unity.MNNDualTurnEndpointingResult.EndOfTurnProbability { get; }
```

```csharp
public double MNN.Unity.MNNDualTurnEndpointingResult.ProcessedSeconds { get; }
```

```csharp
public long MNN.Unity.MNNDualTurnEndpointingResult.TickCount { get; }
```

```csharp
public System.Collections.Generic.IReadOnlyList<float> MNN.Unity.MNNDualTurnEndpointingResult.UserFutureVoiceProbabilities { get; }
```

```csharp
public float MNN.Unity.MNNDualTurnEndpointingResult.UserVoiceProbability { get; }
```

```csharp
public MNN.Unity.MNNPcmFormat MNN.Unity.MNNEchoCancellationWorker.CaptureFormat { get; }
```

```csharp
public MNN.Unity.MNNEchoDelayEstimator MNN.Unity.MNNEchoCancellationWorker.DelayEstimator { get; }
```

```csharp
public int MNN.Unity.MNNEchoCancellationWorker.DelayMilliseconds { get; }
```

```csharp
public long MNN.Unity.MNNEchoCancellationWorker.DroppedCaptureBlocks { get; }
```

```csharp
public System.Exception MNN.Unity.MNNEchoCancellationWorker.Fault { get; }
```

```csharp
public bool MNN.Unity.MNNEchoCancellationWorker.HasPendingCaptureFrames { get; }
```

```csharp
public bool MNN.Unity.MNNEchoCancellationWorker.Healthy { get; }
```

```csharp
public int MNN.Unity.MNNEchoCancellationWorker.MaximumCaptureSamples { get; }
```

```csharp
public string MNN.Unity.MNNEchoCancellationWorker.Status { get; }
```

```csharp
public float MNN.Unity.MNNEchoDelayEstimator.Confidence { get; private set; }
```

```csharp
public int MNN.Unity.MNNEchoDelayEstimator.DelaySamples { get; private set; }
```

```csharp
public bool MNN.Unity.MNNEchoDelayEstimator.IsLocked { get; }
```

```csharp
public MNN.Unity.MNNBackendType MNN.Unity.MNNFrcrnSe.Backend { get; }
```

```csharp
public double MNN.Unity.MNNFsEendActivity.DurationSeconds { get; }
```

```csharp
public int MNN.Unity.MNNFsEendActivity.FrameCount { get; }
```

```csharp
public float[] MNN.Unity.MNNFsEendActivity.Probabilities { get; }
```

```csharp
public MNN.Unity.MNNBackendType MNN.Unity.MNNFsEendDiarization.Backend { get; }
```

```csharp
public string MNN.Unity.MNNFsEendDiarization.ModelName { get; }
```

```csharp
public MNN.Unity.MNNBackendType MNN.Unity.MNNFsmnVad.Backend { get; }
```

```csharp
public MNN.Unity.MNNBackendType MNN.Unity.MNNFullSubNet.Backend { get; }
```

```csharp
public string MNN.Unity.MNNFullSubNet.ModelName { get; }
```

```csharp
public MNN.Unity.MNNBackendType MNN.Unity.MNNGtcrn.Backend { get; }
```

```csharp
public float MNN.Unity.MNNKeywordDetection.Confidence { get; }
```

```csharp
public float MNN.Unity.MNNKeywordDetection.EndSeconds { get; }
```

```csharp
public string MNN.Unity.MNNKeywordDetection.Keyword { get; }
```

```csharp
public float MNN.Unity.MNNKeywordDetection.StartSeconds { get; }
```

```csharp
public System.Collections.Generic.IReadOnlyList<int> MNN.Unity.MNNKeywordDetection.TokenIds { get; }
```

```csharp
public MNN.Unity.MNNBackendType MNN.Unity.MNNKeywordSpotter.Backend { get; }
```

```csharp
public static string MNN.Unity.MNNKeywordSpotter.DefaultKeywords { get; }
```

```csharp
public MNN.Unity.MNNKeywordLanguage MNN.Unity.MNNKeywordSpotter.Language { get; }
```

```csharp
public MNN.Unity.MNNKeywordLanguage MNN.Unity.MNNKeywordSpotterConfig.Language { get; }
```

```csharp
public MNN.Unity.MNNBackendType MNN.Unity.MNNLiveKitTurnDetector.Backend { get; }
```

```csharp
public double MNN.Unity.MNNLsEendActivity.DurationSeconds { get; }
```

```csharp
public int MNN.Unity.MNNLsEendActivity.FrameCount { get; }
```

```csharp
public float[] MNN.Unity.MNNLsEendActivity.Probabilities { get; }
```

```csharp
public MNN.Unity.MNNBackendType MNN.Unity.MNNLsEendDiarization.DecoderBackend { get; }
```

```csharp
public MNN.Unity.MNNBackendType MNN.Unity.MNNLsEendDiarization.EncoderBackend { get; }
```

```csharp
public MNN.Unity.MNNBackendType MNN.Unity.MNNMarbleNetVad.Backend { get; }
```

```csharp
public MNN.Unity.MNNBackendType MNN.Unity.MNNMossFormer2Se.Backend { get; }
```

```csharp
public MNN.Unity.MNNBackendType MNN.Unity.MNNMossFormer2Separation.Backend { get; }
```

```csharp
public MNN.Unity.MNNBackendType MNN.Unity.MNNMossFormerGan.Backend { get; }
```

```csharp
public MNN.Unity.MNNBackendType MNN.Unity.MNNNamoTurnDetector.Backend { get; }
```

The backend used by the latest call; long Metal requests run on CPU.

```csharp
public int MNN.Unity.MNNPcmFormat.Channels { get; }
```

```csharp
public int MNN.Unity.MNNPcmFormat.SampleRate { get; }
```

```csharp
public int MNN.Unity.MNNPcmFrameAccumulator.FrameSamples { get; }
```

```csharp
public bool MNN.Unity.MNNPcmFrameAccumulator.IsCompleted { get; }
```

```csharp
public int MNN.Unity.MNNPcmFrameAccumulator.PendingSamples { get; }
```

```csharp
public MNN.Unity.MNNSpeechChunkInfo MNN.Unity.MNNPlaybackReceipt.Chunk { get; }
```

```csharp
public bool MNN.Unity.MNNPlaybackReceipt.EndsChunk { get; }
```

```csharp
public bool MNN.Unity.MNNPlaybackReceipt.EndsSegment { get; }
```

```csharp
public int MNN.Unity.MNNPlaybackReceipt.FrameCount { get; }
```

```csharp
public long MNN.Unity.MNNPlaybackReceipt.OutputEndFrame { get; }
```

```csharp
public long MNN.Unity.MNNPlaybackReceipt.OutputStartFrame { get; }
```

```csharp
public int MNN.Unity.MNNPlaybackReceipt.SourceStartFrame { get; }
```

```csharp
public MNN.Unity.MNNBackendType MNN.Unity.MNNProfiledVoiceActivity.Backend { get; }
```

```csharp
public System.Threading.Tasks.Task MNN.Unity.MNNRealtimeAudioCaptureWorker.Completion { get; }
```

One shared task, completed after processing and the shutdown flush, or faulted with the original error. Observe this task to report failures without polling. Registered continuations are dispatched asynchronously. Completion does not join the OS thread: stop the capture producer, then call Stop before releasing resources used by the worker. Stopping before Start completes successfully without flushing.

```csharp
public long MNN.Unity.MNNRealtimeAudioCaptureWorker.DroppedCaptureBlocks { get; }
```

```csharp
public long MNN.Unity.MNNRealtimeAudioCaptureWorker.DroppedModelFrames { get; }
```

```csharp
public long MNN.Unity.MNNRealtimeAudioCaptureWorker.EmptyPolls { get; }
```

```csharp
public long MNN.Unity.MNNRealtimeAudioCaptureWorker.ExpiredCaptureBlocks { get; }
```

```csharp
public long MNN.Unity.MNNRealtimeAudioCaptureWorker.ExpiredCaptureFrames { get; }
```

```csharp
public long MNN.Unity.MNNRealtimeAudioCaptureWorker.ExpiredModelFrames { get; }
```

```csharp
public System.Exception MNN.Unity.MNNRealtimeAudioCaptureWorker.Fault { get; }
```

```csharp
public bool MNN.Unity.MNNRealtimeAudioCaptureWorker.IsFaulted { get; }
```

```csharp
public bool MNN.Unity.MNNRealtimeAudioCaptureWorker.IsRunning { get; }
```

```csharp
public bool MNN.Unity.MNNRealtimeAudioCaptureWorker.IsStopped { get; }
```

```csharp
public System.TimeSpan MNN.Unity.MNNRealtimeAudioCaptureWorker.MaximumCaptureAge { get; }
```

```csharp
public long MNN.Unity.MNNRealtimeAudioCaptureWorker.ProcessedBlocks { get; }
```

```csharp
public long MNN.Unity.MNNRealtimeAudioCaptureWorker.ProcessedInputFrames { get; }
```

```csharp
public bool MNN.Unity.MNNRealtimeCapturePolicy.CaptureRequested { get; }
```

```csharp
public bool MNN.Unity.MNNRealtimeCapturePort.Accepting { get; set; }
```

```csharp
public MNN.Unity.MNNPcmFormat MNN.Unity.MNNRealtimeCapturePort.CaptureFormat { get; }
```

```csharp
public long MNN.Unity.MNNRealtimeCapturePort.DroppedCaptureBlocks { get; }
```

```csharp
public System.Exception MNN.Unity.MNNRealtimeCapturePort.Fault { get; }
```

```csharp
public bool MNN.Unity.MNNRealtimeCapturePort.HasPendingCaptureFrames { get; }
```

```csharp
public int MNN.Unity.MNNRealtimeCapturePort.MaximumCaptureSamples { get; }
```

```csharp
public double MNN.Unity.MNNRealtimeClockSynchronizer.CaptureDriftPpm { get; }
```

```csharp
public double MNN.Unity.MNNRealtimeClockSynchronizer.CaptureRateRatio { get; }
```

```csharp
public double MNN.Unity.MNNRealtimeClockSynchronizer.CaptureToPresentationFramesPerFrame { get; }
```

```csharp
public bool MNN.Unity.MNNRealtimeClockSynchronizer.HasCaptureClock { get; }
```

```csharp
public bool MNN.Unity.MNNRealtimeClockSynchronizer.HasPresentationClock { get; }
```

```csharp
public double MNN.Unity.MNNRealtimeClockSynchronizer.PresentationDriftPpm { get; }
```

```csharp
public double MNN.Unity.MNNRealtimeClockSynchronizer.PresentationRateRatio { get; }
```

```csharp
public System.Exception MNN.Unity.MNNRealtimeDemandFeeder.Fault { get; }
```

```csharp
public abstract MNN.Unity.IMNNAudioCaptureSource MNN.Unity.MNNRealtimeDeviceBinding.Capture { get; }
```

```csharp
public virtual System.Exception MNN.Unity.MNNRealtimeDeviceBinding.Fault { get; }
```

```csharp
public virtual bool MNN.Unity.MNNRealtimeDeviceBinding.OpensPlaybackFirst { get; }
```

```csharp
public abstract MNN.Unity.MNNPcmFormat MNN.Unity.MNNRealtimeDeviceBinding.OutputFormat { get; }
```

```csharp
public virtual int MNN.Unity.MNNRealtimeDeviceBinding.ReferenceBlockFrames { get; }
```

```csharp
public virtual bool MNN.Unity.MNNRealtimeDeviceBinding.SpeechMayBeAudible { get; }
```

```csharp
public virtual float? MNN.Unity.MNNRealtimeDeviceBinding.SpeechVolume { get; }
```

```csharp
public virtual MNN.Unity.MNNPlaybackWaveform MNN.Unity.MNNRealtimeDeviceBinding.Waveform { get; }
```

```csharp
public MNN.Unity.MNNRealtimeEchoHealth MNN.Unity.MNNRealtimeDeviceSession.BackendEchoHealth { get; }
```

```csharp
public bool MNN.Unity.MNNRealtimeDeviceSession.CanInterruptSpeech { get; }
```

```csharp
public MNN.Unity.IMNNAudioCaptureSource MNN.Unity.MNNRealtimeDeviceSession.Capture { get; }
```

```csharp
public MNN.Unity.MNNEchoCancellationWorker MNN.Unity.MNNRealtimeDeviceSession.Echo { get; }
```

```csharp
public int? MNN.Unity.MNNRealtimeDeviceSession.EchoDelayMilliseconds { get; }
```

```csharp
public string MNN.Unity.MNNRealtimeDeviceSession.EchoStatus { get; }
```

```csharp
public System.Exception MNN.Unity.MNNRealtimeDeviceSession.Fault { get; }
```

```csharp
public bool MNN.Unity.MNNRealtimeDeviceSession.HasBeenUsed { get; }
```

```csharp
public bool MNN.Unity.MNNRealtimeDeviceSession.IsClosed { get; }
```

```csharp
public bool MNN.Unity.MNNRealtimeDeviceSession.IsReady { get; }
```

```csharp
public MNN.Unity.MNNRealtimeOutputPipeline MNN.Unity.MNNRealtimeDeviceSession.Output { get; }
```

```csharp
public double MNN.Unity.MNNRealtimeDeviceSession.PresentationTailMilliseconds { get; }
```

```csharp
public bool MNN.Unity.MNNRealtimeDeviceSession.SpeechMayBeAudible { get; }
```

```csharp
public float? MNN.Unity.MNNRealtimeDeviceSession.SpeechVolume { get; }
```

```csharp
public bool MNN.Unity.MNNRealtimeEchoHealth.Healthy { get; }
```

```csharp
public int MNN.Unity.MNNRealtimeModelInputBuffer.Capacity { get; }
```

```csharp
public long MNN.Unity.MNNRealtimeModelInputBuffer.DroppedFrames { get; }
```

```csharp
public long MNN.Unity.MNNRealtimeModelInputBuffer.ExpiredFrames { get; }
```

```csharp
public bool MNN.Unity.MNNRealtimeModelInputBuffer.HasPendingFrames { get; }
```

```csharp
public System.TimeSpan MNN.Unity.MNNRealtimeModelInputBuffer.MaximumQueueAge { get; }
```

```csharp
public bool MNN.Unity.MNNRealtimeModelInputInfo.Discontinuity { get; }
```

```csharp
public long MNN.Unity.MNNRealtimeModelInputInfo.EnqueuedMonotonicTicks { get; }
```

Enqueue time in current-process Stopwatch ticks, including when expiration is disabled.

```csharp
public bool MNN.Unity.MNNRealtimeModelInputInfo.IsSpeechActive { get; }
```

```csharp
public long MNN.Unity.MNNRealtimeModelInputInfo.Sequence { get; }
```

```csharp
public bool MNN.Unity.MNNRealtimeModelInputInfo.SpeechEnded { get; }
```

```csharp
public float MNN.Unity.MNNRealtimeModelInputInfo.SpeechProbability { get; }
```

```csharp
public bool MNN.Unity.MNNRealtimeModelInputInfo.SpeechStarted { get; }
```

```csharp
public long MNN.Unity.MNNRealtimeModelInputInfo.StartSample { get; }
```

```csharp
public MNN.Unity.MNNRealtimeVadUpdate MNN.Unity.MNNRealtimeModelInputInfo.Vad { get; }
```

The VAD result consumed with this PCM frame, on the input owner's thread.

```csharp
public MNN.Unity.MNNSpeechFade MNN.Unity.MNNRealtimeOutputPull.Fade { get; }
```

```csharp
public System.Exception MNN.Unity.MNNRealtimeOutputPull.Fault { get; }
```

```csharp
public bool MNN.Unity.MNNRealtimeOutputPull.MayHaveAudibleSpeech { get; }
```

```csharp
public long MNN.Unity.MNNRealtimeOutputPull.RenderedFrames { get; }
```

```csharp
public MNN.Unity.MNNPlaybackWaveform MNN.Unity.MNNRealtimeOutputPull.Waveform { get; }
```

```csharp
public MNN.Unity.MNNResponseId MNN.Unity.MNNRealtimePlayback.CurrentResponse { get; }
```

```csharp
public long MNN.Unity.MNNRealtimePlayback.DiscardedChunks { get; }
```

```csharp
public MNN.Unity.MNNPcmFormat MNN.Unity.MNNRealtimePlayback.Format { get; }
```

```csharp
public bool MNN.Unity.MNNRealtimePlayback.HasPendingAudio { get; }
```

```csharp
public bool MNN.Unity.MNNRealtimePlayback.HasPendingReceipts { get; }
```

```csharp
public bool MNN.Unity.MNNRealtimePlayback.IsFaulted { get; }
```

```csharp
public int MNN.Unity.MNNRealtimePlayback.MaximumSamples { get; }
```

```csharp
public double MNN.Unity.MNNRealtimePlayback.QueuedAudioMilliseconds { get; }
```

```csharp
public long MNN.Unity.MNNRealtimePlayback.QueuedFrames { get; }
```

```csharp
public long MNN.Unity.MNNRealtimePlayback.RenderedFrames { get; }
```

```csharp
public long MNN.Unity.MNNRealtimePlayback.UnderrunFrames { get; }
```

```csharp
public bool MNN.Unity.MNNRealtimeUtteranceBuffer.HasSpeech { get; }
```

```csharp
public bool MNN.Unity.MNNRealtimeUtteranceBuffer.HasSpeechStarted { get; }
```

Any speech has entered the current turn, including speech below the acceptance threshold.

```csharp
public bool MNN.Unity.MNNRealtimeUtteranceBuffer.IsCandidate { get; }
```

```csharp
public bool MNN.Unity.MNNRealtimeUtteranceBuffer.IsFull { get; }
```

```csharp
public long MNN.Unity.MNNRealtimeUtteranceBuffer.Revision { get; }
```

```csharp
public int MNN.Unity.MNNRealtimeUtteranceBuffer.SilenceSamples { get; }
```

```csharp
public bool MNN.Unity.MNNRealtimeVadUpdate.IsSpeechActive { get; }
```

```csharp
public bool MNN.Unity.MNNRealtimeVadUpdate.SpeechEnded { get; }
```

```csharp
public float MNN.Unity.MNNRealtimeVadUpdate.SpeechProbability { get; }
```

```csharp
public bool MNN.Unity.MNNRealtimeVadUpdate.SpeechStarted { get; }
```

```csharp
public long MNN.Unity.MNNRealtimeVadUpdate.StartSample { get; }
```

```csharp
public MNN.Unity.MNNBackendType MNN.Unity.MNNRealtimeVoiceActivity.Backend { get; }
```

```csharp
public int MNN.Unity.MNNRealtimeVoiceActivity.HangoverFrames { get; private set; }
```

```csharp
public bool MNN.Unity.MNNRealtimeVoiceActivity.IsSpeechActive { get; }
```

```csharp
public long MNN.Unity.MNNRealtimeVoiceActivity.ProcessedSamples { get; }
```

```csharp
public float MNN.Unity.MNNRealtimeVoiceActivity.ReleaseThreshold { get; private set; }
```

```csharp
public float MNN.Unity.MNNRealtimeVoiceActivity.Threshold { get; private set; }
```

```csharp
public MNN.Unity.MNNPcmFormat MNN.Unity.MNNRealtimeVoiceInputProcessor.InputFormat { get; }
```

```csharp
public bool MNN.Unity.MNNRealtimeVoiceInputProcessor.IsCompleted { get; }
```

```csharp
public bool MNN.Unity.MNNRealtimeVoiceInputProcessor.IsFaulted { get; }
```

```csharp
public int MNN.Unity.MNNRealtimeVoiceInputProcessor.MaximumInputBlockFrames { get; }
```

```csharp
public int MNN.Unity.MNNRealtimeVoiceInputProcessor.PendingVadSamples { get; }
```

```csharp
public MNN.Unity.MNNBackendType MNN.Unity.MNNResembleDenoiser.Backend { get; }
```

```csharp
public static string MNN.Unity.MNNResembleEnhancer.ConfigFileName { get; }
```

```csharp
public MNN.Unity.MNNBackendType MNN.Unity.MNNResembleEnhancer.DecoderBackend { get; }
```

```csharp
public MNN.Unity.MNNBackendType MNN.Unity.MNNResembleEnhancer.DenoiserBackend { get; }
```

```csharp
public MNN.Unity.MNNBackendType MNN.Unity.MNNResembleEnhancer.EncoderBackend { get; }
```

```csharp
public MNN.Unity.MNNBackendType MNN.Unity.MNNResembleEnhancer.VelocityBackend { get; }
```

```csharp
public MNN.Unity.MNNBackendType MNN.Unity.MNNResembleEnhancer.VocoderBackend { get; }
```

```csharp
public MNN.Unity.MNNBackendType MNN.Unity.MNNSegExtraction.Backend { get; }
```

```csharp
public int MNN.Unity.MNNSegGestureFeatures.FrameCount { get; }
```

```csharp
public MNN.Unity.MNNBackendType MNN.Unity.MNNSepFormerSeparation.Backend { get; }
```

```csharp
public MNN.Unity.MNNBackendType MNN.Unity.MNNSepReformerSeparation.Backend { get; }
```

```csharp
public MNN.Unity.MNNBackendType MNN.Unity.MNNSileroVad.Backend { get; }
```

```csharp
public MNN.Unity.MNNBackendType MNN.Unity.MNNSmartTurn.Backend { get; }
```

```csharp
public MNN.Unity.MNNBackendType MNN.Unity.MNNSortformerDiarization.CoreBackend { get; }
```

```csharp
public MNN.Unity.MNNBackendType MNN.Unity.MNNSortformerDiarization.PreencodeBackend { get; }
```

```csharp
public MNN.Unity.MNNBackendType MNN.Unity.MNNSpExPlusExtraction.Backend { get; }
```

```csharp
public MNN.Unity.MNNBackendType MNN.Unity.MNNSpExPlusExtraction.ReferenceBackend { get; }
```

```csharp
public int MNN.Unity.MNNSpExPlusReference.Size { get; }
```

```csharp
public double MNN.Unity.MNNSpeakerActivity.DurationSeconds { get; }
```

```csharp
public int MNN.Unity.MNNSpeakerActivity.FrameCount { get; }
```

```csharp
public float[] MNN.Unity.MNNSpeakerActivity.Probabilities { get; }
```

```csharp
public MNN.Unity.MNNBackendType MNN.Unity.MNNSpeakerEmbedding.Backend { get; }
```

```csharp
public string MNN.Unity.MNNSpeakerEmbedding.ModelName { get; }
```

```csharp
public int MNN.Unity.MNNSpeechChunkInfo.ChunkSequence { get; }
```

```csharp
public bool MNN.Unity.MNNSpeechChunkInfo.EndsSegment { get; }
```

```csharp
public MNN.Unity.MNNResponseId MNN.Unity.MNNSpeechChunkInfo.Response { get; }
```

```csharp
public long MNN.Unity.MNNSpeechChunkInfo.SegmentId { get; }
```

```csharp
public MNN.Unity.MNNSpeechFade MNN.Unity.MNNSpeechOutputProcessor.Fade { get; }
```

```csharp
public bool MNN.Unity.MNNSpeechOutputProcessor.MayBeAudible { get; }
```

```csharp
public MNN.Unity.MNNPlaybackWaveform MNN.Unity.MNNSpeechOutputProcessor.Waveform { get; }
```

```csharp
public double MNN.Unity.MNNSpeechSegment.DurationSeconds { get; }
```

```csharp
public double MNN.Unity.MNNSpeechSegment.EndSeconds { get; }
```

```csharp
public double MNN.Unity.MNNSpeechSegment.StartSeconds { get; }
```

```csharp
public long MNN.Unity.MNNStreamingResampler.InputFrames { get; }
```

```csharp
public bool MNN.Unity.MNNStreamingResampler.IsCompleted { get; }
```

```csharp
public double MNN.Unity.MNNStreamingResampler.NextOutputPosition { get; }
```

```csharp
public MNN.Unity.MNNBackendType MNN.Unity.MNNSudoRmRfSeparation.Backend { get; }
```

```csharp
public MNN.Unity.MNNBackendType MNN.Unity.MNNTigerSeparation.Backend { get; }
```

```csharp
public float MNN.Unity.MNNTurnDetectionResult.CompletionProbability { get; }
```

```csharp
public double MNN.Unity.MNNTurnDetectionResult.DurationSeconds { get; }
```

```csharp
public bool MNN.Unity.MNNTurnDetectionResult.IsComplete { get; }
```

```csharp
public MNN.Unity.MNNBackendType MNN.Unity.MNNTurnDetector.Backend { get; }
```

```csharp
public bool MNN.Unity.MNNTurnDetector.RequiresTranscript { get; }
```

```csharp
public MNN.Unity.MNNBackendType MNN.Unity.MNNTurnSense.Backend { get; }
```

```csharp
public float MNN.Unity.MNNTurnSenseResult.EndOfUtteranceProbability { get; }
```

```csharp
public bool MNN.Unity.MNNTurnSenseResult.IsComplete { get; }
```

```csharp
public float MNN.Unity.MNNTurnSenseResult.NonEndOfUtteranceProbability { get; }
```

```csharp
public MNN.Unity.MNNBackendType MNN.Unity.MNNVbxEmbedding.Backend { get; }
```

```csharp
public int MNN.Unity.MNNVbxEmbedding.InputSampleRate { get; }
```

```csharp
public string MNN.Unity.MNNVbxEmbedding.ModelName { get; }
```

```csharp
public int MNN.Unity.MNNVbxEmbedding.ThreadCount { get; }
```

```csharp
public MNN.Unity.MNNBackendType MNN.Unity.MNNVoiceActivityDetector.Backend { get; }
```

```csharp
public double MNN.Unity.MNNVoiceActivityResult.AudioSeconds { get; }
```

```csharp
public System.Collections.Generic.IReadOnlyList<float> MNN.Unity.MNNVoiceActivityResult.Probabilities { get; }
```

```csharp
public System.Collections.Generic.IReadOnlyList<MNN.Unity.MNNSpeechSegment> MNN.Unity.MNNVoiceActivityResult.Segments { get; }
```

```csharp
public MNN.Unity.MNNBackendType MNN.Unity.MNNVoxTurn.AudioBackend { get; }
```

```csharp
public static string MNN.Unity.MNNVoxTurn.ConfigFileName { get; }
```

```csharp
public MNN.Unity.MNNBackendType MNN.Unity.MNNVoxTurn.FirstLanguageBackend { get; }
```

```csharp
public MNN.Unity.MNNBackendType MNN.Unity.MNNVoxTurn.SecondLanguageBackend { get; }
```

```csharp
public static string[] MNN.Unity.MNNVoxTurn.SupportedTasks { get; }
```

```csharp
public System.Collections.Generic.IReadOnlyList<string> MNN.Unity.MNNVoxTurnAnswer.Labels { get; }
```

```csharp
public System.Collections.Generic.IReadOnlyList<float> MNN.Unity.MNNVoxTurnAnswer.Logits { get; }
```

```csharp
public System.Collections.Generic.IReadOnlyList<float> MNN.Unity.MNNVoxTurnAnswer.Probabilities { get; }
```

```csharp
public string MNN.Unity.MNNVoxTurnAnswer.Task { get; }
```

```csharp
public bool MNN.Unity.MNNVoxTurnRequest.HasAudio { get; }
```

```csharp
public string MNN.Unity.MNNVoxTurnRequest.Language { get; }
```

```csharp
public string MNN.Unity.MNNVoxTurnRequest.Speaker { get; }
```

```csharp
public string MNN.Unity.MNNVoxTurnRequest.Text { get; }
```

```csharp
public System.Collections.Generic.IReadOnlyDictionary<string, MNN.Unity.MNNVoxTurnAnswer> MNN.Unity.MNNVoxTurnResult.Answers { get; }
```

```csharp
public long MNN.Unity.MNNWakeAudioBuffer.Position { get; }
```

```csharp
public long MNN.Unity.MNNWakeAudioBuffer.Revision { get; }
```

```csharp
public System.Func<bool> MNN.Unity.MNNWakeDetector.Allowed { get; set; }
```

```csharp
public MNN.Unity.MNNWakeAudioBuffer MNN.Unity.MNNWakeDetector.Audio { get; }
```

```csharp
public long MNN.Unity.MNNWakeDetector.CaptureRevision { get; }
```

```csharp
public bool MNN.Unity.MNNWakeDetector.Enabled { get; }
```

```csharp
public MNN.Unity.MNNBackendType MNN.Unity.MNNWeSpeakerEmbedding.Backend { get; }
```

```csharp
interface MNN.Unity.IMNNAcousticEnhancer
```

```csharp
interface MNN.Unity.IMNNAcousticKeywordModel
```

```csharp
interface MNN.Unity.IMNNAcousticModelFactory
```

One owned acoustic role per call. Implementations may wrap local or remote models. Frame methods run on a capture worker; blocking noncausal methods run on the utterance worker. Dispose must be idempotent and permit retry after a failed release.

```csharp
interface MNN.Unity.IMNNAcousticSeparator
```

```csharp
interface MNN.Unity.IMNNAcousticSpeakerEncoder
```

```csharp
interface MNN.Unity.IMNNAudioCaptureSource
```

Single-producer capture queue consumed by one audio-processing worker.

```csharp
interface MNN.Unity.IMNNEchoHopProcessor
```

One worker-owned 16 kHz/128-sample echo stage with 384 samples of output delay. Calls are serialized by MNNEchoCancellationWorker. Implementations must permit release retry.

```csharp
interface MNN.Unity.IMNNRealtimeDemandSink
```

```csharp
interface MNN.Unity.IMNNRealtimeDeviceClock
```

```csharp
interface MNN.Unity.IMNNRealtimeVoiceActivity
```

Single-worker fixed-frame detector: 512 normalized mono samples at 16 kHz.

```csharp
interface MNN.Unity.IMNNRealtimeVoiceInputProcessor
```

Worker-owned streaming audio processor that emits fixed VAD frames from bounded PCM blocks.

```csharp
class MNN.Unity.MNNAcousticCheckEntry
```

Stable code for applications; message, path and recovery action for developers.

```csharp
class MNN.Unity.MNNAcousticCheckReport
```

Immutable file-check snapshot. A successful check permits a startup attempt, not inference certification.

```csharp
enum MNN.Unity.MNNAcousticCheckSeverity
```

```csharp
class MNN.Unity.MNNAcousticFrontend
```

Reusable acoustic session: enhancement, continuous wake and utterance speaker selection. Stop capture/recognition workers before release. The session owns models and retains failed releases for retry.

```csharp
class MNN.Unity.MNNAcousticInputProcessor
```

Worker-owned resampling, 16 kHz/512-sample enhancement, observation and independent VAD/ASR routing. Callbacks borrow worker buffers for the duration of the call; observers must not mutate or retain them. The enhancer modifies its frame in place and may have at most 512 samples of signal delay. Process on a model worker, never a device callback. Reset after a callback fault.

```csharp
class MNN.Unity.MNNAcousticMnnModelFactory
```

Profile-selected MNN acoustic models with worker-local recurrent state. No device dependency.

```csharp
class MNN.Unity.MNNAcousticModelFiles
```

File hints for the existing pinned loaders. Integrity and graph protocols remain owned by MNN.Unity.

```csharp
class MNN.Unity.MNNAcousticModelResources
```

```csharp
class MNN.Unity.MNNAcousticNoiseReduction
```

Capture-worker streaming enhancement and separately owned utterance enhancement.

```csharp
class MNN.Unity.MNNAcousticPreflight
```

Configuration and local file checks only. Does not load models or prove graph integrity/compatibility.

```csharp
enum MNN.Unity.MNNAcousticProfile
```

```csharp
class MNN.Unity.MNNAcousticSettings
```

Independent of the microphone transport and ASR provider. Models must already be installed.

```csharp
class MNN.Unity.MNNAcousticSpeakerSelection
```

```csharp
class MNN.Unity.MNNAdaptiveEchoFilter
```

Improved proportionate NLMS (alpha=0) estimates the linear room response after bulk-delay alignment. Its residual power bounds suppression of DTLN output. One processing worker owns this state.

```csharp
class MNN.Unity.MNNAecDelayCalibration
```

Qualifies a stable host buffer-delay window, never the duration of AEC convergence. One worker owns this state. Add accepts fresh device snapshots and monotonic seconds without allocating.

```csharp
class MNN.Unity.MNNAudioAnalysis
```

Model-free streaming PCM measurements for meters, visualization and audio-reactive controls.

```csharp
class MNN.Unity.MNNAudioCaptureBuffer
```

One capture producer and one processing consumer. Full buffers report a gap, never move the reader.

```csharp
class MNN.Unity.MNNAudioEnvelope
```

Frame-rate-independent attack/release for normalized audio-driven controls.

```csharp
readonly struct MNN.Unity.MNNAudioFeatures
```

Immutable measurements of captured or rendered PCM, independent of visualization.

```csharp
readonly struct MNN.Unity.MNNAudioFrameInfo
```

Capture position and monotonic timestamp, independent of Unity frames and reply generations.

```csharp
class MNN.Unity.MNNAudioMixtureContribution
```

Recovers relative source energy from a mixture despite independent output gains.

```csharp
class MNN.Unity.MNNAudioProfileFiles
```

Availability hints from an installed export manifest. Runners still verify the full export contract.

```csharp
class MNN.Unity.MNNAudioReferenceTimeline
```

Worker-owned bounded mono history indexed by sample time. Gaps invalidate the old window. Storage is allocated once; one processing worker owns all calls.

```csharp
class MNN.Unity.MNNAutomaticSpeakerSelector
```

Worker-owned source selection. Contribution is relative source energy, not a speech probability. Slot numbers never represent identity; supply similarity to the previously selected speaker and monotonic seconds.

```csharp
class MNN.Unity.MNNAvConvTasNetConfig
```

```csharp
class MNN.Unity.MNNAvConvTasNetExtraction
```

Noncausal LRS2 AV-ConvTasNet extraction with synchronized target-face crops.

```csharp
class MNN.Unity.MNNAvDprnnConfig
```

```csharp
class MNN.Unity.MNNAvDprnnExtraction
```

Noncausal VoxCeleb2 AV-DPRNN extraction with synchronized target-face crops.

```csharp
class MNN.Unity.MNNAvMossFormer2Tse
```

Audio-visual target-speaker extraction using lip-conditioned MossFormer2.

```csharp
class MNN.Unity.MNNAvMossFormer2TseConfig
```

```csharp
class MNN.Unity.MNNAvMossFormer2TseVisual
```

Compatible AV-MossFormer2 entry point for ClearerVoice target-face preparation.

```csharp
enum MNN.Unity.MNNBargeInAction
```

```csharp
class MNN.Unity.MNNBargeInPolicy
```

Confirms interruption from VAD plus an explicit post-AEC near-end signal. It does not perform echo cancellation. Call once per audio-processing frame, outside device callbacks.

```csharp
class MNN.Unity.MNNClearerVoiceFaceFrames
```

Prepares an aligned target-face crop for ClearerVoice audio-visual models at 25 frames per second.

```csharp
class MNN.Unity.MNNConvTasNetConfig
```

```csharp
class MNN.Unity.MNNConvTasNetSeparation
```

Original two-speaker Libri2Mix ConvTasNet, with independent 8 kHz recording inference.

```csharp
class MNN.Unity.MNNCueLightAnalysis
```

```csharp
class MNN.Unity.MNNCueLightConfig
```

```csharp
class MNN.Unity.MNNCueLightFrame
```

```csharp
class MNN.Unity.MNNCueLightTurnDetector
```

CueLight Nano and Nano-SSM causal 20 ms speech and turn detection with author replay and endpoint policies.

```csharp
class MNN.Unity.MNNDccrnConfig
```

```csharp
class MNN.Unity.MNNDccrnEnhancement
```

Mayavoz DCCRN checkpoint-owned first-eval forward, with hop padding and length restoration.

```csharp
class MNN.Unity.MNNDccrnetConfig
```

```csharp
class MNN.Unity.MNNDccrnetEnhancement
```

Original one-source Libri1Mix DCCRNet waveform enhancement at 16 kHz.

```csharp
class MNN.Unity.MNNDcunetConfig
```

```csharp
class MNN.Unity.MNNDcunetEnhancement
```

Original one-source Libri1Mix DCUNet waveform enhancement at 16 kHz.

```csharp
class MNN.Unity.MNNDeepFilterNet3
```

Offline 48 kHz DeepFilterNet3 enhancement. Each call resets the full feature/GRU sequence. Managed DSP follows libDF; the three exported graphs process the complete utterance.

```csharp
class MNN.Unity.MNNDeepFilterNet3Config
```

```csharp
class MNN.Unity.MNNDprnnConfig
```

```csharp
class MNN.Unity.MNNDprnnSeparation
```

Original two-speaker WHAM DPRNN, with independent 8 kHz recording inference.

```csharp
class MNN.Unity.MNNDptnetConfig
```

```csharp
class MNN.Unity.MNNDptnetEnhancement
```

Original one-source Libri1Mix DPTNet waveform enhancement at 16 kHz.

```csharp
class MNN.Unity.MNNDtln
```

Two-stage 16 kHz DTLN enhancement. One instance owns one stream. Fixed graph sessions and DSP buffers are reused across 128-sample hops.

```csharp
class MNN.Unity.MNNDtlnAec
```

Two-stage 16 kHz mono echo cancellation with a synchronized playback reference. One instance owns one stream. Device/reference delay alignment belongs to the caller.

```csharp
class MNN.Unity.MNNDtlnConfig
```

```csharp
class MNN.Unity.MNNDtlnEchoHopProcessor
```

```csharp
class MNN.Unity.MNNDualTurnEndpointing
```

Stateful 24 kHz dual-channel endpointing. Feed exactly one 1920-sample tick per Push.

```csharp
class MNN.Unity.MNNDualTurnEndpointingConfig
```

```csharp
class MNN.Unity.MNNDualTurnEndpointingResult
```

One 80 ms frame: user EOT, both VAD channels, and four FVAD horizons per channel.

```csharp
class MNN.Unity.MNNEchoCancellationOptions
```

```csharp
class MNN.Unity.MNNEchoCancellationWorker
```

One worker owns DTLN states, resamplers, delay estimation and reference history. Final model stage consumed by VAD, ASR and direct-audio routes for any backend. Compose subsequent model stages through IMNNAudioCaptureSource on this worker, never on device callbacks. The audio callback only downmixes/copies into a bounded SPSC queue.

```csharp
class MNN.Unity.MNNEchoDelayEstimator
```

Managed correlation estimator inspired by AEC3's decimation, excitation gating and lag voting. This is not a port of WebRTC's adaptive matched-filter bank. Inputs share a 16 kHz timeline. One processing worker owns this state; calls must not overlap.

```csharp
class MNN.Unity.MNNFrcrnSe
```

FRCRN complex-spectrum enhancement. STFT/iSTFT and overlap-add stay in managed DSP.

```csharp
class MNN.Unity.MNNFrcrnSeConfig
```

```csharp
class MNN.Unity.MNNFsEendActivity
```

Five original post-dummy posterior columns per 100 ms frame. Speaker slots are local to a recording.

```csharp
class MNN.Unity.MNNFsEendConfig
```

```csharp
class MNN.Unity.MNNFsEendDiarization
```

FS-EEND complete-recording diarization with causal attention, original lookahead and five posterior columns.

```csharp
class MNN.Unity.MNNFsmnVad
```

FunASR FSMN streaming VAD with managed fbank/LFR and recurrent cache ownership.

```csharp
class MNN.Unity.MNNFsmnVadConfig
```

```csharp
class MNN.Unity.MNNFullSubNet
```

Offline FullSubNet complex ratio mask enhancement. Each recording uses utterance-level normalization.

```csharp
class MNN.Unity.MNNFullSubNetConfig
```

```csharp
class MNN.Unity.MNNGtcrn
```

GTCRN with one fixed spectrum frame and three explicit recurrent caches.

```csharp
class MNN.Unity.MNNGtcrnConfig
```

```csharp
class MNN.Unity.MNNKeywordDetection
```

A keyword match, with acoustic confidence and times relative to the stream's start.

```csharp
enum MNN.Unity.MNNKeywordLanguage
```

Language of ordinary wake-word text. Automatic checks compatibility before inference.

```csharp
class MNN.Unity.MNNKeywordSpotter
```

Chinese/English Zipformer2 keyword spotting using the bundled official MNN C++ ABI. Each instance owns one stream. Reset before changing recording; dispose after use.

```csharp
class MNN.Unity.MNNKeywordSpotterConfig
```

The pinned Chinese/English Zipformer2 KWS graph protocol.

```csharp
class MNN.Unity.MNNKeywordText
```

Offline ordinary-text preparation for the supported Chinese Wenetspeech and English GigaSpeech KWS models. Does not load native libraries, write files, download assets or require Python.

```csharp
class MNN.Unity.MNNLiveKitTurnDetector
```

LiveKit transcript-semantic end-of-turn detector.

```csharp
class MNN.Unity.MNNLiveKitTurnDetectorConfig
```

```csharp
class MNN.Unity.MNNLiveKitTurnMessage
```

```csharp
class MNN.Unity.MNNLsEendActivity
```

Nine original post-dummy posterior columns per 100 ms frame. Speaker slots are local to a recording.

```csharp
class MNN.Unity.MNNLsEendConfig
```

```csharp
class MNN.Unity.MNNLsEendDiarization
```

LS-EEND simulated 1–8 speaker checkpoint, complete-recording input and stateful one-frame neural execution.

```csharp
class MNN.Unity.MNNMarbleNetVad
```

MarbleNet multilingual frame VAD with managed log-mel extraction.

```csharp
class MNN.Unity.MNNMarbleNetVadConfig
```

```csharp
class MNN.Unity.MNNMossFormer2Config
```

```csharp
class MNN.Unity.MNNMossFormer2Se
```

Offline 48 kHz enhancement with the original dithered fbank and noncentered Hamming STFT.

```csharp
class MNN.Unity.MNNMossFormer2SeConfig
```

```csharp
class MNN.Unity.MNNMossFormer2Separation
```

Two-speaker MossFormer2 separation with managed chunking and overlap-add.

```csharp
class MNN.Unity.MNNMossFormerGan
```

Offline MossFormerGAN enhancement with original RMS, compressed spectra and Hamming STFT.

```csharp
class MNN.Unity.MNNMossFormerGanConfig
```

```csharp
class MNN.Unity.MNNNamoTurnDetector
```

Multilingual transcript end-of-turn probability from the author-published NAMO v1 classifier.

```csharp
class MNN.Unity.MNNNamoTurnDetectorConfig
```

```csharp
class MNN.Unity.MNNNoiseSuppressor
```

Profile-selected utterance enhancement. Owns one runner; never call from a device callback.

```csharp
readonly struct MNN.Unity.MNNPcmFormat
```

Interleaved float PCM. Counts in this API are sample frames unless named samples.

```csharp
class MNN.Unity.MNNPcmFrameAccumulator
```

Packs arbitrary sample chunks into fixed-size frames with one reusable partial-frame buffer. A single processing worker owns each instance; device callbacks should only copy into a bounded queue.

```csharp
readonly struct MNN.Unity.MNNPlaybackReceipt
```

A submitted slice, not proof of acoustic presentation. Positions are exclusive at the end.

```csharp
class MNN.Unity.MNNPlaybackWaveform
```

One render writer and one UI reader. Bounded, nonblocking snapshots; no callback allocations.

```csharp
class MNN.Unity.MNNProfiledVoiceActivity
```

Adapts profiled offline VAD to fixed capture frames on a worker. Uses a bounded 640 ms context; this recomputes features and makes no inference-throughput guarantee. Silero retains its native streaming path.

```csharp
class MNN.Unity.MNNRealtimeAudioCaptureWorker
```

Drains one bounded capture source on a dedicated long-lived thread and forwards borrowed PCM blocks to a single processor. Expired blocks are discarded by the consumer and the next block carries a gap. Stop the capture producer before stopping the worker; queued blocks drain under the age policy before exit. The processor must not retain the buffer or call Unity APIs.

```csharp
class MNN.Unity.MNNRealtimeCapturePolicy
```

Worker-safe half-duplex/cue policy. Explicit input ownership alone controls publication; loss of echo health must never splice holes into an active user recording.

```csharp
class MNN.Unity.MNNRealtimeCapturePort
```

Transport-independent bounded mono capture, safety gate epochs and asynchronous drain. Producers copy borrowed spans; one worker consumes the queue.

```csharp
class MNN.Unity.MNNRealtimeCapturePort.Producer
```

```csharp
class MNN.Unity.MNNRealtimeClockSynchronizer
```

Maps independent capture and presentation sample clocks onto one monotonic timeline. Call from one audio-processing worker, never from an audio device callback.

```csharp
class MNN.Unity.MNNRealtimeDemandFeeder
```

Bounded zero demand ahead of a pull-based playback route. Speech is rendered later by MNNRealtimeOutputPull. Stop before disposing the sink. No per-iteration allocation or Unity API access.

```csharp
class MNN.Unity.MNNRealtimeDeviceBinding
```

Primitive device operations. Session sequencing, supervision and ownership belong to MNNRealtimeDeviceSession. Control operations run on the application's control thread; Capture is one-producer/one-consumer.

```csharp
class MNN.Unity.MNNRealtimeDeviceSession
```

Reusable exclusive duplex-device session. One control thread sequences permission, negotiation, warm-up, route supervision, estimated presentation, echo ownership and restart. Capture publication is worker-safe.

```csharp
class MNN.Unity.MNNRealtimeDeviceSessionOptions
```

```csharp
readonly struct MNN.Unity.MNNRealtimeDeviceSnapshot
```

Observed device facts. Bindings gather public API values; the shared session applies policy.

```csharp
class MNN.Unity.MNNRealtimeEchoDiagnostics
```

Optional public object-shaped APM diagnostics adapter. Call on the control thread and publish the value result through MNNRealtimeEchoHealth; never reflect or box plugin values on PCM callbacks.

```csharp
class MNN.Unity.MNNRealtimeEchoHealth
```

Control-thread publication of qualified backend AEC health; worker reads never inspect plugin objects. A stalled supervisor expires qualification without waiting for another control-thread update.

```csharp
class MNN.Unity.MNNRealtimeFrameVoiceActivity
```

Adapts a bounded frame detector to the shared capture processor. Turn completion stays independent.

```csharp
class MNN.Unity.MNNRealtimeModelInputBuffer
```

Fixed-capacity SPSC transport from the audio-processing worker to a single model owner. Input is always 512 mono samples at 16 kHz; overflow drops a frame and marks the next accepted frame discontinuous. The consumer discards frames queued longer than the age budget and marks the next delivered frame with a gap.

```csharp
readonly struct MNN.Unity.MNNRealtimeModelInputInfo
```

Metadata for one fixed 512-sample mono 16 kHz model input frame.

```csharp
class MNN.Unity.MNNRealtimeOutputPull
```

Pull speech at render time, before mixing/AEC reference. Reuses bounded scratch storage.

```csharp
class MNN.Unity.MNNRealtimePlayback
```

Bounded PCM output shared by Omni and TTS. One enqueue producer, one render consumer, one receipt consumer. Control may invalidate generations without touching queue cursors.

```csharp
class MNN.Unity.MNNRealtimeUtteranceBuffer
```

Single input-owner endpoint buffer. Fixed memory, configurable pre-roll, independent speech/silence budgets. Silence produces a candidate, not a semantic decision. Resume/gaps invalidate candidate revisions. Capacity rounds up to a whole 32 ms frame; consume/reset a full buffer before appending again.

```csharp
readonly struct MNN.Unity.MNNRealtimeVadUpdate
```

One stateful VAD result; frame positions count 16 kHz mono samples within this stream.

```csharp
class MNN.Unity.MNNRealtimeVoiceActivity
```

Stateful Silero v5 inference for consecutive mono 16 kHz PCM. Run on an audio-processing worker, never from an audio-device callback. Call Reset after any lost samples or format change. Matching Silero loads share inference resources; this instance exclusively owns its stream state.

```csharp
class MNN.Unity.MNNRealtimeVoiceInputProcessor
```

Composes streaming resampling, fixed-frame assembly and recurrent Silero VAD for one mono input stream. Process on a dedicated audio-processing worker, never from a device callback.

```csharp
class MNN.Unity.MNNResembleDenoiser
```

Offline Resemble denoising with the original 44.1 kHz single-chunk inference contract.

```csharp
class MNN.Unity.MNNResembleDenoiserConfig
```

```csharp
class MNN.Unity.MNNResembleEnhancer
```

Complete offline Resemble CFM enhancement: 44.1 kHz, seed 0, 32-NFE midpoint.

```csharp
class MNN.Unity.MNNSegConfig
```

```csharp
class MNN.Unity.MNNSegExtraction
```

Noncausal 16 kHz target extraction conditioned on synchronized gesture features.

```csharp
class MNN.Unity.MNNSegGestureFeatures
```

Immutable, synchronized frame-major SEG gesture features at 15 FPS.

```csharp
class MNN.Unity.MNNSepFormerConfig
```

```csharp
class MNN.Unity.MNNSepFormerSeparation
```

Original two-speaker WSJ02Mix SepFormer, with independent 8 kHz recording inference.

```csharp
class MNN.Unity.MNNSepReformerConfig
```

```csharp
class MNN.Unity.MNNSepReformerSeparation
```

Original SepReformer-B two-speaker WSJ0-2mix inference.

```csharp
class MNN.Unity.MNNSeparatedAudio
```

Two speaker waveforms returned by a source-separation graph.

```csharp
class MNN.Unity.MNNSileroVad
```

Silero v5 voice activity detection with per-recording state reset. Matching realtime/offline loads share inference resources until the last consumer disposes; recording state remains independent.

```csharp
class MNN.Unity.MNNSileroVadConfig
```

```csharp
class MNN.Unity.MNNSmartTurn
```

Smart Turn v3.2 audio endpoint probability with an eight-second context.

```csharp
class MNN.Unity.MNNSmartTurnConfig
```

```csharp
class MNN.Unity.MNNSortformerConfig
```

```csharp
class MNN.Unity.MNNSortformerDiarization
```

Batch-one Sortformer v2.1 with the original synchronous cache policy, driven from a complete recording.

```csharp
class MNN.Unity.MNNSpExPlusConfig
```

```csharp
class MNN.Unity.MNNSpExPlusExtraction
```

Noncausal 8 kHz SpEx+ target speaker extraction with reusable reference conditioning.

```csharp
class MNN.Unity.MNNSpExPlusReference
```

Immutable, unnormalized SpEx+ conditioning, reusable across extractions.

```csharp
class MNN.Unity.MNNSpeakerActivity
```

Four concurrent speaker-slot probabilities per 80 ms frame.

```csharp
class MNN.Unity.MNNSpeakerEmbedding
```

Original-checkpoint ERes2NetV2, CAM++, ECAPA and TitaNet unit speaker vectors.

```csharp
class MNN.Unity.MNNSpeakerEncoder
```

Owned profiled speaker encoder, including the VBx 8/16 kHz frontend.

```csharp
class MNN.Unity.MNNSpeakerModelConfig
```

```csharp
readonly struct MNN.Unity.MNNSpeechChunkInfo
```

Output is already converted to the playback format before entering the render queue.

```csharp
class MNN.Unity.MNNSpeechFade
```

Single render-thread gain ramp; requests may come from UI or VAD workers.

```csharp
class MNN.Unity.MNNSpeechOutputProcessor
```

Allocation-free rendered speech fade, response-bound waveform and audible-tail tracking. Configure on the control thread; call Process from one render producer.

```csharp
class MNN.Unity.MNNSpeechSegment
```

Speech interval in seconds in the original recording. End is exclusive.

```csharp
class MNN.Unity.MNNSpeechSeparator
```

Profile-selected two-source separation. Output preserves the caller's sample rate.

```csharp
class MNN.Unity.MNNStreamingResampler
```

Stateful fixed-memory windowed-sinc resampler. Each instance accepts one producer and is not thread-safe. Process on the audio worker, never from a device callback. Reset between independent streams.

```csharp
class MNN.Unity.MNNSudoRmRfConfig
```

```csharp
class MNN.Unity.MNNSudoRmRfSeparation
```

Original improved U16/512 SudoRMRF two-speaker WSJ0-2mix inference.

```csharp
class MNN.Unity.MNNTigerConfig
```

```csharp
class MNN.Unity.MNNTigerSeparation
```

Noncausal two-source TIGER separation with the original PCM amplitude and source order.

```csharp
class MNN.Unity.MNNTurnDetectionResult
```

Probability that the current audio turn is complete, using its last eight seconds.

```csharp
class MNN.Unity.MNNTurnDetector
```

Owned audio/text endpoint detector selected from an installed export profile. Callers must supply an ASR transcript when RequiresTranscript is true.

```csharp
class MNN.Unity.MNNTurnSense
```

SmolLM2-135M TurnSense transcript EOU with exact author prefix and padded256 inputs.

```csharp
class MNN.Unity.MNNTurnSenseConfig
```

```csharp
class MNN.Unity.MNNTurnSenseResult
```

The author's two probabilities, with no additional softmax.

```csharp
class MNN.Unity.MNNVbxConfig
```

```csharp
class MNN.Unity.MNNVbxEmbedding
```

Author VBx ResNet101 direct utterance entry, returning the original raw 256-dimensional x-vector.

```csharp
class MNN.Unity.MNNVoiceActivityDetector
```

One owned VAD runner, selected by its installed export profile, for Editor and Player.

```csharp
class MNN.Unity.MNNVoiceActivityResult
```

Owned speech intervals and probabilities; each model defines its frame cadence.

```csharp
class MNN.Unity.MNNVoxTurn
```

VoxTurn v1 multimodal turn decisions at 16 kHz, with original unmerged LoRA.

```csharp
class MNN.Unity.MNNVoxTurnAnswer
```

```csharp
class MNN.Unity.MNNVoxTurnRequest
```

Immutable request: optional normalized mono 16 kHz audio and transcript context.

```csharp
class MNN.Unity.MNNVoxTurnResult
```

```csharp
class MNN.Unity.MNNWakeAudioBuffer
```

Bounded command audio after a keyword; copied once when realtime listening starts.

```csharp
class MNN.Unity.MNNWakeDetector
```

Continuous language-banked keyword detection with a bounded command handoff and hot replacement. Models run on the capture worker. Activation gating never pauses their sample clock.

```csharp
class MNN.Unity.MNNWakeWordSettings
```

One language/model decision shared by settings, preflight and the capture worker.

```csharp
class MNN.Unity.MNNWeSpeakerEmbedding
```

WeSpeaker ResNet34-LM 256-dimensional speaker vectors using official MNN graphs.

```csharp
class MNN.Unity.MNNWeSpeakerModelConfig
```

## Common

```csharp
MNN.Unity.MNNBackendType.Auto = 4
```

探测可用加速后端并保留 CPU 回退

```csharp
MNN.Unity.MNNBackendType.CPU = 0
```

CPU后端

```csharp
MNN.Unity.MNNBackendType.CUDA = 2
```

CUDA后端 (NVIDIA GPU)

```csharp
MNN.Unity.MNNBackendType.Metal = 1
```

Metal后端 (iOS/macOS)

```csharp
MNN.Unity.MNNBackendType.NN = 5
```

CoreML/NNAPI后端 (iOS/Android)

```csharp
MNN.Unity.MNNBackendType.OpenCL = 3
```

OpenCL后端 (Android/Desktop GPU)

```csharp
MNN.Unity.MNNBackendType.OpenGL = 6
```

OpenGL后端

```csharp
MNN.Unity.MNNBackendType.Vulkan = 7
```

Vulkan后端

```csharp
MNN.Unity.MNNDataType.Double = 1
```

64位浮点数

```csharp
MNN.Unity.MNNDataType.Float = 0
```

32位浮点数

```csharp
MNN.Unity.MNNDataType.Half = 6
```

16位浮点数

```csharp
MNN.Unity.MNNDataType.Int32 = 2
```

32位整数

```csharp
MNN.Unity.MNNDataType.Int64 = 3
```

64位整数

```csharp
MNN.Unity.MNNDataType.Int8 = 5
```

8位有符号整数

```csharp
MNN.Unity.MNNDataType.UInt8 = 4
```

8位无符号整数

```csharp
MNN.Unity.MNNDimensionType.Caffe = 1
```

Caffe格式 (NCHW)

```csharp
MNN.Unity.MNNDimensionType.CaffeC4 = 2
```

Caffe C4格式 (NC4HW4)

```csharp
MNN.Unity.MNNDimensionType.TensorFlow = 0
```

TensorFlow格式 (NHWC)

```csharp
MNN.Unity.MNNErrorCode.CallBackStop = 11
```

回调停止

```csharp
MNN.Unity.MNNErrorCode.ComputeSizeError = 3
```

计算大小错误

```csharp
MNN.Unity.MNNErrorCode.InputDataError = 10
```

输入数据错误

```csharp
MNN.Unity.MNNErrorCode.InvalidValue = 5
```

无效值

```csharp
MNN.Unity.MNNErrorCode.NoError = 0
```

无错误

```csharp
MNN.Unity.MNNErrorCode.NoExecution = 4
```

无执行器

```csharp
MNN.Unity.MNNErrorCode.NotSupport = 2
```

不支持的操作

```csharp
MNN.Unity.MNNErrorCode.OutOfMemory = 1
```

内存不足

```csharp
MNN.Unity.MNNErrorCode.TensorNeedDivide = 21
```

张量需要分割

```csharp
MNN.Unity.MNNErrorCode.TensorNotSupport = 20
```

张量不支持

```csharp
MNN.Unity.MNNGpuMemoryMode.Buffer = 64
```

Buffer模式

```csharp
MNN.Unity.MNNGpuMemoryMode.Image = 128
```

Image模式

```csharp
MNN.Unity.MNNGpuTuningMode.Fast = 16
```

快速调优

```csharp
MNN.Unity.MNNGpuTuningMode.Heavy = 2
```

重度调优

```csharp
MNN.Unity.MNNGpuTuningMode.None = 1
```

无调优

```csharp
MNN.Unity.MNNGpuTuningMode.Normal = 8
```

普通调优

```csharp
MNN.Unity.MNNGpuTuningMode.Wide = 4
```

广泛调优 (推荐)

```csharp
MNN.Unity.MNNMemoryMode.High = 1
```

高内存模式

```csharp
MNN.Unity.MNNMemoryMode.Low = 2
```

低内存模式

```csharp
MNN.Unity.MNNMemoryMode.Normal = 0
```

普通内存模式

```csharp
MNN.Unity.MNNPowerMode.High = 1
```

高功耗

```csharp
MNN.Unity.MNNPowerMode.Low = 2
```

低功耗

```csharp
MNN.Unity.MNNPowerMode.Normal = 0
```

普通功耗

```csharp
MNN.Unity.MNNPrecisionMode.High = 1
```

高精度（具体计算类型由后端实现决定）

```csharp
MNN.Unity.MNNPrecisionMode.Low = 2
```

低精度 (FP16)

```csharp
MNN.Unity.MNNPrecisionMode.LowBF16 = 3
```

低精度 (BF16)

```csharp
MNN.Unity.MNNPrecisionMode.Normal = 0
```

后端默认精度（Metal 可使用 FP16）

```csharp
MNN.Unity.MNNSessionMode.Debug = 0
```

调试模式 (支持回调)

```csharp
MNN.Unity.MNNSessionMode.InputInside = 3
```

输入在内部

```csharp
MNN.Unity.MNNSessionMode.InputOutside = 2
```

输入在外部

```csharp
MNN.Unity.MNNSessionMode.OutputInside = 5
```

输出在内部

```csharp
MNN.Unity.MNNSessionMode.OutputOutside = 4
```

输出在外部

```csharp
MNN.Unity.MNNSessionMode.Release = 1
```

发布模式 (不支持回调)

```csharp
public const string MNN.Unity.MNNSystemDiagnostics.Unavailable = "Unavailable · platform / runtime does not expose this metric"
```

```csharp
MNN.Unity.MNNTensorMapType.Read = 1
```

读取映射

```csharp
MNN.Unity.MNNTensorMapType.Write = 0
```

写入映射

```csharp
void MNN.Unity.IMNNSecretStore.Delete(string alias)
```

```csharp
string MNN.Unity.IMNNSecretStore.Read(string alias)
```

```csharp
void MNN.Unity.IMNNSecretStore.Write(string alias, string secret)
```

```csharp
public static float[] MNN.Unity.MNNAudioResampling.Resample(float[] audio, int source, int target, System.Threading.CancellationToken token = default(System.Threading.CancellationToken), int tailSamples = 0)
```

Return independently owned PCM at the target rate. tailSamples limits output to its last samples; zero keeps the entire signal. Rates must be 8–96 kHz; input must contain finite PCM.

```csharp
public MNN.Unity.MNNBackendType.MNNBackendType()
```

```csharp
public MNN.Unity.MNNDataType.MNNDataType()
```

```csharp
public MNN.Unity.MNNDimensionType.MNNDimensionType()
```

```csharp
public MNN.Unity.MNNErrorCode.MNNErrorCode()
```

```csharp
public MNN.Unity.MNNException.MNNException(MNN.Unity.MNNErrorCode errorCode, string message)
```

创建MNN异常

```csharp
public MNN.Unity.MNNException.MNNException(MNN.Unity.MNNErrorCode errorCode, string message, System.Exception innerException)
```

创建MNN异常

```csharp
public static void MNN.Unity.MNNException.ThrowIfError(int errorCode, string operation)
```

检查错误码并抛出异常

```csharp
public MNN.Unity.MNNFftPlan.MNNFftPlan(int length, bool preferBurst = true)
```

```csharp
public void MNN.Unity.MNNFftPlan.Transform(double[] real, double[] imaginary, bool inverse = false)
```

Forward transform is unscaled; inverse divides by Length. Arrays must be distinct. A plan may be shared between threads using separate input arrays.

```csharp
public MNN.Unity.MNNGpuMemoryMode.MNNGpuMemoryMode()
```

```csharp
public MNN.Unity.MNNGpuTuningMode.MNNGpuTuningMode()
```

```csharp
public static MNN.Unity.MNNHeadphoneStatus MNN.Unity.MNNHeadphoneDetection.Detect()
```

Main thread. Queries documented OS APIs; never opens an audio device or guesses from device names.

```csharp
public MNN.Unity.MNNHeadphoneStatus.MNNHeadphoneStatus()
```

```csharp
public MNN.Unity.MNNHeadphoneStatus.MNNHeadphoneStatus(bool known, bool connected, string detail)
```

```csharp
public MNN.Unity.MNNJsonSettingsStore<T>.MNNJsonSettingsStore(string path, System.Action<T> validate, int maximumBytes = 1048576)
```

```csharp
public T MNN.Unity.MNNJsonSettingsStore<T>.Load()
```

```csharp
public void MNN.Unity.MNNJsonSettingsStore<T>.Save(T settings)
```

```csharp
public MNN.Unity.MNNMemoryMode.MNNMemoryMode()
```

```csharp
public MNN.Unity.MNNMemorySecretStore.MNNMemorySecretStore()
```

```csharp
public void MNN.Unity.MNNMemorySecretStore.Delete(string alias)
```

```csharp
public string MNN.Unity.MNNMemorySecretStore.Read(string alias)
```

```csharp
public void MNN.Unity.MNNMemorySecretStore.Write(string alias, string secret)
```

```csharp
public static float MNN.Unity.MNNMotion.ProjectDrawer(float position, float velocity, UnityEngine.Vector2 range, float coastTime)
```

```csharp
public static float MNN.Unity.MNNMotion.RubberBand(float distance, float limit = 28)
```

Asymptotic resistance in logical points; stretching never exceeds the limit.

```csharp
public static void MNN.Unity.MNNMotion.Spring(ref float position, ref float velocity, float target, float deltaTime, float frequency = 18, float damping = 0.82)
```

```csharp
public static void MNN.Unity.MNNMotion.Spring(ref UnityEngine.Vector3 position, ref UnityEngine.Vector3 velocity, UnityEngine.Vector3 target, float omega, float damping, float dt)
```

```csharp
public static void MNN.Unity.MNNMotion.SpringRect(ref UnityEngine.Rect bounds, ref UnityEngine.Vector4 velocity, UnityEngine.Rect target, float deltaTime, float frequency = 30, float damping = 0.99)
```

Retarget a rectangle without restarting its position or size velocity.

```csharp
public MNN.Unity.MNNPasswordSecretStore.MNNPasswordSecretStore(string directory, string passphrase)
```

```csharp
public void MNN.Unity.MNNPasswordSecretStore.Delete(string alias)
```

```csharp
public string MNN.Unity.MNNPasswordSecretStore.Read(string alias)
```

```csharp
public void MNN.Unity.MNNPasswordSecretStore.Write(string alias, string secret)
```

```csharp
public MNN.Unity.MNNPowerMode.MNNPowerMode()
```

```csharp
public MNN.Unity.MNNPrecisionMode.MNNPrecisionMode()
```

```csharp
public static string MNN.Unity.MNNProtectedSecrets.Decrypt(byte[] envelope, string passphrase)
```

```csharp
public static byte[] MNN.Unity.MNNProtectedSecrets.Encrypt(string plaintext, string passphrase)
```

```csharp
public MNN.Unity.MNNScreenSpaceFreeRegionSolver.MNNScreenSpaceFreeRegionSolver()
```

```csharp
public void MNN.Unity.MNNScreenSpaceFreeRegionSolver.Exclude(UnityEngine.Rect occupied, float padding = 0)
```

```csharp
public void MNN.Unity.MNNScreenSpaceFreeRegionSolver.Reset(UnityEngine.Rect viewport)
```

```csharp
public static string MNN.Unity.MNNSecretStore.AliasHash(string alias)
```

```csharp
public static MNN.Unity.IMNNSecretStore MNN.Unity.MNNSecretStore.Create(string applicationId, string directory)
```

```csharp
public static void MNN.Unity.MNNSecretStore.WriteAtomic(string path, byte[] data)
```

```csharp
public MNN.Unity.MNNSessionMode.MNNSessionMode()
```

```csharp
public MNN.Unity.MNNSoftKeyboard.MNNSoftKeyboard()
```

```csharp
public void MNN.Unity.MNNSoftKeyboard.Dispose()
```

```csharp
public static UnityEngine.Rect MNN.Unity.MNNSoftKeyboard.MapOcclusion(UnityEngine.Rect keyboard, UnityEngine.Rect client, UnityEngine.Vector2 viewport)
```

Map top-left desktop/Android coordinates into a bottom-left Unity client viewport.

```csharp
public bool MNN.Unity.MNNSoftKeyboard.Read(out UnityEngine.Rect? area)
```

```csharp
public static bool MNN.Unity.MNNSystemAppearance.IsDark(bool fallback = true)
```

```csharp
public MNN.Unity.MNNSystemDiagnostics.MNNSystemDiagnostics()
```

```csharp
public static string MNN.Unity.MNNSystemDiagnostics.Bytes(long value)
```

```csharp
public void MNN.Unity.MNNSystemDiagnostics.Dispose()
```

```csharp
public bool MNN.Unity.MNNSystemDiagnostics.Tick(float delta, double now)
```

```csharp
public MNN.Unity.MNNTensorMapType.MNNTensorMapType()
```

```csharp
public static float MNN.Unity.MNNVectorSimilarity.Cosine(float[] left, float[] right)
```

```csharp
public static string MNN.Unity.MNNVersion.GetVersion()
```

获取MNN版本字符串

```csharp
public static bool MNN.Unity.MNNVersion.IsLoaded()
```

检查MNN是否正常加载

```csharp
public static void MNN.Unity.MNNVersion.LogInfo()
```

打印MNN信息到控制台

```csharp
public static byte[] MNN.Unity.MNNWaveAudio.EncodePcm16(float[] samples, int sampleRate)
```

Bounded mono PCM16 WAV for speech endpoints; no disk roundtrip.

```csharp
public static float[] MNN.Unity.MNNWaveAudio.Read(string path, out int sampleRate)
```

```csharp
public static float[][] MNN.Unity.MNNWaveAudio.ReadChannels(string path, out int sampleRate, bool requireNormalized)
```

```csharp
bool MNN.Unity.IMNNSecretStore.CanPersist { get; }
```

```csharp
string MNN.Unity.IMNNSecretStore.Protection { get; }
```

```csharp
public MNN.Unity.MNNErrorCode MNN.Unity.MNNException.ErrorCode { get; }
```

错误码

```csharp
public static bool MNN.Unity.MNNFftPlan.IsBurstAvailable { get; }
```

```csharp
public int MNN.Unity.MNNFftPlan.Length { get; }
```

```csharp
public bool MNN.Unity.MNNFftPlan.PreferBurst { get; set; }
```

```csharp
public bool MNN.Unity.MNNFftPlan.UsesBurst { get; }
```

```csharp
public bool MNN.Unity.MNNHeadphoneStatus.Connected { get; }
```

```csharp
public string MNN.Unity.MNNHeadphoneStatus.Detail { get; }
```

```csharp
public bool MNN.Unity.MNNHeadphoneStatus.IsKnown { get; }
```

```csharp
public static MNN.Unity.MNNHeadphoneStatus MNN.Unity.MNNHeadphoneStatus.Unknown { get; }
```

```csharp
public bool MNN.Unity.MNNMemorySecretStore.CanPersist { get; }
```

```csharp
public string MNN.Unity.MNNMemorySecretStore.Protection { get; }
```

```csharp
public bool MNN.Unity.MNNPasswordSecretStore.CanPersist { get; }
```

```csharp
public string MNN.Unity.MNNPasswordSecretStore.Protection { get; }
```

```csharp
public static string MNN.Unity.MNNPlatformSupport.AbiContract { get; }
```

The compiler/runtime ABI implemented by this package, not a portable MNN C# ABI.

```csharp
public static bool MNN.Unity.MNNPlatformSupport.IsSupported { get; }
```

```csharp
public static string MNN.Unity.MNNPlatformSupport.UnsupportedReason { get; }
```

```csharp
public UnityEngine.Rect MNN.Unity.MNNScreenSpaceFreeRegionSolver.LargestRegion { get; }
```

```csharp
public string MNN.Unity.MNNSystemDiagnostics.ApplicationInfo { get; private set; }
```

```csharp
public string MNN.Unity.MNNSystemDiagnostics.Device { get; }
```

```csharp
public string MNN.Unity.MNNSystemDiagnostics.Display { get; private set; }
```

```csharp
public string MNN.Unity.MNNSystemDiagnostics.Graphics { get; }
```

```csharp
public string MNN.Unity.MNNSystemDiagnostics.Memory { get; private set; }
```

```csharp
public string MNN.Unity.MNNSystemDiagnostics.Network { get; }
```

```csharp
public string MNN.Unity.MNNSystemDiagnostics.Performance { get; private set; }
```

```csharp
public string MNN.Unity.MNNSystemDiagnostics.SampleTime { get; private set; }
```

```csharp
interface MNN.Unity.IMNNSecretStore
```

```csharp
class MNN.Unity.MNNAudioResampling
```

Managed mono PCM resampling shared by applications and task frontends.

```csharp
enum MNN.Unity.MNNBackendType
```

MNN后端类型

```csharp
enum MNN.Unity.MNNDataType
```

MNN数据类型

```csharp
enum MNN.Unity.MNNDimensionType
```

MNN张量维度类型

```csharp
enum MNN.Unity.MNNErrorCode
```

MNN错误码

```csharp
class MNN.Unity.MNNException
```

MNN运行时异常

```csharp
class MNN.Unity.MNNFftPlan
```

Reusable in-place complex FFT. Radix-2 is used for power-of-two lengths; lengths composed of factors 2, 3, 5 and 7 use an allocation-free Bluestein convolution. An optional Burst adapter accelerates the radix-2 kernel.

```csharp
enum MNN.Unity.MNNGpuMemoryMode
```

GPU内存模式

```csharp
enum MNN.Unity.MNNGpuTuningMode
```

GPU调优模式

```csharp
class MNN.Unity.MNNHeadphoneDetection
```

```csharp
readonly struct MNN.Unity.MNNHeadphoneStatus
```

Output-route evidence, not a sensor indicating whether a person is wearing a device.

```csharp
class MNN.Unity.MNNJsonSettingsStore<T> where T : class
```

Atomic, size-bounded nonsecret JSON preferences, usable by Inspector or a runtime settings UI.

```csharp
enum MNN.Unity.MNNMemoryMode
```

MNN内存模式

```csharp
class MNN.Unity.MNNMemorySecretStore
```

```csharp
class MNN.Unity.MNNMotion
```

Analytic, velocity-preserving springs, independent of frame rate and time scale.

```csharp
class MNN.Unity.MNNPasswordSecretStore
```

Portable opt-in vault. The application must request its passphrase at each launch, never bundle it.

```csharp
class MNN.Unity.MNNPlatformSupport
```

Availability of the implemented Interpreter/Session/Tensor C++ ABI binding, not certification of a model, native binary, graphics backend or device.

```csharp
enum MNN.Unity.MNNPowerMode
```

MNN功耗模式

```csharp
enum MNN.Unity.MNNPrecisionMode
```

MNN精度模式

```csharp
class MNN.Unity.MNNProtectedSecrets
```

Versioned encrypt-then-MAC envelope; independent random salt/IV and separated AES/HMAC keys.

```csharp
class MNN.Unity.MNNScreenSpaceFreeRegionSolver
```

Reusable bounded search for an axis-aligned empty region. Coordinates may be pixels or logical units. Buffers are retained; no allocations after construction.

```csharp
class MNN.Unity.MNNSecretStore
```

Application credentials. Unsupported systems default to memory, never plaintext PlayerPrefs.

```csharp
enum MNN.Unity.MNNSessionMode
```

会话模式

```csharp
class MNN.Unity.MNNSoftKeyboard
```

Keyboard occlusion in Unity screen pixels; never guesses a fixed keyboard height.

```csharp
class MNN.Unity.MNNSystemAppearance
```

Reads public OS appearance preferences. No native plugin or custom exports.

```csharp
class MNN.Unity.MNNSystemDiagnostics
```

Main-thread Unity sampling; network enumeration runs independently without probing servers.

```csharp
enum MNN.Unity.MNNTensorMapType
```

张量映射类型

```csharp
class MNN.Unity.MNNVectorSimilarity
```

Cosine similarity for finite nonzero vectors in the same embedding space.

```csharp
class MNN.Unity.MNNVersion
```

MNN版本信息和基础功能

```csharp
class MNN.Unity.MNNWaveAudio
```

## Components

```csharp
public System.Action MNN.Unity.MNNHarnessBackendSetup.Changed
```

```csharp
public static System.Action<string> MNN.Unity.MNNModelManager.OnModelLoaded
```

```csharp
public static System.Action<string> MNN.Unity.MNNModelManager.OnModelRegistered
```

全局事件

```csharp
public static System.Action<string> MNN.Unity.MNNModelManager.OnModelUnloaded
```

```csharp
public System.Action<MNN.Unity.MNNRealtimeSessionState, System.Exception> MNN.Unity.MNNRealtimeSessionComponent.StateChanged
```

```csharp
public bool MNN.Unity.MNNInferenceComponent.autoCreateSession
```

```csharp
public bool MNN.Unity.MNNInferenceComponent.denormalizeOutput
```

```csharp
public UnityEngine.Texture2D MNN.Unity.MNNInferenceComponent.inputTexture
```

```csharp
public bool MNN.Unity.MNNInferenceComponent.normalizeInput
```

```csharp
public MNN.Unity.InferenceCompleteEvent MNN.Unity.MNNInferenceComponent.onInferenceComplete
```

```csharp
public MNN.Unity.InferenceErrorEvent MNN.Unity.MNNInferenceComponent.onInferenceError
```

```csharp
public MNN.Unity.InferenceEvent MNN.Unity.MNNInferenceComponent.onInferenceStart
```

```csharp
public UnityEngine.Texture2D MNN.Unity.MNNInferenceComponent.outputTexture
```

```csharp
public MNN.Unity.MNNSessionConfig MNN.Unity.MNNInferenceComponent.sessionConfig
```

```csharp
public int MNN.Unity.MNNModelLoader.downloadTimeout
```

```csharp
public bool MNN.Unity.MNNModelLoader.loadOnStart
```

```csharp
public MNN.Unity.MNNModelConfig MNN.Unity.MNNModelLoader.modelConfig
```

```csharp
public MNN.Unity.ModelLoadCompleteEvent MNN.Unity.MNNModelLoader.onLoadComplete
```

```csharp
public MNN.Unity.ModelLoadErrorEvent MNN.Unity.MNNModelLoader.onLoadError
```

```csharp
public MNN.Unity.ModelLoadProgressEvent MNN.Unity.MNNModelLoader.onLoadProgress
```

```csharp
public MNN.Unity.ModelLoadEvent MNN.Unity.MNNModelLoader.onLoadStart
```

```csharp
public bool MNN.Unity.MNNModelLoader.preferLocalCache
```

```csharp
public bool MNN.Unity.MNNModelLoader.useProjectDeliverySettings
```

```csharp
public bool MNN.Unity.MNNModelManager.enableDebugLog
```

```csharp
public bool MNN.Unity.MNNModelManager.initializeOnAwake
```

```csharp
public System.Collections.Generic.List<MNN.Unity.MNNModelConfig> MNN.Unity.MNNModelManager.managedModels
```

```csharp
public MNN.Unity.MNNModelConfig MNN.Unity.ModelRuntimeInfo.config
```

```csharp
public long MNN.Unity.ModelRuntimeInfo.fileSize
```

```csharp
public bool MNN.Unity.ModelRuntimeInfo.isAvailable
```

```csharp
public bool MNN.Unity.ModelRuntimeInfo.isLoaded
```

```csharp
public System.DateTime MNN.Unity.ModelRuntimeInfo.loadedTime
```

```csharp
public string MNN.Unity.ModelRuntimeInfo.localPath
```

```csharp
public System.DateTime MNN.Unity.ModelRuntimeInfo.registeredTime
```

```csharp
public MNN.Unity.InferenceCompleteEvent.InferenceCompleteEvent()
```

```csharp
public MNN.Unity.InferenceErrorEvent.InferenceErrorEvent()
```

```csharp
public MNN.Unity.InferenceEvent.InferenceEvent()
```

```csharp
public MNN.Unity.MNNHarnessBackendSetup.MNNHarnessBackendSetup()
```

```csharp
public void MNN.Unity.MNNHarnessBackendSetup.Configure(string runtime = null, string assembly = null, string serviceDirectory = null)
```

```csharp
public System.Threading.Tasks.Task<string> MNN.Unity.MNNHarnessBackendSetup.InstallAsync(string backend)
```

```csharp
public System.Threading.Tasks.Task<MNN.Samples.SiriAI.Harnesses.HarnessLocalSetup> MNN.Unity.MNNHarnessBackendSetup.PrepareAsync(string backend, string workspace, int port = 0, string endpoint = null, string model = null, string apiKey = null)
```

```csharp
public string MNN.Unity.MNNHarnessBackendSetup.ReadAccessToken(MNN.Samples.SiriAI.Harnesses.HarnessLocalSetup setup)
```

```csharp
public string MNN.Unity.MNNHarnessBackendSetup.ReadSavedAccessToken(string scope)
```

```csharp
public void MNN.Unity.MNNHarnessBackendSetup.RefreshEnvironment()
```

```csharp
public System.Threading.Tasks.Task<MNN.Samples.SiriAI.Harnesses.HarnessInstallation[]> MNN.Unity.MNNHarnessBackendSetup.ScanAsync()
```

```csharp
public System.Threading.Tasks.Task<string> MNN.Unity.MNNHarnessBackendSetup.StartHostAsync()
```

```csharp
public MNN.Unity.MNNInferenceComponent.MNNInferenceComponent()
```

```csharp
public MNN.Unity.MNNTensor MNN.Unity.MNNInferenceComponent.GetInputTensor()
```

获取输入张量（专业用户）

```csharp
public MNN.Unity.MNNTensor MNN.Unity.MNNInferenceComponent.GetOutputTensor()
```

获取输出张量（专业用户）

```csharp
public void MNN.Unity.MNNInferenceComponent.RunInference()
```

运行推理（同步）

```csharp
public System.Collections.IEnumerator MNN.Unity.MNNInferenceComponent.RunInferenceAsync()
```

运行推理（异步）

```csharp
public void MNN.Unity.MNNInferenceComponent.SetInput(UnityEngine.Texture2D texture)
```

手动设置输入

```csharp
public MNN.Unity.MNNModelLoader.MNNModelLoader()
```

```csharp
public void MNN.Unity.MNNModelLoader.LoadModel()
```

手动加载模型

```csharp
public System.Collections.IEnumerator MNN.Unity.MNNModelLoader.LoadModelAsync()
```

异步加载模型

```csharp
public void MNN.Unity.MNNModelLoader.UnloadModel()
```

卸载模型

```csharp
public MNN.Unity.MNNModelManager.MNNModelManager()
```

```csharp
public void MNN.Unity.MNNModelManager.CleanupUnusedModels()
```

清理未使用的模型文件

```csharp
public static System.Threading.Tasks.Task<MNN.Unity.MNNHuggingFacePage> MNN.Unity.MNNModelManager.FetchHuggingFaceCatalogPageAsync(string cursor = null, System.Threading.CancellationToken token = default(System.Threading.CancellationToken))
```

Fetch one catalog page, following the previous page's cursor when provided.

```csharp
public static System.Threading.Tasks.Task<MNN.Unity.MNNModelScopePage> MNN.Unity.MNNModelManager.FetchModelScopeCatalogPageAsync(int page, System.Threading.CancellationToken token = default(System.Threading.CancellationToken))
```

Fetch one catalog page without loading or downloading model weights.

```csharp
public MNN.Unity.MNNInterpreter MNN.Unity.MNNModelManager.GetLoadedModel(string modelId)
```

获取已加载的模型

```csharp
public string[] MNN.Unity.MNNModelManager.GetLoadedModelIds()
```

获取所有已加载的模型ID

```csharp
public MNN.Unity.ModelRuntimeInfo MNN.Unity.MNNModelManager.GetModelInfo(string modelId)
```

获取模型信息

```csharp
public string[] MNN.Unity.MNNModelManager.GetRegisteredModelIds()
```

获取所有注册的模型ID

```csharp
public void MNN.Unity.MNNModelManager.Initialize()
```

初始化管理器

```csharp
public bool MNN.Unity.MNNModelManager.IsModelLoaded(string modelId)
```

检查模型是否已加载

```csharp
public MNN.Unity.MNNInterpreter MNN.Unity.MNNModelManager.LoadModel(string modelId)
```

加载模型（同步）

```csharp
public System.Threading.Tasks.Task<MNN.Unity.MNNInterpreter> MNN.Unity.MNNModelManager.LoadModelAsync(string modelId, System.Threading.CancellationToken token = default(System.Threading.CancellationToken))
```

Prepares a model using project delivery settings before native loading. Call on the Unity main thread.

```csharp
public void MNN.Unity.MNNModelManager.RegisterModel(MNN.Unity.MNNModelConfig config)
```

注册模型

```csharp
public static System.Threading.Tasks.Task<MNN.Unity.MNNHuggingFacePage> MNN.Unity.MNNModelManager.SearchHuggingFaceCatalogPageAsync(int stream, string cursor, string query = null, System.Threading.CancellationToken token = default(System.Threading.CancellationToken))
```

```csharp
public static System.Threading.Tasks.Task<MNN.Unity.MNNModelScopePage> MNN.Unity.MNNModelManager.SearchModelScopeCatalogPageAsync(int stream, int page, string query = null, System.Threading.CancellationToken token = default(System.Threading.CancellationToken))
```

```csharp
public void MNN.Unity.MNNModelManager.UnloadAllModels()
```

卸载所有模型

```csharp
public void MNN.Unity.MNNModelManager.UnloadModel(string modelId)
```

卸载模型

```csharp
public MNN.Unity.MNNRealtimeAudioDevice.MNNRealtimeAudioDevice()
```

```csharp
public void MNN.Unity.MNNRealtimeAudioDevice.Bind(MNN.Unity.MNNRealtimeOutputPipeline output, MNN.Unity.MNNPcmFormat captureFormat, int captureQueueBlocks = 16, int maximumCaptureSamplesPerBlock = 4096)
```

Control thread only. Allocates bounded capture storage; call before starting capture callbacks.

```csharp
public double MNN.Unity.MNNRealtimeAudioDevice.GetUnityDspBufferMilliseconds()
```

Approximate Unity DSP buffering, available before pipeline binding. Hardware and operating-system tail are not included.

```csharp
public void MNN.Unity.MNNRealtimeAudioDevice.MarkCaptureDiscontinuity()
```

Capture producer only. Marks a recording-ring overrun or source discontinuity.

```csharp
public void MNN.Unity.MNNRealtimeAudioDevice.RenderOutputPcm(float[] destination)
```

Called by Unity's audio thread. Fills every requested sample without locks or allocation.

```csharp
public void MNN.Unity.MNNRealtimeAudioDevice.StartOutput()
```

Control thread only. Unity pulls PCM at its actual DSP filter callback, with no streaming-clip pre-read.

```csharp
public void MNN.Unity.MNNRealtimeAudioDevice.StopOutput()
```

Control thread only. Interrupts queued output and restores the AudioSource configuration.

```csharp
public bool MNN.Unity.MNNRealtimeAudioDevice.TryReadCapture(float[] destination, int offset, out MNN.Unity.MNNAudioFrameInfo info, out int count)
```

Single processing consumer only. The caller reuses destination storage.

```csharp
public bool MNN.Unity.MNNRealtimeAudioDevice.TrySubmitCapture(float[] samples, int offset, int count, long monotonicTicks)
```

Capture adapter callback. Timestamp the first sample frame in current-process Stopwatch ticks, using the same monotonic clock as output observations, rather than the enqueue time. One producer only; this method copies into preallocated storage and never waits for the consumer.

```csharp
public MNN.Unity.MNNRealtimeSessionComponent.MNNRealtimeSessionComponent()
```

```csharp
public void MNN.Unity.MNNRealtimeSessionComponent.Configure(MNN.Unity.IMNNRealtimeSessionRuntime runtime, MNN.Unity.MNNRealtimeOutputPipeline output, MNN.Unity.MNNRealtimeReport preflight)
```

Injects the sole session runtime and shared output pipeline before starting.

```csharp
public void MNN.Unity.MNNRealtimeSessionComponent.Configure(MNN.Unity.MNNRealtimeFallbackRuntime runtime)
```

Injects fallback through the same session owner, using its shared output and live route report.

```csharp
public System.Threading.Tasks.Task MNN.Unity.MNNRealtimeSessionComponent.InterruptAsync()
```

Interrupts the current response without stopping microphone input or the session.

```csharp
public System.Threading.Tasks.Task MNN.Unity.MNNRealtimeSessionComponent.StartSessionAsync(System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken))
```

Starts the configured session. The returned task can be awaited by application control code.

```csharp
public System.Threading.Tasks.Task MNN.Unity.MNNRealtimeSessionComponent.StopSessionAsync()
```

Closes the response gate immediately, then cancels, joins and releases the runtime asynchronously.

```csharp
public MNN.Unity.MNNSpeechOutputFilter.MNNSpeechOutputFilter()
```

```csharp
public void MNN.Unity.MNNSpeechOutputFilter.Configure(MNN.Unity.MNNRealtimeOutputPipeline output, double presentationTailMilliseconds = 0)
```

```csharp
public System.Threading.Tasks.Task MNN.Unity.MNNSpeechOutputFilter.FadeOutAsync()
```

```csharp
public void MNN.Unity.MNNSpeechOutputFilter.SetPresentationTail(double milliseconds)
```

```csharp
public MNN.Unity.MNNUnityAudioDeviceBinding.MNNUnityAudioDeviceBinding(MNN.Unity.MNNUnityMicrophoneCapture microphone, MNN.Unity.MNNRealtimeAudioDevice playback, System.Func<bool> active = null)
```

```csharp
public override System.IDisposable MNN.Unity.MNNUnityAudioDeviceBinding.BindEchoReference(MNN.Unity.MNNEchoCancellationWorker worker)
```

```csharp
public override void MNN.Unity.MNNUnityAudioDeviceBinding.BindOutput(MNN.Unity.MNNRealtimeOutputPipeline output, MNN.Unity.MNNPcmFormat captureFormat, int capacity)
```

```csharp
public override System.Threading.Tasks.Task MNN.Unity.MNNUnityAudioDeviceBinding.FadeOutAsync()
```

```csharp
public override System.Threading.Tasks.Task MNN.Unity.MNNUnityAudioDeviceBinding.FlushCaptureAsync(System.Threading.CancellationToken token)
```

```csharp
public override MNN.Unity.MNNRealtimeDeviceSnapshot MNN.Unity.MNNUnityAudioDeviceBinding.ReadState()
```

```csharp
public override System.Threading.Tasks.Task MNN.Unity.MNNUnityAudioDeviceBinding.RequestPermissionAsync(System.Threading.CancellationToken token)
```

```csharp
public override void MNN.Unity.MNNUnityAudioDeviceBinding.ResetForRetry()
```

```csharp
public override void MNN.Unity.MNNUnityAudioDeviceBinding.SetCaptureEnabled(bool enabled)
```

```csharp
public override void MNN.Unity.MNNUnityAudioDeviceBinding.SetPresentationTail(double milliseconds)
```

```csharp
public override void MNN.Unity.MNNUnityAudioDeviceBinding.SetSpeechVolume(float volume)
```

```csharp
public override void MNN.Unity.MNNUnityAudioDeviceBinding.StartCapture(System.Action<MNN.Unity.MNNPcmFormat, int> negotiated)
```

```csharp
public override void MNN.Unity.MNNUnityAudioDeviceBinding.StartPlayback()
```

```csharp
public override void MNN.Unity.MNNUnityAudioDeviceBinding.StopCapture()
```

```csharp
public override void MNN.Unity.MNNUnityAudioDeviceBinding.StopPlayback()
```

```csharp
public override void MNN.Unity.MNNUnityAudioDeviceBinding.ValidateFresh()
```

```csharp
public MNN.Unity.MNNUnityEchoReferenceTap.MNNUnityEchoReferenceTap()
```

```csharp
public void MNN.Unity.MNNUnityEchoReferenceTap.Bind(MNN.Unity.MNNEchoCancellationWorker capture, int sampleRate, double dspBufferMilliseconds)
```

```csharp
public void MNN.Unity.MNNUnityEchoReferenceTap.Detach(MNN.Unity.MNNEchoCancellationWorker capture)
```

```csharp
public MNN.Unity.MNNUnityMicrophoneCapture.MNNUnityMicrophoneCapture()
```

```csharp
public System.Threading.Tasks.Task MNN.Unity.MNNUnityMicrophoneCapture.FlushCaptureAsync(System.Threading.CancellationToken token)
```

Main thread. Publish all samples captured at this recording boundary, waiting for bounded queue backpressure without stopping physical recording.

```csharp
public void MNN.Unity.MNNUnityMicrophoneCapture.PollCapture(float now, long nowTicks)
```

Poll capture on the Unity main thread when driven by an Editor host. Player Update polls automatically.

```csharp
public void MNN.Unity.MNNUnityMicrophoneCapture.StartCapture(MNN.Unity.MNNRealtimeAudioDevice device, string deviceName = null, int requestedSampleRate = 48000, int clipLengthSeconds = 1, int minimumDelayMilliseconds = 30)
```

Starts a Unity microphone ring buffer. Permission must already be granted by the owning application. No AudioSource is needed for recording.

```csharp
public void MNN.Unity.MNNUnityMicrophoneCapture.StartCaptureNegotiated(MNN.Unity.MNNRealtimeAudioDevice device, System.Action<MNN.Unity.MNNPcmFormat, int> prepareCapture, string deviceName = null, int requestedSampleRate = 48000, int clipLengthSeconds = 1, int minimumDelayMilliseconds = 30)
```

Negotiates the raw microphone clip format before polling starts. The callback must bind the supplied device to this format and at least the reported sample capacity; runs on Unity's main thread.

```csharp
public void MNN.Unity.MNNUnityMicrophoneCapture.StopCapture()
```

Stops microphone recording without changing the owner's AudioSource. Control thread only.

```csharp
public MNN.Unity.ModelLoadCompleteEvent.ModelLoadCompleteEvent()
```

```csharp
public MNN.Unity.ModelLoadErrorEvent.ModelLoadErrorEvent()
```

```csharp
public MNN.Unity.ModelLoadEvent.ModelLoadEvent()
```

```csharp
public MNN.Unity.ModelLoadProgressEvent.ModelLoadProgressEvent()
```

```csharp
public MNN.Unity.ModelRuntimeInfo.ModelRuntimeInfo()
```

```csharp
public string MNN.Unity.ModelRuntimeInfo.GetStatusString()
```

```csharp
public bool MNN.Unity.MNNHarnessBackendSetup.AutomaticDetection { get; set; }
```

```csharp
public bool MNN.Unity.MNNHarnessBackendSetup.DetectionCompleted { get; private set; }
```

```csharp
public string MNN.Unity.MNNHarnessBackendSetup.HostAssemblyPath { get; private set; }
```

```csharp
public MNN.Samples.SiriAI.Harnesses.HarnessInstallation[] MNN.Unity.MNNHarnessBackendSetup.Installations { get; private set; }
```

```csharp
public bool MNN.Unity.MNNHarnessBackendSetup.IsBusy { get; private set; }
```

```csharp
public string MNN.Unity.MNNHarnessBackendSetup.RecommendedBackend { get; }
```

```csharp
public string MNN.Unity.MNNHarnessBackendSetup.RuntimePath { get; private set; }
```

```csharp
public string MNN.Unity.MNNHarnessBackendSetup.ServiceDirectory { get; private set; }
```

```csharp
public bool MNN.Unity.MNNInferenceComponent.IsInferring { get; }
```

是否正在推理

```csharp
public bool MNN.Unity.MNNInferenceComponent.IsReady { get; }
```

是否准备好推理

```csharp
public MNN.Unity.MNNSession MNN.Unity.MNNInferenceComponent.Session { get; }
```

当前会话

```csharp
public MNN.Unity.MNNInterpreter MNN.Unity.MNNModelLoader.Interpreter { get; }
```

```csharp
public bool MNN.Unity.MNNModelLoader.IsLoaded { get; }
```

```csharp
public string MNN.Unity.MNNModelLoader.LoadedModelPath { get; }
```

```csharp
public static MNN.Unity.MNNModelManager MNN.Unity.MNNModelManager.Instance { get; }
```

单例实例

```csharp
public bool MNN.Unity.MNNRealtimeAudioDevice.CanSubmitCapture { get; }
```

```csharp
public bool MNN.Unity.MNNRealtimeAudioDevice.CaptureEnabled { get; set; }
```

Gate before startup/stop without moving the SPSC reader. Reopening marks a capture gap.

```csharp
public MNN.Unity.MNNPcmFormat MNN.Unity.MNNRealtimeAudioDevice.CaptureFormat { get; }
```

```csharp
public long MNN.Unity.MNNRealtimeAudioDevice.DroppedCaptureBlocks { get; }
```

```csharp
public System.Exception MNN.Unity.MNNRealtimeAudioDevice.Fault { get; }
```

```csharp
public bool MNN.Unity.MNNRealtimeAudioDevice.HasPendingCaptureFrames { get; }
```

```csharp
public bool MNN.Unity.MNNRealtimeAudioDevice.IsOutputRunning { get; }
```

```csharp
public int MNN.Unity.MNNRealtimeAudioDevice.MaximumCaptureSamples { get; }
```

```csharp
public long MNN.Unity.MNNRealtimeAudioDevice.RenderedOutputFrames { get; }
```

Rendered source frames, not speaker presentation. Reset by Bind before callbacks start.

```csharp
public System.Exception MNN.Unity.MNNRealtimeSessionComponent.Fault { get; }
```

```csharp
public bool MNN.Unity.MNNRealtimeSessionComponent.IsConfigured { get; }
```

```csharp
public MNN.Unity.MNNRealtimeOutputPipeline MNN.Unity.MNNRealtimeSessionComponent.Output { get; }
```

```csharp
public MNN.Unity.MNNRealtimeReport MNN.Unity.MNNRealtimeSessionComponent.Preflight { get; }
```

```csharp
public MNN.Unity.MNNRealtimeSession MNN.Unity.MNNRealtimeSessionComponent.Session { get; }
```

```csharp
public MNN.Unity.MNNRealtimeSessionState MNN.Unity.MNNRealtimeSessionComponent.State { get; }
```

```csharp
public bool MNN.Unity.MNNSpeechOutputFilter.MayBeAudible { get; }
```

```csharp
public MNN.Unity.MNNPlaybackWaveform MNN.Unity.MNNSpeechOutputFilter.Waveform { get; private set; }
```

```csharp
public override MNN.Unity.IMNNAudioCaptureSource MNN.Unity.MNNUnityAudioDeviceBinding.Capture { get; }
```

```csharp
public override System.Exception MNN.Unity.MNNUnityAudioDeviceBinding.Fault { get; }
```

```csharp
public int MNN.Unity.MNNUnityAudioDeviceBinding.MaximumInputSampleRate { get; set; }
```

```csharp
public string MNN.Unity.MNNUnityAudioDeviceBinding.MicrophoneDevice { get; set; }
```

```csharp
public override MNN.Unity.MNNPcmFormat MNN.Unity.MNNUnityAudioDeviceBinding.OutputFormat { get; }
```

```csharp
public override int MNN.Unity.MNNUnityAudioDeviceBinding.ReferenceBlockFrames { get; }
```

```csharp
public override bool MNN.Unity.MNNUnityAudioDeviceBinding.SpeechMayBeAudible { get; }
```

```csharp
public override float? MNN.Unity.MNNUnityAudioDeviceBinding.SpeechVolume { get; }
```

```csharp
public override MNN.Unity.MNNPlaybackWaveform MNN.Unity.MNNUnityAudioDeviceBinding.Waveform { get; }
```

```csharp
public long MNN.Unity.MNNUnityMicrophoneCapture.CaptureLagFrames { get; }
```

```csharp
public double MNN.Unity.MNNUnityMicrophoneCapture.CaptureLagMilliseconds { get; }
```

```csharp
public long MNN.Unity.MNNUnityMicrophoneCapture.CapturedFrames { get; }
```

```csharp
public string MNN.Unity.MNNUnityMicrophoneCapture.DeviceName { get; }
```

```csharp
public System.Exception MNN.Unity.MNNUnityMicrophoneCapture.Fault { get; }
```

```csharp
public bool MNN.Unity.MNNUnityMicrophoneCapture.IsCapturing { get; }
```

```csharp
public bool MNN.Unity.MNNUnityMicrophoneCapture.IsWaitingForBuffer { get; }
```

```csharp
public float MNN.Unity.MNNUnityMicrophoneCapture.LastBlockRms { get; }
```

```csharp
public long MNN.Unity.MNNUnityMicrophoneCapture.OverrunFrames { get; }
```

```csharp
class MNN.Unity.InferenceCompleteEvent
```

```csharp
class MNN.Unity.InferenceErrorEvent
```

```csharp
class MNN.Unity.InferenceEvent
```

```csharp
class MNN.Unity.MNNHarnessBackendSetup
```

Reusable desktop backend setup. Discovery is read-only; installation and host startup are explicit.

```csharp
class MNN.Unity.MNNInferenceComponent
```

MNN推理组件 - 快速上手版 封装了完整的推理流程，开发者无需关心细节

```csharp
class MNN.Unity.MNNModelLoader
```

MNN模型加载器（运行时） 提供模型下载、缓存和加载功能 健壮性设计：即使缺少UnityWebRequest也能基本工作

```csharp
class MNN.Unity.MNNModelManager
```

MNN模型管理器（运行时） 集中管理所有模型的生命周期、路径和状态

```csharp
class MNN.Unity.MNNRealtimeAudioDevice
```

Unity audio callback adapter for the realtime PCM ports. Capture is pushed by one device adapter; processing and model workers pull it independently. This component does not provide capture or AEC.

```csharp
class MNN.Unity.MNNRealtimeSessionComponent
```

Owns one realtime session across Unity lifecycle events; concrete backends are injected by the application.

```csharp
class MNN.Unity.MNNSpeechOutputFilter
```

Unity streaming-clip post-filter, downstream of the official managed audio device.

```csharp
class MNN.Unity.MNNUnityAudioDeviceBinding
```

Unity microphone/AudioSource primitives for the shared device session. Contains no application preferences or UI policy.

```csharp
class MNN.Unity.MNNUnityEchoReferenceTap
```

Final listener mix: includes speech fade, source gain and Unity cue sounds.

```csharp
class MNN.Unity.MNNUnityMicrophoneCapture
```

Unity-managed microphone capture. The recording ring is read directly on the main thread into bounded PCM blocks, independently of speaker mixing, muting or voice virtualization. It does not provide echo cancellation or device certification.

```csharp
class MNN.Unity.ModelLoadCompleteEvent
```

```csharp
class MNN.Unity.ModelLoadErrorEvent
```

```csharp
class MNN.Unity.ModelLoadEvent
```

```csharp
class MNN.Unity.ModelLoadProgressEvent
```

```csharp
class MNN.Unity.ModelRuntimeInfo
```

模型运行时信息

## Generation

```csharp
public const int MNN.Unity.MNNCosyVoice300M.OutputSampleRate = 22050
```

```csharp
MNN.Unity.MNNCosyVoice300MDecoderMode.ExperimentalFfnInt8 = 2
```

```csharp
MNN.Unity.MNNCosyVoice300MDecoderMode.ExperimentalInt8 = 1
```

```csharp
MNN.Unity.MNNCosyVoice300MDecoderMode.Float16 = 0
```

```csharp
public const int MNN.Unity.MNNOmniVoice.SampleRate = 24000
```

```csharp
MNN.Unity.MNNSpeechModelKind.BertVits2 = 5
```

```csharp
MNN.Unity.MNNSpeechModelKind.CosyVoice = 6
```

```csharp
MNN.Unity.MNNSpeechModelKind.CosyVoice300M = 16
```

```csharp
MNN.Unity.MNNSpeechModelKind.DhVaani = 12
```

```csharp
MNN.Unity.MNNSpeechModelKind.FireRedTts3 = 8
```

```csharp
MNN.Unity.MNNSpeechModelKind.IndexTts25 = 7
```

```csharp
MNN.Unity.MNNSpeechModelKind.IndicF5 = 15
```

```csharp
MNN.Unity.MNNSpeechModelKind.IndicVits = 4
```

```csharp
MNN.Unity.MNNSpeechModelKind.Kokoro = 3
```

```csharp
MNN.Unity.MNNSpeechModelKind.MmsTamil = 9
```

```csharp
MNN.Unity.MNNSpeechModelKind.MossNano = 11
```

```csharp
MNN.Unity.MNNSpeechModelKind.Piper = 2
```

```csharp
MNN.Unity.MNNSpeechModelKind.Pocket = 14
```

```csharp
MNN.Unity.MNNSpeechModelKind.Qwen3Explicit = 13
```

```csharp
MNN.Unity.MNNSpeechModelKind.SherpaVitsChinese = 10
```

```csharp
MNN.Unity.MNNSpeechModelKind.Supertonic = 1
```

```csharp
MNN.Unity.MNNSpeechModelKind.Unknown = 0
```

```csharp
public const int MNN.Unity.MNNStableSpeechTextSegmenter.MaximumInputDeltaCharacters = 4096
```

```csharp
public void MNN.Unity.MNNBertVits2.Dispose()
```

```csharp
public static MNN.Unity.MNNBertVits2 MNN.Unity.MNNBertVits2.Load(string directory, int threads = 1, MNN.Unity.MNNBackendType backendType = MNN.Unity.MNNBackendType.Auto)
```

```csharp
public MNN.Unity.MNNGeneratedAudio MNN.Unity.MNNBertVits2.Synthesize(string text, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken))
```

```csharp
public MNN.Unity.MNNCosyVoice.MNNCosyVoice()
```

```csharp
public void MNN.Unity.MNNCosyVoice.Dispose()
```

```csharp
public static MNN.Unity.MNNCosyVoice MNN.Unity.MNNCosyVoice.Load(string directory, int threads = 4, MNN.Unity.MNNBackendType backendType = MNN.Unity.MNNBackendType.Auto)
```

```csharp
public static string MNN.Unity.MNNCosyVoice.ResolveDirectory(string directory)
```

```csharp
public static bool MNN.Unity.MNNCosyVoice.SupportsRepository(string repository)
```

```csharp
public MNN.Unity.MNNGeneratedAudio MNN.Unity.MNNCosyVoice.Synthesize(string text, int seed = 42, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken))
```

```csharp
public int MNN.Unity.MNNCosyVoice.SynthesizeStreaming(string text, System.Action<float[], int, bool> onPcm, int seed = 42, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken))
```

Generates the existing bounded text chunks one at a time and publishes each decoded waveform as soon as it is ready. This keeps the model single-owner while allowing the playback pipeline to start the first chunk before later chunks finish. The callback is invoked synchronously on the synthesis worker and must not retain the waveform.

```csharp
public MNN.Unity.MNNCosyVoice300M.MNNCosyVoice300M()
```

```csharp
public void MNN.Unity.MNNCosyVoice300M.Dispose()
```

```csharp
public static MNN.Unity.MNNCosyVoice300M MNN.Unity.MNNCosyVoice300M.Load(string directory, int threads = 4, MNN.Unity.MNNBackendType acousticBackend = MNN.Unity.MNNBackendType.Auto, bool experimentalQuantizedDecoder = false, MNN.Unity.MNNCosyVoice300MDecoderMode decoderMode = MNN.Unity.MNNCosyVoice300MDecoderMode.Float16, int metalShapeCacheCapacity = 4)
```

Full prompt, FP16 weights and High computation by default. Quantized decode is explicit research opt-in; Python timings do not describe this managed runner.

```csharp
public MNN.Unity.MNNGeneratedAudio MNN.Unity.MNNCosyVoice300M.Synthesize(string text, int seed = 42, int flowSteps = 10, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken))
```

Generates one complete utterance. Ten Flow steps and the full reference prompt are retained by default. Smaller step counts need separate quality evaluation.

```csharp
public MNN.Unity.MNNCosyVoice300MDecoderMode.MNNCosyVoice300MDecoderMode()
```

```csharp
public static string MNN.Unity.MNNCosyVoiceAssets.FileHash(string path, System.Threading.CancellationToken token = default(System.Threading.CancellationToken))
```

```csharp
public static string MNN.Unity.MNNCosyVoiceAssets.Hash(string path, System.Threading.CancellationToken token = default(System.Threading.CancellationToken))
```

```csharp
public static void MNN.Unity.MNNCosyVoiceAssets.Install(string stagingPath, System.Threading.CancellationToken token)
```

```csharp
public static void MNN.Unity.MNNCosyVoiceAssets.InstallArchive(string stagingPath, string sha256, System.Collections.Generic.IDictionary<string, long> required, System.Threading.CancellationToken token = default(System.Threading.CancellationToken), System.Action<long> onExtracted = null)
```

Compatibility entry point for the official complete CosyVoice archive. Its pinned archive SHA establishes provenance; existing callers provide the exact file sizes.

```csharp
public void MNN.Unity.MNNDhVaani.Dispose()
```

```csharp
public static MNN.Unity.MNNDhVaani MNN.Unity.MNNDhVaani.Load(string directory, int threads = 4)
```

```csharp
public MNN.Unity.MNNGeneratedAudio MNN.Unity.MNNDhVaani.Synthesize(string text, MNN.Unity.MNNReferenceAudio reference, string referenceText, int steps = 8, float speed = 1, int seed = 666, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken))
```

```csharp
public System.Threading.Tasks.Task<MNN.Unity.MNNGeneratedAudio> MNN.Unity.MNNDhVaani.SynthesizeAsync(string text, MNN.Unity.MNNReferenceAudio reference, string referenceText, int steps = 8, float speed = 1, int seed = 666, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken))
```

```csharp
public void MNN.Unity.MNNFireRedTts3.Dispose()
```

```csharp
public static MNN.Unity.MNNFireRedTts3 MNN.Unity.MNNFireRedTts3.Load(string directory, int threads = 4)
```

```csharp
public MNN.Unity.MNNGeneratedAudio MNN.Unity.MNNFireRedTts3.Synthesize(string text, MNN.Unity.MNNReferenceAudio reference, string referenceText, string language = "zh", int seed = 42, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken))
```

Reference must be 0.5–15 seconds. Its exact transcript is required. Languages: zh/en.

```csharp
public MNN.Unity.MNNGeneratedImage.MNNGeneratedImage(byte[] rgb, int width, int height, string prompt, int seed)
```

Create an RGB24 result. The caller transfers ownership of the supplied pixel array.

```csharp
public void MNN.Unity.MNNIndexTts25.Dispose()
```

```csharp
public static MNN.Unity.MNNIndexTts25 MNN.Unity.MNNIndexTts25.Load(string directory, int threads = 4)
```

```csharp
public MNN.Unity.MNNGeneratedAudio MNN.Unity.MNNIndexTts25.Synthesize(string text, MNN.Unity.MNNReferenceAudio reference, string language = "zh", int seed = 42, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken))
```

Plain Chinese/English text, at most 120 BPE tokens; write numbers as spoken words. Reference: 0.5–15 seconds.

```csharp
public void MNN.Unity.MNNIndicF5Tts.Dispose()
```

```csharp
public static MNN.Unity.MNNIndicF5Tts MNN.Unity.MNNIndicF5Tts.Load(string directory, int threads = 4)
```

```csharp
public MNN.Unity.MNNGeneratedAudio MNN.Unity.MNNIndicF5Tts.Synthesize(string text, MNN.Unity.MNNReferenceAudio reference, string referenceText, int seed = 42, int steps = 32, float speed = 1, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken))
```

```csharp
public System.Threading.Tasks.Task<MNN.Unity.MNNGeneratedAudio> MNN.Unity.MNNIndicF5Tts.SynthesizeAsync(string text, MNN.Unity.MNNReferenceAudio reference, string referenceText, int seed = 42, int steps = 32, float speed = 1, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken))
```

```csharp
public void MNN.Unity.MNNIndicVits.Dispose()
```

```csharp
public static MNN.Unity.MNNIndicVits MNN.Unity.MNNIndicVits.Load(string directory, int threads = 4)
```

```csharp
public MNN.Unity.MNNGeneratedAudio MNN.Unity.MNNIndicVits.Synthesize(string text, int speakerId = 18, int emotionId = 0, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken))
```

```csharp
public System.Threading.Tasks.Task<MNN.Unity.MNNGeneratedAudio> MNN.Unity.MNNIndicVits.SynthesizeAsync(string text, int speakerId = 18, int emotionId = 0, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken))
```

```csharp
public void MNN.Unity.MNNKokoro.Dispose()
```

```csharp
public static string[] MNN.Unity.MNNKokoro.InstalledVoices(string directory)
```

```csharp
public static MNN.Unity.MNNKokoro MNN.Unity.MNNKokoro.Load(string directory, int threads = 4)
```

```csharp
public MNN.Unity.MNNGeneratedAudio MNN.Unity.MNNKokoro.Synthesize(string text, string voice = null, float speed = 1, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken))
```

```csharp
public System.Threading.Tasks.Task<MNN.Unity.MNNGeneratedAudio> MNN.Unity.MNNKokoro.SynthesizeAsync(string text, string voice = null, float speed = 1, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken))
```

```csharp
public MNN.Unity.MNNLocalSpeechFallback.MNNLocalSpeechFallback(string preferredDirectory, int sampleRate, int threads = 1, string[] modelRoots = null)
```

```csharp
public void MNN.Unity.MNNLocalSpeechFallback.Dispose()
```

```csharp
public MNN.Unity.MNNGeneratedAudio MNN.Unity.MNNLocalSpeechFallback.Synthesize(string text, string[] preferredLanguages, System.Func<MNN.Unity.MNNGeneratedAudio> preferred, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken))
```

```csharp
public void MNN.Unity.MNNMmsTamil.Dispose()
```

```csharp
public static string[] MNN.Unity.MNNMmsTamil.InstalledPrecisions(string directory)
```

```csharp
public static MNN.Unity.MNNMmsTamil MNN.Unity.MNNMmsTamil.Load(string directory, int threads = 4, string precision = "int8")
```

```csharp
public MNN.Unity.MNNGeneratedAudio MNN.Unity.MNNMmsTamil.Synthesize(string text, float speed = 1, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken))
```

```csharp
public System.Threading.Tasks.Task<MNN.Unity.MNNGeneratedAudio> MNN.Unity.MNNMmsTamil.SynthesizeAsync(string text, float speed = 1, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken))
```

```csharp
public void MNN.Unity.MNNMossNano.Dispose()
```

```csharp
public static string[] MNN.Unity.MNNMossNano.InstalledVoices(string directory)
```

```csharp
public static MNN.Unity.MNNMossNano MNN.Unity.MNNMossNano.Load(string directory, int threads = 4)
```

```csharp
public MNN.Unity.MNNGeneratedAudio MNN.Unity.MNNMossNano.Synthesize(string text, string voice = "Junhao", int seed = 1234, int maxFrames = 375, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken))
```

```csharp
public System.Threading.Tasks.Task<MNN.Unity.MNNGeneratedAudio> MNN.Unity.MNNMossNano.SynthesizeAsync(string text, string voice = "Junhao", int seed = 1234, int maxFrames = 375, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken))
```

```csharp
public MNN.Unity.MNNOmniVoicePrompt MNN.Unity.MNNOmniVoice.CreateVoicePrompt(MNN.Unity.MNNReferenceAudio reference, string transcript, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken))
```

Encode a 0.5-15 second mono recording once. Supply its matching transcript; 3-10 seconds is recommended.

```csharp
public void MNN.Unity.MNNOmniVoice.Dispose()
```

```csharp
public static MNN.Unity.MNNOmniVoice MNN.Unity.MNNOmniVoice.Load(string directory, int threads = 4, MNN.Unity.MNNBackendType lmBackend = MNN.Unity.MNNBackendType.Auto, MNN.Unity.MNNPrecisionMode precision = MNN.Unity.MNNPrecisionMode.High, bool enablePackedCfg = true)
```

```csharp
public static MNN.Unity.MNNOmniVoice MNN.Unity.MNNOmniVoice.LoadCompact(string directory, int threads = 4, MNN.Unity.MNNBackendType lmBackend = MNN.Unity.MNNBackendType.Auto)
```

Use the optional compact graph with High precision and packed CFG; shares the original external weights.

```csharp
public MNN.Unity.MNNGeneratedAudio MNN.Unity.MNNOmniVoice.Synthesize(string text, float durationSeconds, MNN.Unity.MNNOmniVoicePrompt voice = null, string language = "zh", string instruction = null, int seed = 47, int steps = 32, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken))
```

Synthesize up to 15 seconds. Duration sets the exact number of 25 Hz codec frames before postprocessing.

```csharp
public System.Threading.Tasks.Task<MNN.Unity.MNNGeneratedAudio> MNN.Unity.MNNOmniVoice.SynthesizeAsync(string text, float durationSeconds, MNN.Unity.MNNOmniVoicePrompt voice = null, string language = "zh", string instruction = null, int seed = 47, int steps = 32, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken))
```

```csharp
public void MNN.Unity.MNNPiper.Dispose()
```

```csharp
public static string MNN.Unity.MNNPiper.FindEspeak(string directory = null)
```

```csharp
public static string[] MNN.Unity.MNNPiper.InstalledVoices(string directory)
```

```csharp
public static bool MNN.Unity.MNNPiper.IsEspeakExecutable(string path)
```

```csharp
public static MNN.Unity.MNNPiper MNN.Unity.MNNPiper.Load(string directory, int threads = 1, string espeakExecutable = null, string espeakDataDirectory = null, MNN.Unity.MNNBackendType backendType = MNN.Unity.MNNBackendType.Auto)
```

```csharp
public static string MNN.Unity.MNNPiper.ResolveDataDirectory(string directory, string configured = null, string selected = null, bool requireEnglish = true)
```

```csharp
public static string MNN.Unity.MNNPiper.ResolveEspeak(string requested, string directory = null)
```

```csharp
public MNN.Unity.MNNGeneratedAudio MNN.Unity.MNNPiper.Synthesize(string text, string voice = null, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken))
```

```csharp
public void MNN.Unity.MNNPocketTts.Dispose()
```

```csharp
public static MNN.Unity.MNNPocketTts MNN.Unity.MNNPocketTts.Load(string directory, int threads = 4)
```

```csharp
public static MNN.Unity.MNNPocketTts MNN.Unity.MNNPocketTts.Load(string directory, int threads, MNN.Unity.MNNPrecisionMode mainPrecision)
```

Select Normal or opt-in Low precision for the autoregressive main graph only. Low may change generated speech; qualify content, voice quality and throughput on the target device. Other stages retain Normal precision. Neither option guarantees realtime delivery.

```csharp
public MNN.Unity.MNNGeneratedAudio MNN.Unity.MNNPocketTts.Synthesize(string text, MNN.Unity.MNNReferenceAudio reference, int seed = 42, int maxFrames = 375, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken))
```

Generate up to 30 seconds from at most 50 English tokens and a 0.5–15 second reference WAV.

```csharp
public System.Threading.Tasks.Task<MNN.Unity.MNNGeneratedAudio> MNN.Unity.MNNPocketTts.SynthesizeAsync(string text, MNN.Unity.MNNReferenceAudio reference, int seed = 42, int maxFrames = 375, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken))
```

```csharp
public int MNN.Unity.MNNPocketTts.SynthesizeStreaming(string text, MNN.Unity.MNNReferenceAudio reference, System.Action<float[], int, int, bool> onPcmChunk, int seed = 42, int maxFrames = 375, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken))
```

Generate speech and synchronously publish each decoded 80 ms PCM chunk as soon as it is available. The callback runs on the synthesis worker while this model is exclusively owned; copy or enqueue the samples before returning, do not retain the array, re-enter this model, or call Unity APIs. The final callback receives endsSegment=true. Returns the number of generated mono PCM samples.

```csharp
public void MNN.Unity.MNNQwen3ExplicitTts.Dispose()
```

```csharp
public static string[] MNN.Unity.MNNQwen3ExplicitTts.InstalledVoices(string directory)
```

Names of valid prepared voice folders under voices/. Raw WAV files are not voice profiles.

```csharp
public static MNN.Unity.MNNQwen3ExplicitTts MNN.Unity.MNNQwen3ExplicitTts.Load(string directory, int threads = 4, bool cacheCodePredictorShapes = false)
```

```csharp
public MNN.Unity.MNNGeneratedAudio MNN.Unity.MNNQwen3ExplicitTts.Synthesize(string text, string language = "zh", int seed = 42, int maxFrames = 360, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken), string voice = null)
```

Synthesizes Chinese or English with a prepared voice (demo by default), at 24 kHz mono. The export does not include a raw-WAV voice encoder. A frame limit without EOS is an error.

```csharp
public System.Threading.Tasks.Task<MNN.Unity.MNNGeneratedAudio> MNN.Unity.MNNQwen3ExplicitTts.SynthesizeAsync(string text, string language = "zh", int seed = 42, int maxFrames = 360, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken), string voice = null)
```

```csharp
public MNN.Unity.MNNRealtimeSpeechStream.MNNRealtimeSpeechStream(MNN.Unity.MNNRealtimeOutputPipeline output, MNN.Unity.MNNResponseId response, MNN.Unity.MNNRealtimeSynthesisMeter synthesisMeter = null)
```

```csharp
public MNN.Unity.MNNRealtimeSpeechStream.MNNRealtimeSpeechStream(MNN.Unity.MNNRealtimeOutputPipeline output, MNN.Unity.MNNResponseId response, System.TimeSpan maximumBackpressureWait, MNN.Unity.MNNRealtimeSynthesisMeter synthesisMeter = null)
```

Limits each continuous PCM publication stall. Timeout.InfiniteTimeSpan is for offline processing. This does not bound synthesis calls, idle text waits, or audio already submitted to a device.

```csharp
public bool MNN.Unity.MNNRealtimeSpeechStream.BeginSegment(MNN.Unity.MNNRealtimeSpeechTextSegment segment)
```

Starts a segment read from the response's bounded model-text channel.

```csharp
public bool MNN.Unity.MNNRealtimeSpeechStream.BeginSegment(long segmentId, string stableSpokenText)
```

Register immutable spoken text before the first audio block for this segment.

```csharp
public bool MNN.Unity.MNNRealtimeSpeechStream.CompleteResponse()
```

Closes production after all segment producers have emitted their final PCM blocks.

```csharp
public bool MNN.Unity.MNNRealtimeSpeechStream.Produce(MNN.Unity.MNNRealtimeSpeechTextChannel textChannel, MNN.Unity.MNNPcmFormat synthesisFormat, System.Action<string, System.Action<float[], int, int, bool>, System.Threading.CancellationToken> synthesize, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken))
```

Blocking, single-use TTS owner loop. Call on a worker, never a Unity or audio callback thread. The synthesizer must synchronously emit borrowed PCM, mark its last block and honor cancellation; it must not retain the callback. Format must match playback; resample upstream if necessary. Splits blocks without accumulating a waveform and retries queue backpressure with short polling waits. Returns false when interrupted or output faults; cancellation and backend failures propagate to the control runtime, which must invalidate output and await quiescence before disposing or reusing the backend. Throws TimeoutException when PCM publication stays blocked beyond MaximumBackpressureWait.

```csharp
public System.Threading.Tasks.Task<bool> MNN.Unity.MNNRealtimeSpeechStream.ProduceAsync(MNN.Unity.MNNRealtimeSpeechTextChannel textChannel, MNN.Unity.MNNPcmFormat format, System.Func<string, System.Func<float[], int, int, bool, System.Threading.Tasks.Task>, System.Threading.CancellationToken, System.Threading.Tasks.Task> synthesize, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken))
```

Async single-owner equivalent of Produce for network synthesizers. Callbacks are awaited in order; buffers are borrowed until the callback completes. No Unity or device callback is blocked. RTF includes synthesis/network wait and excludes text wait and playback backpressure.

```csharp
public void MNN.Unity.MNNRealtimeSpeechStream.RecordSynthesisWork(long computeTicks, int generatedFrames)
```

Record decoder work once per generated block; generatedFrames uses the playback sample rate.

```csharp
public bool MNN.Unity.MNNRealtimeSpeechStream.TryPublishPcm(float[] samples, int offset, int count, bool endsSegment)
```

Publish a block now. On false, retry only while IsCurrent remains true; otherwise abandon this response. A race with interruption is resolved by the output response gate.

```csharp
public MNN.Unity.MNNRealtimeSpeechTextChannel.MNNRealtimeSpeechTextChannel(MNN.Unity.MNNRealtimeOutputPipeline output, MNN.Unity.MNNResponseId response, int capacity = 2, int maximumSegmentCharacters = 180)
```

```csharp
public bool MNN.Unity.MNNRealtimeSpeechTextChannel.Append(string textDelta)
```

Model owner only. The delta is consumed exactly once even when false is returned; pause text generation and call DrainPending after the TTS owner frees queue capacity. If IsCurrent is false, abandon this channel instead of retrying a stale response.

```csharp
public System.Threading.Tasks.Task<bool> MNN.Unity.MNNRealtimeSpeechTextChannel.AppendAsync(string text, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken))
```

Model owner only. Appends text in bounded deltas and awaits consumer backpressure. Returns false if this response becomes stale. Await each call before appending again.

```csharp
public bool MNN.Unity.MNNRealtimeSpeechTextChannel.Complete()
```

Model owner only. EOS may be retried after backpressure; no text is silently discarded.

```csharp
public bool MNN.Unity.MNNRealtimeSpeechTextChannel.DrainPending()
```

Model owner only. Retry stable text retained after queue backpressure without re-appending model text.

```csharp
public bool MNN.Unity.MNNRealtimeSpeechTextChannel.TryRead(out MNN.Unity.MNNRealtimeSpeechTextSegment segment)
```

TTS owner only. Old-response segments are returned with their immutable response ID for gate checks.

```csharp
public void MNN.Unity.MNNRealtimeSpeechTextChannel.WaitForData(System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken))
```

Blocking equivalent for a dedicated worker, never an audio callback.

```csharp
public System.Threading.Tasks.Task MNN.Unity.MNNRealtimeSpeechTextChannel.WaitForDataAsync(System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken))
```

Wait without polling until text, completion, cancellation or response interruption.

```csharp
public MNN.Unity.MNNRealtimeSpeechTextSegment.MNNRealtimeSpeechTextSegment()
```

```csharp
public MNN.Unity.MNNRealtimeSynthesisMeter.MNNRealtimeSynthesisMeter(double minimumWindowAudioSeconds = 1)
```

```csharp
public void MNN.Unity.MNNRealtimeSynthesisMeter.Record(long computeTicks, int generatedFrames, int sampleRate)
```

Record completed synthesis work once per generated chunk, including chunks awaiting queue retry.

```csharp
public MNN.Unity.MNNReferenceAudio.MNNReferenceAudio(float[] samples, int sampleRate)
```

```csharp
public static MNN.Unity.MNNReferenceAudio MNN.Unity.MNNReferenceAudio.FromWaveFile(string path)
```

Read RIFF WAV: PCM16/24/32 or IEEE float32, mono or stereo (mixed to mono).

```csharp
public void MNN.Unity.MNNSana.Dispose()
```

```csharp
public MNN.Unity.MNNGeneratedImage MNN.Unity.MNNSana.Edit(string prompt, byte[] referenceRgb, int steps = 10, int seed = 42, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken), System.Action<int> progress = null, float guidance = 4.5)
```

Edit a top-left RGB24 image already resized to 512×512.

```csharp
public static MNN.Unity.MNNSana MNN.Unity.MNNSana.Load(string directory, int threads = 4, MNN.Unity.MNNBackendType backendType = MNN.Unity.MNNBackendType.Auto)
```

```csharp
public void MNN.Unity.MNNSherpaVitsChinese.Dispose()
```

```csharp
public static string[] MNN.Unity.MNNSherpaVitsChinese.InstalledVoices(string directory)
```

```csharp
public static MNN.Unity.MNNSherpaVitsChinese MNN.Unity.MNNSherpaVitsChinese.Load(string directory, int threads = 4)
```

```csharp
public MNN.Unity.MNNGeneratedAudio MNN.Unity.MNNSherpaVitsChinese.Synthesize(string text, int speakerId = 0, float speed = 1, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken))
```

```csharp
public System.Threading.Tasks.Task<MNN.Unity.MNNGeneratedAudio> MNN.Unity.MNNSherpaVitsChinese.SynthesizeAsync(string text, int speakerId = 0, float speed = 1, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken))
```

```csharp
public MNN.Unity.MNNSpeechLanguageException.MNNSpeechLanguageException(string message)
```

```csharp
public static bool MNN.Unity.MNNSpeechLanguages.HasSpeech(string text)
```

```csharp
public static string MNN.Unity.MNNSpeechLanguages.Normalize(string language)
```

```csharp
public static string[] MNN.Unity.MNNSpeechLanguages.Scripts(string text)
```

Distinct writing systems, retaining all scripts in mixed text.

```csharp
public static string MNN.Unity.MNNSpeechLanguages.Select(string text, string[] languages, string preferred = null)
```

```csharp
public static string[] MNN.Unity.MNNSpeechLanguages.SplitScriptRuns(string text)
```

Preserves every character and groups adjacent writing systems; punctuation stays with its preceding run.

```csharp
public static bool MNN.Unity.MNNSpeechLanguages.SupportsText(string text, params string[] languages)
```

```csharp
public MNN.Unity.MNNSpeechModelKind.MNNSpeechModelKind()
```

```csharp
public MNN.Unity.MNNSpeechSynthesisOptions.MNNSpeechSynthesisOptions()
```

```csharp
public static MNN.Unity.MNNSpeechModelKind MNN.Unity.MNNSpeechSynthesizer.DetectModelKind(string directory, string name = null)
```

Detect a known export protocol from local metadata. No weights are loaded or files created. Detection alone does not prove that all required assets are installed.

```csharp
public void MNN.Unity.MNNSpeechSynthesizer.Dispose()
```

```csharp
public static bool MNN.Unity.MNNSpeechSynthesizer.HasProfile(string directory)
```

Check the detected frontend's required local assets without loading weights.

```csharp
public static string[] MNN.Unity.MNNSpeechSynthesizer.InstalledSupertonicLanguages(string directory)
```

Read the languages declared by the installed Supertonic version without loading weights.

```csharp
public static string[] MNN.Unity.MNNSpeechSynthesizer.InstalledVoices(string directory)
```

Read installed voice identifiers without loading a model. The returned array belongs to the caller.

```csharp
public static bool MNN.Unity.MNNSpeechSynthesizer.IsComplete(string directory, MNN.Unity.MNNSpeechModelKind kind, string speechDataDirectory = null)
```

Check required local assets without loading weights or verifying platform support. speechDataDirectory optionally selects Piper's external eSpeak data directory.

```csharp
public static MNN.Unity.MNNSpeechSynthesizer MNN.Unity.MNNSpeechSynthesizer.Load(string directory, MNN.Unity.MNNSpeechSynthesisOptions options = null, int threadCount = 4, MNN.Unity.MNNBackendType backendType = MNN.Unity.MNNBackendType.Auto)
```

```csharp
public static bool MNN.Unity.MNNSpeechSynthesizer.RequiresReferenceAudio(MNN.Unity.MNNRemoteModel model)
```

```csharp
public static bool MNN.Unity.MNNSpeechSynthesizer.RequiresReferenceAudio(string directory)
```

```csharp
public static bool MNN.Unity.MNNSpeechSynthesizer.RequiresReferenceText(MNN.Unity.MNNRemoteModel model)
```

```csharp
public static bool MNN.Unity.MNNSpeechSynthesizer.SupportsRepository(MNN.Unity.MNNRemoteModel model)
```

```csharp
public MNN.Unity.MNNGeneratedAudio MNN.Unity.MNNSpeechSynthesizer.Synthesize(string text, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken))
```

```csharp
public MNN.Unity.MNNSpeechTextCleaner.MNNSpeechTextCleaner()
```

```csharp
public string MNN.Unity.MNNSpeechTextCleaner.Append(string delta)
```

Returns newly stable speech. Retains incomplete markup until a later delta or Complete.

```csharp
public static string MNN.Unity.MNNSpeechTextCleaner.Clean(string text)
```

Returns plain spoken text, or empty text when the input contains no speech.

```csharp
public string MNN.Unity.MNNSpeechTextCleaner.Complete()
```

Flushes remaining prose and discards unclosed code, tags and links. Call once per response.

```csharp
public void MNN.Unity.MNNStableDiffusion.Dispose()
```

```csharp
public MNN.Unity.MNNGeneratedImage MNN.Unity.MNNStableDiffusion.Generate(string prompt, string negativePrompt = "", int steps = 20, float guidance = 7.5, int seed = 42, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken), System.Action<int> progress = null)
```

```csharp
public System.Threading.Tasks.Task<MNN.Unity.MNNGeneratedImage> MNN.Unity.MNNStableDiffusion.GenerateAsync(string prompt, string negativePrompt = "", int steps = 20, float guidance = 7.5, int seed = 42, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken), System.Action<int> progress = null)
```

```csharp
public static MNN.Unity.MNNStableDiffusion MNN.Unity.MNNStableDiffusion.Load(string directory, int threads = 4, MNN.Unity.MNNBackendType backendType = MNN.Unity.MNNBackendType.Auto)
```

```csharp
public MNN.Unity.MNNStableSpeechTextSegmenter.MNNStableSpeechTextSegmenter(int maximumCharacters = 180)
```

```csharp
public bool MNN.Unity.MNNStableSpeechTextSegmenter.Append(string textDelta, System.Func<string, bool> emitStableSegment)
```

Append a model text delta and synchronously emit each newly stable segment once.

```csharp
public bool MNN.Unity.MNNStableSpeechTextSegmenter.Complete(System.Func<string, bool> emitStableSegment)
```

Emit the remaining stable text. Returns false when the consumer applies backpressure.

```csharp
public bool MNN.Unity.MNNStableSpeechTextSegmenter.DrainStableSegments(System.Func<string, bool> emitStableSegment)
```

Retry pending stable segments without appending another model delta.

```csharp
public MNN.Unity.MNNStreamingSpeechConverter.MNNStreamingSpeechConverter(int inputSampleRate, MNN.Unity.MNNPcmFormat output, System.Func<float[], int, int, bool, System.Threading.Tasks.Task> publish)
```

```csharp
public MNN.Unity.MNNStreamingSpeechConverter.MNNStreamingSpeechConverter(int inputSampleRate, MNN.Unity.MNNPcmFormat output, System.Func<float[], int, int, bool, System.Threading.Tasks.Task> publish, bool trimTrailingSilence)
```

When enabled, holds candidate silence until speech resumes or the segment ends. Only long quiet tails are shortened; internal pauses and short natural endings retain their PCM. Use a new converter for each independently synthesized text segment.

```csharp
public System.Threading.Tasks.Task MNN.Unity.MNNStreamingSpeechConverter.AppendAsync(float[] mono, int count)
```

```csharp
public System.Threading.Tasks.Task MNN.Unity.MNNStreamingSpeechConverter.CompleteAsync()
```

```csharp
public void MNN.Unity.MNNSupertonic.Dispose()
```

```csharp
public static string[] MNN.Unity.MNNSupertonic.InstalledVoices(string directory)
```

```csharp
public static string[] MNN.Unity.MNNSupertonic.LanguagesForVersion(string version)
```

```csharp
public static MNN.Unity.MNNSupertonic MNN.Unity.MNNSupertonic.Load(string directory, int threads = 4, string precision = "fp16", MNN.Unity.MNNBackendType backendType = MNN.Unity.MNNBackendType.Auto)
```

```csharp
public MNN.Unity.MNNGeneratedAudio MNN.Unity.MNNSupertonic.Synthesize(string text, string voice = "M1", int steps = 5, float speed = 1.05, int seed = 42, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken), string language = null)
```

```csharp
public System.Threading.Tasks.Task<MNN.Unity.MNNGeneratedAudio> MNN.Unity.MNNSupertonic.SynthesizeAsync(string text, string voice = "M1", int steps = 5, float speed = 1.05, int seed = 42, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken), string language = null)
```

```csharp
public int MNN.Unity.MNNBertVits2.SampleRate { get; }
```

```csharp
public System.Collections.Generic.IReadOnlyDictionary<string, MNN.Unity.MNNBackendType> MNN.Unity.MNNBertVits2.StageBackends { get; }
```

Current stage backends; native unsupported operations may still run on CPU.

```csharp
public static string MNN.Unity.MNNCosyVoice.ArchiveName { get; }
```

```csharp
public MNN.Unity.MNNBackendType MNN.Unity.MNNCosyVoice.Backend { get; }
```

Configured LLM backend. Conditioner, Flow, F0 and HiFT use CPU. Individual unsupported LLM operators may also run on CPU.

```csharp
public double MNN.Unity.MNNCosyVoice.LastFlowSeconds { get; private set; }
```

```csharp
public double MNN.Unity.MNNCosyVoice.LastTokenSeconds { get; private set; }
```

```csharp
public double MNN.Unity.MNNCosyVoice.LastVocoderSeconds { get; private set; }
```

```csharp
public int MNN.Unity.MNNCosyVoice.SampleRate { get; }
```

```csharp
public MNN.Unity.MNNBackendType MNN.Unity.MNNCosyVoice300M.DecoderBackend { get; }
```

```csharp
public MNN.Unity.MNNCosyVoice300MDecoderMode MNN.Unity.MNNCosyVoice300M.DecoderMode { get; private set; }
```

```csharp
public bool MNN.Unity.MNNCosyVoice300M.ExperimentalQuantizedDecoder { get; private set; }
```

```csharp
public MNN.Unity.MNNBackendType MNN.Unity.MNNCosyVoice300M.FlowBackend { get; }
```

```csharp
public bool MNN.Unity.MNNCosyVoice300M.LastAcousticCpuRetry { get; private set; }
```

```csharp
public double MNN.Unity.MNNCosyVoice300M.LastFlowSeconds { get; private set; }
```

```csharp
public double MNN.Unity.MNNCosyVoice300M.LastRealTimeFactor { get; private set; }
```

```csharp
public int MNN.Unity.MNNCosyVoice300M.LastSpeechTokens { get; private set; }
```

```csharp
public double MNN.Unity.MNNCosyVoice300M.LastSynthesisSeconds { get; private set; }
```

```csharp
public double MNN.Unity.MNNCosyVoice300M.LastTokenSeconds { get; private set; }
```

```csharp
public double MNN.Unity.MNNCosyVoice300M.LastVocoderSeconds { get; private set; }
```

```csharp
public int MNN.Unity.MNNCosyVoice300M.SampleRate { get; }
```

```csharp
public bool MNN.Unity.MNNCosyVoice300M.UsesCompactDecoder { get; }
```

```csharp
public MNN.Unity.MNNBackendType MNN.Unity.MNNCosyVoice300M.VocoderBackend { get; }
```

```csharp
public int MNN.Unity.MNNGeneratedAudio.SampleRate { get; }
```

```csharp
public string MNN.Unity.MNNGeneratedAudio.Text { get; }
```

```csharp
public float[] MNN.Unity.MNNGeneratedAudio.Waveform { get; }
```

```csharp
public int MNN.Unity.MNNGeneratedImage.Height { get; }
```

```csharp
public string MNN.Unity.MNNGeneratedImage.Prompt { get; }
```

```csharp
public byte[] MNN.Unity.MNNGeneratedImage.Rgb { get; }
```

RGB24 pixels, row-major from top left. Unity textures require vertically reversed rows.

```csharp
public int MNN.Unity.MNNGeneratedImage.Seed { get; }
```

```csharp
public int MNN.Unity.MNNGeneratedImage.Width { get; }
```

```csharp
public static string[] MNN.Unity.MNNIndicVits.InstalledSpeakers { get; }
```

Installed frontend speaker identifiers; each call returns an independent snapshot.

```csharp
public static int[] MNN.Unity.MNNIndicVits.InstalledStyleIds { get; }
```

```csharp
public static string[] MNN.Unity.MNNIndicVits.InstalledStyles { get; }
```

```csharp
public int MNN.Unity.MNNIndicVits.SampleRate { get; }
```

```csharp
public static string[] MNN.Unity.MNNIndicVits.SpeakerExampleTexts { get; }
```

```csharp
public string[] MNN.Unity.MNNIndicVits.Voices { get; }
```

Documented speaker labels, ordered by speaker ID (0–19).

```csharp
public int MNN.Unity.MNNKokoro.SampleRate { get; }
```

```csharp
public string[] MNN.Unity.MNNKokoro.Voices { get; }
```

```csharp
public string MNN.Unity.MNNLocalSpeechFallback.LastModelDirectory { get; private set; }
```

```csharp
public int MNN.Unity.MNNMmsTamil.SampleRate { get; }
```

```csharp
public string[] MNN.Unity.MNNMossNano.Voices { get; }
```

```csharp
public bool MNN.Unity.MNNOmniVoice.UsesCompactSampling { get; }
```

Whether the optional graph returns predicted codes and confidence instead of full logits.

```csharp
public bool MNN.Unity.MNNOmniVoice.UsesPackedCfg { get; }
```

Whether this instance packs both CFG branches into one input sequence.

```csharp
public MNN.Unity.MNNBackendType MNN.Unity.MNNPiper.Backend { get; }
```

```csharp
public string[] MNN.Unity.MNNPiper.Languages { get; }
```

```csharp
public string MNN.Unity.MNNPiper.LastSynthesisModelDirectory { get; }
```

```csharp
public int MNN.Unity.MNNPiper.SampleRate { get; }
```

```csharp
public string[] MNN.Unity.MNNPiper.Voices { get; }
```

```csharp
public int MNN.Unity.MNNPocketTts.SampleRate { get; }
```

```csharp
public static string MNN.Unity.MNNQwen3ExplicitTts.RecommendedRepository { get; }
```

```csharp
public double MNN.Unity.MNNRealtimeSpeechStream.FirstChunkLatencyMilliseconds { get; }
```

```csharp
public bool MNN.Unity.MNNRealtimeSpeechStream.IsCurrent { get; }
```

```csharp
public System.TimeSpan MNN.Unity.MNNRealtimeSpeechStream.MaximumBackpressureWait { get; }
```

```csharp
public MNN.Unity.MNNResponseId MNN.Unity.MNNRealtimeSpeechStream.Response { get; }
```

```csharp
public MNN.Unity.MNNRealtimeSynthesisMeter MNN.Unity.MNNRealtimeSpeechStream.SynthesisMeter { get; }
```

```csharp
public long MNN.Unity.MNNRealtimeSpeechTextChannel.BackpressureCount { get; }
```

```csharp
public bool MNN.Unity.MNNRealtimeSpeechTextChannel.HasQueuedSegments { get; }
```

```csharp
public bool MNN.Unity.MNNRealtimeSpeechTextChannel.IsCompleted { get; }
```

```csharp
public bool MNN.Unity.MNNRealtimeSpeechTextChannel.IsCurrent { get; }
```

```csharp
public MNN.Unity.MNNResponseId MNN.Unity.MNNRealtimeSpeechTextChannel.Response { get; }
```

```csharp
public long MNN.Unity.MNNRealtimeSpeechTextChannel.StaleAppendCount { get; }
```

```csharp
public MNN.Unity.MNNResponseId MNN.Unity.MNNRealtimeSpeechTextSegment.Response { get; }
```

```csharp
public long MNN.Unity.MNNRealtimeSpeechTextSegment.SegmentId { get; }
```

```csharp
public string MNN.Unity.MNNRealtimeSpeechTextSegment.Text { get; }
```

```csharp
public bool MNN.Unity.MNNRealtimeSynthesisMeter.HasRealtimeHeadroom { get; }
```

```csharp
public double MNN.Unity.MNNRealtimeSynthesisMeter.LifetimeRtf { get; }
```

```csharp
public double MNN.Unity.MNNRealtimeSynthesisMeter.TotalComputeMilliseconds { get; }
```

```csharp
public double MNN.Unity.MNNRealtimeSynthesisMeter.WindowAudioSeconds { get; }
```

```csharp
public int MNN.Unity.MNNRealtimeSynthesisMeter.WindowChunkCount { get; }
```

```csharp
public double MNN.Unity.MNNRealtimeSynthesisMeter.WindowRtf { get; }
```

```csharp
public double MNN.Unity.MNNReferenceAudio.Duration { get; }
```

```csharp
public int MNN.Unity.MNNReferenceAudio.SampleRate { get; }
```

```csharp
public System.Collections.Generic.IReadOnlyDictionary<string, MNN.Unity.MNNBackendType> MNN.Unity.MNNSana.StageBackends { get; }
```

Current stage backends; native unsupported operations may still run on CPU.

```csharp
public int MNN.Unity.MNNSherpaVitsChinese.SampleRate { get; }
```

```csharp
public string[] MNN.Unity.MNNSherpaVitsChinese.Voices { get; }
```

```csharp
public string MNN.Unity.MNNSpeechSynthesisOptions.Language { get; set; }
```

```csharp
public string MNN.Unity.MNNSpeechSynthesisOptions.Precision { get; set; }
```

```csharp
public string MNN.Unity.MNNSpeechSynthesisOptions.ReferenceText { get; set; }
```

```csharp
public string MNN.Unity.MNNSpeechSynthesisOptions.ReferenceWavePath { get; set; }
```

```csharp
public int MNN.Unity.MNNSpeechSynthesisOptions.Seed { get; set; }
```

```csharp
public float MNN.Unity.MNNSpeechSynthesisOptions.Speed { get; set; }
```

```csharp
public int MNN.Unity.MNNSpeechSynthesisOptions.Steps { get; set; }
```

```csharp
public string MNN.Unity.MNNSpeechSynthesisOptions.Voice { get; set; }
```

```csharp
public string[] MNN.Unity.MNNSpeechSynthesizer.Languages { get; }
```

```csharp
public int MNN.Unity.MNNSpeechSynthesizer.SampleRate { get; }
```

```csharp
public System.Collections.Generic.IReadOnlyDictionary<string, MNN.Unity.MNNBackendType> MNN.Unity.MNNStableDiffusion.StageBackends { get; }
```

Current stage backends; native unsupported operations may still run on CPU.

```csharp
public bool MNN.Unity.MNNStableSpeechTextSegmenter.IsCompleted { get; }
```

```csharp
public int MNN.Unity.MNNStableSpeechTextSegmenter.PendingCharacters { get; }
```

```csharp
public string[] MNN.Unity.MNNSupertonic.Languages { get; }
```

```csharp
public int MNN.Unity.MNNSupertonic.SampleRate { get; }
```

```csharp
public System.Collections.Generic.IReadOnlyDictionary<string, MNN.Unity.MNNBackendType> MNN.Unity.MNNSupertonic.StageBackends { get; }
```

Current stage backends; native unsupported operations may still run on CPU.

```csharp
public string MNN.Unity.MNNSupertonic.Version { get; }
```

```csharp
public string[] MNN.Unity.MNNSupertonic.Voices { get; }
```

```csharp
class MNN.Unity.MNNBertVits2
```

Chinese/English Chenxi speech from MNN's Bert-VITS2 repository. Uses managed G2P and official MNN graph APIs.

```csharp
class MNN.Unity.MNNCosyVoice
```

CosyVoice3 complete fp16 release, built-in reference voice, 24 kHz mono. Uses the bundled macOS Mono C++ ABI. Voice enrollment is not included.

```csharp
class MNN.Unity.MNNCosyVoice300M
```

First-generation CosyVoice-300M, built-in reference speaker, 22050 Hz mono. Uses official MNN APIs through the package's pinned platform ABI. No Python process is required.

```csharp
enum MNN.Unity.MNNCosyVoice300MDecoderMode
```

Explicit decoder candidates. INT8 modes retain High computation and the original FP16 prefill; complete speech quality requires evaluation.

```csharp
class MNN.Unity.MNNCosyVoiceAssets
```

```csharp
class MNN.Unity.MNNDhVaani
```

DhVaani v1.1 Hindi/English reference speech using the bounded macOS Mono MNN ABI.

```csharp
class MNN.Unity.MNNFireRedTts3
```

FireRedTTS3 reference-conditioned speech, using official MNN Interpreter graphs on CPU.

```csharp
class MNN.Unity.MNNGeneratedAudio
```

```csharp
class MNN.Unity.MNNGeneratedImage
```

```csharp
class MNN.Unity.MNNIndexTts25
```

IndexTTS-2.5 fp16 weights with FP32 CPU compute and a supplied reference voice.

```csharp
class MNN.Unity.MNNIndicF5Tts
```

Reference-conditioned IndicF5 speech on the bounded macOS Mono MNN ABI.

```csharp
class MNN.Unity.MNNIndicVits
```

Indic VITS q8 export with its character vocabulary and explicit speaker/emotion inputs.

```csharp
class MNN.Unity.MNNKokoro
```

Kokoro speech using repository IPA lexicons, voice style tables and official MNN Module APIs.

```csharp
class MNN.Unity.MNNLocalSpeechFallback
```

Reuses local models for scripts the preferred voice cannot pronounce. Never downloads models. Construction loads no native models; synthesize and dispose from one serialized owner.

```csharp
class MNN.Unity.MNNMmsTamil
```

Single-speaker Tamil MMS VITS exports with their character frontend.

```csharp
class MNN.Unity.MNNMossNano
```

MOSS Nano fixed sampled-frame export with publisher-supplied reference codes.

```csharp
class MNN.Unity.MNNOmniVoice
```

OmniVoice noncausal speech synthesis through official MNN Interpreter and Tensor APIs.

```csharp
class MNN.Unity.MNNOmniVoicePrompt
```

Reusable encoded reference for OmniVoice voice cloning.

```csharp
class MNN.Unity.MNNPiper
```

Runs MNN Piper voice graphs with the upstream eSpeak-NG phonemizer command.

```csharp
class MNN.Unity.MNNPocketTts
```

English Pocket-TTS using MNN for reference encoding, generation and decoding. CPU, macOS Mono ABI.

```csharp
class MNN.Unity.MNNQwen3ExplicitTts
```

Qwen3-TTS explicit-KV export with its bundled prepared reference voice. Requires the package's bounded macOS Mono MNN ABI contract.

```csharp
class MNN.Unity.MNNRealtimeSpeechStream
```

Bridges a streaming TTS producer to the shared live output pipeline. A producer may publish each PCM block as soon as it is decoded; it must retain and retry a block after backpressure. All methods except immutable properties belong to one TTS producer thread.

```csharp
class MNN.Unity.MNNRealtimeSpeechTextChannel
```

Per-response bounded SPSC channel from append-only model text to one TTS owner. The model owner writes; the TTS owner reads; neither may share ownership of the segmenter.

```csharp
readonly struct MNN.Unity.MNNRealtimeSpeechTextSegment
```

Immutable spoken text queued from the model owner to the TTS owner.

```csharp
class MNN.Unity.MNNRealtimeSynthesisMeter
```

Fixed-memory rolling and lifetime RTF accounting for one TTS producer.

```csharp
class MNN.Unity.MNNReferenceAudio
```

A mono reference recording. Samples are copied and must be finite normalized PCM.

```csharp
class MNN.Unity.MNNSana
```

Managed Sana Edit V2 pipeline for the ModelScope MNN model format. Uses the bundled official Interpreter/Tensor APIs, with no additional native ABI.

```csharp
class MNN.Unity.MNNSherpaVitsChinese
```

Five-speaker Chinese Sherpa VITS export with the published Bopomofo lexicon.

```csharp
class MNN.Unity.MNNSpeechLanguageException
```

A recoverable request failure: no installed speech model can pronounce this input.

```csharp
class MNN.Unity.MNNSpeechLanguages
```

Conservative script checks, not statistical language identification.

```csharp
enum MNN.Unity.MNNSpeechModelKind
```

Describes the managed TTS frontends that are currently implemented by the package. A repository being classified as SpeechSynthesis is not enough to select a frontend: each graph family has a different tokenizer and tensor contract.

```csharp
class MNN.Unity.MNNSpeechSynthesisOptions
```

Inputs shared by profiled speech synthesis. Reference paths are local WAV files.

```csharp
class MNN.Unity.MNNSpeechSynthesizer
```

Buffered synthesis over all registered TTS export protocols. Owns one task runner.

```csharp
class MNN.Unity.MNNSpeechTextCleaner
```

Per-response speech filter. Feed model deltas before sentence segmentation.

```csharp
class MNN.Unity.MNNStableDiffusion
```

Managed SD 1.5 accelerator-first pipeline for the legacy MNN general model repository. Calls the official Interpreter/Tensor APIs through the supported macOS Mono ABI.

```csharp
class MNN.Unity.MNNStableSpeechTextSegmenter
```

Turns append-only spoken text into immutable, bounded TTS segments with explicit consumer backpressure.

```csharp
class MNN.Unity.MNNStreamingSpeechConverter
```

Streaming mono synthesis → output-rate/channel conversion, with optional segment-tail trimming.

```csharp
class MNN.Unity.MNNSupertonic
```

Managed Supertonic pipeline over official MNN Interpreter/Tensor APIs. Supports Supertonic 1/2/3 exports within the package platform ABI contract.

## Inference

```csharp
public static bool MNN.Unity.MNNAcceleration.IsBackendAvailable(MNN.Unity.MNNBackendType backend)
```

```csharp
public static MNN.Unity.MNNInterpreter MNN.Unity.MNNInterpreter.CreateFromBuffer(byte[] buffer)
```

从字节数组加载模型

```csharp
public static MNN.Unity.MNNInterpreter MNN.Unity.MNNInterpreter.CreateFromFile(string modelPath)
```

从文件加载模型

```csharp
public MNN.Unity.MNNSession MNN.Unity.MNNInterpreter.CreateSession(MNN.Unity.MNNSessionConfig config = null)
```

创建推理会话

```csharp
public void MNN.Unity.MNNInterpreter.Dispose()
```

释放资源

```csharp
protected MNN.Unity.MNNInterpreter.~MNNInterpreter()
```

```csharp
public void MNN.Unity.MNNInterpreter.ReleaseModel()
```

释放模型数据（在创建所有会话后调用以节省内存）

```csharp
public void MNN.Unity.MNNInterpreter.SetCacheFile(string cacheFilePath)
```

设置GPU缓存文件（加速后续启动）

```csharp
public static System.Threading.Tasks.Task<T> MNN.Unity.MNNModelLoading.LoadAsync<T>(System.Func<T> factory, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken)) where T : System.IDisposable
```

The factory must use thread-safe model APIs and no Unity objects. Native loading cannot be interrupted; cancellation disposes the result before returning it. Exceptions from the factory propagate to the caller.

```csharp
public override string MNN.Unity.MNNNeuralDevice.ToString()
```

```csharp
public static void MNN.Unity.MNNRuntime.Initialize()
```

Capture Unity paths and device information on the Unity main thread before loading on workers. Safe to repeat after a device change. Player startup also performs this automatically.

```csharp
public void MNN.Unity.MNNSession.Dispose()
```

释放资源

```csharp
protected MNN.Unity.MNNSession.~MNNSession()
```

```csharp
public MNN.Unity.MNNTensor MNN.Unity.MNNSession.GetInput(string name = null)
```

获取输入张量

```csharp
public MNN.Unity.MNNTensor MNN.Unity.MNNSession.GetOutput(string name = null)
```

获取输出张量

```csharp
public void MNN.Unity.MNNSession.ResizeInput(string name, int[] shape)
```

调整输入张量大小

```csharp
public void MNN.Unity.MNNSession.ResizeInputs(System.Collections.Generic.IReadOnlyDictionary<string, int[]> shapes)
```

Resize related inputs together before updating graph shapes. All borrowed input and output views become invalid.

```csharp
public void MNN.Unity.MNNSession.Run()
```

运行推理（同步）

```csharp
public System.Threading.Tasks.Task MNN.Unity.MNNSession.RunAsync()
```

运行推理（异步）

```csharp
public void MNN.Unity.MNNSession.UpdateCache()
```

更新GPU缓存（在ResizeInput后调用）

```csharp
public MNN.Unity.MNNSessionConfig.MNNSessionConfig()
```

创建默认配置

```csharp
public MNN.Unity.MNNSessionConfig MNN.Unity.MNNSessionConfig.Clone()
```

克隆配置

```csharp
public static MNN.Unity.MNNSessionConfig MNN.Unity.MNNSessionConfig.CreateForCurrentPlatform()
```

为当前平台创建优化配置

```csharp
public static MNN.Unity.MNNSessionConfig MNN.Unity.MNNSessionConfig.CreateHighPerformance()
```

创建高性能配置（牺牲精度）

```csharp
public static MNN.Unity.MNNSessionConfig MNN.Unity.MNNSessionConfig.CreateHighPrecision()
```

创建高精度配置（牺牲性能）

```csharp
public static MNN.Unity.MNNSessionConfig MNN.Unity.MNNSessionConfig.CreateLowPower()
```

创建低功耗配置

```csharp
public override string MNN.Unity.MNNSessionConfig.ToString()
```

输出配置信息

```csharp
public void MNN.Unity.MNNTensor.CopyFromArray<T>(T[] data) where T : unmanaged
```

从数组拷贝数据

```csharp
public void MNN.Unity.MNNTensor.CopyFromSpan<T>(System.ReadOnlySpan<T> data) where T : unmanaged
```

从Span拷贝数据

```csharp
public T[] MNN.Unity.MNNTensor.CopyToArray<T>() where T : unmanaged
```

拷贝数据到数组

```csharp
public void MNN.Unity.MNNTensor.Dispose()
```

释放资源

```csharp
protected MNN.Unity.MNNTensor.~MNNTensor()
```

```csharp
public System.ReadOnlySpan<T> MNN.Unity.MNNTensor.MapForRead<T>() where T : unmanaged
```

映射张量内存用于读取（零拷贝）

```csharp
public System.Span<T> MNN.Unity.MNNTensor.MapForWrite<T>() where T : unmanaged
```

映射张量内存用于写入（零拷贝）

```csharp
public override string MNN.Unity.MNNTensor.ToString()
```

获取张量信息字符串

```csharp
public void MNN.Unity.MNNTensor.Unmap()
```

取消映射张量内存

```csharp
public static void MNN.Unity.MNNTensorExtensions.CopyFromTexture(this MNN.Unity.MNNTensor tensor, UnityEngine.Texture2D texture, bool normalize = true)
```

从Texture2D拷贝数据（自动选择最优实现）

```csharp
public static void MNN.Unity.MNNTensorExtensions.CopyToTexture(this MNN.Unity.MNNTensor tensor, UnityEngine.Texture2D texture, bool denormalize = true)
```

拷贝到Texture2D（自动选择最优实现）

```csharp
public static void MNN.Unity.MNNTensorExtensions.Normalize(this MNN.Unity.MNNTensor tensor, float mean, float std)
```

批量归一化（自动选择最优实现）

```csharp
public static System.Collections.Generic.IReadOnlyList<MNN.Unity.MNNNeuralDevice> MNN.Unity.MNNAcceleration.NeuralDevices { get; }
```

Read Android NNAPI drivers through documented Android C APIs. A reported accelerator is not proof that a given MNN graph is placed on it.

```csharp
public static MNN.Unity.MNNBackendType MNN.Unity.MNNAcceleration.PreferredBackend { get; }
```

```csharp
public bool MNN.Unity.MNNInterpreter.IsDisposed { get; }
```

是否已释放

```csharp
public bool MNN.Unity.MNNInterpreter.IsModelReleased { get; }
```

是否已释放模型数据

```csharp
public int MNN.Unity.MNNNeuralDevice.DeviceType { get; }
```

NDK device type: 0 unknown, 1 other, 2 CPU, 3 GPU, 4 accelerator.

```csharp
public long MNN.Unity.MNNNeuralDevice.FeatureLevel { get; }
```

```csharp
public bool MNN.Unity.MNNNeuralDevice.IsNeuralAccelerator { get; }
```

```csharp
public string MNN.Unity.MNNNeuralDevice.Name { get; }
```

```csharp
public static string MNN.Unity.MNNRuntime.GraphicsDevice { get; }
```

Captured GPU and driver identity; empty before initialization.

```csharp
public MNN.Unity.MNNBackendType MNN.Unity.MNNSession.ActualBackend { get; }
```

Main scheduled backend reported by MNN. Individual unsupported ops may use CPU.

```csharp
public MNN.Unity.MNNSessionConfig MNN.Unity.MNNSession.Config { get; }
```

会话配置

```csharp
public bool MNN.Unity.MNNSession.IsDisposed { get; }
```

是否已释放

```csharp
public MNN.Unity.MNNBackendType MNN.Unity.MNNSession.RequestedBackend { get; }
```

```csharp
public MNN.Unity.MNNBackendType MNN.Unity.MNNSessionConfig.BackendType { get; set; }
```

后端类型

```csharp
public MNN.Unity.MNNBackendType MNN.Unity.MNNSessionConfig.BackupBackendType { get; set; }
```

备用后端类型；创建会话时固定使用 CPU 处理不支持的操作

```csharp
public MNN.Unity.MNNGpuMemoryMode MNN.Unity.MNNSessionConfig.GpuMemoryMode { get; set; }
```

GPU内存模式（仅OpenCL后端有效）

```csharp
public MNN.Unity.MNNGpuTuningMode MNN.Unity.MNNSessionConfig.GpuTuningMode { get; set; }
```

GPU调优模式（Metal/OpenCL/Vulkan）

```csharp
public MNN.Unity.MNNMemoryMode MNN.Unity.MNNSessionConfig.MemoryMode { get; set; }
```

内存模式

```csharp
public MNN.Unity.MNNPowerMode MNN.Unity.MNNSessionConfig.PowerMode { get; set; }
```

功耗模式

```csharp
public MNN.Unity.MNNPrecisionMode MNN.Unity.MNNSessionConfig.PrecisionMode { get; set; }
```

精度模式

```csharp
public string[] MNN.Unity.MNNSessionConfig.SavedTensors { get; set; }
```

Named intermediate tensors to retain as outputs via official ScheduleConfig.saveTensors. Uses the package's pinned C++ ABI; null retains only normal graph outputs.

```csharp
public int MNN.Unity.MNNSessionConfig.ThreadCount { get; set; }
```

CPU线程数；Metal/OpenCL/Vulkan 使用独立 GPU 调优配置

```csharp
public int MNN.Unity.MNNTensor.ByteSize { get; private set; }
```

原生存储字节大小，可能包含后端通道填充

```csharp
public MNN.Unity.MNNDataType MNN.Unity.MNNTensor.DataType { get; private set; }
```

数据类型

```csharp
public MNN.Unity.MNNDimensionType MNN.Unity.MNNTensor.DimensionType { get; private set; }
```

维度类型

```csharp
public int MNN.Unity.MNNTensor.Dimensions { get; }
```

维度数量

```csharp
public int MNN.Unity.MNNTensor.ElementCount { get; private set; }
```

逻辑元素数量；映射使用公开的 NCHW/NHWC 维度类型，不包含原生通道填充

```csharp
public bool MNN.Unity.MNNTensor.IsDisposed { get; }
```

是否已释放

```csharp
public int[] MNN.Unity.MNNTensor.Shape { get; private set; }
```

张量形状

```csharp
class MNN.Unity.MNNAcceleration
```

Device/runtime selection for the implemented native ABI. Availability is not a guarantee that every operation runs on the device. Auto prefers the platform GPU, then CPU on Android; NNAPI is explicit because graph compatibility must be verified.

```csharp
class MNN.Unity.MNNInterpreter
```

MNN模型解释器 - 模型加载和会话管理

```csharp
class MNN.Unity.MNNModelLoading
```

Loads managed MNN models away from Unity's render thread.

```csharp
class MNN.Unity.MNNNeuralDevice
```

An Android NNAPI driver reported by the OS; not per-operator execution evidence.

```csharp
class MNN.Unity.MNNRuntime
```

Unity runtime environment used by model loading and cache selection.

```csharp
class MNN.Unity.MNNSession
```

MNN推理会话 - 管理推理执行和张量访问

```csharp
class MNN.Unity.MNNSessionConfig
```

MNN会话配置

```csharp
class MNN.Unity.MNNTensor
```

MNN张量 - 多维数据容器

```csharp
class MNN.Unity.MNNTensorExtensions
```

MNN张量扩展 - 自适应高性能实现 自动检测Unity.Collections，如可用则使用零拷贝NativeArray，否则使用优化的托管实现

## Language

```csharp
public string MNN.Samples.SiriAI.Harnesses.ConfiguredHarness.Backend
```

```csharp
public string MNN.Samples.SiriAI.Harnesses.ConfiguredHarness.CancellationScope
```

```csharp
public MNN.Samples.SiriAI.Harnesses.HarnessCapabilities MNN.Samples.SiriAI.Harnesses.ConfiguredHarness.Capabilities
```

```csharp
public string MNN.Samples.SiriAI.Harnesses.ConfiguredHarness.Id
```

```csharp
public string MNN.Samples.SiriAI.Harnesses.ConfiguredHarness.PermissionMode
```

```csharp
public bool MNN.Samples.SiriAI.Harnesses.HarnessAcpCapabilities.Audio
```

```csharp
public bool MNN.Samples.SiriAI.Harnesses.HarnessAcpCapabilities.EmbeddedContext
```

```csharp
public bool MNN.Samples.SiriAI.Harnesses.HarnessAcpCapabilities.Images
```

```csharp
public bool MNN.Samples.SiriAI.Harnesses.HarnessAcpCapabilities.ListSessions
```

```csharp
public bool MNN.Samples.SiriAI.Harnesses.HarnessAcpCapabilities.LoadSession
```

```csharp
public bool MNN.Samples.SiriAI.Harnesses.HarnessAcpCapabilities.McpHttp
```

```csharp
public bool MNN.Samples.SiriAI.Harnesses.HarnessAcpCapabilities.McpSse
```

```csharp
public bool MNN.Samples.SiriAI.Harnesses.HarnessAcpCapabilities.ResumeSession
```

```csharp
public bool MNN.Samples.SiriAI.Harnesses.HarnessAcpCapabilities.Steering
```

```csharp
public string MNN.Samples.SiriAI.Harnesses.HarnessBackendVersion.Minimum
```

```csharp
public string MNN.Samples.SiriAI.Harnesses.HarnessBackendVersion.State
```

```csharp
public string MNN.Samples.SiriAI.Harnesses.HarnessBackendVersion.Version
```

```csharp
MNN.Samples.SiriAI.Harnesses.HarnessCapabilities.Attachments = 16
```

```csharp
MNN.Samples.SiriAI.Harnesses.HarnessCapabilities.Cancel = 1
```

```csharp
MNN.Samples.SiriAI.Harnesses.HarnessCapabilities.Input = 2
```

```csharp
MNN.Samples.SiriAI.Harnesses.HarnessCapabilities.None = 0
```

```csharp
MNN.Samples.SiriAI.Harnesses.HarnessCapabilities.Permission = 4
```

```csharp
MNN.Samples.SiriAI.Harnesses.HarnessCapabilities.Update = 8
```

```csharp
MNN.Samples.SiriAI.Harnesses.HarnessCapabilities.UpdateAttachments = 32
```

```csharp
public const int MNN.Samples.SiriAI.Harnesses.HarnessContentFiles.MaximumFileBytes = 184320
```

```csharp
MNN.Samples.SiriAI.Harnesses.HarnessContentKind.Base64 = 3
```

```csharp
MNN.Samples.SiriAI.Harnesses.HarnessContentKind.Json = 2
```

```csharp
MNN.Samples.SiriAI.Harnesses.HarnessContentKind.Text = 0
```

```csharp
MNN.Samples.SiriAI.Harnesses.HarnessContentKind.Url = 1
```

```csharp
MNN.Samples.SiriAI.Harnesses.HarnessExecutionKind.Activity = 0
```

```csharp
MNN.Samples.SiriAI.Harnesses.HarnessExecutionKind.Error = 5
```

```csharp
MNN.Samples.SiriAI.Harnesses.HarnessExecutionKind.Plan = 1
```

```csharp
MNN.Samples.SiriAI.Harnesses.HarnessExecutionKind.Result = 4
```

```csharp
MNN.Samples.SiriAI.Harnesses.HarnessExecutionKind.ToolCall = 2
```

```csharp
MNN.Samples.SiriAI.Harnesses.HarnessExecutionKind.ToolResult = 3
```

```csharp
public MNN.Samples.SiriAI.Harnesses.ConfiguredHarness[] MNN.Samples.SiriAI.Harnesses.HarnessHostCatalog.Configured
```

```csharp
public MNN.Samples.SiriAI.Harnesses.HarnessInstallation[] MNN.Samples.SiriAI.Harnesses.HarnessHostCatalog.Installations
```

```csharp
public string[] MNN.Samples.SiriAI.Harnesses.HarnessHostCatalog.Workspaces
```

```csharp
MNN.Samples.SiriAI.Harnesses.HarnessInputAction.Answer = 0
```

```csharp
MNN.Samples.SiriAI.Harnesses.HarnessInputAction.Cancel = 2
```

```csharp
MNN.Samples.SiriAI.Harnesses.HarnessInputAction.Clarify = 1
```

```csharp
MNN.Samples.SiriAI.Harnesses.HarnessInputAction.Decline = 3
```

```csharp
public string MNN.Samples.SiriAI.Harnesses.HarnessInputChoice.Description
```

```csharp
public string MNN.Samples.SiriAI.Harnesses.HarnessInputChoice.Label
```

```csharp
public string MNN.Samples.SiriAI.Harnesses.HarnessInputChoice.Value
```

```csharp
public bool MNN.Samples.SiriAI.Harnesses.HarnessInputField.AllowOther
```

```csharp
public MNN.Samples.SiriAI.Harnesses.HarnessInputChoice[] MNN.Samples.SiriAI.Harnesses.HarnessInputField.Choices
```

```csharp
public string[] MNN.Samples.SiriAI.Harnesses.HarnessInputField.Defaults
```

```csharp
public string MNN.Samples.SiriAI.Harnesses.HarnessInputField.Description
```

```csharp
public string MNN.Samples.SiriAI.Harnesses.HarnessInputField.Label
```

```csharp
public string MNN.Samples.SiriAI.Harnesses.HarnessInputField.Name
```

```csharp
public bool MNN.Samples.SiriAI.Harnesses.HarnessInputField.Required
```

```csharp
public bool MNN.Samples.SiriAI.Harnesses.HarnessInputField.Secret
```

```csharp
public string MNN.Samples.SiriAI.Harnesses.HarnessInputField.Type
```

```csharp
public MNN.Samples.SiriAI.Harnesses.HarnessInputField[] MNN.Samples.SiriAI.Harnesses.HarnessInputForm.Fields
```

```csharp
MNN.Samples.SiriAI.Harnesses.HarnessInputMode.Text = 0
```

```csharp
MNN.Samples.SiriAI.Harnesses.HarnessInputMode.Url = 1
```

```csharp
public string MNN.Samples.SiriAI.Harnesses.HarnessInstallation.AdapterExecutable
```

```csharp
public string[] MNN.Samples.SiriAI.Harnesses.HarnessInstallation.Arguments
```

```csharp
public string MNN.Samples.SiriAI.Harnesses.HarnessInstallation.Backend
```

```csharp
public string MNN.Samples.SiriAI.Harnesses.HarnessInstallation.Executable
```

```csharp
public string MNN.Samples.SiriAI.Harnesses.HarnessInstallation.Id
```

```csharp
public bool MNN.Samples.SiriAI.Harnesses.HarnessInstallation.Installed
```

```csharp
public string MNN.Samples.SiriAI.Harnesses.HarnessInstallation.Integration
```

```csharp
public bool MNN.Samples.SiriAI.Harnesses.HarnessInstallation.InteroperabilityVerified
```

```csharp
public string MNN.Samples.SiriAI.Harnesses.HarnessInstallation.MinimumVersion
```

```csharp
public string MNN.Samples.SiriAI.Harnesses.HarnessInstallation.Name
```

```csharp
public bool MNN.Samples.SiriAI.Harnesses.HarnessInstallation.ProtocolCandidate
```

```csharp
public bool MNN.Samples.SiriAI.Harnesses.HarnessInstallation.RequiresExplicitConfiguration
```

```csharp
MNN.Samples.SiriAI.Harnesses.HarnessInstructionState.Delivered = 1
```

```csharp
MNN.Samples.SiriAI.Harnesses.HarnessInstructionState.Pending = 0
```

```csharp
MNN.Samples.SiriAI.Harnesses.HarnessInstructionState.Rejected = 2
```

```csharp
MNN.Samples.SiriAI.Harnesses.HarnessInstructionState.Uncertain = 3
```

```csharp
public string MNN.Samples.SiriAI.Harnesses.HarnessLocalSetup.Address
```

```csharp
public string MNN.Samples.SiriAI.Harnesses.HarnessLocalSetup.Backend
```

```csharp
public string MNN.Samples.SiriAI.Harnesses.HarnessLocalSetup.ScopeId
```

```csharp
public string MNN.Samples.SiriAI.Harnesses.HarnessLocalSetup.TokenPath
```

```csharp
public string MNN.Samples.SiriAI.Harnesses.HarnessLocalSetup.Workspace
```

```csharp
public MNN.Samples.SiriAI.Harnesses.HarnessNativeHistoryEntry[] MNN.Samples.SiriAI.Harnesses.HarnessNativeHistory.Entries
```

```csharp
public int MNN.Samples.SiriAI.Harnesses.HarnessNativeHistory.ReceivedEntries
```

```csharp
public string MNN.Samples.SiriAI.Harnesses.HarnessNativeHistory.SessionId
```

```csharp
public bool MNN.Samples.SiriAI.Harnesses.HarnessNativeHistory.Supported
```

```csharp
public bool MNN.Samples.SiriAI.Harnesses.HarnessNativeHistory.Truncated
```

```csharp
public bool MNN.Samples.SiriAI.Harnesses.HarnessNativeHistory.Uncertain
```

```csharp
public string MNN.Samples.SiriAI.Harnesses.HarnessNativeHistoryEntry.Kind
```

```csharp
public string MNN.Samples.SiriAI.Harnesses.HarnessNativeHistoryEntry.MessageId
```

```csharp
public string MNN.Samples.SiriAI.Harnesses.HarnessNativeHistoryEntry.Payload
```

```csharp
public int MNN.Samples.SiriAI.Harnesses.HarnessNativeHistoryEntry.Sequence
```

```csharp
public string MNN.Samples.SiriAI.Harnesses.HarnessNativeHistoryEntry.Text
```

```csharp
public string MNN.Samples.SiriAI.Harnesses.HarnessNativeSession.Cwd
```

```csharp
public string MNN.Samples.SiriAI.Harnesses.HarnessNativeSession.SessionId
```

```csharp
public string MNN.Samples.SiriAI.Harnesses.HarnessNativeSession.Title
```

```csharp
public string MNN.Samples.SiriAI.Harnesses.HarnessNativeSession.UpdatedAt
```

```csharp
public string MNN.Samples.SiriAI.Harnesses.HarnessNativeSessionPage.NextCursor
```

```csharp
public MNN.Samples.SiriAI.Harnesses.HarnessNativeSession[] MNN.Samples.SiriAI.Harnesses.HarnessNativeSessionPage.Sessions
```

```csharp
public bool MNN.Samples.SiriAI.Harnesses.HarnessNativeSessionPage.Supported
```

```csharp
public bool MNN.Samples.SiriAI.Harnesses.HarnessNativeSessionPage.Uncertain
```

```csharp
public MNN.Samples.SiriAI.Harnesses.HarnessAcpCapabilities MNN.Samples.SiriAI.Harnesses.HarnessReadiness.AcpCapabilities
```

```csharp
public MNN.Samples.SiriAI.Harnesses.HarnessBackendVersion MNN.Samples.SiriAI.Harnesses.HarnessReadiness.BackendVersion
```

```csharp
public long MNN.Samples.SiriAI.Harnesses.HarnessReadiness.CheckedUtcTicks
```

```csharp
public string MNN.Samples.SiriAI.Harnesses.HarnessReadiness.Evidence
```

```csharp
public string MNN.Samples.SiriAI.Harnesses.HarnessReadiness.HarnessId
```

```csharp
public string MNN.Samples.SiriAI.Harnesses.HarnessReadiness.Protocol
```

```csharp
public string MNN.Samples.SiriAI.Harnesses.HarnessReadiness.State
```

```csharp
public string MNN.Samples.SiriAI.Harnesses.HarnessReadiness.WorkspaceKey
```

```csharp
public string MNN.Samples.SiriAI.Harnesses.HarnessReadinessRequest.WorkspaceKey
```

```csharp
public string MNN.Samples.SiriAI.Harnesses.HarnessSessionChoice.Group
```

```csharp
public string MNN.Samples.SiriAI.Harnesses.HarnessSessionChoice.Name
```

```csharp
public string MNN.Samples.SiriAI.Harnesses.HarnessSessionChoice.Value
```

```csharp
public string MNN.Samples.SiriAI.Harnesses.HarnessSessionConfiguration.CurrentModeId
```

```csharp
public MNN.Samples.SiriAI.Harnesses.HarnessSessionChoice[] MNN.Samples.SiriAI.Harnesses.HarnessSessionConfiguration.Modes
```

```csharp
public MNN.Samples.SiriAI.Harnesses.HarnessSessionOption[] MNN.Samples.SiriAI.Harnesses.HarnessSessionConfiguration.Options
```

```csharp
public string MNN.Samples.SiriAI.Harnesses.HarnessSessionConfiguration.SessionId
```

```csharp
public bool MNN.Samples.SiriAI.Harnesses.HarnessSessionOption.BooleanValue
```

```csharp
public string MNN.Samples.SiriAI.Harnesses.HarnessSessionOption.Category
```

```csharp
public MNN.Samples.SiriAI.Harnesses.HarnessSessionChoice[] MNN.Samples.SiriAI.Harnesses.HarnessSessionOption.Choices
```

```csharp
public string MNN.Samples.SiriAI.Harnesses.HarnessSessionOption.CurrentValue
```

```csharp
public string MNN.Samples.SiriAI.Harnesses.HarnessSessionOption.Id
```

```csharp
public bool MNN.Samples.SiriAI.Harnesses.HarnessSessionOption.IsBoolean
```

```csharp
public string MNN.Samples.SiriAI.Harnesses.HarnessSessionOption.Name
```

```csharp
public string[] MNN.Samples.SiriAI.Harnesses.HarnessSkillCatalog.DefaultTargets
```

```csharp
public bool MNN.Samples.SiriAI.Harnesses.HarnessSkillCatalog.Enabled
```

```csharp
public string MNN.Samples.SiriAI.Harnesses.HarnessSkillCatalog.Package
```

```csharp
public MNN.Samples.SiriAI.Harnesses.HarnessSkillTarget[] MNN.Samples.SiriAI.Harnesses.HarnessSkillCatalog.Targets
```

```csharp
public string[] MNN.Samples.SiriAI.Harnesses.HarnessSkillRequest.Backends
```

```csharp
public string[] MNN.Samples.SiriAI.Harnesses.HarnessSkillRequest.Names
```

```csharp
public string MNN.Samples.SiriAI.Harnesses.HarnessSkillRequest.Operation
```

```csharp
public string MNN.Samples.SiriAI.Harnesses.HarnessSkillRequest.Source
```

```csharp
public string MNN.Samples.SiriAI.Harnesses.HarnessSkillRequest.SubmissionKey
```

```csharp
public string MNN.Samples.SiriAI.Harnesses.HarnessSkillTarget.Backend
```

```csharp
public string MNN.Samples.SiriAI.Harnesses.HarnessSkillTarget.Installer
```

```csharp
MNN.Samples.SiriAI.Harnesses.HarnessTaskState.Cancelled = 5
```

```csharp
MNN.Samples.SiriAI.Harnesses.HarnessTaskState.Cancelling = 2
```

```csharp
MNN.Samples.SiriAI.Harnesses.HarnessTaskState.Completed = 3
```

```csharp
MNN.Samples.SiriAI.Harnesses.HarnessTaskState.Failed = 4
```

```csharp
MNN.Samples.SiriAI.Harnesses.HarnessTaskState.Interrupted = 6
```

```csharp
MNN.Samples.SiriAI.Harnesses.HarnessTaskState.Queued = 0
```

```csharp
MNN.Samples.SiriAI.Harnesses.HarnessTaskState.Running = 1
```

```csharp
MNN.Samples.SiriAI.Harnesses.HarnessTaskToolOperation.Cancel = 2
```

```csharp
MNN.Samples.SiriAI.Harnesses.HarnessTaskToolOperation.Status = 1
```

```csharp
MNN.Samples.SiriAI.Harnesses.HarnessTaskToolOperation.Submit = 0
```

```csharp
MNN.Samples.SiriAI.Harnesses.HarnessTaskToolOperation.Update = 3
```

```csharp
MNN.Samples.SiriAI.Harnesses.HarnessWaitingFor.Input = 1
```

```csharp
MNN.Samples.SiriAI.Harnesses.HarnessWaitingFor.None = 0
```

```csharp
MNN.Samples.SiriAI.Harnesses.HarnessWaitingFor.Permission = 2
```

```csharp
public string MNN.Samples.SiriAI.Harnesses.ResultClaimRequest.ClientId
```

```csharp
public string MNN.Samples.SiriAI.Harnesses.ResultClaimRequest.ConversationId
```

```csharp
public int MNN.Samples.SiriAI.Harnesses.ResultClaimRequest.LeaseSeconds
```

```csharp
MNN.Samples.SiriAI.Harnesses.ResultReceipt.ContextInjected = 0
```

```csharp
MNN.Samples.SiriAI.Harnesses.ResultReceipt.Interrupted = 3
```

```csharp
MNN.Samples.SiriAI.Harnesses.ResultReceipt.PlaybackCompleted = 2
```

```csharp
MNN.Samples.SiriAI.Harnesses.ResultReceipt.PlaybackStarted = 1
```

```csharp
MNN.Samples.SiriAI.Harnesses.ResultReceipt.TextDisplayed = 4
```

```csharp
public string MNN.Samples.SiriAI.Harnesses.ResultReceiptRequest.LeaseId
```

```csharp
public int MNN.Samples.SiriAI.Harnesses.ResultReceiptRequest.LeaseSeconds
```

```csharp
public MNN.Samples.SiriAI.Harnesses.ResultReceipt MNN.Samples.SiriAI.Harnesses.ResultReceiptRequest.Receipt
```

```csharp
public MNN.Samples.SiriAI.Harnesses.HarnessContentPart[] MNN.Samples.SiriAI.Harnesses.SubmitTaskRequest.Attachments
```

```csharp
public string MNN.Samples.SiriAI.Harnesses.SubmitTaskRequest.ConversationId
```

```csharp
public string MNN.Samples.SiriAI.Harnesses.SubmitTaskRequest.HarnessId
```

```csharp
public bool MNN.Samples.SiriAI.Harnesses.SubmitTaskRequest.IsolatedContext
```

```csharp
public string MNN.Samples.SiriAI.Harnesses.SubmitTaskRequest.Objective
```

```csharp
public string MNN.Samples.SiriAI.Harnesses.SubmitTaskRequest.SubmissionKey
```

```csharp
public string MNN.Samples.SiriAI.Harnesses.SubmitTaskRequest.WorkspaceKey
```

```csharp
public bool MNN.Samples.SiriAI.Harnesses.TaskControlRequest.AdmitOnly
```

```csharp
public bool MNN.Samples.SiriAI.Harnesses.TaskControlRequest.Allow
```

```csharp
public MNN.Samples.SiriAI.Harnesses.HarnessContentPart[] MNN.Samples.SiriAI.Harnesses.TaskControlRequest.Attachments
```

```csharp
public MNN.Samples.SiriAI.Harnesses.HarnessInputAction MNN.Samples.SiriAI.Harnesses.TaskControlRequest.InputAction
```

```csharp
public string MNN.Samples.SiriAI.Harnesses.TaskControlRequest.RequestId
```

```csharp
public string MNN.Samples.SiriAI.Harnesses.TaskControlRequest.Text
```

```csharp
public string MNN.Samples.SiriAI.Harnesses.TaskHostError.Code
```

```csharp
public string MNN.Samples.SiriAI.Harnesses.TaskHostError.Message
```

```csharp
public MNN.Samples.SiriAI.Harnesses.HarnessContentPart[] MNN.Samples.SiriAI.Harnesses.TaskUpdateRequest.Attachments
```

```csharp
public string MNN.Samples.SiriAI.Harnesses.TaskUpdateRequest.RequestId
```

```csharp
public string MNN.Samples.SiriAI.Harnesses.TaskUpdateRequest.TaskId
```

```csharp
public string MNN.Samples.SiriAI.Harnesses.TaskUpdateRequest.Text
```

```csharp
public bool MNN.Unity.MNNAgentOptions.AllowCloud
```

```csharp
public int MNN.Unity.MNNAgentOptions.CloudContextCharacters
```

```csharp
public int MNN.Unity.MNNAgentOptions.CloudHistoryMessages
```

```csharp
public bool MNN.Unity.MNNAgentOptions.KnowledgeAnswers
```

```csharp
public string MNN.Unity.MNNAgentOptions.LocalCapabilities
```

```csharp
public int MNN.Unity.MNNAgentOptions.LocalContextCharacters
```

```csharp
public int MNN.Unity.MNNAgentOptions.PlanningSeconds
```

```csharp
public MNN.Unity.MNNAgentRoutingStrategy MNN.Unity.MNNAgentOptions.Strategy
```

```csharp
public bool MNN.Unity.MNNAgentOptions.UseRouter
```

```csharp
MNN.Unity.MNNAgentRoutingStrategy.ModelPlanner = 1
```

```csharp
MNN.Unity.MNNAgentRoutingStrategy.Policy = 0
```

```csharp
MNN.Unity.MNNChatRole.Assistant = 2
```

```csharp
MNN.Unity.MNNChatRole.System = 0
```

```csharp
MNN.Unity.MNNChatRole.User = 1
```

```csharp
public string MNN.Unity.MNNCloudSpeechOptions.ApplicationId
```

```csharp
public string MNN.Unity.MNNCloudSpeechOptions.DisplayName
```

```csharp
public string MNN.Unity.MNNCloudSpeechOptions.Endpoint
```

```csharp
public string MNN.Unity.MNNCloudSpeechOptions.Language
```

```csharp
public string MNN.Unity.MNNCloudSpeechOptions.Model
```

```csharp
public MNN.Unity.MNNCloudSpeechProtocol MNN.Unity.MNNCloudSpeechOptions.Protocol
```

```csharp
public string MNN.Unity.MNNCloudSpeechOptions.ResourceId
```

```csharp
public string[] MNN.Unity.MNNCloudSpeechOptions.SpeechLanguages
```

```csharp
public MNN.Unity.MNNCloudSpeechTask MNN.Unity.MNNCloudSpeechOptions.Task
```

```csharp
public int MNN.Unity.MNNCloudSpeechOptions.TimeoutSeconds
```

```csharp
public string MNN.Unity.MNNCloudSpeechOptions.Voice
```

```csharp
MNN.Unity.MNNCloudSpeechProtocol.DashScopeRecognition = 2
```

```csharp
MNN.Unity.MNNCloudSpeechProtocol.DashScopeSpeech = 3
```

```csharp
MNN.Unity.MNNCloudSpeechProtocol.MiniMaxSpeech = 6
```

```csharp
MNN.Unity.MNNCloudSpeechProtocol.OpenAISpeech = 1
```

```csharp
MNN.Unity.MNNCloudSpeechProtocol.VolcengineRecognition = 4
```

```csharp
MNN.Unity.MNNCloudSpeechProtocol.VolcengineSpeech = 5
```

```csharp
MNN.Unity.MNNCloudSpeechTask.Recognition = 1
```

```csharp
MNN.Unity.MNNCloudSpeechTask.Synthesis = 2
```

```csharp
MNN.Unity.MNNConversationCacheStatus.Cold = 0
```

```csharp
MNN.Unity.MNNConversationCacheStatus.Disabled = 2
```

```csharp
MNN.Unity.MNNConversationCacheStatus.HistoryMismatch = 6
```

```csharp
MNN.Unity.MNNConversationCacheStatus.Interrupted = 7
```

```csharp
MNN.Unity.MNNConversationCacheStatus.Media = 3
```

```csharp
MNN.Unity.MNNConversationCacheStatus.Reused = 1
```

```csharp
MNN.Unity.MNNConversationCacheStatus.UnsupportedModel = 4
```

```csharp
MNN.Unity.MNNConversationCacheStatus.UnsupportedRuntime = 5
```

```csharp
MNN.Unity.MNNConversationEntryKind.Artifact = 6
```

```csharp
MNN.Unity.MNNConversationEntryKind.Error = 5
```

```csharp
MNN.Unity.MNNConversationEntryKind.Message = 0
```

```csharp
MNN.Unity.MNNConversationEntryKind.Plan = 1
```

```csharp
MNN.Unity.MNNConversationEntryKind.ReasoningSummary = 2
```

```csharp
MNN.Unity.MNNConversationEntryKind.ToolCall = 3
```

```csharp
MNN.Unity.MNNConversationEntryKind.ToolResult = 4
```

```csharp
MNN.Unity.MNNConversationEntryState.Cancelled = 3
```

```csharp
MNN.Unity.MNNConversationEntryState.Completed = 1
```

```csharp
MNN.Unity.MNNConversationEntryState.Failed = 2
```

```csharp
MNN.Unity.MNNConversationEntryState.Running = 0
```

```csharp
public const int MNN.Unity.MNNConversationInputState.MaximumSamples = 960000
```

```csharp
public const int MNN.Unity.MNNConversationInputState.SampleRate = 16000
```

```csharp
public MNN.Unity.MNNBackendType MNN.Unity.MNNConversationLocalModelOptions.Backend
```

```csharp
public string MNN.Unity.MNNConversationLocalModelOptions.BuildIdentity
```

```csharp
public string MNN.Unity.MNNConversationLocalModelOptions.CacheDirectory
```

```csharp
public string MNN.Unity.MNNConversationLocalModelOptions.CacheRoot
```

```csharp
public bool MNN.Unity.MNNConversationLocalModelOptions.DeterministicDecisions
```

```csharp
public string MNN.Unity.MNNConversationLocalModelOptions.DeviceIdentity
```

```csharp
public string MNN.Unity.MNNConversationLocalModelOptions.Language
```

```csharp
public int MNN.Unity.MNNConversationLocalModelOptions.MaximumAudioTokens
```

```csharp
public int MNN.Unity.MNNConversationLocalModelOptions.MaximumReplyTokens
```

```csharp
public string MNN.Unity.MNNConversationLocalModelOptions.Precision
```

```csharp
public int MNN.Unity.MNNConversationLocalModelOptions.RecognitionThreads
```

```csharp
public string MNN.Unity.MNNConversationLocalModelOptions.ReferenceText
```

```csharp
public string MNN.Unity.MNNConversationLocalModelOptions.ReferenceWavePath
```

```csharp
public string MNN.Unity.MNNConversationLocalModelOptions.ScratchDirectory
```

```csharp
public bool MNN.Unity.MNNConversationLocalModelOptions.SpeechOutput
```

```csharp
public int MNN.Unity.MNNConversationLocalModelOptions.SynthesisSeed
```

```csharp
public float MNN.Unity.MNNConversationLocalModelOptions.SynthesisSpeed
```

```csharp
public int MNN.Unity.MNNConversationLocalModelOptions.SynthesisSteps
```

```csharp
public float MNN.Unity.MNNConversationLocalModelOptions.Temperature
```

```csharp
public bool MNN.Unity.MNNConversationLocalModelOptions.Thinking
```

```csharp
public int MNN.Unity.MNNConversationLocalModelOptions.Threads
```

```csharp
public string MNN.Unity.MNNConversationLocalModelOptions.Voice
```

```csharp
public const int MNN.Unity.MNNConversationMailbox.MaximumTextCharacters = 65536
```

```csharp
public readonly System.Collections.Generic.List<MNN.Unity.MNNOpenAIMessage> MNN.Unity.MNNConversationMemory.Messages
```

```csharp
public System.Collections.Generic.IReadOnlyList<string> MNN.Unity.MNNConversationOptions.SpeechLanguages
```

```csharp
public bool MNN.Unity.MNNConversationOptions.endRealtimeOnExitIntent
```

```csharp
public float MNN.Unity.MNNConversationOptions.energyThreshold
```

```csharp
public bool MNN.Unity.MNNConversationOptions.filterVoiceInput
```

```csharp
public string MNN.Unity.MNNConversationOptions.instructions
```

```csharp
public int MNN.Unity.MNNConversationOptions.maximumHistoryMessages
```

```csharp
public int MNN.Unity.MNNConversationOptions.maximumResponseCharacters
```

```csharp
public int MNN.Unity.MNNConversationOptions.maximumResponseSeconds
```

```csharp
public int MNN.Unity.MNNConversationOptions.maximumUtteranceSeconds
```

```csharp
public int MNN.Unity.MNNConversationOptions.minimumSpeechMilliseconds
```

```csharp
public int MNN.Unity.MNNConversationOptions.realtimeInactivitySeconds
```

```csharp
public int MNN.Unity.MNNConversationOptions.silenceMilliseconds
```

```csharp
public bool MNN.Unity.MNNConversationOptions.speechOutput
```

```csharp
public int MNN.Unity.MNNConversationOptions.speechSegmentCharacters
```

```csharp
public int MNN.Unity.MNNConversationOptions.turnRetryMilliseconds
```

```csharp
public string MNN.Unity.MNNConversationProviderMetadata.Directory
```

```csharp
public string MNN.Unity.MNNConversationProviderMetadata.Location
```

```csharp
public string MNN.Unity.MNNConversationProviderMetadata.Model
```

```csharp
public MNN.Unity.IMNNConversationProvider MNN.Unity.MNNConversationProviders.Asr
```

```csharp
public static readonly System.Collections.Generic.IEqualityComparer<MNN.Unity.IMNNConversationProvider> MNN.Unity.MNNConversationProviders.Identity
```

```csharp
public MNN.Unity.IMNNConversationProvider MNN.Unity.MNNConversationProviders.Model
```

```csharp
public System.Func<MNN.Unity.IMNNConversationProvider, System.Threading.CancellationToken, System.Threading.Tasks.Task> MNN.Unity.MNNConversationProviders.Prepare
```

```csharp
public System.Action<MNN.Unity.IMNNConversationProvider> MNN.Unity.MNNConversationProviders.Release
```

```csharp
public System.Func<MNN.Unity.MNNConversationSpeechTimings> MNN.Unity.MNNConversationProviders.SpeechTimings
```

```csharp
public MNN.Unity.IMNNConversationProvider MNN.Unity.MNNConversationProviders.Tts
```

```csharp
public MNN.Unity.IMNNConversationProvider MNN.Unity.MNNConversationProviders.Turn
```

```csharp
public MNN.Unity.IMNNConversationProvider MNN.Unity.MNNConversationProviders.Vad
```

```csharp
public bool MNN.Unity.MNNConversationRegistrations.AllowBufferedSpeech
```

```csharp
public MNN.Unity.MNNConversationStageRegistration MNN.Unity.MNNConversationRegistrations.Asr
```

```csharp
public MNN.Unity.MNNConversationStageRegistration MNN.Unity.MNNConversationRegistrations.Model
```

```csharp
public MNN.Unity.MNNConversationStageRegistration MNN.Unity.MNNConversationRegistrations.Transcription
```

```csharp
public MNN.Unity.MNNConversationStageRegistration MNN.Unity.MNNConversationRegistrations.Tts
```

```csharp
public MNN.Unity.MNNConversationStageRegistration MNN.Unity.MNNConversationRegistrations.Turn
```

```csharp
public bool MNN.Unity.MNNConversationRegistrations.TurnRequiresTranscript
```

```csharp
public MNN.Unity.MNNConversationStageRegistration MNN.Unity.MNNConversationRegistrations.Vad
```

```csharp
public readonly bool MNN.Unity.MNNConversationRuntime.VoiceCandidate.Accepted
```

```csharp
public readonly float[] MNN.Unity.MNNConversationRuntime.VoiceCandidate.Audio
```

```csharp
public readonly bool MNN.Unity.MNNConversationRuntime.VoiceCandidate.Complete
```

```csharp
public readonly string MNN.Unity.MNNConversationRuntime.VoiceCandidate.Text
```

```csharp
protected float[] MNN.Unity.MNNConversationRuntime._acceptedAudio
```

```csharp
protected long MNN.Unity.MNNConversationRuntime._acceptedRevision
```

```csharp
protected string MNN.Unity.MNNConversationRuntime._acceptedText
```

```csharp
protected MNN.Unity.MNNConversationTelemetry.Trace MNN.Unity.MNNConversationRuntime._activeTrace
```

```csharp
protected readonly System.Threading.SemaphoreSlim MNN.Unity.MNNConversationRuntime._asrGate
```

```csharp
protected readonly MNN.Unity.MNNBargeInPolicy MNN.Unity.MNNConversationRuntime._bargeIn
```

```csharp
protected string MNN.Unity.MNNConversationRuntime._bargeInEcho
```

```csharp
protected MNN.Unity.MNNRealtimeAudioCaptureWorker MNN.Unity.MNNConversationRuntime._capture
```

```csharp
protected readonly MNN.Unity.MNNConversationOptions MNN.Unity.MNNConversationRuntime._config
```

```csharp
protected readonly MNN.Unity.IMNNConversationContext MNN.Unity.MNNConversationRuntime._context
```

```csharp
protected int MNN.Unity.MNNConversationRuntime._echoSuppressed
```

```csharp
protected MNN.Unity.MNNConversationEventReply MNN.Unity.MNNConversationRuntime._eventReply
```

```csharp
protected MNN.Unity.MNNRealtimeModelInputBuffer MNN.Unity.MNNConversationRuntime._input
```

```csharp
protected System.Exception MNN.Unity.MNNConversationRuntime._inputFailure
```

```csharp
protected bool MNN.Unity.MNNConversationRuntime._inputFinished
```

```csharp
protected long MNN.Unity.MNNConversationRuntime._inputGaps
```

```csharp
protected int MNN.Unity.MNNConversationRuntime._inputRevision
```

```csharp
protected System.Threading.Tasks.Task MNN.Unity.MNNConversationRuntime._inputTask
```

```csharp
protected string MNN.Unity.MNNConversationRuntime._lastVoiceRejection
```

```csharp
protected readonly System.Threading.CancellationTokenSource MNN.Unity.MNNConversationRuntime._lifetime
```

```csharp
protected readonly System.Threading.SemaphoreSlim MNN.Unity.MNNConversationRuntime._modelGate
```

```csharp
protected int MNN.Unity.MNNConversationRuntime._nextTurnSilenceSamples
```

```csharp
protected readonly MNN.Unity.MNNRealtimeOutputPipeline MNN.Unity.MNNConversationRuntime._output
```

```csharp
protected readonly MNN.Unity.MNNConversationProviders MNN.Unity.MNNConversationRuntime._providers
```

```csharp
protected MNN.Unity.MNNConversationRecognitionWorker MNN.Unity.MNNConversationRuntime._recognitionPreview
```

```csharp
protected System.Threading.Tasks.TaskCompletionSource<bool> MNN.Unity.MNNConversationRuntime._recordingDrain
```

```csharp
protected System.Threading.Tasks.Task MNN.Unity.MNNConversationRuntime._recovery
```

```csharp
protected long MNN.Unity.MNNConversationRuntime._rejectedRevision
```

```csharp
protected long MNN.Unity.MNNConversationRuntime._rejectedVoiceInputs
```

```csharp
protected MNN.Unity.MNNResponseId MNN.Unity.MNNConversationRuntime._response
```

```csharp
protected System.Threading.CancellationTokenSource MNN.Unity.MNNConversationRuntime._responseCancellation
```

```csharp
protected System.Threading.Tasks.Task MNN.Unity.MNNConversationRuntime._responseTask
```

```csharp
protected long MNN.Unity.MNNConversationRuntime._resumeAfter
```

```csharp
protected readonly MNN.Unity.MNNRealtimeRoute MNN.Unity.MNNConversationRuntime._route
```

```csharp
protected MNN.Unity.MNNRealtimeRouteUnavailableException MNN.Unity.MNNConversationRuntime._routeFailure
```

```csharp
protected bool MNN.Unity.MNNConversationRuntime._stopped
```

```csharp
protected readonly object MNN.Unity.MNNConversationRuntime._sync
```

```csharp
protected float[] MNN.Unity.MNNConversationRuntime._turnAudio
```

```csharp
protected System.Threading.CancellationTokenSource MNN.Unity.MNNConversationRuntime._turnCancellation
```

```csharp
protected readonly int MNN.Unity.MNNConversationRuntime._turnRetrySilenceSamples
```

```csharp
protected long MNN.Unity.MNNConversationRuntime._turnRevision
```

```csharp
protected int MNN.Unity.MNNConversationRuntime._turnSilenceSamples
```

```csharp
protected System.Threading.Tasks.Task<MNN.Unity.MNNConversationRuntime.VoiceCandidate> MNN.Unity.MNNConversationRuntime._turnTask
```

```csharp
protected MNN.Unity.MNNRealtimeUtteranceBuffer MNN.Unity.MNNConversationRuntime._utterance
```

```csharp
public string MNN.Unity.MNNConversationRuntimeDiagnostics.Entry.Backend
```

```csharp
public string MNN.Unity.MNNConversationRuntimeDiagnostics.Entry.Contract
```

```csharp
public string MNN.Unity.MNNConversationRuntimeDiagnostics.Entry.Directory
```

```csharp
public string MNN.Unity.MNNConversationRuntimeDiagnostics.Entry.Implementation
```

```csharp
public string MNN.Unity.MNNConversationRuntimeDiagnostics.Entry.Limitation
```

```csharp
public string MNN.Unity.MNNConversationRuntimeDiagnostics.Entry.Location
```

```csharp
public string MNN.Unity.MNNConversationRuntimeDiagnostics.Entry.Model
```

```csharp
public string MNN.Unity.MNNConversationRuntimeDiagnostics.Entry.Stage
```

```csharp
public string MNN.Unity.MNNConversationRuntimeDiagnostics.Entry.State
```

```csharp
public readonly double MNN.Unity.MNNConversationSpeechTimings.FlowMs
```

```csharp
public readonly double MNN.Unity.MNNConversationSpeechTimings.TokenGenerationMs
```

```csharp
public readonly double MNN.Unity.MNNConversationSpeechTimings.VocoderMs
```

```csharp
MNN.Unity.MNNConversationStage.Error = 10
```

```csharp
MNN.Unity.MNNConversationStage.Generating = 5
```

```csharp
MNN.Unity.MNNConversationStage.Idle = 0
```

```csharp
MNN.Unity.MNNConversationStage.Interrupted = 9
```

```csharp
MNN.Unity.MNNConversationStage.Listening = 3
```

```csharp
MNN.Unity.MNNConversationStage.Loading = 1
```

```csharp
MNN.Unity.MNNConversationStage.ModelReady = 2
```

```csharp
MNN.Unity.MNNConversationStage.Recognizing = 4
```

```csharp
MNN.Unity.MNNConversationStage.ReplyCompleted = 8
```

```csharp
MNN.Unity.MNNConversationStage.Speaking = 7
```

```csharp
MNN.Unity.MNNConversationStage.Stopped = 11
```

```csharp
MNN.Unity.MNNConversationStage.Synthesizing = 6
```

```csharp
public readonly MNN.Unity.MNNRealtimeCapabilities MNN.Unity.MNNConversationStageRegistration.Capabilities
```

```csharp
public readonly System.Func<MNN.Unity.IMNNConversationProvider> MNN.Unity.MNNConversationStageRegistration.Create
```

```csharp
public readonly System.Func<System.Threading.CancellationToken, System.Threading.Tasks.Task> MNN.Unity.MNNConversationStageRegistration.Install
```

```csharp
public readonly double MNN.Unity.MNNConversationTelemetry.MNNConversationTiming.AsrMs
```

```csharp
public readonly bool MNN.Unity.MNNConversationTelemetry.MNNConversationTiming.Failed
```

```csharp
public readonly double MNN.Unity.MNNConversationTelemetry.MNNConversationTiming.FirstAudioMs
```

```csharp
public readonly double MNN.Unity.MNNConversationTelemetry.MNNConversationTiming.FirstTokenMs
```

```csharp
public readonly double MNN.Unity.MNNConversationTelemetry.MNNConversationTiming.ModelMs
```

```csharp
public readonly double MNN.Unity.MNNConversationTelemetry.MNNConversationTiming.PresentationMs
```

```csharp
public readonly double MNN.Unity.MNNConversationTelemetry.MNNConversationTiming.QueueMs
```

```csharp
public readonly string MNN.Unity.MNNConversationTelemetry.MNNConversationTiming.Route
```

```csharp
public readonly double MNN.Unity.MNNConversationTelemetry.MNNConversationTiming.SpeechElapsedMs
```

```csharp
public readonly double MNN.Unity.MNNConversationTelemetry.MNNConversationTiming.SpeechMs
```

```csharp
public readonly double MNN.Unity.MNNConversationTelemetry.MNNConversationTiming.TotalMs
```

```csharp
public readonly double MNN.Unity.MNNConversationTelemetry.MNNConversationTiming.TtsFlowMs
```

```csharp
public readonly double MNN.Unity.MNNConversationTelemetry.MNNConversationTiming.TtsTokenMs
```

```csharp
public readonly double MNN.Unity.MNNConversationTelemetry.MNNConversationTiming.TtsVocoderMs
```

```csharp
public readonly double MNN.Unity.MNNConversationTelemetry.MNNConversationTiming.VoiceIntentMs
```

```csharp
public readonly long MNN.Unity.MNNConversationTelemetry.Trace.AcceptedAt
```

```csharp
public double MNN.Unity.MNNConversationTelemetry.Trace.AsrMs
```

```csharp
public bool MNN.Unity.MNNConversationTelemetry.Trace.Failed
```

```csharp
public long MNN.Unity.MNNConversationTelemetry.Trace.FirstAudioAt
```

```csharp
public long MNN.Unity.MNNConversationTelemetry.Trace.FirstTokenAt
```

```csharp
public long MNN.Unity.MNNConversationTelemetry.Trace.ModelFinishedAt
```

```csharp
public long MNN.Unity.MNNConversationTelemetry.Trace.ModelStartedAt
```

```csharp
public long MNN.Unity.MNNConversationTelemetry.Trace.PresentedAt
```

```csharp
public long MNN.Unity.MNNConversationTelemetry.Trace.ProcessingStartedAt
```

```csharp
public int MNN.Unity.MNNConversationTelemetry.Trace.Recorded
```

```csharp
public string MNN.Unity.MNNConversationTelemetry.Trace.Route
```

```csharp
public long MNN.Unity.MNNConversationTelemetry.Trace.SpeechFinishedAt
```

```csharp
public long MNN.Unity.MNNConversationTelemetry.Trace.SpeechStartedAt
```

```csharp
public double MNN.Unity.MNNConversationTelemetry.Trace.SpeechWorkMs
```

```csharp
public double MNN.Unity.MNNConversationTelemetry.Trace.TtsFlowMs
```

```csharp
public double MNN.Unity.MNNConversationTelemetry.Trace.TtsTokenMs
```

```csharp
public double MNN.Unity.MNNConversationTelemetry.Trace.TtsVocoderMs
```

```csharp
public double MNN.Unity.MNNConversationTelemetry.Trace.VoiceIntentMs
```

```csharp
MNN.Unity.MNNConversationTextTask.Completion = 1
```

```csharp
MNN.Unity.MNNConversationTextTask.Title = 0
```

```csharp
public const int MNN.Unity.MNNConversationTranscriptStore.BlockSize = 64
```

```csharp
public const long MNN.Unity.MNNConversationTranscriptStore.DefaultMaximumBytes = 268435456
```

```csharp
public readonly long MNN.Unity.MNNConversationWakeHandoff.CaptureRevision
```

```csharp
public readonly long MNN.Unity.MNNConversationWakeHandoff.EndSample
```

```csharp
public readonly float[] MNN.Unity.MNNConversationWakeHandoff.Frames
```

```csharp
public readonly bool MNN.Unity.MNNConversationWakeHandoff.LostPrefix
```

```csharp
MNN.Unity.MNNDecisionKind.Boolean = 2
```

```csharp
MNN.Unity.MNNDecisionKind.Choice = 0
```

```csharp
MNN.Unity.MNNDecisionKind.Score = 1
```

```csharp
MNN.Unity.MNNLlmDynamicQuantization.PerBlock = 2
```

```csharp
MNN.Unity.MNNLlmDynamicQuantization.PerChannel = 0
```

```csharp
MNN.Unity.MNNLlmDynamicQuantization.PerTensor = 1
```

```csharp
MNN.Unity.MNNMarkdownBlockType.Code = 5
```

```csharp
MNN.Unity.MNNMarkdownBlockType.Heading = 1
```

```csharp
MNN.Unity.MNNMarkdownBlockType.Image = 6
```

```csharp
MNN.Unity.MNNMarkdownBlockType.OrderedList = 4
```

```csharp
MNN.Unity.MNNMarkdownBlockType.Paragraph = 0
```

```csharp
MNN.Unity.MNNMarkdownBlockType.Quote = 2
```

```csharp
MNN.Unity.MNNMarkdownBlockType.ThematicBreak = 7
```

```csharp
MNN.Unity.MNNMarkdownBlockType.UnorderedList = 3
```

```csharp
public const int MNN.Unity.MNNMarkdownHistoryLayout.DefaultChunkCharacters = 1024
```

```csharp
public readonly float MNN.Unity.MNNMarkdownHistoryLayout.MarkdownMeasurement.BodyHeight
```

```csharp
public readonly float MNN.Unity.MNNMarkdownHistoryLayout.MarkdownMeasurement.TotalHeight
```

```csharp
MNN.Unity.MNNMarkdownImageFailure.Cancelled = 7
```

```csharp
MNN.Unity.MNNMarkdownImageFailure.Decode = 6
```

```csharp
MNN.Unity.MNNMarkdownImageFailure.DecodedSizeLimit = 5
```

```csharp
MNN.Unity.MNNMarkdownImageFailure.Download = 2
```

```csharp
MNN.Unity.MNNMarkdownImageFailure.EncodedSizeLimit = 3
```

```csharp
MNN.Unity.MNNMarkdownImageFailure.InvalidUrl = 1
```

```csharp
MNN.Unity.MNNMarkdownImageFailure.None = 0
```

```csharp
MNN.Unity.MNNMarkdownImageFailure.UnsupportedImage = 4
```

```csharp
public const int MNN.Unity.MNNMarkdownImageRequest.DefaultMaximumEncodedBytes = 16777216
```

```csharp
public const long MNN.Unity.MNNMarkdownImageRequest.DefaultMaximumPixels = 16777216
```

```csharp
MNN.Unity.MNNMarkdownTheme.Dark = 0
```

```csharp
MNN.Unity.MNNMarkdownTheme.Light = 1
```

```csharp
public const string MNN.Unity.MNNMiniMindO.ConfigFileName = "mnn_minimind_o.json"
```

```csharp
MNN.Unity.MNNModelCapabilities.AudioInput = 2
```

```csharp
MNN.Unity.MNNModelCapabilities.SpeechOutput = 4
```

```csharp
MNN.Unity.MNNModelCapabilities.Text = 0
```

```csharp
MNN.Unity.MNNModelCapabilities.Vision = 1
```

```csharp
public const int MNN.Unity.MNNOpenAIClient.SpeechSampleRate = 24000
```

```csharp
public bool MNN.Unity.MNNOpenAIConversationOptions.AcceptsAudioInput
```

```csharp
public string MNN.Unity.MNNOpenAIConversationOptions.Endpoint
```

```csharp
public int MNN.Unity.MNNOpenAIConversationOptions.MaximumCharacters
```

```csharp
public int MNN.Unity.MNNOpenAIConversationOptions.MaximumTokens
```

```csharp
public string MNN.Unity.MNNOpenAIConversationOptions.Model
```

```csharp
public string[] MNN.Unity.MNNOpenAIConversationOptions.SpeechLanguages
```

```csharp
public bool MNN.Unity.MNNOpenAIConversationOptions.SupportsProgressivePcm
```

```csharp
public float MNN.Unity.MNNOpenAIConversationOptions.Temperature
```

```csharp
public int MNN.Unity.MNNOpenAIConversationOptions.TimeoutSeconds
```

```csharp
public string MNN.Unity.MNNOpenAIConversationOptions.Voice
```

```csharp
MNN.Unity.MNNOpenAIConversationStage.Asr = 1
```

```csharp
MNN.Unity.MNNOpenAIConversationStage.Model = 0
```

```csharp
MNN.Unity.MNNOpenAIConversationStage.Tts = 2
```

```csharp
public const long MNN.Unity.MNNPocketModel.Bytes = 174160405
```

```csharp
public const string MNN.Unity.MNNPocketModel.InstallationName = "MNN-Pocket-0.3B-MNN"
```

```csharp
public const string MNN.Unity.MNNPocketModel.ModelId = "MNN/MNN-Pocket-0.3B"
```

```csharp
public const string MNN.Unity.MNNPocketModel.Repository = "MNN/MNN-Pocket-0.3B-MNN"
```

```csharp
public const string MNN.Unity.MNNPocketModel.Revision = "92aaad61ae9ecc5a560402a70d1583c08f1c6442"
```

```csharp
public const double MNN.Unity.MNNRealtimeCapabilities.MinimumRtfWindowSeconds = 1
```

```csharp
MNN.Unity.MNNRealtimeExecution.LocalMnn = 0
```

```csharp
MNN.Unity.MNNRealtimeExecution.Managed = 2
```

```csharp
MNN.Unity.MNNRealtimeExecution.RemoteApi = 1
```

```csharp
MNN.Unity.MNNRealtimeFeature.BoundedCancellation = 32
```

```csharp
MNN.Unity.MNNRealtimeFeature.BoundedScheduling = 16
```

```csharp
MNN.Unity.MNNRealtimeFeature.BufferedSpeech = 262144
```

Produces a complete waveform; no progressive PCM or throughput claim.

```csharp
MNN.Unity.MNNRealtimeFeature.ContextRecovery = 64
```

```csharp
MNN.Unity.MNNRealtimeFeature.ContinuousUnderstanding = 8
```

```csharp
MNN.Unity.MNNRealtimeFeature.DuplexDevice = 1024
```

```csharp
MNN.Unity.MNNRealtimeFeature.EchoControl = 2048
```

```csharp
MNN.Unity.MNNRealtimeFeature.HalfDuplexDevice = 524288
```

Capture is gated during response production and presentation.

```csharp
MNN.Unity.MNNRealtimeFeature.HeadphoneRoute = 8192
```

```csharp
MNN.Unity.MNNRealtimeFeature.InProcessMnn = 2
```

```csharp
MNN.Unity.MNNRealtimeFeature.IndependentTts = 512
```

```csharp
MNN.Unity.MNNRealtimeFeature.MemoryAudioInput = 4
```

```csharp
MNN.Unity.MNNRealtimeFeature.None = 0
```

```csharp
MNN.Unity.MNNRealtimeFeature.PresentationClock = 4096
```

```csharp
MNN.Unity.MNNRealtimeFeature.SpeechRecognition = 65536
```

```csharp
MNN.Unity.MNNRealtimeFeature.StableSpokenText = 256
```

```csharp
MNN.Unity.MNNRealtimeFeature.StreamingSpeech = 128
```

```csharp
MNN.Unity.MNNRealtimeFeature.SupportedAbi = 1
```

```csharp
MNN.Unity.MNNRealtimeFeature.SustainedRtfBelowOne = 16384
```

```csharp
MNN.Unity.MNNRealtimeFeature.TextInput = 32768
```

```csharp
MNN.Unity.MNNRealtimeFeature.VoiceActivityDetection = 131072
```

```csharp
MNN.Unity.MNNRealtimeMode.InterruptibleConversation = 0
```

```csharp
MNN.Unity.MNNRealtimeMode.NativeDuplex = 1
```

```csharp
MNN.Unity.MNNRealtimeRoute.AudioTextToSpeech = 3
```

```csharp
MNN.Unity.MNNRealtimeRoute.CascadedSpeech = 4
```

```csharp
MNN.Unity.MNNRealtimeRoute.EndToEndSpeech = 2
```

```csharp
MNN.Unity.MNNRealtimeRoute.NativeDuplexSpeech = 0
```

```csharp
MNN.Unity.MNNRealtimeRoute.NativeDuplexTextToSpeech = 1
```

```csharp
MNN.Unity.MNNRealtimeRoute.TextChat = 5
```

```csharp
MNN.Unity.MNNRealtimeSessionState.Created = 0
```

```csharp
MNN.Unity.MNNRealtimeSessionState.Faulted = 6
```

```csharp
MNN.Unity.MNNRealtimeSessionState.Preparing = 1
```

```csharp
MNN.Unity.MNNRealtimeSessionState.Recovering = 3
```

```csharp
MNN.Unity.MNNRealtimeSessionState.Running = 2
```

```csharp
MNN.Unity.MNNRealtimeSessionState.Stopped = 5
```

```csharp
MNN.Unity.MNNRealtimeSessionState.Stopping = 4
```

```csharp
MNN.Unity.MNNSpeechRoute.IndependentTts = 1
```

```csharp
MNN.Unity.MNNSpeechRoute.OmniSpeech = 0
```

```csharp
public MNN.Samples.SiriAI.Harnesses.ConfiguredHarness.ConfiguredHarness()
```

```csharp
public MNN.Samples.SiriAI.Harnesses.DesktopHostServiceClient.DesktopHostServiceClient(string runtime, string assembly, string directory, bool mobile)
```

```csharp
public System.Threading.Tasks.Task<MNN.Samples.SiriAI.Harnesses.HarnessInstallation[]> MNN.Samples.SiriAI.Harnesses.DesktopHostServiceClient.DiscoverAsync()
```

```csharp
public System.Threading.Tasks.Task<string> MNN.Samples.SiriAI.Harnesses.DesktopHostServiceClient.ExecuteAsync(string command)
```

```csharp
public System.Threading.Tasks.Task<string> MNN.Samples.SiriAI.Harnesses.DesktopHostServiceClient.InstallBackendAsync(string backend)
```

```csharp
public System.Threading.Tasks.Task<string> MNN.Samples.SiriAI.Harnesses.DesktopHostServiceClient.PrepareAsync(string id, string config, System.Collections.Generic.IDictionary<string, string> environment = null)
```

```csharp
public System.Threading.Tasks.Task<MNN.Samples.SiriAI.Harnesses.HarnessLocalSetup> MNN.Samples.SiriAI.Harnesses.DesktopHostServiceClient.PrepareBackendAsync(string backend, string workspace, int port = 8765, string endpoint = null, string model = null, System.Collections.Generic.IDictionary<string, string> environment = null)
```

```csharp
public string MNN.Samples.SiriAI.Harnesses.DesktopHostServiceClient.ReadLocalAccessToken(MNN.Samples.SiriAI.Harnesses.HarnessLocalSetup setup)
```

```csharp
public MNN.Samples.SiriAI.Harnesses.FileTaskStore.FileTaskStore(string directory)
```

```csharp
public void MNN.Samples.SiriAI.Harnesses.FileTaskStore.Dispose()
```

```csharp
public MNN.Samples.SiriAI.Harnesses.HarnessTask[] MNN.Samples.SiriAI.Harnesses.FileTaskStore.Load()
```

```csharp
public void MNN.Samples.SiriAI.Harnesses.FileTaskStore.Save(MNN.Samples.SiriAI.Harnesses.HarnessTask[] tasks)
```

```csharp
public MNN.Samples.SiriAI.Harnesses.HarnessAcpCapabilities.HarnessAcpCapabilities()
```

```csharp
public MNN.Samples.SiriAI.Harnesses.HarnessActivity.HarnessActivity(MNN.Samples.SiriAI.Harnesses.HarnessWaitingFor waitingFor, string requestId, string summary, MNN.Samples.SiriAI.Harnesses.HarnessArtifact[] artifacts = null, MNN.Samples.SiriAI.Harnesses.HarnessInputForm inputForm = null, MNN.Samples.SiriAI.Harnesses.HarnessExecutionKind executionKind = MNN.Samples.SiriAI.Harnesses.HarnessExecutionKind.Activity, string executionDetail = null, string executionState = "Running", MNN.Samples.SiriAI.Harnesses.HarnessInputPrompt inputPrompt = null, MNN.Samples.SiriAI.Harnesses.HarnessSessionConfiguration sessionConfiguration = null, MNN.Samples.SiriAI.Harnesses.HarnessNativeToolState nativeTools = null)
```

```csharp
public MNN.Samples.SiriAI.Harnesses.HarnessArtifact.HarnessArtifact(string id, string name, string description, MNN.Samples.SiriAI.Harnesses.HarnessContentPart[] parts, string sourceTaskId = null, string sourceArtifactId = null)
```

```csharp
public static int MNN.Samples.SiriAI.Harnesses.HarnessArtifactPresentation.DecodedBytes(MNN.Samples.SiriAI.Harnesses.HarnessContentPart part)
```

```csharp
public static string MNN.Samples.SiriAI.Harnesses.HarnessArtifactPresentation.Describe(MNN.Samples.SiriAI.Harnesses.HarnessContentPart part)
```

```csharp
public static string MNN.Samples.SiriAI.Harnesses.HarnessArtifactPresentation.Summary(MNN.Samples.SiriAI.Harnesses.HarnessArtifact[] artifacts, int maximumCharacters = 2000)
```

```csharp
public static string MNN.Samples.SiriAI.Harnesses.HarnessBackendCatalog.ChooseLocal(System.Collections.Generic.IEnumerable<MNN.Samples.SiriAI.Harnesses.HarnessInstallation> installations, string preferred = null)
```

```csharp
public static string MNN.Samples.SiriAI.Harnesses.HarnessBackendCatalog.DisplayName(string id)
```

```csharp
public static MNN.Samples.SiriAI.Harnesses.HarnessBackendDefinition MNN.Samples.SiriAI.Harnesses.HarnessBackendCatalog.Get(string id)
```

```csharp
public MNN.Samples.SiriAI.Harnesses.HarnessInstallComponent[] MNN.Samples.SiriAI.Harnesses.HarnessBackendDefinition.MissingComponents(MNN.Samples.SiriAI.Harnesses.HarnessInstallation installation)
```

```csharp
public MNN.Samples.SiriAI.Harnesses.HarnessBackendVersion.HarnessBackendVersion()
```

```csharp
public MNN.Samples.SiriAI.Harnesses.HarnessCapabilities.HarnessCapabilities()
```

```csharp
public static MNN.Samples.SiriAI.Harnesses.HarnessCollectedResult MNN.Samples.SiriAI.Harnesses.HarnessCollectedResult.Capture(MNN.Samples.SiriAI.Harnesses.HarnessTask task)
```

```csharp
public static string MNN.Samples.SiriAI.Harnesses.HarnessCollectedResult.ConversationFor(string parentTaskId)
```

```csharp
public static MNN.Samples.SiriAI.Harnesses.HarnessArtifact[] MNN.Samples.SiriAI.Harnesses.HarnessCollectedResult.MergeArtifacts(MNN.Samples.SiriAI.Harnesses.HarnessArtifact[] own, MNN.Samples.SiriAI.Harnesses.HarnessTask[] children)
```

```csharp
public static string MNN.Samples.SiriAI.Harnesses.HarnessContentFiles.Export(string directory, MNN.Samples.SiriAI.Harnesses.HarnessContentPart part)
```

```csharp
public static MNN.Samples.SiriAI.Harnesses.HarnessContentPart MNN.Samples.SiriAI.Harnesses.HarnessContentFiles.Import(string path)
```

```csharp
public MNN.Samples.SiriAI.Harnesses.HarnessContentKind.HarnessContentKind()
```

```csharp
public MNN.Samples.SiriAI.Harnesses.HarnessContentPart.HarnessContentPart(MNN.Samples.SiriAI.Harnesses.HarnessContentKind kind, string payload, string mediaType = null, string fileName = null)
```

```csharp
public static string MNN.Samples.SiriAI.Harnesses.HarnessDesktopPaths.FindHost(System.Collections.Generic.IEnumerable<string> roots)
```

```csharp
public static string MNN.Samples.SiriAI.Harnesses.HarnessDesktopPaths.FindRuntime()
```

```csharp
public static string MNN.Samples.SiriAI.Harnesses.HarnessDesktopPaths.FindWorkspace(System.Collections.Generic.IEnumerable<string> candidates)
```

Resolve saved, conversation and application project candidates in caller-defined order, without creating folders.

```csharp
public MNN.Samples.SiriAI.Harnesses.HarnessExecutionKind.HarnessExecutionKind()
```

```csharp
public static System.Uri MNN.Samples.SiriAI.Harnesses.HarnessHostAddress.Parse(string value, bool mobile = false)
```

```csharp
public MNN.Samples.SiriAI.Harnesses.HarnessHostCatalog.HarnessHostCatalog()
```

```csharp
public string MNN.Samples.SiriAI.Harnesses.HarnessHostCatalog.ChooseBackend(string preferred = null)
```

Keep a saved choice when available; otherwise prefer a configured agent over bare text completion.

```csharp
public string MNN.Samples.SiriAI.Harnesses.HarnessHostCatalog.ChooseWorkspace(string preferred = null)
```

Choose only among workspace keys advertised by this authenticated host.

```csharp
public MNN.Samples.SiriAI.Harnesses.HarnessHostHealth.HarnessHostHealth()
```

```csharp
public MNN.Samples.SiriAI.Harnesses.HarnessHostProfile.HarnessHostProfile(string id, string name, string address, string harnessId, string workspaceKey)
```

```csharp
public MNN.Samples.SiriAI.Harnesses.HarnessInputAction.HarnessInputAction()
```

```csharp
public MNN.Samples.SiriAI.Harnesses.HarnessInputChoice.HarnessInputChoice()
```

```csharp
public MNN.Samples.SiriAI.Harnesses.HarnessInputField.HarnessInputField()
```

```csharp
public MNN.Samples.SiriAI.Harnesses.HarnessInputForm.HarnessInputForm()
```

```csharp
public System.Collections.Generic.Dictionary<string, string[]> MNN.Samples.SiriAI.Harnesses.HarnessInputForm.CreateDraft()
```

```csharp
public string MNN.Samples.SiriAI.Harnesses.HarnessInputForm.EncodeValues(System.Collections.Generic.IDictionary<string, string[]> values)
```

```csharp
public static string[] MNN.Samples.SiriAI.Harnesses.HarnessInputForm.InitialValue(MNN.Samples.SiriAI.Harnesses.HarnessInputField field)
```

```csharp
public MNN.Samples.SiriAI.Harnesses.HarnessInputMode.HarnessInputMode()
```

```csharp
public MNN.Samples.SiriAI.Harnesses.HarnessInputPrompt.HarnessInputPrompt(MNN.Samples.SiriAI.Harnesses.HarnessInputMode mode, string url = null, string nativeId = null, bool backendCompleted = false)
```

```csharp
public MNN.Samples.SiriAI.Harnesses.HarnessInstallation.HarnessInstallation()
```

```csharp
public MNN.Samples.SiriAI.Harnesses.HarnessInstructionState.HarnessInstructionState()
```

```csharp
public static MNN.Samples.SiriAI.Harnesses.HarnessInstallation[] MNN.Samples.SiriAI.Harnesses.HarnessLocalDiscovery.Scan(System.Collections.Generic.IEnumerable<string> searchDirectories = null)
```

```csharp
public MNN.Samples.SiriAI.Harnesses.HarnessLocalSetup.HarnessLocalSetup()
```

```csharp
public static string MNN.Samples.SiriAI.Harnesses.HarnessLocalSetup.ScopeFor(string backend, string workspace)
```

```csharp
public MNN.Samples.SiriAI.Harnesses.HarnessNativeHistory.HarnessNativeHistory()
```

```csharp
public MNN.Samples.SiriAI.Harnesses.HarnessNativeHistoryEntry.HarnessNativeHistoryEntry()
```

```csharp
public MNN.Samples.SiriAI.Harnesses.HarnessNativeSession.HarnessNativeSession()
```

```csharp
public MNN.Samples.SiriAI.Harnesses.HarnessNativeSessionPage.HarnessNativeSessionPage()
```

```csharp
public MNN.Samples.SiriAI.Harnesses.HarnessNativeToolCall.HarnessNativeToolCall(string toolCallId, string title, string kind, string status, string payload, string[] omittedFields = null)
```

```csharp
public MNN.Samples.SiriAI.Harnesses.HarnessNativeToolState.HarnessNativeToolState(MNN.Samples.SiriAI.Harnesses.HarnessNativeToolCall[] calls, bool truncated = false)
```

```csharp
public static MNN.Samples.SiriAI.Harnesses.HarnessOutcome MNN.Samples.SiriAI.Harnesses.HarnessOutcome.Cancelled()
```

```csharp
public static MNN.Samples.SiriAI.Harnesses.HarnessOutcome MNN.Samples.SiriAI.Harnesses.HarnessOutcome.Cancelled(string scopeDescription)
```

```csharp
public static MNN.Samples.SiriAI.Harnesses.HarnessOutcome MNN.Samples.SiriAI.Harnesses.HarnessOutcome.Completed(string text, MNN.Samples.SiriAI.Harnesses.HarnessArtifact[] artifacts = null)
```

```csharp
public static MNN.Samples.SiriAI.Harnesses.HarnessOutcome MNN.Samples.SiriAI.Harnesses.HarnessOutcome.Failed(string text)
```

```csharp
public static MNN.Samples.SiriAI.Harnesses.HarnessOutcome MNN.Samples.SiriAI.Harnesses.HarnessOutcome.Interrupted(string text)
```

```csharp
public MNN.Samples.SiriAI.Harnesses.HarnessOutcome MNN.Samples.SiriAI.Harnesses.HarnessOutcome.WithArtifacts(MNN.Samples.SiriAI.Harnesses.HarnessArtifact[] artifacts)
```

```csharp
public MNN.Samples.SiriAI.Harnesses.HarnessOutcome MNN.Samples.SiriAI.Harnesses.HarnessOutcome.WithCollectedResults(MNN.Samples.SiriAI.Harnesses.HarnessCollectedResult[] sources)
```

```csharp
public MNN.Samples.SiriAI.Harnesses.HarnessProfileCatalog.HarnessProfileCatalog(string directory)
```

```csharp
public void MNN.Samples.SiriAI.Harnesses.HarnessProfileCatalog.Dispose()
```

```csharp
public MNN.Samples.SiriAI.Harnesses.HarnessHostProfile MNN.Samples.SiriAI.Harnesses.HarnessProfileCatalog.Get(string id)
```

```csharp
public bool MNN.Samples.SiriAI.Harnesses.HarnessProfileCatalog.Remove(string id)
```

Remove a saved profile atomically. Removing the selection leaves no selected host; a missing identity is a no-op.

```csharp
public void MNN.Samples.SiriAI.Harnesses.HarnessProfileCatalog.Save(MNN.Samples.SiriAI.Harnesses.HarnessHostProfile profile, bool select)
```

```csharp
public void MNN.Samples.SiriAI.Harnesses.HarnessProfileCatalog.Select(string id)
```

```csharp
public MNN.Samples.SiriAI.Harnesses.HarnessReadiness.HarnessReadiness()
```

```csharp
public MNN.Samples.SiriAI.Harnesses.HarnessReadinessRequest.HarnessReadinessRequest()
```

```csharp
public MNN.Samples.SiriAI.Harnesses.HarnessResultConnection.HarnessResultConnection(MNN.Samples.SiriAI.Harnesses.RemoteTaskClient client, string conversationId)
```

```csharp
public MNN.Samples.SiriAI.Harnesses.HarnessResultFeedback.HarnessResultFeedback(System.Func<MNN.Samples.SiriAI.Harnesses.HarnessResultConnection> connection, MNN.Samples.SiriAI.Harnesses.IHarnessResultPresenter presenter, System.Action<System.Exception> report = null)
```

```csharp
public System.Threading.Tasks.Task MNN.Samples.SiriAI.Harnesses.HarnessResultFeedback.RunAsync(System.Threading.CancellationToken cancellation)
```

```csharp
public MNN.Samples.SiriAI.Harnesses.HarnessSessionChoice.HarnessSessionChoice()
```

```csharp
public MNN.Samples.SiriAI.Harnesses.HarnessSessionConfiguration.HarnessSessionConfiguration()
```

```csharp
public MNN.Samples.SiriAI.Harnesses.HarnessSessionOption.HarnessSessionOption()
```

```csharp
public MNN.Samples.SiriAI.Harnesses.HarnessSkillCatalog.HarnessSkillCatalog()
```

```csharp
public MNN.Samples.SiriAI.Harnesses.HarnessSkillClient.HarnessSkillClient(MNN.Samples.SiriAI.Harnesses.RemoteTaskClient remote, string journalDirectory)
```

```csharp
public void MNN.Samples.SiriAI.Harnesses.HarnessSkillClient.Dispose()
```

```csharp
public System.Threading.Tasks.Task MNN.Samples.SiriAI.Harnesses.HarnessSkillClient.RetryAsync(System.Threading.CancellationToken cancellation)
```

```csharp
public System.Threading.Tasks.Task<MNN.Samples.SiriAI.Harnesses.HarnessTask> MNN.Samples.SiriAI.Harnesses.HarnessSkillClient.SubmitAsync(MNN.Samples.SiriAI.Harnesses.HarnessSkillRequest request, System.Threading.CancellationToken cancellation)
```

```csharp
public MNN.Samples.SiriAI.Harnesses.HarnessSkillRequest.HarnessSkillRequest()
```

```csharp
public void MNN.Samples.SiriAI.Harnesses.HarnessSkillRequest.Validate()
```

```csharp
public MNN.Samples.SiriAI.Harnesses.HarnessSkillTarget.HarnessSkillTarget()
```

```csharp
public MNN.Samples.SiriAI.Harnesses.HarnessTask.HarnessTask()
```

```csharp
public MNN.Samples.SiriAI.Harnesses.HarnessTaskDashboard.HarnessTaskDashboard()
```

```csharp
public bool MNN.Samples.SiriAI.Harnesses.HarnessTaskDashboard.Apply(long generation, MNN.Samples.SiriAI.Harnesses.HarnessTask[] tasks)
```

```csharp
public MNN.Samples.SiriAI.Harnesses.TaskControlRequest MNN.Samples.SiriAI.Harnesses.HarnessTaskDashboard.CreateReply(long generation, string taskId, string requestId, string text, bool allow, MNN.Samples.SiriAI.Harnesses.HarnessInputAction action = MNN.Samples.SiriAI.Harnesses.HarnessInputAction.Answer)
```

```csharp
public string MNN.Samples.SiriAI.Harnesses.HarnessTaskDashboard.InputUrl(long generation, string taskId, string requestId)
```

```csharp
public long MNN.Samples.SiriAI.Harnesses.HarnessTaskDashboard.ResetScope()
```

```csharp
public MNN.Samples.SiriAI.Harnesses.HarnessTaskDraft.HarnessTaskDraft()
```

```csharp
public void MNN.Samples.SiriAI.Harnesses.HarnessTaskDraft.Add(MNN.Samples.SiriAI.Harnesses.HarnessContentPart part)
```

```csharp
public bool MNN.Samples.SiriAI.Harnesses.HarnessTaskDraft.ClearIfCurrent(long scope, long revision)
```

```csharp
public void MNN.Samples.SiriAI.Harnesses.HarnessTaskDraft.RemoveAt(int index)
```

```csharp
public void MNN.Samples.SiriAI.Harnesses.HarnessTaskDraft.SetScope(long scope)
```

```csharp
public MNN.Samples.SiriAI.Harnesses.HarnessTaskDraftSnapshot MNN.Samples.SiriAI.Harnesses.HarnessTaskDraft.Snapshot()
```

```csharp
public bool MNN.Samples.SiriAI.Harnesses.HarnessTaskDraft.TryAdd(long scope, long revision, MNN.Samples.SiriAI.Harnesses.HarnessContentPart part)
```

```csharp
public MNN.Samples.SiriAI.Harnesses.HarnessTaskObserver.HarnessTaskObserver(MNN.Samples.SiriAI.Harnesses.RemoteTaskClient client, MNN.Samples.SiriAI.Harnesses.SubmissionJournal journal)
```

```csharp
public System.Threading.Tasks.Task MNN.Samples.SiriAI.Harnesses.HarnessTaskObserver.RunAsync(System.Action<MNN.Samples.SiriAI.Harnesses.HarnessTask> changed, System.Action<System.Exception> report, System.TimeSpan interval, System.Threading.CancellationToken cancellation)
```

```csharp
public MNN.Samples.SiriAI.Harnesses.HarnessTaskState.HarnessTaskState()
```

```csharp
public MNN.Samples.SiriAI.Harnesses.HarnessTaskTool.HarnessTaskTool(MNN.Samples.SiriAI.Harnesses.RemoteTaskClient client, System.Func<MNN.Samples.SiriAI.Harnesses.SubmitTaskRequest, System.Threading.CancellationToken, System.Threading.Tasks.Task<MNN.Samples.SiriAI.Harnesses.HarnessTask>> submit, string conversation, MNN.Samples.SiriAI.Harnesses.HarnessTaskToolOperation operation, string harness, string workspace, bool network, string unavailableMessage = null)
```

```csharp
public System.Threading.Tasks.Task<string> MNN.Samples.SiriAI.Harnesses.HarnessTaskTool.ExecuteAsync(string input, System.Threading.CancellationToken cancellation)
```

```csharp
public MNN.Samples.SiriAI.Harnesses.HarnessTaskToolOperation.HarnessTaskToolOperation()
```

```csharp
public MNN.Samples.SiriAI.Harnesses.HarnessWaitingFor.HarnessWaitingFor()
```

```csharp
System.Threading.Tasks.Task<bool> MNN.Samples.SiriAI.Harnesses.IHarnessAdapter.CancelAsync(MNN.Samples.SiriAI.Harnesses.HarnessTask task, System.Threading.CancellationToken rpcCancellation)
```

```csharp
System.Threading.Tasks.Task MNN.Samples.SiriAI.Harnesses.IHarnessAdapter.RespondAsync(MNN.Samples.SiriAI.Harnesses.HarnessTask task, string requestId, string reply, bool allow, System.Threading.CancellationToken rpcCancellation)
```

```csharp
System.Threading.Tasks.Task<MNN.Samples.SiriAI.Harnesses.HarnessOutcome> MNN.Samples.SiriAI.Harnesses.IHarnessAdapter.RunAsync(MNN.Samples.SiriAI.Harnesses.HarnessTask task, System.Action<MNN.Samples.SiriAI.Harnesses.HarnessActivity> activity, System.Threading.CancellationToken hostStopping)
```

```csharp
System.Threading.Tasks.Task MNN.Samples.SiriAI.Harnesses.IHarnessAdapter.UpdateAsync(MNN.Samples.SiriAI.Harnesses.HarnessTask task, string instruction, System.Threading.CancellationToken rpcCancellation)
```

```csharp
string MNN.Samples.SiriAI.Harnesses.IHarnessAdmissionCheckpoint.CreateAdmissionCheckpoint(MNN.Samples.SiriAI.Harnesses.TaskSubmission request)
```

```csharp
System.Threading.Tasks.Task<MNN.Samples.SiriAI.Harnesses.HarnessOutcome> MNN.Samples.SiriAI.Harnesses.IHarnessCancellationOutcomeAdapter.CancelWithOutcomeAsync(MNN.Samples.SiriAI.Harnesses.HarnessTask task, System.Threading.CancellationToken rpcCancellation)
```

```csharp
System.Threading.Tasks.Task MNN.Samples.SiriAI.Harnesses.IHarnessInputActionAdapter.RespondInputAsync(MNN.Samples.SiriAI.Harnesses.HarnessTask task, string requestId, string reply, MNN.Samples.SiriAI.Harnesses.HarnessInputAction action, System.Threading.CancellationToken cancellation)
```

```csharp
System.Threading.Tasks.Task MNN.Samples.SiriAI.Harnesses.IHarnessInstructionAdapter.UpdateInstructionAsync(MNN.Samples.SiriAI.Harnesses.HarnessTask task, MNN.Samples.SiriAI.Harnesses.HarnessInstruction instruction, System.Threading.CancellationToken cancellation)
```

```csharp
void MNN.Samples.SiriAI.Harnesses.IHarnessInstructionAdapter.ValidateInstruction(MNN.Samples.SiriAI.Harnesses.HarnessTask task, MNN.Samples.SiriAI.Harnesses.HarnessInstruction instruction)
```

```csharp
System.Threading.Tasks.Task MNN.Samples.SiriAI.Harnesses.IHarnessResultPresenter.InterruptAsync(MNN.Samples.SiriAI.Harnesses.IHarnessResultReply reply)
```

```csharp
MNN.Samples.SiriAI.Harnesses.IHarnessResultReply MNN.Samples.SiriAI.Harnesses.IHarnessResultPresenter.TryBeginReply(string eventId, string context, System.Threading.CancellationToken cancellation)
```

```csharp
void MNN.Samples.SiriAI.Harnesses.IHarnessSubmissionValidator.ValidateSubmission(MNN.Samples.SiriAI.Harnesses.TaskSubmission submission)
```

```csharp
MNN.Samples.SiriAI.Harnesses.HarnessTask[] MNN.Samples.SiriAI.Harnesses.IHarnessTaskStore.Load()
```

```csharp
void MNN.Samples.SiriAI.Harnesses.IHarnessTaskStore.Save(MNN.Samples.SiriAI.Harnesses.HarnessTask[] tasks)
```

```csharp
string MNN.Samples.SiriAI.Harnesses.IIsolatedCoordinatorHarness.CoordinatorLane(MNN.Samples.SiriAI.Harnesses.HarnessTask task)
```

```csharp
System.Threading.Tasks.Task<MNN.Samples.SiriAI.Harnesses.HarnessOutcome> MNN.Samples.SiriAI.Harnesses.ILocalCancellationHarnessAdapter.RunCancellableAsync(MNN.Samples.SiriAI.Harnesses.HarnessTask task, System.Action<MNN.Samples.SiriAI.Harnesses.HarnessActivity> activity, System.Threading.CancellationToken cancellationRequested, System.Threading.CancellationToken hostStopping)
```

```csharp
bool MNN.Samples.SiriAI.Harnesses.IRecoverableHarnessAdapter.CanRecover(string checkpoint)
```

```csharp
System.Threading.Tasks.Task<MNN.Samples.SiriAI.Harnesses.HarnessOutcome> MNN.Samples.SiriAI.Harnesses.IRecoverableHarnessAdapter.RunDurableAsync(MNN.Samples.SiriAI.Harnesses.HarnessTask task, System.Action<MNN.Samples.SiriAI.Harnesses.HarnessActivity> activity, System.Action<string> checkpoint, System.Threading.CancellationToken hostStopping)
```

```csharp
public MNN.Samples.SiriAI.Harnesses.MemoryTaskStore.MemoryTaskStore()
```

```csharp
public MNN.Samples.SiriAI.Harnesses.HarnessTask[] MNN.Samples.SiriAI.Harnesses.MemoryTaskStore.Load()
```

```csharp
public void MNN.Samples.SiriAI.Harnesses.MemoryTaskStore.Save(MNN.Samples.SiriAI.Harnesses.HarnessTask[] tasks)
```

```csharp
public MNN.Samples.SiriAI.Harnesses.PendingSubmissionException.PendingSubmissionException(string key, System.Exception cause)
```

```csharp
public MNN.Samples.SiriAI.Harnesses.PersistentTaskRuntime.PersistentTaskRuntime(MNN.Samples.SiriAI.Harnesses.IHarnessTaskStore store, System.Collections.Generic.IEnumerable<MNN.Samples.SiriAI.Harnesses.IHarnessAdapter> adapters, int maximumConcurrent = 1, int maximumRecords = 10000)
```

```csharp
public void MNN.Samples.SiriAI.Harnesses.PersistentTaskRuntime.AcknowledgeResult(string taskId, string ownerId, string leaseId, MNN.Samples.SiriAI.Harnesses.ResultReceipt receipt)
```

```csharp
public System.Threading.Tasks.Task MNN.Samples.SiriAI.Harnesses.PersistentTaskRuntime.CancelAsync(string taskId, string ownerId, System.Threading.CancellationToken cancellation)
```

```csharp
public MNN.Samples.SiriAI.Harnesses.HarnessTask MNN.Samples.SiriAI.Harnesses.PersistentTaskRuntime.ClaimResult(string ownerId, string conversationId, string clientId, System.TimeSpan duration)
```

```csharp
public void MNN.Samples.SiriAI.Harnesses.PersistentTaskRuntime.Dispose()
```

```csharp
public MNN.Samples.SiriAI.Harnesses.HarnessTask MNN.Samples.SiriAI.Harnesses.PersistentTaskRuntime.Get(string taskId, string ownerId)
```

```csharp
public MNN.Samples.SiriAI.Harnesses.HarnessTask[] MNN.Samples.SiriAI.Harnesses.PersistentTaskRuntime.List(string ownerId)
```

```csharp
public void MNN.Samples.SiriAI.Harnesses.PersistentTaskRuntime.ReleaseResult(string taskId, string ownerId, string leaseId)
```

```csharp
public void MNN.Samples.SiriAI.Harnesses.PersistentTaskRuntime.RenewResult(string taskId, string ownerId, string leaseId, System.TimeSpan duration)
```

```csharp
public System.Threading.Tasks.Task MNN.Samples.SiriAI.Harnesses.PersistentTaskRuntime.RespondAsync(string taskId, string ownerId, string requestId, string reply, bool allow, System.Threading.CancellationToken cancellation, MNN.Samples.SiriAI.Harnesses.HarnessInputAction inputAction = MNN.Samples.SiriAI.Harnesses.HarnessInputAction.Answer)
```

```csharp
public MNN.Samples.SiriAI.Harnesses.HarnessTask MNN.Samples.SiriAI.Harnesses.PersistentTaskRuntime.Submit(MNN.Samples.SiriAI.Harnesses.TaskSubmission request)
```

```csharp
public System.Threading.Tasks.Task MNN.Samples.SiriAI.Harnesses.PersistentTaskRuntime.UpdateAsync(string taskId, string ownerId, string instruction, System.Threading.CancellationToken cancellation, string instructionId = null, bool waitForDelivery = true)
```

```csharp
public System.Threading.Tasks.Task MNN.Samples.SiriAI.Harnesses.PersistentTaskRuntime.UpdateContentAsync(string taskId, string ownerId, string instruction, MNN.Samples.SiriAI.Harnesses.HarnessContentPart[] attachments, System.Threading.CancellationToken cancellation, string instructionId = null, bool waitForDelivery = true)
```

```csharp
public System.Threading.Tasks.Task MNN.Samples.SiriAI.Harnesses.PersistentTaskRuntime.WaitForIdleAsync()
```

```csharp
public MNN.Samples.SiriAI.Harnesses.RemoteTaskClient.RemoteTaskClient(System.Uri endpoint, string token, System.Net.Http.HttpMessageHandler handler = null)
```

```csharp
public System.Threading.Tasks.Task<MNN.Samples.SiriAI.Harnesses.HarnessTask> MNN.Samples.SiriAI.Harnesses.RemoteTaskClient.AcknowledgeAsync(string taskId, MNN.Samples.SiriAI.Harnesses.ResultReceiptRequest request, System.Threading.CancellationToken cancellation)
```

```csharp
public System.Threading.Tasks.Task<MNN.Samples.SiriAI.Harnesses.HarnessTask> MNN.Samples.SiriAI.Harnesses.RemoteTaskClient.CancelAsync(string taskId, System.Threading.CancellationToken cancellation)
```

```csharp
public System.Threading.Tasks.Task<MNN.Samples.SiriAI.Harnesses.HarnessTask> MNN.Samples.SiriAI.Harnesses.RemoteTaskClient.CancelSkillOperationAsync(string id, System.Threading.CancellationToken cancellation)
```

```csharp
public System.Threading.Tasks.Task<MNN.Samples.SiriAI.Harnesses.HarnessTask> MNN.Samples.SiriAI.Harnesses.RemoteTaskClient.ClaimAsync(MNN.Samples.SiriAI.Harnesses.ResultClaimRequest request, System.Threading.CancellationToken cancellation)
```

```csharp
public void MNN.Samples.SiriAI.Harnesses.RemoteTaskClient.Dispose()
```

```csharp
public System.Threading.Tasks.Task<MNN.Samples.SiriAI.Harnesses.HarnessTask> MNN.Samples.SiriAI.Harnesses.RemoteTaskClient.GetAsync(string taskId, System.Threading.CancellationToken cancellation)
```

```csharp
public System.Threading.Tasks.Task<MNN.Samples.SiriAI.Harnesses.HarnessHostCatalog> MNN.Samples.SiriAI.Harnesses.RemoteTaskClient.GetCatalogAsync(System.Threading.CancellationToken cancellation)
```

```csharp
public System.Threading.Tasks.Task<MNN.Samples.SiriAI.Harnesses.HarnessHostHealth> MNN.Samples.SiriAI.Harnesses.RemoteTaskClient.GetHealthAsync(System.Threading.CancellationToken cancellation)
```

```csharp
public System.Threading.Tasks.Task<MNN.Samples.SiriAI.Harnesses.HarnessReadiness> MNN.Samples.SiriAI.Harnesses.RemoteTaskClient.GetReadinessAsync(string harnessId, string workspaceKey, System.Threading.CancellationToken cancellation)
```

```csharp
public System.Threading.Tasks.Task<MNN.Samples.SiriAI.Harnesses.HarnessSkillCatalog> MNN.Samples.SiriAI.Harnesses.RemoteTaskClient.GetSkillCatalogAsync(System.Threading.CancellationToken cancellation)
```

```csharp
public System.Threading.Tasks.Task<MNN.Samples.SiriAI.Harnesses.HarnessTask> MNN.Samples.SiriAI.Harnesses.RemoteTaskClient.GetSkillOperationAsync(string id, System.Threading.CancellationToken cancellation)
```

```csharp
public System.Threading.Tasks.Task<MNN.Samples.SiriAI.Harnesses.HarnessTask[]> MNN.Samples.SiriAI.Harnesses.RemoteTaskClient.ListAsync(System.Threading.CancellationToken cancellation)
```

```csharp
public System.Threading.Tasks.Task<MNN.Samples.SiriAI.Harnesses.HarnessNativeSessionPage> MNN.Samples.SiriAI.Harnesses.RemoteTaskClient.ListNativeSessionsAsync(string taskId, string cursor, System.Threading.CancellationToken cancellation)
```

```csharp
public System.Threading.Tasks.Task<MNN.Samples.SiriAI.Harnesses.HarnessTask[]> MNN.Samples.SiriAI.Harnesses.RemoteTaskClient.ListSkillOperationsAsync(System.Threading.CancellationToken cancellation)
```

```csharp
public System.Threading.Tasks.Task<MNN.Samples.SiriAI.Harnesses.HarnessNativeHistory> MNN.Samples.SiriAI.Harnesses.RemoteTaskClient.LoadNativeHistoryAsync(string taskId, System.Threading.CancellationToken cancellation)
```

```csharp
public System.Threading.Tasks.Task<MNN.Samples.SiriAI.Harnesses.HarnessReadiness> MNN.Samples.SiriAI.Harnesses.RemoteTaskClient.ProbeReadinessAsync(string harnessId, string workspaceKey, System.Threading.CancellationToken cancellation)
```

```csharp
public System.Threading.Tasks.Task<MNN.Samples.SiriAI.Harnesses.HarnessTask> MNN.Samples.SiriAI.Harnesses.RemoteTaskClient.ReleaseAsync(string taskId, string leaseId, System.Threading.CancellationToken cancellation)
```

```csharp
public System.Threading.Tasks.Task<MNN.Samples.SiriAI.Harnesses.HarnessTask> MNN.Samples.SiriAI.Harnesses.RemoteTaskClient.RenewAsync(string taskId, string leaseId, int seconds, System.Threading.CancellationToken cancellation)
```

```csharp
public System.Threading.Tasks.Task<MNN.Samples.SiriAI.Harnesses.HarnessTask> MNN.Samples.SiriAI.Harnesses.RemoteTaskClient.RespondAsync(string taskId, MNN.Samples.SiriAI.Harnesses.TaskControlRequest request, System.Threading.CancellationToken cancellation)
```

```csharp
public System.Threading.Tasks.Task<MNN.Samples.SiriAI.Harnesses.HarnessTask> MNN.Samples.SiriAI.Harnesses.RemoteTaskClient.SubmitAsync(MNN.Samples.SiriAI.Harnesses.SubmitTaskRequest request, System.Threading.CancellationToken cancellation)
```

```csharp
public System.Threading.Tasks.Task<MNN.Samples.SiriAI.Harnesses.HarnessTask> MNN.Samples.SiriAI.Harnesses.RemoteTaskClient.SubmitSkillOperationAsync(MNN.Samples.SiriAI.Harnesses.HarnessSkillRequest request, System.Threading.CancellationToken cancellation)
```

```csharp
public System.Threading.Tasks.Task<MNN.Samples.SiriAI.Harnesses.HarnessTask> MNN.Samples.SiriAI.Harnesses.RemoteTaskClient.UpdateAsync(string taskId, MNN.Samples.SiriAI.Harnesses.TaskControlRequest request, System.Threading.CancellationToken cancellation)
```

```csharp
public MNN.Samples.SiriAI.Harnesses.ResultClaimRequest.ResultClaimRequest()
```

```csharp
public MNN.Samples.SiriAI.Harnesses.ResultReceipt.ResultReceipt()
```

```csharp
public MNN.Samples.SiriAI.Harnesses.ResultReceiptRequest.ResultReceiptRequest()
```

```csharp
public MNN.Samples.SiriAI.Harnesses.SubmissionJournal.SubmissionJournal(string directory)
```

```csharp
public void MNN.Samples.SiriAI.Harnesses.SubmissionJournal.Dispose()
```

```csharp
public System.Threading.Tasks.Task MNN.Samples.SiriAI.Harnesses.SubmissionJournal.DisposeAsync()
```

```csharp
public System.Threading.Tasks.Task MNN.Samples.SiriAI.Harnesses.SubmissionJournal.ReplayAsync(System.Func<MNN.Samples.SiriAI.Harnesses.SubmitTaskRequest, System.Threading.CancellationToken, System.Threading.Tasks.Task<MNN.Samples.SiriAI.Harnesses.HarnessTask>> send, System.Threading.CancellationToken cancellation)
```

```csharp
public System.Threading.Tasks.Task<MNN.Samples.SiriAI.Harnesses.HarnessTask> MNN.Samples.SiriAI.Harnesses.SubmissionJournal.SubmitAsync(MNN.Samples.SiriAI.Harnesses.SubmitTaskRequest request, System.Func<MNN.Samples.SiriAI.Harnesses.SubmitTaskRequest, System.Threading.CancellationToken, System.Threading.Tasks.Task<MNN.Samples.SiriAI.Harnesses.HarnessTask>> send, System.Threading.CancellationToken cancellation)
```

```csharp
public MNN.Samples.SiriAI.Harnesses.SubmitTaskRequest.SubmitTaskRequest()
```

```csharp
public MNN.Samples.SiriAI.Harnesses.TaskControlRequest.TaskControlRequest()
```

```csharp
public MNN.Samples.SiriAI.Harnesses.TaskHostError.TaskHostError()
```

```csharp
public static T MNN.Samples.SiriAI.Harnesses.TaskHostJson.Read<T>(byte[] bytes)
```

```csharp
public static byte[] MNN.Samples.SiriAI.Harnesses.TaskHostJson.Write<T>(T value)
```

```csharp
public MNN.Samples.SiriAI.Harnesses.TaskHostRequestException.TaskHostRequestException(int statusCode, string message)
```

```csharp
public MNN.Samples.SiriAI.Harnesses.TaskSubmission.TaskSubmission(string ownerId, string conversationId, string submissionKey, string harnessId, string workspaceKey, string objective, MNN.Samples.SiriAI.Harnesses.HarnessContentPart[] attachments, string resultConversationId, string title)
```

```csharp
public MNN.Samples.SiriAI.Harnesses.TaskSubmission.TaskSubmission(string ownerId, string conversationId, string submissionKey, string harnessId, string workspaceKey, string objective, MNN.Samples.SiriAI.Harnesses.HarnessContentPart[] attachments = null, string resultConversationId = null, string title = null, bool isolatedContext = false)
```

```csharp
public MNN.Samples.SiriAI.Harnesses.TaskUpdateRequest.TaskUpdateRequest()
```

```csharp
System.Threading.Tasks.Task<string> MNN.Unity.IMNNAgentTool.ExecuteAsync(string input, System.Threading.CancellationToken token)
```

```csharp
bool MNN.Unity.IMNNAgentTraceSource.TryReadTrace(out MNN.Unity.MNNConversationTranscriptEntry entry)
```

```csharp
System.Threading.Tasks.Task<string> MNN.Unity.IMNNConversationAsrProvider.TranscribeAsync(float[] audio, int sampleRate, System.Threading.CancellationToken token)
```

```csharp
MNN.Unity.IMNNRealtimeVoiceInputProcessor MNN.Unity.IMNNConversationContext.CreateVoiceInputProcessor(MNN.Unity.MNNPcmFormat format, int maximumBlockFrames, MNN.Unity.IMNNRealtimeVoiceActivity activity)
```

```csharp
System.Threading.Tasks.Task MNN.Unity.IMNNConversationContext.InterruptAsync()
```

```csharp
System.Threading.Tasks.Task<float[]> MNN.Unity.IMNNConversationContext.PrepareVoiceAsync(float[] samples, System.Threading.CancellationToken token)
```

```csharp
System.Threading.Tasks.Task<bool> MNN.Unity.IMNNConversationContext.PublishAssistantSnapshotAsync(MNN.Unity.MNNResponseId response, System.Text.StringBuilder text, System.Threading.CancellationToken token)
```

```csharp
void MNN.Unity.IMNNConversationContext.PublishInputWarning(string message)
```

```csharp
void MNN.Unity.IMNNConversationContext.PublishRecognitionPreview(int revision, string text)
```

```csharp
bool MNN.Unity.IMNNConversationContext.RequestRealtimeExit(int revision)
```

```csharp
void MNN.Unity.IMNNConversationContext.ResetIdentity()
```

```csharp
MNN.Unity.MNNConversationWakeHandoff MNN.Unity.IMNNConversationContext.TakeWakeCommand()
```

```csharp
bool MNN.Unity.IMNNConversationContext.TryPublishAssistantSnapshot(MNN.Unity.MNNResponseId response, System.Text.StringBuilder text)
```

```csharp
bool MNN.Unity.IMNNConversationContext.TryPublishStage(MNN.Unity.MNNResponseId response, MNN.Unity.MNNConversationStage stage)
```

```csharp
bool MNN.Unity.IMNNConversationContext.TryPublishUserText(string text)
```

```csharp
System.Threading.Tasks.Task MNN.Unity.IMNNConversationEventProvider.GenerateEventResponseAsync(System.Collections.Generic.IReadOnlyList<MNN.Unity.MNNOpenAIMessage> history, MNN.Unity.MNNConversationEvent input, System.Func<string, System.Threading.Tasks.Task> delta, System.Threading.CancellationToken token)
```

```csharp
bool MNN.Unity.IMNNConversationInput.Consume(float[] frame, bool speechActive = false, long enqueuedMonotonicTicks = 0, bool discontinuity = false)
```

```csharp
bool MNN.Unity.IMNNConversationInput.TryAcceptRealtime(int revision, System.Action accept)
```

```csharp
System.Threading.Tasks.Task MNN.Unity.IMNNConversationProvider.PrepareAsync(System.Threading.CancellationToken token)
```

```csharp
MNN.Unity.IMNNRealtimeSessionRuntime MNN.Unity.IMNNConversationRealtimeProvider.CreateRuntime(MNN.Unity.IMNNConversationContext context, MNN.Unity.MNNRealtimeOutputPipeline output)
```

```csharp
string MNN.Unity.IMNNConversationRecognitionStream.AcceptWaveform(float[] samples, int offset, int count, System.Threading.CancellationToken token)
```

```csharp
void MNN.Unity.IMNNConversationRecognitionStream.Reset()
```

```csharp
System.Threading.Tasks.Task MNN.Unity.IMNNConversationRequestPlanner.PlanRequestAsync(System.Collections.Generic.IReadOnlyList<MNN.Unity.MNNOpenAIMessage> prompt, System.Func<string, System.Threading.Tasks.Task> delta, System.Threading.CancellationToken token)
```

```csharp
System.Threading.Tasks.Task MNN.Unity.IMNNConversationResponseProvider.GenerateResponseAsync(System.Collections.Generic.IReadOnlyList<MNN.Unity.MNNOpenAIMessage> history, string instructions, System.Func<string, System.Threading.Tasks.Task> textDelta, System.Threading.CancellationToken token)
```

```csharp
System.Threading.Tasks.Task<MNN.Unity.MNNMultimodalResult> MNN.Unity.IMNNConversationSpeechModelProvider.GenerateSpeechAsync(System.Collections.Generic.IReadOnlyList<MNN.Unity.MNNOpenAIMessage> history, System.Func<string, System.Threading.Tasks.Task> textDelta, System.Threading.CancellationToken token)
```

```csharp
MNN.Unity.IMNNConversationRecognitionStream MNN.Unity.IMNNConversationStreamingAsrProvider.CreateStream()
```

```csharp
System.Threading.Tasks.Task MNN.Unity.IMNNConversationTextAssistanceProvider.GenerateAssistanceAsync(MNN.Unity.MNNConversationTextTask task, System.Collections.Generic.IReadOnlyList<MNN.Unity.MNNOpenAIMessage> prompt, System.Func<string, System.Threading.Tasks.Task> textDelta, System.Threading.CancellationToken token)
```

```csharp
System.Threading.Tasks.Task MNN.Unity.IMNNConversationTextProvider.GenerateAsync(System.Collections.Generic.IReadOnlyList<MNN.Unity.MNNOpenAIMessage> history, float[] audio, int sampleRate, System.Func<string, System.Threading.Tasks.Task> textDelta, System.Threading.CancellationToken token)
```

```csharp
System.Threading.Tasks.Task<bool> MNN.Unity.IMNNConversationTranscriptTurnProvider.IsTurnCompleteAsync(float[] utterance, int sampleRate, string transcript, System.Threading.CancellationToken token)
```

```csharp
System.Threading.Tasks.Task MNN.Unity.IMNNConversationTtsProvider.SynthesizeAsync(string text, System.Func<float[], int, System.Threading.Tasks.Task> pcm, System.Threading.CancellationToken token)
```

```csharp
System.Threading.Tasks.Task<bool> MNN.Unity.IMNNConversationTurnProvider.IsTurnCompleteAsync(float[] utterance, int sampleRate, System.Threading.CancellationToken token)
```

```csharp
bool MNN.Unity.IMNNConversationVadProvider.IsSpeech(float[] monoFrame, int count, int sampleRate)
```

```csharp
void MNN.Unity.IMNNConversationVadProvider.Reset()
```

```csharp
System.Threading.Tasks.Task MNN.Unity.IMNNConversationVoiceIntentProvider.ClassifyVoiceIntentAsync(System.Collections.Generic.IReadOnlyList<MNN.Unity.MNNOpenAIMessage> prompt, System.Func<string, System.Threading.Tasks.Task> textDelta, System.Threading.CancellationToken token)
```

```csharp
System.Threading.Tasks.Task<MNN.Unity.IMNNRealtimeSessionRuntime> MNN.Unity.IMNNRealtimeRuntimeFactory.CreateAsync(MNN.Unity.MNNRealtimeReport report, MNN.Unity.MNNRealtimeOutputPipeline output, System.Threading.CancellationToken cancellationToken)
```

Transfer sole ownership on successful return; on throw, clean up all acquired resources yourself. Do not start capture, generation or output here. Prepare restores application-owned committed user history and only output.GetSegment(i).IsPresented assistant history; never replay cancelled speech. Keep the shared output format and presentation timeline across device instances, and drain final receipts/presentation before old runtime quiescence completes. Dispatch Unity work to its main thread. Report only route-specific unavailability using MNNRealtimeRouteUnavailableException with report.Route. Permission, cancellation, ABI and shared device failures must retain their original exception types.

```csharp
System.Threading.Tasks.Task MNN.Unity.IMNNRealtimeSessionRuntime.DisposeAsync()
```

```csharp
System.Threading.Tasks.Task MNN.Unity.IMNNRealtimeSessionRuntime.PrepareAsync(System.Threading.CancellationToken cancellationToken)
```

```csharp
System.Threading.Tasks.Task MNN.Unity.IMNNRealtimeSessionRuntime.RequestCancellationAsync(MNN.Unity.MNNResponseId response)
```

```csharp
System.Threading.Tasks.Task MNN.Unity.IMNNRealtimeSessionRuntime.RestoreContextAsync(MNN.Unity.MNNResponseId interruptedResponse)
```

```csharp
System.Threading.Tasks.Task MNN.Unity.IMNNRealtimeSessionRuntime.StartListeningAsync(System.Threading.CancellationToken cancellationToken)
```

```csharp
System.Threading.Tasks.Task MNN.Unity.IMNNRealtimeSessionRuntime.StopListeningAsync()
```

```csharp
System.Threading.Tasks.Task MNN.Unity.IMNNRealtimeSessionRuntime.WaitForInferenceQuiescenceAsync(MNN.Unity.MNNResponseId response)
```

```csharp
System.Threading.Tasks.Task MNN.Unity.IMNNRealtimeSessionRuntime.WaitForQuiescenceAsync()
```

```csharp
public MNN.Unity.MNNAgentOptions.MNNAgentOptions()
```

```csharp
public static MNN.Unity.MNNAgentPlan MNN.Unity.MNNAgentPlan.Automatic()
```

```csharp
public static MNN.Unity.MNNAgentPlan MNN.Unity.MNNAgentPlan.Clarify(string question)
```

```csharp
public static MNN.Unity.MNNAgentPlan MNN.Unity.MNNAgentPlan.Cloud()
```

```csharp
public static MNN.Unity.MNNAgentPlan MNN.Unity.MNNAgentPlan.InvokeTool(string name, string input)
```

```csharp
public static MNN.Unity.MNNAgentPlan MNN.Unity.MNNAgentPlan.Local()
```

```csharp
public static MNN.Unity.MNNAgentPlan MNN.Unity.MNNAgentPlan.Parse(string text)
```

```csharp
public MNN.Unity.MNNAgentProvider.MNNAgentProvider(MNN.Unity.IMNNConversationTextProvider local, MNN.Unity.MNNAgentOptions config, System.Func<MNN.Unity.IMNNConversationTextProvider> cloud, System.Collections.Generic.IEnumerable<MNN.Unity.IMNNAgentTool> tools = null, System.Func<string, System.Threading.CancellationToken, System.Threading.Tasks.Task<bool>> router = null, string instructions = "", System.IDisposable transport = null, System.Func<string, MNN.Unity.MNNAgentPlan> policy = null, System.Func<string> planningInstructions = null)
```

```csharp
public System.Threading.Tasks.Task MNN.Unity.MNNAgentProvider.ClassifyVoiceIntentAsync(System.Collections.Generic.IReadOnlyList<MNN.Unity.MNNOpenAIMessage> prompt, System.Func<string, System.Threading.Tasks.Task> delta, System.Threading.CancellationToken token)
```

```csharp
public void MNN.Unity.MNNAgentProvider.Dispose()
```

```csharp
public System.Threading.Tasks.Task MNN.Unity.MNNAgentProvider.GenerateAssistanceAsync(MNN.Unity.MNNConversationTextTask task, System.Collections.Generic.IReadOnlyList<MNN.Unity.MNNOpenAIMessage> prompt, System.Func<string, System.Threading.Tasks.Task> delta, System.Threading.CancellationToken token)
```

```csharp
public System.Threading.Tasks.Task MNN.Unity.MNNAgentProvider.GenerateAsync(System.Collections.Generic.IReadOnlyList<MNN.Unity.MNNOpenAIMessage> history, float[] audio, int sampleRate, System.Func<string, System.Threading.Tasks.Task> delta, System.Threading.CancellationToken token)
```

```csharp
public System.Threading.Tasks.Task MNN.Unity.MNNAgentProvider.GenerateEventResponseAsync(System.Collections.Generic.IReadOnlyList<MNN.Unity.MNNOpenAIMessage> history, MNN.Unity.MNNConversationEvent input, System.Func<string, System.Threading.Tasks.Task> delta, System.Threading.CancellationToken token)
```

```csharp
public System.Threading.Tasks.Task MNN.Unity.MNNAgentProvider.GenerateResponseAsync(System.Collections.Generic.IReadOnlyList<MNN.Unity.MNNOpenAIMessage> history, string instructions, System.Func<string, System.Threading.Tasks.Task> delta, System.Threading.CancellationToken token)
```

```csharp
public System.Threading.Tasks.Task MNN.Unity.MNNAgentProvider.PrepareAsync(System.Threading.CancellationToken token)
```

```csharp
public bool MNN.Unity.MNNAgentProvider.TryReadTrace(out MNN.Unity.MNNConversationTranscriptEntry entry)
```

```csharp
public MNN.Unity.MNNAgentRoutingStrategy.MNNAgentRoutingStrategy()
```

```csharp
public MNN.Unity.MNNArithmeticTool.MNNArithmeticTool()
```

```csharp
public System.Threading.Tasks.Task<string> MNN.Unity.MNNArithmeticTool.ExecuteAsync(string input, System.Threading.CancellationToken token)
```

```csharp
public MNN.Unity.MNNBoundedHttpClient.MNNBoundedHttpClient(System.Net.Http.HttpMessageHandler handler = null)
```

```csharp
public void MNN.Unity.MNNBoundedHttpClient.Dispose()
```

```csharp
public System.Threading.Tasks.Task<string> MNN.Unity.MNNBoundedHttpClient.GetAsync(System.Uri uri, System.Threading.CancellationToken token)
```

```csharp
public System.Threading.Tasks.Task<string> MNN.Unity.MNNBoundedHttpClient.SendAsync(System.Uri uri, string body, System.Threading.CancellationToken token)
```

```csharp
public MNN.Unity.MNNChatMessage.MNNChatMessage(MNN.Unity.MNNChatRole role, string content)
```

```csharp
public MNN.Unity.MNNChatRole.MNNChatRole()
```

```csharp
public static float[] MNN.Unity.MNNCloudSpeechAudio.Decode(byte[] bytes, bool wave, int targetRate)
```

```csharp
public static byte[] MNN.Unity.MNNCloudSpeechAudio.Hex(string hex)
```

```csharp
public static byte[] MNN.Unity.MNNCloudSpeechAudio.VolcChunks(string lines)
```

```csharp
public MNN.Unity.MNNCloudSpeechOptions.MNNCloudSpeechOptions()
```

```csharp
public MNN.Unity.MNNCloudSpeechProtocol.MNNCloudSpeechProtocol()
```

```csharp
public MNN.Unity.MNNCloudSpeechProvider.MNNCloudSpeechProvider(MNN.Unity.MNNCloudSpeechOptions settings, string key = null, System.Net.Http.HttpMessageHandler handler = null)
```

```csharp
public static MNN.Unity.MNNRealtimeCapabilities MNN.Unity.MNNCloudSpeechProvider.Describe(MNN.Unity.MNNCloudSpeechOptions settings)
```

```csharp
public void MNN.Unity.MNNCloudSpeechProvider.Dispose()
```

```csharp
public System.Threading.Tasks.Task MNN.Unity.MNNCloudSpeechProvider.PrepareAsync(System.Threading.CancellationToken token)
```

```csharp
public System.Threading.Tasks.Task MNN.Unity.MNNCloudSpeechProvider.SynthesizeAsync(string text, System.Func<float[], int, System.Threading.Tasks.Task> pcm, System.Threading.CancellationToken token)
```

```csharp
public System.Threading.Tasks.Task<string> MNN.Unity.MNNCloudSpeechProvider.TranscribeAsync(float[] audio, int sampleRate, System.Threading.CancellationToken token)
```

```csharp
public MNN.Unity.MNNCloudSpeechTask.MNNCloudSpeechTask()
```

```csharp
public MNN.Unity.MNNContextRequestTool.MNNContextRequestTool(string name, string description, System.Func<string, string> execute, System.Threading.SynchronizationContext context = null)
```

```csharp
public System.Threading.Tasks.Task<string> MNN.Unity.MNNContextRequestTool.ExecuteAsync(string input, System.Threading.CancellationToken token)
```

```csharp
public MNN.Unity.MNNConversationApplication.MNNConversationApplication(MNN.Unity.MNNRealtimeDeviceBinding binding, MNN.Unity.MNNRealtimeCapabilities deviceCapabilities, MNN.Unity.MNNConversationRegistrations registrations, string transcriptDirectory, MNN.Unity.MNNConversationOptions options = null, MNN.Unity.MNNRealtimeDeviceSessionOptions deviceOptions = null)
```

```csharp
public System.Threading.Tasks.Task MNN.Unity.MNNConversationApplication.StartAsync()
```

```csharp
public System.Threading.Tasks.Task MNN.Unity.MNNConversationApplication.StopAsync()
```

Silence and stop devices synchronously, then join startup/commands/runtime and release providers. Failed owners remain attached; call again to retry. A fresh instance owns the next conversation.

```csharp
public void MNN.Unity.MNNConversationApplication.Tick()
```

Bound notification consumption and physical presentation acknowledgement to one UI frame.

```csharp
public MNN.Unity.MNNConversationCacheStatus.MNNConversationCacheStatus()
```

```csharp
public MNN.Unity.MNNConversationCatalog.MNNConversationCatalog(string directory)
```

```csharp
public MNN.Unity.MNNConversationCatalog.MNNConversationCatalog(string directory, bool deferCreation)
```

```csharp
public void MNN.Unity.MNNConversationCatalog.Archive(string id, bool archived)
```

```csharp
public void MNN.Unity.MNNConversationCatalog.BeginDraft()
```

An unsent draft has no identity, list row or persistent journal.

```csharp
public string MNN.Unity.MNNConversationCatalog.CommitDraft()
```

Promote the current draft once, on the first accepted user turn.

```csharp
public string MNN.Unity.MNNConversationCatalog.Create()
```

```csharp
public void MNN.Unity.MNNConversationCatalog.Delete(string id)
```

```csharp
public void MNN.Unity.MNNConversationCatalog.Dispose()
```

```csharp
public void MNN.Unity.MNNConversationCatalog.Flush()
```

```csharp
public bool MNN.Unity.MNNConversationCatalog.ImportExecution(string sourceId, long sequence, MNN.Unity.MNNConversationTranscriptEntry entry)
```

Imports an ordered external execution stream once, including after reconnect or catalog recovery. Flush checkpoints journal bytes before saving its cursor, so a crash cannot skip an event.

```csharp
public MNN.Unity.MNNConversationTranscriptStore MNN.Unity.MNNConversationCatalog.OpenHistory(string id)
```

```csharp
public void MNN.Unity.MNNConversationCatalog.Rename(string id, string title)
```

```csharp
public void MNN.Unity.MNNConversationCatalog.Select(string id)
```

```csharp
public void MNN.Unity.MNNConversationCatalog.SetDraft(string value)
```

```csharp
public void MNN.Unity.MNNConversationCatalog.SetViewState(MNN.Unity.MNNConversationViewState value)
```

```csharp
public void MNN.Unity.MNNConversationCatalog.Touch(string firstUserText = null)
```

```csharp
public bool MNN.Unity.MNNConversationCatalog.TrySetGeneratedTitle(string id, string title)
```

Late model results cannot overwrite an explicit name or resurrect deleted chats.

```csharp
public MNN.Unity.MNNConversationCommandBindings.MNNConversationCommandBindings(MNN.Unity.MNNConversationInputState input, MNN.Unity.MNNConversationRecording recording)
```

```csharp
public MNN.Unity.MNNConversationCommands.MNNConversationCommands(MNN.Unity.MNNConversationCommandBindings ports)
```

```csharp
public System.Threading.Tasks.Task<bool> MNN.Unity.MNNConversationCommands.BeginRecordingAsync()
```

```csharp
public void MNN.Unity.MNNConversationCommands.CancelPreparation()
```

```csharp
public void MNN.Unity.MNNConversationCommands.CancelRecording()
```

```csharp
public System.Threading.Tasks.Task<string> MNN.Unity.MNNConversationCommands.FinishRecordingAsync(bool transcribeOnly)
```

```csharp
public System.Threading.Tasks.Task MNN.Unity.MNNConversationCommands.SendTextAsync(string text)
```

```csharp
public System.Threading.Tasks.Task MNN.Unity.MNNConversationCommands.SetRealtimeAsync(bool enabled)
```

```csharp
public static bool MNN.Unity.MNNConversationContext.ContainsMedia(string text)
```

```csharp
public MNN.Unity.MNNConversationEntryKind.MNNConversationEntryKind()
```

```csharp
public MNN.Unity.MNNConversationEntryState.MNNConversationEntryState()
```

```csharp
public MNN.Unity.MNNConversationEvent.MNNConversationEvent(string id, string text)
```

```csharp
public System.Collections.Generic.IReadOnlyList<MNN.Unity.MNNOpenAIMessage> MNN.Unity.MNNConversationEvent.CreateHistory(System.Collections.Generic.IReadOnlyList<MNN.Unity.MNNOpenAIMessage> history, int maximumCharacters = 65536)
```

```csharp
public void MNN.Unity.MNNConversationEventReply.AcknowledgeTextDisplayed(MNN.Unity.MNNResponseId response, int characters)
```

Call after the application actually delivers a cumulative text snapshot to its presentation.

```csharp
public void MNN.Unity.MNNConversationEventReply.ObservePresentation(MNN.Unity.MNNRealtimeOutputPipeline output)
```

Presentation starts at the first presented frame; completion still requires every complete segment.

```csharp
public static bool MNN.Unity.MNNConversationExitIntent.IsExitVerdict(string verdict)
```

```csharp
public static System.Collections.Generic.IReadOnlyList<MNN.Unity.MNNOpenAIMessage> MNN.Unity.MNNConversationExitIntent.Prompt(string text)
```

```csharp
public MNN.Unity.MNNConversationInactivity.MNNConversationInactivity()
```

```csharp
public bool MNN.Unity.MNNConversationInactivity.Tick(double now, int revision, bool realtime, bool activity, int timeoutSeconds)
```

```csharp
public MNN.Unity.MNNConversationInfo.MNNConversationInfo()
```

```csharp
public MNN.Unity.MNNConversationInputState.MNNConversationInputState(bool realtime = false)
```

```csharp
public void MNN.Unity.MNNConversationInputState.BeginRecording()
```

```csharp
public bool MNN.Unity.MNNConversationInputState.Consume(float[] frame, bool speechActive = false, long enqueuedMonotonicTicks = 0, bool discontinuity = false)
```

Consumes manual/idle frames; only live conversation frames reach turn detection.

```csharp
public float[] MNN.Unity.MNNConversationInputState.FinishRecording(bool cancel)
```

```csharp
public void MNN.Unity.MNNConversationInputState.SetRealtime(bool enabled)
```

```csharp
public void MNN.Unity.MNNConversationInputState.SetRealtime(bool enabled, System.Action initialize)
```

```csharp
public bool MNN.Unity.MNNConversationInputState.TryAcceptRealtime(int revision, System.Action accept)
```

```csharp
public bool MNN.Unity.MNNConversationInputState.TryReadWaveform(float[] points)
```

```csharp
public MNN.Unity.MNNConversationLedger.MNNConversationLedger(int segmentCapacity = 256)
```

```csharp
public void MNN.Unity.MNNConversationLedger.AcknowledgePresentation(long throughFrame, bool estimated)
```

Device output frame clock, exclusive. Never use generated length or Unity frame time.

```csharp
public MNN.Unity.MNNConversationSegment MNN.Unity.MNNConversationLedger.GetSegment(int index)
```

```csharp
public void MNN.Unity.MNNConversationLedger.Interrupt(MNN.Unity.MNNResponseId response)
```

```csharp
public void MNN.Unity.MNNConversationLedger.RecordSubmission(MNN.Unity.MNNPlaybackReceipt receipt)
```

Receipts must be drained in render order, including after interruption.

```csharp
public bool MNN.Unity.MNNConversationLedger.TryRegister(MNN.Unity.MNNResponseId response, long segmentId, string spokenText)
```

Control thread. Capacity exhaustion requires explicit context rollover, never silent eviction.

```csharp
public MNN.Unity.MNNConversationLocalModelOptions.MNNConversationLocalModelOptions()
```

```csharp
public static System.Collections.Generic.List<MNN.Unity.MNNChatMessage> MNN.Unity.MNNConversationLocalProviders.ConversationMessages(System.Collections.Generic.IReadOnlyList<MNN.Unity.MNNOpenAIMessage> history, System.Func<float[], int, string> saveAudio, out string lastAudio)
```

```csharp
public static MNN.Unity.IMNNConversationProvider MNN.Unity.MNNConversationLocalProviders.Create(MNN.Unity.MNNConversationModelAdapter kind, string directory, MNN.Unity.MNNConversationLocalModelOptions options = null)
```

```csharp
public static MNN.Unity.MNNRealtimeCapabilities MNN.Unity.MNNConversationLocalProviders.DescribeCapabilities(MNN.Unity.MNNConversationModelAdapter kind)
```

```csharp
public static bool MNN.Unity.MNNConversationLocalProviders.HasPronounceableText(string text)
```

```csharp
public static string MNN.Unity.MNNConversationLocalProviders.PrepareKokoroText(string text)
```

```csharp
public MNN.Unity.MNNConversationMailbox.MNNConversationMailbox(System.Func<MNN.Unity.MNNResponseId, bool> accepts, int transcriptCapacity = 64, int stageCapacity = 32)
```

```csharp
public void MNN.Unity.MNNConversationMailbox.Clear()
```

```csharp
public System.Threading.Tasks.Task<bool> MNN.Unity.MNNConversationMailbox.PublishAssistantSnapshotAsync(MNN.Unity.MNNResponseId response, System.Text.StringBuilder text, System.Threading.CancellationToken token)
```

```csharp
public void MNN.Unity.MNNConversationMailbox.PublishInputWarning(string warning)
```

```csharp
public void MNN.Unity.MNNConversationMailbox.PublishRecognitionPreview(int revision, string text)
```

```csharp
public string MNN.Unity.MNNConversationMailbox.TakeInputWarning()
```

```csharp
public bool MNN.Unity.MNNConversationMailbox.TryPublishAssistantSnapshot(MNN.Unity.MNNResponseId response, System.Text.StringBuilder text)
```

```csharp
public bool MNN.Unity.MNNConversationMailbox.TryPublishAssistantText(MNN.Unity.MNNResponseId response, string text)
```

```csharp
public bool MNN.Unity.MNNConversationMailbox.TryPublishStage(MNN.Unity.MNNResponseId response, MNN.Unity.MNNConversationStage stage)
```

```csharp
public bool MNN.Unity.MNNConversationMailbox.TryPublishUserText(string text)
```

```csharp
public bool MNN.Unity.MNNConversationMailbox.TryRead(out MNN.Unity.MNNConversationNotification notification)
```

```csharp
public bool MNN.Unity.MNNConversationMailbox.TryReadQueuedStage(out MNN.Unity.MNNConversationNotification notification)
```

Dequeues one queued stage with its response identity. The caller must check the response before presenting it; use TryReadStage to discard stale stages automatically.

```csharp
public bool MNN.Unity.MNNConversationMailbox.TryReadRecognitionPreview(int revision, bool realtime, out string text)
```

```csharp
public bool MNN.Unity.MNNConversationMailbox.TryReadStage(out MNN.Unity.MNNConversationNotification notification)
```

```csharp
public bool MNN.Unity.MNNConversationMailbox.TryReadTranscript(out MNN.Unity.MNNConversationNotification notification)
```

```csharp
public MNN.Unity.MNNConversationMemory.MNNConversationMemory(System.Collections.Generic.List<MNN.Unity.MNNOpenAIMessage> messages, int maximumMessages)
```

```csharp
public void MNN.Unity.MNNConversationMemory.AddAssistant(string text)
```

```csharp
public void MNN.Unity.MNNConversationMemory.AddEvent(MNN.Unity.MNNConversationEvent input)
```

```csharp
public void MNN.Unity.MNNConversationMemory.AddUser(MNN.Unity.MNNOpenAIMessage message, string text)
```

```csharp
public void MNN.Unity.MNNConversationMemory.CommitPresented(MNN.Unity.MNNRealtimeOutputPipeline output, MNN.Unity.MNNResponseId response)
```

```csharp
public void MNN.Unity.MNNConversationMemory.RememberAssistant(string text)
```

```csharp
public void MNN.Unity.MNNConversationMemory.Reset()
```

```csharp
public void MNN.Unity.MNNConversationMemory.Restore(MNN.Unity.IMNNConversationTranscriptSource source, int maximumCharacters = 65536)
```

Prepare a bounded text-only context before replacing live memory. Display-only execution evidence never becomes model instructions or consumes the message-count window.

```csharp
public static string MNN.Unity.MNNConversationMemory.Tail(string text)
```

```csharp
public static MNN.Unity.IMNNConversationTextProvider MNN.Unity.MNNConversationMnnProviders.Language(string directory, int threads = 4, int maximumTokens = 256, bool thinking = false, string cacheDirectory = null)
```

```csharp
public static MNN.Unity.IMNNConversationAsrProvider MNN.Unity.MNNConversationMnnProviders.Recognition(string directory, int threads = 1, string language = null)
```

```csharp
public static MNN.Unity.IMNNConversationTtsProvider MNN.Unity.MNNConversationMnnProviders.Speech(string directory, MNN.Unity.MNNSpeechSynthesisOptions options = null, int threads = 4)
```

```csharp
public MNN.Unity.MNNConversationNotification.MNNConversationNotification()
```

```csharp
public MNN.Unity.MNNConversationOptions.MNNConversationOptions()
```

```csharp
public MNN.Unity.MNNConversationOptions MNN.Unity.MNNConversationOptions.Snapshot()
```

```csharp
public MNN.Unity.MNNConversationPorts.MNNConversationPorts(MNN.Unity.IMNNAudioCaptureSource capture, System.Func<MNN.Unity.MNNRealtimeSession> session, System.Func<bool> speakerAudible = null)
```

```csharp
public bool MNN.Unity.MNNConversationPorts.Consume(float[] frame, bool speechActive = false, long enqueuedMonotonicTicks = 0, bool discontinuity = false)
```

```csharp
public MNN.Unity.MNNConversationCommandBindings MNN.Unity.MNNConversationPorts.CreateCommandBindings()
```

Creates command bindings with shared capture ownership. Supply provider submission and entrance delegates.

```csharp
public MNN.Unity.IMNNRealtimeVoiceInputProcessor MNN.Unity.MNNConversationPorts.CreateVoiceInputProcessor(MNN.Unity.MNNPcmFormat format, int maximumBlockFrames, MNN.Unity.IMNNRealtimeVoiceActivity activity)
```

```csharp
public System.Threading.Tasks.Task MNN.Unity.MNNConversationPorts.InterruptAsync()
```

```csharp
public System.Threading.Tasks.Task<float[]> MNN.Unity.MNNConversationPorts.PrepareVoiceAsync(float[] samples, System.Threading.CancellationToken token)
```

```csharp
public System.Threading.Tasks.Task<bool> MNN.Unity.MNNConversationPorts.PublishAssistantSnapshotAsync(MNN.Unity.MNNResponseId response, System.Text.StringBuilder text, System.Threading.CancellationToken token)
```

```csharp
public void MNN.Unity.MNNConversationPorts.PublishInputWarning(string message)
```

```csharp
public void MNN.Unity.MNNConversationPorts.PublishRecognitionPreview(int revision, string text)
```

```csharp
public bool MNN.Unity.MNNConversationPorts.RequestRealtimeExit(int revision)
```

```csharp
public void MNN.Unity.MNNConversationPorts.ResetIdentity()
```

```csharp
public void MNN.Unity.MNNConversationPorts.SetCaptureEnabled(bool enabled)
```

```csharp
public void MNN.Unity.MNNConversationPorts.SetRealtime(bool enabled)
```

```csharp
public string MNN.Unity.MNNConversationPorts.TakeInputWarning()
```

```csharp
public bool MNN.Unity.MNNConversationPorts.TakeRealtimeExit()
```

```csharp
public string MNN.Unity.MNNConversationPorts.TakeRecognitionPreview()
```

```csharp
public MNN.Unity.MNNConversationWakeHandoff MNN.Unity.MNNConversationPorts.TakeWakeCommand()
```

```csharp
public bool MNN.Unity.MNNConversationPorts.TryAcceptRealtime(int revision, System.Action accept)
```

```csharp
public bool MNN.Unity.MNNConversationPorts.TryPublishAssistantSnapshot(MNN.Unity.MNNResponseId response, System.Text.StringBuilder text)
```

```csharp
public bool MNN.Unity.MNNConversationPorts.TryPublishStage(MNN.Unity.MNNResponseId response, MNN.Unity.MNNConversationStage stage)
```

```csharp
public bool MNN.Unity.MNNConversationPorts.TryPublishUserText(string text)
```

```csharp
public bool MNN.Unity.MNNConversationPorts.TryRead(out MNN.Unity.MNNConversationNotification notification)
```

```csharp
public void MNN.Unity.MNNConversationProviderCleanup.Dispose()
```

Retries only failed providers. May be called again after a failure or successful completion.

```csharp
public MNN.Unity.MNNConversationProviderMetadata.MNNConversationProviderMetadata()
```

```csharp
public MNN.Unity.MNNConversationProviderRuntime.MNNConversationProviderRuntime(MNN.Unity.IMNNConversationProvider provider, MNN.Unity.MNNConversationStageRegistration registration, MNN.Unity.IMNNRealtimeSessionRuntime inner, MNN.Unity.MNNConversationRuntimeDiagnostics diagnostics = null)
```

```csharp
public System.Threading.Tasks.Task MNN.Unity.MNNConversationProviderRuntime.DisposeAsync()
```

```csharp
public System.Threading.Tasks.Task MNN.Unity.MNNConversationProviderRuntime.PrepareAsync(System.Threading.CancellationToken token)
```

```csharp
public System.Threading.Tasks.Task MNN.Unity.MNNConversationProviderRuntime.RequestCancellationAsync(MNN.Unity.MNNResponseId response)
```

```csharp
public System.Threading.Tasks.Task MNN.Unity.MNNConversationProviderRuntime.RestoreContextAsync(MNN.Unity.MNNResponseId response)
```

```csharp
public System.Threading.Tasks.Task MNN.Unity.MNNConversationProviderRuntime.StartListeningAsync(System.Threading.CancellationToken token)
```

```csharp
public System.Threading.Tasks.Task MNN.Unity.MNNConversationProviderRuntime.StopListeningAsync()
```

```csharp
public System.Threading.Tasks.Task MNN.Unity.MNNConversationProviderRuntime.WaitForInferenceQuiescenceAsync(MNN.Unity.MNNResponseId response)
```

```csharp
public System.Threading.Tasks.Task MNN.Unity.MNNConversationProviderRuntime.WaitForQuiescenceAsync()
```

```csharp
public MNN.Unity.MNNConversationProviders.MNNConversationProviders()
```

```csharp
public void MNN.Unity.MNNConversationProviders.Dispose(MNN.Unity.IMNNConversationProvider provider)
```

```csharp
public MNN.Unity.MNNConversationProviders MNN.Unity.MNNConversationProviders.Snapshot()
```

```csharp
public MNN.Unity.MNNConversationRecognitionWorker.MNNConversationRecognitionWorker(MNN.Unity.IMNNConversationRecognitionStream stream, int preRollFrames = 8, int maximumAgeMilliseconds = 250)
```

```csharp
public bool MNN.Unity.MNNConversationRecognitionWorker.Append(float[] frame, bool speech)
```

```csharp
public void MNN.Unity.MNNConversationRecognitionWorker.Dispose()
```

```csharp
public void MNN.Unity.MNNConversationRecognitionWorker.Reset()
```

```csharp
public System.Threading.Tasks.Task MNN.Unity.MNNConversationRecognitionWorker.StopAsync()
```

```csharp
public bool MNN.Unity.MNNConversationRecognitionWorker.TryRead(out string text)
```

```csharp
public MNN.Unity.MNNConversationRecording.MNNConversationRecording(MNN.Unity.MNNConversationInputState input)
```

```csharp
public void MNN.Unity.MNNConversationRecording.Begin()
```

```csharp
public System.Threading.Tasks.Task MNN.Unity.MNNConversationRecording.DrainAsync(System.Threading.CancellationToken token)
```

```csharp
public void MNN.Unity.MNNConversationRecording.EnsureDrained()
```

```csharp
public System.Threading.Tasks.Task MNN.Unity.MNNConversationRecording.PrepareAsync(System.Threading.CancellationToken token)
```

Drains previous-mode audio before Begin. Loss outside the new recording does not invalidate it.

```csharp
public MNN.Unity.MNNConversationRegistrations.MNNConversationRegistrations()
```

```csharp
public MNN.Unity.MNNConversationRowLayout.MNNConversationRowLayout()
```

```csharp
public MNN.Unity.MNNConversationRowLayout.MNNConversationRowLayout(float availableWidth, MNN.Unity.MNNChatRole role, MNN.Unity.MNNConversationEntryKind kind, bool first)
```

```csharp
public MNN.Unity.MNNConversationRuntime.MNNConversationRuntime(MNN.Unity.IMNNConversationContext context, MNN.Unity.MNNConversationOptions config, MNN.Unity.MNNRealtimeOutputPipeline output, MNN.Unity.MNNRealtimeRoute route, MNN.Unity.MNNConversationProviders providers, System.Collections.Generic.List<MNN.Unity.MNNOpenAIMessage> history = null)
```

```csharp
protected System.Threading.Tasks.Task<bool> MNN.Unity.MNNConversationRuntime.AcceptUserInputAsync(string text, float[] audio, bool voiceValidated, System.Threading.CancellationToken token, MNN.Unity.MNNConversationTelemetry.Trace trace, bool audioPrepared, int exitRevision)
```

```csharp
protected static void MNN.Unity.MNNConversationRuntime.AppendContext(System.Text.StringBuilder context, string label, string text)
```

```csharp
public System.Threading.Tasks.Task<string> MNN.Unity.MNNConversationRuntime.AssistTextAsync(MNN.Unity.MNNConversationTextTask task, string input, System.Action<string> preview, System.Threading.CancellationToken token)
```

Preview receives complete words on a worker thread. The returned task still owns generation and must be observed. Callers must marshal previews and discard canceled results.

```csharp
public System.Threading.Tasks.Task<string> MNN.Unity.MNNConversationRuntime.AssistTextAsync(MNN.Unity.MNNConversationTextTask task, string input, System.Threading.CancellationToken token)
```

Low-priority generation on the existing model gate. No chat memory, tools or speech events.

```csharp
protected void MNN.Unity.MNNConversationRuntime.BeginTurnCandidate()
```

```csharp
protected System.Collections.Generic.List<MNN.Unity.MNNOpenAIMessage> MNN.Unity.MNNConversationRuntime.BuildResponseHistory()
```

```csharp
public static void MNN.Unity.MNNConversationRuntime.CancelProductionAfterSpeechFailure(System.Threading.CancellationTokenSource production, System.Exception speechError)
```

```csharp
protected static System.Exception MNN.Unity.MNNConversationRuntime.CombineFailures(string message, System.Exception first, System.Exception second)
```

```csharp
protected void MNN.Unity.MNNConversationRuntime.CommitPresentedHistory()
```

```csharp
public System.Threading.Tasks.Task MNN.Unity.MNNConversationRuntime.DisposeAsync()
```

```csharp
protected void MNN.Unity.MNNConversationRuntime.DisposeProvider(ref MNN.Unity.IMNNConversationProvider provider, ref System.Collections.Generic.List<System.Exception> failures)
```

```csharp
protected System.Threading.Tasks.Task MNN.Unity.MNNConversationRuntime.DrainRecordingInputAsync()
```

```csharp
public static System.Threading.Tasks.Task MNN.Unity.MNNConversationRuntime.DrainUnspokenTextAsync(MNN.Unity.MNNRealtimeSpeechTextChannel channel, System.Threading.CancellationToken token)
```

```csharp
protected System.Threading.Tasks.Task<MNN.Unity.MNNConversationRuntime.VoiceCandidate> MNN.Unity.MNNConversationRuntime.EvaluateVoiceCandidateAsync(float[] audio, bool full, string echo, System.Threading.CancellationToken token)
```

```csharp
protected void MNN.Unity.MNNConversationRuntime.FinishTurnCandidate()
```

```csharp
protected System.Threading.Tasks.Task MNN.Unity.MNNConversationRuntime.GenerateTextAsync(System.Collections.Generic.IReadOnlyList<MNN.Unity.MNNOpenAIMessage> history, System.Func<string, System.Threading.Tasks.Task> delta, System.Threading.CancellationToken token)
```

```csharp
protected static bool MNN.Unity.MNNConversationRuntime.HasAcousticEvidence(float[] audio)
```

```csharp
protected System.Threading.Tasks.Task<bool> MNN.Unity.MNNConversationRuntime.HasMeaningAsync(string text, string echo, System.Threading.CancellationToken token)
```

```csharp
protected void MNN.Unity.MNNConversationRuntime.InputLoop()
```

```csharp
protected System.Threading.Tasks.Task MNN.Unity.MNNConversationRuntime.ObserveCaptureAsync()
```

```csharp
protected void MNN.Unity.MNNConversationRuntime.ObservePresentedResponse()
```

```csharp
protected void MNN.Unity.MNNConversationRuntime.OnVad(MNN.Unity.MNNRealtimeVadUpdate update)
```

```csharp
public System.Threading.Tasks.Task MNN.Unity.MNNConversationRuntime.PrepareAsync(System.Threading.CancellationToken token)
```

```csharp
protected System.Threading.Tasks.Task MNN.Unity.MNNConversationRuntime.ProduceModelSpeechAsync(System.Collections.Generic.IReadOnlyList<MNN.Unity.MNNOpenAIMessage> history, System.Threading.CancellationToken token)
```

```csharp
protected System.Threading.Tasks.Task MNN.Unity.MNNConversationRuntime.ProduceTextReplyAsync(System.Collections.Generic.IReadOnlyList<MNN.Unity.MNNOpenAIMessage> history, System.Threading.CancellationToken token, MNN.Unity.MNNConversationTelemetry.Trace trace)
```

```csharp
protected void MNN.Unity.MNNConversationRuntime.RejectFullUtterance()
```

```csharp
protected MNN.Unity.MNNConversationRuntime.VoiceCandidate MNN.Unity.MNNConversationRuntime.RejectVoice(string reason)
```

```csharp
protected void MNN.Unity.MNNConversationRuntime.ReportFault(System.Exception error)
```

```csharp
public System.Threading.Tasks.Task MNN.Unity.MNNConversationRuntime.RequestCancellationAsync(MNN.Unity.MNNResponseId response)
```

```csharp
protected void MNN.Unity.MNNConversationRuntime.RequestRouteFallback(MNN.Unity.MNNRealtimeRouteUnavailableException error)
```

```csharp
public System.Threading.Tasks.Task MNN.Unity.MNNConversationRuntime.ResetConversationAsync()
```

```csharp
protected void MNN.Unity.MNNConversationRuntime.ResetUtterance()
```

```csharp
protected System.Threading.Tasks.Task MNN.Unity.MNNConversationRuntime.RespondAsync(string text, float[] audio, System.Threading.CancellationToken requestToken, MNN.Unity.MNNConversationTelemetry.Trace trace, int realtimeRevision, bool voiceValidated, bool audioPrepared, int exitRevision)
```

```csharp
public System.Threading.Tasks.Task MNN.Unity.MNNConversationRuntime.RestoreContextAsync(MNN.Unity.MNNResponseId response)
```

```csharp
protected bool MNN.Unity.MNNConversationRuntime.ShouldEvaluateTurn()
```

```csharp
protected System.Threading.Tasks.Task<bool> MNN.Unity.MNNConversationRuntime.ShouldExitRealtimeAsync(string text, int revision, System.Threading.CancellationToken token)
```

```csharp
protected string MNN.Unity.MNNConversationRuntime.SnapshotPlaybackText()
```

```csharp
public System.Threading.Tasks.Task MNN.Unity.MNNConversationRuntime.StartListeningAsync(System.Threading.CancellationToken token)
```

```csharp
protected System.Threading.Tasks.Task MNN.Unity.MNNConversationRuntime.StartResponse(string text, float[] audio, System.Threading.CancellationToken token, int realtimeRevision = -1, bool voiceValidated = false, bool audioPrepared = false)
```

```csharp
public System.Threading.Tasks.Task MNN.Unity.MNNConversationRuntime.StopListeningAsync()
```

```csharp
public System.Threading.Tasks.Task MNN.Unity.MNNConversationRuntime.SubmitAudioAsync(float[] audio, System.Threading.CancellationToken token)
```

```csharp
public System.Threading.Tasks.Task MNN.Unity.MNNConversationRuntime.SubmitTextAsync(string text, System.Threading.CancellationToken token)
```

```csharp
protected bool MNN.Unity.MNNConversationRuntime.SuppressSpeakerEcho()
```

```csharp
public System.Threading.Tasks.Task<string> MNN.Unity.MNNConversationRuntime.TranscribeAsync(float[] audio, System.Threading.CancellationToken token)
```

```csharp
protected System.Threading.Tasks.Task<string> MNN.Unity.MNNConversationRuntime.TranscribePreparedAsync(float[] audio, System.Threading.CancellationToken token)
```

```csharp
public MNN.Unity.MNNConversationEventReply MNN.Unity.MNNConversationRuntime.TryBeginEventReply(MNN.Unity.MNNConversationEvent input, System.Threading.CancellationToken token)
```

Returns null when the foreground owns the slot. The caller retains and retries the event.

```csharp
public static bool MNN.Unity.MNNConversationRuntime.TryGetRouteFailure(System.Exception error, MNN.Unity.MNNRealtimeRoute route, out MNN.Unity.MNNRealtimeRouteUnavailableException routeFailure)
```

```csharp
protected void MNN.Unity.MNNConversationRuntime.TryLaunchAccepted()
```

```csharp
public bool MNN.Unity.MNNConversationRuntime.TryReadAgentTrace(out MNN.Unity.MNNConversationTranscriptEntry entry)
```

```csharp
protected System.Threading.Tasks.Task<MNN.Unity.MNNConversationRuntime.VoiceCandidate> MNN.Unity.MNNConversationRuntime.ValidateVoiceAsync(float[] audio, string echo, System.Threading.CancellationToken token, string transcript = null)
```

```csharp
protected System.Threading.Tasks.Task<MNN.Unity.MNNConversationRuntime.VoiceCandidate> MNN.Unity.MNNConversationRuntime.ValidateVoiceCoreAsync(float[] audio, string echo, System.Threading.CancellationToken token, string preparedTranscript = null)
```

```csharp
public MNN.Unity.MNNConversationRuntime.VoiceCandidate.VoiceCandidate()
```

```csharp
public MNN.Unity.MNNConversationRuntime.VoiceCandidate.VoiceCandidate(bool complete, bool accepted, string text = null, float[] audio = null)
```

```csharp
public System.Threading.Tasks.Task MNN.Unity.MNNConversationRuntime.WaitForInferenceQuiescenceAsync(MNN.Unity.MNNResponseId response)
```

```csharp
public System.Threading.Tasks.Task MNN.Unity.MNNConversationRuntime.WaitForQuiescenceAsync()
```

```csharp
public MNN.Unity.MNNConversationRuntimeDiagnostics.MNNConversationRuntimeDiagnostics(System.Func<string, MNN.Unity.MNNConversationProviderMetadata> metadata = null)
```

```csharp
public MNN.Unity.IMNNConversationProvider MNN.Unity.MNNConversationRuntimeDiagnostics.Acquire(string stage, MNN.Unity.MNNConversationStageRegistration registration)
```

```csharp
public void MNN.Unity.MNNConversationRuntimeDiagnostics.Dispose(MNN.Unity.IMNNConversationProvider provider)
```

```csharp
public MNN.Unity.MNNConversationRuntimeDiagnostics.Entry.Entry()
```

```csharp
public System.Threading.Tasks.Task MNN.Unity.MNNConversationRuntimeDiagnostics.PrepareAsync(MNN.Unity.MNNConversationStageRegistration registration, MNN.Unity.IMNNConversationProvider provider, System.Threading.CancellationToken token)
```

```csharp
public MNN.Unity.MNNConversationRuntimeDiagnostics.Entry[] MNN.Unity.MNNConversationRuntimeDiagnostics.Read()
```

```csharp
public MNN.Unity.MNNConversationRuntimeFactory.MNNConversationRuntimeFactory(MNN.Unity.IMNNConversationContext context, MNN.Unity.MNNConversationOptions options, MNN.Unity.MNNConversationRegistrations stages, MNN.Unity.MNNConversationRuntimeDiagnostics diagnostics = null, System.Func<MNN.Unity.IMNNConversationProvider, MNN.Unity.IMNNConversationContext, MNN.Unity.MNNRealtimeOutputPipeline, MNN.Unity.IMNNRealtimeSessionRuntime> createRealtime = null)
```

```csharp
public System.Threading.Tasks.Task<MNN.Unity.IMNNRealtimeSessionRuntime> MNN.Unity.MNNConversationRuntimeFactory.CreateAsync(MNN.Unity.MNNRealtimeReport report, MNN.Unity.MNNRealtimeOutputPipeline output, System.Threading.CancellationToken token)
```

```csharp
public MNN.Unity.MNNRealtimeFallbackRuntime MNN.Unity.MNNConversationRuntimeFactory.CreateFallback(MNN.Unity.MNNRealtimeOutputPipeline output, MNN.Unity.MNNRealtimeCapabilities device, System.Collections.Generic.IReadOnlyCollection<MNN.Unity.MNNRealtimeRoute> excludedRoutes = null, bool requireMeasuredSpeech = true)
```

```csharp
public static void MNN.Unity.MNNConversationRuntimeFactory.DisposeUntransferredProviders(System.Collections.Generic.IEnumerable<MNN.Unity.IMNNConversationProvider> providers, System.Exception creationError, MNN.Unity.MNNConversationRuntimeDiagnostics diagnostics = null)
```

Attempts every distinct provider. A thrown AggregateException retains the retryable cleanup group, accessible through GetFailedCreationCleanup. Factory-created groups are also retained until ReleaseAsync.

```csharp
public static MNN.Unity.MNNConversationProviderCleanup MNN.Unity.MNNConversationRuntimeFactory.GetFailedCreationCleanup(System.AggregateException error)
```

```csharp
public System.Threading.Tasks.Task MNN.Unity.MNNConversationRuntimeFactory.ReleaseAsync()
```

```csharp
public System.Threading.Tasks.Task MNN.Unity.MNNConversationRuntimeFactory.ResetConversationAsync()
```

```csharp
public System.Threading.Tasks.Task MNN.Unity.MNNConversationRuntimeFactory.RestoreConversationAsync(MNN.Unity.IMNNConversationTranscriptSource source)
```

After joining the previous response, restore bounded model memory from display history.

```csharp
public System.Threading.Tasks.Task MNN.Unity.MNNConversationRuntimeFactory.SubmitAudioAsync(float[] audio, System.Threading.CancellationToken token)
```

```csharp
public System.Threading.Tasks.Task MNN.Unity.MNNConversationRuntimeFactory.SubmitTextAsync(string text, System.Threading.CancellationToken token)
```

```csharp
public System.Threading.Tasks.Task<string> MNN.Unity.MNNConversationRuntimeFactory.TranscribeAsync(float[] audio, System.Threading.CancellationToken token)
```

```csharp
public MNN.Unity.MNNConversationSegment.MNNConversationSegment()
```

```csharp
public static string MNN.Unity.MNNConversationSpeechInstructions.Append(string userInstructions, System.Collections.Generic.IReadOnlyList<string> languages)
```

```csharp
public MNN.Unity.MNNConversationSpeechTextBuffer.MNNConversationSpeechTextBuffer(int maximumCharacters)
```

```csharp
public void MNN.Unity.MNNConversationSpeechTextBuffer.Append(string delta)
```

```csharp
public void MNN.Unity.MNNConversationSpeechTextBuffer.Complete()
```

```csharp
public void MNN.Unity.MNNConversationSpeechTextBuffer.Dispose()
```

```csharp
public System.Threading.Tasks.Task MNN.Unity.MNNConversationSpeechTextBuffer.DrainAsync(System.Func<string, System.Threading.CancellationToken, System.Threading.Tasks.Task> consume, System.Threading.CancellationToken token)
```

```csharp
public MNN.Unity.MNNConversationSpeechTimings.MNNConversationSpeechTimings()
```

```csharp
public MNN.Unity.MNNConversationSpeechTimings.MNNConversationSpeechTimings(double tokens, double flow, double vocoder)
```

```csharp
public MNN.Unity.MNNConversationStage.MNNConversationStage()
```

```csharp
public MNN.Unity.MNNConversationStagePlan.MNNConversationStagePlan()
```

```csharp
public static MNN.Unity.MNNConversationStagePlan MNN.Unity.MNNConversationStagePlan.ForRoute(MNN.Unity.MNNConversationOptions config, MNN.Unity.MNNRealtimeRoute route, bool allowBufferedSpeech = false, bool providerVad = false, bool providerTurn = false, bool turnRequiresTranscript = false)
```

```csharp
public static MNN.Unity.MNNConversationStagePlan MNN.Unity.MNNConversationStagePlan.ForRoutes(MNN.Unity.MNNConversationOptions config, System.Collections.Generic.ICollection<MNN.Unity.MNNRealtimeRoute> excluded, bool allowBufferedSpeech = false, bool providerVad = false, bool providerTurn = false, bool turnRequiresTranscript = false)
```

```csharp
public MNN.Unity.MNNConversationStageRegistration.MNNConversationStageRegistration(MNN.Unity.MNNRealtimeCapabilities capabilities, System.Func<MNN.Unity.IMNNConversationProvider> create, System.Func<System.Threading.CancellationToken, System.Threading.Tasks.Task> install = null)
```

```csharp
public System.Threading.Tasks.Task MNN.Unity.MNNConversationStageRegistration.PrepareAsync(MNN.Unity.IMNNConversationProvider provider, System.Threading.CancellationToken token)
```

```csharp
public MNN.Unity.MNNConversationTelemetry.MNNConversationTelemetry()
```

```csharp
public void MNN.Unity.MNNConversationTelemetry.Complete(MNN.Unity.MNNConversationTelemetry.Trace trace, long timestamp = 0)
```

```csharp
public MNN.Unity.MNNConversationTelemetry.MNNConversationTiming.MNNConversationTiming()
```

```csharp
public MNN.Unity.MNNConversationTelemetry.MNNConversationTiming.MNNConversationTiming(MNN.Unity.MNNConversationTelemetry.Trace trace, long completedAt)
```

```csharp
public MNN.Unity.MNNConversationTelemetry.Trace.Trace()
```

```csharp
public static System.Collections.Generic.IReadOnlyList<MNN.Unity.MNNOpenAIMessage> MNN.Unity.MNNConversationTextAssistance.CreatePrompt(MNN.Unity.MNNConversationTextTask task, string input)
```

```csharp
public static int MNN.Unity.MNNConversationTextAssistance.MaximumTokens(MNN.Unity.MNNConversationTextTask task)
```

```csharp
public static string MNN.Unity.MNNConversationTextAssistance.Normalize(MNN.Unity.MNNConversationTextTask task, string prefix, string generated)
```

```csharp
public static string MNN.Unity.MNNConversationTextAssistance.RemainingCompletion(string prefix, string suffix, string input)
```

Consume only text the user actually typed; mismatches invalidate the suggestion.

```csharp
public MNN.Unity.MNNConversationTextTask.MNNConversationTextTask()
```

```csharp
public MNN.Unity.MNNConversationTimelinePresentation.MNNConversationTimelinePresentation()
```

```csharp
public void MNN.Unity.MNNConversationTimelinePresentation.Bind(MNN.Unity.IMNNConversationTranscriptSource source)
```

```csharp
public int[] MNN.Unity.MNNConversationTimelinePresentation.CaptureExpanded()
```

```csharp
public void MNN.Unity.MNNConversationTimelinePresentation.Clear()
```

```csharp
public bool MNN.Unity.MNNConversationTimelinePresentation.IsExpanded(int index)
```

```csharp
public void MNN.Unity.MNNConversationTimelinePresentation.RestoreExpanded(System.Collections.Generic.IReadOnlyList<int> entries)
```

```csharp
public bool MNN.Unity.MNNConversationTimelinePresentation.Toggle(int index)
```

```csharp
public MNN.Unity.MNNConversationTranscriptEntry.MNNConversationTranscriptEntry()
```

```csharp
public MNN.Unity.MNNConversationTranscriptEntry.MNNConversationTranscriptEntry(MNN.Unity.MNNChatRole role, string text)
```

```csharp
public MNN.Unity.MNNConversationTranscriptEntry.MNNConversationTranscriptEntry(MNN.Unity.MNNChatRole role, string text, MNN.Unity.MNNConversationEntryKind kind, string id, string parentId, string title, MNN.Unity.MNNConversationEntryState state)
```

```csharp
public MNN.Unity.MNNConversationTranscriptStore.MNNConversationTranscriptStore(string directory, long maximumBytes = 268435456)
```

```csharp
public void MNN.Unity.MNNConversationTranscriptStore.Add(MNN.Unity.MNNChatRole role, string text)
```

```csharp
public void MNN.Unity.MNNConversationTranscriptStore.Add(MNN.Unity.MNNConversationTranscriptEntry entry)
```

```csharp
public void MNN.Unity.MNNConversationTranscriptStore.Clear()
```

```csharp
public void MNN.Unity.MNNConversationTranscriptStore.Dispose()
```

```csharp
public void MNN.Unity.MNNConversationTranscriptStore.Flush()
```

Flushes journal bytes to the backing device. Session tails remain in memory.

```csharp
public static MNN.Unity.MNNConversationTranscriptStore MNN.Unity.MNNConversationTranscriptStore.OpenPersistent(string filePath, long maximumBytes = 268435456)
```

Opens one journal writer. A corrupt header is rejected; a corrupt suffix is truncated.

```csharp
public void MNN.Unity.MNNConversationTranscriptStore.UpdateLast(string text)
```

```csharp
public MNN.Unity.MNNConversationTranscriptionService.MNNConversationTranscriptionService(MNN.Unity.MNNConversationStageRegistration registration, MNN.Unity.MNNConversationRuntimeDiagnostics diagnostics = null)
```

```csharp
public System.Threading.Tasks.Task MNN.Unity.MNNConversationTranscriptionService.ReleaseAsync()
```

```csharp
public System.Threading.Tasks.Task<string> MNN.Unity.MNNConversationTranscriptionService.TranscribeAsync(float[] audio, System.Threading.CancellationToken token, int sampleRate = 16000)
```

```csharp
public MNN.Unity.MNNConversationViewState.MNNConversationViewState(int pageStart, int firstEntry, int characterOffset, float scrollOffset, bool followingLatest, System.Collections.Generic.IReadOnlyList<int> expandedEntries)
```

```csharp
public static bool MNN.Unity.MNNConversationVoiceInputFilter.AcceptsVerdict(string verdict)
```

```csharp
public static bool MNN.Unity.MNNConversationVoiceInputFilter.IsCommand(string text)
```

```csharp
public static bool MNN.Unity.MNNConversationVoiceInputFilter.IsLikelyEcho(string text, string spoken)
```

```csharp
public static System.Collections.Generic.IReadOnlyList<MNN.Unity.MNNOpenAIMessage> MNN.Unity.MNNConversationVoiceInputFilter.SemanticPrompt(string text, string context)
```

```csharp
public static bool MNN.Unity.MNNConversationVoiceInputFilter.TryPrepare(string transcript, out string text, out string reason)
```

```csharp
public MNN.Unity.MNNConversationWakeHandoff.MNNConversationWakeHandoff(float[] frames, long endSample, long captureRevision, bool lostPrefix)
```

```csharp
public MNN.Unity.MNNDecisionKind.MNNDecisionKind()
```

```csharp
public MNN.Unity.MNNDecisionOption.MNNDecisionOption(string label, string description = null)
```

```csharp
public static MNN.Unity.MNNDecisionQuestion MNN.Unity.MNNDecisionQuestion.Boolean(string instructions, string falseCriterion = null, string trueCriterion = null)
```

```csharp
public static MNN.Unity.MNNDecisionQuestion MNN.Unity.MNNDecisionQuestion.Choice(string instructions, params MNN.Unity.MNNDecisionOption[] options)
```

```csharp
public static MNN.Unity.MNNDecisionQuestion MNN.Unity.MNNDecisionQuestion.Score(string instructions, params string[] levels)
```

```csharp
public MNN.Unity.MNNGenerationDelta.MNNGenerationDelta()
```

```csharp
public MNN.Unity.MNNGenerationUpdate.MNNGenerationUpdate(string text, int tokens)
```

Create a cumulative provider update with a nonnegative generated-token count.

```csharp
public int MNN.Unity.MNNLlm.CountConversationTokens(System.Collections.Generic.IReadOnlyList<MNN.Unity.MNNChatMessage> messages)
```

Counts the official rendered text chat prompt, including role/template tokens. This does not change generation or cached conversation state.

```csharp
public int MNN.Unity.MNNLlm.CountTokens(string text)
```

```csharp
public void MNN.Unity.MNNLlm.Dispose()
```

```csharp
public MNN.Unity.MNNLlmResult MNN.Unity.MNNLlm.Generate(string prompt, int maxNewTokens = 128)
```

Starts an independent conversation with deterministic greedy sampling.

```csharp
public System.Threading.Tasks.Task<MNN.Unity.MNNLlmResult> MNN.Unity.MNNLlm.GenerateAsync(string prompt, int maxNewTokens = 128)
```

```csharp
public MNN.Unity.MNNMultimodalResult MNN.Unity.MNNLlm.GenerateConversation(System.Collections.Generic.IReadOnlyList<MNN.Unity.MNNChatMessage> messages, int maxNewTokens = 128, string imagePath = null, string audioPath = null, bool generateSpeech = false, int maxAudioTokens = 256, System.Action<MNN.Unity.MNNGenerationUpdate> onProgress = null, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken))
```

Uses the model's official chat template with separate roles. Media belongs to the final user message; previous messages contain text only. Exact matching token prefixes may reuse KV state; changes and interrupted requests reset it. Same platform/ABI and speech-output constraints as GenerateMultimodal.

```csharp
public System.Threading.Tasks.Task<MNN.Unity.MNNMultimodalResult> MNN.Unity.MNNLlm.GenerateConversationAsync(System.Collections.Generic.IReadOnlyList<MNN.Unity.MNNChatMessage> messages, int maxNewTokens = 128, string imagePath = null, string audioPath = null, bool generateSpeech = false, int maxAudioTokens = 256)
```

```csharp
public MNN.Unity.MNNMultimodalResult MNN.Unity.MNNLlm.GenerateConversationStreaming(System.Collections.Generic.IReadOnlyList<MNN.Unity.MNNChatMessage> messages, System.Action<MNN.Unity.MNNGenerationUpdate> onProgress, int maxNewTokens = 128, string imagePath = null, string audioPath = null, bool generateSpeech = false, int maxAudioTokens = 256, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken))
```

Real incremental decode through the official response/generate API. Callbacks run synchronously on the caller thread; marshal UI updates to the main thread. Cancellation is observed between tokens and before speech generation; it cannot interrupt prefill or a native waveform call. Existing platform/ABI limits apply.

```csharp
public System.Threading.Tasks.Task<MNN.Unity.MNNMultimodalResult> MNN.Unity.MNNLlm.GenerateConversationStreamingAsync(System.Collections.Generic.IReadOnlyList<MNN.Unity.MNNChatMessage> messages, System.Action<MNN.Unity.MNNGenerationUpdate> onProgress, int maxNewTokens = 128, string imagePath = null, string audioPath = null, bool generateSpeech = false, int maxAudioTokens = 256, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken))
```

```csharp
public MNN.Unity.MNNMultimodalResult MNN.Unity.MNNLlm.GenerateConversationStreamingDeltas(System.Collections.Generic.IReadOnlyList<MNN.Unity.MNNChatMessage> messages, System.Action<MNN.Unity.MNNGenerationDelta> onDelta, int maxNewTokens = 128, string imagePath = null, string audioPath = null, bool generateSpeech = false, int maxAudioTokens = 256, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken))
```

Streams only newly decoded text without allocating cumulative snapshots. Append each delta once; the returned result still contains the complete reply. Callbacks run synchronously on the inference caller's thread. Cancellation and platform/ABI constraints are the same as GenerateConversationStreaming.

```csharp
public System.Threading.Tasks.Task<MNN.Unity.MNNMultimodalResult> MNN.Unity.MNNLlm.GenerateConversationStreamingDeltasAsync(System.Collections.Generic.IReadOnlyList<MNN.Unity.MNNChatMessage> messages, System.Action<MNN.Unity.MNNGenerationDelta> onDelta, int maxNewTokens = 128, string imagePath = null, string audioPath = null, bool generateSpeech = false, int maxAudioTokens = 256, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken))
```

Schedules one inference worker for the request. Delta callbacks run on that worker; marshal Unity UI work to the main thread.

```csharp
public MNN.Unity.MNNMultimodalResult MNN.Unity.MNNLlm.GenerateMultimodal(string prompt, string imagePath = null, string audioPath = null, int maxNewTokens = 128, bool generateSpeech = false, int maxAudioTokens = 256)
```

One image and/or audio file plus text. Native speech output is currently unavailable; generateSpeech=true is rejected. Audio input must be a decodable WAV file. Operations are serialized with text generation and disposal.

```csharp
public System.Threading.Tasks.Task<MNN.Unity.MNNMultimodalResult> MNN.Unity.MNNLlm.GenerateMultimodalAsync(string prompt, string imagePath = null, string audioPath = null, int maxNewTokens = 128, bool generateSpeech = false, int maxAudioTokens = 256)
```

```csharp
public static MNN.Unity.MNNLlm MNN.Unity.MNNLlm.Load(string modelDirectory, MNN.Unity.MNNLlmPerformanceOptions performance, bool enableThinking = false, string cacheDirectory = null)
```

Unassigned properties use automatic tuning; explicit properties win. A caller-owned cacheDirectory persists native caches across model disposal.

```csharp
public static MNN.Unity.MNNLlm MNN.Unity.MNNLlm.Load(string modelDirectory, bool enableThinking = false, string cacheDirectory = null, MNN.Unity.MNNPrecisionMode? precisionMode = null, MNN.Unity.MNNBackendType backendType = MNN.Unity.MNNBackendType.Auto)
```

Loads with automatic device/model tuning for unassigned settings. Use MNNModelLoading.LoadAsync to keep loading off the render thread.

```csharp
public static MNN.Unity.MNNLlm MNN.Unity.MNNLlm.Load(string modelDirectory, int threadCount, bool enableThinking, string cacheDirectory, MNN.Unity.MNNPrecisionMode precisionMode, MNN.Unity.MNNBackendType backendType)
```

```csharp
public static MNN.Unity.MNNLlm MNN.Unity.MNNLlm.Load(string modelDirectory, int threadCount, bool enableThinking = false, string cacheDirectory = null, MNN.Unity.MNNPrecisionMode? precisionMode = null, MNN.Unity.MNNBackendType backendType = MNN.Unity.MNNBackendType.Auto)
```

Overrides the thread count; omitted precision/power still use automatic tuning.

```csharp
public static MNN.Unity.MNNLlm MNN.Unity.MNNLlm.LoadForChat(string modelDirectory, int threadCount, bool thinking, string cacheDirectory)
```

```csharp
public static MNN.Unity.MNNLlm MNN.Unity.MNNLlm.LoadForConversation(string modelDirectory, MNN.Unity.MNNLlmPerformanceOptions performance, float temperature = 0.85, bool enableThinking = false, string cacheDirectory = null)
```

Conversational sampling with automatic defaults and explicit property overrides.

```csharp
public static MNN.Unity.MNNLlm MNN.Unity.MNNLlm.LoadForConversation(string modelDirectory, bool enableThinking = false, string cacheDirectory = null, MNN.Unity.MNNPrecisionMode? precisionMode = null, MNN.Unity.MNNBackendType backendType = MNN.Unity.MNNBackendType.Auto)
```

```csharp
public static MNN.Unity.MNNLlm MNN.Unity.MNNLlm.LoadForConversation(string modelDirectory, int threadCount, bool enableThinking, string cacheDirectory, MNN.Unity.MNNPrecisionMode precisionMode, MNN.Unity.MNNBackendType backendType)
```

```csharp
public static MNN.Unity.MNNLlm MNN.Unity.MNNLlm.LoadForConversation(string modelDirectory, int threadCount, bool enableThinking = false, string cacheDirectory = null, MNN.Unity.MNNPrecisionMode? precisionMode = null, MNN.Unity.MNNBackendType backendType = MNN.Unity.MNNBackendType.Auto)
```

```csharp
public static MNN.Unity.MNNLlm MNN.Unity.MNNLlm.LoadForConversation(string modelDirectory, float temperature, bool enableThinking = false, string cacheDirectory = null, MNN.Unity.MNNPrecisionMode? precisionMode = null, MNN.Unity.MNNBackendType backendType = MNN.Unity.MNNBackendType.Auto)
```

Explicit temperature (0 to 2); unassigned execution settings use automatic tuning. Zero selects greedy decoding; positive values use penalty, top-k, top-p and temperature.

```csharp
public static MNN.Unity.MNNLlm MNN.Unity.MNNLlm.LoadForConversation(string modelDirectory, float temperature, int threadCount, bool enableThinking, string cacheDirectory, MNN.Unity.MNNPrecisionMode precisionMode, MNN.Unity.MNNBackendType backendType)
```

```csharp
public static MNN.Unity.MNNLlm MNN.Unity.MNNLlm.LoadForConversation(string modelDirectory, float temperature, int threadCount, bool enableThinking = false, string cacheDirectory = null, MNN.Unity.MNNPrecisionMode? precisionMode = null, MNN.Unity.MNNBackendType backendType = MNN.Unity.MNNBackendType.Auto)
```

```csharp
public MNN.Unity.MNNConversationContext MNN.Unity.MNNLlm.PrepareConversation(System.Collections.Generic.IReadOnlyList<MNN.Unity.MNNChatMessage> messages, int contextTokens, int reservedOutputTokens = 128)
```

Fits text history using exact official template/tokenizer counts. Preserves the first system message and latest user message, removing oldest complete turns only when needed. Supply the actual model context limit and reserve the same output budget used for generation. Media is rejected.

```csharp
public static MNN.Unity.MNNLlmCache MNN.Unity.MNNLlmCache.Acquire(string root, string modelDirectory, string nativeBuildIdentity, string deviceIdentity, MNN.Unity.MNNLlmPerformanceOptions performance, string modelIdentity = null)
```

Acquire a stable slot. nativeBuildIdentity must change when the native binary or its build options change (for example a build-manifest hash). deviceIdentity must include OS, GPU and driver identity. An optional verified modelIdentity from the delivery manifest avoids enumerating model file metadata; it must change for every model/tokenizer/template artifact update. Configuration text is always included. Cache root must be outside the model directory.

```csharp
public void MNN.Unity.MNNLlmCache.Dispose()
```

```csharp
public MNN.Unity.MNNLlmDynamicQuantization.MNNLlmDynamicQuantization()
```

```csharp
public MNN.Unity.MNNLlmPerformanceOptions.MNNLlmPerformanceOptions()
```

```csharp
public MNN.Unity.MNNLlmPerformanceOptions MNN.Unity.MNNLlmPerformanceOptions.Resolve(string modelDirectory)
```

Fill unassigned properties from measured device/model profiles or the platform fallback. Backend Auto still permits native availability/CPU fallback. Does not benchmark, load a model, modify model files, or change this object.

```csharp
public MNN.Unity.MNNLocalKnowledgeTool.MNNLocalKnowledgeTool(System.Collections.Generic.IEnumerable<System.Collections.Generic.KeyValuePair<string, string>> documents)
```

```csharp
public System.Threading.Tasks.Task<string> MNN.Unity.MNNLocalKnowledgeTool.ExecuteAsync(string input, System.Threading.CancellationToken token)
```

```csharp
public System.Collections.Generic.IReadOnlyList<System.Collections.Generic.KeyValuePair<string, string>> MNN.Unity.MNNLocalKnowledgeTool.Search(string input, System.Threading.CancellationToken token)
```

```csharp
public MNN.Unity.MNNMarkdownBlockType.MNNMarkdownBlockType()
```

```csharp
public static MNN.Unity.MNNMarkdownDocument MNN.Unity.MNNMarkdownDocument.Parse(string source)
```

```csharp
public MNN.Unity.MNNMarkdownHistoryLayout.MNNMarkdownHistoryLayout(int chunkCharacters = 1024, int maximumRenderCacheEntries = 256, float sliceSpacing = 0, float verticalPadding = 0, float firstSlicePadding = 0, float minimumLineHeight = 1.2, int maximumRenderCacheCharacters = 1048576)
```

```csharp
public static int MNN.Unity.MNNMarkdownHistoryLayout.ChunkLength(string text, int start, int chunkCharacters = 1024)
```

```csharp
public void MNN.Unity.MNNMarkdownHistoryLayout.Clear()
```

```csharp
public int MNN.Unity.MNNMarkdownHistoryLayout.FindSlice(int entry, int start)
```

```csharp
public int MNN.Unity.MNNMarkdownHistoryLayout.FirstVisible(float top)
```

```csharp
public MNN.Unity.MNNMarkdownHistoryLayout.MarkdownMeasurement.MarkdownMeasurement()
```

```csharp
public MNN.Unity.MNNMarkdownHistoryLayout.MarkdownMeasurement.MarkdownMeasurement(float bodyHeight, float totalHeight)
```

```csharp
public float MNN.Unity.MNNMarkdownHistoryLayout.Rebuild(MNN.Unity.IMNNConversationTranscriptSource entries, int pageStart, int pageSize, int dirtyFrom, int fontSize, System.Func<MNN.Unity.MNNMarkdownRenderResult, MNN.Unity.MNNMarkdownHistoryLayout.MarkdownMeasurement> measure, MNN.Unity.MNNMarkdownTheme theme = MNN.Unity.MNNMarkdownTheme.Dark)
```

```csharp
public float MNN.Unity.MNNMarkdownHistoryLayout.Rebuild(MNN.Unity.IMNNConversationTranscriptSource entries, int pageStart, int pageSize, int dirtyFrom, int fontSize, System.Func<MNN.Unity.MNNMarkdownRenderResult, MNN.Unity.MNNMarkdownHistoryLayout.MarkdownMeasurement> measure, MNN.Unity.MNNMarkdownTheme theme, System.Func<MNN.Unity.MNNConversationTranscriptEntry, MNN.Unity.MNNMarkdownRenderResult, MNN.Unity.MNNMarkdownHistoryLayout.MarkdownMeasurement> measureEntry)
```

```csharp
public MNN.Unity.MNNMarkdownImageCache.MNNMarkdownImageCache(int maximumImages = 32, long maximumEstimatedBytes = 67108864)
```

```csharp
public void MNN.Unity.MNNMarkdownImageCache.Clear()
```

Releases cached ownership. Displayed images survive until their last lease is released.

```csharp
public void MNN.Unity.MNNMarkdownImageCache.Dispose()
```

```csharp
public void MNN.Unity.MNNMarkdownImageCache.Lease.Dispose()
```

```csharp
public bool MNN.Unity.MNNMarkdownImageCache.TryAcquire(string key, out MNN.Unity.MNNMarkdownImageCache.Lease lease)
```

```csharp
public bool MNN.Unity.MNNMarkdownImageCache.TryStore(string key, UnityEngine.Texture2D texture, out MNN.Unity.MNNMarkdownImageCache.Lease lease)
```

Transfers ownership after argument validation, including on rejection. A duplicate key reuses the existing image and destroys the redundant texture. A texture exceeding the byte budget is destroyed. Byte estimates use Unity's reported native allocation with an RGBA mip estimate as a floor; they are platform-dependent accounting, not an exact GPU-memory limit or a bound on active displayed images.

```csharp
public MNN.Unity.MNNMarkdownImageFailure.MNNMarkdownImageFailure()
```

```csharp
public static bool MNN.Unity.MNNMarkdownImageHeader.TryReadDimensions(byte[] bytes, out int width, out int height)
```

```csharp
public MNN.Unity.MNNMarkdownImageRequest.MNNMarkdownImageRequest(string url, int maximumEncodedBytes = 16777216, long maximumPixels = 16777216, int timeoutSeconds = 20)
```

```csharp
public void MNN.Unity.MNNMarkdownImageRequest.Dispose()
```

```csharp
public bool MNN.Unity.MNNMarkdownImageRequest.TryGetTexture(out UnityEngine.Texture2D texture)
```

```csharp
public static System.Collections.Generic.IReadOnlyList<MNN.Unity.MNNMarkdownImage> MNN.Unity.MNNMarkdownRenderer.ExtractImages(string markdown)
```

```csharp
public static bool MNN.Unity.MNNMarkdownRenderer.IsSafeImageUrl(string url)
```

```csharp
public static MNN.Unity.MNNMarkdownRenderResult MNN.Unity.MNNMarkdownRenderer.Render(MNN.Unity.MNNMarkdownDocument document)
```

```csharp
public static MNN.Unity.MNNMarkdownRenderResult MNN.Unity.MNNMarkdownRenderer.Render(MNN.Unity.MNNMarkdownDocument document, int baseFontSize)
```

```csharp
public static MNN.Unity.MNNMarkdownRenderResult MNN.Unity.MNNMarkdownRenderer.Render(MNN.Unity.MNNMarkdownDocument document, int baseFontSize, MNN.Unity.MNNMarkdownTheme theme)
```

```csharp
public static MNN.Unity.MNNMarkdownRenderResult MNN.Unity.MNNMarkdownRenderer.Render(string markdown)
```

```csharp
public static MNN.Unity.MNNMarkdownRenderResult MNN.Unity.MNNMarkdownRenderer.Render(string markdown, int baseFontSize)
```

```csharp
public static MNN.Unity.MNNMarkdownRenderResult MNN.Unity.MNNMarkdownRenderer.Render(string markdown, int baseFontSize, MNN.Unity.MNNMarkdownTheme theme)
```

```csharp
public static System.Collections.Generic.IReadOnlyList<MNN.Unity.MNNMarkdownRenderResult> MNN.Unity.MNNMarkdownRenderer.RenderSlices(string markdown, int maxCharacters = 1024, int baseFontSize = 20)
```

Parses the complete source before dividing formatted text into bounded UI slices. Tags are balanced per slice; images belong to the slice containing their marker. The limit counts UTF-16 text units, excluding generated formatting tags.

```csharp
public static System.Collections.Generic.IReadOnlyList<MNN.Unity.MNNMarkdownRenderResult> MNN.Unity.MNNMarkdownRenderer.RenderSlices(string markdown, int maxCharacters, int baseFontSize, MNN.Unity.MNNMarkdownTheme theme)
```

Renders bounded slices using the caller's surface theme.

```csharp
public static string MNN.Unity.MNNMarkdownRenderer.ToCodeRichText(string code, string language, MNN.Unity.MNNMarkdownTheme theme = MNN.Unity.MNNMarkdownTheme.Dark)
```

Escapes literal code and applies lexical colors for a supported fence language. No Markdown is interpreted.

```csharp
public static string MNN.Unity.MNNMarkdownRenderer.ToRichText(MNN.Unity.MNNMarkdownBlock block)
```

```csharp
public static string MNN.Unity.MNNMarkdownRenderer.ToRichText(MNN.Unity.MNNMarkdownBlock block, MNN.Unity.MNNMarkdownTheme theme)
```

```csharp
public static string MNN.Unity.MNNMarkdownRenderer.ToRichText(string markdown)
```

```csharp
public static string MNN.Unity.MNNMarkdownRenderer.ToRichText(string markdown, MNN.Unity.MNNMarkdownTheme theme)
```

```csharp
public MNN.Unity.MNNMarkdownTheme.MNNMarkdownTheme()
```

```csharp
public void MNN.Unity.MNNMiniMindO.Dispose()
```

```csharp
public MNN.Unity.MNNMiniMindOResult MNN.Unity.MNNMiniMindO.Generate(MNN.Unity.MNNMiniMindORequest request, MNN.Unity.MNNMiniMindOOptions options = null, System.Action<MNN.Unity.MNNMiniMindOUpdate> onUpdate = null, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken))
```

Run one completed turn. At most 512 prompt plus generation tokens are allowed by this bounded-memory adapter.

```csharp
public MNN.Unity.MNNMiniMindOResult MNN.Unity.MNNMiniMindO.Generate(string prompt, int maxNewTokens = 128, int seed = 42, System.Action<MNN.Unity.MNNMiniMindOUpdate> onUpdate = null, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken))
```

Generate a text turn with the released temperature 0.7/top-p 0.85 and default voice.

```csharp
public System.Threading.Tasks.Task<MNN.Unity.MNNMiniMindOResult> MNN.Unity.MNNMiniMindO.GenerateAsync(MNN.Unity.MNNMiniMindORequest request, MNN.Unity.MNNMiniMindOOptions options = null, System.Action<MNN.Unity.MNNMiniMindOUpdate> onUpdate = null, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken))
```

Run synchronous native calls on a worker; callbacks also run on that worker.

```csharp
public static bool MNN.Unity.MNNMiniMindO.IsComplete(string directory)
```

Check all graphs required for text, image/audio input and speech output without loading native libraries.

```csharp
public static MNN.Unity.MNNMiniMindO MNN.Unity.MNNMiniMindO.Load(string directory, int threads = 4)
```

Load an installed MiniMind-o package. CPU/Low precision/Low memory preserves the measured quantization recipe.

```csharp
public static float[] MNN.Unity.MNNMiniMindO.PrepareImage(MNN.Unity.MNNRgbImage image, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken))
```

Apply the released SigLIP2 processor: Pillow-compatible bilinear resize and RGB normalization.

```csharp
public MNN.Unity.MNNMiniMindOOptions.MNNMiniMindOOptions()
```

```csharp
public MNN.Unity.MNNMiniMindORequest.MNNMiniMindORequest()
```

```csharp
public MNN.Unity.MNNModelCapabilities.MNNModelCapabilities()
```

```csharp
public MNN.Unity.MNNOpenAIClient.MNNOpenAIClient(System.Uri endpoint, string key, System.TimeSpan timeout, System.Net.Http.HttpClient client = null)
```

```csharp
public void MNN.Unity.MNNOpenAIClient.Dispose()
```

```csharp
public System.Threading.Tasks.Task MNN.Unity.MNNOpenAIClient.GenerateTextAsync(string model, System.Collections.Generic.IReadOnlyList<MNN.Unity.MNNOpenAIMessage> history, System.Func<string, System.Threading.Tasks.Task> onDelta, System.Threading.CancellationToken token, float[] monoAudio = null, int sampleRate = 16000, int maximumCharacters = 16384, int? maximumTokens = null, float? temperature = null)
```

```csharp
public System.Threading.Tasks.Task MNN.Unity.MNNOpenAIClient.SynthesizeAsync(string model, string voice, string text, System.Func<float[], int, System.Threading.Tasks.Task> onPcm, System.Threading.CancellationToken token)
```

Borrowed mono 24 kHz normalized PCM, at most 480 samples per callback; no full-waveform buffering.

```csharp
public System.Threading.Tasks.Task<string> MNN.Unity.MNNOpenAIClient.TranscribeAsync(string model, float[] monoAudio, int sampleRate, System.Threading.CancellationToken token)
```

```csharp
public MNN.Unity.MNNOpenAIConversationOptions.MNNOpenAIConversationOptions()
```

```csharp
public MNN.Unity.MNNOpenAIConversationProvider.MNNOpenAIConversationProvider(MNN.Unity.MNNOpenAIConversationOptions settings, string key, MNN.Unity.MNNOpenAIConversationStage stage, System.Net.Http.HttpClient client = null)
```

```csharp
public System.Threading.Tasks.Task MNN.Unity.MNNOpenAIConversationProvider.ClassifyVoiceIntentAsync(System.Collections.Generic.IReadOnlyList<MNN.Unity.MNNOpenAIMessage> prompt, System.Func<string, System.Threading.Tasks.Task> textDelta, System.Threading.CancellationToken token)
```

```csharp
public static MNN.Unity.MNNRealtimeCapabilities MNN.Unity.MNNOpenAIConversationProvider.Describe(MNN.Unity.MNNOpenAIConversationOptions config, MNN.Unity.MNNOpenAIConversationStage stage)
```

```csharp
public void MNN.Unity.MNNOpenAIConversationProvider.Dispose()
```

```csharp
public System.Threading.Tasks.Task MNN.Unity.MNNOpenAIConversationProvider.GenerateAssistanceAsync(MNN.Unity.MNNConversationTextTask task, System.Collections.Generic.IReadOnlyList<MNN.Unity.MNNOpenAIMessage> prompt, System.Func<string, System.Threading.Tasks.Task> textDelta, System.Threading.CancellationToken token)
```

```csharp
public System.Threading.Tasks.Task MNN.Unity.MNNOpenAIConversationProvider.GenerateAsync(System.Collections.Generic.IReadOnlyList<MNN.Unity.MNNOpenAIMessage> history, float[] audio, int sampleRate, System.Func<string, System.Threading.Tasks.Task> textDelta, System.Threading.CancellationToken token)
```

```csharp
public System.Threading.Tasks.Task MNN.Unity.MNNOpenAIConversationProvider.PrepareAsync(System.Threading.CancellationToken token)
```

```csharp
public System.Threading.Tasks.Task MNN.Unity.MNNOpenAIConversationProvider.SynthesizeAsync(string text, System.Func<float[], int, System.Threading.Tasks.Task> pcm, System.Threading.CancellationToken token)
```

```csharp
public System.Threading.Tasks.Task<string> MNN.Unity.MNNOpenAIConversationProvider.TranscribeAsync(float[] audio, int sampleRate, System.Threading.CancellationToken token)
```

```csharp
public MNN.Unity.MNNOpenAIConversationStage.MNNOpenAIConversationStage()
```

```csharp
public MNN.Unity.MNNOpenAIHttpException.MNNOpenAIHttpException(System.Net.HttpStatusCode status)
```

```csharp
public MNN.Unity.MNNOpenAIMessage.MNNOpenAIMessage()
```

```csharp
public MNN.Unity.MNNOpenAIMessage.MNNOpenAIMessage(MNN.Unity.MNNChatRole role, string text)
```

```csharp
public MNN.Unity.MNNOpenAIMessage.MNNOpenAIMessage(float[] audio, int sampleRate)
```

```csharp
public static MNN.Unity.MNNModelRepositoryFile[] MNN.Unity.MNNPocketModel.Files()
```

Returns a fresh installation plan, including the upstream Apache 2.0 license.

```csharp
public static bool MNN.Unity.MNNPocketModel.IsRepository(string repository)
```

```csharp
public MNN.Unity.MNNRealtimeCapabilities.MNNRealtimeCapabilities(MNN.Unity.MNNRealtimeFeature features, string contract, string evidence, string limitation = null, double? sustainedRtf = null, double sustainedRtfWindowAudioSeconds = 0, MNN.Unity.MNNRealtimeExecution execution = MNN.Unity.MNNRealtimeExecution.LocalMnn)
```

```csharp
public MNN.Unity.MNNRealtimeExecution.MNNRealtimeExecution()
```

```csharp
public MNN.Unity.MNNRealtimeFallbackRuntime.MNNRealtimeFallbackRuntime(MNN.Unity.IMNNRealtimeRuntimeFactory factory, MNN.Unity.MNNRealtimeOutputPipeline output, MNN.Unity.MNNRealtimeCapabilities model, MNN.Unity.MNNRealtimeCapabilities device, MNN.Unity.MNNRealtimeCapabilities tts = null, MNN.Unity.MNNRealtimeCapabilities asr = null, MNN.Unity.MNNRealtimeCapabilities vad = null, System.Collections.Generic.IReadOnlyCollection<MNN.Unity.MNNRealtimeRoute> excludedRoutes = null, bool requireMeasuredSpeech = true, bool allowBufferedSpeech = false, bool speechOutput = true)
```

Validates initial selection without acquiring resources. Capability snapshots stay fixed.

```csharp
public System.Threading.Tasks.Task MNN.Unity.MNNRealtimeFallbackRuntime.DisposeAsync()
```

```csharp
public System.Threading.Tasks.Task MNN.Unity.MNNRealtimeFallbackRuntime.PrepareAsync(System.Threading.CancellationToken cancellationToken)
```

```csharp
public System.Threading.Tasks.Task MNN.Unity.MNNRealtimeFallbackRuntime.RequestCancellationAsync(MNN.Unity.MNNResponseId response)
```

```csharp
public System.Threading.Tasks.Task MNN.Unity.MNNRealtimeFallbackRuntime.RestoreContextAsync(MNN.Unity.MNNResponseId response)
```

Session already silenced and joined the interrupted producer. A route-specific restore failure releases the entire old runtime, then prepares and starts the next one at a new turn boundary. No old response is generated again. New runtime Prepare must restore committed history.

```csharp
public System.Threading.Tasks.Task MNN.Unity.MNNRealtimeFallbackRuntime.StartListeningAsync(System.Threading.CancellationToken cancellationToken)
```

```csharp
public System.Threading.Tasks.Task MNN.Unity.MNNRealtimeFallbackRuntime.StopListeningAsync()
```

```csharp
public System.Threading.Tasks.Task MNN.Unity.MNNRealtimeFallbackRuntime.WaitForInferenceQuiescenceAsync(MNN.Unity.MNNResponseId response)
```

```csharp
public System.Threading.Tasks.Task MNN.Unity.MNNRealtimeFallbackRuntime.WaitForQuiescenceAsync()
```

```csharp
public MNN.Unity.MNNRealtimeFeature.MNNRealtimeFeature()
```

```csharp
public MNN.Unity.MNNRealtimeMode.MNNRealtimeMode()
```

```csharp
public MNN.Unity.MNNRealtimeOutputPipeline.MNNRealtimeOutputPipeline(MNN.Unity.MNNPcmFormat format, int framesPerBlock, int blockCapacity = 6, int receiptCapacity = 256, int segmentCapacity = 256)
```

```csharp
public bool MNN.Unity.MNNRealtimeOutputPipeline.Accepts(MNN.Unity.MNNResponseId response)
```

```csharp
public void MNN.Unity.MNNRealtimeOutputPipeline.AcknowledgePresentation(long throughFrame, bool estimated)
```

The watermark is the exclusive device presentation frame.

```csharp
public MNN.Unity.MNNResponseId MNN.Unity.MNNRealtimeOutputPipeline.BeginResponse()
```

Control owner only. Use Interrupt first when replacing a response before it naturally finishes.

```csharp
public bool MNN.Unity.MNNRealtimeOutputPipeline.CanBeginNextResponse(MNN.Unity.MNNResponseId completedResponse)
```

```csharp
public bool MNN.Unity.MNNRealtimeOutputPipeline.CompleteResponse(MNN.Unity.MNNResponseId response)
```

Marks generation EOS after its sole producer has stopped publishing; queued audio still plays.

```csharp
public int MNN.Unity.MNNRealtimeOutputPipeline.DrainReceipts(int maximumReceipts)
```

Drain in render order; receipts become submitted history, not yet presented history.

```csharp
public MNN.Unity.MNNConversationSegment MNN.Unity.MNNRealtimeOutputPipeline.GetSegment(int index)
```

```csharp
public MNN.Unity.MNNResponseId MNN.Unity.MNNRealtimeOutputPipeline.Interrupt()
```

Immediately gates playback; hardware already submitted remains subject to its device tail.

```csharp
public void MNN.Unity.MNNRealtimeOutputPipeline.Render(float[] destination, int offset, int count)
```

Device callback only. Supplies every output sample, including silence.

```csharp
public void MNN.Unity.MNNRealtimeOutputPipeline.RetireHistoryThrough(MNN.Unity.MNNResponseId response)
```

Explicit context rollover after the caller joins old producers and snapshots presented text. Late device receipts from these epochs are ignored conservatively. The presentation clock is preserved.

```csharp
public bool MNN.Unity.MNNRealtimeOutputPipeline.TryEnqueue(float[] samples, int offset, int count, MNN.Unity.MNNSpeechChunkInfo chunk)
```

Producer copies bounded PCM. False means stale response, fault or queue backpressure.

```csharp
public bool MNN.Unity.MNNRealtimeOutputPipeline.TryRegisterSegment(MNN.Unity.MNNResponseId response, long segmentId, string stableSpokenText)
```

Register stable spoken text before enqueueing audio. Safe from the single active response producer.

```csharp
public static MNN.Unity.MNNRealtimeReport MNN.Unity.MNNRealtimePreflight.Check(MNN.Unity.MNNRealtimeMode mode, MNN.Unity.MNNSpeechRoute route, MNN.Unity.MNNRealtimeCapabilities model, MNN.Unity.MNNRealtimeCapabilities device, MNN.Unity.MNNRealtimeCapabilities tts = null, MNN.Unity.MNNRealtimeCapabilities asr = null, MNN.Unity.MNNRealtimeCapabilities vad = null, bool requireMeasuredSpeech = true, bool allowBufferedSpeech = false)
```

```csharp
public static MNN.Unity.MNNRealtimeReport MNN.Unity.MNNRealtimePreflight.Check(MNN.Unity.MNNRealtimeRoute route, MNN.Unity.MNNRealtimeCapabilities model, MNN.Unity.MNNRealtimeCapabilities device, MNN.Unity.MNNRealtimeCapabilities tts = null, MNN.Unity.MNNRealtimeCapabilities asr = null, MNN.Unity.MNNRealtimeCapabilities vad = null, bool requireMeasuredSpeech = true, bool allowBufferedSpeech = false, bool speechOutput = true)
```

Checks a specific topology. Buffered speech is opt-in and available only for turn-based routes with explicit waveform and half-duplex capture capabilities. It never infers progressive output, continuous understanding or measured throughput. Audio-model routes do not infer continuous understanding from audio input alone.

```csharp
public static MNN.Unity.MNNRealtimeReport MNN.Unity.MNNRealtimePreflight.CheckBundledBackend(MNN.Unity.MNNRealtimeMode mode, MNN.Unity.MNNSpeechRoute route)
```

No currently registered official continuous-audio backend exists. Existing batch APIs are not substituted.

```csharp
public void MNN.Unity.MNNRealtimeReport.RequireSupported()
```

```csharp
public override string MNN.Unity.MNNRealtimeReport.ToString()
```

```csharp
public MNN.Unity.MNNRealtimeRoute.MNNRealtimeRoute()
```

```csharp
public MNN.Unity.MNNRealtimeReport MNN.Unity.MNNRealtimeRouteSelection.RequireSelected()
```

Returns the report consumed by MNNRealtimeSessionComponent.Configure, or explains all rejected routes.

```csharp
public override string MNN.Unity.MNNRealtimeRouteSelection.ToString()
```

```csharp
public static MNN.Unity.MNNRealtimeRouteSelection MNN.Unity.MNNRealtimeRouteSelector.SelectBest(MNN.Unity.MNNRealtimeCapabilities model, MNN.Unity.MNNRealtimeCapabilities device, MNN.Unity.MNNRealtimeCapabilities tts = null, MNN.Unity.MNNRealtimeCapabilities asr = null, MNN.Unity.MNNRealtimeCapabilities vad = null, System.Collections.Generic.IReadOnlyCollection<MNN.Unity.MNNRealtimeRoute> excludedRoutes = null, bool requireMeasuredSpeech = true, bool allowBufferedSpeech = false, bool speechOutput = true)
```

Runs before resource acquisition or after old owners have quiesced. Exclude failed routes to make subsequent selections progress without retry loops. Cancellation, permissions and shared device/ABI failures are not reasons to blindly replay a response on a different route. Allocation is bounded by the five topologies and occurs only on the control path.

```csharp
public MNN.Unity.MNNRealtimeRouteUnavailableException.MNNRealtimeRouteUnavailableException(MNN.Unity.MNNRealtimeRoute route, string message, System.Exception innerException = null)
```

```csharp
public MNN.Unity.MNNRealtimeSession.MNNRealtimeSession(MNN.Unity.IMNNRealtimeSessionRuntime runtime, MNN.Unity.MNNRealtimeOutputPipeline output)
```

```csharp
public MNN.Unity.MNNResponseId MNN.Unity.MNNRealtimeSession.BeginResponse()
```

Starts a new reply epoch. Only one live reply may own the shared output pipeline.

```csharp
public bool MNN.Unity.MNNRealtimeSession.CompleteResponse(MNN.Unity.MNNResponseId response)
```

Signals model EOS after its producer has stopped; playback continues until presentation is acknowledged.

```csharp
public System.Threading.Tasks.Task MNN.Unity.MNNRealtimeSession.InterruptAsync()
```

Silences audio before returning to the caller, then serially cancels, joins, and restores the old response. Repeated calls during the same recovery share one operation.

```csharp
public System.Threading.Tasks.Task MNN.Unity.MNNRealtimeSession.ReportFaultAsync(MNN.Unity.MNNResponseId response, System.Exception error)
```

Reports a fatal response-producer failure only while that response is current. Errors from an interrupted, replaced or foreign response are ignored. Repeated reports of the fault that initiated shutdown share the shutdown task. Use the session-wide overload for device and recovery failures.

```csharp
public System.Threading.Tasks.Task MNN.Unity.MNNRealtimeSession.ReportFaultAsync(System.Exception error)
```

Reports a fatal device/worker failure, silences synchronously and joins the runtime before disposal. The first reported failure is retained in Fault and the returned shutdown task. This is a control operation, not an audio callback API. A worker that shutdown must join must report without waiting and exit; an external owner observes the task. Awaiting it inside that worker deadlocks.

```csharp
public System.Threading.Tasks.Task MNN.Unity.MNNRealtimeSession.StartAsync(System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken))
```

```csharp
public System.Threading.Tasks.Task MNN.Unity.MNNRealtimeSession.StopAsync()
```

Silences immediately, then stops input and disposes resources only after startup/recovery has settled.

```csharp
public MNN.Unity.MNNRealtimeSessionState.MNNRealtimeSessionState()
```

```csharp
public MNN.Unity.MNNResponseId.MNNResponseId()
```

```csharp
public bool MNN.Unity.MNNResponseId.Equals(MNN.Unity.MNNResponseId other)
```

```csharp
public override bool MNN.Unity.MNNResponseId.Equals(object obj)
```

```csharp
public override int MNN.Unity.MNNResponseId.GetHashCode()
```

```csharp
public static bool MNN.Unity.MNNResponseId.operator ==(MNN.Unity.MNNResponseId left, MNN.Unity.MNNResponseId right)
```

```csharp
public static bool MNN.Unity.MNNResponseId.operator !=(MNN.Unity.MNNResponseId left, MNN.Unity.MNNResponseId right)
```

```csharp
public MNN.Unity.MNNRouteLlmClient.MNNRouteLlmClient(MNN.Unity.MNNBoundedHttpClient http, System.Uri endpoint, float threshold = 0.5)
```

```csharp
public System.Threading.Tasks.Task<bool> MNN.Unity.MNNRouteLlmClient.SelectCloudAsync(string prompt, System.Threading.CancellationToken token)
```

```csharp
public MNN.Unity.MNNSpeechRoute.MNNSpeechRoute()
```

```csharp
public MNN.Unity.MNNDecisionResult MNN.Unity.MNNTextDecision.Decide(string state, MNN.Unity.MNNDecisionQuestion question, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken))
```

```csharp
public System.Threading.Tasks.Task<MNN.Unity.MNNDecisionResult> MNN.Unity.MNNTextDecision.DecideAsync(string state, MNN.Unity.MNNDecisionQuestion question, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken))
```

```csharp
public void MNN.Unity.MNNTextDecision.Dispose()
```

```csharp
public static MNN.Unity.MNNTextDecision MNN.Unity.MNNTextDecision.Load(string directory, int threadCount = 4, MNN.Unity.MNNBackendType backendType = MNN.Unity.MNNBackendType.Auto)
```

```csharp
public MNN.Samples.SiriAI.Harnesses.HarnessArtifact[] MNN.Samples.SiriAI.Harnesses.HarnessActivity.Artifacts { get; }
```

```csharp
public string MNN.Samples.SiriAI.Harnesses.HarnessActivity.ExecutionDetail { get; }
```

```csharp
public MNN.Samples.SiriAI.Harnesses.HarnessExecutionKind MNN.Samples.SiriAI.Harnesses.HarnessActivity.ExecutionKind { get; }
```

```csharp
public string MNN.Samples.SiriAI.Harnesses.HarnessActivity.ExecutionState { get; }
```

```csharp
public MNN.Samples.SiriAI.Harnesses.HarnessInputForm MNN.Samples.SiriAI.Harnesses.HarnessActivity.InputForm { get; }
```

```csharp
public MNN.Samples.SiriAI.Harnesses.HarnessInputPrompt MNN.Samples.SiriAI.Harnesses.HarnessActivity.InputPrompt { get; }
```

```csharp
public MNN.Samples.SiriAI.Harnesses.HarnessNativeToolState MNN.Samples.SiriAI.Harnesses.HarnessActivity.NativeTools { get; }
```

```csharp
public string MNN.Samples.SiriAI.Harnesses.HarnessActivity.RequestId { get; }
```

```csharp
public MNN.Samples.SiriAI.Harnesses.HarnessSessionConfiguration MNN.Samples.SiriAI.Harnesses.HarnessActivity.SessionConfiguration { get; }
```

```csharp
public string MNN.Samples.SiriAI.Harnesses.HarnessActivity.Summary { get; }
```

```csharp
public MNN.Samples.SiriAI.Harnesses.HarnessWaitingFor MNN.Samples.SiriAI.Harnesses.HarnessActivity.WaitingFor { get; }
```

```csharp
public string MNN.Samples.SiriAI.Harnesses.HarnessArtifact.Description { get; private set; }
```

```csharp
public string MNN.Samples.SiriAI.Harnesses.HarnessArtifact.Id { get; private set; }
```

```csharp
public string MNN.Samples.SiriAI.Harnesses.HarnessArtifact.Name { get; private set; }
```

```csharp
public MNN.Samples.SiriAI.Harnesses.HarnessContentPart[] MNN.Samples.SiriAI.Harnesses.HarnessArtifact.Parts { get; }
```

```csharp
public string MNN.Samples.SiriAI.Harnesses.HarnessArtifact.SourceArtifactId { get; private set; }
```

```csharp
public string MNN.Samples.SiriAI.Harnesses.HarnessArtifact.SourceTaskId { get; private set; }
```

```csharp
public static System.Collections.Generic.IReadOnlyList<MNN.Samples.SiriAI.Harnesses.HarnessBackendDefinition> MNN.Samples.SiriAI.Harnesses.HarnessBackendCatalog.All { get; }
```

```csharp
public string MNN.Samples.SiriAI.Harnesses.HarnessBackendDefinition.Adapter { get; }
```

```csharp
public string MNN.Samples.SiriAI.Harnesses.HarnessBackendDefinition.AdapterVariable { get; }
```

```csharp
public string MNN.Samples.SiriAI.Harnesses.HarnessBackendDefinition.Command { get; }
```

```csharp
public System.Collections.Generic.IReadOnlyList<MNN.Samples.SiriAI.Harnesses.HarnessInstallComponent> MNN.Samples.SiriAI.Harnesses.HarnessBackendDefinition.Components { get; }
```

```csharp
public string MNN.Samples.SiriAI.Harnesses.HarnessBackendDefinition.ConfigurationHint { get; }
```

```csharp
public string MNN.Samples.SiriAI.Harnesses.HarnessBackendDefinition.DocumentationUrl { get; }
```

```csharp
public System.Collections.Generic.IReadOnlyList<string> MNN.Samples.SiriAI.Harnesses.HarnessBackendDefinition.ExecutableVariables { get; }
```

```csharp
public string MNN.Samples.SiriAI.Harnesses.HarnessBackendDefinition.Id { get; }
```

```csharp
public string MNN.Samples.SiriAI.Harnesses.HarnessBackendDefinition.Integration { get; }
```

```csharp
public string MNN.Samples.SiriAI.Harnesses.HarnessBackendDefinition.Name { get; }
```

```csharp
public string MNN.Samples.SiriAI.Harnesses.HarnessCollectedResult.ContentHash { get; private set; }
```

```csharp
public string MNN.Samples.SiriAI.Harnesses.HarnessCollectedResult.TaskId { get; private set; }
```

```csharp
public string MNN.Samples.SiriAI.Harnesses.HarnessContentPart.FileName { get; private set; }
```

```csharp
public MNN.Samples.SiriAI.Harnesses.HarnessContentKind MNN.Samples.SiriAI.Harnesses.HarnessContentPart.Kind { get; private set; }
```

```csharp
public string MNN.Samples.SiriAI.Harnesses.HarnessContentPart.MediaType { get; private set; }
```

```csharp
public string MNN.Samples.SiriAI.Harnesses.HarnessContentPart.Payload { get; private set; }
```

```csharp
public MNN.Samples.SiriAI.Harnesses.HarnessExecutionKind MNN.Samples.SiriAI.Harnesses.HarnessExecutionStep.Kind { get; private set; }
```

```csharp
public long MNN.Samples.SiriAI.Harnesses.HarnessExecutionStep.Sequence { get; private set; }
```

```csharp
public string MNN.Samples.SiriAI.Harnesses.HarnessExecutionStep.State { get; private set; }
```

```csharp
public string MNN.Samples.SiriAI.Harnesses.HarnessExecutionStep.Text { get; private set; }
```

```csharp
public string MNN.Samples.SiriAI.Harnesses.HarnessExecutionStep.Title { get; private set; }
```

```csharp
public string MNN.Samples.SiriAI.Harnesses.HarnessHostHealth.HostId { get; set; }
```

```csharp
public string MNN.Samples.SiriAI.Harnesses.HarnessHostHealth.InstanceId { get; set; }
```

```csharp
public string MNN.Samples.SiriAI.Harnesses.HarnessHostHealth.OwnerId { get; set; }
```

```csharp
public int MNN.Samples.SiriAI.Harnesses.HarnessHostHealth.ProcessId { get; set; }
```

```csharp
public int MNN.Samples.SiriAI.Harnesses.HarnessHostHealth.ProtocolVersion { get; set; }
```

```csharp
public string MNN.Samples.SiriAI.Harnesses.HarnessHostHealth.ServiceLabel { get; set; }
```

```csharp
public long MNN.Samples.SiriAI.Harnesses.HarnessHostHealth.StartedUtcTicks { get; set; }
```

```csharp
public string MNN.Samples.SiriAI.Harnesses.HarnessHostProfile.Address { get; private set; }
```

```csharp
public string MNN.Samples.SiriAI.Harnesses.HarnessHostProfile.HarnessId { get; private set; }
```

```csharp
public string MNN.Samples.SiriAI.Harnesses.HarnessHostProfile.Id { get; private set; }
```

```csharp
public string MNN.Samples.SiriAI.Harnesses.HarnessHostProfile.Name { get; private set; }
```

```csharp
public string MNN.Samples.SiriAI.Harnesses.HarnessHostProfile.WorkspaceKey { get; private set; }
```

```csharp
public bool MNN.Samples.SiriAI.Harnesses.HarnessInputPrompt.BackendCompleted { get; private set; }
```

```csharp
public MNN.Samples.SiriAI.Harnesses.HarnessInputMode MNN.Samples.SiriAI.Harnesses.HarnessInputPrompt.Mode { get; private set; }
```

```csharp
public string MNN.Samples.SiriAI.Harnesses.HarnessInputPrompt.NativeId { get; private set; }
```

```csharp
public string MNN.Samples.SiriAI.Harnesses.HarnessInputPrompt.Url { get; private set; }
```

```csharp
public bool MNN.Samples.SiriAI.Harnesses.HarnessInstallComponent.Adapter { get; }
```

```csharp
public string MNN.Samples.SiriAI.Harnesses.HarnessInstallComponent.Package { get; }
```

```csharp
public string MNN.Samples.SiriAI.Harnesses.HarnessInstallComponent.ScriptUrl { get; }
```

```csharp
public MNN.Samples.SiriAI.Harnesses.HarnessContentPart[] MNN.Samples.SiriAI.Harnesses.HarnessInstruction.Attachments { get; }
```

```csharp
public string MNN.Samples.SiriAI.Harnesses.HarnessInstruction.Detail { get; private set; }
```

```csharp
public string MNN.Samples.SiriAI.Harnesses.HarnessInstruction.Id { get; private set; }
```

```csharp
public MNN.Samples.SiriAI.Harnesses.HarnessInstructionState MNN.Samples.SiriAI.Harnesses.HarnessInstruction.State { get; private set; }
```

```csharp
public string MNN.Samples.SiriAI.Harnesses.HarnessInstruction.Text { get; private set; }
```

```csharp
public string MNN.Samples.SiriAI.Harnesses.HarnessNativeToolCall.Kind { get; private set; }
```

```csharp
public string[] MNN.Samples.SiriAI.Harnesses.HarnessNativeToolCall.OmittedFields { get; }
```

```csharp
public string MNN.Samples.SiriAI.Harnesses.HarnessNativeToolCall.Payload { get; private set; }
```

```csharp
public string MNN.Samples.SiriAI.Harnesses.HarnessNativeToolCall.Status { get; private set; }
```

```csharp
public string MNN.Samples.SiriAI.Harnesses.HarnessNativeToolCall.Title { get; private set; }
```

```csharp
public string MNN.Samples.SiriAI.Harnesses.HarnessNativeToolCall.ToolCallId { get; private set; }
```

```csharp
public MNN.Samples.SiriAI.Harnesses.HarnessNativeToolCall[] MNN.Samples.SiriAI.Harnesses.HarnessNativeToolState.Calls { get; }
```

```csharp
public bool MNN.Samples.SiriAI.Harnesses.HarnessNativeToolState.Truncated { get; private set; }
```

```csharp
public MNN.Samples.SiriAI.Harnesses.HarnessArtifact[] MNN.Samples.SiriAI.Harnesses.HarnessOutcome.Artifacts { get; }
```

```csharp
public MNN.Samples.SiriAI.Harnesses.HarnessCollectedResult[] MNN.Samples.SiriAI.Harnesses.HarnessOutcome.CollectedResults { get; }
```

```csharp
public MNN.Samples.SiriAI.Harnesses.HarnessTaskState MNN.Samples.SiriAI.Harnesses.HarnessOutcome.State { get; }
```

```csharp
public string MNN.Samples.SiriAI.Harnesses.HarnessOutcome.Text { get; }
```

```csharp
public MNN.Samples.SiriAI.Harnesses.HarnessHostProfile[] MNN.Samples.SiriAI.Harnesses.HarnessProfileCatalog.Profiles { get; }
```

```csharp
public string MNN.Samples.SiriAI.Harnesses.HarnessProfileCatalog.SelectedId { get; }
```

```csharp
public MNN.Samples.SiriAI.Harnesses.RemoteTaskClient MNN.Samples.SiriAI.Harnesses.HarnessResultConnection.Client { get; }
```

```csharp
public string MNN.Samples.SiriAI.Harnesses.HarnessResultConnection.ConversationId { get; }
```

```csharp
public int MNN.Samples.SiriAI.Harnesses.HarnessSkillClient.PendingCount { get; }
```

```csharp
public string MNN.Samples.SiriAI.Harnesses.HarnessTask.Activity { get; internal set; }
```

```csharp
public MNN.Samples.SiriAI.Harnesses.HarnessArtifact[] MNN.Samples.SiriAI.Harnesses.HarnessTask.Artifacts { get; internal set; }
```

```csharp
public MNN.Samples.SiriAI.Harnesses.HarnessContentPart[] MNN.Samples.SiriAI.Harnesses.HarnessTask.Attachments { get; internal set; }
```

```csharp
public string MNN.Samples.SiriAI.Harnesses.HarnessTask.BackendState { get; internal set; }
```

```csharp
public MNN.Samples.SiriAI.Harnesses.HarnessCollectedResult[] MNN.Samples.SiriAI.Harnesses.HarnessTask.CollectedResults { get; internal set; }
```

```csharp
public bool MNN.Samples.SiriAI.Harnesses.HarnessTask.ContextInjected { get; internal set; }
```

```csharp
public string MNN.Samples.SiriAI.Harnesses.HarnessTask.ConversationId { get; internal set; }
```

```csharp
public long MNN.Samples.SiriAI.Harnesses.HarnessTask.CreatedUtcTicks { get; internal set; }
```

```csharp
public string MNN.Samples.SiriAI.Harnesses.HarnessTask.DeliveryConversationId { get; }
```

```csharp
public MNN.Samples.SiriAI.Harnesses.HarnessExecutionStep[] MNN.Samples.SiriAI.Harnesses.HarnessTask.Execution { get; internal set; }
```

```csharp
public string MNN.Samples.SiriAI.Harnesses.HarnessTask.HarnessId { get; internal set; }
```

```csharp
public string MNN.Samples.SiriAI.Harnesses.HarnessTask.Id { get; internal set; }
```

```csharp
public MNN.Samples.SiriAI.Harnesses.HarnessInputForm MNN.Samples.SiriAI.Harnesses.HarnessTask.InputForm { get; internal set; }
```

```csharp
public MNN.Samples.SiriAI.Harnesses.HarnessInputPrompt MNN.Samples.SiriAI.Harnesses.HarnessTask.InputPrompt { get; internal set; }
```

```csharp
public MNN.Samples.SiriAI.Harnesses.HarnessInstruction[] MNN.Samples.SiriAI.Harnesses.HarnessTask.Instructions { get; internal set; }
```

```csharp
public bool MNN.Samples.SiriAI.Harnesses.HarnessTask.IsTerminal { get; }
```

```csharp
public bool MNN.Samples.SiriAI.Harnesses.HarnessTask.IsolatedContext { get; internal set; }
```

```csharp
public string MNN.Samples.SiriAI.Harnesses.HarnessTask.LeaseId { get; internal set; }
```

```csharp
public long MNN.Samples.SiriAI.Harnesses.HarnessTask.LeaseUntilUtcTicks { get; internal set; }
```

```csharp
public MNN.Samples.SiriAI.Harnesses.HarnessNativeToolState MNN.Samples.SiriAI.Harnesses.HarnessTask.NativeTools { get; internal set; }
```

```csharp
public string MNN.Samples.SiriAI.Harnesses.HarnessTask.Objective { get; internal set; }
```

```csharp
public string MNN.Samples.SiriAI.Harnesses.HarnessTask.OwnerId { get; internal set; }
```

```csharp
public bool MNN.Samples.SiriAI.Harnesses.HarnessTask.PlaybackCompleted { get; internal set; }
```

```csharp
public bool MNN.Samples.SiriAI.Harnesses.HarnessTask.PlaybackInterrupted { get; internal set; }
```

```csharp
public bool MNN.Samples.SiriAI.Harnesses.HarnessTask.PlaybackStarted { get; internal set; }
```

```csharp
public string MNN.Samples.SiriAI.Harnesses.HarnessTask.RequestId { get; internal set; }
```

```csharp
public string MNN.Samples.SiriAI.Harnesses.HarnessTask.Result { get; internal set; }
```

```csharp
public string MNN.Samples.SiriAI.Harnesses.HarnessTask.ResultCollectedBy { get; internal set; }
```

```csharp
public string MNN.Samples.SiriAI.Harnesses.HarnessTask.ResultConversationId { get; internal set; }
```

```csharp
public bool MNN.Samples.SiriAI.Harnesses.HarnessTask.ResultPending { get; internal set; }
```

```csharp
public long MNN.Samples.SiriAI.Harnesses.HarnessTask.Revision { get; internal set; }
```

```csharp
public MNN.Samples.SiriAI.Harnesses.HarnessSessionConfiguration MNN.Samples.SiriAI.Harnesses.HarnessTask.SessionConfiguration { get; internal set; }
```

```csharp
public MNN.Samples.SiriAI.Harnesses.HarnessTaskState MNN.Samples.SiriAI.Harnesses.HarnessTask.State { get; internal set; }
```

```csharp
public string MNN.Samples.SiriAI.Harnesses.HarnessTask.SubmissionKey { get; internal set; }
```

```csharp
public string MNN.Samples.SiriAI.Harnesses.HarnessTask.Title { get; internal set; }
```

```csharp
public long MNN.Samples.SiriAI.Harnesses.HarnessTask.UpdatedUtcTicks { get; internal set; }
```

```csharp
public MNN.Samples.SiriAI.Harnesses.HarnessWaitingFor MNN.Samples.SiriAI.Harnesses.HarnessTask.WaitingFor { get; internal set; }
```

```csharp
public string MNN.Samples.SiriAI.Harnesses.HarnessTask.WorkspaceKey { get; internal set; }
```

```csharp
public long MNN.Samples.SiriAI.Harnesses.HarnessTaskDashboard.Generation { get; private set; }
```

```csharp
public MNN.Samples.SiriAI.Harnesses.HarnessTask[] MNN.Samples.SiriAI.Harnesses.HarnessTaskDashboard.Tasks { get; }
```

```csharp
public MNN.Samples.SiriAI.Harnesses.HarnessContentPart[] MNN.Samples.SiriAI.Harnesses.HarnessTaskDraft.Attachments { get; }
```

```csharp
public string MNN.Samples.SiriAI.Harnesses.HarnessTaskDraft.Objective { get; set; }
```

```csharp
public long MNN.Samples.SiriAI.Harnesses.HarnessTaskDraft.Revision { get; private set; }
```

```csharp
public long MNN.Samples.SiriAI.Harnesses.HarnessTaskDraft.Scope { get; private set; }
```

```csharp
public MNN.Samples.SiriAI.Harnesses.HarnessContentPart[] MNN.Samples.SiriAI.Harnesses.HarnessTaskDraftSnapshot.Attachments { get; }
```

```csharp
public string MNN.Samples.SiriAI.Harnesses.HarnessTaskDraftSnapshot.Objective { get; }
```

```csharp
public long MNN.Samples.SiriAI.Harnesses.HarnessTaskDraftSnapshot.Revision { get; }
```

```csharp
public long MNN.Samples.SiriAI.Harnesses.HarnessTaskDraftSnapshot.Scope { get; }
```

```csharp
public bool MNN.Samples.SiriAI.Harnesses.HarnessTaskTool.CanUseNetwork { get; }
```

```csharp
public string MNN.Samples.SiriAI.Harnesses.HarnessTaskTool.Description { get; }
```

```csharp
public string MNN.Samples.SiriAI.Harnesses.HarnessTaskTool.Name { get; }
```

```csharp
public bool MNN.Samples.SiriAI.Harnesses.HarnessTaskTool.RequiresNetwork { get; }
```

```csharp
MNN.Samples.SiriAI.Harnesses.HarnessCapabilities MNN.Samples.SiriAI.Harnesses.IHarnessAdapter.Capabilities { get; }
```

```csharp
string MNN.Samples.SiriAI.Harnesses.IHarnessAdapter.Id { get; }
```

```csharp
string MNN.Samples.SiriAI.Harnesses.IHarnessCancellationOutcomeAdapter.CancellationScope { get; }
```

```csharp
bool MNN.Samples.SiriAI.Harnesses.IHarnessResultPresenter.CanPresent { get; }
```

```csharp
bool MNN.Samples.SiriAI.Harnesses.IHarnessResultPresenter.UsesSpeech { get; }
```

```csharp
System.Threading.Tasks.Task MNN.Samples.SiriAI.Harnesses.IHarnessResultReply.Completion { get; }
```

```csharp
bool MNN.Samples.SiriAI.Harnesses.IHarnessResultReply.ContextInjected { get; }
```

```csharp
bool MNN.Samples.SiriAI.Harnesses.IHarnessResultReply.Interrupted { get; }
```

```csharp
bool MNN.Samples.SiriAI.Harnesses.IHarnessResultReply.PlaybackCompleted { get; }
```

```csharp
bool MNN.Samples.SiriAI.Harnesses.IHarnessResultReply.PlaybackStarted { get; }
```

```csharp
bool MNN.Samples.SiriAI.Harnesses.IHarnessResultReply.Succeeded { get; }
```

```csharp
bool MNN.Samples.SiriAI.Harnesses.IHarnessResultReply.TextDisplayed { get; }
```

```csharp
string MNN.Samples.SiriAI.Harnesses.ILocalCancellationHarnessAdapter.CancellationScope { get; }
```

```csharp
public bool MNN.Samples.SiriAI.Harnesses.MemoryTaskStore.FailWrites { get; set; }
```

```csharp
public string MNN.Samples.SiriAI.Harnesses.PendingSubmissionException.SubmissionKey { get; }
```

```csharp
public System.Exception MNN.Samples.SiriAI.Harnesses.PersistentTaskRuntime.Fault { get; }
```

```csharp
public int MNN.Samples.SiriAI.Harnesses.SubmissionJournal.PendingCount { get; }
```

```csharp
public bool MNN.Samples.SiriAI.Harnesses.TaskHostRequestException.Retryable { get; }
```

```csharp
public int MNN.Samples.SiriAI.Harnesses.TaskHostRequestException.StatusCode { get; }
```

```csharp
public MNN.Samples.SiriAI.Harnesses.HarnessContentPart[] MNN.Samples.SiriAI.Harnesses.TaskSubmission.Attachments { get; }
```

```csharp
public string MNN.Samples.SiriAI.Harnesses.TaskSubmission.ConversationId { get; }
```

```csharp
public string MNN.Samples.SiriAI.Harnesses.TaskSubmission.HarnessId { get; }
```

```csharp
public bool MNN.Samples.SiriAI.Harnesses.TaskSubmission.IsolatedContext { get; }
```

```csharp
public string MNN.Samples.SiriAI.Harnesses.TaskSubmission.Objective { get; }
```

```csharp
public string MNN.Samples.SiriAI.Harnesses.TaskSubmission.OwnerId { get; }
```

```csharp
public string MNN.Samples.SiriAI.Harnesses.TaskSubmission.ResultConversationId { get; }
```

```csharp
public string MNN.Samples.SiriAI.Harnesses.TaskSubmission.SubmissionKey { get; }
```

```csharp
public string MNN.Samples.SiriAI.Harnesses.TaskSubmission.Title { get; }
```

```csharp
public string MNN.Samples.SiriAI.Harnesses.TaskSubmission.WorkspaceKey { get; }
```

```csharp
bool MNN.Unity.IMNNAgentNetworkPermission.CanUseNetwork { get; }
```

```csharp
string MNN.Unity.IMNNAgentTool.Description { get; }
```

```csharp
string MNN.Unity.IMNNAgentTool.Name { get; }
```

```csharp
bool MNN.Unity.IMNNAgentTool.RequiresNetwork { get; }
```

```csharp
bool MNN.Unity.IMNNConversationContext.AllowUnverifiedBargeIn { get; }
```

```csharp
bool MNN.Unity.IMNNConversationContext.CanInterruptSpeech { get; }
```

```csharp
MNN.Unity.IMNNAudioCaptureSource MNN.Unity.IMNNConversationContext.Capture { get; }
```

```csharp
long MNN.Unity.IMNNConversationContext.CaptureRevision { get; }
```

```csharp
System.Func<System.Threading.Tasks.Task> MNN.Unity.IMNNConversationContext.FlushInputAsync { get; set; }
```

```csharp
MNN.Unity.IMNNConversationInput MNN.Unity.IMNNConversationContext.Input { get; }
```

```csharp
System.Func<long> MNN.Unity.IMNNConversationContext.LostInputFrames { get; set; }
```

```csharp
System.TimeSpan MNN.Unity.IMNNConversationContext.MaximumCaptureAge { get; }
```

```csharp
int MNN.Unity.IMNNConversationContext.RealtimeInactivitySeconds { set; }
```

```csharp
bool MNN.Unity.IMNNConversationContext.RequiresUtteranceSpeakerSelection { get; }
```

```csharp
MNN.Unity.MNNRealtimeSession MNN.Unity.IMNNConversationContext.Session { get; }
```

```csharp
bool MNN.Unity.IMNNConversationContext.SpeakerAudible { get; }
```

```csharp
bool MNN.Unity.IMNNConversationInput.Realtime { get; }
```

```csharp
int MNN.Unity.IMNNConversationInput.RealtimeRevision { get; }
```

```csharp
bool MNN.Unity.IMNNConversationInput.Recording { get; }
```

```csharp
int MNN.Unity.IMNNConversationInput.Revision { get; }
```

```csharp
MNN.Unity.MNNRealtimeCapabilities MNN.Unity.IMNNConversationProvider.Capabilities { get; }
```

```csharp
string MNN.Unity.IMNNConversationProviderDiagnostics.RuntimeBackend { get; }
```

```csharp
System.Collections.Generic.IReadOnlyList<string> MNN.Unity.IMNNConversationSpeechLanguages.SupportedSpeechLanguages { get; }
```

```csharp
int MNN.Unity.IMNNConversationTranscriptSource.Count { get; }
```

```csharp
MNN.Unity.MNNConversationTranscriptEntry MNN.Unity.IMNNConversationTranscriptSource.this[int index] { get; }
```

```csharp
bool MNN.Unity.IMNNConversationTranscriptTurnProvider.RequiresTranscript { get; }
```

```csharp
MNN.Unity.MNNConversationSpeechTimings MNN.Unity.IMNNConversationTtsDiagnostics.LastStageTimings { get; }
```

```csharp
int MNN.Unity.IMNNConversationTtsProvider.SampleRate { get; }
```

```csharp
public string MNN.Unity.MNNAgentPlan.Input { get; }
```

```csharp
public string MNN.Unity.MNNAgentPlan.Message { get; }
```

```csharp
public string MNN.Unity.MNNAgentPlan.Route { get; }
```

```csharp
public string MNN.Unity.MNNAgentPlan.Tool { get; }
```

```csharp
public MNN.Unity.MNNRealtimeCapabilities MNN.Unity.MNNAgentProvider.Capabilities { get; }
```

```csharp
public string MNN.Unity.MNNAgentProvider.RuntimeBackend { get; }
```

```csharp
public string MNN.Unity.MNNArithmeticTool.Description { get; }
```

```csharp
public string MNN.Unity.MNNArithmeticTool.Name { get; }
```

```csharp
public bool MNN.Unity.MNNArithmeticTool.RequiresNetwork { get; }
```

```csharp
public string MNN.Unity.MNNChatMessage.Content { get; }
```

```csharp
public MNN.Unity.MNNChatRole MNN.Unity.MNNChatMessage.Role { get; }
```

```csharp
public MNN.Unity.MNNRealtimeCapabilities MNN.Unity.MNNCloudSpeechProvider.Capabilities { get; }
```

```csharp
public int MNN.Unity.MNNCloudSpeechProvider.SampleRate { get; }
```

```csharp
public System.Collections.Generic.IReadOnlyList<string> MNN.Unity.MNNCloudSpeechProvider.SupportedSpeechLanguages { get; }
```

```csharp
public string MNN.Unity.MNNContextRequestTool.Description { get; }
```

```csharp
public string MNN.Unity.MNNContextRequestTool.Name { get; }
```

```csharp
public bool MNN.Unity.MNNContextRequestTool.RequiresNetwork { get; }
```

```csharp
public MNN.Unity.MNNConversationCommands MNN.Unity.MNNConversationApplication.Commands { get; private set; }
```

```csharp
public MNN.Unity.MNNRealtimeDeviceSession MNN.Unity.MNNConversationApplication.Devices { get; }
```

```csharp
public MNN.Unity.MNNConversationRuntimeDiagnostics MNN.Unity.MNNConversationApplication.Diagnostics { get; }
```

```csharp
public System.Exception MNN.Unity.MNNConversationApplication.Fault { get; private set; }
```

```csharp
public bool MNN.Unity.MNNConversationApplication.IsClosed { get; private set; }
```

```csharp
public bool MNN.Unity.MNNConversationApplication.IsReady { get; }
```

```csharp
public MNN.Unity.MNNConversationPorts MNN.Unity.MNNConversationApplication.Ports { get; }
```

```csharp
public string MNN.Unity.MNNConversationApplication.RecognitionPreview { get; private set; }
```

```csharp
public System.Threading.Tasks.Task MNN.Unity.MNNConversationApplication.Shutdown { get; }
```

```csharp
public MNN.Unity.MNNConversationStage MNN.Unity.MNNConversationApplication.Stage { get; private set; }
```

```csharp
public MNN.Unity.MNNConversationTelemetry MNN.Unity.MNNConversationApplication.Telemetry { get; }
```

```csharp
public MNN.Unity.MNNConversationTranscriptStore MNN.Unity.MNNConversationApplication.Transcript { get; }
```

```csharp
public bool MNN.Unity.MNNConversationApplication.TranscriptAvailable { get; }
```

```csharp
public string MNN.Unity.MNNConversationApplication.Warning { get; private set; }
```

```csharp
public string MNN.Unity.MNNConversationCatalog.ActiveId { get; }
```

```csharp
public string MNN.Unity.MNNConversationCatalog.ActiveTitle { get; }
```

```csharp
public System.Collections.Generic.IReadOnlyList<MNN.Unity.MNNConversationInfo> MNN.Unity.MNNConversationCatalog.Conversations { get; }
```

```csharp
public string MNN.Unity.MNNConversationCatalog.Draft { get; }
```

```csharp
public MNN.Unity.MNNConversationTranscriptStore MNN.Unity.MNNConversationCatalog.History { get; private set; }
```

```csharp
public MNN.Unity.MNNConversationViewState MNN.Unity.MNNConversationCatalog.ViewState { get; }
```

```csharp
public System.Action MNN.Unity.MNNConversationCommandBindings.ActivateRealtime { get; set; }
```

```csharp
public System.Func<bool> MNN.Unity.MNNConversationCommandBindings.Active { get; set; }
```

```csharp
public System.Action<System.Exception> MNN.Unity.MNNConversationCommandBindings.BackgroundFailure { get; set; }
```

```csharp
public System.Func<bool> MNN.Unity.MNNConversationCommandBindings.CanInterrupt { get; set; }
```

```csharp
public System.Action<bool> MNN.Unity.MNNConversationCommandBindings.CaptureFinishing { get; set; }
```

```csharp
public System.Func<bool> MNN.Unity.MNNConversationCommandBindings.CaptureReady { get; set; }
```

```csharp
public System.Func<int> MNN.Unity.MNNConversationCommandBindings.CaptureTailMilliseconds { get; set; }
```

```csharp
public MNN.Unity.MNNConversationInputState MNN.Unity.MNNConversationCommandBindings.Input { get; }
```

```csharp
public System.Func<System.Threading.Tasks.Task> MNN.Unity.MNNConversationCommandBindings.InterruptAsync { get; set; }
```

```csharp
public System.Func<System.Threading.CancellationToken> MNN.Unity.MNNConversationCommandBindings.Lifetime { get; set; }
```

```csharp
public System.Action MNN.Unity.MNNConversationCommandBindings.PreparationCancelled { get; set; }
```

```csharp
public System.Func<System.Threading.CancellationToken, System.Threading.Tasks.Task> MNN.Unity.MNNConversationCommandBindings.PrepareRealtimeAsync { get; set; }
```

```csharp
public MNN.Unity.MNNConversationRecording MNN.Unity.MNNConversationCommandBindings.Recording { get; }
```

```csharp
public System.Func<bool> MNN.Unity.MNNConversationCommandBindings.Resetting { get; set; }
```

```csharp
public System.Func<MNN.Unity.MNNRealtimeSessionState> MNN.Unity.MNNConversationCommandBindings.SessionState { get; set; }
```

```csharp
public System.Action<bool> MNN.Unity.MNNConversationCommandBindings.SetCaptureEnabled { get; set; }
```

```csharp
public System.Func<bool> MNN.Unity.MNNConversationCommandBindings.SpeakerAudible { get; set; }
```

```csharp
public System.Func<float[], System.Threading.CancellationToken, System.Threading.Tasks.Task> MNN.Unity.MNNConversationCommandBindings.SubmitAudioAsync { get; set; }
```

```csharp
public System.Func<string, System.Threading.CancellationToken, System.Threading.Tasks.Task> MNN.Unity.MNNConversationCommandBindings.SubmitTextAsync { get; set; }
```

```csharp
public System.Func<bool> MNN.Unity.MNNConversationCommandBindings.SupportsRealtime { get; set; }
```

```csharp
public System.Func<bool> MNN.Unity.MNNConversationCommandBindings.SupportsRecordedVoice { get; set; }
```

```csharp
public System.Func<bool> MNN.Unity.MNNConversationCommandBindings.SupportsTranscription { get; set; }
```

```csharp
public System.Func<float[], System.Threading.CancellationToken, System.Threading.Tasks.Task<string>> MNN.Unity.MNNConversationCommandBindings.TranscribeAsync { get; set; }
```

```csharp
public System.Exception MNN.Unity.MNNConversationCommands.BackgroundError { get; private set; }
```

```csharp
public System.Threading.Tasks.Task MNN.Unity.MNNConversationCommands.CaptureFinished { get; }
```

```csharp
public System.Threading.Tasks.Task MNN.Unity.MNNConversationCommands.InterruptionFinished { get; }
```

```csharp
public bool MNN.Unity.MNNConversationCommands.IsBusy { get; }
```

```csharp
public bool MNN.Unity.MNNConversationCommands.IsFinishingRecording { get; }
```

```csharp
public bool MNN.Unity.MNNConversationCommands.IsPreparingRealtime { get; private set; }
```

```csharp
public bool MNN.Unity.MNNConversationCommands.IsRealtime { get; }
```

```csharp
public bool MNN.Unity.MNNConversationCommands.IsSubmitting { get; }
```

```csharp
public System.Threading.Tasks.Task MNN.Unity.MNNConversationCommands.PreparationFinished { get; }
```

```csharp
public System.Threading.Tasks.Task<bool> MNN.Unity.MNNConversationCommands.RecordingStarted { get; }
```

```csharp
public System.Threading.Tasks.Task MNN.Unity.MNNConversationCommands.SubmissionFinished { get; }
```

```csharp
public int MNN.Unity.MNNConversationContext.InputTokens { get; }
```

Exact token count after applying this model's official chat template.

```csharp
public System.Collections.Generic.IReadOnlyList<MNN.Unity.MNNChatMessage> MNN.Unity.MNNConversationContext.Messages { get; }
```

```csharp
public int MNN.Unity.MNNConversationContext.RemovedMessages { get; }
```

```csharp
public string MNN.Unity.MNNConversationEvent.Id { get; }
```

```csharp
public string MNN.Unity.MNNConversationEvent.Text { get; }
```

```csharp
public System.Threading.Tasks.Task MNN.Unity.MNNConversationEventReply.Completion { get; internal set; }
```

```csharp
public bool MNN.Unity.MNNConversationEventReply.ContextInjected { get; }
```

```csharp
public MNN.Unity.MNNConversationEvent MNN.Unity.MNNConversationEventReply.Input { get; }
```

```csharp
public bool MNN.Unity.MNNConversationEventReply.Interrupted { get; }
```

```csharp
public bool MNN.Unity.MNNConversationEventReply.PlaybackCompleted { get; }
```

```csharp
public bool MNN.Unity.MNNConversationEventReply.PlaybackStarted { get; }
```

```csharp
public MNN.Unity.MNNResponseId MNN.Unity.MNNConversationEventReply.Response { get; }
```

```csharp
public bool MNN.Unity.MNNConversationEventReply.Succeeded { get; }
```

```csharp
public bool MNN.Unity.MNNConversationEventReply.TextDisplayed { get; }
```

```csharp
public bool MNN.Unity.MNNConversationInfo.Archived { get; }
```

```csharp
public string MNN.Unity.MNNConversationInfo.Draft { get; }
```

```csharp
public string MNN.Unity.MNNConversationInfo.Id { get; }
```

```csharp
public bool MNN.Unity.MNNConversationInfo.NeedsGeneratedTitle { get; }
```

```csharp
public string MNN.Unity.MNNConversationInfo.Title { get; }
```

```csharp
public long MNN.Unity.MNNConversationInfo.UpdatedUtcTicks { get; }
```

```csharp
public float MNN.Unity.MNNConversationInputState.Duration { get; }
```

```csharp
public float MNN.Unity.MNNConversationInputState.Level { get; }
```

```csharp
public bool MNN.Unity.MNNConversationInputState.Realtime { get; }
```

```csharp
public int MNN.Unity.MNNConversationInputState.RealtimeRevision { get; }
```

```csharp
public bool MNN.Unity.MNNConversationInputState.Recording { get; }
```

```csharp
public int MNN.Unity.MNNConversationInputState.RecordingAllocatedSamples { get; }
```

Reserved recording PCM capacity; excludes the final returned snapshot.

```csharp
public int MNN.Unity.MNNConversationInputState.Revision { get; }
```

```csharp
public bool MNN.Unity.MNNConversationInputState.Speaking { get; }
```

```csharp
public MNN.Unity.MNNAudioFeatures MNN.Unity.MNNConversationInputState.VoiceFeatures { get; }
```

```csharp
public int MNN.Unity.MNNConversationLedger.Count { get; }
```

```csharp
public long MNN.Unity.MNNConversationLedger.PresentedThroughFrame { get; }
```

```csharp
public string MNN.Unity.MNNConversationMemory.LastAssistantText { get; }
```

```csharp
public string MNN.Unity.MNNConversationMemory.LastUserText { get; }
```

```csharp
public static MNN.Unity.MNNRealtimeCapabilities MNN.Unity.MNNConversationMnnProviders.EnergyCapabilities { get; }
```

```csharp
public bool MNN.Unity.MNNConversationNotification.Assistant { get; }
```

```csharp
public MNN.Unity.MNNResponseId MNN.Unity.MNNConversationNotification.Response { get; }
```

```csharp
public MNN.Unity.MNNConversationStage MNN.Unity.MNNConversationNotification.Stage { get; }
```

```csharp
public string MNN.Unity.MNNConversationNotification.Text { get; }
```

```csharp
public bool MNN.Unity.MNNConversationPorts.AllowUnverifiedBargeIn { get; }
```

```csharp
public bool MNN.Unity.MNNConversationPorts.CanInterruptSpeech { get; set; }
```

```csharp
public MNN.Unity.IMNNAudioCaptureSource MNN.Unity.MNNConversationPorts.Capture { get; }
```

```csharp
public System.Action<bool> MNN.Unity.MNNConversationPorts.CaptureGate { get; set; }
```

```csharp
public System.Func<bool> MNN.Unity.MNNConversationPorts.CaptureReady { get; set; }
```

```csharp
public long MNN.Unity.MNNConversationPorts.CaptureRevision { get; }
```

```csharp
public System.Func<System.Threading.Tasks.Task> MNN.Unity.MNNConversationPorts.FlushInputAsync { get; set; }
```

```csharp
public MNN.Unity.IMNNConversationInput MNN.Unity.MNNConversationPorts.Input { get; }
```

```csharp
public MNN.Unity.MNNConversationInputState MNN.Unity.MNNConversationPorts.InputState { get; }
```

```csharp
public System.Func<long> MNN.Unity.MNNConversationPorts.LostInputFrames { get; set; }
```

```csharp
public MNN.Unity.MNNConversationMailbox MNN.Unity.MNNConversationPorts.Mailbox { get; }
```

```csharp
public System.TimeSpan MNN.Unity.MNNConversationPorts.MaximumCaptureAge { get; set; }
```

```csharp
public System.Func<System.Threading.CancellationToken, System.Threading.Tasks.Task> MNN.Unity.MNNConversationPorts.PauseCaptureAsync { get; set; }
```

```csharp
public bool MNN.Unity.MNNConversationPorts.Realtime { get; }
```

```csharp
public int MNN.Unity.MNNConversationPorts.RealtimeInactivitySeconds { get; set; }
```

```csharp
public int MNN.Unity.MNNConversationPorts.RealtimeRevision { get; }
```

```csharp
public bool MNN.Unity.MNNConversationPorts.Recording { get; }
```

```csharp
public MNN.Unity.MNNConversationRecording MNN.Unity.MNNConversationPorts.RecordingControl { get; }
```

```csharp
public bool MNN.Unity.MNNConversationPorts.RequiresUtteranceSpeakerSelection { get; }
```

```csharp
public int MNN.Unity.MNNConversationPorts.Revision { get; }
```

```csharp
public MNN.Unity.MNNRealtimeSession MNN.Unity.MNNConversationPorts.Session { get; }
```

```csharp
public bool MNN.Unity.MNNConversationPorts.SpeakerAudible { get; }
```

```csharp
public int MNN.Unity.MNNConversationProviderCleanup.PendingProviderCount { get; }
```

```csharp
public long MNN.Unity.MNNConversationRecognitionWorker.DroppedFrames { get; }
```

```csharp
public System.Exception MNN.Unity.MNNConversationRecognitionWorker.Fault { get; }
```

```csharp
public System.Func<System.Threading.Tasks.Task> MNN.Unity.MNNConversationRecording.FlushInputAsync { get; set; }
```

```csharp
public System.Func<long> MNN.Unity.MNNConversationRecording.LostInputFrames { get; set; }
```

```csharp
public System.Func<System.Threading.CancellationToken, System.Threading.Tasks.Task> MNN.Unity.MNNConversationRecording.PauseCaptureAsync { get; set; }
```

```csharp
public System.Threading.Tasks.Task MNN.Unity.MNNConversationRecording.PendingDrain { get; }
```

```csharp
public System.Action<bool> MNN.Unity.MNNConversationRecording.SetCaptureEnabled { get; set; }
```

```csharp
public float MNN.Unity.MNNConversationRowLayout.BodyTop { get; }
```

```csharp
public bool MNN.Unity.MNNConversationRowLayout.CanDisclose { get; }
```

```csharp
public bool MNN.Unity.MNNConversationRowLayout.DrawBubble { get; }
```

```csharp
public float MNN.Unity.MNNConversationRowLayout.HorizontalInset { get; }
```

```csharp
public float MNN.Unity.MNNConversationRowLayout.Left { get; }
```

```csharp
public float MNN.Unity.MNNConversationRowLayout.Width { get; }
```

```csharp
protected bool MNN.Unity.MNNConversationRuntime.HalfDuplex { get; }
```

```csharp
public bool MNN.Unity.MNNConversationRuntime.HasAsr { get; }
```

```csharp
public string MNN.Unity.MNNConversationRuntime.InputDiagnostics { get; }
```

```csharp
public bool MNN.Unity.MNNConversationRuntime.IsResponding { get; }
```

```csharp
public string MNN.Unity.MNNConversationRuntime.LastVoiceRejection { get; }
```

```csharp
public MNN.Unity.MNNConversationMemory MNN.Unity.MNNConversationRuntime.Memory { get; }
```

```csharp
public long MNN.Unity.MNNConversationRuntime.RejectedVoiceInputs { get; }
```

```csharp
public bool MNN.Unity.MNNConversationRuntime.SupportsTextAssistance { get; }
```

```csharp
public MNN.Unity.MNNRealtimeSynthesisMeter MNN.Unity.MNNConversationRuntime.SynthesisMeter { get; }
```

```csharp
public MNN.Unity.MNNConversationTelemetry MNN.Unity.MNNConversationRuntime.TurnTelemetry { get; }
```

```csharp
public MNN.Unity.MNNConversationRuntime MNN.Unity.MNNConversationRuntimeFactory.Active { get; private set; }
```

```csharp
public MNN.Unity.MNNConversationRuntimeDiagnostics MNN.Unity.MNNConversationRuntimeFactory.Diagnostics { get; }
```

```csharp
public MNN.Unity.MNNConversationTelemetry MNN.Unity.MNNConversationRuntimeFactory.TurnTelemetry { get; }
```

```csharp
public bool MNN.Unity.MNNConversationSegment.IsEstimatedPresentation { get; }
```

```csharp
public bool MNN.Unity.MNNConversationSegment.IsInterrupted { get; }
```

```csharp
public bool MNN.Unity.MNNConversationSegment.IsPresented { get; }
```

```csharp
public MNN.Unity.MNNResponseId MNN.Unity.MNNConversationSegment.Response { get; }
```

```csharp
public long MNN.Unity.MNNConversationSegment.SegmentId { get; }
```

```csharp
public long MNN.Unity.MNNConversationSegment.SubmittedFrames { get; }
```

```csharp
public string MNN.Unity.MNNConversationSegment.Text { get; }
```

```csharp
public bool MNN.Unity.MNNConversationStagePlan.Asr { get; private set; }
```

```csharp
public bool MNN.Unity.MNNConversationStagePlan.Tts { get; private set; }
```

```csharp
public bool MNN.Unity.MNNConversationStagePlan.Turn { get; private set; }
```

```csharp
public bool MNN.Unity.MNNConversationStagePlan.TurnInput { get; private set; }
```

```csharp
public bool MNN.Unity.MNNConversationStagePlan.Vad { get; private set; }
```

```csharp
public static MNN.Unity.MNNConversationStageRegistration MNN.Unity.MNNConversationStageRegistration.Energy { get; }
```

```csharp
public System.Action MNN.Unity.MNNConversationStageRegistration.Prepared { get; set; }
```

```csharp
public string MNN.Unity.MNNConversationTelemetry.Bottleneck { get; }
```

```csharp
public long MNN.Unity.MNNConversationTelemetry.CompletedCount { get; }
```

Lifetime count of distinct completed traces, independent of the bounded timing history.

```csharp
public MNN.Unity.MNNConversationTelemetry.MNNConversationTiming MNN.Unity.MNNConversationTelemetry.Latest { get; }
```

```csharp
public string MNN.Unity.MNNConversationTelemetry.Summary { get; }
```

```csharp
public int MNN.Unity.MNNConversationTimelinePresentation.Count { get; }
```

```csharp
public MNN.Unity.MNNConversationTranscriptEntry MNN.Unity.MNNConversationTimelinePresentation.this[int index] { get; }
```

```csharp
public string MNN.Unity.MNNConversationTranscriptEntry.Id { get; }
```

```csharp
public MNN.Unity.MNNConversationEntryKind MNN.Unity.MNNConversationTranscriptEntry.Kind { get; }
```

```csharp
public string MNN.Unity.MNNConversationTranscriptEntry.ParentId { get; }
```

```csharp
public MNN.Unity.MNNChatRole MNN.Unity.MNNConversationTranscriptEntry.Role { get; }
```

```csharp
public MNN.Unity.MNNConversationEntryState MNN.Unity.MNNConversationTranscriptEntry.State { get; }
```

```csharp
public string MNN.Unity.MNNConversationTranscriptEntry.Text { get; }
```

```csharp
public string MNN.Unity.MNNConversationTranscriptEntry.Title { get; }
```

```csharp
public int MNN.Unity.MNNConversationTranscriptStore.Count { get; private set; }
```

```csharp
public MNN.Unity.MNNConversationTranscriptEntry MNN.Unity.MNNConversationTranscriptStore.this[int index] { get; }
```

```csharp
public long MNN.Unity.MNNConversationTranscriptStore.RecoveredBytes { get; private set; }
```

Bytes discarded from an invalid/incomplete journal suffix during opening.

```csharp
public bool MNN.Unity.MNNConversationTranscriptStore.RecoveryIndexUsed { get; private set; }
```

True when a matching disposable index avoided decoding sealed history on opening. Sealed records still validate their journal checksum when read. Missing, stale or damaged indexes fall back to journal recovery; the journal remains authoritative.

```csharp
public int MNN.Unity.MNNConversationTranscriptStore.ResidentCount { get; }
```

```csharp
public int MNN.Unity.MNNConversationViewState.CharacterOffset { get; }
```

```csharp
public System.Collections.Generic.IReadOnlyList<int> MNN.Unity.MNNConversationViewState.ExpandedEntries { get; }
```

```csharp
public int MNN.Unity.MNNConversationViewState.FirstEntry { get; }
```

```csharp
public bool MNN.Unity.MNNConversationViewState.FollowingLatest { get; }
```

```csharp
public static MNN.Unity.MNNConversationViewState MNN.Unity.MNNConversationViewState.Latest { get; }
```

```csharp
public int MNN.Unity.MNNConversationViewState.PageStart { get; }
```

```csharp
public float MNN.Unity.MNNConversationViewState.ScrollOffset { get; }
```

```csharp
public string MNN.Unity.MNNDecisionOption.Description { get; }
```

```csharp
public string MNN.Unity.MNNDecisionOption.Label { get; }
```

```csharp
public string MNN.Unity.MNNDecisionQuestion.Instructions { get; }
```

```csharp
public MNN.Unity.MNNDecisionKind MNN.Unity.MNNDecisionQuestion.Kind { get; }
```

```csharp
public System.Collections.Generic.IReadOnlyList<MNN.Unity.MNNDecisionOption> MNN.Unity.MNNDecisionQuestion.Options { get; }
```

```csharp
public double MNN.Unity.MNNDecisionResult.ActProbability { get; }
```

```csharp
public int MNN.Unity.MNNDecisionResult.BestIndex { get; }
```

```csharp
public string MNN.Unity.MNNDecisionResult.Choice { get; }
```

```csharp
public double MNN.Unity.MNNDecisionResult.Confidence { get; }
```

```csharp
public double? MNN.Unity.MNNDecisionResult.ExpectedScore { get; }
```

```csharp
public int MNN.Unity.MNNDecisionResult.InputTokens { get; }
```

```csharp
public MNN.Unity.MNNDecisionKind MNN.Unity.MNNDecisionResult.Kind { get; }
```

```csharp
public System.Collections.Generic.IReadOnlyList<string> MNN.Unity.MNNDecisionResult.Labels { get; }
```

```csharp
public System.Collections.Generic.IReadOnlyList<double> MNN.Unity.MNNDecisionResult.Probabilities { get; }
```

```csharp
public bool MNN.Unity.MNNDecisionResult.QuestionTruncated { get; }
```

```csharp
public bool MNN.Unity.MNNDecisionResult.StateTruncated { get; }
```

```csharp
public double MNN.Unity.MNNDecisionResult.Temperature { get; }
```

```csharp
public double? MNN.Unity.MNNDecisionResult.TrueProbability { get; }
```

```csharp
public int MNN.Unity.MNNGenerationDelta.GeneratedTokens { get; }
```

Total generated tokens so far, including tokens that emitted no text.

```csharp
public string MNN.Unity.MNNGenerationDelta.Text { get; }
```

New text since the previous callback. Append it once to the reply.

```csharp
public int MNN.Unity.MNNGenerationUpdate.GeneratedTokens { get; }
```

```csharp
public string MNN.Unity.MNNGenerationUpdate.Text { get; }
```

```csharp
public MNN.Unity.MNNBackendType MNN.Unity.MNNLlm.Backend { get; }
```

```csharp
public MNN.Unity.MNNModelCapabilities MNN.Unity.MNNLlm.Capabilities { get; }
```

```csharp
public bool MNN.Unity.MNNLlm.EnableConversationCache { get; set; }
```

Reuse an exact token prefix between text conversations on this instance. Incompatible templates, changed history, media, cancellation and errors start cold. The pinned macOS x64 runtime currently uses full prefill after a continuation quality failure. Quantized attention configurations use full prefill after INT8 continuation quality failures. Disable to compare against full prefill. Uses the same pinned compiler ABI as generation.

```csharp
public MNN.Unity.MNNBackendType MNN.Unity.MNNLlm.MediaBackend { get; }
```

Configured image/audio processor runtime.

```csharp
public string MNN.Unity.MNNLlmCache.DirectoryPath { get; }
```

```csharp
public MNN.Unity.MNNBackendType MNN.Unity.MNNLlmPerformanceOptions.BackendType { get; set; }
```

```csharp
public MNN.Unity.MNNLlmDynamicQuantization? MNN.Unity.MNNLlmPerformanceOptions.DynamicQuantization { get; set; }
```

Null preserves the model's dynamic quantization mode.

```csharp
public bool MNN.Unity.MNNLlmPerformanceOptions.EnableWeightReordering { get; set; }
```

Enable official dynamic_option bit 3 (WEIGHT_ONLINE_REORDER). Its benefit and memory cost depend on the selected CPU kernel and model. Unassigned inherits the model setting; an explicit false clears only this bit.

```csharp
public bool? MNN.Unity.MNNLlmPerformanceOptions.FlashAttention { get; set; }
```

Null preserves attention_mode (or legacy quant_qkv). Changes only the FlashAttention bit, retaining the model's KV quantization mode. Support depends on the native backend and model.

```csharp
public MNN.Unity.MNNPowerMode MNN.Unity.MNNLlmPerformanceOptions.PowerMode { get; set; }
```

```csharp
public MNN.Unity.MNNPrecisionMode MNN.Unity.MNNLlmPerformanceOptions.PrecisionMode { get; set; }
```

```csharp
public int? MNN.Unity.MNNLlmPerformanceOptions.PrefillChunkSize { get; set; }
```

Null inherits chunk; zero disables chunking. Positive values bound prefill chunks. Models with chunk_limits require their exported shape contract.

```csharp
public int MNN.Unity.MNNLlmPerformanceOptions.ThreadCount { get; set; }
```

Unassigned: select at load. The unresolved getter returns the fallback value.

```csharp
public long MNN.Unity.MNNLlmResult.DecodeMicroseconds { get; }
```

```csharp
public int MNN.Unity.MNNLlmResult.GeneratedTokens { get; }
```

```csharp
public long MNN.Unity.MNNLlmResult.LoadMicroseconds { get; }
```

```csharp
public long MNN.Unity.MNNLlmResult.PrefillMicroseconds { get; }
```

```csharp
public int MNN.Unity.MNNLlmResult.PromptTokens { get; }
```

Templated input token count reported by official LlmContext.

```csharp
public bool MNN.Unity.MNNLlmResult.ReachedTokenLimit { get; }
```

```csharp
public long MNN.Unity.MNNLlmResult.SampleMicroseconds { get; }
```

Native sampling time; separate from the model's decode time.

```csharp
public string MNN.Unity.MNNLlmResult.Text { get; }
```

```csharp
public string MNN.Unity.MNNLocalKnowledgeTool.Description { get; }
```

```csharp
public string MNN.Unity.MNNLocalKnowledgeTool.Name { get; }
```

```csharp
public bool MNN.Unity.MNNLocalKnowledgeTool.RequiresNetwork { get; }
```

```csharp
public int MNN.Unity.MNNMarkdownBlock.HeadingLevel { get; }
```

```csharp
public string MNN.Unity.MNNMarkdownBlock.ImageAlt { get; }
```

```csharp
public string MNN.Unity.MNNMarkdownBlock.ImageUrl { get; }
```

```csharp
public System.Collections.Generic.IReadOnlyList<MNN.Unity.MNNMarkdownListItem> MNN.Unity.MNNMarkdownBlock.Items { get; }
```

```csharp
public string MNN.Unity.MNNMarkdownBlock.Language { get; }
```

```csharp
public int MNN.Unity.MNNMarkdownBlock.OrderedStart { get; }
```

```csharp
public string MNN.Unity.MNNMarkdownBlock.Text { get; }
```

```csharp
public MNN.Unity.MNNMarkdownBlockType MNN.Unity.MNNMarkdownBlock.Type { get; }
```

```csharp
public System.Collections.Generic.IReadOnlyList<MNN.Unity.MNNMarkdownBlock> MNN.Unity.MNNMarkdownDocument.Blocks { get; }
```

```csharp
public int MNN.Unity.MNNMarkdownHistoryLayout.CachedRenderCharacters { get; }
```

```csharp
public int MNN.Unity.MNNMarkdownHistoryLayout.CachedRenderCount { get; }
```

```csharp
public float MNN.Unity.MNNMarkdownHistoryLayout.Slice.BodyHeight { get; internal set; }
```

```csharp
public int MNN.Unity.MNNMarkdownHistoryLayout.Slice.Entry { get; internal set; }
```

```csharp
public string MNN.Unity.MNNMarkdownHistoryLayout.Slice.EventId { get; internal set; }
```

```csharp
public bool MNN.Unity.MNNMarkdownHistoryLayout.Slice.First { get; internal set; }
```

```csharp
public float MNN.Unity.MNNMarkdownHistoryLayout.Slice.Height { get; internal set; }
```

```csharp
public MNN.Unity.MNNConversationEntryKind MNN.Unity.MNNMarkdownHistoryLayout.Slice.Kind { get; internal set; }
```

```csharp
public string MNN.Unity.MNNMarkdownHistoryLayout.Slice.ParentId { get; internal set; }
```

```csharp
public MNN.Unity.MNNMarkdownRenderResult MNN.Unity.MNNMarkdownHistoryLayout.Slice.RenderResult { get; internal set; }
```

```csharp
public MNN.Unity.MNNChatRole MNN.Unity.MNNMarkdownHistoryLayout.Slice.Role { get; internal set; }
```

```csharp
public int MNN.Unity.MNNMarkdownHistoryLayout.Slice.Start { get; internal set; }
```

```csharp
public MNN.Unity.MNNConversationEntryState MNN.Unity.MNNMarkdownHistoryLayout.Slice.State { get; internal set; }
```

```csharp
public string MNN.Unity.MNNMarkdownHistoryLayout.Slice.Text { get; internal set; }
```

```csharp
public float MNN.Unity.MNNMarkdownHistoryLayout.Slice.Top { get; internal set; }
```

```csharp
public System.Collections.Generic.IReadOnlyList<MNN.Unity.MNNMarkdownHistoryLayout.Slice> MNN.Unity.MNNMarkdownHistoryLayout.Slices { get; }
```

```csharp
public string MNN.Unity.MNNMarkdownImage.Alt { get; }
```

```csharp
public int MNN.Unity.MNNMarkdownImage.RichTextLength { get; }
```

```csharp
public int MNN.Unity.MNNMarkdownImage.RichTextStart { get; }
```

```csharp
public string MNN.Unity.MNNMarkdownImage.Url { get; }
```

```csharp
public int MNN.Unity.MNNMarkdownImageCache.ActiveLeaseCount { get; }
```

```csharp
public int MNN.Unity.MNNMarkdownImageCache.CachedCount { get; }
```

```csharp
public long MNN.Unity.MNNMarkdownImageCache.CachedEstimatedBytes { get; }
```

```csharp
public UnityEngine.Texture2D MNN.Unity.MNNMarkdownImageCache.Lease.Texture { get; }
```

```csharp
public long MNN.Unity.MNNMarkdownImageCache.ResidentEstimatedBytes { get; }
```

Estimated bytes still owned by cache or leases. Player destruction completes at Unity's frame boundary.

```csharp
public long MNN.Unity.MNNMarkdownImageRequest.EncodedBytes { get; }
```

```csharp
public MNN.Unity.MNNMarkdownImageFailure MNN.Unity.MNNMarkdownImageRequest.Failure { get; private set; }
```

```csharp
public bool MNN.Unity.MNNMarkdownImageRequest.IsDone { get; }
```

```csharp
public int MNN.Unity.MNNMarkdownListItem.Level { get; }
```

```csharp
public string MNN.Unity.MNNMarkdownListItem.Text { get; }
```

```csharp
public int MNN.Unity.MNNMarkdownRenderResult.CharacterOffset { get; }
```

UTF-16 text offset of a rendered slice, excluding generated rich-text tags.

```csharp
public MNN.Unity.MNNMarkdownDocument MNN.Unity.MNNMarkdownRenderResult.Document { get; }
```

```csharp
public int MNN.Unity.MNNMarkdownRenderResult.ImageCount { get; }
```

```csharp
public System.Collections.Generic.IReadOnlyList<MNN.Unity.MNNMarkdownImage> MNN.Unity.MNNMarkdownRenderResult.Images { get; }
```

```csharp
public string MNN.Unity.MNNMarkdownRenderResult.RichText { get; }
```

```csharp
public bool MNN.Unity.MNNMiniMindO.SupportsAudioInput { get; }
```

```csharp
public bool MNN.Unity.MNNMiniMindO.SupportsImageInput { get; }
```

```csharp
public bool MNN.Unity.MNNMiniMindO.SupportsSpeechOutput { get; }
```

```csharp
public bool MNN.Unity.MNNMiniMindOOptions.GenerateSpeech { get; set; }
```

```csharp
public int MNN.Unity.MNNMiniMindOOptions.MaxNewTokens { get; set; }
```

```csharp
public int MNN.Unity.MNNMiniMindOOptions.Seed { get; set; }
```

```csharp
public float MNN.Unity.MNNMiniMindOOptions.Temperature { get; set; }
```

Zero selects greedy text. Speech uses the released temperature 0.2/top-k 50.

```csharp
public float MNN.Unity.MNNMiniMindOOptions.TopP { get; set; }
```

```csharp
public float[] MNN.Unity.MNNMiniMindORequest.Audio16Khz { get; set; }
```

```csharp
public MNN.Unity.MNNChatMessage[] MNN.Unity.MNNMiniMindORequest.History { get; set; }
```

Previous text turns, excluding the current user turn. Historical media must be supplied again to be understood.

```csharp
public float[] MNN.Unity.MNNMiniMindORequest.ImagePixels { get; set; }
```

```csharp
public string MNN.Unity.MNNMiniMindORequest.Text { get; set; }
```

```csharp
public int[][] MNN.Unity.MNNMiniMindOResult.AudioCodes { get; }
```

```csharp
public int MNN.Unity.MNNMiniMindOResult.GeneratedTokens { get; }
```

```csharp
public int MNN.Unity.MNNMiniMindOResult.SampleRate { get; }
```

```csharp
public string MNN.Unity.MNNMiniMindOResult.Text { get; }
```

```csharp
public bool MNN.Unity.MNNMiniMindOResult.TokenLimitReached { get; }
```

True when the generation budget ended before text and all speech streams stopped.

```csharp
public float[] MNN.Unity.MNNMiniMindOResult.Waveform { get; }
```

```csharp
public int[] MNN.Unity.MNNMiniMindOUpdate.AudioFrame { get; }
```

```csharp
public int MNN.Unity.MNNMiniMindOUpdate.GeneratedTokens { get; }
```

```csharp
public string MNN.Unity.MNNMiniMindOUpdate.Text { get; }
```

```csharp
public float[] MNN.Unity.MNNMiniMindOUpdate.Waveform { get; }
```

New mono 24 kHz PCM samples, without samples emitted by earlier callbacks.

```csharp
public long MNN.Unity.MNNMultimodalResult.AudioMicroseconds { get; }
```

```csharp
public bool MNN.Unity.MNNMultimodalResult.Cancelled { get; }
```

```csharp
public MNN.Unity.MNNConversationCacheStatus MNN.Unity.MNNMultimodalResult.ConversationCacheStatus { get; }
```

```csharp
public long MNN.Unity.MNNMultimodalResult.DecodeMicroseconds { get; }
```

```csharp
public long MNN.Unity.MNNMultimodalResult.FirstTextMicroseconds { get; }
```

Generation start to the first nonempty streaming text callback. Zero when streaming produced no text; excludes async queueing and waiting for the model lock.

```csharp
public System.Collections.Generic.IReadOnlyList<int> MNN.Unity.MNNMultimodalResult.GeneratedTokenIds { get; }
```

Owned snapshot of the official LlmContext output token IDs.

```csharp
public int MNN.Unity.MNNMultimodalResult.GeneratedTokens { get; }
```

```csharp
public float MNN.Unity.MNNMultimodalResult.InputAudioSeconds { get; }
```

```csharp
public long MNN.Unity.MNNMultimodalResult.PrefillMicroseconds { get; }
```

```csharp
public long MNN.Unity.MNNMultimodalResult.ProgressCallbackMicroseconds { get; }
```

Total wall time inside the caller's synchronous progress callbacks. This time pauses native decoding and includes any downstream waits in those callbacks.

```csharp
public int MNN.Unity.MNNMultimodalResult.PromptTokens { get; }
```

```csharp
public bool MNN.Unity.MNNMultimodalResult.ReachedTokenLimit { get; }
```

```csharp
public int MNN.Unity.MNNMultimodalResult.ReusedPromptTokens { get; }
```

Tokens reused from the previous exact conversation prefix.

```csharp
public long MNN.Unity.MNNMultimodalResult.SampleMicroseconds { get; }
```

```csharp
public int MNN.Unity.MNNMultimodalResult.SampleRate { get; }
```

```csharp
public string MNN.Unity.MNNMultimodalResult.Text { get; }
```

```csharp
public long MNN.Unity.MNNMultimodalResult.TextGenerationMicroseconds { get; }
```

Wall-clock time spent generating response text, in microseconds.

```csharp
public int MNN.Unity.MNNMultimodalResult.TotalPromptTokens { get; }
```

Full rendered prompt size, including reused tokens. PromptTokens is the newly computed suffix.

```csharp
public long MNN.Unity.MNNMultimodalResult.VisionMicroseconds { get; }
```

```csharp
public float[] MNN.Unity.MNNMultimodalResult.Waveform { get; }
```

Owned mono PCM samples, independent of the next inference call.

```csharp
public long MNN.Unity.MNNMultimodalResult.WaveformGenerationMicroseconds { get; }
```

Wall-clock time spent converting generated speech tokens to waveform, in microseconds.

```csharp
public MNN.Unity.MNNRealtimeCapabilities MNN.Unity.MNNOpenAIConversationProvider.Capabilities { get; }
```

```csharp
public int MNN.Unity.MNNOpenAIConversationProvider.SampleRate { get; }
```

```csharp
public System.Collections.Generic.IReadOnlyList<string> MNN.Unity.MNNOpenAIConversationProvider.SupportedSpeechLanguages { get; }
```

```csharp
public System.Net.HttpStatusCode MNN.Unity.MNNOpenAIHttpException.Status { get; }
```

```csharp
public float[] MNN.Unity.MNNOpenAIMessage.Audio { get; }
```

```csharp
public MNN.Unity.MNNChatRole MNN.Unity.MNNOpenAIMessage.Role { get; }
```

```csharp
public int MNN.Unity.MNNOpenAIMessage.SampleRate { get; }
```

```csharp
public string MNN.Unity.MNNOpenAIMessage.Text { get; }
```

```csharp
public string MNN.Unity.MNNRealtimeCapabilities.Contract { get; }
```

```csharp
public string MNN.Unity.MNNRealtimeCapabilities.Evidence { get; }
```

```csharp
public MNN.Unity.MNNRealtimeExecution MNN.Unity.MNNRealtimeCapabilities.Execution { get; }
```

```csharp
public MNN.Unity.MNNRealtimeFeature MNN.Unity.MNNRealtimeCapabilities.Features { get; }
```

```csharp
public string MNN.Unity.MNNRealtimeCapabilities.Limitation { get; }
```

```csharp
public double? MNN.Unity.MNNRealtimeCapabilities.SustainedRtf { get; }
```

```csharp
public double MNN.Unity.MNNRealtimeCapabilities.SustainedRtfWindowAudioSeconds { get; }
```

```csharp
public MNN.Unity.MNNRealtimeReport MNN.Unity.MNNRealtimeFallbackRuntime.CurrentReport { get; }
```

```csharp
public System.Collections.Generic.IReadOnlyList<MNN.Unity.MNNRealtimeRouteUnavailableException> MNN.Unity.MNNRealtimeFallbackRuntime.Failures { get; }
```

```csharp
public MNN.Unity.MNNRealtimeOutputPipeline MNN.Unity.MNNRealtimeFallbackRuntime.Output { get; }
```

```csharp
public MNN.Unity.MNNRealtimeRouteSelection MNN.Unity.MNNRealtimeFallbackRuntime.Selection { get; }
```

```csharp
public MNN.Unity.MNNResponseId MNN.Unity.MNNRealtimeOutputPipeline.CurrentResponse { get; }
```

```csharp
public long MNN.Unity.MNNRealtimeOutputPipeline.DiscardedChunks { get; }
```

```csharp
public MNN.Unity.MNNPcmFormat MNN.Unity.MNNRealtimeOutputPipeline.Format { get; }
```

```csharp
public bool MNN.Unity.MNNRealtimeOutputPipeline.IsFaulted { get; }
```

```csharp
public int MNN.Unity.MNNRealtimeOutputPipeline.MaximumSamplesPerChunk { get; }
```

```csharp
public double MNN.Unity.MNNRealtimeOutputPipeline.QueuedAudioMilliseconds { get; }
```

```csharp
public long MNN.Unity.MNNRealtimeOutputPipeline.QueuedFrames { get; }
```

```csharp
public int MNN.Unity.MNNRealtimeOutputPipeline.SegmentCount { get; }
```

```csharp
public long MNN.Unity.MNNRealtimeOutputPipeline.UnderrunFrames { get; }
```

```csharp
public MNN.Unity.MNNRealtimeCapabilities MNN.Unity.MNNRealtimeReport.AsrCapabilities { get; }
```

```csharp
public bool MNN.Unity.MNNRealtimeReport.CanStart { get; }
```

```csharp
public string MNN.Unity.MNNRealtimeReport.DeviceContract { get; }
```

```csharp
public string MNN.Unity.MNNRealtimeReport.DeviceEvidence { get; }
```

```csharp
public string MNN.Unity.MNNRealtimeReport.DeviceLimitation { get; }
```

```csharp
public MNN.Unity.MNNRealtimeFeature MNN.Unity.MNNRealtimeReport.MissingAsr { get; }
```

```csharp
public MNN.Unity.MNNRealtimeFeature MNN.Unity.MNNRealtimeReport.MissingDevice { get; }
```

```csharp
public MNN.Unity.MNNRealtimeFeature MNN.Unity.MNNRealtimeReport.MissingModel { get; }
```

```csharp
public MNN.Unity.MNNRealtimeFeature MNN.Unity.MNNRealtimeReport.MissingTts { get; }
```

```csharp
public MNN.Unity.MNNRealtimeFeature MNN.Unity.MNNRealtimeReport.MissingVad { get; }
```

```csharp
public MNN.Unity.MNNRealtimeMode MNN.Unity.MNNRealtimeReport.Mode { get; }
```

```csharp
public string MNN.Unity.MNNRealtimeReport.ModelContract { get; }
```

```csharp
public string MNN.Unity.MNNRealtimeReport.ModelEvidence { get; }
```

```csharp
public string MNN.Unity.MNNRealtimeReport.ModelLimitation { get; }
```

```csharp
public double? MNN.Unity.MNNRealtimeReport.ModelSustainedRtf { get; }
```

```csharp
public double MNN.Unity.MNNRealtimeReport.ModelSustainedRtfWindowAudioSeconds { get; }
```

```csharp
public bool MNN.Unity.MNNRealtimeReport.ProducesSpeech { get; }
```

False retains the input topology while bypassing speech generation and TTS qualification.

```csharp
public bool MNN.Unity.MNNRealtimeReport.RequiresMeasuredSpeech { get; }
```

```csharp
public MNN.Unity.MNNRealtimeRoute MNN.Unity.MNNRealtimeReport.Route { get; }
```

```csharp
public MNN.Unity.MNNSpeechRoute MNN.Unity.MNNRealtimeReport.SpeechRoute { get; }
```

```csharp
public string MNN.Unity.MNNRealtimeReport.TtsContract { get; }
```

```csharp
public string MNN.Unity.MNNRealtimeReport.TtsEvidence { get; }
```

```csharp
public string MNN.Unity.MNNRealtimeReport.TtsLimitation { get; }
```

```csharp
public double? MNN.Unity.MNNRealtimeReport.TtsSustainedRtf { get; }
```

```csharp
public double MNN.Unity.MNNRealtimeReport.TtsSustainedRtfWindowAudioSeconds { get; }
```

```csharp
public bool MNN.Unity.MNNRealtimeReport.UsesSpeechRecognition { get; }
```

```csharp
public MNN.Unity.MNNRealtimeCapabilities MNN.Unity.MNNRealtimeReport.VadCapabilities { get; }
```

```csharp
public System.Collections.Generic.IReadOnlyList<MNN.Unity.MNNRealtimeReport> MNN.Unity.MNNRealtimeRouteSelection.Attempts { get; }
```

```csharp
public bool MNN.Unity.MNNRealtimeRouteSelection.CanStart { get; }
```

```csharp
public System.Collections.Generic.IReadOnlyList<MNN.Unity.MNNRealtimeRoute> MNN.Unity.MNNRealtimeRouteSelection.ExcludedRoutes { get; }
```

```csharp
public MNN.Unity.MNNRealtimeReport MNN.Unity.MNNRealtimeRouteSelection.Selected { get; }
```

```csharp
public MNN.Unity.MNNRealtimeRoute MNN.Unity.MNNRealtimeRouteUnavailableException.Route { get; }
```

```csharp
public System.Exception MNN.Unity.MNNRealtimeSession.Fault { get; }
```

```csharp
public MNN.Unity.MNNRealtimeOutputPipeline MNN.Unity.MNNRealtimeSession.Output { get; }
```

```csharp
public bool MNN.Unity.MNNRealtimeSession.ResourcesReleased { get; }
```

True only after the runtime has quiesced and disposal has succeeded. A retained Fault can coexist with released resources; failed shutdown keeps ownership here for StopAsync to retry.

```csharp
public MNN.Unity.MNNRealtimeSessionState MNN.Unity.MNNRealtimeSession.State { get; }
```

```csharp
public long MNN.Unity.MNNResponseId.Epoch { get; }
```

```csharp
public bool MNN.Unity.MNNResponseId.IsValid { get; }
```

```csharp
public long MNN.Unity.MNNResponseId.Session { get; }
```

```csharp
public MNN.Unity.MNNBackendType MNN.Unity.MNNTextDecision.Backend { get; }
```

```csharp
public static string[] MNN.Unity.MNNTextDecision.ModelFiles { get; }
```

```csharp
class MNN.Samples.SiriAI.Harnesses.ConfiguredHarness
```

```csharp
class MNN.Samples.SiriAI.Harnesses.DesktopHostServiceClient
```

Desktop-only CLI control. The OS owns the host; disposing a Unity client never stops it.

```csharp
class MNN.Samples.SiriAI.Harnesses.FileTaskStore
```

One process holds the writer lease. File replacement commits tasks and outbox together.

```csharp
class MNN.Samples.SiriAI.Harnesses.HarnessAcpCapabilities
```

```csharp
class MNN.Samples.SiriAI.Harnesses.HarnessActivity
```

```csharp
class MNN.Samples.SiriAI.Harnesses.HarnessArtifact
```

```csharp
class MNN.Samples.SiriAI.Harnesses.HarnessArtifactPresentation
```

```csharp
class MNN.Samples.SiriAI.Harnesses.HarnessBackendCatalog
```

```csharp
class MNN.Samples.SiriAI.Harnesses.HarnessBackendDefinition
```

Reusable onboarding metadata, independent of Unity, process execution and product UI.

```csharp
class MNN.Samples.SiriAI.Harnesses.HarnessBackendVersion
```

Normalized numeric version evidence, never raw backend process output.

```csharp
enum MNN.Samples.SiriAI.Harnesses.HarnessCapabilities
```

```csharp
class MNN.Samples.SiriAI.Harnesses.HarnessCollectedResult
```

Exact terminal content collected into a parent result, independent of presentation leases.

```csharp
class MNN.Samples.SiriAI.Harnesses.HarnessContentFiles
```

Explicit local file operations. No URI fetch, execution, or overwrite.

```csharp
enum MNN.Samples.SiriAI.Harnesses.HarnessContentKind
```

```csharp
class MNN.Samples.SiriAI.Harnesses.HarnessContentPart
```

A bounded value; URI parts are references and are never automatically downloaded.

```csharp
class MNN.Samples.SiriAI.Harnesses.HarnessDesktopPaths
```

```csharp
enum MNN.Samples.SiriAI.Harnesses.HarnessExecutionKind
```

```csharp
class MNN.Samples.SiriAI.Harnesses.HarnessExecutionStep
```

```csharp
class MNN.Samples.SiriAI.Harnesses.HarnessHostAddress
```

```csharp
class MNN.Samples.SiriAI.Harnesses.HarnessHostCatalog
```

```csharp
class MNN.Samples.SiriAI.Harnesses.HarnessHostHealth
```

```csharp
class MNN.Samples.SiriAI.Harnesses.HarnessHostProfile
```

Non-secret connection settings. Credentials belong to the platform vault, keyed by profile ID.

```csharp
enum MNN.Samples.SiriAI.Harnesses.HarnessInputAction
```

```csharp
class MNN.Samples.SiriAI.Harnesses.HarnessInputChoice
```

```csharp
class MNN.Samples.SiriAI.Harnesses.HarnessInputField
```

```csharp
class MNN.Samples.SiriAI.Harnesses.HarnessInputForm
```

Protocol-neutral display schema. The backend owns full constraint validation before settling a reply.

```csharp
enum MNN.Samples.SiriAI.Harnesses.HarnessInputMode
```

```csharp
class MNN.Samples.SiriAI.Harnesses.HarnessInputPrompt
```

Immutable pending text or external URL handoff. Completion is a backend report, not proof of authentication.

```csharp
class MNN.Samples.SiriAI.Harnesses.HarnessInstallComponent
```

```csharp
class MNN.Samples.SiriAI.Harnesses.HarnessInstallation
```

```csharp
class MNN.Samples.SiriAI.Harnesses.HarnessInstruction
```

```csharp
enum MNN.Samples.SiriAI.Harnesses.HarnessInstructionState
```

```csharp
class MNN.Samples.SiriAI.Harnesses.HarnessLocalDiscovery
```

Read-only local inventory without a running host, Node.js or .NET CLI. Presence is not authentication or protocol proof.

```csharp
class MNN.Samples.SiriAI.Harnesses.HarnessLocalSetup
```

Non-secret result of local host preparation. Read the private token only when connecting.

```csharp
class MNN.Samples.SiriAI.Harnesses.HarnessNativeHistory
```

A bounded native session/load replay, distinct from current task results and delivery receipts.

```csharp
class MNN.Samples.SiriAI.Harnesses.HarnessNativeHistoryEntry
```

```csharp
class MNN.Samples.SiriAI.Harnesses.HarnessNativeSession
```

```csharp
class MNN.Samples.SiriAI.Harnesses.HarnessNativeSessionPage
```

One native list page filtered to the task's already-owned context. Listing does not load history.

```csharp
class MNN.Samples.SiriAI.Harnesses.HarnessNativeToolCall
```

```csharp
class MNN.Samples.SiriAI.Harnesses.HarnessNativeToolState
```

Latest bounded native tool states. This is neither an execution log nor a task result.

```csharp
class MNN.Samples.SiriAI.Harnesses.HarnessOutcome
```

```csharp
class MNN.Samples.SiriAI.Harnesses.HarnessProfileCatalog
```

One writer commits profile settings and selected identity in the same atomic replacement.

```csharp
class MNN.Samples.SiriAI.Harnesses.HarnessReadiness
```

Timestamped protocol evidence, never a promise of model inference or current service availability.

```csharp
class MNN.Samples.SiriAI.Harnesses.HarnessReadinessRequest
```

```csharp
class MNN.Samples.SiriAI.Harnesses.HarnessResultConnection
```

Captured client and conversation scope. Availability is supplied by the application.

```csharp
class MNN.Samples.SiriAI.Harnesses.HarnessResultFeedback
```

Claims, renews and acknowledges results through application presentation ports. Await preserves the caller's context. Stopping delivery never cancels backend work.

```csharp
class MNN.Samples.SiriAI.Harnesses.HarnessSessionChoice
```

```csharp
class MNN.Samples.SiriAI.Harnesses.HarnessSessionConfiguration
```

Backend-reported session state. Task results preserve their own last observed snapshot.

```csharp
class MNN.Samples.SiriAI.Harnesses.HarnessSessionOption
```

```csharp
class MNN.Samples.SiriAI.Harnesses.HarnessSkillCatalog
```

```csharp
class MNN.Samples.SiriAI.Harnesses.HarnessSkillClient
```

Separate durable admission journal for management; never replays to the user-task route.

```csharp
class MNN.Samples.SiriAI.Harnesses.HarnessSkillRequest
```

Explicit host-wide skill management. Never an executable command line.

```csharp
class MNN.Samples.SiriAI.Harnesses.HarnessSkillTarget
```

```csharp
class MNN.Samples.SiriAI.Harnesses.HarnessTask
```

Detached, serializable task snapshot. Runtime state is never shared with consumers.

```csharp
class MNN.Samples.SiriAI.Harnesses.HarnessTaskDashboard
```

Main-thread presentation state. Scope generations fence late network responses after a host/account switch.

```csharp
class MNN.Samples.SiriAI.Harnesses.HarnessTaskDraft
```

Main-thread draft. An asynchronous operation can commit only to the revision it captured.

```csharp
class MNN.Samples.SiriAI.Harnesses.HarnessTaskDraftSnapshot
```

```csharp
class MNN.Samples.SiriAI.Harnesses.HarnessTaskObserver
```

A captured host scope owns replay and revision observations. Runs serialize without sharing writers.

```csharp
enum MNN.Samples.SiriAI.Harnesses.HarnessTaskState
```

```csharp
class MNN.Samples.SiriAI.Harnesses.HarnessTaskTool
```

Plain C# model-tool operations. A supplied durable submitter owns admission identity and recovery.

```csharp
enum MNN.Samples.SiriAI.Harnesses.HarnessTaskToolOperation
```

```csharp
enum MNN.Samples.SiriAI.Harnesses.HarnessWaitingFor
```

```csharp
interface MNN.Samples.SiriAI.Harnesses.IHarnessAdapter
```

Owned by a host, never by a foreground response or connection.

```csharp
interface MNN.Samples.SiriAI.Harnesses.IHarnessAdmissionCheckpoint
```

Seals adapter-specific admission identity in the same durable commit as a new task. The checkpoint must distinguish admission from execution; recovery must never replay unknown work.

```csharp
interface MNN.Samples.SiriAI.Harnesses.IHarnessCancellationOutcomeAdapter
```

A native cancel command is only admission. Its observed terminal outcome owns the result.

```csharp
interface MNN.Samples.SiriAI.Harnesses.IHarnessInputActionAdapter
```

```csharp
interface MNN.Samples.SiriAI.Harnesses.IHarnessInstructionAdapter
```

Supports exact durable instruction identities and new bounded content. Validation has no side effects. Delivery must report uncertainty rather than replay unknown work.

```csharp
interface MNN.Samples.SiriAI.Harnesses.IHarnessResultPresenter
```

Application-owned dialogue and presentation. Invoke on the application's owning context.

```csharp
interface MNN.Samples.SiriAI.Harnesses.IHarnessResultReply
```

Observed presentation evidence. Model completion and queued audio do not imply delivery.

```csharp
interface MNN.Samples.SiriAI.Harnesses.IHarnessSubmissionValidator
```

```csharp
interface MNN.Samples.SiriAI.Harnesses.IHarnessTaskStore
```

```csharp
interface MNN.Samples.SiriAI.Harnesses.IIsolatedCoordinatorHarness
```

A control-plane adapter executes in its own workspace and capacity pool. Its children still use the ordinary project lane and execution limit.

```csharp
interface MNN.Samples.SiriAI.Harnesses.ILocalCancellationHarnessAdapter
```

Runtime owns a cancellation token per execution. Run returns the observed terminal outcome; aborting a local request does not establish cancellation of work at its remote provider.

```csharp
interface MNN.Samples.SiriAI.Harnesses.IRecoverableHarnessAdapter
```

Opaque bounded checkpoint contains remote handles, never credentials. Recovery must not replay uncertain side effects.

```csharp
class MNN.Samples.SiriAI.Harnesses.MemoryTaskStore
```

```csharp
class MNN.Samples.SiriAI.Harnesses.PendingSubmissionException
```

```csharp
class MNN.Samples.SiriAI.Harnesses.PersistentTaskRuntime
```

Serial state owner; adapters run independently of callers and frontend connections.

```csharp
class MNN.Samples.SiriAI.Harnesses.RemoteTaskClient
```

Connection lifetime never controls host work. Reuse submission keys after ambiguous failures.

```csharp
class MNN.Samples.SiriAI.Harnesses.ResultClaimRequest
```

```csharp
enum MNN.Samples.SiriAI.Harnesses.ResultReceipt
```

```csharp
class MNN.Samples.SiriAI.Harnesses.ResultReceiptRequest
```

```csharp
class MNN.Samples.SiriAI.Harnesses.SubmissionJournal
```

Persist an invocation before transmission. Reconnect replays identities, never objective text deduplication.

```csharp
class MNN.Samples.SiriAI.Harnesses.SubmitTaskRequest
```

```csharp
class MNN.Samples.SiriAI.Harnesses.TaskControlRequest
```

```csharp
class MNN.Samples.SiriAI.Harnesses.TaskHostError
```

```csharp
class MNN.Samples.SiriAI.Harnesses.TaskHostJson
```

Versioned host DTO codec shared with Unity; no runtime-generated proxies.

```csharp
class MNN.Samples.SiriAI.Harnesses.TaskHostRequestException
```

```csharp
class MNN.Samples.SiriAI.Harnesses.TaskSubmission
```

```csharp
class MNN.Samples.SiriAI.Harnesses.TaskUpdateRequest
```

```csharp
interface MNN.Unity.IMNNAgentNetworkPermission
```

Application-owned tools may carry explicit network permission independent of cloud model consent.

```csharp
interface MNN.Unity.IMNNAgentTool
```

Borrowed task tool. Validates parsed arguments before any side effect; receives no conversation history.

```csharp
interface MNN.Unity.IMNNAgentTraceSource
```

```csharp
interface MNN.Unity.IMNNConversationAsrProvider
```

```csharp
interface MNN.Unity.IMNNConversationContext
```

```csharp
interface MNN.Unity.IMNNConversationEventProvider
```

Routing providers implement an explicit response-only path for application events.

```csharp
interface MNN.Unity.IMNNConversationInput
```

```csharp
interface MNN.Unity.IMNNConversationProvider
```

```csharp
interface MNN.Unity.IMNNConversationProviderDiagnostics
```

```csharp
interface MNN.Unity.IMNNConversationRealtimeProvider
```

```csharp
interface MNN.Unity.IMNNConversationRecognitionStream
```

```csharp
interface MNN.Unity.IMNNConversationRequestPlanner
```

```csharp
interface MNN.Unity.IMNNConversationResponseProvider
```

```csharp
interface MNN.Unity.IMNNConversationSpeechLanguages
```

```csharp
interface MNN.Unity.IMNNConversationSpeechModelProvider
```

```csharp
interface MNN.Unity.IMNNConversationStreamingAsrProvider
```

```csharp
interface MNN.Unity.IMNNConversationTextAssistanceProvider
```

Optional short text generation on the same model, without planning, tools or speech.

```csharp
interface MNN.Unity.IMNNConversationTextProvider
```

```csharp
interface MNN.Unity.IMNNConversationTranscriptSource
```

Random access to immutable display entries without allocating message wrappers.

```csharp
interface MNN.Unity.IMNNConversationTranscriptTurnProvider
```

```csharp
interface MNN.Unity.IMNNConversationTtsDiagnostics
```

```csharp
interface MNN.Unity.IMNNConversationTtsProvider
```

```csharp
interface MNN.Unity.IMNNConversationTurnProvider
```

```csharp
interface MNN.Unity.IMNNConversationVadProvider
```

```csharp
interface MNN.Unity.IMNNConversationVoiceIntentProvider
```

```csharp
interface MNN.Unity.IMNNRealtimeRuntimeFactory
```

Creates a fresh, unprepared runtime for one validated route of the selected model.

```csharp
interface MNN.Unity.IMNNRealtimeSessionRuntime
```

Owns concrete device/model resources for one session. Implementations must keep native resources alive until WaitForInferenceQuiescenceAsync completes and must not execute device callbacks on the control path. Lifecycle methods run on asynchronous control continuations, not Unity's main thread. Dispatch device and model work to their owners and return without blocking. Do not synchronously wait for a reentrant session operation or await shutdown from a method that shutdown itself must join.

```csharp
class MNN.Unity.MNNAgentOptions
```

```csharp
class MNN.Unity.MNNAgentPlan
```

A validated single action. Tools, clarifications and model routes have distinct payloads.

```csharp
class MNN.Unity.MNNAgentProvider
```

One serialized language stage. Owns local/cloud providers and optional transport; tools and policy delegates are borrowed. The caller serializes requests and waits for completion before disposal.

```csharp
enum MNN.Unity.MNNAgentRoutingStrategy
```

```csharp
class MNN.Unity.MNNArithmeticTool
```

```csharp
class MNN.Unity.MNNBoundedHttpClient
```

```csharp
class MNN.Unity.MNNChatMessage
```

One role/content pair for the official MNN ChatMessages API.

```csharp
enum MNN.Unity.MNNChatRole
```

```csharp
class MNN.Unity.MNNCloudSpeechAudio
```

```csharp
class MNN.Unity.MNNCloudSpeechOptions
```

Explicit protocol configuration. Service presets and credential storage belong to the application.

```csharp
enum MNN.Unity.MNNCloudSpeechProtocol
```

```csharp
class MNN.Unity.MNNCloudSpeechProvider
```

Managed task-specific cloud speech protocols. Buffered mono PCM, bounded HTTP and cancellation.

```csharp
enum MNN.Unity.MNNCloudSpeechTask
```

```csharp
class MNN.Unity.MNNContextRequestTool
```

```csharp
class MNN.Unity.MNNConversationApplication
```

One-shot conversation application owner. Call control methods and Tick on the creating UI thread. Reuse package devices, runtime factory, commands and transcript storage with any registered providers.

```csharp
enum MNN.Unity.MNNConversationCacheStatus
```

Why a text conversation did or did not reuse the previous native state.

```csharp
class MNN.Unity.MNNConversationCatalog
```

One active journal writer; inactive chats retain only small catalog records.

```csharp
class MNN.Unity.MNNConversationCommandBindings
```

Application ports for shared input commands. Configure on the owning UI context before use. Delegates run on that context, except TranscribeAsync which is dispatched to a worker.

```csharp
class MNN.Unity.MNNConversationCommands
```

Single UI-context owner of input mode, entrance, recording and submission ordering. Cancelling a gesture cancels its provider request while the capture tail still joins independently. Another hold can start after capture drains; provider submissions remain serialized.

```csharp
class MNN.Unity.MNNConversationContext
```

A text conversation fitted to an explicit model context limit. Oldest complete turns may be removed. No summarization or content rewriting occurs.

```csharp
enum MNN.Unity.MNNConversationEntryKind
```

```csharp
enum MNN.Unity.MNNConversationEntryState
```

```csharp
class MNN.Unity.MNNConversationEvent
```

An application event, separate from a user turn and never a tool-planning request.

```csharp
class MNN.Unity.MNNConversationEventReply
```

Generation evidence only. Playback requires the output pipeline's presentation receipts.

```csharp
class MNN.Unity.MNNConversationExitIntent
```

Model decision contract. No user text is interpreted by a lexical exit rule.

```csharp
class MNN.Unity.MNNConversationInactivity
```

Single-owner monotonic inactivity clock. A new input revision always starts a full window.

```csharp
class MNN.Unity.MNNConversationInfo
```

```csharp
class MNN.Unity.MNNConversationInputState
```

Thread-safe input ownership. Manual recordings are bounded mono PCM at 16 kHz.

```csharp
class MNN.Unity.MNNConversationLedger
```

Bounded low-frequency metadata ledger. Registration, receipts, interruption and snapshots may come from separate producer/control workers; none are called from the real-time audio callback. Commits complete segments only, using a device presentation watermark. Generated/queued text is not spoken history.

```csharp
class MNN.Unity.MNNConversationLocalModelOptions
```

Per-provider configuration. Create snapshots these values; no Unity object or secret ownership.

```csharp
class MNN.Unity.MNNConversationLocalProviders
```

Complete local conversation task adapters, each owned by one session.

```csharp
class MNN.Unity.MNNConversationMailbox
```

Bounded worker-to-UI notifications. A single consumer drains on its UI thread. Adjacent snapshots of one response coalesce; user turns and response boundaries retain FIFO order.

```csharp
class MNN.Unity.MNNConversationMemory
```

Prompt history and voice-validation context share one reset boundary. The response owner mutates messages; capture validation reads only the atomic text tails.

```csharp
class MNN.Unity.MNNConversationMnnProviders
```

Convenience entry points over the complete local task factory. Loading/inference execute off the UI thread.

```csharp
readonly struct MNN.Unity.MNNConversationNotification
```

```csharp
class MNN.Unity.MNNConversationOptions
```

Immutable-at-construction limits for the shared conversation engine. Providers own model settings.

```csharp
class MNN.Unity.MNNConversationPorts
```

Default application ports over shared input and notification ownership.

```csharp
class MNN.Unity.MNNConversationProviderCleanup
```

Retains failed owners until cleanup succeeds. Successful owners are removed before any error is reported.

```csharp
class MNN.Unity.MNNConversationProviderMetadata
```

```csharp
class MNN.Unity.MNNConversationProviderRuntime
```

```csharp
class MNN.Unity.MNNConversationProviders
```

```csharp
class MNN.Unity.MNNConversationRecognitionWorker
```

Bounded, optional recognition preview. Inference owns no capture/endpoint state. Loss invalidates the preview until the next turn; final recognition remains independent.

```csharp
class MNN.Unity.MNNConversationRecording
```

Owns manual-recording capture boundaries. Previous input drains before a recording opens; its physical tail, processing flush and lost-frame check finish before it is submitted.

```csharp
class MNN.Unity.MNNConversationRegistrations
```

Registrations describe tasks without loading models. Route composition acquires only required stages.

```csharp
readonly struct MNN.Unity.MNNConversationRowLayout
```

Shared conversation geometry: trailing user bubbles, full-width assistant prose and compact execution rows. The host binds these values to its UI toolkit and supplies its own colors and fonts.

```csharp
class MNN.Unity.MNNConversationRuntime
```

Composes shared capture/VAD/input, session interruption and speech output. Input, turn evaluation, text generation and synthesis have separate owners; no model/network work runs on a device callback.

```csharp
readonly struct MNN.Unity.MNNConversationRuntime.VoiceCandidate
```

```csharp
class MNN.Unity.MNNConversationRuntimeDiagnostics
```

Tracks acquired instances, including temporary transcription and provider-owned sessions.

```csharp
class MNN.Unity.MNNConversationRuntimeDiagnostics.Entry
```

```csharp
class MNN.Unity.MNNConversationRuntimeFactory
```

Composes route stages, submissions, diagnostics and lazy transcription. One owner serializes creation and submissions through MNNRealtimeSession. Stop the session before final ReleaseAsync, which releases optional transcription and failed creation resources.

```csharp
readonly struct MNN.Unity.MNNConversationSegment
```

Immutable snapshot; only IsPresented text may be projected into spoken assistant history.

```csharp
class MNN.Unity.MNNConversationSpeechInstructions
```

```csharp
class MNN.Unity.MNNConversationSpeechTextBuffer
```

```csharp
readonly struct MNN.Unity.MNNConversationSpeechTimings
```

```csharp
enum MNN.Unity.MNNConversationStage
```

```csharp
class MNN.Unity.MNNConversationStagePlan
```

```csharp
class MNN.Unity.MNNConversationStageRegistration
```

```csharp
class MNN.Unity.MNNConversationTelemetry
```

```csharp
readonly struct MNN.Unity.MNNConversationTelemetry.MNNConversationTiming
```

```csharp
class MNN.Unity.MNNConversationTelemetry.Trace
```

```csharp
class MNN.Unity.MNNConversationTextAssistance
```

Bounded text-only prompts and output rules shared by desktop and editor clients.

```csharp
enum MNN.Unity.MNNConversationTextTask
```

```csharp
class MNN.Unity.MNNConversationTimelinePresentation
```

Package-owned disclosure and Markdown projection for conversation execution entries. Apps bind the resulting layout and invoke Toggle; tool payloads remain literal fenced data.

```csharp
readonly struct MNN.Unity.MNNConversationTranscriptEntry
```

Persistable public execution evidence. ReasoningSummary contains provider-supplied public summaries only. Id identifies an event; ParentId links it to its turn or tool invocation.

```csharp
class MNN.Unity.MNNConversationTranscriptStore
```

Single-owner, bounded-resident display history. Session mode spills sealed blocks and deletes its file on disposal. Persistent mode journals changes, restores the valid prefix after a torn write, and retains the file. Call Flush for a durable checkpoint; writes alone are not durability promises.

```csharp
class MNN.Unity.MNNConversationTranscriptionService
```

```csharp
class MNN.Unity.MNNConversationViewState
```

Immutable conversation reading position. Text identity and pixel offset survive reflow; expansion indices refer to append-only transcript entries, not recycled UI rows.

```csharp
class MNN.Unity.MNNConversationVoiceInputFilter
```

Conservative transcript checks. Text plausibility is evidence, never proof of human speech.

```csharp
class MNN.Unity.MNNConversationWakeHandoff
```

Whole mono 16 kHz model frames and their exclusive stream cursor, transferred at wake activation. The caller transfers ownership of Frames and must not modify it after construction.

```csharp
enum MNN.Unity.MNNDecisionKind
```

```csharp
class MNN.Unity.MNNDecisionOption
```

```csharp
class MNN.Unity.MNNDecisionQuestion
```

One structured question, evaluated in one forward pass. Option order defines model indices; score levels start at zero.

```csharp
class MNN.Unity.MNNDecisionResult
```

Owned probabilities in the original option order. Values depend on checkpoint temperature and are not calibrated to an application's data.

```csharp
readonly struct MNN.Unity.MNNGenerationDelta
```

An immutable fragment containing only newly decoded complete UTF-8 text. Delivered synchronously on the inference caller's thread, not the Unity main thread.

```csharp
class MNN.Unity.MNNGenerationUpdate
```

Immutable cumulative snapshot delivered on the inference caller's thread.

```csharp
class MNN.Unity.MNNLlm
```

Local text generation using an MNN LLM directory (config, tokenizer and weights). Operations on an instance are serialized. Dispose waits for an active generation. Direct official C++ calls require macOS Mono or mobile ARM64 IL2CPP with the pinned MNN 3.6.2 compiler ABI. Supports text and image/audio input; waveform callbacks are not implemented.

```csharp
class MNN.Unity.MNNLlmCache
```

Exclusive persistent native cache directory for one live LLM instance. Dispose the model before releasing its lease. This is a shader/mmap cache, not a persisted conversation or KV snapshot.

```csharp
enum MNN.Unity.MNNLlmDynamicQuantization
```

Official MNN dynamic_option low bits; weight reordering is independent.

```csharp
class MNN.Unity.MNNLlmPerformanceOptions
```

LLM tuning for the bundled compiler ABI. Unassigned properties use device/model defaults when loaded; explicitly assigned properties take precedence. Resolve returns a snapshot without changing this object.

```csharp
class MNN.Unity.MNNLlmResult
```

```csharp
class MNN.Unity.MNNLocalKnowledgeTool
```

Bounded lexical retrieval over explicitly assigned local documents. No embedding service or uploads.

```csharp
class MNN.Unity.MNNMarkdownBlock
```

Immutable block data shared by Editor preview and Player UI.

```csharp
enum MNN.Unity.MNNMarkdownBlockType
```

Block constructs understood by the managed SiriAI Markdown pipeline.

```csharp
class MNN.Unity.MNNMarkdownDocument
```

Pure C# Markdown document. Parsing is deliberately separate from Unity UI and MNN inference, so the same AST is usable by Editor previews, uGUI and non-visual tooling.

```csharp
class MNN.Unity.MNNMarkdownHistoryLayout
```

Text slicing and viewport lookup independent of uGUI objects and font measurement.

```csharp
readonly struct MNN.Unity.MNNMarkdownHistoryLayout.MarkdownMeasurement
```

```csharp
class MNN.Unity.MNNMarkdownHistoryLayout.Slice
```

```csharp
class MNN.Unity.MNNMarkdownImage
```

Safe image destination extracted from a Markdown image token.

```csharp
class MNN.Unity.MNNMarkdownImageCache
```

Main-thread image ownership shared by chat views and previews. Cached entries have both an LRU count and an estimated native-byte budget. Active leases keep evicted images alive and can exceed that budget. Every transferred texture must be a newly owned runtime texture, never an imported or borrowed asset.

```csharp
class MNN.Unity.MNNMarkdownImageCache.Lease
```

One displayed binding. Dispose on the creating thread when hidden, rebound or destroyed.

```csharp
enum MNN.Unity.MNNMarkdownImageFailure
```

```csharp
class MNN.Unity.MNNMarkdownImageHeader
```

Reads the PNG/JPEG dimensions before calling Unity's native decoder. Unsupported or incomplete headers are rejected. This validates dimensions, not image integrity.

```csharp
class MNN.Unity.MNNMarkdownImageRequest
```

Bounded HTTP/file image transport. Create, poll, decode and dispose on Unity's main thread. Reception stops at the encoded limit without relying on polling or Content-Length. PNG/JPEG dimensions are checked before native decode. A successful, non-readable texture belongs to the caller.

```csharp
class MNN.Unity.MNNMarkdownListItem
```

A single item in an ordered or unordered Markdown list.

```csharp
class MNN.Unity.MNNMarkdownRenderResult
```

The shared output of one Markdown pass. Editor and Player adapters can keep this object alongside their view model so parsing, Rich Text conversion and image discovery are performed once for the same source value.

```csharp
class MNN.Unity.MNNMarkdownRenderer
```

Converts Markdown inline syntax into Unity's safe subset of rich text.

```csharp
enum MNN.Unity.MNNMarkdownTheme
```

The surface theme used for generated rich-text colors. Body text inherits its control's color.

```csharp
class MNN.Unity.MNNMiniMindO
```

MiniMind-o Thinker/Talker through the package's official MNN C++ Interpreter/Tensor bindings. Accepts completed turns, streams text and speech, and supports cancellation. The released model is not native full duplex.

```csharp
class MNN.Unity.MNNMiniMindOOptions
```

Bounded MiniMind-o generation using the released closed-thinking sampling schedule.

```csharp
class MNN.Unity.MNNMiniMindORequest
```

One completed input turn. Audio is mono 16 kHz PCM; pixels are planar RGB 256x256 in [-1,1].

```csharp
class MNN.Unity.MNNMiniMindOResult
```

Generated text, aligned eight-codebook Mimi frames and decoded mono speech.

```csharp
class MNN.Unity.MNNMiniMindOUpdate
```

A text snapshot and newly available aligned audio codes. Callbacks run on the inference caller's thread.

```csharp
enum MNN.Unity.MNNModelCapabilities
```

```csharp
class MNN.Unity.MNNMultimodalResult
```

```csharp
class MNN.Unity.MNNOpenAIClient
```

Reusable OpenAI-compatible HTTP transport. The default transport refuses redirects. Text and PCM are consumed incrementally with awaited backpressure; no scene or device ownership.

```csharp
class MNN.Unity.MNNOpenAIConversationOptions
```

```csharp
class MNN.Unity.MNNOpenAIConversationProvider
```

Conversation ports over the shared OpenAI HTTP, WAV, SSE and PCM services.

```csharp
enum MNN.Unity.MNNOpenAIConversationStage
```

```csharp
class MNN.Unity.MNNOpenAIHttpException
```

Status-only failure; provider bodies and credentials are never copied to diagnostics.

```csharp
readonly struct MNN.Unity.MNNOpenAIMessage
```

HTTP multimodal message; borrowed audio must remain immutable until the request completes.

```csharp
class MNN.Unity.MNNPocketModel
```

Official Pocket instruction model identity and its pinned MNN W4A16 deployment assets.

```csharp
class MNN.Unity.MNNRealtimeCapabilities
```

Evidence supplied by an implemented backend; the preflight never infers it from a model name.

```csharp
enum MNN.Unity.MNNRealtimeExecution
```

```csharp
class MNN.Unity.MNNRealtimeFallbackRuntime
```

Decorates concrete runtimes with bounded startup/recovery fallback. Inject into the existing Session; it remains the sole lifecycle coordinator. No inference, audio callback, response replay or native code. Lifecycle methods must be serialized by that Session. Diagnostics are immutable, thread-safe snapshots.

```csharp
enum MNN.Unity.MNNRealtimeFeature
```

```csharp
enum MNN.Unity.MNNRealtimeMode
```

```csharp
class MNN.Unity.MNNRealtimeOutputPipeline
```

Composes response gating, bounded PCM playback and presentation-aware spoken history. One control owner calls lifecycle methods, one producer enqueues, and one device callback renders. Ledger metadata operations are internally serialized and never run on the device callback.

```csharp
class MNN.Unity.MNNRealtimePreflight
```

Managed capability evaluation only. Does not load native code, request permissions or download models.

```csharp
class MNN.Unity.MNNRealtimeReport
```

```csharp
enum MNN.Unity.MNNRealtimeRoute
```

Conversation topology, independent of model names and physical device qualification.

```csharp
class MNN.Unity.MNNRealtimeRouteSelection
```

Immutable startup/control-path decision. Does not own, create or switch inference resources.

```csharp
class MNN.Unity.MNNRealtimeRouteSelector
```

Prefers continuous understanding, then model speech, then audio-to-text/TTS, then ASR/text-model/TTS. Uses one selected model's validated capabilities. Does not infer capabilities from a model name, download replacements or relax ABI, interruption, echo-control and measured throughput requirements.

```csharp
class MNN.Unity.MNNRealtimeRouteUnavailableException
```

Explicit backend classification permitting finite fallback for this topology only. Never use for user cancellation, permissions, shared ABI/device failures or failure to quiesce/release resources.

```csharp
class MNN.Unity.MNNRealtimeSession
```

Serializes session lifecycle and recovery while allowing output to be silenced synchronously on interruption. This coordinates implemented backends; it does not provide a device or model backend itself. Operation tasks are published before runtime dispatch. Runtime methods and cancellation callbacks never run under the session state lock or on the lifecycle caller's synchronous call stack.

```csharp
enum MNN.Unity.MNNRealtimeSessionState
```

```csharp
readonly struct MNN.Unity.MNNResponseId
```

Immutable identity captured when a reply starts, including by every asynchronous producer.

```csharp
class MNN.Unity.MNNRouteLlmClient
```

```csharp
enum MNN.Unity.MNNSpeechRoute
```

```csharp
class MNN.Unity.MNNTextDecision
```

Pinned English Laya structured decisions. One question per forward pass; probabilities are checkpoint outputs, not application calibration.

## Models

```csharp
MNN.Unity.MNNAcousticModelTask.EnglishWakeDetection = 2
```

```csharp
MNN.Unity.MNNAcousticModelTask.NoiseReduction = 0
```

```csharp
MNN.Unity.MNNAcousticModelTask.Separation = 3
```

```csharp
MNN.Unity.MNNAcousticModelTask.SpeakerEmbedding = 4
```

```csharp
MNN.Unity.MNNAcousticModelTask.VoiceActivity = 5
```

```csharp
MNN.Unity.MNNAcousticModelTask.WakeDetection = 1
```

```csharp
MNN.Unity.MNNBundledModelStorage.Automatic = 0
```

```csharp
MNN.Unity.MNNBundledModelStorage.PersistentData = 1
```

```csharp
MNN.Unity.MNNConversationModelAdapter.AudioTurn = 16
```

```csharp
MNN.Unity.MNNConversationModelAdapter.BertVits2 = 10
```

```csharp
MNN.Unity.MNNConversationModelAdapter.CosyVoice = 4
```

```csharp
MNN.Unity.MNNConversationModelAdapter.DtlnAec = 13
```

```csharp
MNN.Unity.MNNConversationModelAdapter.KeywordSpotting = 21
```

```csharp
MNN.Unity.MNNConversationModelAdapter.Kokoro = 3
```

```csharp
MNN.Unity.MNNConversationModelAdapter.Language = 0
```

```csharp
MNN.Unity.MNNConversationModelAdapter.MossNano = 12
```

```csharp
MNN.Unity.MNNConversationModelAdapter.NoiseReduction = 18
```

```csharp
MNN.Unity.MNNConversationModelAdapter.Omni = 1
```

```csharp
MNN.Unity.MNNConversationModelAdapter.Paraformer = 14
```

```csharp
MNN.Unity.MNNConversationModelAdapter.Piper = 9
```

```csharp
MNN.Unity.MNNConversationModelAdapter.Qwen3Asr = 7
```

```csharp
MNN.Unity.MNNConversationModelAdapter.SenseVoice = 2
```

```csharp
MNN.Unity.MNNConversationModelAdapter.Separation = 19
```

```csharp
MNN.Unity.MNNConversationModelAdapter.SherpaVitsChinese = 11
```

```csharp
MNN.Unity.MNNConversationModelAdapter.Silero = 5
```

```csharp
MNN.Unity.MNNConversationModelAdapter.SmartTurn = 6
```

```csharp
MNN.Unity.MNNConversationModelAdapter.SpeakerEmbedding = 20
```

```csharp
MNN.Unity.MNNConversationModelAdapter.SpeechRecognition = 22
```

```csharp
MNN.Unity.MNNConversationModelAdapter.SpeechSynthesis = 23
```

```csharp
MNN.Unity.MNNConversationModelAdapter.Supertonic = 8
```

```csharp
MNN.Unity.MNNConversationModelAdapter.TextTurn = 17
```

```csharp
MNN.Unity.MNNConversationModelAdapter.VoiceActivity = 15
```

```csharp
public MNN.Unity.MNNConversationModelAdapter MNN.Unity.MNNConversationModelCatalog.Entry.adapter
```

```csharp
public long MNN.Unity.MNNConversationModelCatalog.Entry.bytes
```

```csharp
public bool MNN.Unity.MNNConversationModelCatalog.Entry.catalogOnly
```

```csharp
public string MNN.Unity.MNNConversationModelCatalog.Entry.modelId
```

```csharp
public bool MNN.Unity.MNNConversationModelCatalog.Entry.modelScope
```

```csharp
public bool MNN.Unity.MNNConversationModelCatalog.Entry.online
```

```csharp
public MNN.Unity.MNNRemoteModel MNN.Unity.MNNConversationModelCatalog.Entry.remote
```

```csharp
public string MNN.Unity.MNNConversationModelCatalog.Entry.repository
```

```csharp
public string MNN.Unity.MNNConversationModelCatalog.Entry.revision
```

```csharp
public static readonly string[] MNN.Unity.MNNHuggingFaceSourcePool.DefaultDiscoveryUrls
```

```csharp
public const string MNN.Unity.MNNInsufficientDiskSpaceException.ErrorCode = "insufficient_disk_space"
```

```csharp
public const int MNN.Unity.MNNModelArchiveInstaller.MaximumEntries = 8192
```

```csharp
public const long MNN.Unity.MNNModelArchiveInstaller.MaximumExpandedBytes = 34359738368
```

```csharp
public long MNN.Unity.MNNModelAssetFile.bytes
```

```csharp
public string MNN.Unity.MNNModelAssetFile.path
```

```csharp
public string MNN.Unity.MNNModelAssetFile.sha256
```

```csharp
public string MNN.Unity.MNNModelAssetFile.url
```

```csharp
public int MNN.Unity.MNNModelBenchmark.ConcurrentSequences
```

```csharp
public int MNN.Unity.MNNModelBenchmark.ContextTokens
```

```csharp
public string MNN.Unity.MNNModelBenchmark.Evidence
```

```csharp
public string MNN.Unity.MNNModelBenchmark.ExecutionId
```

```csharp
public string MNN.Unity.MNNModelBenchmark.ExportIdentity
```

```csharp
public double MNN.Unity.MNNModelBenchmark.FirstResultSeconds
```

```csharp
public string MNN.Unity.MNNModelBenchmark.HardwareId
```

```csharp
public double MNN.Unity.MNNModelBenchmark.InputUnits
```

```csharp
public MNN.Unity.MNNModelMemoryKind MNN.Unity.MNNModelBenchmark.MemoryKind
```

```csharp
public int MNN.Unity.MNNModelBenchmark.PrefillChunkTokens
```

```csharp
public int MNN.Unity.MNNModelBenchmark.PromptTokens
```

```csharp
public string MNN.Unity.MNNModelBenchmark.RuntimeId
```

```csharp
public double MNN.Unity.MNNModelBenchmark.ThroughputPerSecond
```

```csharp
public int MNN.Unity.MNNModelCacheProfile.AllocationBlockTokens
```

```csharp
public int MNN.Unity.MNNModelCacheProfile.BytesPerElement
```

```csharp
public long MNN.Unity.MNNModelCacheProfile.FixedBytesPerSequence
```

```csharp
public int MNN.Unity.MNNModelCacheProfile.KeyHeadDimension
```

```csharp
public int MNN.Unity.MNNModelCacheProfile.KeyValueHeads
```

```csharp
public int MNN.Unity.MNNModelCacheProfile.Layers
```

```csharp
public int MNN.Unity.MNNModelCacheProfile.ValueHeadDimension
```

```csharp
public int MNN.Unity.MNNModelCacheProfile.WindowTokens
```

```csharp
public static readonly string[] MNN.Unity.MNNModelCatalog.FeaturedHuggingFaceRepositories
```

```csharp
MNN.Unity.MNNModelCatalogCategory.All = 0
```

```csharp
MNN.Unity.MNNModelCatalogCategory.AudioClassification = 51
```

```csharp
MNN.Unity.MNNModelCatalogCategory.AudioGeneration = 40
```

```csharp
MNN.Unity.MNNModelCatalogCategory.AudioLanguageModel = 16
```

```csharp
MNN.Unity.MNNModelCatalogCategory.AudioProcessing = 9
```

```csharp
MNN.Unity.MNNModelCatalogCategory.AvatarAnimation = 29
```

```csharp
MNN.Unity.MNNModelCatalogCategory.CodeModel = 20
```

```csharp
MNN.Unity.MNNModelCatalogCategory.DepthEstimation = 38
```

```csharp
MNN.Unity.MNNModelCatalogCategory.DocumentLayout = 31
```

```csharp
MNN.Unity.MNNModelCatalogCategory.DocumentRectification = 42
```

```csharp
MNN.Unity.MNNModelCatalogCategory.EchoCancellation = 44
```

```csharp
MNN.Unity.MNNModelCatalogCategory.EmbeddingModel = 17
```

```csharp
MNN.Unity.MNNModelCatalogCategory.FaceDetection = 5
```

```csharp
MNN.Unity.MNNModelCatalogCategory.FaceLandmarks = 30
```

```csharp
MNN.Unity.MNNModelCatalogCategory.FaceRecognition = 6
```

```csharp
MNN.Unity.MNNModelCatalogCategory.ImageClassification = 1
```

```csharp
MNN.Unity.MNNModelCatalogCategory.ImageEmbedding = 32
```

```csharp
MNN.Unity.MNNModelCatalogCategory.ImageGenerationModel = 22
```

```csharp
MNN.Unity.MNNModelCatalogCategory.ImageMatting = 26
```

```csharp
MNN.Unity.MNNModelCatalogCategory.ImageRestoration = 27
```

```csharp
MNN.Unity.MNNModelCatalogCategory.KeywordSpotting = 43
```

```csharp
MNN.Unity.MNNModelCatalogCategory.LargeLanguageModel = 13
```

```csharp
MNN.Unity.MNNModelCatalogCategory.ModelCollection = 34
```

```csharp
MNN.Unity.MNNModelCatalogCategory.ModelDiagnostics = 36
```

```csharp
MNN.Unity.MNNModelCatalogCategory.NLP = 8
```

```csharp
MNN.Unity.MNNModelCatalogCategory.OCR = 7
```

```csharp
MNN.Unity.MNNModelCatalogCategory.OCRModel = 19
```

```csharp
MNN.Unity.MNNModelCatalogCategory.ObjectDetection = 2
```

```csharp
MNN.Unity.MNNModelCatalogCategory.OmniModel = 15
```

```csharp
MNN.Unity.MNNModelCatalogCategory.Other = 12
```

```csharp
MNN.Unity.MNNModelCatalogCategory.PoseEstimation = 4
```

```csharp
MNN.Unity.MNNModelCatalogCategory.Reconstruction3D = 28
```

```csharp
MNN.Unity.MNNModelCatalogCategory.Reranker = 18
```

```csharp
MNN.Unity.MNNModelCatalogCategory.Robotics = 39
```

```csharp
MNN.Unity.MNNModelCatalogCategory.RuntimeResources = 35
```

```csharp
MNN.Unity.MNNModelCatalogCategory.SafetyModel = 21
```

```csharp
MNN.Unity.MNNModelCatalogCategory.SemanticSegmentation = 3
```

```csharp
MNN.Unity.MNNModelCatalogCategory.SpeakerDiarization = 50
```

```csharp
MNN.Unity.MNNModelCatalogCategory.SpeakerRecognition = 41
```

```csharp
MNN.Unity.MNNModelCatalogCategory.SpeechEnhancement = 47
```

```csharp
MNN.Unity.MNNModelCatalogCategory.SpeechRecognition = 25
```

```csharp
MNN.Unity.MNNModelCatalogCategory.SpeechSeparation = 48
```

```csharp
MNN.Unity.MNNModelCatalogCategory.SpeechSynthesis = 24
```

```csharp
MNN.Unity.MNNModelCatalogCategory.StyleTransfer = 11
```

```csharp
MNN.Unity.MNNModelCatalogCategory.SuperResolution = 10
```

```csharp
MNN.Unity.MNNModelCatalogCategory.TargetSpeakerExtraction = 49
```

```csharp
MNN.Unity.MNNModelCatalogCategory.TextGeneration = 23
```

```csharp
MNN.Unity.MNNModelCatalogCategory.Translation = 37
```

```csharp
MNN.Unity.MNNModelCatalogCategory.TurnDetection = 46
```

```csharp
MNN.Unity.MNNModelCatalogCategory.VideoGeneration = 33
```

```csharp
MNN.Unity.MNNModelCatalogCategory.VisionLanguageModel = 14
```

```csharp
MNN.Unity.MNNModelCatalogCategory.VoiceActivityDetection = 45
```

```csharp
public string MNN.Unity.MNNModelConfig.customPath
```

```csharp
public string MNN.Unity.MNNModelConfig.displayName
```

```csharp
public string MNN.Unity.MNNModelConfig.downloadUrl
```

```csharp
public bool MNN.Unity.MNNModelConfig.enableHotUpdate
```

```csharp
public bool MNN.Unity.MNNModelConfig.includeInBuild
```

```csharp
public string MNN.Unity.MNNModelConfig.modelFileName
```

```csharp
public string MNN.Unity.MNNModelConfig.modelId
```

```csharp
public MNN.Unity.PlatformModelConfig[] MNN.Unity.MNNModelConfig.platformConfigs
```

```csharp
public MNN.Unity.ModelStorageLocation MNN.Unity.MNNModelConfig.storageLocation
```

```csharp
public string MNN.Unity.MNNModelConfig.version
```

```csharp
public bool MNN.Unity.MNNModelDeliveryFile.bundled
```

```csharp
public long MNN.Unity.MNNModelDeliveryFile.bytes
```

```csharp
public string MNN.Unity.MNNModelDeliveryFile.path
```

```csharp
public string MNN.Unity.MNNModelDeliveryFile.sha256
```

```csharp
public string MNN.Unity.MNNModelDeliveryFile.url
```

```csharp
public const string MNN.Unity.MNNModelDeliveryManifest.ModelsPath = "MNN/Models"
```

```csharp
public const string MNN.Unity.MNNModelDeliveryManifest.StreamingPath = "MNN/model-delivery.json"
```

```csharp
public bool MNN.Unity.MNNModelDeliveryManifest.allowDownloads
```

```csharp
public MNN.Unity.MNNBundledModelStorage MNN.Unity.MNNModelDeliveryManifest.bundledStorage
```

```csharp
public MNN.Unity.MNNModelDeliveryFile[] MNN.Unity.MNNModelDeliveryManifest.files
```

```csharp
public int MNN.Unity.MNNModelDeliveryManifest.requestTimeout
```

```csharp
public int MNN.Unity.MNNModelDeliveryManifest.schemaVersion
```

```csharp
MNN.Unity.MNNModelDownloadProgress.Phase.Cancelled = 9
```

```csharp
MNN.Unity.MNNModelDownloadProgress.Phase.Checking = 1
```

```csharp
MNN.Unity.MNNModelDownloadProgress.Phase.Complete = 7
```

```csharp
MNN.Unity.MNNModelDownloadProgress.Phase.Connecting = 3
```

```csharp
MNN.Unity.MNNModelDownloadProgress.Phase.Downloading = 4
```

```csharp
MNN.Unity.MNNModelDownloadProgress.Phase.Failed = 10
```

```csharp
MNN.Unity.MNNModelDownloadProgress.Phase.Loading = 8
```

```csharp
MNN.Unity.MNNModelDownloadProgress.Phase.Preparing = 5
```

```csharp
MNN.Unity.MNNModelDownloadProgress.Phase.Queued = 0
```

```csharp
MNN.Unity.MNNModelDownloadProgress.Phase.Reused = 6
```

```csharp
MNN.Unity.MNNModelDownloadProgress.Phase.Waiting = 2
```

```csharp
public readonly double MNN.Unity.MNNModelDownloadProgress.Snapshot.BytesPerSecond
```

```csharp
public readonly int MNN.Unity.MNNModelDownloadProgress.Snapshot.CompleteModels
```

```csharp
public readonly string MNN.Unity.MNNModelDownloadProgress.Snapshot.Directory
```

```csharp
public readonly int MNN.Unity.MNNModelDownloadProgress.Snapshot.DownloadedModels
```

```csharp
public readonly double MNN.Unity.MNNModelDownloadProgress.Snapshot.ElapsedSeconds
```

```csharp
public readonly bool MNN.Unity.MNNModelDownloadProgress.Snapshot.Estimated
```

```csharp
public readonly string MNN.Unity.MNNModelDownloadProgress.Snapshot.File
```

```csharp
public readonly float MNN.Unity.MNNModelDownloadProgress.Snapshot.Fraction
```

```csharp
public readonly string MNN.Unity.MNNModelDownloadProgress.Snapshot.ModelName
```

```csharp
public readonly long MNN.Unity.MNNModelDownloadProgress.Snapshot.ModelReceivedBytes
```

```csharp
public readonly bool MNN.Unity.MNNModelDownloadProgress.Snapshot.ModelReused
```

```csharp
public readonly long MNN.Unity.MNNModelDownloadProgress.Snapshot.ModelTotalBytes
```

```csharp
public readonly System.Collections.Generic.KeyValuePair<string, MNN.Unity.MNNModelDownloadProgress.Phase>[] MNN.Unity.MNNModelDownloadProgress.Snapshot.QueueItems
```

```csharp
public readonly long MNN.Unity.MNNModelDownloadProgress.Snapshot.ReceivedBytes
```

```csharp
public readonly int MNN.Unity.MNNModelDownloadProgress.Snapshot.ReusedModels
```

```csharp
public readonly string MNN.Unity.MNNModelDownloadProgress.Snapshot.Source
```

```csharp
public readonly MNN.Unity.MNNModelDownloadProgress.Phase MNN.Unity.MNNModelDownloadProgress.Snapshot.State
```

```csharp
public readonly long MNN.Unity.MNNModelDownloadProgress.Snapshot.TotalBytes
```

```csharp
public readonly bool MNN.Unity.MNNModelDownloadProgress.Snapshot.TotalKnown
```

```csharp
public readonly int MNN.Unity.MNNModelDownloadProgress.Snapshot.TotalModels
```

```csharp
public long MNN.Unity.MNNModelHardware.AvailableDeviceMemoryBytes
```

```csharp
public long MNN.Unity.MNNModelHardware.AvailableSystemMemoryBytes
```

```csharp
public long MNN.Unity.MNNModelHardware.DeviceMemoryBytes
```

```csharp
public string MNN.Unity.MNNModelHardware.DeviceName
```

```csharp
public string MNN.Unity.MNNModelHardware.ExecutionId
```

```csharp
public string MNN.Unity.MNNModelHardware.HardwareId
```

```csharp
public double MNN.Unity.MNNModelHardware.MemoryBandwidthBytesPerSecond
```

```csharp
public MNN.Unity.MNNModelMemoryKind MNN.Unity.MNNModelHardware.MemoryKind
```

```csharp
public double MNN.Unity.MNNModelHardware.OperationsPerSecond
```

```csharp
public string MNN.Unity.MNNModelHardware.PerformanceEvidence
```

```csharp
public int MNN.Unity.MNNModelHardware.ProcessorCount
```

```csharp
public long MNN.Unity.MNNModelHardware.ReservedDeviceBytes
```

```csharp
public long MNN.Unity.MNNModelHardware.ReservedSystemBytes
```

```csharp
public string MNN.Unity.MNNModelHardware.RuntimeId
```

```csharp
public double MNN.Unity.MNNModelHardware.SustainedPerformanceFactor
```

```csharp
public long MNN.Unity.MNNModelHardware.SystemMemoryBytes
```

```csharp
public string MNN.Unity.MNNModelInputSpec.dataType
```

```csharp
public string MNN.Unity.MNNModelInputSpec.format
```

```csharp
public string MNN.Unity.MNNModelInputSpec.name
```

```csharp
public int[] MNN.Unity.MNNModelInputSpec.shape
```

```csharp
MNN.Unity.MNNModelInstallation.Phase.Cancelled = 6
```

```csharp
MNN.Unity.MNNModelInstallation.Phase.Checking = 1
```

```csharp
MNN.Unity.MNNModelInstallation.Phase.Complete = 4
```

```csharp
MNN.Unity.MNNModelInstallation.Phase.Connecting = 2
```

```csharp
MNN.Unity.MNNModelInstallation.Phase.Failed = 7
```

```csharp
MNN.Unity.MNNModelInstallation.Phase.Preparing = 3
```

```csharp
MNN.Unity.MNNModelInstallation.Phase.Reused = 5
```

```csharp
MNN.Unity.MNNModelInstallation.Phase.Waiting = 0
```

```csharp
public const string MNN.Unity.MNNModelInstallation.ReceiptName = ".mnn-install.json"
```

```csharp
MNN.Unity.MNNModelKind.AvConvTasNet = 0
```

```csharp
MNN.Unity.MNNModelKind.AvDprnn = 1
```

```csharp
MNN.Unity.MNNModelKind.AvMossFormer2Tse = 2
```

```csharp
MNN.Unity.MNNModelKind.ConvTasNet = 3
```

```csharp
MNN.Unity.MNNModelKind.CosyVoice = 4
```

```csharp
MNN.Unity.MNNModelKind.CosyVoice300M = 5
```

```csharp
MNN.Unity.MNNModelKind.CueLight = 6
```

```csharp
MNN.Unity.MNNModelKind.Dccrn = 7
```

```csharp
MNN.Unity.MNNModelKind.Dccrnet = 8
```

```csharp
MNN.Unity.MNNModelKind.Dcunet = 9
```

```csharp
MNN.Unity.MNNModelKind.DeepFilterNet3 = 10
```

```csharp
MNN.Unity.MNNModelKind.DocumentLayout = 11
```

```csharp
MNN.Unity.MNNModelKind.Dprnn = 12
```

```csharp
MNN.Unity.MNNModelKind.Dptnet = 13
```

```csharp
MNN.Unity.MNNModelKind.Dtln = 14
```

```csharp
MNN.Unity.MNNModelKind.DtlnAec = 15
```

```csharp
MNN.Unity.MNNModelKind.DualTurnEndpointing = 16
```

```csharp
MNN.Unity.MNNModelKind.FrcrnSe = 17
```

```csharp
MNN.Unity.MNNModelKind.FsEend = 18
```

```csharp
MNN.Unity.MNNModelKind.FsmnVad = 19
```

```csharp
MNN.Unity.MNNModelKind.FullSubNet = 20
```

```csharp
MNN.Unity.MNNModelKind.Gtcrn = 21
```

```csharp
MNN.Unity.MNNModelKind.ImageEmbedding = 22
```

```csharp
MNN.Unity.MNNModelKind.Kokoro = 23
```

```csharp
MNN.Unity.MNNModelKind.Kws = 24
```

```csharp
MNN.Unity.MNNModelKind.LiveKitTurnDetector = 25
```

```csharp
MNN.Unity.MNNModelKind.LsEend = 26
```

```csharp
MNN.Unity.MNNModelKind.MarbleNetVad = 27
```

```csharp
MNN.Unity.MNNModelKind.MossFormer2 = 28
```

```csharp
MNN.Unity.MNNModelKind.MossFormer2Se = 29
```

```csharp
MNN.Unity.MNNModelKind.MossFormerGan = 30
```

```csharp
MNN.Unity.MNNModelKind.NamoTurnDetector = 31
```

```csharp
MNN.Unity.MNNModelKind.Paraformer = 32
```

```csharp
MNN.Unity.MNNModelKind.Qwen3Asr = 33
```

```csharp
MNN.Unity.MNNModelKind.ResembleDenoiser = 34
```

```csharp
MNN.Unity.MNNModelKind.ResembleEnhancer = 35
```

```csharp
MNN.Unity.MNNModelKind.Seg = 36
```

```csharp
MNN.Unity.MNNModelKind.SenseVoice = 37
```

```csharp
MNN.Unity.MNNModelKind.SepFormer = 38
```

```csharp
MNN.Unity.MNNModelKind.SepReformer = 39
```

```csharp
MNN.Unity.MNNModelKind.Silero = 40
```

```csharp
MNN.Unity.MNNModelKind.SmartTurn = 41
```

```csharp
MNN.Unity.MNNModelKind.Sortformer = 42
```

```csharp
MNN.Unity.MNNModelKind.SpExPlus = 43
```

```csharp
MNN.Unity.MNNModelKind.Speaker = 44
```

```csharp
MNN.Unity.MNNModelKind.SudoRmRf = 45
```

```csharp
MNN.Unity.MNNModelKind.TextDecision = 46
```

```csharp
MNN.Unity.MNNModelKind.Tiger = 47
```

```csharp
MNN.Unity.MNNModelKind.TurnSense = 48
```

```csharp
MNN.Unity.MNNModelKind.Vbx = 49
```

```csharp
MNN.Unity.MNNModelKind.VoxTurn = 50
```

```csharp
MNN.Unity.MNNModelKind.WeSpeaker = 51
```

```csharp
MNN.Unity.MNNModelKind.Whisper = 52
```

```csharp
MNN.Unity.MNNModelKind.Zipformer = 53
```

```csharp
MNN.Unity.MNNModelMemoryKind.Cpu = 1
```

```csharp
MNN.Unity.MNNModelMemoryKind.DiscreteGpu = 4
```

```csharp
MNN.Unity.MNNModelMemoryKind.Unified = 2
```

```csharp
public int MNN.Unity.MNNModelOutputSpec.classCount
```

```csharp
public string MNN.Unity.MNNModelOutputSpec.dataType
```

```csharp
public string MNN.Unity.MNNModelOutputSpec.name
```

```csharp
public int[] MNN.Unity.MNNModelOutputSpec.shape
```

```csharp
public long MNN.Unity.MNNModelRecommendationPolicy.ApplicationReserveBytes
```

```csharp
public double MNN.Unity.MNNModelRecommendationPolicy.BandwidthEfficiency
```

```csharp
public double MNN.Unity.MNNModelRecommendationPolicy.DeviceReserveFraction
```

```csharp
public double MNN.Unity.MNNModelRecommendationPolicy.HeadroomWeight
```

```csharp
public int MNN.Unity.MNNModelRecommendationPolicy.MaximumCandidatesPerTask
```

```csharp
public int MNN.Unity.MNNModelRecommendationPolicy.MaximumPlanCandidates
```

```csharp
public long MNN.Unity.MNNModelRecommendationPolicy.MinimumDeviceReserveBytes
```

```csharp
public long MNN.Unity.MNNModelRecommendationPolicy.MinimumSystemReserveBytes
```

```csharp
public double MNN.Unity.MNNModelRecommendationPolicy.QualityWeight
```

```csharp
public double MNN.Unity.MNNModelRecommendationPolicy.ResponsivenessWeight
```

```csharp
public double MNN.Unity.MNNModelRecommendationPolicy.SafetyMarginFraction
```

```csharp
public double MNN.Unity.MNNModelRecommendationPolicy.SystemReserveFraction
```

```csharp
public const long MNN.Unity.MNNModelRecommendationProfiles.Qwen27DownloadBytes = 18861746870
```

```csharp
public const string MNN.Unity.MNNModelRecommendationProfiles.Qwen27Id = "Qwen3.5-27B-MNN"
```

```csharp
public const string MNN.Unity.MNNModelRecommendationProfiles.Qwen27Repository = "MNN/Qwen3.5-27B-MNN"
```

```csharp
public const string MNN.Unity.MNNModelRecommendationProfiles.Qwen27Revision = "70e74126a5c32d98853a2fd9bd3df4e80141d2f6"
```

```csharp
public const long MNN.Unity.MNNModelRecommendationProfiles.Qwen4DownloadBytes = 2845947034
```

```csharp
public const string MNN.Unity.MNNModelRecommendationProfiles.Qwen4Id = "Qwen3.5-4B-MNN"
```

```csharp
public const string MNN.Unity.MNNModelRecommendationProfiles.Qwen4Repository = "MNN/Qwen3.5-4B-MNN"
```

```csharp
public const string MNN.Unity.MNNModelRecommendationProfiles.Qwen4Revision = "33045fd83cd206d66976af438f7a58255b258a58"
```

```csharp
public const long MNN.Unity.MNNModelRecommendationProfiles.Qwen9DownloadBytes = 7274876613
```

```csharp
public const string MNN.Unity.MNNModelRecommendationProfiles.Qwen9Id = "Qwen3.5-9B-MNN"
```

```csharp
public const string MNN.Unity.MNNModelRecommendationProfiles.Qwen9Repository = "MNN/Qwen3.5-9B-MNN"
```

```csharp
public const string MNN.Unity.MNNModelRecommendationProfiles.Qwen9Revision = "4def89fe8459266be4be64d5ab7ae8bbe9066081"
```

```csharp
public const string MNN.Unity.MNNModelRecommendationProfiles.Version = "2026-11-10.1"
```

```csharp
MNN.Unity.MNNModelRecommendationStatus.Provisional = 1
```

```csharp
MNN.Unity.MNNModelRecommendationStatus.Rejected = 2
```

```csharp
MNN.Unity.MNNModelRecommendationStatus.Suitable = 0
```

```csharp
MNN.Unity.MNNModelRecommendationTask.AudioProcessing = 5
```

```csharp
MNN.Unity.MNNModelRecommendationTask.Embedding = 4
```

```csharp
MNN.Unity.MNNModelRecommendationTask.Language = 0
```

```csharp
MNN.Unity.MNNModelRecommendationTask.Other = 6
```

```csharp
MNN.Unity.MNNModelRecommendationTask.SpeechRecognition = 1
```

```csharp
MNN.Unity.MNNModelRecommendationTask.SpeechSynthesis = 2
```

```csharp
MNN.Unity.MNNModelRecommendationTask.Vision = 3
```

```csharp
public string MNN.Unity.MNNModelRepositoryFile.Path
```

```csharp
public string MNN.Unity.MNNModelRepositoryFile.Revision
```

```csharp
public string MNN.Unity.MNNModelRepositoryFile.Sha256
```

```csharp
public long MNN.Unity.MNNModelRepositoryFile.Size
```

```csharp
public string MNN.Unity.MNNModelRepositoryFile.SourceRepository
```

```csharp
public string MNN.Unity.MNNModelRepositoryFile.Type
```

```csharp
public long MNN.Unity.MNNModelResourceProfile.ActiveParameterCount
```

```csharp
public MNN.Unity.MNNModelBenchmark[] MNN.Unity.MNNModelResourceProfile.Benchmarks
```

```csharp
public MNN.Unity.MNNModelCacheProfile[] MNN.Unity.MNNModelResourceProfile.Cache
```

```csharp
public string[] MNN.Unity.MNNModelResourceProfile.Capabilities
```

```csharp
public long MNN.Unity.MNNModelResourceProfile.DownloadBytes
```

```csharp
public string MNN.Unity.MNNModelResourceProfile.Evidence
```

```csharp
public string MNN.Unity.MNNModelResourceProfile.ExportIdentity
```

```csharp
public long MNN.Unity.MNNModelResourceProfile.HostStagingBytes
```

```csharp
public string MNN.Unity.MNNModelResourceProfile.Id
```

```csharp
public string[] MNN.Unity.MNNModelResourceProfile.Languages
```

```csharp
public int MNN.Unity.MNNModelResourceProfile.MaximumContextTokens
```

```csharp
public double MNN.Unity.MNNModelResourceProfile.OperationsPerUnit
```

```csharp
public long MNN.Unity.MNNModelResourceProfile.ParameterCount
```

```csharp
public long MNN.Unity.MNNModelResourceProfile.PeakWorkspaceBytes
```

```csharp
public double MNN.Unity.MNNModelResourceProfile.PrefillBytesPerToken
```

```csharp
public double MNN.Unity.MNNModelResourceProfile.QualityScore
```

```csharp
public int MNN.Unity.MNNModelResourceProfile.QuantizationBytesPerGroup
```

```csharp
public int MNN.Unity.MNNModelResourceProfile.QuantizationGroupSize
```

```csharp
public bool MNN.Unity.MNNModelResourceProfile.RuntimeAvailable
```

```csharp
public MNN.Unity.MNNModelMemoryKind MNN.Unity.MNNModelResourceProfile.SupportedMemoryKinds
```

```csharp
public MNN.Unity.MNNModelRecommendationTask MNN.Unity.MNNModelResourceProfile.Task
```

```csharp
public int MNN.Unity.MNNModelResourceProfile.WeightBits
```

```csharp
public long MNN.Unity.MNNModelResourceProfile.WeightBytes
```

```csharp
public double MNN.Unity.MNNModelResourceProfile.WeightReadBytesPerUnit
```

```csharp
public double MNN.Unity.MNNModelResourceProfile.WorkspaceBytesPerInputUnit
```

```csharp
MNN.Unity.MNNModelSource.HuggingFace = 1
```

```csharp
MNN.Unity.MNNModelSource.ModelScope = 0
```

```csharp
public string MNN.Unity.MNNModelTransferFile.Path
```

```csharp
public string MNN.Unity.MNNModelTransferFile.Revision
```

```csharp
public string MNN.Unity.MNNModelTransferFile.Sha256
```

```csharp
public long MNN.Unity.MNNModelTransferFile.Size
```

```csharp
public System.Func<string, long> MNN.Unity.MNNModelTransferOptions.AvailableSpaceBytes
```

Optional platform-specific destination-volume query. Return available bytes; called on workers.

```csharp
public long MNN.Unity.MNNModelTransferOptions.DiskSpaceReserveBytes
```

Free bytes kept for manifests, receipts and other application writes. Defaults to 16 MiB.

```csharp
public int MNN.Unity.MNNModelTransferOptions.MaxConnections
```

```csharp
public long MNN.Unity.MNNModelTransferOptions.ParallelThresholdBytes
```

```csharp
public bool MNN.Unity.MNNModelTransferOptions.PreservePartialDownloads
```

```csharp
public int MNN.Unity.MNNModelTransferOptions.RetryCount
```

```csharp
public bool MNN.Unity.MNNModelWorkload.AllowEstimatedPerformance
```

```csharp
public string[] MNN.Unity.MNNModelWorkload.AllowedModelIds
```

```csharp
public int MNN.Unity.MNNModelWorkload.ConcurrentSequences
```

Total retained prompt + generated tokens per sequence; never the model's advertised maximum.

```csharp
public int MNN.Unity.MNNModelWorkload.ContextTokens
```

Total retained prompt + generated tokens per sequence; never the model's advertised maximum.

```csharp
public double MNN.Unity.MNNModelWorkload.InputUnits
```

Seconds of audio, image count, etc.; units must match the profile.

```csharp
public double MNN.Unity.MNNModelWorkload.MaximumFirstResultSeconds
```

Seconds of audio, image count, etc.; units must match the profile.

```csharp
public double MNN.Unity.MNNModelWorkload.MinimumThroughputPerSecond
```

Seconds of audio, image count, etc.; units must match the profile.

```csharp
public int MNN.Unity.MNNModelWorkload.PrefillChunkTokens
```

Total retained prompt + generated tokens per sequence; never the model's advertised maximum.

```csharp
public int MNN.Unity.MNNModelWorkload.PromptTokens
```

Total retained prompt + generated tokens per sequence; never the model's advertised maximum.

```csharp
public string[] MNN.Unity.MNNModelWorkload.RequiredCapabilities
```

```csharp
public string[] MNN.Unity.MNNModelWorkload.RequiredLanguages
```

```csharp
public MNN.Unity.MNNModelRecommendationTask MNN.Unity.MNNModelWorkload.Task
```

```csharp
public bool MNN.Unity.MNNOnlineModelCache.Discovery.HuggingFaceHasMore
```

```csharp
public string MNN.Unity.MNNOnlineModelCache.Discovery.HuggingFaceNextCursor
```

```csharp
public int MNN.Unity.MNNOnlineModelCache.Discovery.HuggingFaceStream
```

```csharp
public int MNN.Unity.MNNOnlineModelCache.Discovery.ModelScopeNextPage
```

```csharp
public int MNN.Unity.MNNOnlineModelCache.Discovery.ModelScopeStream
```

```csharp
public MNN.Unity.MNNRemoteModel[] MNN.Unity.MNNOnlineModelCache.Discovery.Models
```

```csharp
public bool MNN.Unity.MNNOnlineModelCache.Discovery.Succeeded
```

```csharp
public string[] MNN.Unity.MNNOnlineModelCache.Discovery.Warnings
```

```csharp
public bool MNN.Unity.MNNOnlineModelCache.Snapshot.huggingFaceHasMore
```

```csharp
public string MNN.Unity.MNNOnlineModelCache.Snapshot.huggingFaceNextCursor
```

```csharp
public int MNN.Unity.MNNOnlineModelCache.Snapshot.huggingFaceStream
```

```csharp
public int MNN.Unity.MNNOnlineModelCache.Snapshot.modelScopeNextPage
```

```csharp
public int MNN.Unity.MNNOnlineModelCache.Snapshot.modelScopeStream
```

```csharp
public MNN.Unity.MNNRemoteModel[] MNN.Unity.MNNOnlineModelCache.Snapshot.models
```

```csharp
public int MNN.Unity.MNNOnlineModelCache.Snapshot.schemaVersion
```

```csharp
public long MNN.Unity.MNNOnlineModelCache.Snapshot.updatedUtcTicks
```

```csharp
public string[] MNN.Unity.MNNOnlineModelCache.Snapshot.warnings
```

```csharp
public MNN.Unity.MNNConversationModelAdapter MNN.Unity.MNNPinnedModelManifest.adapter
```

```csharp
public MNN.Unity.MNNModelAssetFile MNN.Unity.MNNPinnedModelManifest.archive
```

```csharp
public string MNN.Unity.MNNPinnedModelManifest.archiveDirectory
```

Optional directory containing the extracted file layout, relative to the model root. Empty installs directly in the owned model root; CosyVoice compatibility uses "model".

```csharp
public MNN.Unity.MNNModelAssetFile[] MNN.Unity.MNNPinnedModelManifest.files
```

```csharp
public string MNN.Unity.MNNPinnedModelManifest.modelId
```

```csharp
public string MNN.Unity.MNNPinnedModelManifest.revision
```

```csharp
public MNN.Unity.MNNConversationModelAdapter MNN.Unity.MNNPinnedModelMetadata.ManifestEntry.adapter
```

```csharp
public long MNN.Unity.MNNPinnedModelMetadata.ManifestEntry.bytes
```

```csharp
public string MNN.Unity.MNNPinnedModelMetadata.ManifestEntry.modelId
```

```csharp
public bool MNN.Unity.MNNPinnedModelMetadata.ManifestEntry.modelScope
```

```csharp
public string MNN.Unity.MNNPinnedModelMetadata.ManifestEntry.proofHash
```

```csharp
public string MNN.Unity.MNNPinnedModelMetadata.ManifestEntry.proofPath
```

```csharp
public string MNN.Unity.MNNPinnedModelMetadata.ManifestEntry.repository
```

```csharp
public string MNN.Unity.MNNPinnedModelMetadata.ManifestEntry.revision
```

```csharp
public bool MNN.Unity.MNNPinnedModelMetadata.Proof.ModelScope
```

```csharp
public string MNN.Unity.MNNPinnedModelMetadata.Proof.Path
```

```csharp
public string MNN.Unity.MNNPinnedModelMetadata.Proof.Repository
```

```csharp
public string MNN.Unity.MNNPinnedModelMetadata.Proof.Revision
```

```csharp
public System.Action<bool> MNN.Unity.MNNPinnedModelMetadata.Proof.SetOnline
```

```csharp
public string MNN.Unity.MNNPinnedModelMetadata.Proof.Sha256
```

```csharp
public string MNN.Unity.MNNRemoteModel.author
```

```csharp
public string MNN.Unity.MNNRemoteModel.category
```

```csharp
public string MNN.Unity.MNNRemoteModel.description
```

```csharp
public string MNN.Unity.MNNRemoteModel.displayName
```

```csharp
public long MNN.Unity.MNNRemoteModel.downloadCount
```

```csharp
public string MNN.Unity.MNNRemoteModel.downloadUrl
```

```csharp
public string MNN.Unity.MNNRemoteModel.family
```

```csharp
public long MNN.Unity.MNNRemoteModel.fileSize
```

```csharp
public string MNN.Unity.MNNRemoteModel.generation
```

```csharp
public string MNN.Unity.MNNRemoteModel.id
```

```csharp
public MNN.Unity.MNNModelInputSpec MNN.Unity.MNNRemoteModel.inputSpec
```

```csharp
public bool MNN.Unity.MNNRemoteModel.isRepository
```

```csharp
public string[] MNN.Unity.MNNRemoteModel.mnnFiles
```

```csharp
public string MNN.Unity.MNNRemoteModel.modelScopeId
```

```csharp
public string MNN.Unity.MNNRemoteModel.name
```

```csharp
public MNN.Unity.MNNModelOutputSpec MNN.Unity.MNNRemoteModel.outputSpec
```

```csharp
public long MNN.Unity.MNNRemoteModel.parameterCount
```

```csharp
public bool MNN.Unity.MNNRemoteModel.parameterCountInferred
```

```csharp
public string MNN.Unity.MNNRemoteModel.previewImage
```

```csharp
public MNN.Unity.MNNModelSource MNN.Unity.MNNRemoteModel.provider
```

```csharp
public string MNN.Unity.MNNRemoteModel.repositoryId
```

```csharp
public bool MNN.Unity.MNNRemoteModel.requiresTranscript
```

```csharp
public string MNN.Unity.MNNRemoteModel.revision
```

```csharp
public bool MNN.Unity.MNNRemoteModel.runtimeAvailable
```

```csharp
public string MNN.Unity.MNNRemoteModel.supportSummary
```

```csharp
public string[] MNN.Unity.MNNRemoteModel.tags
```

```csharp
public string[] MNN.Unity.MNNRemoteModel.tasks
```

```csharp
public string MNN.Unity.MNNRemoteModel.version
```

```csharp
MNN.Unity.ModelStorageLocation.Custom = 3
```

自定义绝对路径

```csharp
MNN.Unity.ModelStorageLocation.PersistentData = 1
```

PersistentData（可写目录）

```csharp
MNN.Unity.ModelStorageLocation.Resources = 2
```

Resources（资源文件夹，不推荐大文件）

```csharp
MNN.Unity.ModelStorageLocation.StreamingAssets = 0
```

StreamingAssets（随包）

```csharp
public bool MNN.Unity.PlatformModelConfig.enableGPU
```

```csharp
public UnityEngine.RuntimePlatform MNN.Unity.PlatformModelConfig.platform
```

```csharp
public string MNN.Unity.PlatformModelConfig.platformDownloadUrl
```

```csharp
public string MNN.Unity.PlatformModelConfig.platformModelFileName
```

```csharp
public MNN.Unity.ModelStorageLocation MNN.Unity.PlatformModelConfig.platformStorageLocation
```

```csharp
public static System.Collections.Generic.IReadOnlyList<MNN.Unity.MNNAcousticModelCatalog.Entry> MNN.Unity.MNNAcousticModelCatalog.Discover(System.Collections.Generic.IEnumerable<string> directories)
```

```csharp
public MNN.Unity.MNNAcousticModelCatalog.Entry.Entry()
```

```csharp
public MNN.Unity.MNNAcousticModelTask.MNNAcousticModelTask()
```

```csharp
public MNN.Unity.MNNBundledModelStorage.MNNBundledModelStorage()
```

```csharp
public MNN.Unity.MNNConversationModelAdapter.MNNConversationModelAdapter()
```

```csharp
public MNN.Unity.MNNConversationModelCatalog.MNNConversationModelCatalog()
```

```csharp
public MNN.Unity.MNNConversationModelCatalog.Entry MNN.Unity.MNNConversationModelCatalog.AddCatalogModel(MNN.Unity.MNNRemoteModel model)
```

```csharp
public MNN.Unity.MNNConversationModelCatalog.Entry MNN.Unity.MNNConversationModelCatalog.AddRemote(MNN.Unity.MNNRemoteModel model)
```

```csharp
public void MNN.Unity.MNNConversationModelCatalog.ApplyOnlineModels(System.Collections.Generic.IEnumerable<MNN.Unity.MNNRemoteModel> models, System.Collections.Generic.IEnumerable<string> warnings)
```

```csharp
public static string MNN.Unity.MNNConversationModelCatalog.DefaultInstallationName(MNN.Unity.MNNRemoteModel model)
```

```csharp
public static string MNN.Unity.MNNConversationModelCatalog.DefaultRevision(MNN.Unity.MNNModelSource source)
```

```csharp
public MNN.Unity.MNNConversationModelCatalog.Entry.Entry()
```

```csharp
public static bool MNN.Unity.MNNConversationModelCatalog.IsCatalogModel(MNN.Unity.MNNRemoteModel model)
```

```csharp
public static bool MNN.Unity.MNNConversationModelCatalog.IsDtlnModel(MNN.Unity.MNNRemoteModel model)
```

```csharp
public System.Threading.Tasks.Task<int> MNN.Unity.MNNConversationModelCatalog.RefreshAsync(System.Threading.CancellationToken token, System.Net.Http.HttpClient client = null)
```

```csharp
public static bool MNN.Unity.MNNConversationModelCatalog.TryGetAdapter(MNN.Unity.MNNRemoteModel model, out MNN.Unity.MNNConversationModelAdapter adapter)
```

```csharp
public static string MNN.Unity.MNNConversationModelFiles.FindExistingDirectory(string root, MNN.Unity.MNNRemoteModel model, string target)
```

```csharp
public static bool MNN.Unity.MNNConversationModelFiles.IsExistingComplete(string directory, MNN.Unity.MNNRemoteModel model)
```

```csharp
public MNN.Unity.MNNHuggingFacePage.MNNHuggingFacePage()
```

```csharp
public MNN.Unity.MNNHuggingFaceSource.MNNHuggingFaceSource(string name, string apiRoot, string fileRoot = null, bool supportsApi = true)
```

```csharp
public string MNN.Unity.MNNHuggingFaceSource.GetFileUrl(string repositoryId, string revision, string path)
```

```csharp
public MNN.Unity.MNNHuggingFaceSourcePool.MNNHuggingFaceSourcePool(System.Collections.Generic.IEnumerable<MNN.Unity.MNNHuggingFaceSource> mirrors = null, System.TimeSpan? failureCooldown = null)
```

```csharp
public void MNN.Unity.MNNHuggingFaceSourcePool.Add(MNN.Unity.MNNHuggingFaceSource mirror)
```

```csharp
public System.Collections.Generic.IReadOnlyList<MNN.Unity.MNNHuggingFaceSource> MNN.Unity.MNNHuggingFaceSourcePool.GetApiCandidates()
```

```csharp
public System.Collections.Generic.IReadOnlyList<MNN.Unity.MNNHuggingFaceSource> MNN.Unity.MNNHuggingFaceSourcePool.GetApiProbeCandidates()
```

```csharp
public System.Collections.Generic.IReadOnlyList<MNN.Unity.MNNHuggingFaceSource> MNN.Unity.MNNHuggingFaceSourcePool.GetFileCandidates()
```

```csharp
public System.Threading.Tasks.Task<int> MNN.Unity.MNNHuggingFaceSourcePool.RefreshFromWebAsync(System.Net.Http.HttpClient client, System.Collections.Generic.IEnumerable<string> indexUrls, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken))
```

Reads HTTPS origins from operator-supplied pages or plain-text indexes. Only origins are accepted; paths and credentials are discarded before a source is added to the pool.

```csharp
public void MNN.Unity.MNNHuggingFaceSourcePool.ReportFailure(MNN.Unity.MNNHuggingFaceSource mirror)
```

```csharp
public void MNN.Unity.MNNHuggingFaceSourcePool.ReportSuccess(MNN.Unity.MNNHuggingFaceSource mirror)
```

```csharp
public static void MNN.Unity.MNNModelArchiveInstaller.Install(string archivePath, string archiveSha256, string destination, System.Collections.Generic.IReadOnlyList<MNN.Unity.MNNModelAssetFile> requiredFiles, System.Threading.CancellationToken token = default(System.Threading.CancellationToken), System.Action<long> onExtracted = null)
```

Installs exact relative paths. A single enclosing ZIP folder may be removed when it yields the exact declared layout. File SHA-256 values are required. Cancellation is observed during verification and extraction, before the short publication step.

```csharp
public static System.Threading.Tasks.Task<string> MNN.Unity.MNNModelAssetDownloader.EnsureAsync(string directory, MNN.Unity.MNNModelAssetFile file, System.Action<long, long> progress = null, System.Threading.CancellationToken token = default(System.Threading.CancellationToken))
```

```csharp
public static string MNN.Unity.MNNModelAssetDownloader.ResolvePath(string directory, string relative)
```

```csharp
public MNN.Unity.MNNModelAssetFile.MNNModelAssetFile()
```

```csharp
public MNN.Unity.MNNModelBenchmark.MNNModelBenchmark()
```

```csharp
public MNN.Unity.MNNModelCacheProfile.MNNModelCacheProfile()
```

```csharp
public MNN.Unity.MNNModelCatalog.MNNModelCatalog()
```

```csharp
public MNN.Unity.MNNModelCatalog.MNNModelCatalog(System.Collections.Generic.IEnumerable<MNN.Unity.MNNRemoteModel> initialModels)
```

Start a catalog from an existing collection without sharing its mutable list.

```csharp
public static string MNN.Unity.MNNModelCatalog.Classify(string id, string name = null, string description = null, string[] tasks = null, string[] tags = null, string[] paths = null)
```

```csharp
public static MNN.Unity.MNNModelCatalog MNN.Unity.MNNModelCatalog.CreateDefault()
```

```csharp
public System.Threading.Tasks.Task MNN.Unity.MNNModelCatalog.DiscoverHuggingFaceModelsAsync(System.Net.Http.HttpClient httpClient = null, MNN.Unity.MNNHuggingFaceSourcePool mirrorPool = null, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken))
```

Search all public HF owners. Follow Link pagination and publish only a complete refresh.

```csharp
public System.Threading.Tasks.Task MNN.Unity.MNNModelCatalog.DiscoverModelScopeModelsAsync(System.Net.Http.HttpClient httpClient = null, string apiRoot = null, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken))
```

Search public ModelScope repositories and inspect manifests without downloading weights.

```csharp
public System.Threading.Tasks.Task<MNN.Unity.MNNModelCatalogPage> MNN.Unity.MNNModelCatalog.FetchCatalogPageAsync(MNN.Unity.MNNModelSource source, int stream = 0, int page = 1, string cursor = null, string query = null, System.Net.Http.HttpClient httpClient = null, string modelScopeApiRoot = null, MNN.Unity.MNNHuggingFaceSourcePool mirrorPool = null, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken))
```

Shared provider search and continuation policy for Editor and Player consumers.

```csharp
public System.Threading.Tasks.Task<MNN.Unity.MNNHuggingFacePage> MNN.Unity.MNNModelCatalog.FetchHuggingFacePageAsync(string cursor = null, System.Net.Http.HttpClient httpClient = null, MNN.Unity.MNNHuggingFaceSourcePool mirrorPool = null, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken))
```

Fetches one Hugging Face page, following a validated continuation only when requested.

```csharp
public System.Threading.Tasks.Task<MNN.Unity.MNNHuggingFacePage> MNN.Unity.MNNModelCatalog.FetchHuggingFaceSearchPageAsync(int stream, string cursor, string search, System.Net.Http.HttpClient httpClient = null, MNN.Unity.MNNHuggingFaceSourcePool mirrorPool = null, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken))
```

Fetch a page from an official, format-tag, name, or user search stream.

```csharp
public System.Threading.Tasks.Task<MNN.Unity.MNNModelScopePage> MNN.Unity.MNNModelCatalog.FetchModelScopePageAsync(int page, System.Net.Http.HttpClient httpClient = null, string apiRoot = null, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken))
```

Fetches one page of public MNN repositories without downloading model weights.

```csharp
public System.Threading.Tasks.Task<MNN.Unity.MNNModelScopePage> MNN.Unity.MNNModelCatalog.FetchModelScopeSearchPageAsync(int stream, int page, string search, System.Net.Http.HttpClient httpClient = null, string apiRoot = null, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken))
```

Fetch a page from an official, format-tag, name, or user search stream.

```csharp
public System.Collections.Generic.List<MNN.Unity.MNNRemoteModel> MNN.Unity.MNNModelCatalog.GetModelsByCategory(MNN.Unity.MNNModelCatalogCategory category)
```

```csharp
public static bool MNN.Unity.MNNModelCatalog.HasMnnArtifacts(System.Collections.Generic.IEnumerable<string> paths)
```

```csharp
public static bool MNN.Unity.MNNModelCatalog.HasMnnRepositoryArtifacts(string id, string[] tags, System.Collections.Generic.IEnumerable<string> paths)
```

```csharp
public void MNN.Unity.MNNModelCatalog.IncludeCosyVoice300MProfile()
```

```csharp
public void MNN.Unity.MNNModelCatalog.IncludeDtlnAecProfiles()
```

```csharp
public void MNN.Unity.MNNModelCatalog.IncludePocketProfiles()
```

```csharp
public void MNN.Unity.MNNModelCatalog.IncludePublishedSpeechProfiles()
```

```csharp
public static string MNN.Unity.MNNModelCatalog.InstallFolderName(MNN.Unity.MNNRemoteModel model)
```

```csharp
public static bool MNN.Unity.MNNModelCatalog.IsAudioProcessingCategory(string category)
```

```csharp
public static bool MNN.Unity.MNNModelCatalog.IsKnownArchiveSearchFalsePositive(string id, System.Collections.Generic.IEnumerable<string> paths)
```

```csharp
public static bool MNN.Unity.MNNModelCatalog.IsPublishedSpeechRepository(string repositoryId)
```

```csharp
public static bool MNN.Unity.MNNModelCatalog.IsSpecificAudioCategory(string category)
```

```csharp
public System.Threading.Tasks.Task<MNN.Unity.MNNRemoteModel> MNN.Unity.MNNModelCatalog.LoadHuggingFaceModelAsync(string repositoryId, System.Net.Http.HttpClient httpClient, MNN.Unity.MNNHuggingFaceSourcePool mirrorPool, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken))
```

```csharp
public System.Threading.Tasks.Task MNN.Unity.MNNModelCatalog.LoadHuggingFaceModelsAsync(System.Collections.Generic.IEnumerable<string> repositoryIds = null, System.Net.Http.HttpClient httpClient = null, MNN.Unity.MNNHuggingFaceSourcePool mirrorPool = null, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken))
```

```csharp
public System.Threading.Tasks.Task MNN.Unity.MNNModelCatalog.LoadModelScopeModelsAsync(System.Net.Http.HttpClient httpClient = null, string apiRoot = null, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken))
```

```csharp
public static bool MNN.Unity.MNNModelCatalog.MatchesInstallMarker(MNN.Unity.MNNRemoteModel model, string marker)
```

```csharp
public static bool MNN.Unity.MNNModelCatalog.MatchesSearch(MNN.Unity.MNNRemoteModel model, string query)
```

Shared local search semantics for catalog consumers, including audio task aliases.

```csharp
public static bool MNN.Unity.MNNModelCatalog.MatchesSearch(string query, string name, string displayName, string description, string modelScopeId, string repositoryId, string[] tags, string[] tasks, string category)
```

```csharp
public System.Collections.Generic.List<MNN.Unity.MNNRemoteModel> MNN.Unity.MNNModelCatalog.SearchModels(string query)
```

```csharp
public MNN.Unity.MNNModelCatalogCategory.MNNModelCatalogCategory()
```

```csharp
public MNN.Unity.MNNModelCatalogPage.MNNModelCatalogPage()
```

```csharp
public MNN.Unity.MNNModelConfig.MNNModelConfig()
```

```csharp
public string MNN.Unity.MNNModelConfig.GetEffectiveDownloadUrl()
```

```csharp
public string MNN.Unity.MNNModelConfig.GetEffectiveModelFileName()
```

```csharp
public MNN.Unity.ModelStorageLocation MNN.Unity.MNNModelConfig.GetEffectiveStorageLocation()
```

```csharp
public string MNN.Unity.MNNModelConfig.GetModelPath()
```

获取模型的完整加载路径

```csharp
public string MNN.Unity.MNNModelConfig.GetPersistentPath()
```

获取模型的持久化存储路径

```csharp
public MNN.Unity.PlatformModelConfig MNN.Unity.MNNModelConfig.GetPlatformConfig()
```

获取当前平台配置

```csharp
public string MNN.Unity.MNNModelConfig.GetStreamingAssetsPath()
```

获取StreamingAssets路径

```csharp
public bool MNN.Unity.MNNModelConfig.IsModelAvailable()
```

检查模型是否存在

```csharp
public System.Threading.Tasks.Task<string> MNN.Unity.MNNModelConfig.PrepareAsync(System.Action<long, long> progress = null, System.Threading.CancellationToken token = default(System.Threading.CancellationToken))
```

Prepares a declared build model for native file access; call from Unity's main thread.

```csharp
public MNN.Unity.MNNModelDelivery.MNNModelDelivery(MNN.Unity.MNNModelDeliveryManifest manifest, string streamingAssetsRoot, string persistentModelsRoot)
```

```csharp
public static System.Threading.Tasks.Task<MNN.Unity.MNNModelDelivery> MNN.Unity.MNNModelDelivery.LoadAsync(System.Threading.CancellationToken token = default(System.Threading.CancellationToken))
```

```csharp
public System.Threading.Tasks.Task<string> MNN.Unity.MNNModelDelivery.PrepareDirectoryAsync(string relativeDirectory, System.Action<long, long> progress = null, System.Threading.CancellationToken token = default(System.Threading.CancellationToken))
```

Seeds every declared dependency into one writable directory, including files inside Android APKs.

```csharp
public System.Threading.Tasks.Task<string> MNN.Unity.MNNModelDelivery.PrepareFileAsync(string relativePath, System.Action<long, long> progress = null, System.Threading.CancellationToken token = default(System.Threading.CancellationToken), bool copyToPersistentData = false)
```

```csharp
public MNN.Unity.MNNModelDeliveryFile.MNNModelDeliveryFile()
```

```csharp
public MNN.Unity.MNNModelDeliveryManifest.MNNModelDeliveryManifest()
```

```csharp
public static string MNN.Unity.MNNModelDeliveryManifest.ComputeSha256(string path, System.Threading.CancellationToken token = default(System.Threading.CancellationToken))
```

```csharp
public void MNN.Unity.MNNModelDeliveryManifest.Validate()
```

```csharp
public MNN.Unity.MNNModelDownloadProgress.MNNModelDownloadProgress(System.Func<double> clock = null)
```

```csharp
public void MNN.Unity.MNNModelDownloadProgress.Add(string key, string name, string source, long estimatedBytes, string localDirectory = null)
```

```csharp
public MNN.Unity.MNNModelDownloadProgress.Phase.Phase()
```

```csharp
public static string MNN.Unity.MNNModelDownloadProgress.PhaseText(MNN.Unity.MNNModelDownloadProgress.Phase phase)
```

```csharp
public MNN.Unity.MNNModelDownloadProgress.Snapshot MNN.Unity.MNNModelDownloadProgress.Read()
```

```csharp
public void MNN.Unity.MNNModelDownloadProgress.Report(string key, long received, long total, string file)
```

```csharp
public void MNN.Unity.MNNModelDownloadProgress.SetPhase(string key, MNN.Unity.MNNModelDownloadProgress.Phase phase, string directory = null)
```

```csharp
public MNN.Unity.MNNModelDownloadProgress.Snapshot.Snapshot(int models, int reused, int complete, int downloaded, long received, long total, bool known, bool modelReused, bool estimated, float fraction, double speed, double elapsed, string name, string source, string file, string directory, MNN.Unity.MNNModelDownloadProgress.Phase state, long modelReceived, long modelTotal, System.Collections.Generic.KeyValuePair<string, MNN.Unity.MNNModelDownloadProgress.Phase>[] queue)
```

```csharp
public static string MNN.Unity.MNNModelDownloadProgress.Snapshot.Bytes(long bytes)
```

```csharp
public static string MNN.Unity.MNNModelDownloadProgress.Snapshot.Duration(double seconds)
```

```csharp
public MNN.Unity.MNNModelDownloader.MNNModelDownloader(System.Net.Http.HttpClient httpClient = null, string apiRoot = null, MNN.Unity.MNNModelTransferOptions options = null, MNN.Unity.MNNHuggingFaceSourcePool mirrorPool = null)
```

```csharp
public void MNN.Unity.MNNModelDownloader.Dispose()
```

```csharp
public System.Threading.Tasks.Task<string> MNN.Unity.MNNModelDownloader.DownloadAsync(MNN.Unity.MNNModelSource source, string repositoryId, string directory, string revision = null, System.Action<long, long, string> onProgress = null, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken))
```

Download from ModelScope or Hugging Face, then run the same preparation used by Model Manager.

```csharp
public System.Threading.Tasks.Task<string> MNN.Unity.MNNModelDownloader.DownloadAsync(MNN.Unity.MNNRemoteModel model, string directory, System.Action<long, long, string> onProgress = null, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken))
```

Download a catalog entry and prepare its supported inference profile. Returns the absolute model directory.

```csharp
public System.Threading.Tasks.Task<long> MNN.Unity.MNNModelDownloader.DownloadDocumentLayoutProfileAsync(string repository, string directory, System.Action<long, long, string> onProgress, System.Threading.CancellationToken cancellationToken)
```

```csharp
public System.Threading.Tasks.Task<long> MNN.Unity.MNNModelDownloader.DownloadHuggingFaceRepositoryAsync(string repositoryId, string stagingDirectory, string revision = "main", System.Action<long, long, string> onProgress = null, MNN.Unity.MNNHuggingFaceSourcePool mirrorPool = null, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken))
```

```csharp
public System.Threading.Tasks.Task<long> MNN.Unity.MNNModelDownloader.DownloadImageEmbeddingProfileAsync(string repository, string directory, System.Action<long, long, string> onProgress, System.Threading.CancellationToken cancellationToken)
```

```csharp
public System.Threading.Tasks.Task<long> MNN.Unity.MNNModelDownloader.DownloadImageProfileAsync(string repository, string directory, System.Action<long, long, string> onProgress, System.Threading.CancellationToken cancellationToken)
```

```csharp
public System.Threading.Tasks.Task<long> MNN.Unity.MNNModelDownloader.DownloadRepositoryAsync(string modelScopeId, string stagingDirectory, System.Action<long, long, string> onProgress = null, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken), string revision = "master")
```

```csharp
public System.Threading.Tasks.Task<long> MNN.Unity.MNNModelDownloader.DownloadRepositoryFileAsync(string modelScopeId, MNN.Unity.MNNModelRepositoryFile file, string destination, System.Action<long, long, string> onProgress = null, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken))
```

```csharp
public static MNN.Unity.MNNModelRepositoryFile[] MNN.Unity.MNNModelDownloader.DtlnAecDownloadPlan(string repository)
```

```csharp
public System.Threading.Tasks.Task<MNN.Unity.MNNModelRepositoryFile[]> MNN.Unity.MNNModelDownloader.GetRepositoryFilesAsync(string modelScopeId, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken), string revision = "master")
```

```csharp
public static void MNN.Unity.MNNModelDownloader.PrepareArchives(string directory, MNN.Unity.MNNModelSource source, string repository, System.Threading.CancellationToken token = default(System.Threading.CancellationToken))
```

```csharp
public static MNN.Unity.MNNModelRepositoryFile[] MNN.Unity.MNNModelDownloader.ReferenceSpeechDownloadPlan(string repositoryId, string revision)
```

```csharp
public MNN.Unity.MNNModelFileTransfer.MNNModelFileTransfer(System.Net.Http.HttpClient client, MNN.Unity.MNNModelTransferOptions options = null)
```

```csharp
public void MNN.Unity.MNNModelFileTransfer.Dispose()
```

```csharp
public System.Threading.Tasks.Task<long> MNN.Unity.MNNModelFileTransfer.DownloadAsync(string modelScopeId, MNN.Unity.MNNModelTransferFile file, string destination, System.Action<long> onProgress, System.Threading.CancellationToken token, System.Func<string> urlProvider, System.Action<bool> sourceResult = null, int retryLimit = -1)
```

```csharp
public MNN.Unity.MNNModelHardware.MNNModelHardware()
```

```csharp
public static MNN.Unity.MNNModelHardware MNN.Unity.MNNModelHardware.Capture()
```

Main-thread Unity snapshot. Does not allocate models, request permissions, benchmark, or access the network.

```csharp
public MNN.Unity.MNNModelInputSpec.MNNModelInputSpec()
```

```csharp
public static string MNN.Unity.MNNModelInstallation.DirectoryFor(string root, MNN.Unity.MNNRemoteModel model, string installationName = null)
```

```csharp
public static System.Threading.Tasks.Task<string> MNN.Unity.MNNModelInstallation.EnsureAsync(string directory, MNN.Unity.MNNRemoteModel model, System.Action<long, long, string> progress = null, System.Threading.CancellationToken token = default(System.Threading.CancellationToken), MNN.Unity.MNNModelDownloader downloader = null, System.Action<MNN.Unity.MNNModelInstallation.Phase, string> state = null, System.Func<System.Threading.CancellationToken, System.Threading.Tasks.Task> download = null, System.Func<bool> canAdoptLocal = null)
```

```csharp
public static bool MNN.Unity.MNNModelInstallation.HasReceipt(string directory)
```

```csharp
public static bool MNN.Unity.MNNModelInstallation.HasRecordedFiles(string directory, MNN.Unity.MNNRemoteModel model)
```

Cheap file-presence hint for UI only. This skips SHA-256; EnsureAsync always verifies before reuse.

```csharp
public static bool MNN.Unity.MNNModelInstallation.IsInstalled(string directory, MNN.Unity.MNNRemoteModel model, System.Threading.CancellationToken token = default(System.Threading.CancellationToken))
```

```csharp
public MNN.Unity.MNNModelInstallation.Phase.Phase()
```

```csharp
public static System.Collections.Generic.IReadOnlyList<MNN.Unity.MNNRemoteModel> MNN.Unity.MNNModelInstallation.ReadInstalled(string root, System.Threading.CancellationToken token = default(System.Threading.CancellationToken))
```

```csharp
public static System.Collections.Generic.IReadOnlyList<MNN.Unity.MNNRemoteModel> MNN.Unity.MNNModelInstallation.ReadRecordedModels(string root)
```

Reads catalog metadata from receipts without hashing weights. Presence is not an integrity result.

```csharp
public MNN.Unity.MNNModelKind.MNNModelKind()
```

```csharp
public MNN.Unity.MNNModelMemoryKind.MNNModelMemoryKind()
```

```csharp
public MNN.Unity.MNNModelOutputSpec.MNNModelOutputSpec()
```

```csharp
public static void MNN.Unity.MNNModelPathResolver.EnsureDirectory(string path)
```

确保目录存在

```csharp
public static string MNN.Unity.MNNModelPathResolver.GetModelPath(MNN.Unity.MNNModelConfig config)
```

解析模型路径

```csharp
public static MNN.Unity.ModelStorageLocation MNN.Unity.MNNModelPathResolver.GetRecommendedStorageLocation()
```

获取推荐的存储位置

```csharp
public static MNN.Unity.MNNModelMemoryEstimate MNN.Unity.MNNModelRecommendation.Estimate(MNN.Unity.MNNModelResourceProfile profile, MNN.Unity.MNNModelWorkload workload, MNN.Unity.MNNModelRecommendationPolicy policy = null)
```

Resident weights + grouped KV/fixed state + peak workspace + safety margin. No native calls.

```csharp
public static System.Collections.Generic.IReadOnlyList<MNN.Unity.MNNModelRecommendationResult> MNN.Unity.MNNModelRecommendation.Rank(MNN.Unity.MNNModelHardware hardware, MNN.Unity.MNNModelWorkload workload, System.Collections.Generic.IEnumerable<MNN.Unity.MNNModelResourceProfile> profiles, MNN.Unity.MNNModelRecommendationPolicy policy = null)
```

```csharp
public static MNN.Unity.MNNModelRecommendationResult MNN.Unity.MNNModelRecommendation.Recommend(MNN.Unity.MNNModelHardware hardware, MNN.Unity.MNNModelWorkload workload, System.Collections.Generic.IEnumerable<MNN.Unity.MNNModelResourceProfile> profiles, MNN.Unity.MNNModelRecommendationPolicy policy = null)
```

```csharp
public static MNN.Unity.MNNModelRecommendationPlan MNN.Unity.MNNModelRecommendation.RecommendPlan(MNN.Unity.MNNModelHardware hardware, System.Collections.Generic.IEnumerable<MNN.Unity.MNNModelWorkload> workloads, System.Collections.Generic.IEnumerable<MNN.Unity.MNNModelResourceProfile> profiles, MNN.Unity.MNNModelRecommendationPolicy policy = null)
```

Bounded joint search; all stages remain resident. Never grants each stage the entire free memory pool.

```csharp
public MNN.Unity.MNNModelRecommendationPolicy.MNNModelRecommendationPolicy()
```

```csharp
public static System.Collections.Generic.IReadOnlyList<MNN.Unity.MNNModelResourceProfile> MNN.Unity.MNNModelRecommendationProfiles.CreateDefault()
```

```csharp
public MNN.Unity.MNNModelRecommendationStatus.MNNModelRecommendationStatus()
```

```csharp
public MNN.Unity.MNNModelRecommendationTask.MNNModelRecommendationTask()
```

```csharp
public MNN.Unity.MNNModelRepositoryFile.MNNModelRepositoryFile()
```

```csharp
public MNN.Unity.MNNModelRepositoryIdentity.MNNModelRepositoryIdentity()
```

```csharp
public static string MNN.Unity.MNNModelRepositoryIdentity.DefaultRevision(MNN.Unity.MNNModelSource source)
```

```csharp
public bool MNN.Unity.MNNModelRepositoryIdentity.Equals(MNN.Unity.MNNModelRepositoryIdentity other)
```

```csharp
public override bool MNN.Unity.MNNModelRepositoryIdentity.Equals(object obj)
```

```csharp
public static MNN.Unity.MNNModelRepositoryIdentity MNN.Unity.MNNModelRepositoryIdentity.FromModel(MNN.Unity.MNNRemoteModel model)
```

```csharp
public override int MNN.Unity.MNNModelRepositoryIdentity.GetHashCode()
```

```csharp
public override string MNN.Unity.MNNModelRepositoryIdentity.ToString()
```

```csharp
public static bool MNN.Unity.MNNModelRepositoryIdentity.TryCreate(MNN.Unity.MNNRemoteModel model, out MNN.Unity.MNNModelRepositoryIdentity identity)
```

```csharp
public MNN.Unity.MNNModelResourceProfile.MNNModelResourceProfile()
```

```csharp
public static string MNN.Unity.MNNModelRevisions.Resolve(MNN.Unity.MNNModelSource source, string repositoryId, string revision = null)
```

Resolves a supported repository's pinned revision; rejects incompatible revisions. Unknown repositories retain the requested revision or their provider's default branch.

```csharp
public MNN.Unity.MNNModelScopePage.MNNModelScopePage()
```

```csharp
public MNN.Unity.MNNModelSource.MNNModelSource()
```

```csharp
public static string MNN.Unity.MNNModelSources.DtlnAec(int units)
```

```csharp
public static bool MNN.Unity.MNNModelSources.IsCosyVoice(string repository)
```

```csharp
public static bool MNN.Unity.MNNModelSources.IsCosyVoice300M(string repository)
```

```csharp
public static bool MNN.Unity.MNNModelSources.IsHuggingFaceSpeech(string repository)
```

```csharp
public static bool MNN.Unity.MNNModelSources.IsReferenceSpeech(string repository)
```

```csharp
public static bool MNN.Unity.MNNModelSupport.IsComplete(MNN.Unity.MNNModelKind kind, string directory)
```

Checks the export's local layout and recorded sizes. This does not establish SHA integrity, platform compatibility, inference accuracy or device availability. It creates no files.

```csharp
public MNN.Unity.MNNModelTransferFile.MNNModelTransferFile()
```

```csharp
public MNN.Unity.MNNModelTransferOptions.MNNModelTransferOptions()
```

```csharp
public MNN.Unity.MNNModelWorkload.MNNModelWorkload()
```

```csharp
public MNN.Unity.MNNOnlineModelCache.MNNOnlineModelCache(string path, System.Func<System.Threading.CancellationToken, System.Threading.Tasks.Task<MNN.Unity.MNNOnlineModelCache.Discovery>> discover = null, System.Func<MNN.Unity.MNNOnlineModelCache.Snapshot, System.Threading.CancellationToken, System.Threading.Tasks.Task<MNN.Unity.MNNOnlineModelCache.Discovery>> discoverNext = null)
```

Opens a private cache synchronously. For UI use ForPath, whose disk snapshot loads in the background; await Ready or LoadAsync.

```csharp
public MNN.Unity.MNNOnlineModelCache.Discovery.Discovery()
```

```csharp
public static MNN.Unity.MNNOnlineModelCache MNN.Unity.MNNOnlineModelCache.ForPath(string path)
```

```csharp
public System.Threading.Tasks.Task<MNN.Unity.MNNOnlineModelCache.Snapshot> MNN.Unity.MNNOnlineModelCache.LoadAsync(bool forceRefresh = false, System.Threading.CancellationToken token = default(System.Threading.CancellationToken))
```

```csharp
public System.Threading.Tasks.Task<MNN.Unity.MNNOnlineModelCache.Snapshot> MNN.Unity.MNNOnlineModelCache.LoadCompleteAsync(bool forceRefresh = false, System.Threading.CancellationToken token = default(System.Threading.CancellationToken), System.Action<MNN.Unity.MNNOnlineModelCache.Snapshot> available = null)
```

```csharp
public System.Threading.Tasks.Task<MNN.Unity.MNNOnlineModelCache.Snapshot> MNN.Unity.MNNOnlineModelCache.LoadMoreAsync(System.Threading.CancellationToken token = default(System.Threading.CancellationToken))
```

```csharp
public MNN.Unity.MNNOnlineModelCache.Snapshot MNN.Unity.MNNOnlineModelCache.Read()
```

```csharp
public MNN.Unity.MNNOnlineModelCache.Snapshot.Snapshot()
```

```csharp
public MNN.Unity.MNNPinnedModelManifest.MNNPinnedModelManifest()
```

```csharp
public System.Func<System.Threading.CancellationToken, System.Threading.Tasks.Task> MNN.Unity.MNNPinnedModelManifest.CaptureInstaller(string modelsDirectory, System.Action<string, long, long> progress = null, string bundledModelsDirectory = null)
```

```csharp
public System.Threading.Tasks.Task MNN.Unity.MNNPinnedModelManifest.EnsureAsync(string modelsDirectory, System.Threading.CancellationToken token)
```

```csharp
public static void MNN.Unity.MNNPinnedModelManifest.VerifyFiles(string directory, MNN.Unity.MNNModelAssetFile[] files, System.Threading.CancellationToken token)
```

```csharp
public static System.Collections.Generic.IReadOnlyList<MNN.Unity.MNNPinnedModelMetadata.ManifestEntry> MNN.Unity.MNNPinnedModelMetadata.Describe(System.Collections.Generic.IEnumerable<MNN.Unity.MNNPinnedModelManifest> manifests)
```

```csharp
public MNN.Unity.MNNPinnedModelMetadata.ManifestEntry.ManifestEntry()
```

```csharp
public MNN.Unity.MNNPinnedModelMetadata.Proof.Proof()
```

```csharp
public static System.Threading.Tasks.Task<int> MNN.Unity.MNNPinnedModelMetadata.RefreshAsync(System.Collections.Generic.IEnumerable<MNN.Unity.MNNPinnedModelMetadata.Proof> proofs, System.Threading.CancellationToken token, System.Net.Http.HttpClient client = null)
```

```csharp
public MNN.Unity.MNNRemoteModel.MNNRemoteModel()
```

```csharp
public static MNN.Unity.MNNRemoteModel MNN.Unity.MNNRemoteModelSnapshot.Copy(MNN.Unity.MNNRemoteModel model)
```

```csharp
public MNN.Unity.ModelStorageLocation.ModelStorageLocation()
```

```csharp
public MNN.Unity.PlatformModelConfig.PlatformModelConfig()
```

```csharp
public long MNN.Unity.MNNAcousticModelCatalog.Entry.Bytes { get; internal set; }
```

```csharp
public string MNN.Unity.MNNAcousticModelCatalog.Entry.Directory { get; internal set; }
```

```csharp
public MNN.Unity.MNNAcousticModelTask MNN.Unity.MNNAcousticModelCatalog.Entry.Task { get; internal set; }
```

```csharp
public bool MNN.Unity.MNNConversationModelCatalog.DiscoverySucceeded { get; }
```

```csharp
public System.Collections.Generic.IReadOnlyList<string> MNN.Unity.MNNConversationModelCatalog.DiscoveryWarnings { get; }
```

```csharp
public System.Collections.Generic.IReadOnlyList<MNN.Unity.MNNConversationModelCatalog.Entry> MNN.Unity.MNNConversationModelCatalog.Entries { get; }
```

```csharp
public System.Collections.Generic.IReadOnlyList<MNN.Unity.MNNRemoteModel> MNN.Unity.MNNHuggingFacePage.Models { get; internal set; }
```

```csharp
public string MNN.Unity.MNNHuggingFacePage.NextCursor { get; internal set; }
```

```csharp
public string MNN.Unity.MNNHuggingFaceSource.ApiRoot { get; }
```

```csharp
public string MNN.Unity.MNNHuggingFaceSource.FileRoot { get; }
```

```csharp
public string MNN.Unity.MNNHuggingFaceSource.Name { get; }
```

```csharp
public bool MNN.Unity.MNNHuggingFaceSource.SupportsApi { get; }
```

```csharp
public static MNN.Unity.MNNHuggingFaceSourcePool MNN.Unity.MNNHuggingFaceSourcePool.Shared { get; }
```

```csharp
public long MNN.Unity.MNNInsufficientDiskSpaceException.AvailableBytes { get; }
```

```csharp
public long MNN.Unity.MNNInsufficientDiskSpaceException.BytesToFree { get; }
```

```csharp
public string MNN.Unity.MNNInsufficientDiskSpaceException.DirectoryPath { get; }
```

```csharp
public string MNN.Unity.MNNInsufficientDiskSpaceException.RepositoryId { get; }
```

```csharp
public long MNN.Unity.MNNInsufficientDiskSpaceException.RequiredBytes { get; }
```

Additional free bytes required, including assembly workspace and the configured reserve.

```csharp
public System.Collections.Generic.IReadOnlyList<string> MNN.Unity.MNNModelCatalog.DiscoveryWarnings { get; }
```

```csharp
public System.Collections.Generic.IReadOnlyList<MNN.Unity.MNNRemoteModel> MNN.Unity.MNNModelCatalog.Models { get; }
```

```csharp
public bool MNN.Unity.MNNModelCatalogPage.HasMore { get; internal set; }
```

```csharp
public System.Collections.Generic.IReadOnlyList<MNN.Unity.MNNRemoteModel> MNN.Unity.MNNModelCatalogPage.Models { get; internal set; }
```

```csharp
public string MNN.Unity.MNNModelCatalogPage.NextCursor { get; internal set; }
```

```csharp
public int MNN.Unity.MNNModelCatalogPage.NextPage { get; internal set; }
```

```csharp
public int MNN.Unity.MNNModelCatalogPage.NextStream { get; internal set; }
```

```csharp
public System.Collections.Generic.IReadOnlyList<string> MNN.Unity.MNNModelCatalogPage.Warnings { get; internal set; }
```

```csharp
public string MNN.Unity.MNNModelDownloadProgress.Snapshot.Details { get; }
```

```csharp
public int MNN.Unity.MNNModelDownloadProgress.Snapshot.DownloadModels { get; }
```

```csharp
public string MNN.Unity.MNNModelDownloadProgress.Snapshot.Queue { get; }
```

```csharp
public string MNN.Unity.MNNModelDownloadProgress.Snapshot.Summary { get; }
```

```csharp
public long MNN.Unity.MNNModelMemoryEstimate.CacheBytes { get; }
```

```csharp
public long MNN.Unity.MNNModelMemoryEstimate.PeakBytes { get; }
```

```csharp
public long MNN.Unity.MNNModelMemoryEstimate.WeightBytes { get; }
```

```csharp
public long MNN.Unity.MNNModelMemoryEstimate.WorkspaceBytes { get; }
```

```csharp
public bool MNN.Unity.MNNModelRecommendationPlan.IsComplete { get; }
```

```csharp
public long MNN.Unity.MNNModelRecommendationPlan.PeakDeviceBytes { get; }
```

```csharp
public long MNN.Unity.MNNModelRecommendationPlan.PeakSystemBytes { get; }
```

```csharp
public System.Collections.Generic.IReadOnlyList<MNN.Unity.MNNModelRecommendationResult> MNN.Unity.MNNModelRecommendationPlan.Selections { get; }
```

```csharp
public System.Collections.Generic.IReadOnlyList<string> MNN.Unity.MNNModelRecommendationPlan.Warnings { get; }
```

```csharp
public string MNN.Unity.MNNModelRecommendationResult.Evidence { get; }
```

```csharp
public double MNN.Unity.MNNModelRecommendationResult.FirstResultSeconds { get; }
```

```csharp
public bool MNN.Unity.MNNModelRecommendationResult.HasMeasuredPerformance { get; }
```

```csharp
public MNN.Unity.MNNModelMemoryEstimate MNN.Unity.MNNModelRecommendationResult.Memory { get; }
```

```csharp
public string MNN.Unity.MNNModelRecommendationResult.ModelId { get; }
```

```csharp
public long MNN.Unity.MNNModelRecommendationResult.PeakDeviceBytes { get; }
```

```csharp
public long MNN.Unity.MNNModelRecommendationResult.PeakSystemBytes { get; }
```

```csharp
public System.Collections.Generic.IReadOnlyList<string> MNN.Unity.MNNModelRecommendationResult.Reasons { get; }
```

```csharp
public double MNN.Unity.MNNModelRecommendationResult.Score { get; }
```

```csharp
public MNN.Unity.MNNModelRecommendationStatus MNN.Unity.MNNModelRecommendationResult.Status { get; }
```

```csharp
public MNN.Unity.MNNModelRecommendationTask MNN.Unity.MNNModelRecommendationResult.Task { get; }
```

```csharp
public double MNN.Unity.MNNModelRecommendationResult.ThroughputPerSecond { get; }
```

```csharp
public string MNN.Unity.MNNModelRepositoryIdentity.Repository { get; }
```

```csharp
public string MNN.Unity.MNNModelRepositoryIdentity.Revision { get; }
```

```csharp
public MNN.Unity.MNNModelSource MNN.Unity.MNNModelRepositoryIdentity.Source { get; }
```

```csharp
public System.Collections.Generic.IReadOnlyList<MNN.Unity.MNNRemoteModel> MNN.Unity.MNNModelScopePage.Models { get; internal set; }
```

```csharp
public int MNN.Unity.MNNModelScopePage.NextPage { get; internal set; }
```

```csharp
public System.Collections.Generic.IReadOnlyList<string> MNN.Unity.MNNModelScopePage.Warnings { get; internal set; }
```

```csharp
public static string MNN.Unity.MNNModelSources.DocumentLayout { get; }
```

```csharp
public static string MNN.Unity.MNNModelSources.ImageEmbedding { get; }
```

```csharp
public static string MNN.Unity.MNNModelSources.Kws { get; }
```

```csharp
public static string MNN.Unity.MNNModelSources.Paraformer { get; }
```

```csharp
public static string MNN.Unity.MNNModelSources.ParaformerDuplicate { get; }
```

```csharp
public static string MNN.Unity.MNNModelSources.Qwen3Asr { get; }
```

```csharp
public static string MNN.Unity.MNNModelSources.Qwen3AsrModelScope { get; }
```

```csharp
public static string MNN.Unity.MNNModelSources.Qwen3Explicit { get; }
```

```csharp
public static string MNN.Unity.MNNModelSources.ReferenceSpeechFireRed { get; }
```

```csharp
public static string MNN.Unity.MNNModelSources.ReferenceSpeechIndex { get; }
```

```csharp
public static string MNN.Unity.MNNModelSources.SenseVoice { get; }
```

```csharp
public static string MNN.Unity.MNNModelSources.SenseVoiceHuggingFace { get; }
```

```csharp
public static string MNN.Unity.MNNModelSources.Silero { get; }
```

```csharp
public static string MNN.Unity.MNNModelSources.SmartTurn { get; }
```

```csharp
public static string MNN.Unity.MNNModelSources.Speaker { get; }
```

```csharp
public static string MNN.Unity.MNNModelSources.TextDecision { get; }
```

```csharp
public static string MNN.Unity.MNNModelSources.WeSpeaker { get; }
```

```csharp
public static string MNN.Unity.MNNModelSources.Whisper { get; }
```

```csharp
public static string MNN.Unity.MNNModelSources.YoloV8 { get; }
```

```csharp
public System.Threading.Tasks.Task MNN.Unity.MNNOnlineModelCache.Ready { get; }
```

```csharp
class MNN.Unity.MNNAcousticModelCatalog
```

Discovers structurally complete local acoustic exports, without loading models or claiming inference validation.

```csharp
class MNN.Unity.MNNAcousticModelCatalog.Entry
```

```csharp
enum MNN.Unity.MNNAcousticModelTask
```

```csharp
enum MNN.Unity.MNNBundledModelStorage
```

```csharp
enum MNN.Unity.MNNConversationModelAdapter
```

Registered execution protocols; catalog discovery alone does not imply a runnable export.

```csharp
class MNN.Unity.MNNConversationModelCatalog
```

Registered conversation export protocols and identity-indexed metadata. Product stages and UI stay outside this service.

```csharp
class MNN.Unity.MNNConversationModelCatalog.Entry
```

```csharp
class MNN.Unity.MNNConversationModelFiles
```

```csharp
class MNN.Unity.MNNHuggingFacePage
```

A single Hugging Face search page and its opaque continuation URL.

```csharp
class MNN.Unity.MNNHuggingFaceSource
```

```csharp
class MNN.Unity.MNNHuggingFaceSourcePool
```

Known and discovered Hugging Face-compatible sources with failure cooling.

```csharp
class MNN.Unity.MNNInsufficientDiskSpaceException
```

A model download cannot fit on its destination volume. Catch this at the application boundary.

```csharp
class MNN.Unity.MNNModelArchiveInstaller
```

Verifies an exact ZIP model layout in a private directory before publishing it. The destination is wholly owned by this installation. Callers serialize operations for that directory. An archive inside the destination is retained across publication. No archive metadata is trusted as a filesystem path.

```csharp
class MNN.Unity.MNNModelAssetDownloader
```

On-demand, bounded-memory model installation. Pinned SHA-256 is mandatory; partial files resume.

```csharp
class MNN.Unity.MNNModelAssetFile
```

```csharp
class MNN.Unity.MNNModelBenchmark
```

A measurement applies only to this exact model export, runtime, device and workload.

```csharp
class MNN.Unity.MNNModelCacheProfile
```

One homogeneous attention/cache group. Fixed state is independent of context length.

```csharp
class MNN.Unity.MNNModelCatalog
```

Runtime catalog discovery and search for public MNN model repositories.

```csharp
enum MNN.Unity.MNNModelCatalogCategory
```

```csharp
class MNN.Unity.MNNModelCatalogPage
```

A provider page with the complete continuation across official, tag and name streams.

```csharp
class MNN.Unity.MNNModelConfig
```

MNN模型资源配置 管理模型的存储位置、下载策略和加载路径

```csharp
class MNN.Unity.MNNModelDelivery
```

Call from Unity's main thread. Prepares verified model files before invoking any inference API.

```csharp
class MNN.Unity.MNNModelDeliveryFile
```

```csharp
class MNN.Unity.MNNModelDeliveryManifest
```

Versioned Player snapshot. Paths are relative to MNN/Models, never Editor paths.

```csharp
class MNN.Unity.MNNModelDownloadProgress
```

A thread-safe batch of repository transfers. Repository callbacks are cumulative across files.

```csharp
enum MNN.Unity.MNNModelDownloadProgress.Phase
```

```csharp
class MNN.Unity.MNNModelDownloadProgress.Snapshot
```

```csharp
class MNN.Unity.MNNModelDownloader
```

Streaming transfers with bounded concurrency, version-pinned checkpoints and verified Range responses.

```csharp
class MNN.Unity.MNNModelFileTransfer
```

Shared Editor/Player transfer engine: bounded connections, resume, pinned checkpoints and verification.

```csharp
class MNN.Unity.MNNModelHardware
```

Supply available memory and sustained measurements when the host can obtain them. Zero means unknown.

```csharp
class MNN.Unity.MNNModelInputSpec
```

```csharp
class MNN.Unity.MNNModelInstallation
```

One writer per installation, atomic SHA-256 receipts and retryable interrupted downloads. Synchronous checks hash model files; use EnsureAsync or call checks on a background worker. A local adoption records integrity from adoption time; it is not proof of remote provenance.

```csharp
enum MNN.Unity.MNNModelInstallation.Phase
```

```csharp
enum MNN.Unity.MNNModelKind
```

Implemented local export protocols for offline model inspection.

```csharp
class MNN.Unity.MNNModelMemoryEstimate
```

```csharp
enum MNN.Unity.MNNModelMemoryKind
```

```csharp
class MNN.Unity.MNNModelOutputSpec
```

```csharp
class MNN.Unity.MNNModelPathResolver
```

MNN模型路径解析器 根据平台和配置自动解析模型路径

```csharp
class MNN.Unity.MNNModelRecommendation
```

Deterministic, offline resource/experience recommendations. Does not load or download models.

```csharp
class MNN.Unity.MNNModelRecommendationPlan
```

```csharp
class MNN.Unity.MNNModelRecommendationPolicy
```

Policy values are conservative tuning knobs, not measured MNN performance claims.

```csharp
class MNN.Unity.MNNModelRecommendationProfiles
```

Versioned, extensible starting metadata; estimates are not device benchmarks or universal quality rankings.

```csharp
class MNN.Unity.MNNModelRecommendationResult
```

```csharp
enum MNN.Unity.MNNModelRecommendationStatus
```

```csharp
enum MNN.Unity.MNNModelRecommendationTask
```

```csharp
class MNN.Unity.MNNModelRepositoryFile
```

One file in a remote model repository, including verification and revision metadata.

```csharp
readonly struct MNN.Unity.MNNModelRepositoryIdentity
```

Normalized source/repository/revision key, independent of display names and execution support.

```csharp
class MNN.Unity.MNNModelResourceProfile
```

Resource metadata for one runnable export, including its quantization and auxiliary graphs.

```csharp
class MNN.Unity.MNNModelRevisions
```

```csharp
class MNN.Unity.MNNModelScopePage
```

A single ModelScope search page. NextPage is zero when the search is complete.

```csharp
enum MNN.Unity.MNNModelSource
```

```csharp
class MNN.Unity.MNNModelSources
```

Curated repository identifiers used by the package. Query the catalog for their metadata and availability.

```csharp
class MNN.Unity.MNNModelSupport
```

Inspects installed task assets without loading weights, calling native APIs or downloading files.

```csharp
class MNN.Unity.MNNModelTransferFile
```

```csharp
class MNN.Unity.MNNModelTransferOptions
```

```csharp
class MNN.Unity.MNNModelWorkload
```

```csharp
class MNN.Unity.MNNOnlineModelCache
```

Shared metadata cache. Automatic callers reuse disk/memory; only explicit refresh fetches again.

```csharp
class MNN.Unity.MNNOnlineModelCache.Discovery
```

```csharp
class MNN.Unity.MNNOnlineModelCache.Snapshot
```

```csharp
class MNN.Unity.MNNPinnedModelManifest
```

```csharp
class MNN.Unity.MNNPinnedModelMetadata
```

Bounded metadata-only proof checks; never downloads weights or claims inference compatibility.

```csharp
class MNN.Unity.MNNPinnedModelMetadata.ManifestEntry
```

```csharp
class MNN.Unity.MNNPinnedModelMetadata.Proof
```

```csharp
class MNN.Unity.MNNRemoteModel
```

```csharp
class MNN.Unity.MNNRemoteModelSnapshot
```

Detached mutable DTO copies for API boundaries; no shared arrays or tensor shapes.

```csharp
enum MNN.Unity.ModelStorageLocation
```

模型存储位置

```csharp
class MNN.Unity.PlatformModelConfig
```

平台特定模型配置

## Retrieval

```csharp
public const string MNN.Unity.MNNBertEmbeddingConfig.FileName = "mnn_embedding.json"
```

```csharp
public string MNN.Unity.MNNBertEmbeddingConfig.attentionMask
```

```csharp
public int MNN.Unity.MNNBertEmbeddingConfig.dimension
```

```csharp
public string MNN.Unity.MNNBertEmbeddingConfig.graph
```

```csharp
public string MNN.Unity.MNNBertEmbeddingConfig.inputIds
```

```csharp
public bool MNN.Unity.MNNBertEmbeddingConfig.lowerCase
```

```csharp
public int MNN.Unity.MNNBertEmbeddingConfig.maximumTokens
```

```csharp
public bool MNN.Unity.MNNBertEmbeddingConfig.normalize
```

```csharp
public string MNN.Unity.MNNBertEmbeddingConfig.output
```

```csharp
public MNN.Unity.MNNBertPooling MNN.Unity.MNNBertEmbeddingConfig.pooling
```

```csharp
public string MNN.Unity.MNNBertEmbeddingConfig.queryPrefix
```

```csharp
public string MNN.Unity.MNNBertEmbeddingConfig.tokenTypes
```

```csharp
public string MNN.Unity.MNNBertEmbeddingConfig.tokenizer
```

```csharp
public string MNN.Unity.MNNBertEmbeddingConfig.vocabulary
```

```csharp
MNN.Unity.MNNBertPooling.Cls = 0
```

```csharp
MNN.Unity.MNNBertPooling.Mean = 1
```

```csharp
public void MNN.Unity.MNNBertEmbedding.Dispose()
```

```csharp
public float[] MNN.Unity.MNNBertEmbedding.Encode(string text, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken))
```

Encodes a document or raw text; truncation reserves CLS/SEP and returns an owned vector.

```csharp
public System.Threading.Tasks.Task<float[]> MNN.Unity.MNNBertEmbedding.EncodeAsync(string text, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken))
```

```csharp
public float[] MNN.Unity.MNNBertEmbedding.EncodeQuery(string text, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken))
```

```csharp
public static MNN.Unity.MNNBertEmbedding MNN.Unity.MNNBertEmbedding.Load(string directory, MNN.Unity.MNNBertEmbeddingConfig config = null, int threadCount = 4, MNN.Unity.MNNPrecisionMode precisionMode = MNN.Unity.MNNPrecisionMode.Normal, MNN.Unity.MNNBackendType backendType = MNN.Unity.MNNBackendType.Auto)
```

```csharp
public MNN.Unity.MNNBertEmbeddingConfig.MNNBertEmbeddingConfig()
```

```csharp
public static bool MNN.Unity.MNNBertEmbeddingConfig.IsConfigured(string directory)
```

```csharp
public static MNN.Unity.MNNBertEmbeddingConfig MNN.Unity.MNNBertEmbeddingConfig.Read(string directory)
```

```csharp
public MNN.Unity.MNNBertPooling.MNNBertPooling()
```

```csharp
public void MNN.Unity.MNNEmbedding.Dispose()
```

```csharp
public float[] MNN.Unity.MNNEmbedding.Encode(string text)
```

Returns an owned vector; Qwen3 retrieval queries should include the model's Instruct/Query prefix.

```csharp
public System.Threading.Tasks.Task<float[]> MNN.Unity.MNNEmbedding.EncodeAsync(string text)
```

```csharp
public float[] MNN.Unity.MNNEmbedding.EncodeQuery(string text)
```

```csharp
public static MNN.Unity.MNNEmbedding MNN.Unity.MNNEmbedding.Load(string modelDirectory, int threadCount = 4, string cacheDirectory = null, MNN.Unity.MNNPrecisionMode precisionMode = MNN.Unity.MNNPrecisionMode.Normal, MNN.Unity.MNNBackendType backendType = MNN.Unity.MNNBackendType.Auto)
```

```csharp
public void MNN.Unity.MNNImageEmbedding.Dispose()
```

```csharp
public float[] MNN.Unity.MNNImageEmbedding.EncodeImage(MNN.Unity.MNNRgbImage image, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken))
```

```csharp
public System.Threading.Tasks.Task<float[]> MNN.Unity.MNNImageEmbedding.EncodeImageAsync(MNN.Unity.MNNRgbImage image, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken))
```

```csharp
public float[] MNN.Unity.MNNImageEmbedding.EncodeText(string text, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken))
```

```csharp
public System.Threading.Tasks.Task<float[]> MNN.Unity.MNNImageEmbedding.EncodeTextAsync(string text, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken))
```

```csharp
public static MNN.Unity.MNNImageEmbedding MNN.Unity.MNNImageEmbedding.Load(string directory, int threadCount = 4, MNN.Unity.MNNBackendType backendType = MNN.Unity.MNNBackendType.Auto)
```

```csharp
public static float MNN.Unity.MNNImageEmbedding.Similarity(float[] left, float[] right)
```

```csharp
public void MNN.Unity.MNNReranker.Dispose()
```

```csharp
public static MNN.Unity.MNNReranker MNN.Unity.MNNReranker.Load(string modelDirectory, int threadCount = 4, string cacheDirectory = null, MNN.Unity.MNNPrecisionMode precisionMode = MNN.Unity.MNNPrecisionMode.Normal, MNN.Unity.MNNBackendType backendType = MNN.Unity.MNNBackendType.Auto)
```

```csharp
public float MNN.Unity.MNNReranker.Score(string query, string document)
```

```csharp
public System.Threading.Tasks.Task<float> MNN.Unity.MNNReranker.ScoreAsync(string query, string document)
```

```csharp
public MNN.Unity.MNNBackendType MNN.Unity.MNNBertEmbedding.Backend { get; }
```

```csharp
public int MNN.Unity.MNNBertEmbedding.Dimension { get; }
```

```csharp
public MNN.Unity.MNNBackendType MNN.Unity.MNNEmbedding.Backend { get; }
```

```csharp
public int MNN.Unity.MNNEmbedding.Dimension { get; }
```

```csharp
public int MNN.Unity.MNNImageEmbedding.Dimension { get; }
```

```csharp
public MNN.Unity.MNNBackendType MNN.Unity.MNNImageEmbedding.ImageBackend { get; }
```

```csharp
public static string MNN.Unity.MNNImageEmbedding.MarkerFileName { get; }
```

```csharp
public MNN.Unity.MNNBackendType MNN.Unity.MNNImageEmbedding.TextBackend { get; }
```

```csharp
public MNN.Unity.MNNBackendType MNN.Unity.MNNReranker.Backend { get; }
```

```csharp
class MNN.Unity.MNNBertEmbedding
```

WordPiece BERT/MiniLM embedding graphs through existing official Interpreter/Tensor APIs.

```csharp
class MNN.Unity.MNNBertEmbeddingConfig
```

Explicit graph/tokenizer contract for a BERT WordPiece sentence encoder.

```csharp
enum MNN.Unity.MNNBertPooling
```

```csharp
class MNN.Unity.MNNEmbedding
```

MNN text embedding model. Call Load on the Unity main thread; EncodeAsync runs inference in a worker.

```csharp
class MNN.Unity.MNNImageEmbedding
```

Image and text vectors in the same 512-dimensional MobileCLIP space.

```csharp
class MNN.Unity.MNNReranker
```

Qwen3 reranker returning a relevance probability for each query/document pair.

## SpeechRecognition

```csharp
public const string MNN.Unity.MNNParaformerConfig.FileName = "mnn_paraformer.json"
```

```csharp
public string MNN.Unity.MNNParaformerConfig.repository
```

```csharp
public const string MNN.Unity.MNNQwen3AsrConfig.FileName = "mnn_qwen3_asr.json"
```

```csharp
public string MNN.Unity.MNNQwen3AsrConfig.repository
```

```csharp
public const string MNN.Unity.MNNSenseVoiceConfig.FileName = "mnn_sensevoice.json"
```

```csharp
public string MNN.Unity.MNNSenseVoiceConfig.language
```

```csharp
public string MNN.Unity.MNNSenseVoiceConfig.model
```

```csharp
public string MNN.Unity.MNNSenseVoiceConfig.profile
```

```csharp
public string MNN.Unity.MNNSenseVoiceConfig.tokens
```

```csharp
public bool MNN.Unity.MNNSenseVoiceConfig.useInverseTextNormalization
```

```csharp
public const string MNN.Unity.MNNWhisperConfig.FileName = "mnn_whisper.json"
```

```csharp
public string MNN.Unity.MNNWhisperConfig.decoder
```

```csharp
public string MNN.Unity.MNNWhisperConfig.encoder
```

```csharp
public string MNN.Unity.MNNWhisperConfig.language
```

```csharp
public string MNN.Unity.MNNWhisperConfig.profile
```

```csharp
public string MNN.Unity.MNNWhisperConfig.tokens
```

```csharp
public const string MNN.Unity.MNNZipformerConfig.FileName = "mnn_zipformer.json"
```

```csharp
public string MNN.Unity.MNNZipformerConfig.decoder
```

```csharp
public string MNN.Unity.MNNZipformerConfig.encoder
```

```csharp
public string MNN.Unity.MNNZipformerConfig.joiner
```

```csharp
public string MNN.Unity.MNNZipformerConfig.tokens
```

```csharp
public MNN.Unity.MNNParaformerConfig.MNNParaformerConfig()
```

```csharp
public static MNN.Unity.MNNParaformerConfig MNN.Unity.MNNParaformerConfig.Read(string directory)
```

```csharp
public MNN.Unity.MNNParaformerRecognizer.Stream MNN.Unity.MNNParaformerRecognizer.CreateStream()
```

Create independent recurrent/CIF state sharing this recognizer's native graphs. Supply mono 16 kHz PCM. Native operations serialize; dispose streams before their recognizer.

```csharp
public void MNN.Unity.MNNParaformerRecognizer.Dispose()
```

```csharp
public static MNN.Unity.MNNParaformerRecognizer MNN.Unity.MNNParaformerRecognizer.Load(string directory, MNN.Unity.MNNParaformerConfig config = null, int threadCount = 4, MNN.Unity.MNNBackendType backendType = MNN.Unity.MNNBackendType.Auto)
```

```csharp
public MNN.Unity.MNNSpeechRecognition MNN.Unity.MNNParaformerRecognizer.Recognize(float[] samples, int sampleRate, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken))
```

Recognize normalized mono PCM at 8–96 kHz, 85 ms to ten minutes. No timestamp alignment.

```csharp
public System.Threading.Tasks.Task<MNN.Unity.MNNSpeechRecognition> MNN.Unity.MNNParaformerRecognizer.RecognizeAsync(float[] samples, int sampleRate, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken))
```

```csharp
public MNN.Unity.MNNSpeechRecognition MNN.Unity.MNNParaformerRecognizer.RecognizeWaveFile(string path, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken))
```

```csharp
public string MNN.Unity.MNNParaformerRecognizer.Stream.AcceptWaveform(float[] samples, int offset, int count, System.Threading.CancellationToken token = default(System.Threading.CancellationToken))
```

Consume at most 30 seconds per call. The caller retains ownership of its array. Argument rejection leaves state intact; interrupted inference requires Reset.

```csharp
public void MNN.Unity.MNNParaformerRecognizer.Stream.Dispose()
```

```csharp
public MNN.Unity.MNNSpeechRecognition MNN.Unity.MNNParaformerRecognizer.Stream.InputFinished(System.Threading.CancellationToken token = default(System.Threading.CancellationToken))
```

Drain one second of acoustic context and the CIF tail exactly once.

```csharp
public void MNN.Unity.MNNParaformerRecognizer.Stream.Reset()
```

```csharp
public MNN.Unity.MNNQwen3AsrConfig.MNNQwen3AsrConfig()
```

```csharp
public static MNN.Unity.MNNQwen3AsrConfig MNN.Unity.MNNQwen3AsrConfig.Read(string directory)
```

```csharp
public void MNN.Unity.MNNQwen3AsrRecognizer.Dispose()
```

```csharp
public static MNN.Unity.MNNQwen3AsrRecognizer MNN.Unity.MNNQwen3AsrRecognizer.Load(string directory, int threadCount = 4, string cacheDirectory = null, MNN.Unity.MNNBackendType backendType = MNN.Unity.MNNBackendType.Auto, MNN.Unity.MNNPrecisionMode precisionMode = MNN.Unity.MNNPrecisionMode.Normal)
```

Use MNNModelLoading.LoadAsync for background loading. Auto prefers the quality-checked Metal route, with CPU fallback.

```csharp
public static MNN.Unity.MNNQwen3AsrRecognizer MNN.Unity.MNNQwen3AsrRecognizer.Load(string directory, string language, int threadCount = 4, string cacheDirectory = null, MNN.Unity.MNNBackendType backendType = MNN.Unity.MNNBackendType.Auto, MNN.Unity.MNNPrecisionMode precisionMode = MNN.Unity.MNNPrecisionMode.Normal)
```

Override the installed template's language: auto, zh, en, ja, ko or yue. Auto generates and removes language metadata. Null preserves the installed template.

```csharp
public MNN.Unity.MNNSpeechRecognition MNN.Unity.MNNQwen3AsrRecognizer.Recognize(float[] samples, int sampleRate, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken), System.Action<MNN.Unity.MNNGenerationUpdate> onProgress = null)
```

Finite normalized mono PCM, 8–96 kHz, 0.1–30 seconds. No timestamp alignment. Language conditioning is selected when loading; the original overload preserves the installed template.

```csharp
public System.Threading.Tasks.Task<MNN.Unity.MNNSpeechRecognition> MNN.Unity.MNNQwen3AsrRecognizer.RecognizeAsync(float[] samples, int sampleRate, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken))
```

```csharp
public MNN.Unity.MNNSpeechRecognition MNN.Unity.MNNQwen3AsrRecognizer.RecognizeWaveFile(string path, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken), System.Action<MNN.Unity.MNNGenerationUpdate> onProgress = null)
```

```csharp
public MNN.Unity.MNNSenseVoiceConfig.MNNSenseVoiceConfig()
```

```csharp
public static MNN.Unity.MNNSenseVoiceConfig MNN.Unity.MNNSenseVoiceConfig.Read(string directory)
```

```csharp
public void MNN.Unity.MNNSenseVoiceRecognizer.Dispose()
```

```csharp
public static MNN.Unity.MNNSenseVoiceRecognizer MNN.Unity.MNNSenseVoiceRecognizer.Load(string directory, MNN.Unity.MNNSenseVoiceConfig config = null, int threadCount = 4, MNN.Unity.MNNBackendType backendType = MNN.Unity.MNNBackendType.Auto)
```

```csharp
public MNN.Unity.MNNSpeechRecognition MNN.Unity.MNNSenseVoiceRecognizer.Recognize(float[] samples, int sampleRate, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken))
```

Recognize finite normalized mono PCM at 8–96 kHz, 85 ms to 60 seconds per recording.

```csharp
public System.Threading.Tasks.Task<MNN.Unity.MNNSpeechRecognition> MNN.Unity.MNNSenseVoiceRecognizer.RecognizeAsync(float[] samples, int sampleRate, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken))
```

```csharp
public MNN.Unity.MNNSpeechRecognition MNN.Unity.MNNSenseVoiceRecognizer.RecognizeWaveFile(string path, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken))
```

```csharp
public void MNN.Unity.MNNSpeechRecognizer.Dispose()
```

```csharp
public static MNN.Unity.MNNSpeechRecognizer MNN.Unity.MNNSpeechRecognizer.Load(string directory, int threadCount = 4, MNN.Unity.MNNBackendType backendType = MNN.Unity.MNNBackendType.Auto, string language = null)
```

```csharp
public static string MNN.Unity.MNNSpeechRecognizer.ProfileFile(string directory)
```

```csharp
public static bool MNN.Unity.MNNSpeechRecognizer.SupportsRepository(MNN.Unity.MNNRemoteModel model)
```

```csharp
public string MNN.Unity.MNNSpeechRecognizer.Transcribe(float[] audio, int sampleRate, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken))
```

```csharp
public MNN.Unity.MNNWhisperConfig.MNNWhisperConfig()
```

```csharp
public static MNN.Unity.MNNWhisperConfig MNN.Unity.MNNWhisperConfig.Read(string directory)
```

```csharp
public void MNN.Unity.MNNWhisperRecognizer.Dispose()
```

```csharp
public static MNN.Unity.MNNWhisperRecognizer MNN.Unity.MNNWhisperRecognizer.Load(string directory, MNN.Unity.MNNWhisperConfig config = null, int threadCount = 4)
```

```csharp
public MNN.Unity.MNNSpeechRecognition MNN.Unity.MNNWhisperRecognizer.Recognize(float[] samples, int sampleRate, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken))
```

Finite normalized mono PCM, 8–96 kHz, 25 ms to 29.5 seconds. Token timestamps are unavailable.

```csharp
public System.Threading.Tasks.Task<MNN.Unity.MNNSpeechRecognition> MNN.Unity.MNNWhisperRecognizer.RecognizeAsync(float[] samples, int sampleRate, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken))
```

```csharp
public MNN.Unity.MNNSpeechRecognition MNN.Unity.MNNWhisperRecognizer.RecognizeWaveFile(string path, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken))
```

```csharp
public static bool MNN.Unity.MNNWhisperRecognizer.SupportsChinese(MNN.Unity.MNNWhisperConfig config)
```

Whether the registered Whisper frontend implements Chinese token decoding.

```csharp
public MNN.Unity.MNNZipformerConfig.MNNZipformerConfig()
```

```csharp
public static MNN.Unity.MNNZipformerConfig MNN.Unity.MNNZipformerConfig.Read(string directory)
```

```csharp
public void MNN.Unity.MNNZipformerRecognizer.Dispose()
```

```csharp
public static MNN.Unity.MNNZipformerRecognizer MNN.Unity.MNNZipformerRecognizer.Load(string directory, MNN.Unity.MNNZipformerConfig config = null, int threadCount = 4, MNN.Unity.MNNBackendType backendType = MNN.Unity.MNNBackendType.Auto)
```

```csharp
public MNN.Unity.MNNSpeechRecognition MNN.Unity.MNNZipformerRecognizer.Recognize(float[] samples, int sampleRate, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken))
```

Recognize finite normalized mono PCM at 8–96 kHz, up to ten minutes. Silence is valid.

```csharp
public System.Threading.Tasks.Task<MNN.Unity.MNNSpeechRecognition> MNN.Unity.MNNZipformerRecognizer.RecognizeAsync(float[] samples, int sampleRate, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken))
```

```csharp
public MNN.Unity.MNNSpeechRecognition MNN.Unity.MNNZipformerRecognizer.RecognizeWaveFile(string path, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken))
```

Read a mono/stereo PCM16/24/32 or float32 RIFF WAV and transcribe it.

```csharp
public MNN.Unity.MNNBackendType MNN.Unity.MNNParaformerRecognizer.Backend { get; }
```

```csharp
public MNN.Unity.MNNBackendType MNN.Unity.MNNParaformerRecognizer.DecoderBackend { get; }
```

```csharp
public MNN.Unity.MNNBackendType MNN.Unity.MNNQwen3AsrRecognizer.Backend { get; }
```

```csharp
public MNN.Unity.MNNBackendType MNN.Unity.MNNQwen3AsrRecognizer.MediaBackend { get; }
```

```csharp
public MNN.Unity.MNNBackendType MNN.Unity.MNNSenseVoiceRecognizer.Backend { get; }
```

```csharp
public string MNN.Unity.MNNSpeechRecognition.Text { get; }
```

```csharp
public System.Collections.Generic.IReadOnlyList<float> MNN.Unity.MNNSpeechRecognition.Timestamps { get; }
```

```csharp
public System.Collections.Generic.IReadOnlyList<int> MNN.Unity.MNNSpeechRecognition.TokenIds { get; }
```

```csharp
public MNN.Unity.MNNBackendType MNN.Unity.MNNSpeechRecognizer.Backend { get; }
```

```csharp
public MNN.Unity.MNNBackendType MNN.Unity.MNNWhisperRecognizer.Backend { get; }
```

```csharp
public string MNN.Unity.MNNWhisperRecognizer.Language { get; set; }
```

```csharp
public string MNN.Unity.MNNWhisperRecognizer.ModelName { get; }
```

```csharp
public MNN.Unity.MNNBackendType MNN.Unity.MNNZipformerRecognizer.Backend { get; }
```

```csharp
class MNN.Unity.MNNParaformerConfig
```

Identifies the pinned Paraformer Chinese online 8404-token graph protocol.

```csharp
class MNN.Unity.MNNParaformerRecognizer
```

Chinese greedy ASR for the pinned 8404-token Paraformer online graph exports.

```csharp
class MNN.Unity.MNNParaformerRecognizer.Stream
```

Bounded incremental PCM, 600 ms acoustic advances and 25 ms feature context. Text is cumulative and provisional until InputFinished; no timestamp alignment is claimed.

```csharp
class MNN.Unity.MNNQwen3AsrConfig
```

Identifies the verified Qwen3-ASR 0.6B INT8 asset set.

```csharp
class MNN.Unity.MNNQwen3AsrRecognizer
```

Qwen3-ASR through the existing official MNN Llm audio API and supported Apple ABI. Cancellation is observed between decoder tokens; audio encoding and prefill are synchronous.

```csharp
class MNN.Unity.MNNSenseVoiceConfig
```

Verified SenseVoiceSmall graph profile. Language is auto, zh, en, ja, ko or yue.

```csharp
class MNN.Unity.MNNSenseVoiceRecognizer
```

Offline greedy CTC ASR for the two verified ModelScope/Hugging Face SenseVoiceSmall profiles.

```csharp
class MNN.Unity.MNNSpeechRecognition
```

Speech transcript, token IDs and optional token start times in seconds. Timestamps are empty when the decoder does not provide alignment.

```csharp
class MNN.Unity.MNNSpeechRecognizer
```

Profile-selected ASR over the package's existing task APIs. Owns its runner.

```csharp
class MNN.Unity.MNNWhisperConfig
```

Pinned Whisper encoder/decoder profile; timestamps are disabled.

```csharp
class MNN.Unity.MNNWhisperRecognizer
```

Whisper greedy ASR using the verified encoder/decoder profiles.

```csharp
class MNN.Unity.MNNZipformerConfig
```

Sherpa Zipformer v1 streaming-transducer graph locations.

```csharp
class MNN.Unity.MNNZipformerRecognizer
```

Zipformer v1 greedy ASR for the 2023-02-20 bilingual and 2023-02-21 English graph protocols.

## Vision

```csharp
public const string MNN.Unity.MNNImageTaskConfig.FileName = "mnn_image_task.json"
```

```csharp
public MNN.Unity.MNNImageTaskProfile MNN.Unity.MNNImageTaskConfig.profile
```

```csharp
MNN.Unity.MNNImageTaskProfile.CandyStyle = 8
```

```csharp
MNN.Unity.MNNImageTaskProfile.DeepLabV3 = 9
```

```csharp
MNN.Unity.MNNImageTaskProfile.DepthAnythingV2Small = 11
```

```csharp
MNN.Unity.MNNImageTaskProfile.Landmark106 = 5
```

```csharp
MNN.Unity.MNNImageTaskProfile.MobileFaceNet = 4
```

```csharp
MNN.Unity.MNNImageTaskProfile.MobileNetV2 = 6
```

```csharp
MNN.Unity.MNNImageTaskProfile.ModNet = 0
```

```csharp
MNN.Unity.MNNImageTaskProfile.MoveNetLightning = 10
```

```csharp
MNN.Unity.MNNImageTaskProfile.PglNet = 7
```

```csharp
MNN.Unity.MNNImageTaskProfile.RealEsrganX4 = 1
```

```csharp
MNN.Unity.MNNImageTaskProfile.Scrfd500M = 3
```

```csharp
MNN.Unity.MNNImageTaskProfile.UVDoc = 2
```

```csharp
public const string MNN.Unity.MNNPaddleOcrConfig.FileName = "mnn_paddle_ocr.json"
```

```csharp
public float MNN.Unity.MNNPaddleOcrConfig.boxThreshold
```

```csharp
public string MNN.Unity.MNNPaddleOcrConfig.graph
```

```csharp
public string MNN.Unity.MNNPaddleOcrConfig.input
```

```csharp
public int MNN.Unity.MNNPaddleOcrConfig.maximumCandidates
```

```csharp
public int MNN.Unity.MNNPaddleOcrConfig.maximumSide
```

```csharp
public string MNN.Unity.MNNPaddleOcrConfig.output
```

```csharp
public float MNN.Unity.MNNPaddleOcrConfig.pixelThreshold
```

```csharp
public MNN.Unity.MNNBackendType MNN.Unity.MNNPaddleOcrConfig.preferredDetectionBackend
```

```csharp
public MNN.Unity.MNNBackendType MNN.Unity.MNNPaddleOcrConfig.preferredRecognitionBackend
```

```csharp
public float MNN.Unity.MNNPaddleOcrConfig.recognitionThreshold
```

```csharp
public float MNN.Unity.MNNPaddleOcrConfig.unclipRatio
```

```csharp
public const string MNN.Unity.MNNPaddleTextConfig.FileName = "mnn_paddle_text.json"
```

```csharp
public bool MNN.Unity.MNNPaddleTextConfig.appendSpace
```

```csharp
public string MNN.Unity.MNNPaddleTextConfig.dictionary
```

```csharp
public string MNN.Unity.MNNPaddleTextConfig.graph
```

```csharp
public int MNN.Unity.MNNPaddleTextConfig.height
```

```csharp
public string MNN.Unity.MNNPaddleTextConfig.input
```

```csharp
public int MNN.Unity.MNNPaddleTextConfig.maximumWidth
```

```csharp
public int MNN.Unity.MNNPaddleTextConfig.minimumWidth
```

```csharp
public string MNN.Unity.MNNPaddleTextConfig.output
```

```csharp
public string MNN.Unity.MNNPaddleTextConfig.pageConfig
```

```csharp
public MNN.Unity.MNNBackendType MNN.Unity.MNNPaddleTextConfig.preferredBackend
```

```csharp
public bool MNN.Unity.MNNPaddleTextConfig.reverseArabic
```

```csharp
MNN.Unity.MNNPoseJoint.LeftAnkle = 15
```

```csharp
MNN.Unity.MNNPoseJoint.LeftEar = 3
```

```csharp
MNN.Unity.MNNPoseJoint.LeftElbow = 7
```

```csharp
MNN.Unity.MNNPoseJoint.LeftEye = 1
```

```csharp
MNN.Unity.MNNPoseJoint.LeftHip = 11
```

```csharp
MNN.Unity.MNNPoseJoint.LeftKnee = 13
```

```csharp
MNN.Unity.MNNPoseJoint.LeftShoulder = 5
```

```csharp
MNN.Unity.MNNPoseJoint.LeftWrist = 9
```

```csharp
MNN.Unity.MNNPoseJoint.Nose = 0
```

```csharp
MNN.Unity.MNNPoseJoint.RightAnkle = 16
```

```csharp
MNN.Unity.MNNPoseJoint.RightEar = 4
```

```csharp
MNN.Unity.MNNPoseJoint.RightElbow = 8
```

```csharp
MNN.Unity.MNNPoseJoint.RightEye = 2
```

```csharp
MNN.Unity.MNNPoseJoint.RightHip = 12
```

```csharp
MNN.Unity.MNNPoseJoint.RightKnee = 14
```

```csharp
MNN.Unity.MNNPoseJoint.RightShoulder = 6
```

```csharp
MNN.Unity.MNNPoseJoint.RightWrist = 10
```

```csharp
MNN.Unity.MNNYoloV8BoxEncoding.AnchorDistances = 1
```

```csharp
MNN.Unity.MNNYoloV8BoxEncoding.CenterSize = 0
```

```csharp
public const string MNN.Unity.MNNYoloV8Config.FileName = "mnn_yolov8.json"
```

```csharp
public MNN.Unity.MNNYoloV8BoxEncoding MNN.Unity.MNNYoloV8Config.boxEncoding
```

```csharp
public string MNN.Unity.MNNYoloV8Config.graph
```

```csharp
public int MNN.Unity.MNNYoloV8Config.height
```

```csharp
public string MNN.Unity.MNNYoloV8Config.input
```

```csharp
public string[] MNN.Unity.MNNYoloV8Config.labels
```

```csharp
public string MNN.Unity.MNNYoloV8Config.output
```

```csharp
public int[] MNN.Unity.MNNYoloV8Config.strides
```

```csharp
public int MNN.Unity.MNNYoloV8Config.width
```

```csharp
public MNN.Unity.MNNDocumentLayoutResult MNN.Unity.MNNDocumentLayout.Analyze(MNN.Unity.MNNRgbImage image, float scoreThreshold = 0.5, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken))
```

Stretch RGB to 800x800 with pixel/255 normalization. Native execution is synchronous; cancellation is observed before and after that call. This returns regions, not text or table cells.

```csharp
public System.Threading.Tasks.Task<MNN.Unity.MNNDocumentLayoutResult> MNN.Unity.MNNDocumentLayout.AnalyzeAsync(MNN.Unity.MNNRgbImage image, float scoreThreshold = 0.5, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken))
```

```csharp
public void MNN.Unity.MNNDocumentLayout.Dispose()
```

```csharp
public static MNN.Unity.MNNDocumentLayout MNN.Unity.MNNDocumentLayout.Load(string directory, int threadCount = 4, MNN.Unity.MNNBackendType backendType = MNN.Unity.MNNBackendType.Auto)
```

```csharp
public float MNN.Unity.MNNImageDepthResult.SampleAt(int x, int y)
```

```csharp
public static System.Collections.Generic.IReadOnlyList<MNN.Unity.MNNImageModelSource> MNN.Unity.MNNImageModelSource.GetAll()
```

Returns a snapshot of supported image export sources. No network requests are made.

```csharp
public int MNN.Unity.MNNImageSegmentationResult.ClassAt(int x, int y)
```

```csharp
public float MNN.Unity.MNNImageSegmentationResult.ConfidenceAt(int x, int y)
```

```csharp
public static MNN.Unity.MNNRgbImage MNN.Unity.MNNImageTask.AlignFace(MNN.Unity.MNNRgbImage image, MNN.Unity.MNNImagePoint[] keypoints)
```

Five-point least-squares similarity alignment to the ArcFace 112px template.

```csharp
public static MNN.Unity.MNNRgbImage MNN.Unity.MNNImageTask.CropFace(MNN.Unity.MNNRgbImage image, MNN.Unity.MNNDetection face)
```

Axis-aligned 192px crop with 1.5x face-box margin for Landmark106. The returned landmark coordinates are relative to this crop.

```csharp
public void MNN.Unity.MNNImageTask.Dispose()
```

```csharp
public static MNN.Unity.MNNImageTask MNN.Unity.MNNImageTask.Load(string directory, int threadCount = 4, MNN.Unity.MNNBackendType backendType = MNN.Unity.MNNBackendType.Auto)
```

```csharp
public MNN.Unity.MNNImageTaskResult MNN.Unity.MNNImageTask.Process(MNN.Unity.MNNRgbImage image, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken))
```

For Landmark106, pass a face crop including the 1.5x bbox margin. MobileFaceNet requires a five-point aligned 112x112 crop, never a full photo.

```csharp
public System.Threading.Tasks.Task<MNN.Unity.MNNImageTaskResult> MNN.Unity.MNNImageTask.ProcessAsync(MNN.Unity.MNNRgbImage image, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken))
```

```csharp
public static byte MNN.Unity.MNNImageTask.SegmentationColor(int id, int channel)
```

```csharp
public MNN.Unity.MNNImageTaskConfig.MNNImageTaskConfig()
```

```csharp
public static MNN.Unity.MNNImageTaskConfig MNN.Unity.MNNImageTaskConfig.Read(string directory)
```

```csharp
public string MNN.Unity.MNNImageTaskConfig.Resolve(string directory, bool verifyHash)
```

```csharp
public MNN.Unity.MNNImageTaskProfile.MNNImageTaskProfile()
```

```csharp
public MNN.Unity.MNNImageTaskResult.MNNImageTaskResult()
```

```csharp
public void MNN.Unity.MNNPaddleOcr.Dispose()
```

```csharp
public static MNN.Unity.MNNPaddleOcr MNN.Unity.MNNPaddleOcr.Load(string directory, string recognizerProfile = ".", MNN.Unity.MNNPaddleOcrConfig config = null, int threadCount = 4, MNN.Unity.MNNBackendType backendType = MNN.Unity.MNNBackendType.Auto)
```

```csharp
public MNN.Unity.MNNOcrResult MNN.Unity.MNNPaddleOcr.Recognize(MNN.Unity.MNNRgbImage image, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken))
```

```csharp
public System.Threading.Tasks.Task<MNN.Unity.MNNOcrResult> MNN.Unity.MNNPaddleOcr.RecognizeAsync(MNN.Unity.MNNRgbImage image, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken))
```

```csharp
public MNN.Unity.MNNTextRecognition MNN.Unity.MNNPaddleOcr.RecognizeLine(MNN.Unity.MNNRgbImage line, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken))
```

Recognize an already cropped upright line without DB detection or another resampling crop.

```csharp
public MNN.Unity.MNNPaddleOcrConfig.MNNPaddleOcrConfig()
```

```csharp
public static MNN.Unity.MNNPaddleOcrConfig MNN.Unity.MNNPaddleOcrConfig.Read(string directory)
```

```csharp
public static MNN.Unity.MNNPaddleOcrConfig MNN.Unity.MNNPaddleOcrConfig.ReadForProfile(string directory, string profile)
```

```csharp
public static string MNN.Unity.MNNPaddleOcrConfig.RecognizerDirectory(string directory, string profile)
```

```csharp
public void MNN.Unity.MNNPaddleOcrConfig.Validate(string directory)
```

Validate the declared OCR profile before loading native resources.

```csharp
public MNN.Unity.MNNPaddleTextConfig.MNNPaddleTextConfig()
```

```csharp
public static MNN.Unity.MNNPaddleTextConfig MNN.Unity.MNNPaddleTextConfig.Read(string directory)
```

```csharp
public void MNN.Unity.MNNPaddleTextConfig.Validate(string directory)
```

```csharp
public MNN.Unity.MNNTextBox[] MNN.Unity.MNNPaddleTextDetector.Detect(MNN.Unity.MNNRgbImage image, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken))
```

```csharp
public System.Threading.Tasks.Task<MNN.Unity.MNNTextBox[]> MNN.Unity.MNNPaddleTextDetector.DetectAsync(MNN.Unity.MNNRgbImage image, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken))
```

```csharp
public void MNN.Unity.MNNPaddleTextDetector.Dispose()
```

```csharp
public static MNN.Unity.MNNPaddleTextDetector MNN.Unity.MNNPaddleTextDetector.Load(string directory, MNN.Unity.MNNPaddleOcrConfig config = null, int threadCount = 4, MNN.Unity.MNNBackendType backendType = MNN.Unity.MNNBackendType.Auto)
```

```csharp
public void MNN.Unity.MNNPaddleTextRecognizer.Dispose()
```

```csharp
public static MNN.Unity.MNNPaddleTextRecognizer MNN.Unity.MNNPaddleTextRecognizer.Load(string directory, MNN.Unity.MNNPaddleTextConfig config = null, int threadCount = 4, MNN.Unity.MNNBackendType backendType = MNN.Unity.MNNBackendType.Auto)
```

```csharp
public MNN.Unity.MNNTextRecognition MNN.Unity.MNNPaddleTextRecognizer.Recognize(MNN.Unity.MNNRgbImage line, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken))
```

```csharp
public System.Threading.Tasks.Task<MNN.Unity.MNNTextRecognition> MNN.Unity.MNNPaddleTextRecognizer.RecognizeAsync(MNN.Unity.MNNRgbImage line, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken))
```

```csharp
public MNN.Unity.MNNPoseJoint.MNNPoseJoint()
```

```csharp
public MNN.Unity.MNNPoseKeypoint MNN.Unity.MNNPoseResult.GetKeypoint(MNN.Unity.MNNPoseJoint joint)
```

```csharp
public MNN.Unity.MNNRgbImage.MNNRgbImage(byte[] pixels, int width, int height)
```

```csharp
public byte[] MNN.Unity.MNNRgbImage.ToArray()
```

```csharp
public MNN.Unity.MNNTextRecognition.MNNTextRecognition(string text, float confidence)
```

```csharp
public MNN.Unity.MNNDetection[] MNN.Unity.MNNYoloV8.Detect(MNN.Unity.MNNRgbImage image, float scoreThreshold = 0.25, float iouThreshold = 0.45, int maximumDetections = 300, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken))
```

```csharp
public System.Threading.Tasks.Task<MNN.Unity.MNNDetection[]> MNN.Unity.MNNYoloV8.DetectAsync(MNN.Unity.MNNRgbImage image, float scoreThreshold = 0.25, float iouThreshold = 0.45, int maximumDetections = 300, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken))
```

```csharp
public void MNN.Unity.MNNYoloV8.Dispose()
```

```csharp
public static MNN.Unity.MNNYoloV8 MNN.Unity.MNNYoloV8.Load(string directory, MNN.Unity.MNNYoloV8Config config = null, int threadCount = 4, MNN.Unity.MNNBackendType backendType = MNN.Unity.MNNBackendType.Auto)
```

```csharp
public MNN.Unity.MNNYoloV8BoxEncoding.MNNYoloV8BoxEncoding()
```

```csharp
public MNN.Unity.MNNYoloV8Config.MNNYoloV8Config()
```

```csharp
public static MNN.Unity.MNNYoloV8Config MNN.Unity.MNNYoloV8Config.Read(string directory)
```

```csharp
public string MNN.Unity.MNNYoloV8Config.ResolveGraph(string directory)
```

```csharp
public int MNN.Unity.MNNDetection.ClassIndex { get; }
```

```csharp
public float MNN.Unity.MNNDetection.Height { get; }
```

```csharp
public string MNN.Unity.MNNDetection.Label { get; }
```

```csharp
public float MNN.Unity.MNNDetection.Score { get; }
```

```csharp
public float MNN.Unity.MNNDetection.Width { get; }
```

```csharp
public float MNN.Unity.MNNDetection.X { get; }
```

```csharp
public float MNN.Unity.MNNDetection.Y { get; }
```

```csharp
public MNN.Unity.MNNBackendType MNN.Unity.MNNDocumentLayout.Backend { get; }
```

```csharp
public static string MNN.Unity.MNNDocumentLayout.GraphFileName { get; }
```

```csharp
public static string MNN.Unity.MNNDocumentLayout.MetadataFileName { get; }
```

```csharp
public int MNN.Unity.MNNDocumentLayoutResult.Height { get; }
```

```csharp
public MNN.Unity.MNNDocumentRegion[] MNN.Unity.MNNDocumentLayoutResult.Regions { get; }
```

```csharp
public int MNN.Unity.MNNDocumentLayoutResult.Width { get; }
```

```csharp
public MNN.Unity.MNNDetection MNN.Unity.MNNDocumentRegion.Detection { get; }
```

```csharp
public int MNN.Unity.MNNDocumentRegion.ReadingOrder { get; }
```

Model-predicted reading order. Gaps can remain after confidence filtering.

```csharp
public int MNN.Unity.MNNImageDepthResult.GridHeight { get; }
```

```csharp
public int MNN.Unity.MNNImageDepthResult.GridWidth { get; }
```

```csharp
public int MNN.Unity.MNNImageDepthResult.Height { get; }
```

```csharp
public float MNN.Unity.MNNImageDepthResult.Maximum { get; }
```

```csharp
public float MNN.Unity.MNNImageDepthResult.Minimum { get; }
```

```csharp
public float[] MNN.Unity.MNNImageDepthResult.RelativeInverseDepth { get; }
```

```csharp
public int MNN.Unity.MNNImageDepthResult.Width { get; }
```

```csharp
public MNN.Unity.MNNImageTaskProfile MNN.Unity.MNNImageModelSource.Profile { get; }
```

```csharp
public string MNN.Unity.MNNImageModelSource.Repository { get; }
```

```csharp
public string MNN.Unity.MNNImageModelSource.Url { get; }
```

```csharp
public float MNN.Unity.MNNImagePoint.X { get; }
```

```csharp
public float MNN.Unity.MNNImagePoint.Y { get; }
```

```csharp
public byte[] MNN.Unity.MNNImageSegmentationResult.ClassIds { get; }
```

```csharp
public int[] MNN.Unity.MNNImageSegmentationResult.ClassPixelCounts { get; }
```

```csharp
public float[] MNN.Unity.MNNImageSegmentationResult.Confidence { get; }
```

```csharp
public int MNN.Unity.MNNImageSegmentationResult.Height { get; }
```

```csharp
public string[] MNN.Unity.MNNImageSegmentationResult.Labels { get; }
```

```csharp
public int MNN.Unity.MNNImageSegmentationResult.Width { get; }
```

```csharp
public MNN.Unity.MNNBackendType MNN.Unity.MNNImageTask.Backend { get; }
```

```csharp
public MNN.Unity.MNNImageTaskProfile MNN.Unity.MNNImageTask.Profile { get; }
```

```csharp
public float[] MNN.Unity.MNNImageTaskResult.Alpha { get; internal set; }
```

```csharp
public MNN.Unity.MNNImageDepthResult MNN.Unity.MNNImageTaskResult.Depth { get; internal set; }
```

```csharp
public MNN.Unity.MNNDetection[] MNN.Unity.MNNImageTaskResult.Detections { get; internal set; }
```

```csharp
public MNN.Unity.MNNImagePoint[][] MNN.Unity.MNNImageTaskResult.FaceKeypoints { get; internal set; }
```

```csharp
public MNN.Unity.MNNGeneratedImage MNN.Unity.MNNImageTaskResult.Image { get; internal set; }
```

```csharp
public string[] MNN.Unity.MNNImageTaskResult.Labels { get; internal set; }
```

```csharp
public MNN.Unity.MNNImagePoint[] MNN.Unity.MNNImageTaskResult.Points { get; internal set; }
```

```csharp
public MNN.Unity.MNNPoseResult MNN.Unity.MNNImageTaskResult.Pose { get; internal set; }
```

```csharp
public float[] MNN.Unity.MNNImageTaskResult.Probabilities { get; internal set; }
```

```csharp
public MNN.Unity.MNNImageSegmentationResult MNN.Unity.MNNImageTaskResult.Segmentation { get; internal set; }
```

```csharp
public float[] MNN.Unity.MNNImageTaskResult.Vector { get; internal set; }
```

```csharp
public float MNN.Unity.MNNOcrResult.Confidence { get; }
```

```csharp
public System.Collections.Generic.IReadOnlyList<MNN.Unity.MNNTextRegion> MNN.Unity.MNNOcrResult.Regions { get; }
```

```csharp
public string MNN.Unity.MNNOcrResult.Text { get; }
```

```csharp
public MNN.Unity.MNNBackendType MNN.Unity.MNNPaddleOcr.DetectionBackend { get; }
```

```csharp
public MNN.Unity.MNNBackendType MNN.Unity.MNNPaddleOcr.RecognitionBackend { get; }
```

```csharp
public MNN.Unity.MNNBackendType MNN.Unity.MNNPaddleTextDetector.Backend { get; }
```

```csharp
public MNN.Unity.MNNBackendType MNN.Unity.MNNPaddleTextRecognizer.Backend { get; }
```

```csharp
public float MNN.Unity.MNNPoseKeypoint.Confidence { get; }
```

```csharp
public bool MNN.Unity.MNNPoseKeypoint.IsInBounds { get; }
```

```csharp
public MNN.Unity.MNNPoseJoint MNN.Unity.MNNPoseKeypoint.Joint { get; }
```

```csharp
public float MNN.Unity.MNNPoseKeypoint.X { get; }
```

```csharp
public float MNN.Unity.MNNPoseKeypoint.Y { get; }
```

```csharp
public int MNN.Unity.MNNPoseResult.Height { get; }
```

```csharp
public System.Collections.Generic.IReadOnlyList<MNN.Unity.MNNPoseKeypoint> MNN.Unity.MNNPoseResult.Keypoints { get; }
```

```csharp
public int MNN.Unity.MNNPoseResult.Width { get; }
```

```csharp
public int MNN.Unity.MNNRgbImage.Height { get; }
```

```csharp
public int MNN.Unity.MNNRgbImage.Width { get; }
```

```csharp
public float MNN.Unity.MNNTextBox.Confidence { get; }
```

```csharp
public System.Collections.Generic.IReadOnlyList<UnityEngine.Vector2> MNN.Unity.MNNTextBox.Corners { get; }
```

```csharp
public float MNN.Unity.MNNTextRecognition.Confidence { get; }
```

```csharp
public string MNN.Unity.MNNTextRecognition.Text { get; }
```

```csharp
public MNN.Unity.MNNTextBox MNN.Unity.MNNTextRegion.Box { get; }
```

```csharp
public float MNN.Unity.MNNTextRegion.Confidence { get; }
```

```csharp
public MNN.Unity.MNNTextRecognition MNN.Unity.MNNTextRegion.Recognition { get; }
```

```csharp
public string MNN.Unity.MNNTextRegion.Text { get; }
```

```csharp
public MNN.Unity.MNNBackendType MNN.Unity.MNNYoloV8.Backend { get; }
```

```csharp
class MNN.Unity.MNNDetection
```

One immutable detection in source-image pixels, with top-left origin.

```csharp
class MNN.Unity.MNNDocumentLayout
```

PP-DocLayoutV3 page regions and reading order using the pinned MNN export. Auto selects CPU; this export has failed Metal correctness checks.

```csharp
class MNN.Unity.MNNDocumentLayoutResult
```

Page regions sorted by model-predicted reading order; no OCR or table-cell decoding.

```csharp
class MNN.Unity.MNNDocumentRegion
```

A page region in source-image pixels, with top-left origin.

```csharp
class MNN.Unity.MNNImageDepthResult
```

Relative inverse depth: larger values mean closer. Values have no metric unit and cannot be compared as distances across different images. The raw grid is row-major, top-left origin; SampleAt maps source pixels to that grid with half-pixel bilinear interpolation.

```csharp
class MNN.Unity.MNNImageModelSource
```

Published image graph metadata; a null URL requires preparation with the package tooling.

```csharp
class MNN.Unity.MNNImagePoint
```

Image coordinates use source pixels and a top-left origin.

```csharp
class MNN.Unity.MNNImageSegmentationResult
```

Semantic classes and softmax confidence in original-image pixels, top-left origin. Pixels are nearest-neighbor projections from the model's 257x257 grid; confidence is not calibrated.

```csharp
class MNN.Unity.MNNImageTask
```

Shared image task execution on the existing official MNN C++ binding.

```csharp
class MNN.Unity.MNNImageTaskConfig
```

Exact publisher exports with implemented preprocessing and decoding. A profile does not certify every export sharing the architecture name.

```csharp
enum MNN.Unity.MNNImageTaskProfile
```

```csharp
class MNN.Unity.MNNImageTaskResult
```

Task data, not just a visualization. Alpha has source-image dimensions. Face vectors are unit normalized; recognition expects an aligned 112x112 face. Segmentation classes/confidence and its colored image use source-image dimensions.

```csharp
class MNN.Unity.MNNOcrResult
```

Accepted text lines in geometric reading order, with source-image boxes and confidence.

```csharp
class MNN.Unity.MNNPaddleOcr
```

Full-image DB detection, rectified crops and matched language CTC recognition.

```csharp
class MNN.Unity.MNNPaddleOcrConfig
```

DB text detection and CTC page recognition using an explicitly matched PP-OCR profile.

```csharp
class MNN.Unity.MNNPaddleTextConfig
```

Explicit PP-OCR CTC line recognizer protocol; not a page detector.

```csharp
class MNN.Unity.MNNPaddleTextDetector
```

PP-OCR DB text detection using official Interpreter/Tensor APIs and managed geometry.

```csharp
class MNN.Unity.MNNPaddleTextRecognizer
```

PP-OCR CTC recognition of an upright cropped text line using official Interpreter/Tensor calls.

```csharp
enum MNN.Unity.MNNPoseJoint
```

COCO's 17 joint indices. Left/right refer to the person's body.

```csharp
class MNN.Unity.MNNPoseKeypoint
```

Unclipped source pixel coordinates, top-left origin. Confidence is the model's joint score, not a calibrated person-presence probability.

```csharp
class MNN.Unity.MNNPoseResult
```

SinglePose estimates one primary person, even on an empty scene. All 17 COCO joints are retained, including low scores and padding points.

```csharp
class MNN.Unity.MNNRgbImage
```

Owned RGB24 pixels, row-major from the top left; safe to share with inference workers.

```csharp
class MNN.Unity.MNNTextBox
```

A detected text quadrilateral in source pixels: top-left, top-right, bottom-right, bottom-left.

```csharp
class MNN.Unity.MNNTextRecognition
```

CTC text with mean confidence of the retained nonblank symbols.

```csharp
class MNN.Unity.MNNTextRegion
```

One detected text region and its CTC recognition result.

```csharp
class MNN.Unity.MNNYoloV8
```

YOLOv8 detection exports using the existing official Interpreter/Tensor calls.

```csharp
enum MNN.Unity.MNNYoloV8BoxEncoding
```

```csharp
class MNN.Unity.MNNYoloV8Config
```

YOLOv8 box/class graph, RGB float32 NCHW input and no objectness channel.
