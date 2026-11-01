using System;

namespace MNN.Unity
{
    internal static partial class MNNVerifiedTtsModel
    {
        internal const string Qwen3ExplicitRepository = "yunfengwang/Qwen3-TTS-12Hz-0.6B-Base-MNN";
        internal const string Qwen3ExplicitRevision = "e17666b5a41fb6208378943869d3208754e0b2b9";
        internal static bool IsQwen3ExplicitRepository(string repository) => string.Equals(repository, Qwen3ExplicitRepository, StringComparison.OrdinalIgnoreCase);
        internal static FileEntry[] Qwen3ExplicitFiles() => new[]
        {
                new FileEntry("README.md", 1754L, "481d6d98e7990fec68ac3c9464fd8ff664bd325258e083ff848405f79221923a", Qwen3ExplicitRevision),
                new FileEntry("cp_step.mnn", 157717740L, "cb9514b505cbace6332a8e26e3d7e7cf749e1bbf1f85837982661b19d5aa2bcd", Qwen3ExplicitRevision),
                new FileEntry("talker_step.mnn", 883179040L, "ef9b23e971796d1d98cc450ff17fae031af5fa2972043319a49ccfe646097e8c", Qwen3ExplicitRevision),
                new FileEntry("tokenizer/merges.txt", 1671839L, "599bab54075088774b1733fde865d5bd747cbcc7a547c5bc12610e874e26f5e3", Qwen3ExplicitRevision),
                new FileEntry("tokenizer/vocab.json", 2776833L, "ca10d7e9fb3ed18575dd1e277a2579c16d108e32f27439684afa0e10b1440910", Qwen3ExplicitRevision),
                new FileEntry("vocoder.mnn", 228597880L, "e87e1043d7ca8fa140b343275a2e144ec9172e7fcb29ac4d890f9f36922c93e1", Qwen3ExplicitRevision),
                new FileEntry("voices/demo/meta.json", 757L, "b540ef3247ff2558a52445c11783e7ba5c3bb312d72000d79f32f55400796211", Qwen3ExplicitRevision),
                new FileEntry("voices/demo/ref_code.i32.bin", 8320L, "8e54757e75357595f57bb5a35a9fa32a14a6c28e10493a23f8b97c5f1b4e74ec", Qwen3ExplicitRevision),
                new FileEntry("voices/demo/spk_emb.f32.bin", 4096L, "2e84a39309de7ba3ceb4f80b96a96cb11bd2b8b0fa873ee493900227a5bc18ed", Qwen3ExplicitRevision),
                new FileEntry("weights/codec_embedding.bin", 6291456L, "bb56aeb4cefe0fc165c44591aad5ef56507bb5778b9231e2acad30bbfb31124c", Qwen3ExplicitRevision),
                new FileEntry("weights/codec_head.bin", 6291456L, "8fb88e0b0ec05040e68ae7382cd3258c2f8653278e3c64a1eae52722dd1adf17", Qwen3ExplicitRevision),
                new FileEntry("weights/cp_embed_0.bin", 4194304L, "8e59d9a48b01d9dbd0d546e29f27919c41d4f212f96e19e9e11bae76d861b51e", Qwen3ExplicitRevision),
                new FileEntry("weights/cp_embed_1.bin", 4194304L, "9943b783d147fae119e51cd7c8342e49904421e97ab05228da7b06dc2a4a4ccc", Qwen3ExplicitRevision),
                new FileEntry("weights/cp_embed_10.bin", 4194304L, "c6503f4e413f28cc930bd42826d4ab1bb16f304e0ebc7e3e39009ac30c1b6230", Qwen3ExplicitRevision),
                new FileEntry("weights/cp_embed_11.bin", 4194304L, "ccd5d6e336bcdf79a9c728a73ca203804ad8e773c905c55584c2d5e1c96a676c", Qwen3ExplicitRevision),
                new FileEntry("weights/cp_embed_12.bin", 4194304L, "6d386f2ac3a001b28fbfa2013fbb0c8607ffc22ce13364f4cf4b72b534241b13", Qwen3ExplicitRevision),
                new FileEntry("weights/cp_embed_13.bin", 4194304L, "13a4977f824dd6e9c233506fef980418ba0eb449e1e446b380ca375b703262fc", Qwen3ExplicitRevision),
                new FileEntry("weights/cp_embed_14.bin", 4194304L, "69db8522ae6416f8a8e2dfceae81101b8e5e8b47a7a438e636b46cda5e4fb663", Qwen3ExplicitRevision),
                new FileEntry("weights/cp_embed_2.bin", 4194304L, "e17b0bb0ca664c572fe919c62b1b8b6e5064e8990b3abfd49bea2b42df62824d", Qwen3ExplicitRevision),
                new FileEntry("weights/cp_embed_3.bin", 4194304L, "f2755334d87f80f64830ba626c6dcf48ebf03efec3848100cceadd008e5f9a1f", Qwen3ExplicitRevision),
                new FileEntry("weights/cp_embed_4.bin", 4194304L, "06dc0778d60cdea65b222c31bb05b914833841f4f13a65199982911cfb24ac8b", Qwen3ExplicitRevision),
                new FileEntry("weights/cp_embed_5.bin", 4194304L, "4bcdeda6bba2bc42e28fd769a3d66b5c6b5c57f89116f869ffa1378c8766aa90", Qwen3ExplicitRevision),
                new FileEntry("weights/cp_embed_6.bin", 4194304L, "89b8c6b40a042fe9a1431c097c84ce000efcc64f2c91040ff6b67f61dec8bb9f", Qwen3ExplicitRevision),
                new FileEntry("weights/cp_embed_7.bin", 4194304L, "f4308c502e4fed1ea2ae529f008d87f3f967663c27d7666c27c6e728d16bc6a1", Qwen3ExplicitRevision),
                new FileEntry("weights/cp_embed_8.bin", 4194304L, "7bc490c884e32b3268c194ddd05b908461cd904dca7705a67d0bb55fac738aae", Qwen3ExplicitRevision),
                new FileEntry("weights/cp_embed_9.bin", 4194304L, "6079efcc960cdec50ce832d6c24e63cfa31e5c1a5131043fa93fe4ff140908c5", Qwen3ExplicitRevision),
                new FileEntry("weights/cp_head_0.bin", 4194304L, "31d7cad25b51690ed905713f4598fa26c7101ca946adece515d4517fd41ebbfc", Qwen3ExplicitRevision),
                new FileEntry("weights/cp_head_1.bin", 4194304L, "920b0eeedf34320ff2998cc90fba6749f627c51627f2567c5b2eedce51e4f2e2", Qwen3ExplicitRevision),
                new FileEntry("weights/cp_head_10.bin", 4194304L, "8dab9abb58473b2dec2bfe0cbc06945d9b5fd7608f98b6d2a8785b3165d88605", Qwen3ExplicitRevision),
                new FileEntry("weights/cp_head_11.bin", 4194304L, "37ea1a2e282150e197eb688a14d131af95ebbb47d1cb1371310ea247ae34de79", Qwen3ExplicitRevision),
                new FileEntry("weights/cp_head_12.bin", 4194304L, "f7993627f2a01b0c3f4cc8060fa1473d51c6eb3dfd53aa8b799727bc0ae3c27e", Qwen3ExplicitRevision),
                new FileEntry("weights/cp_head_13.bin", 4194304L, "ff1c425097f37ecedf8458e6e44d2a05406d5e1e88be2acd43d4750d816ce7c5", Qwen3ExplicitRevision),
                new FileEntry("weights/cp_head_14.bin", 4194304L, "f0a8832f0a3942ffd67301008a87650a6f0c46f4ed4af5b7d58102e4ef1ce5f9", Qwen3ExplicitRevision),
                new FileEntry("weights/cp_head_2.bin", 4194304L, "df769a302855197aaef8e54106b64a7270177e5562c19c1e9b12f3ab3e5aa54f", Qwen3ExplicitRevision),
                new FileEntry("weights/cp_head_3.bin", 4194304L, "dd1c390db4e7e9c3f045cc79bc5d8e905503c91f2e486a3d4d7192ce440873f4", Qwen3ExplicitRevision),
                new FileEntry("weights/cp_head_4.bin", 4194304L, "3468149a542197447b15adf35afd11f75f4948ba7a5525574580f33d3c8da896", Qwen3ExplicitRevision),
                new FileEntry("weights/cp_head_5.bin", 4194304L, "d445cecf34e1db67f90bdc5507530dc4c74bb1f3416a7e312dbce8d07b5ac3a7", Qwen3ExplicitRevision),
                new FileEntry("weights/cp_head_6.bin", 4194304L, "3280a46c5a58ce05e0c0e8d229b4e2e2a3517fc0c78f3db219efcff1721ab414", Qwen3ExplicitRevision),
                new FileEntry("weights/cp_head_7.bin", 4194304L, "9529397a51d5b87b07ff3928b38bf3677d646129f3e54a4760513a2543e1902a", Qwen3ExplicitRevision),
                new FileEntry("weights/cp_head_8.bin", 4194304L, "783bd3f8e792d9e25939c702288d56a2090aaf72c9b4a4a8934214fec108fd73", Qwen3ExplicitRevision),
                new FileEntry("weights/cp_head_9.bin", 4194304L, "173ba2067d3263f90288d526ee88b7c1a55ff1f695a0341a8f54c8babbfc6fdb", Qwen3ExplicitRevision),
                new FileEntry("weights/meta.json", 3484L, "b91babc40738ce36a3348758fbc582c0744dad5a697fd78e56dfbf827bebf5ac", Qwen3ExplicitRevision),
                new FileEntry("weights/text_embedding.bin", 622329856L, "8a1333d217f51b02ec8e1fec5409905a91e6e02208d150d7b220625f7ef0efbe", Qwen3ExplicitRevision),
                new FileEntry("weights/text_proj_fc1_b.bin", 4096L, "44d8ffa2c7bf848f066251174b49aa6dead41c71043207ab3dd33c598ca28329", Qwen3ExplicitRevision),
                new FileEntry("weights/text_proj_fc1_w.bin", 8388608L, "a8259e2770a26fc78cc56963bfa77082d13aea6f974c5cb2cac11d8a8fc12b2a", Qwen3ExplicitRevision),
                new FileEntry("weights/text_proj_fc2_b.bin", 2048L, "0f0fc1e02fab0569f395a9d27f150b3b8cd7f386c54b709ef86b43650825a134", Qwen3ExplicitRevision),
                new FileEntry("weights/text_proj_fc2_w.bin", 4194304L, "9b79f0fb4aa7cbbc1ea8eeec330cd26b4ad9bc480593be0af135e7f6f1c0dafa", Qwen3ExplicitRevision),
        };
    }
}
