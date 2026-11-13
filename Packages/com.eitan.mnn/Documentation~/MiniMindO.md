# MiniMind-o

`MNNMiniMindO` runs the released dense MiniMind-3o core, SenseVoiceSmall audio encoder,
SigLIP2 image encoder and Mimi decoder through the package's existing, platform-gated
MNN C++ Interpreter/Tensor bindings. Studio recognizes `mnn_minimind_o.json` and uses
this adapter rather than the generic MNN LLM loader. No new native library, C export,
C++ code or MNN source change is involved.

## Installed model

The development installation is `Assets/StreamingAssets/MNN/Models/MiniMindO-113M-MNN`.
Open **MNN Chat Studio**, refresh its installed models, select **MiniMindO-113M-MNN**,
and use Chat or Omni. Enable **Generate spoken reply** for speech. An image or WAV
can accompany the current turn. Model files are ignored by Git; importing the UPM
package alone does not install weights. Keep all files in the model directory together.

The selected core recipe is FP16 prefill, 56 Thinker decoder projections at asymmetric
W8/block64, and FP16 for the remaining decoder weights, including Talker and output
heads. Auxiliary components and multimodal prefill use FP16 weights. Sessions use CPU,
four threads by default, Low precision and Low memory. The graph runner's new memory
option defaults to Normal for existing callers.

## C# API

```csharp
using System.IO;
using MNN.Unity;
using UnityEngine;

string directory = Path.Combine(Application.streamingAssetsPath,
    "MNN/Models/MiniMindO-113M-MNN");
using (var model = MNNMiniMindO.Load(directory, threads: 4))
{
    var result = await model.GenerateAsync(
        new MNNMiniMindORequest { Text = "你好，请介绍你自己。" },
        new MNNMiniMindOOptions { MaxNewTokens = 128, GenerateSpeech = true },
        update => {
            // Worker-thread callback: enqueue update.Text and update.Waveform
            // for a main-thread consumer. Waveform is new mono 24 kHz PCM.
        }, cancellationToken);
    Debug.Log(result.Text);
    // result.AudioCodes: one eight-codebook Mimi frame per entry.
    // result.Waveform: concatenated PCM chunks; result.SampleRate is 24000.
    // Inspect result.TokenLimitReached before treating a reply as complete.
}
```

`Generate` is the synchronous equivalent. Calls on one instance serialize; callbacks
must not reenter generation or dispose that instance. Cancellation is checked between
native graph executions, including after callbacks. Async inputs/options are copied
before dispatch. Dispose after the active operation finishes.

For audio, set `Audio16Khz` to normalized mono PCM. `MNNWaveAudio.Read` and
`MNNAudioResampling.Resample` can prepare a WAV. Audio must contain 85 ms–20 seconds.
For images, call `MNNMiniMindO.PrepareImage(MNNRgbImage)` and assign the returned planar
RGB tensor to `ImagePixels`. It applies the pinned processor's antialiased bilinear
256×256 resize and normalization. Both inputs can be supplied together.
`History` contains previous `MNNChatMessage` text turns, excluding the current user
turn. Historical media are not retained; supply their current media again if needed.
This bounded adapter rejects prompt plus generation budgets above 512 tokens rather
than silently truncating media or history. It does not perform automatic summarization.

## Capabilities and validation

The release accepts completed input turns and streams generated text, Mimi codes and
PCM. Microphone capture can continue while output plays, and a caller can cancel it to
start another turn. The released model does not consume new input while its current
response is being generated; conversion does not add native model-level full duplex.
PCM uses the upstream WebUI default of four new frames with two overlap frames.
Default voice is implemented; reference speaker cloning is not exposed.

The current Mac validation uses Apple M5 / 24 GB and the bundled MNN 3.6.2 native ABI;
conversion and numerical component checks use MNN 3.6.1 Python tooling. Unity checks
cover tokenizer parity, Kaldi/CMVN features, Pillow resize, delayed audio alignment,
text/speech generation, actual speech input, combined image/audio input, cancellation,
recovery, disposal and Studio loading. Numerical checks preserve the released model's
behavior; they do not establish factual correctness, transcription accuracy or human
speech quality. See the local integration report for exact observations and timing.
Other hardware and Player builds require their own native/runtime validation.

## Rebuilding

Use [MiniMind-o preparation tools](../Tools~/Models/MiniMindO/README.md). Source weights
are pinned by URL, byte count and SHA256. Conversion uses official MNN per-operation
compression guides. Keep Python dependencies and intermediate files in `TestArtifacts~/`;
write reusable model assets under `Assets/StreamingAssets/MNN/Models/`.

Focused Unity checks:

```sh
UNITY_EXECUTABLE -batchmode -nographics -projectPath PROJECT_ROOT \
  -runTests -testPlatform EditMode \
  -testFilter 'MNN.Unity.Tests.MNNMiniMindOTests;MNN.Unity.Tests.MNNMiniMindOPreprocessingTests;MNN.Unity.Tests.MiniMindOIntegrationTests' \
  -testResults PROJECT_ROOT/TestArtifacts~/minimind-results.xml \
  -logFile PROJECT_ROOT/TestArtifacts~/minimind-unity.log
```

The native fixtures are explicit and require the existing local installation. They
never download weights. The preprocessing/sampling fixtures use small deterministic
reference data included with the package.

## Discovery and distribution

Search for `MiniMind-o`, `MiniMindO` or `minimind` in Model Manager and
`MNNModelCatalog.CreateDefault().SearchModels(query)`. Both use the shared OmniModel
profile. `MNNConversationModelCatalog` resolves it to the dedicated `MiniMindO`
adapter; `MNNConversationLocalProviders.Create` loads that protocol rather than
the generic MNN LLM layout. Complete local exports are reusable without a legacy
ModelScope install marker; raw PyTorch and core-only exports are not runnable Omni
installations. Online refresh retains this dedicated protocol.

Deployment repository: `EitanWang/MiniMindO-113M-MNN` on ModelScope. The catalog's
release revision is pinned after publication. Download through
`MNNModelDownloader.DownloadAsync` or Model Manager; use the whole repository.

`Tools~/Models/MiniMindO/publish.py` verifies payload hashes and successful shared
discovery/native runtime test evidence before uploading. It uses the existing
ModelScope SDK login and excludes Unity `.meta` files. Model onboarding acceptance
is recorded in the project's `AGENTS.md`.

## SiriAI example

Apply the checked `minimind-o` stage with `Tools~/Migration/migrate_siriai_conversation.py`
when updating an existing SiriAI import. The updated sample scene uses MiniMind-o
voice directly; the Configurable Backend component also provides **Use MiniMind-o
voice**. This selects buffered model speech, Silero VAD, SmartTurn turn detection and the pinned
ModelScope export without separate ASR/TTS. The separate `minimind-o` settings profile
preserves existing default preferences. The runtime provider retains completed text
turns within the context bound; previous untranscribed audio is explicitly marked.


### Current Mac acceleration result

The SiriAI preset keeps four CPU threads, Low precision/Low memory, and the existing
Thinker W8 + Talker FP16 weights. Silero and SmartTurn remain enabled for Omni voice;
there is no separate ASR/TTS requirement on this route. SiriAI returns completed model
waveforms; low-level `MNNMiniMindO` callbacks expose PCM chunks earlier. This remains
turn-based speech, without native model-level full duplex.

On the current Apple M5, the deployed graphs actually ran on Metal. For the measured
small inputs, Metal prefill/Mimi were slower than CPU; the quantized decoder differed
by about 4.47% relative L2 from CPU, including the FP32 retry. It therefore failed this
acceleration qualification. A two-thread complete voice trial was slower and changed
sampled speech codes, so the preset retains four threads. These are observations on
specific inputs, not a claim that every GPU or future export has the same behavior.

The managed sampler now selects the top 50 candidates before sorting them, preserving
score ties, repetition penalties, nucleus ordering and random draws. One recorded
comparison reduced 4096 eight-codebook sampling calls from 1025 ms to 90 ms, and the
reference reply from 1.47 s to 1.22 s, with identical text and speech-code SHA256.
This checks conversion/sampling consistency, not human-rated voice or answer quality.
Run explicit `MiniMindOAccelerationTests` to reproduce performance and qualification
observations; rejected GPU/thread configurations are reported, never auto-selected.

SiriAI acceptance feeds generated speech PCM through the same capture interface,
loads the real Silero/SmartTurn models, renders the model's 24 kHz PCM and completes
a second audio turn. It does not test a physical microphone, speaker or acoustic echo.


### Omni End-to-End endpointing and latency

After `minimind-o`, apply the checked `omni-turns` migration to its exact baseline.
The local conversation mode is displayed as **Omni End-to-End**; the serialized
`RealtimeSpeech` enum name and ordinal remain compatible. Local Omni voice always
uses provider VAD and turn detection. Startup repairs and persists legacy profiles
whose false detector flags previously overrode the enabled scene. The simple
settings page exposes both required detectors and their model selectors.

Two paced generated-speech turns with an above-energy-threshold noise tail loaded
real MiniMind-o, Silero and SmartTurn. Measured input-end to submission was 348/332 ms;
input-end to first nonzero software-rendered PCM was 1660/2176 ms. Input-end means the
end of the generated input buffer, including its own trailing silence. These are two
observations on the current Mac, not p95, microphone-to-speaker latency, or an
extremely-low-latency guarantee. No dropped or expired capture blocks were observed.
Separate ASR/TTS was not loaded. Human response quality was not assessed.

The current SiriAI provider still returns a complete waveform. The released model
consumes completed audio turns, not continuous audio during its own generation.
Capture/playback concurrency and VAD/turn detection do not establish native model
full duplex. The fixture has no qualified acoustic echo control; diagnostics report
half-duplex echo suppression. A physical speakerphone additionally needs verified
AEC, double-talk handling and interruption/recovery. Genuine model-native Full-duplex
requires a model and provider with a continuous input/output session contract;
conversion, naming and shorter silence thresholds cannot supply that capability.
