using System;
using System.IO;
using System.Linq;

namespace MNN.Unity
{
    /// <summary>Pinned published turn deployments shared by runtime, Editor and installer.</summary>
    internal static class MNNTurnModelProfiles
    {
        internal static readonly MNNModelRegistration[] All =
        {
            Create("SmartTurn-v3.2-FP32-MNN", "0343e4b740d4b460dfedb5b2368fd6f8773b08a2", false, MNNSmartTurnModel.IsComplete, new[]
            {
                File("LICENSE", 1301L, "0d66364067f678c08586ebb60a16a2aed4fa081ec11057df35585759ce0e774f"),
                File("README.md", 2757L, "369b873345949dbab983559415355700848329d5906acce27b417892c1fec8d1"),
                File("manifest.json", 1305L, "2b6e1d8af32858b90fe6eae83562517cb1dfe2eb6a1e97fd5ffa423696f4cf0d"),
                File("mnn_optimization.json", 4373L, "3c4feedc0a2d0ffec5a0e10f3967bc397f2f0d4e715859fd4f7b336e70c7e708"),
                File("mnn_smart_turn.json", 150L, "f1f9b85747387919615fdc3e8cae30511989cf35601acf5ac6242a756a572b79"),
                File("smart-turn-v3.2-gpu-protocol.json", 574L, "011d29771e9473e366c4e9fcfbd0a0761d93cadec89c8a798872547fc01e5fa9"),
                File("smart_turn_v32_fp32.mnn", 32061112L, "defa0e643e16dc7226fa5f80b5d07d813769df323423387ec3cfd01f78aa91cc"),
            }, null, null, new[] { "SmartTurn v3.2 FP32 MNN" }, ""),
            Create("NAMO-Turn-Detector-v1-Multilingual-MNN", "cb4aeb2575150e2ee93ce1b6e0852767be074fac", true, MNNNamoTurnDetectorModel.IsComplete, new[]
            {
                File("LICENSE", 11339L, "7ecbb9c5ba3cc22056ffc55eee42feadd7e7b9f185f9cbe1abdd584bdc6e548a"),
                File("README.md", 3252L, "3101c38596a9ea063996a373fbe069acc683b3194439d58d1fb89172d838ef5d"),
                File("SOURCE_NOTICE.md", 567L, "3ff2883f1cfe1074fe893a0cecbc669d33ea826c15ec5e1911d1697d3681f4f4"),
                File("UPSTREAM_MODEL_CARD.md", 9722L, "2377d8a8990206aaf2ae12925291dfbcc2915f3ae4398b29f7beee570c434947"),
                File("configuration.json", 30L, "cf160099161fefe3bd7b8d1e9c55d4594552e7c9cc0bd319bb4a3664534f76a9"),
                File("manifest.json", 1443L, "355c4c8664e6001b776b96bd6fa33ad7d361b04ee896439bf97d321d32339336"),
                File("mnn_namo_turn_detector.json", 377L, "a65d274750e47588d970b63bdcac584386a3e38453eca5cd766871cf57936ff9"),
                File("mnn_optimization.json", 9958L, "4b9957f40f6a6d05cfb2a9a47347f7af91c8408e1b5b39e53d24673e76c7c554"),
                File("namo_tiled_fp16.mnn", 616408948L, "f60d5c6c378b706743c7c952e1706662ea2cb22f53550061cba23c56a66056ef"),
                File("tokenizer.txt", 13511105L, "3ab50bc781a6c1ee6c57c1c43b6eba0ba1e38617e866ab127f78167be2c89f22"),
            }, null, null, new[] { "NAMO Turn Detector v1 Multilingual MNN" }, ""),
            Create("VoxTurn-v1-MNN", "7feb7ffeae12bb40c55a997c102aa92342bd15ba", false, MNNVoxTurnModel.IsComplete, new[]
            {
                File("BACKBONE_LICENSE.txt", 11339L, "7ecbb9c5ba3cc22056ffc55eee42feadd7e7b9f185f9cbe1abdd584bdc6e548a"),
                File("LICENSE", 19347L, "41003d4a74749c0220e33dd415042164b5a1093ed401f36277234f772d22d3d0"),
                File("README.md", 4162L, "b1d563ed55a8a456c9dba0ad62e762762a9e1b7f48c5da8bb77400c8ff99004b"),
                File("SOURCE_NOTICE.md", 1527L, "268e88a1ccb414e8a4b2fa19fb8a8988eab2aff2c1867f20bbd70071e4839db8"),
                File("audio_encoder.mnn", 745762852L, "d0e501e2b99b89f329dadd0dfd95bd6eaa9057801c08e6777965ce22260743a1"),
                File("configuration.json", 31L, "cb4388f57ed0a95fc8221bf054169e31759374993441c9825499dd0f07b27e22"),
                File("decoder_0.mnn", 903482496L, "7b2a78b40b28fd2603503ba99a12d964f092c7327463966a9af4220fed589417"),
                File("decoder_1.mnn", 903490920L, "56cbbb284a47f36bc14a6c77f096b323dfddaee189eb89cd7c1e49b70f6f6e14"),
                File("embeddings_bf16.bin", 311164928L, "ab067cf4287b06781cd926874df9d0917e79a2360381e661b5010b99651ffc09"),
                File("head-bias.bin", 4L, "07b366c6c7f976eae8961064cd35c1f5ccf6a1952acd6fd2377d3bde96addcd0"),
                File("head-weight.bin", 4096L, "f3e969327e2b3c0d40e783753ff265ee465e12febd9f9d22c8545362f61b1c44"),
                File("manifest.json", 2975L, "59603260e99b5f20389a4ba0eaf406575fe4f8964aef362ccb94116805a4194c"),
                File("mel-filters.bin", 102912L, "c342ed6d06c90b42990bf9e4a3a07ca54f3f1422f7bc3c66b3177a774395df5a"),
                File("mel-window.bin", 1600L, "c692d19f0b2bd745f0dfc967261c3e3d21a2473e485a677d219e9b4a73e0218e"),
                File("mnn_optimization.json", 56134L, "bc26ef1e4e49967570ac67db7191544a6d836c0297fb08cf035850d6d5a60eed"),
                File("mnn_voxturn.json", 562L, "08abf775baa673823125c801615e540d7290bc982421b304666264e9f8f4ddfe"),
                File("questions.json", 9295L, "3a2a7bc92f6c348515e728a39d8e3b5894837290c7c7c1d50fb17f3175d91f80"),
                File("rotary-cos.bin", 1048576L, "3b38a67a631d144c72084309b7bbc01a1bce8ddc7676bc5f1442fb32f1bd03f1"),
                File("rotary-sin.bin", 1048576L, "2e8e92f71df9084e7b6885c8e937d2833356facfcbe1d99dbf8c5b20bc3e390d"),
                File("tokenizer.json", 5973573L, "5ce92320f50d7bc93d785677d514c3a9561c55d3c44caee88ed932ed0740a5fa"),
                File("voxturn-runtime.json", 2633L, "4216053d50e6876dcbf34f9e53f884eb7a18662f46f8322e5a28eec07d49a41a"),
            }, null, null, new[] { "VoxTurn v1 MNN" }, "Noncommercial license. "),
            Create("CueLight-Nano-MNN", "de71212bb0a644d97b6f3a1d28753d3e59d60349", false, MNNCueLightModel.IsComplete, new[]
            {
                File("CONVERSION_NOTES.txt", 951L, "08dadc1730cc0e56249f8ddfeb21905aa558a33484c61e768f2b8e33e6000adf"),
                File("LICENSE", 11358L, "cfc7749b96f63bd31c3c42b5c471bf756814053e847c10f3eb003417bc523d30"),
                File("NOTICE.txt", 2210L, "1c8e62f29dae4a633549f1febed9f22f76bbc2856dcef5ac57cd7419f8570824"),
                File("README.md", 3785L, "8cc518da1753c3acde1203748758796371e5bd34ea9d9bfa45c3a7cf053c474f"),
                File("THIRD_PARTY_LICENSES.txt", 2433L, "d339bafb27c3df43a5400c81687a088925c87a318eb969353f6496dfb8ba64d1"),
                File("configuration.json", 35L, "b4532ab265993475c72389291071986e86c261a40197c3a1fbed9858d1dd0392"),
                File("cuelight_coefficients.bin", 67392L, "39491c322c7c3bf1b33f9231a6b0fae95f159b4315d021dd99e79dedfbc600d0"),
                File("manifest.json", 1713L, "7743473a0eacbd463985a7fd53a934b2e9fc1cf20dac4423f96cc1b453730486"),
                File("mnn_cuelight.json", 666L, "d05d15b2f5ee12aeedbf7c3a9756b93424c5aaa22b4a1e3c531dd0fa0f52244f"),
                File("mnn_optimization.json", 7355L, "159bf5aef335ddc5bc080c476c4a493fe959dd0e62b0ec2e38a0589c204bb054"),
                File("step_accurate_fp32.mnn", 2000936L, "d73504f638574627ea3fa3da2d7074948357219536c5401dc476a455a7be52a3"),
                File("step_fp32.mnn", 1993228L, "e8e7dc004c9decc48c25cc4b94b25c2c94d5b35461410bc78ca1619e7a0112d3"),
            }, null, null, new[] { "CueLight Nano MNN" }, ""),
            Create("CueLight-Nano-SSM-MNN", "b70484bd0699cc89e960240db55efb42e6b93fd6", false, MNNCueLightModel.IsComplete, new[]
            {
                File("CONVERSION_NOTES.txt", 1086L, "3ad364fa37b8d306689bc9d1dd1cc0bf89f32b5690a754e9cdf2b94cd6239872"),
                File("LICENSE", 11358L, "cfc7749b96f63bd31c3c42b5c471bf756814053e847c10f3eb003417bc523d30"),
                File("NOTICE.txt", 2210L, "1c8e62f29dae4a633549f1febed9f22f76bbc2856dcef5ac57cd7419f8570824"),
                File("README.md", 3786L, "56580ded62606cac6c12d04cf2abd1d7dc16e7a782a57337698496178504e167"),
                File("THIRD_PARTY_LICENSES.txt", 2433L, "d339bafb27c3df43a5400c81687a088925c87a318eb969353f6496dfb8ba64d1"),
                File("configuration.json", 35L, "b4532ab265993475c72389291071986e86c261a40197c3a1fbed9858d1dd0392"),
                File("cuelight_coefficients.bin", 67392L, "39491c322c7c3bf1b33f9231a6b0fae95f159b4315d021dd99e79dedfbc600d0"),
                File("manifest.json", 1507L, "c765049863e895381edc074fa3dccd227069b6c1ee5d14e0bc6ebb982b22e80a"),
                File("mnn_cuelight.json", 648L, "19aac737f16a912b1e87777ef2ab427e4ad8462bcfd5154da9624119aa8df8f5"),
                File("mnn_optimization.json", 7702L, "a467195f17dbd0855d2b6263fbb99dcf4b704f20c987bf5837bdd27e76abd119"),
                File("step_fp32.mnn", 2007084L, "8d540a682838c75c8c02b5c6995c5ef23c96c0a084b5257739177be876c396e7"),
            }, null, null, new[] { "CueLight Nano SSM MNN" }, ""),
            Create("TurnSense-SmolLM2-135M-MNN", "c5104f16daa1f2f63586b50b847655d98b70cd47", true, MNNTurnSenseModel.IsComplete, new[]
            {
                File("LICENSE", 11358L, "bfaa70e91716168bdfc085d372eb20f8f65b9c23f31c257cd04084b1e8f9fbcf"),
                File("NOTICE.txt", 691L, "b9b795d7a7b48f6535908d2fd9c620a0fd8f8c40a02690c043a28f998c171126"),
                File("README.md", 3523L, "f4b1972f5b2420de96759d72030d8cebd05960a1780ddd2e6bff8d8542fcc5d9"),
                File("UNICODE-LICENSE.txt", 1995L, "e7a93b009565cfce55919a381437ac4db883e9da2126fa28b91d12732bc53d96"),
                File("configuration.json", 30L, "cf160099161fefe3bd7b8d1e9c55d4594552e7c9cc0bd319bb4a3664534f76a9"),
                File("manifest.json", 1548L, "401a3c642ae65f8ed261cbdd9971373a6d33c0290d7fef896836f183b73d3ef0"),
                File("mnn_optimization.json", 19545L, "43e7da9580d8af9d8a3f1a180ac34f34669f969054aeed25085292bb091fa24f"),
                File("mnn_turnsense.json", 379L, "0fe4e77b34762d23d1bdff94b1fada82f765fa3c0a041b0c6ab04c7094d36cef"),
                File("model_fp32.mnn", 540120312L, "5b7b543d349ebaa2906cb6c21dd690c80f8f81e1ae1846cd1bbc952344b05757"),
                File("tokenizer-unicode.json", 9663L, "545939fab00ee08600f19fd23f99662326ca64ffc48ebc010caa37f4285b4fbd"),
                File("tokenizer.json", 3522828L, "f41789312f1e8ba7c3d23aed1506bafa29e298c0061d9c6bdc002bde3ef620b3"),
            }, null, null, new[] { "TurnSense SmolLM2 135M MNN" }, ""),
            Create("SmartTurn-v3.2-FP16-MNN", "588de6d553e248241e2f00c6d25c2337217a5b6b", false, MNNSmartTurnModel.IsComplete, new[]
            {
                File("LICENSE", 1301L, "0d66364067f678c08586ebb60a16a2aed4fa081ec11057df35585759ce0e774f"),
                File("README.md", 772L, "2a0bfa95ca11958b8726d8779f196834671808e7eb87905d10143b6caaa513f1"),
                File("configuration.json", 53L, "b8f2ec9ed20fdc00fc43ad60dc2261c05fea848236698fe232bb54e1cf9bdec7"),
                File("files.sha256.json", 897L, "3013bb7c1b17455d555c5f847bb4d9d2d9135710e1825bf3df56920ddc3ca048"),
                File("mnn_smart_turn.json", 150L, "61f714f6a93b99efe990f329ad6df236b5dcb5b0cf443210e8dc94a4b96edca2"),
                File("optimization.json", 9407L, "3eb223b9d3f00282e1c776e3747d853a049f9f226cf7527d7b8cf03f8c9594bf"),
                File("smart_turn_v32_fp16.mnn", 16103848L, "770e733e261874bd88986d4939e0ac25f16bdb2d2c101744d195704d2b960eac"),
                File("source.json", 223L, "c99f2ac2e2262b08f53182c76cd12e94081d8003f4a9bbe4c5b915af786366d1"),
            }, "SmartTurn-v3.2-MNN", null, new[] { "SmartTurn v3.2 FP16 MNN", "Smart Turn", "SmartTurn" }, ""),
            Create("Bairong-TurnSense-1.0-MNN", "6bbb56f979618ef53baa591a1469284fd4fb6a02", false, MNNBairongTurnSenseConfig.IsComplete, new[]
            {
                File("LICENSE", 11357L, "c71d239df91726fc519c6eb72d318ec65820627232b2f796219e87dcf35d0ab4"),
                File("README.md", 780L, "41e5712751108ebd4af6cfe01007cf32bf74bb36942ed873d5e9ddbd018c0296"),
                File("am.mvn", 11203L, "29b3c740a2c0cfc6b308126d31d7f265fa2be74f3bb095cd2f143ea970896ae5"),
                File("configuration.json", 53L, "b8f2ec9ed20fdc00fc43ad60dc2261c05fea848236698fe232bb54e1cf9bdec7"),
                File("files.sha256.json", 1012L, "a772dfc11f20cfbc6f117e2f8b4a3e88a5950dcca5b750b93276d2cfd59327f6"),
                File("mnn_bairong_turnsense.json", 355L, "86597c51097555081de5b652ad194689452f2e8704272be8b06393d270ddc331"),
                File("model.mnn", 100900820L, "3563439b4f1e19d136ef4734d1ee0db1e4dc7d1e9ae22a897587b658f1b62c87"),
                File("optimization.json", 9296L, "2ea44271bb328ac625542972d8860c6dd14733fae494460c193a8e7de4b1ad97"),
                File("source.json", 384L, "666de1cb5caad9d9a2d039684ab6c137dc63da9f58b72114dd87aa1ef6fe079c"),
            }, null, MNNBairongTurnSenseConfig.FileName, new[] { "Bairong TurnSense 1.0 MNN", "TurnSense 1.0", "Bairong TurnSense" }, ""),
            Create("Bairong-TurnSense-1.1-MNN", "f0024999d450a0ce446fbed37fa2ba9787ddb83a", false, MNNBairongTurnSenseConfig.IsComplete, new[]
            {
                File("LICENSE", 11357L, "c71d239df91726fc519c6eb72d318ec65820627232b2f796219e87dcf35d0ab4"),
                File("README.md", 780L, "4223c5a0344b8a67529214b3c1af32339496a9d8415fd58ae598f2a5df227c0f"),
                File("am.mvn", 11203L, "29b3c740a2c0cfc6b308126d31d7f265fa2be74f3bb095cd2f143ea970896ae5"),
                File("configuration.json", 53L, "b8f2ec9ed20fdc00fc43ad60dc2261c05fea848236698fe232bb54e1cf9bdec7"),
                File("files.sha256.json", 1012L, "47c8fbc0a2229eb66f0743961af8f47a3bc6afacba9a71cfd10dee7c6bbe58c5"),
                File("mnn_bairong_turnsense.json", 355L, "8ddd995d7f5a17e105f67dcb21e8266d022ca515bd9ff3e95858ffcb6b27e5b3"),
                File("model.mnn", 445277592L, "02dffa1b944160ba9005261a19d7b0c63bc618ca2f396402435b9fae52b34994"),
                File("optimization.json", 9321L, "5d05ab7d898355bdda3da5498cd4109fd61ac576b27546ceaaf117ed1fb61362"),
                File("source.json", 384L, "c9c4ea840d68eff82f84dd30aecfc101a5a6b52233216bb9b8945a66c00a0f74"),
            }, null, MNNBairongTurnSenseConfig.FileName, new[] { "Bairong TurnSense 1.1 MNN", "TurnSense 1.1", "Bairong TurnSense" }, ""),
            Create("LiveKit-Turn-Detector-Multilingual-MNN", "2890eaa8c710d2a28d33cfc1704e408a57b3be05", true, MNNLiveKitMultilingual.IsComplete, new[]
            {
                File("LICENSE", 5625L, "4f5b74db4fd26299a166688de8a3bde046fd7d9cc13ecab64532c24310bc18d7"),
                File("README.md", 1727L, "f0640d4dbcb75ff6660de5d94c21d8a14a1d4f15b69f9f74defb8b75633f880d"),
                File("added_tokens.json", 605L, "58b54bbe36fc752f79a24a271ef66a0a0830054b4dfad94bde757d851968060b"),
                File("configuration.json", 53L, "b8f2ec9ed20fdc00fc43ad60dc2261c05fea848236698fe232bb54e1cf9bdec7"),
                File("files.sha256.json", 1669L, "529183e90db4ce900b55e3e758da273cc230dbc20a75ed14d3561ddf30ecc3af"),
                File("languages.json", 1296L, "05a16e11e4282173331dbf140624048e581a727ba45c361bed35e3014ad68187"),
                File("merges.txt", 1671853L, "8831e4f1a044471340f7c0a83d7bd71306a5b867e95fd870f74d0c5308a904d5"),
                File("mnn_livekit_multilingual.json", 230L, "0185eed347f54d3cbb80bce2d2c84d90400e0494e3a83f8eef6ba09bb917cafb"),
                File("model.mnn", 791446708L, "b27e20e8ed7ab6afa3f24e2d2407c9ec2c3e8bdace11e706486efb773ae7a543"),
                File("optimization.json", 36834L, "809dd06450704a7fd46ab07e67a3bf6f0f5dd3092526ccf36f9a2c5583e8a3bd"),
                File("source.json", 425L, "c5f1a51e44f954fe95a7eee0c9d310fae93398bf1dc63513ffb706b4b75f87a9"),
                File("tokenizer.json", 11421896L, "9c5ae00e602b8860cbd784ba82a8aa14e8feecec692e7076590d014d7b7fdafa"),
                File("tokenizer_config.json", 4860L, "83df6c86106c7f332995b3c456bc6a424cc46ee348bf4e0ae1b5410c63f4393f"),
                File("vocab.json", 2776833L, "ca10d7e9fb3ed18575dd1e277a2579c16d108e32f27439684afa0e10b1440910"),
            }, null, MNNLiveKitMultilingual.ConfigFileName, new[] { "LiveKit Turn Detector Multilingual MNN", "LiveKit multilingual" }, "LiveKit Model License: use only with LiveKit Agents; standalone Unity/MNN use needs separate permission. "),
            Create("Easy-Turn-MNN", "63a2cf65222e5717e25cf56ac7f924f61608b161", false, MNNEasyTurnProfile.IsComplete, new[]
            {
                File("LICENSE", 11357L, "c71d239df91726fc519c6eb72d318ec65820627232b2f796219e87dcf35d0ab4"),
                File("README.md", 1334L, "26403dcd6c729860f81f3ad6f0332d87dae6847678b58473ad33bf12a3e7f136"),
                File("UPSTREAM_MODEL_CARD.md", 8692L, "f33eff0f44e3be915c22d6645b4f7dd220a11cabf1d16ef242e90f96700e6f02"),
                File("added_tokens.json", 605L, "58b54bbe36fc752f79a24a271ef66a0a0830054b4dfad94bde757d851968060b"),
                File("audio.mnn", 400284224L, "e6adb74b425ae04cdd0dbc3ebd5998c3bd0547cb72f4fbe6c382a32b7bdc43a0"),
                File("configuration.json", 53L, "b8f2ec9ed20fdc00fc43ad60dc2261c05fea848236698fe232bb54e1cf9bdec7"),
                File("decoder.mnn", 527612332L, "3a0948e977852cc068db8b2a3eaebc2ca8f846da03b3de2d7b77a817f2718afb"),
                File("embeddings_bf16.bin", 272269312L, "a1a8ee5f138a46066f96d75e61e0ed693970f359fcf839ca10db3cb869ebd529"),
                File("files.sha256.json", 2065L, "d5331959869ad3f210cd6ee12666f24a030ea124dcc3bb8694e2062395b67444"),
                File("merges.txt", 1671853L, "8831e4f1a044471340f7c0a83d7bd71306a5b867e95fd870f74d0c5308a904d5"),
                File("mnn_easy_turn.json", 335L, "93a41818532a1182ce13a01cbb0d43528960579d69e20c3caaf6e2c71adf9783"),
                File("optimization.json", 63184L, "c849bd2b47f0fef8255d3f18c159e35e7c89b8ddb6669a0de203e6b6ed1b8227"),
                File("source.json", 597L, "b7a4524cde6748afec8a7442b8c4f788df8612f08f3d35105239b70050218b55"),
                File("speech-boundary.f32", 7168L, "b55a157f150c5e20c13094c38781bbd2aa3edef2ee9f6e8bfa9091d37b8c573f"),
                File("tokenizer.json", 11421896L, "9c5ae00e602b8860cbd784ba82a8aa14e8feecec692e7076590d014d7b7fdafa"),
                File("tokenizer_config.json", 4860L, "83df6c86106c7f332995b3c456bc6a424cc46ee348bf4e0ae1b5410c63f4393f"),
                File("vocab.json", 2776833L, "ca10d7e9fb3ed18575dd1e277a2579c16d108e32f27439684afa0e10b1440910"),
            }, null, MNNEasyTurn.ConfigFileName, new[] { "Easy Turn MNN", "Easy Turn" }, ""),
            Create("LiveKit-Turn-Detector-MNN", "0cdde333e7e8ca0557c6c0cf1a44b3cbb10d87a5", true, MNNLiveKitTurnDetectorModel.IsComplete, new[]
            {
                File("LICENSE", 5625L, "4f5b74db4fd26299a166688de8a3bde046fd7d9cc13ecab64532c24310bc18d7"),
                File("README.md", 898L, "8d615bdc7db7cff0282a7670e8a5093924d90072e8f4e60543a6ede1cc5cbacb"),
                File("UPSTREAM_MODEL_CARD.md", 5174L, "0336368f8274a5ed09f54cee8d56a851ecee96026d9f391a6735762c16608a60"),
                File("added_tokens.json", 70L, "831c3eb42bcb8dd3eb44adaa4321fb70c771c8567df644a027f19706989a3015"),
                File("config.json", 790L, "30d28a219b65aedf6026b24cfe9e0f7e445b20de469f9053f218799ddc64406e"),
                File("configuration.json", 53L, "b8f2ec9ed20fdc00fc43ad60dc2261c05fea848236698fe232bb54e1cf9bdec7"),
                File("files.sha256.json", 2479L, "bcf9d6c4fff9d111d783b961e29938e1a0ddee138ba76b6391165b5d18f83e54"),
                File("generation_config.json", 111L, "86c3c1fd4de6d39cc981c15d5d53f4b88cef9d4d2abf8f48a7f7f0149413feec"),
                File("manifest.json", 2578L, "a36fc45d52e5fc2f57d969ed12595a1d743b7587ebe963e34d011c28c2045916"),
                File("merges.txt", 466391L, "0b54e8aa4e53d5383e2e4bc635a56b43f9647f7b13832d5d9ecd8f82dac4f510"),
                File("mnn_livekit_turn_detector.json", 957L, "84064bcc1221c4c5bd0c9702ed608e20d17a7e9cca186d055b268857b0059120"),
                File("mnn_optimization.json", 4584L, "babb6a9cbfc631874a5196c6255fabe3ca561be4aae426f9a80939ae4d58c3d5"),
                File("optimization.json", 53110L, "841842426e42fd17766530ccab34a23a111c719296c821db41deda8b59f1761c"),
                File("source.json", 208L, "9a77ef13450ca72c4a46485f1fb20e38727fee1f0a313a0d14f803d51cb94c80"),
                File("special_tokens_map.json", 1224L, "e9ef8c0c827b098555f83d1d7e7a4759793e1d1f0cbbe008d57ddcfe4ca37cb4"),
                File("tokenizer.json", 3523486L, "6f0dc4b1306b1b17da46b55b696c8f54ba7cb05a1b91dc526fd143f31e882fcb"),
                File("tokenizer_config.json", 4127L, "d396075fb874a5344ce13dbfcdc0e7fb64605c676b47ff8d982e2be468a75e20"),
                File("turn_detector_fp16.mnn", 131425228L, "442bd0a111c38b4270c46e6029a389153298e4936e9ea565c99386e55f842bdb"),
                File("turn_detector_fp32.mnn", 262378316L, "fa268d9001d014acffe709b814d31896ef4e2ea009ce6d4447a31959500b8b36"),
                File("vocab.json", 800662L, "82b84012e3add4d01d12ba14442026e49b8cbbaead1f79ecf3d919784f82dc79"),
            }, null, MNNLiveKitTurnDetectorConfig.FileName, new[] { "LiveKit Turn Detector MNN", "LiveKit English" }, "LiveKit Model License: use only with LiveKit Agents; standalone Unity/MNN use needs separate permission. "),
            Create("DualTurn-Endpointing-MNN", "f5c305b9d77366ae01b8cbb1a6b8e8995c49d888", false, MNNDualTurnEndpointingModel.IsComplete, new[]
            {
                File("LICENSE", 11357L, "c71d239df91726fc519c6eb72d318ec65820627232b2f796219e87dcf35d0ab4"),
                File("README.md", 1155L, "f51e77a786bac6fc5408b66ebff2bf7bfd56a9a81d7a611e1f1dca6411fcad74"),
                File("UPSTREAM_MODEL_CARD.md", 16926L, "ef776deac01e6b91a79ac99faf0f04151e864b37cbd5ff6f2dd2ca9bcf6b21d1"),
                File("configuration.json", 53L, "b8f2ec9ed20fdc00fc43ad60dc2261c05fea848236698fe232bb54e1cf9bdec7"),
                File("files.sha256.json", 1173L, "7da437765d1d52be787f19ed2c34bd388e3faf4c3a225cd1a5a07e8b56253887"),
                File("mnn_dualturn_endpointing.json", 390L, "85117bd7f4f0d76e2b9f5082eb1d0cf3bfa8f40536a7fad2ef1ef6e9abfb8277"),
                File("model_fp32.mnn", 647168L, "d8846e630f53e8a2c9fcc9d71f5dbf8f1a0335f8c6d151d05757992974b3e0e9"),
                File("model_fp32.mnn.weight", 262256300L, "e3889903498299efeab92a65e06750cccb0ba2aabff30149bca60a9fb5e9bd65"),
                File("optimization.json", 3219L, "e12f3500eb2a20e66799f089b147d03848ba974bf4d39e1714b5b8ec7b93f5b0"),
                File("source.json", 427L, "b553c5c7c1807eb1d562852f0a40f8622ab62aa64470c55906e2a7793236fbb9"),
            }, null, MNNDualTurnEndpointingConfig.FileName, new[] { "DualTurn Endpointing MNN", "DualTurn", "Dual Turn" }, ""),
        };

        private static MNNModelRepositoryFile File(string path, long bytes, string hash) =>
            new MNNModelRepositoryFile { Path = path, Size = bytes, Sha256 = hash, Type = "blob" };

        private static MNNModelRegistration Create(string name, string revision, bool transcript,
            Func<string, bool> validate, MNNModelRepositoryFile[] files, string conversationAlias = null, string turnMarker = null, string[] searchNames = null, string supportNote = null)
        {
            string repository = "EitanWang/" + name;
            MNNModelRepositoryFile[] Plan() => files.Select(f => new MNNModelRepositoryFile {
                Path = f.Path, Size = f.Size, Sha256 = f.Sha256, Type = f.Type, Revision = revision, SourceRepository = repository
            }).ToArray();
            return new MNNModelRegistration(MNNModelSource.ModelScope, repository, revision, name,
                () => new MNNRemoteModel {
                    displayName = name, author = "EitanWang", category = MNNModelCatalogCategory.TurnDetection.ToString(),
                    tasks = new[] { "turn-detection" }, tags = new[] { "MNN", "turn-detection", name }.Concat(searchNames ?? Array.Empty<string>()).ToArray(),
                    requiresTranscript = transcript,
                    supportSummary = supportNote + "Pinned complete MNN export with per-file SHA-256 install checks. See model card for license, quantization, measured backends and quality limits."
                }, Plan, directory => Plan().All(f => {
                    string path = Path.Combine(directory, f.Path);
                    return System.IO.File.Exists(path) && new FileInfo(path).Length == f.Size;
                }) && validate(directory), null,
                transcript ? MNNConversationModelAdapter.TextTurn : MNNConversationModelAdapter.AudioTurn, conversationAlias, turnMarker);
        }
    }
}
