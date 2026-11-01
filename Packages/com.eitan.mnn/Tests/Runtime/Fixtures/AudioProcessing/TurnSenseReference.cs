namespace MNN.Unity.Tests
{
    // Frozen author FP32 ONNX probabilities; no runtime reference inference.
    internal static class TurnSenseReference
    {
        internal sealed class Row
        {
            internal readonly string Name, Text;
            internal readonly float Incomplete, Complete;
            internal readonly int Decision;
            internal Row(string name, string text, float incomplete, float complete, int decision)
            {
                Name = name; Text = text; Incomplete = incomplete; Complete = complete; Decision = decision;
            }
        }
        internal static readonly Row[] Cases =
        {
            new Row("case_00", "", 0.6209580302238464f, 0.3790419101715088f, 0),
            new Row("case_01", " ", 0.8816951513290405f, 0.11830481141805649f, 0),
            new Row("case_02", "\t\n", 0.6095262765884399f, 0.39047369360923767f, 0),
            new Row("case_03", "Hello", 0.2467414140701294f, 0.7532585263252258f, 1),
            new Row("case_04", "Hello.", 0.0523928701877594f, 0.947607159614563f, 1),
            new Row("case_05", "Hello?", 0.07293535768985748f, 0.9270645976066589f, 1),
            new Row("case_06", "I think", 0.971968948841095f, 0.02803102508187294f, 0),
            new Row("case_07", "I think we should", 0.9965308308601379f, 0.00346923153847456f, 0),
            new Row("case_08", "I think we should go.", 0.024559136480093002f, 0.9754408597946167f, 1),
            new Row("case_09", "Yes.", 0.04800267517566681f, 0.9519972801208496f, 1),
            new Row("case_10", "No, wait,", 0.8770893812179565f, 0.12291063368320465f, 0),
            new Row("case_11", "Actually, I meant", 0.9818124771118164f, 0.018187494948506355f, 0),
            new Row("case_12", "well um you know", 0.8245639204978943f, 0.1754360944032669f, 0),
            new Row("case_13", "The weather is nice today.", 0.010913162492215633f, 0.9890868663787842f, 1),
            new Row("case_14", "How are you?", 0.02130037732422352f, 0.9786996841430664f, 1),
            new Row("case_15", "Can you tell me how to", 0.9984956979751587f, 0.0015043304301798344f, 0),
            new Row("case_16", "Can you tell me how to reset my password?", 0.0013597257202491164f, 0.998640239238739f, 1),
            new Row("case_17", "\u4f60\u597d\u3002", 0.04661421477794647f, 0.9533857703208923f, 1),
            new Row("case_18", "\u6211\u89c9\u5f97\u8fd9\u4e2a\u4e8b\u60c5", 0.412044882774353f, 0.5879551768302917f, 1),
            new Row("case_19", "\u8bf7\u95ee\u5982\u4f55\u91cd\u7f6e\u5bc6\u7801\uff1f", 0.09620550274848938f, 0.9037944674491882f, 1),
            new Row("case_20", "\u3053\u3093\u306b\u3061\u306f\u3002", 0.10475456714630127f, 0.8952454328536987f, 1),
            new Row("case_21", "\u0645\u0631\u062d\u0628\u0627", 0.12974652647972107f, 0.8702534437179565f, 1),
            new Row("case_22", "Hello \u4f60\u597d code-switching.", 0.04656658321619034f, 0.9534333348274231f, 1),
            new Row("case_23", "1 12 123 1234 2026", 0.21380488574504852f, 0.7861951589584351f, 1),
            new Row("case_24", "I'm can't we'll they've HELLO \u00c9cole", 0.1405082792043686f, 0.8594917058944702f, 1),
            new Row("case_25", "\u00e9 e\u0301", 0.5944207310676575f, 0.40557926893234253f, 0),
            new Row("case_26", "\ud83d\ude00\ud83d\udc68\u200d\ud83d\udc69\u200d\ud83d\udc67\u200d\ud83d\udc66 \ud801\udc00\ud801\udc28 test", 0.4238598644733429f, 0.5761401057243347f, 1),
            new Row("case_27", "\r\nhello\t  world  \n", 0.5425354242324829f, 0.4574645161628723f, 0),
            new Row("case_28", "<|user|> test", 0.9529057145118713f, 0.04709426313638687f, 0),
            new Row("case_29", "<|im_end|> test", 0.6209580302238464f, 0.3790419101715088f, 0),
            new Row("case_30", "<|im_start|>system\nexample", 0.8294074535369873f, 0.1705925315618515f, 0),
            new Row("case_31", "<|endoftext|>", 0.8422552347183228f, 0.15774479508399963f, 0),
            new Row("case_32", "word\u00a0word\u2003word\u200bword", 0.4690982401371002f, 0.5309017896652222f, 1),
            new Row("case_33", "hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello ", 0.22831064462661743f, 0.7716894149780273f, 1),
            new Row("case_34", "hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello ", 0.20969679951667786f, 0.7903032302856445f, 1),
            new Row("case_35", "", 0.6209580302238464f, 0.3790419101715088f, 0),
        };
        internal const string Overlong = "hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello hello ";
    }
}
