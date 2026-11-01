# License

## MNN Unity Package

Copyright (c) 2024 Eitan Wong

Licensed under the Apache License, Version 2.0 (the "License");
you may not use this file except in compliance with the License.
You may obtain a copy of the License at

    http://www.apache.org/licenses/LICENSE-2.0

Unless required by applicable law or agreed to in writing, software
distributed under the License is distributed on an "AS IS" BASIS,
WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
See the License for the specific language governing permissions and
limitations under the License.

---

## MNN Inference Engine

This package contains native libraries from [MNN](https://github.com/alibaba/MNN),
which is licensed under the Apache License, Version 2.0.

Copyright (c) Alibaba Group Holding Limited

Licensed under the Apache License, Version 2.0 (the "License");
you may not use this file except in compliance with the License.
You may obtain a copy of the License at

    http://www.apache.org/licenses/LICENSE-2.0

Unless required by applicable law or agreed to in writing, software
distributed under the License is distributed on an "AS IS" BASIS,
WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
See the License for the specific language governing permissions and
limitations under the License.

---

## CosyVoice3-MNN Reference Implementation

The managed CosyVoice RAS, flow and HiFT pipeline is adapted from the
[CosyVoice3-MNN reference runners](https://github.com/Nian27/CosyVoice3-MNN),
licensed under the Apache License, Version 2.0.

Copyright 2026 Nian27

Changes: managed C# execution through the bundled official MNN APIs, managed
sampling and signal processing, cancellation, validation and Unity integration.
The Apache License 2.0 full text below applies to this attribution as well.
Model weights are downloaded separately from their upstream repository.

---

## FireRedTTS3 Reference Implementation

The managed FireRedTTS3 pipeline follows [fireredtts3-mnn 0.2.2](https://huggingface.co/yunfengwang/fireredtts3-mnn)
by yunfengwang and [FireRedTTS3](https://github.com/FireRedTeam/FireRedTTS3),
licensed under Apache-2.0. The full Apache license below applies to this attribution.
Changes: managed C# tokenization, signal processing, graph orchestration, validation,
cancellation and Unity integration. Weights and tokenizer files are downloaded separately.

---

## MOSS-TTS-Nano Reference Implementation

The managed MOSS Nano prompt construction, cached decoding and codec pipeline
follow OpenMOSS/MOSS-TTS-Nano's public ONNX runtime and export descriptions:
https://github.com/OpenMOSS/MOSS-TTS-Nano . Its root LICENSE is Apache-2.0;
the Apache License 2.0 full text below applies to this attribution as well.
The managed implementation uses existing MNN APIs and does not distribute the
upstream ONNX/Python runtime. ModelScope exports by kiven2008 and ZhangZes
retain their original README metadata and are downloaded separately.
SentencePiece vocabulary parsing and BPE are implemented in managed C#;
SentencePiece 0.2.1 is used only as an independent test reference.

## Qwen3-TTS Explicit KV Reference Pipeline

The managed explicit KV prompt construction and generation follow the Python
fallback in [qwen3-tts-mnn 0.3.1](https://pypi.org/project/qwen3-tts-mnn/) by
yunfengwang, licensed under Apache-2.0. The full Apache License below applies.
Changes: managed C# orchestration, BPE, binary16 row reads, sampling, public MNN
graph projections, cancellation, validation and Unity integration. The package
does not include or load the publisher's native extension. Model assets and
prepared reference voice are downloaded separately; their original metadata
and attribution are preserved.

## DhVaani-0.5 MNN Reference Pipeline

The managed DhVaani character frontend, flow schedule, mel analysis and Vocos
synthesis follow Bbkblo/DhVaani-0.5-MNN revision
9ecccbbba2d6269dc083ab1975800de8b0dbe1c1:
https://huggingface.co/Bbkblo/DhVaani-0.5-MNN . The publisher identifies the
repository as Apache-2.0. The Apache License 2.0 full text below applies to
this attribution as well. This package adds no C++, JNI or custom native
exports; model weights and binary DSP assets are downloaded separately.

## Piper Lessac and eSpeak-NG Data

Piper Lessac model files are downloaded separately from
[ekremabiii/pipertts_mnn](https://huggingface.co/ekremabiii/pipertts_mnn).
The fixed installation also downloads English phonemizer data and the original
MODEL_CARD from [csukuangfj/vits-piper-en_US-lessac-high](https://huggingface.co/csukuangfj/vits-piper-en_US-lessac-high).
The model card links the Lessac Blizzard 2013 dataset terms; the package license
does not replace those terms. eSpeak-NG is an external GPL-3.0-or-later program
and its data retains its upstream license. Neither executable nor voice weights
are embedded in this package.

## Apache License 2.0 Full Text

                                 Apache License
                           Version 2.0, January 2004
                        http://www.apache.org/licenses/

   TERMS AND CONDITIONS FOR USE, REPRODUCTION, AND DISTRIBUTION

   1. Definitions.

      "License" shall mean the terms and conditions for use, reproduction,
      and distribution as defined by Sections 1 through 9 of this document.

      "Licensor" shall mean the copyright owner or entity authorized by
      the copyright owner that is granting the License.

      "Legal Entity" shall mean the union of the acting entity and all
      other entities that control, are controlled by, or are under common
      control with that entity. For the purposes of this definition,
      "control" means (i) the power, direct or indirect, to cause the
      direction or management of such entity, whether by contract or
      otherwise, or (ii) ownership of fifty percent (50%) or more of the
      outstanding shares, or (iii) beneficial ownership of such entity.

      "You" (or "Your") shall mean an individual or Legal Entity
      exercising permissions granted by this License.

      "Source" form shall mean the preferred form for making modifications,
      including but not limited to software source code, documentation
      source, and configuration files.

      "Object" form shall mean any form resulting from mechanical
      transformation or translation of a Source form, including but
      not limited to compiled object code, generated documentation,
      and conversions to other media types.

      "Work" shall mean the work of authorship, whether in Source or
      Object form, made available under the License, as indicated by a
      copyright notice that is included in or attached to the work
      (an example is provided in the Appendix below).

      "Derivative Works" shall mean any work, whether in Source or Object
      form, that is based on (or derived from) the Work and for which the
      editorial revisions, annotations, elaborations, or other modifications
      represent, as a whole, an original work of authorship. For the purposes
      of this License, Derivative Works shall not include works that remain
      separable from, or merely link (or bind by name) to the interfaces of,
      the Work and Derivative Works thereof.

      "Contribution" shall mean any work of authorship, including
      the original version of the Work and any modifications or additions
      to that Work or Derivative Works thereof, that is intentionally
      submitted to Licensor for inclusion in the Work by the copyright owner
      or by an individual or Legal Entity authorized to submit on behalf of
      the copyright owner. For the purposes of this definition, "submitted"
      means any form of electronic, verbal, or written communication sent
      to the Licensor or its representatives, including but not limited to
      communication on electronic mailing lists, source code control systems,
      and issue tracking systems that are managed by, or on behalf of, the
      Licensor for the purpose of discussing and improving the Work, but
      excluding communication that is conspicuously marked or otherwise
      designated in writing by the copyright owner as "Not a Contribution."

      "Contributor" shall mean Licensor and any individual or Legal Entity
      on behalf of whom a Contribution has been received by Licensor and
      subsequently incorporated within the Work.

   2. Grant of Copyright License. Subject to the terms and conditions of
      this License, each Contributor hereby grants to You a perpetual,
      worldwide, non-exclusive, no-charge, royalty-free, irrevocable
      copyright license to reproduce, prepare Derivative Works of,
      publicly display, publicly perform, sublicense, and distribute the
      Work and such Derivative Works in Source or Object form.

   3. Grant of Patent License. Subject to the terms and conditions of
      this License, each Contributor hereby grants to You a perpetual,
      worldwide, non-exclusive, no-charge, royalty-free, irrevocable
      (except as stated in this section) patent license to make, have made,
      use, offer to sell, sell, import, and otherwise transfer the Work,
      where such license applies only to those patent claims licensable
      by such Contributor that are necessarily infringed by their
      Contribution(s) alone or by combination of their Contribution(s)
      with the Work to which such Contribution(s) was submitted. If You
      institute patent litigation against any entity (including a
      cross-claim or counterclaim in a lawsuit) alleging that the Work
      or a Contribution incorporated within the Work constitutes direct
      or contributory patent infringement, then any patent licenses
      granted to You under this License for that Work shall terminate
      as of the date such litigation is filed.

   4. Redistribution. You may reproduce and distribute copies of the
      Work or Derivative Works thereof in any medium, with or without
      modifications, and in Source or Object form, provided that You
      meet the following conditions:

      (a) You must give any other recipients of the Work or
          Derivative Works a copy of this License; and

      (b) You must cause any modified files to carry prominent notices
          stating that You changed the files; and

      (c) You must retain, in the Source form of any Derivative Works
          that You distribute, all copyright, patent, trademark, and
          attribution notices from the Source form of the Work,
          excluding those notices that do not pertain to any part of
          the Derivative Works; and

      (d) If the Work includes a "NOTICE" text file as part of its
          distribution, then any Derivative Works that You distribute must
          include a readable copy of the attribution notices contained
          within such NOTICE file, excluding those notices that do not
          pertain to any part of the Derivative Works, in at least one
          of the following places: within a NOTICE text file distributed
          as part of the Derivative Works; within the Source form or
          documentation, if provided along with the Derivative Works; or,
          within a display generated by the Derivative Works, if and
          wherever such third-party notices normally appear. The contents
          of the NOTICE file are for informational purposes only and
          do not modify the License. You may add Your own attribution
          notices within Derivative Works that You distribute, alongside
          or as an addendum to the NOTICE text from the Work, provided
          that such additional attribution notices cannot be construed
          as modifying the License.

      You may add Your own copyright statement to Your modifications and
      may provide additional or different license terms and conditions
      for use, reproduction, or distribution of Your modifications, or
      for any such Derivative Works as a whole, provided Your use,
      reproduction, and distribution of the Work otherwise complies with
      the conditions stated in this License.

   5. Submission of Contributions. Unless You explicitly state otherwise,
      any Contribution intentionally submitted for inclusion in the Work
      by You to the Licensor shall be under the terms and conditions of
      this License, without any additional terms or conditions.
      Notwithstanding the above, nothing herein shall supersede or modify
      the terms of any separate license agreement you may have executed
      with Licensor regarding such Contributions.

   6. Trademarks. This License does not grant permission to use the trade
      names, trademarks, service marks, or product names of the Licensor,
      except as required for reasonable and customary use in describing the
      origin of the Work and reproducing the content of the NOTICE file.

   7. Disclaimer of Warranty. Unless required by applicable law or
      agreed to in writing, Licensor provides the Work (and each
      Contributor provides its Contributions) on an "AS IS" BASIS,
      WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or
      implied, including, without limitation, any warranties or conditions
      of TITLE, NON-INFRINGEMENT, MERCHANTABILITY, or FITNESS FOR A
      PARTICULAR PURPOSE. You are solely responsible for determining the
      appropriateness of using or redistributing the Work and assume any
      risks associated with Your exercise of permissions under this License.

   8. Limitation of Liability. In no event and under no legal theory,
      whether in tort (including negligence), contract, or otherwise,
      unless required by applicable law (such as deliberate and grossly
      negligent acts) or agreed to in writing, shall any Contributor be
      liable to You for damages, including any direct, indirect, special,
      incidental, or consequential damages of any character arising as a
      result of this License or out of the use or inability to use the
      Work (including but not limited to damages for loss of goodwill,
      work stoppage, computer failure or malfunction, or any and all
      other commercial damages or losses), even if such Contributor
      has been advised of the possibility of such damages.

   9. Accepting Warranty or Additional Liability. While redistributing
      the Work or Derivative Works thereof, You may choose to offer,
      and charge a fee for, acceptance of support, warranty, indemnity,
      or other liability obligations and/or rights consistent with this
      License. However, in accepting such obligations, You may act only
      on Your own behalf and on Your sole responsibility, not on behalf
      of any other Contributor, and only if You agree to indemnify,
      defend, and hold each Contributor harmless for any liability
      incurred by, or claims asserted against, such Contributor by reason
      of your accepting any such warranty or additional liability.

   END OF TERMS AND CONDITIONS


---

## IndexTTS-2.5 Reference Implementation and Model Terms

The managed IndexTTS-2.5 pipeline follows [index-tts-2.5-mnn 0.1.1](https://huggingface.co/yunfengwang/IndexTTS-2.5-mnn)
by yunfengwang and [IndexTTS](https://github.com/index-tts/index-tts) by bilibili.
This implementation and the separately downloaded model are subject to the bilibili
Model Use License Agreement reproduced below (as published with IndexTeam/IndexTTS-2.5).
The package's Apache-2.0 notice does not replace those terms for this derived pipeline.
Changes: managed C# frontend, tokenization, graph orchestration, validation,
cancellation and Unity integration; model weights are not modified or bundled.

Any modifications made to the original model in this Derivative Work are not endorsed, warranted, or guaranteed by the original right-holder of the original model, and the original right-holder disclaims all liability related to this Derivative Work.

bilibili Model Use License Agreement

By clicking “I agree” to this bilibili Model Use License Agreement (“this Agreement”) , or by otherwise using any portion or element of the Model or any Derivative Work, you will be deemed to have recognized and accepted the content of this Agreement, which is effective immediately. If you do not agree to this Agreement, you must immediately cease all use and permanently delete the Model and any Derivative Works.

1. Definitions
1.1 “This Agreement”: means the bilibili Model Use License Agreement, including all of its terms and conditions.
1.2 “We”, “us”, or “our”: means bilibili , the original right-holder of the Model.
1.3 “You”: means any natural person or legal entity exercising rights granted by this Agreement and/or using the Model for any purpose and in any field of use.
1.4 “Model”: means the artificial-intelligence model named “bilibili indextts2”, including but not limited to model weights and final code, in each case only to the extent that such components are published by us at https://github.com/index-tts/index-tts.
1.5 “Derivative Work”: means any derivative of the Model, including without limitation:
 (i) any modification of the Model, model outputs, or their derivatives;
 (ii) any work based on the Model, model outputs, or their derivatives;
 (iii) any other machine learning model which is created by re-training, fine-tuning, quantizing, LoRA, parameter-efficient fine-tuning, or any other method involving incremental weights or merged checkpoints, in each case based on the Model, model outputs, or their derivatives.
1.6 “Use”: means downloading, copying, training, modifying, creating Derivative Works, distributing, publishing, running, fine-tuning, publicly displaying, communicating to the public, or otherwise exploiting the Model or any Derivative Work.

2. Scope of License and Restrictions
2.1 Subject to the terms and conditions of this Agreement, we grant you a worldwide, non-exclusive, non-transferable, royalty-free limited license to Use the Model or any Derivative Work based on the intellectual properties or other rights owned by Us embodied in the Model or any Derivative Work.
2.2 If You intend to Use, or have already Used, the Model or any Derivative Work, and either (i) your or any of your Affiliates’ products or services had more than 100 million monthly active users in the immediately preceding calendar month, or (ii) your or any of your Affiliates’ annual revenue in the immediately preceding calendar year exceeded RMB 1 billion, You must request a separated license from us, which We may grant to You in our sole discretion. You are not authorized to exercise any of the rights under this Agreement unless and until We have expressly granted You such rights in writing.
2.3 This Agreement is an open-source license for the Model in which we possess intellectual properties and other rights. It governs your Use of the Model only and does not limit any rights that we have regarding the Model.

3. Disclaimer and Risk Allocation
3.1 The Model and any outputs generated thereby are provided “AS IS,” without warranty of any kind, express or implied, including but not limited to warranties of merchantability, fitness for a particular purpose, non-infringement, absence of errors or omissions, continuity, accuracy, reliability, or stability. You are solely responsible for determining the appropriateness of using or redistributing the Model and assume all risks associated with exercising any rights granted under this Agreement.
3.2 You shall bear sole responsibility for any infringement, illegality, breach of contract, damages, fines, regulatory investigations, or other liabilities (including, without limitation, infringement of third-party patents, copyrights, trademarks, trade secrets, personality rights, data-protection rights, or any other rights) arising out of or related to your Use of the Model or any outputs generated thereby. We assume no joint, several, supplementary, or advance payment liability.
3.3 Under no circumstances shall we be liable to you or any third party for any direct, indirect, incidental, special, punitive, or consequential damages (including, without limitation, loss of data, business interruption, or loss of profits) arising out of or related to the Use of the Model, even if we have been advised of the possibility of such damages.
3.4 Additional Obligations for You and Downstream Recipients
a) You must ensure that any downstream recipient of the Model or any Derivative Work that you distribute complies with this Agreement, and you must impose appropriate contractual terms on such downstream recipients. If any downstream recipient breaches this Agreement, you shall be responsible for the consequences thereof.
b) You must retain all original copyright notices and a copy of this Agreement in every copy of the Model or any Derivative Work that you Use.
c) You may not Use the bilibili indextts2 or any Derivative Work to improve any AI model, except for the bilibili indextts2 itself, its Derivative Works，or non-commercial AI models.

4. Compliance Obligations
4.1 Usage Restrictions
a) If you distribute a Derivative Work, you must clearly state in the distribution page or accompanying documentation: “Any modifications made to the original model in this Derivative Work are not endorsed, warranted, or guaranteed by the original right-holder of the original model, and the original right-holder disclaims all liability related to this Derivative Work.”
b) If your Use of the Model or any Derivative Work incorporates any third-party data or weights, you must obtain all necessary authorizations on your own and bear full responsibility for compliance.
c) You may not Use the Model or any Derivative Work for any purpose that violates the laws or regulatory requirements of the jurisdiction where the outputs and/or the Model are generated or used (including, without limitation, generating false information, discriminatory content, or content that infringes privacy).
d) If the Model or any Derivative Work is capable of generating content, you must ensure that such content does not violate the laws or regulatory requirements of the applicable jurisdiction (including, without limitation, generating false information, discriminatory content, or content that infringes privacy).
4.2 Prohibited High-Risk Use
You must ensure that the Model and any Derivative Work are not deployed, directly or indirectly, in high-risk scenarios such as medical diagnosis, autonomous driving, military applications, critical-infrastructure control, large-scale biometric surveillance, or automated decision-making (e.g., credit or employment evaluations). If you insist on such deployment, you must independently complete all compliance obligations under applicable laws and regulations (including but not limited to GDPR, CCPA, HIPAA, export-control laws, and AI-specific regulations), and we shall bear no liability for any consequences arising therefrom.
4.3 Infringement Liability
Should any third party raise claims against you with respect to any Derivative Work you develop or your Use of the Model or any Derivative Work, you shall bear full and independent responsibility for defending against and resolving such claims. If your actions cause us to incur any third-party claims, administrative penalties, or other losses, you shall indemnify us for all losses we thereby suffer, including but not limited to attorney fees, litigation costs, damages, and fines, and shall take all necessary measures to eliminate any adverse impact on us.

5. Reserved Rights
5.1 We reserve the right to revoke the license granted to you under this Agreement in the event of your breach. Upon revocation, you must immediately cease all Use and permanently delete all copies of the Model and any Derivative Work. Sections 3 and 6 of this Agreement shall survive termination of this Agreement under this circumstance.
5.2 Nothing in this Agreement grants you any right to use our trade names, trademarks, service marks, or product names, except as reasonably and customarily required to describe the origin of the Model or any Derivative Work—such as reproducing the content of a NOTICE file under Section 3.4 of this Agreement.
5.3 If you or any of your Affiliates institutes or participates in any legal proceeding (including any cross-claim or counterclaim in a lawsuit) against us or any of our Affiliates, alleging that the Model or any output or any portion thereof infringes any intellectual property or other rights that you own or control, all licenses granted to you under this Agreement shall terminate automatically as of the date such proceeding is filed.

6. Governing Law and Dispute Resolution
6.1 This Agreement shall be governed by and construed in accordance with the laws of the People’s Republic of China.
6.2 In the event of any dispute arising out of or in connection with this Agreement, the parties shall first attempt to resolve such dispute through friendly negotiation. If negotiation fails, the dispute shall be submitted to the Shanghai Arbitration Commission for arbitration in accordance with its then-effective arbitration rules. The arbitration award shall be final and binding on both parties. The prevailing party shall be entitled to recover reasonable costs, including notarization and investigation fees, arbitration costs, attorneys’ fees, and travel expenses.

7. Severability
If any provision of this Agreement is held to be invalid or unenforceable, the remaining provisions shall remain in full force and effect. The invalid or unenforceable provision shall be replaced with a valid and enforceable provision that, to the maximum extent permitted by law, most closely reflects the original intent of the invalid or unenforceable provision.

8. Version Updates
We may release new versions of the AI Model Use License Agreement. Any new version will apply only to Uses occurring after the date of its release. If you obtained the Model under an earlier version, the new version will not have retroactive effect; nevertheless, you are encouraged to adopt the new version voluntarily.

9. Language Version
In the event of any discrepancy or conflict between the English-language version set forth above and the Chinese-language version of this bilibili Model Use License Agreement, the Chinese-language version shall prevail for all purposes and shall govern the rights and obligations of the parties.


## Zipformer speech recognition frontend and decoding

The managed Zipformer v1 feature extraction and greedy transducer orchestration
follow sherpa-mnn / sherpa-onnx and kaldi-native-fbank algorithms, under Apache-2.0.
Copyright (c) 2023 Xiaomi Corporation and the respective Kaldi/kaldi-native-fbank
contributors. Changes: managed C# sparse mel filters, reusable buffers, state
capture before feedback, cancellation, file validation and Unity Studio routing.
Model weights and test recordings are downloaded separately and are not bundled
with the package. Their repositories retain their own model/data license terms.


## SenseVoiceSmall speech recognition frontend

The managed SenseVoiceSmall frontend follows the published sherpa-mnn SenseVoice
feature extraction, fixed CMVN and greedy CTC decoding algorithms, under Apache-2.0.
Copyright (c) Xiaomi Corporation and contributors. Changes: managed sparse mel
filters, LFR/CMVN preparation, cancellation, validation and Unity Studio routing.
The model files retain their ModelScope license and are not bundled.


## Silero voice activity detection

The managed 16 kHz context/state orchestration and probability-to-interval
algorithm follow [snakers4/silero-vad](https://github.com/snakers4/silero-vad),
under the MIT License, Copyright (c) 2020-present Silero Team.
Changes: managed C# processing, cancellation, owned results, fixed minimum
durations, public MNN serialized-graph packing and Unity Studio routing.
Model weights are downloaded separately; the pinned conversion repository
retains its own license metadata.

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies
of the Software, and to permit persons to whom the Software is furnished to do
so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.


## Smart Turn endpoint detection

The managed eight-second tail preparation and endpoint orchestration follow
[pipecat-ai/smart-turn](https://github.com/pipecat-ai/smart-turn),
revision `4786657e242dfe77dd138699ac564ee074a2a543`.
Changes: C# normalization and centered Whisper feature extraction, phase-preserving
tail-only resampling, cancellation, immutable asset verification and Studio routing.
The Whisper feature algorithm also follows Hugging Face transformers (Apache-2.0).
Model weights are downloaded separately; the conversion repository retains its
Apache-2.0 metadata. Validation-only recordings remain outside the package under
their source dataset licenses (including CC-BY-4.0 Pipecat test data).

BSD 2-Clause License

Copyright (c) 2024–2025, Daily

Redistribution and use in source and binary forms, with or without
modification, are permitted provided that the following conditions are met:

1. Redistributions of source code must retain the above copyright notice, this
   list of conditions and the following disclaimer.

2. Redistributions in binary form must reproduce the above copyright notice,
   this list of conditions and the following disclaimer in the documentation
   and/or other materials provided with the distribution.

THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS"
AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE
IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT HOLDER OR CONTRIBUTORS BE LIABLE
FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL
DAMAGES (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR
SERVICES; LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER
CAUSED AND ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY,
OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE
OF THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.


### Pocket-TTS managed pipeline

The managed Pocket pipeline follows [developerabu/pocket-tts-mnn](https://huggingface.co/developerabu/pocket-tts-mnn),
revision `4e75a38d20a9c532ed55591186584a244c381095`, including its reference adaptation of
KevinAHM/pocket-tts-onnx. Changes: C# SentencePiece unigram, MNN-only voice encoding and priming,
restored exported Shape slice semantics, immutable asset verification, cancellation, graph/reference reuse,
and Studio integration. No publisher Python/native runtime is loaded. Original model weights, based on
Kyutai Labs Pocket TTS `english_2026-04` via IgnitiveLabs/PocketTTS-ONNX, remain CC-BY-4.0:
https://creativecommons.org/licenses/by/4.0/. Weights and recordings are downloaded separately.

MIT License

Copyright (c) 2026 developerabu

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.

## CosyVoice-300M-MNN first-generation pipeline

The managed MNNCosyVoice300M prompt, RAS, flow and HiFT pipeline follows
[FunAudioLLM/CosyVoice](https://github.com/FunAudioLLM/CosyVoice),
revision d2dea3d92846b17e89e25513e855caed64bfc2f5, licensed under Apache-2.0.
The Apache License 2.0 full text in this file applies to this attribution.
Changes: C# numeric asset loading, tokenization, native graph orchestration,
managed sampling/DSP, cancellation and Unity Studio integration.
Weights and Whisper vocabulary are separate model assets.

Whisper multilingual tokenizer vocabulary and special-token definitions follow
[openai/whisper](https://github.com/openai/whisper), under the MIT License.

Copyright (c) 2022 OpenAI

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies
of the Software, and to permit persons to whom the Software is furnished to do
so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
