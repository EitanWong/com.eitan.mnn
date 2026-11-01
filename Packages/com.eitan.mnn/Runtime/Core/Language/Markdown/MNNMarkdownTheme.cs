using System;

namespace MNN.Unity
{
    /// <summary>The surface theme used for generated rich-text colors. Body text inherits its control's color.</summary>
    public enum MNNMarkdownTheme
    {
        Dark,
        Light
    }

    // Immutable per-render state: Editor and Player consumers may render different themes concurrently.
    internal sealed class MNNMarkdownPalette
    {
        private static readonly MNNMarkdownPalette Dark = new MNNMarkdownPalette("#8AB4FF", "#C9D4EA", "#A8B5D0", "#D4B0FF", "#A8D5A2", "#F0C38E");
        private static readonly MNNMarkdownPalette Light = new MNNMarkdownPalette("#154B8B", "#24344D", "#3D4E65", "#6938A0", "#23613C", "#7A4016");
        internal readonly string Link, Code, Muted;
        internal readonly string SyntaxKeyword, SyntaxString, SyntaxNumber;

        private MNNMarkdownPalette(string link, string code, string muted, string syntaxKeyword, string syntaxString, string syntaxNumber)
        {
            Link = link;
            Code = code;
            Muted = muted;
            SyntaxKeyword = syntaxKeyword;
            SyntaxString = syntaxString;
            SyntaxNumber = syntaxNumber;
        }

        internal static MNNMarkdownPalette For(MNNMarkdownTheme theme)
        {
            switch (theme)
            {
                case MNNMarkdownTheme.Dark: return Dark;
                case MNNMarkdownTheme.Light: return Light;
                default: throw new ArgumentOutOfRangeException(nameof(theme));
            }
        }
    }
}
