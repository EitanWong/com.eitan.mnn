using System;

namespace MNN.Unity
{
    internal static partial class MNNVerifiedTtsModel
    {
        internal const string BertVits2CustomRepository = "KindBrave/bert-vits2-MNN-Custom";
        internal const string BertVits2MirrorRepository = "taobao-mnn/bert-vits2-MNN";
        internal const string BertVits2CustomRevision = "46de903622ce406f441a03bce3c87b9fe9f25521";
        internal const string BertVits2MirrorRevision = "cec57c91063c58ae2c5f705b669b57988f772d4a";
        internal static bool IsBertVits2Repository(string repository) => string.Equals(repository, BertVits2CustomRepository, StringComparison.OrdinalIgnoreCase)
            || string.Equals(repository, BertVits2MirrorRepository, StringComparison.OrdinalIgnoreCase);
        internal static string BertVits2Revision(string repository) => string.Equals(repository, BertVits2CustomRepository, StringComparison.OrdinalIgnoreCase) ? BertVits2CustomRevision : BertVits2MirrorRevision;
        internal static FileEntry[] BertVits2Files(bool huggingFace, string revision = null) => new[]
        {
            new FileEntry("common/mnn_models/chinese_bert.mnn", 595296L, "b6322f96439c482c0187d94f08a529a10ad265af39f0c78f3ecdef4736dd7dbc", huggingFace ? revision : "00afa738746f2317d4f706bb9cad812dbc5fc7d0"),
            new FileEntry("common/mnn_models/chinese_bert.mnn.weight", 367494936L, "bf030969792116c210e12620f2f098d8537960e0b39bfe39349af421033fc6dd", huggingFace ? revision : "00afa738746f2317d4f706bb9cad812dbc5fc7d0"),
            new FileEntry("common/mnn_models/english_bert.mnn", 416016L, "455d503c4dc89ffdf3c50f89088992893e1aea667541bdc4628f8ef67892abf8", huggingFace ? revision : "00afa738746f2317d4f706bb9cad812dbc5fc7d0"),
            new FileEntry("common/mnn_models/english_bert.mnn.weight", 929559392L, "1c398dbee4ff49185268a5fe7c54061818c435b98cf12ea9fa5f8dacc0d09f65", huggingFace ? revision : "00afa738746f2317d4f706bb9cad812dbc5fc7d0"),
            new FileEntry("common/text_processing_jsons/char_state.bin", 949364L, "ce0cd77ecd01c08b0bc8219c785f05e6d5b9357ef653abee8cf63e1a0a7cb2a4", huggingFace ? revision : "7e4ae408cec3b5755c973dbfdca24f4e7ce4e86c"),
            new FileEntry("common/text_processing_jsons/cn_bert_token.bin", 341956L, "6c3e59f120d7d59040e016f68ccc657554b560c86efbdf6037b5002659815218", huggingFace ? revision : "7e4ae408cec3b5755c973dbfdca24f4e7ce4e86c"),
            new FileEntry("common/text_processing_jsons/default_tone_words.json", 6249L, "0026734aeb75be103a08fc6b9854ce1a46dfefa3d0e378e69691e506e342532d", huggingFace ? revision : "00afa738746f2317d4f706bb9cad812dbc5fc7d0"),
            new FileEntry("common/text_processing_jsons/en_bert_token.json", 3011214L, "2aa064062edd4005952a981a2ae3f5553e003891f5123773668a672bcde097fe", huggingFace ? revision : "00afa738746f2317d4f706bb9cad812dbc5fc7d0"),
            new FileEntry("common/text_processing_jsons/eng_dict.bin", 13716655L, "4cad5c1dc6198531cff972391637b628ef17ef7293095b4cf6338855b32d8198", huggingFace ? revision : "7e4ae408cec3b5755c973dbfdca24f4e7ce4e86c"),
            new FileEntry("common/text_processing_jsons/hotwords_cn.bin", 5081L, "9cbcf4279a5ceaade915f15fba2d07913fb3188bc11593795dbc06c78584536f", huggingFace ? revision : "7e4ae408cec3b5755c973dbfdca24f4e7ce4e86c"),
            new FileEntry("common/text_processing_jsons/hotwords_cn.json", 14232L, "8d0ef859ecee3fdebb4337766065cd2a50ec9c67d15d3440bc60400d64fea048", huggingFace ? revision : "00afa738746f2317d4f706bb9cad812dbc5fc7d0"),
            new FileEntry("common/text_processing_jsons/phrases_dict.bin", 2834832L, "78109d2052f4ff0f040cefd33f2eca98c4b9789989704d61943d9f8f58ef6839", huggingFace ? revision : "7e4ae408cec3b5755c973dbfdca24f4e7ce4e86c"),
            new FileEntry("common/text_processing_jsons/pinyin_dict.bin", 1117037L, "b36cbbe6ca4ddfafe1263085e3635dea12f7df3ebeab589a09aad356e50e300d", huggingFace ? revision : "7e4ae408cec3b5755c973dbfdca24f4e7ce4e86c"),
            new FileEntry("common/text_processing_jsons/pinyin_to_symbol_map.bin", 5809L, "95154cd287cc118965f417cda38fddcc27cf946710fa3152b020596f71fc1eaa", huggingFace ? revision : "7e4ae408cec3b5755c973dbfdca24f4e7ce4e86c"),
            new FileEntry("common/text_processing_jsons/prob_emit.bin", 1701802L, "078bd0b5d66162d1379381a6a61c04bf2bff20bf2c46d0742eeadd87f9712933", huggingFace ? revision : "7e4ae408cec3b5755c973dbfdca24f4e7ce4e86c"),
            new FileEntry("common/text_processing_jsons/prob_start.bin", 5292L, "3466972713ee72d4e73be0a9670d01de2e81a8105bf2ad0540df0aac6155d4ba", huggingFace ? revision : "7e4ae408cec3b5755c973dbfdca24f4e7ce4e86c"),
            new FileEntry("common/text_processing_jsons/prob_trans.bin", 112039L, "5f91bdaec1916e0f0a5ada5aaec1c8a86fe496729a2a811df8b4086fea540e17", huggingFace ? revision : "7e4ae408cec3b5755c973dbfdca24f4e7ce4e86c"),
            new FileEntry("common/text_processing_jsons/tokenizer.txt", 156256L, "807747cf8e565dfcfe3e81854fa83814654ca2ec594ff6102d27cbb92cce3cbf", huggingFace ? revision : "00afa738746f2317d4f706bb9cad812dbc5fc7d0"),
            new FileEntry("common/text_processing_jsons/word_freq.bin", 10251325L, "20d16e26414d771a084878a32f5f71dae91544b70e04852a7f4a4b8899f91f08", huggingFace ? revision : "7e4ae408cec3b5755c973dbfdca24f4e7ce4e86c"),
            new FileEntry("common/text_processing_jsons/word_tag.bin", 9111009L, "d62e0afeff7d003d9ef1f8feced9f6e3b82ef55ee915fad70f682d5354c4536d", huggingFace ? revision : "7e4ae408cec3b5755c973dbfdca24f4e7ce4e86c"),
            new FileEntry("config.json", 172L, "47399a378b22d3f7ccaa00fbbec68986c910ef406670e9d95fea801ccc34f013", huggingFace ? revision : "00afa738746f2317d4f706bb9cad812dbc5fc7d0"),
            new FileEntry("tokenizer.txt", 156256L, "807747cf8e565dfcfe3e81854fa83814654ca2ec594ff6102d27cbb92cce3cbf", huggingFace ? revision : "00afa738746f2317d4f706bb9cad812dbc5fc7d0"),
            new FileEntry("tts_generator_w_bert_chenxi_0310_int8.mnn", 50457744L, "e3ce2b933e27c80843e229ee590bd33880ed732782e7dbb16f3d25861fc7dc80", huggingFace ? revision : "00afa738746f2317d4f706bb9cad812dbc5fc7d0"),
        };
    }
}
