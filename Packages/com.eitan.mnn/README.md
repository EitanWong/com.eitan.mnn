# MNN for Unity

Unity APIs and editor tools for MNN 3.6.1 inference, language and multimodal models,
retrieval, speech synthesis and image generation. Requires Unity 2021.3 or later;
the verified Unity version is 2021.3.45f2.

## Support and ABI

C# P/Invoke calls official C++ symbols in the bundled library, without additional
C++ sources, custom C exports or a companion bridge. The binding targets **MNN 3.6.1,
Apple clang / libc++ ABI v1, macOS 11+, 64-bit Mono**. STL marshalling, aggregate
returns and virtual dispatch depend on that exact compiler contract. This is a
project-maintained binding, not an official portable MNN C# SDK. An arbitrary
same-version MNN library is not interchangeable with the bundled build.

CPU inference has been verified in arm64 Editor/Player and an x86_64 Player under
Rosetta on Apple Silicon. Intel hardware and x86_64 GPU inference have not been
verified. Metal checks cover macOS arm64 Mono. Other operating systems and IL2CPP
reject inference before native calls; retained native plugins do not imply managed
inference support. CoreML/NPU support is not claimed.

## Install

In Unity Package Manager, select **Add package from git URL**:

```text
https://github.com/EitanWong/com.eitan.mnn.git?path=/Packages/com.eitan.mnn#dev
```

The repository contains a development project, so the package subdirectory is
required. Use `#main` for merged code or an existing commit SHA for a reproducible
installation. The old `upm` branch does not contain this development snapshot.

## Check the native library

```csharp
using MNN.Unity;
using UnityEngine;

public class MNNExample : MonoBehaviour
{
    private void Start()
    {
        if (!MNNPlatformSupport.IsSupported)
        {
            Debug.LogError(MNNPlatformSupport.UnsupportedReason);
            return;
        }

        if (MNNVersion.IsLoaded())
            Debug.Log($"MNN Version: {MNNVersion.GetVersion()}");
        else
            Debug.LogError("MNN failed to load");
    }
}
```

This checks availability, not model correctness. Import **Basic Inference** from
Package Manager for graph/tensor examples, then supply a compatible local model.

## Models and editor tools

Open **Window > MNN > Model Manager** to browse the public ModelScope MNN catalog
and download complete repositories with configuration, tokenizer and external
weights. Downloads support background work, pause/resume and parallel transfers.
Use `Assets/StreamingAssets/MNN/Models` for models included in an application build
and `Application.persistentDataPath/MNN/Models` for runtime downloads.

Open **Window > MNN > Chat Studio** for streaming conversations, image/WAV inputs,
Omni speech replies, search, folders, embeddings and reranking. Its 23 task entries
include SD 1.5 image generation and Supertonic, Bert-VITS2 Chinese and Piper English
speech synthesis. Piper requires upstream eSpeak-NG. Sana editing runs, but semantic
color editing has not met the quality check. Tasks without a dedicated runtime show
model preparation guidance; full-duplex live audio and Sherpa streaming ASR are not
implemented. See [Chat Studio](Documentation~/ChatStudio.md) for task limits.

Auto acceleration prefers available Metal with task-specific CPU compatibility.
Embedding, Piper, Sana and the Bert-VITS2 generator use CPU. Omni speech uses a
High-precision Metal main runtime and CPU media processors. Backend reports are
not evidence that every operator runs on the GPU.

Model weights, local conversation history, caches, generated media and test logs
are excluded from source control. Tests do not automatically download large models.

## Documentation

- [Documentation index](Documentation~/README.md)
- [API reference](Documentation~/API.md)
- [Acceleration and fallback](Documentation~/Acceleration.md)
- [Inference evidence and reproduction](Documentation~/Testing.md)
- [Native build and ABI assumptions](Native~/README.md)
- [Project structure and conventions](Documentation~/ProjectStructure.md)
- [Repository maintenance and ignored files](Documentation~/RepositoryManagement.md)
- [Basic example](Samples~/BasicExample/README.md)
- [Changelog](CHANGELOG.md)

Licensed under [Apache License 2.0](LICENSE.md).
