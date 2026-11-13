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
