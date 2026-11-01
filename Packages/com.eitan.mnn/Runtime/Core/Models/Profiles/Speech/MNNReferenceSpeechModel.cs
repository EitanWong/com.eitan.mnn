using System;
using System.IO;
using System.Linq;

namespace MNN.Unity
{
    // Pinned manifests prevent accidental downloads of duplicate precision sets
    // and keep auxiliary tokenizer assets bound to the graph release.
    internal static class MNNReferenceSpeechModel
    {
        internal const string IndexRepository = "yunfengwang/IndexTTS-2.5-mnn";
        internal const string FireRedRepository = "yunfengwang/fireredtts3-mnn";
        internal const string IndexRevision = "23050b0a363accac3e4cba138f15fa6147723a4e";
        internal const string FireRedRevision = "7a39043ca403fc63edb52fd037cd498ab2587f44";
        internal sealed class File
        {
            internal readonly string Repository, Revision, Path, Sha256;
            internal readonly long Bytes;
            internal File(string repository, string revision, string path, long bytes, string sha256)
            { Repository = repository; Revision = revision; Path = path; Bytes = bytes; Sha256 = sha256; }
        }
        internal static readonly File[] IndexFiles = {
            new File("yunfengwang/IndexTTS-2.5-mnn", "23050b0a363accac3e4cba138f15fa6147723a4e", "bigvgan_fp16.mnn", 225038336L, "2bfc2ddbe61007a6784fc096717f24eef7dc27e0cc5322af4a657cc8b09ed4b8"),
            new File("yunfengwang/IndexTTS-2.5-mnn", "23050b0a363accac3e4cba138f15fa6147723a4e", "campplus_fp16.mnn", 14266116L, "2cc9c8d09fb555c95f45118fdea7dd3dcca1247e4897dea7d6567fe23776ed28"),
            new File("yunfengwang/IndexTTS-2.5-mnn", "23050b0a363accac3e4cba138f15fa6147723a4e", "cfm_estimator_fp16.mnn", 191118016L, "8ee76e76f77606f4636ae4957644aaffc51537d9466ae987fb71921c46483260"),
            new File("yunfengwang/IndexTTS-2.5-mnn", "23050b0a363accac3e4cba138f15fa6147723a4e", "emo_vec_fp16.mnn", 326616352L, "88d8840d1e4572f1edd48de778678c6483d8af193b8a283a8923ee4194aa172e"),
            new File("yunfengwang/IndexTTS-2.5-mnn", "23050b0a363accac3e4cba138f15fa6147723a4e", "gpt_prefill_fp16.mnn", 157521768L, "0b23c7a568963ad9d46add8871a56a3afa5ff740c7444130f16cedba376bcfd7"),
            new File("yunfengwang/IndexTTS-2.5-mnn", "23050b0a363accac3e4cba138f15fa6147723a4e", "gpt_prefill_fp16.mnn.weight", 966345736L, "5892c7599bace448e1e878d725d6670802a7609040ba12505aa386459e8e4dd3"),
            new File("yunfengwang/IndexTTS-2.5-mnn", "23050b0a363accac3e4cba138f15fa6147723a4e", "gpt_step_fp16.mnn", 992778308L, "13ca395e627674f1ecbee351ef8d7ecea9792abbf1d0decee19880d27eeb8418"),
            new File("yunfengwang/IndexTTS-2.5-mnn", "23050b0a363accac3e4cba138f15fa6147723a4e", "hf_cache/w2v-bert-2.0/config.json", 1874L, "f5572bd5998b68182e9c328a43127ed21fed687f6910497136b91a4e3b0e3675"),
            new File("yunfengwang/IndexTTS-2.5-mnn", "23050b0a363accac3e4cba138f15fa6147723a4e", "hf_cache/w2v-bert-2.0/preprocessor_config.json", 275L, "8e6281aad64f97e40534135a59dcc5d33571efae376f2a25adf5551951897ab4"),
            new File("yunfengwang/IndexTTS-2.5-mnn", "23050b0a363accac3e4cba138f15fa6147723a4e", "length_regulator_fp16.mnn", 7906496L, "2cdfa35b6388dc3a7cf45980d7b7259d53f01cc42db075a4af663e2b7ceec326"),
            new File("yunfengwang/IndexTTS-2.5-mnn", "23050b0a363accac3e4cba138f15fa6147723a4e", "multilingual_zh_ja_yue_char_del.tiktoken", 907395L, "747979631e813193436aabcff7c1c235d37de8097b71c563ec8b63b7a515c718"),
            new File("yunfengwang/IndexTTS-2.5-mnn", "23050b0a363accac3e4cba138f15fa6147723a4e", "semantic_codec_decode_fp16.mnn", 50811044L, "ccb091cc022cc227ff841d5a6e6ac45b83c27334bfb8fdece6191a9d26f10d7e"),
            new File("yunfengwang/IndexTTS-2.5-mnn", "23050b0a363accac3e4cba138f15fa6147723a4e", "semantic_model_fp16.mnn", 824114148L, "fa75ddc537d3bcd0bc2ddac59c39cd70143f53ff729571fc7b3b651eb33862d0"),
            new File("yunfengwang/IndexTTS-2.5-mnn", "23050b0a363accac3e4cba138f15fa6147723a4e", "spk_proj.npz", 988666L, "9fe0a868da699d7eef33bd461b4fbaade8abd6c596d4abf9b8cf77fbf11ca932"),
        };
        internal static readonly File[] FireRedFiles = {
            new File("yunfengwang/fireredtts3-mnn", "7a39043ca403fc63edb52fd037cd498ab2587f44", "campp.mnn", 15114132L, "a5d123932f6d70bcd332b05fd97851acad8159c2b63e88c845e12b9a7b305f28"),
            new File("yunfengwang/fireredtts3-mnn", "7a39043ca403fc63edb52fd037cd498ab2587f44", "dit.mnn", 588833124L, "3c4c9e6219fc36281e307ced5fed1708a3596b9e7d24f848835e92684e7033cf"),
            new File("yunfengwang/fireredtts3-mnn", "7a39043ca403fc63edb52fd037cd498ab2587f44", "dit_head.mnn", 4200672L, "061665d25f01b4c4a1b83c3e77b029955d9805b583bae94961430bf5a0bd7474"),
            new File("yunfengwang/fireredtts3-mnn", "7a39043ca403fc63edb52fd037cd498ab2587f44", "embed.mnn", 622330860L, "61250051affc7a1c2d2c03e700d167312dbba63759914f179de4528b6f6299c3"),
            new File("yunfengwang/fireredtts3-mnn", "7a39043ca403fc63edb52fd037cd498ab2587f44", "norm.mnn", 16860L, "d7949eb45db65e6428aa149961e63c1b4d5b4be3d3428010a7ee2acaf2fc5d3f"),
            new File("yunfengwang/fireredtts3-mnn", "7a39043ca403fc63edb52fd037cd498ab2587f44", "patch_encoder.mnn", 206562972L, "5252072b8cb833e019302ae744f9990831a896bc6645e73c93b88a6020829b9d"),
            new File("yunfengwang/fireredtts3-mnn", "7a39043ca403fc63edb52fd037cd498ab2587f44", "prefill_seg1.mnn", 354404596L, "aa218402329740183cc9619bfa06bac4e6a3297c99602558ea0effc08f9fe641"),
            new File("yunfengwang/fireredtts3-mnn", "7a39043ca403fc63edb52fd037cd498ab2587f44", "prefill_seg2.mnn", 354404596L, "622849e11c380bc467061eed10f2db584ad3687f16aac39b2c08abb8e2d421ae"),
            new File("yunfengwang/fireredtts3-mnn", "7a39043ca403fc63edb52fd037cd498ab2587f44", "prefill_seg3.mnn", 354404596L, "e733d77b08e60f95db8e19ccb67e0b5b5fc474f6488d88eaadeaeef2a7e83377"),
            new File("yunfengwang/fireredtts3-mnn", "7a39043ca403fc63edb52fd037cd498ab2587f44", "prefill_seg4.mnn", 354404596L, "2cc56acf8db346e9f2e1a1fcea59714baf27095ac00f9274f5497a968738b8ae"),
            new File("yunfengwang/fireredtts3-mnn", "7a39043ca403fc63edb52fd037cd498ab2587f44", "redae_decoder.mnn", 484459376L, "f282238d114ab8b94a042c57984fd76bf476a62ee8d24cfaf75ecb576b584677"),
            new File("yunfengwang/fireredtts3-mnn", "7a39043ca403fc63edb52fd037cd498ab2587f44", "redae_encoder.mnn", 590217100L, "a4a43686e7aaae73eeb526e918e47211e7dc5572a57f7d34a24d2061dfc4e53c"),
            new File("yunfengwang/fireredtts3-mnn", "7a39043ca403fc63edb52fd037cd498ab2587f44", "spk_proj_dit.mnn", 528604L, "39093af4c9ad049ab2f2759abe73600e405e91563c9e0c88c00c2ef3c87dd0ec"),
            new File("yunfengwang/fireredtts3-mnn", "7a39043ca403fc63edb52fd037cd498ab2587f44", "spk_proj_llm.mnn", 2107612L, "021ab4dc97c631f291dc86db838c48b5e6a0dfd7d20a1fadd5182a20f1dbad95"),
            new File("yunfengwang/fireredtts3-mnn", "7a39043ca403fc63edb52fd037cd498ab2587f44", "step_seg1.mnn", 354404360L, "1ba7c3a4179e5c61d0ece126092e373b59f443cac0818cb624a16133efe12725"),
            new File("yunfengwang/fireredtts3-mnn", "7a39043ca403fc63edb52fd037cd498ab2587f44", "step_seg2.mnn", 354404360L, "83e2370ebf2611ab9a80d6d7a5f4f21894c57887998a0d9e6d4d2c3ad7420457"),
            new File("yunfengwang/fireredtts3-mnn", "7a39043ca403fc63edb52fd037cd498ab2587f44", "step_seg3.mnn", 354404360L, "22463636c2ee665c9499a6b0b8bb9432083032edd3e1e0011a217a986c460436"),
            new File("yunfengwang/fireredtts3-mnn", "7a39043ca403fc63edb52fd037cd498ab2587f44", "step_seg4.mnn", 354404360L, "1bc6f5622501e2ce9e0501b21c1d54ed48bcd34166c149e9e0d3f784df646eef"),
            new File("yunfengwang/fireredtts3-mnn", "7a39043ca403fc63edb52fd037cd498ab2587f44", "stop_head.mnn", 6272L, "5bf4fc3b128150b51ebf763664cb3c5f2863f2ce399cc58108690a2bcfddbfea"),
            new File("FireRedTeam/FireRedTTS3", "dcf1bdcd1b8b25b382fa84c3e34eb82e3054a610", "text_tokenizer/tokenizer.json", 11422654L, "aeb13307a71acd8fe81861d94ad54ab689df773318809eed3cbe794b4492dae4"),
            new File("FireRedTeam/FireRedTTS3", "dcf1bdcd1b8b25b382fa84c3e34eb82e3054a610", "text_tokenizer/tokenizer_config.json", 9732L, "d5d09f07b48c3086c508b30d1c9114bd1189145b74e982a265350c923acd8101"),
            new File("FireRedTeam/FireRedTTS3", "dcf1bdcd1b8b25b382fa84c3e34eb82e3054a610", "text_tokenizer/vocab.json", 2776833L, "ca10d7e9fb3ed18575dd1e277a2579c16d108e32f27439684afa0e10b1440910"),
        };
        internal static bool IsIndex(string repository) => string.Equals(repository, IndexRepository, StringComparison.OrdinalIgnoreCase);
        internal static bool IsFireRed(string repository) => string.Equals(repository, FireRedRepository, StringComparison.OrdinalIgnoreCase);
        internal static bool IsRepository(string repository) => IsIndex(repository) || IsFireRed(repository);
        internal static File[] Files(bool index) => index ? IndexFiles : FireRedFiles;
        internal static string Revision(string repository) => IsIndex(repository) ? IndexRevision : FireRedRevision;
        internal static string ResolveRevision(string repository, string revision)
        {
            string pinned = Revision(repository);
            if (string.IsNullOrWhiteSpace(revision) || revision == "main" || revision == pinned) return pinned;
            throw new ArgumentException("This speech pipeline requires the tested model revision " + pinned + ".", nameof(revision));
        }
        internal static bool IsComplete(string root, bool index)
        {
            if (string.IsNullOrWhiteSpace(root)) return false;
            try { return Files(index).All(file => { var info = new FileInfo(System.IO.Path.Combine(root, file.Path)); return info.Exists && info.Length == file.Bytes; }); }
            catch (Exception error) when (error is IOException || error is UnauthorizedAccessException || error is ArgumentException) { return false; }
        }
        internal static void Validate(string root, bool index)
        {
            if (!IsComplete(root, index)) throw new InvalidDataException("Incomplete " + (index ? "IndexTTS-2.5 fp16" : "FireRedTTS3") + " model. Download the full profile with Model Manager, including tokenizer dependencies.");
        }
    }
}
