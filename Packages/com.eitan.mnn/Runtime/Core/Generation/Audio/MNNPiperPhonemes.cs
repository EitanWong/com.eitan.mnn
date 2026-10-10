using System.Linq;
using System.Text;

namespace MNN.Unity
{
    internal static class MNNPiperPhonemes
    {
        private static readonly string[] Symbols = ("_ ^ $ space ! ' ( ) , - . : ; ? " + "a b c d e f h i j k l m n o p q r s t u v w x y z " + "æ ç ð ø ħ ŋ œ ǀ ǁ ǂ ǃ ɐ ɑ ɒ ɓ ɔ ɕ ɖ ɗ ɘ ə ɚ ɛ ɜ ɞ ɟ ɠ ɡ ɢ ɣ ɤ ɥ ɦ ɧ ɨ ɪ ɫ ɬ ɭ ɮ ɯ ɰ ɱ ɲ ɳ ɴ ɵ ɶ ɸ ɹ ɺ ɻ ɽ ɾ ʀ ʁ ʂ ʃ ʄ ʈ ʉ ʊ ʋ ʌ ʍ ʎ ʏ ʐ ʑ ʒ ʔ ʕ ʘ ʙ ʛ ʜ ʝ ʟ ʡ ʢ ʲ ˈ ˌ ː ˑ ˞ β θ χ ᵻ ⱱ " + "0 1 2 3 4 5 6 7 8 9 ̧ ̃ ̪ ̯ ̩ ʰ ˤ ε ↓ # \" ↑ ̺ ̻ g ʦ X ̝ ̊").Split(' ');
        private static readonly System.Collections.Generic.Dictionary<int, int> Lookup = Symbols.Select((symbol, index) => new
        {
        symbol, index
        }).Where(item => item.symbol != "space").GroupBy(item => char.ConvertToUtf32(item.symbol, 0)).ToDictionary(group => group.Key, group => group.First().index);
        internal static int[] Encode(string ipa)
        {
            var ids = new System.Collections.Generic.List<int>{1, 0};
            string normalized = ipa.Normalize(NormalizationForm.FormD);
            for (int i = 0; i < normalized.Length; ++i)
            {
                int codepoint = char.ConvertToUtf32(normalized, i);
                if (codepoint > 0xffff)
                    ++i;
                if (codepoint == ' ')
                {
                    ids.Add(3);
                    ids.Add(0);
                    continue;
                }

                if (!Lookup.TryGetValue(codepoint, out int id))
                    continue;
                ids.Add(id);
                ids.Add(0);
            }

            ids.Add(2);
            return ids.ToArray();
        }
    }
}
