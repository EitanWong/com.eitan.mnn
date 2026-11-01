using System;
using System.Collections.Generic;

namespace MNN.Unity
{
    internal enum MNNMarkdownCodeKind
    {
        CSharp, Cpp, JavaScript, Python, Json, Shell, Sql, Java, Kotlin, Swift, Go, Rust, Dart,
        Lua, Ruby, Php, R, Markup, Css, Yaml, Toml
    }

    [Flags]
    internal enum MNNMarkdownCodeFeatures
    {
        None = 0, SlashComments = 1, HashComments = 2, DashComments = 4, BlockComments = 8,
        NestedComments = 16, TripleQuotes = 32, RawTripleQuotes = 64, Backticks = 128,
        LiteralBackticks = 256, MultilineStrings = 512, DoubleQuoteOnly = 1024,
        EscapedIdentifiers = 2048, HashNeedsSpace = 4096, DoubledQuotes = 8192,
        LiteralSingleQuote = 16384, HexFloat = 32768
    }

    /// <summary>Immutable lexical profiles. Keyword lookup compares source ranges without allocating token strings.</summary>
    internal sealed class MNNMarkdownCodeLanguage
    {
        internal readonly MNNMarkdownCodeKind Kind;
        private readonly MNNMarkdownCodeFeatures _features;
        private readonly Dictionary<int, List<string>> _keywords = new Dictionary<int, List<string>>();
        private static readonly Dictionary<string, MNNMarkdownCodeLanguage> Languages = CreateLanguages();

        private MNNMarkdownCodeLanguage(MNNMarkdownCodeKind kind, string keywords)
        {
            Kind = kind;
            _features = FeaturesFor(kind);
            foreach (string word in keywords.Split(' '))
            {
                if (!_keywords.TryGetValue(word.Length, out var bucket))
                    _keywords.Add(word.Length, bucket = new List<string>());
                bucket.Add(word);
            }
        }

        internal bool IsKeyword(string source, int start, int length)
        {
            if (!_keywords.TryGetValue(length, out var bucket)) return false;
            var comparison = Kind == MNNMarkdownCodeKind.Sql || Kind == MNNMarkdownCodeKind.Php
                ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
            foreach (string word in bucket)
                if (string.Compare(source, start, word, 0, length, comparison) == 0) return true;
            return false;
        }

        internal bool Has(MNNMarkdownCodeFeatures feature) => (_features & feature) != 0;

        private static MNNMarkdownCodeFeatures FeaturesFor(MNNMarkdownCodeKind kind)
        {
            const MNNMarkdownCodeFeatures slash = MNNMarkdownCodeFeatures.SlashComments | MNNMarkdownCodeFeatures.BlockComments;
            const MNNMarkdownCodeFeatures triple = MNNMarkdownCodeFeatures.TripleQuotes;
            switch (kind)
            {
                case MNNMarkdownCodeKind.CSharp: return slash;
                case MNNMarkdownCodeKind.Cpp: return slash | MNNMarkdownCodeFeatures.HexFloat;
                case MNNMarkdownCodeKind.JavaScript: return slash | MNNMarkdownCodeFeatures.Backticks;
                case MNNMarkdownCodeKind.Json: return slash | MNNMarkdownCodeFeatures.DoubleQuoteOnly;
                case MNNMarkdownCodeKind.Python: return MNNMarkdownCodeFeatures.HashComments | triple;
                case MNNMarkdownCodeKind.Shell: return MNNMarkdownCodeFeatures.HashComments | MNNMarkdownCodeFeatures.HashNeedsSpace |
                    MNNMarkdownCodeFeatures.Backticks | MNNMarkdownCodeFeatures.MultilineStrings | MNNMarkdownCodeFeatures.LiteralSingleQuote;
                case MNNMarkdownCodeKind.Sql: return MNNMarkdownCodeFeatures.DashComments | MNNMarkdownCodeFeatures.BlockComments |
                    MNNMarkdownCodeFeatures.MultilineStrings | MNNMarkdownCodeFeatures.DoubledQuotes;
                case MNNMarkdownCodeKind.Java: return slash | triple | MNNMarkdownCodeFeatures.HexFloat;
                case MNNMarkdownCodeKind.Kotlin: return slash | triple | MNNMarkdownCodeFeatures.RawTripleQuotes |
                    MNNMarkdownCodeFeatures.NestedComments | MNNMarkdownCodeFeatures.EscapedIdentifiers;
                case MNNMarkdownCodeKind.Swift: return slash | triple | MNNMarkdownCodeFeatures.NestedComments |
                    MNNMarkdownCodeFeatures.EscapedIdentifiers | MNNMarkdownCodeFeatures.DoubleQuoteOnly | MNNMarkdownCodeFeatures.HexFloat;
                case MNNMarkdownCodeKind.Go: return slash | MNNMarkdownCodeFeatures.Backticks |
                    MNNMarkdownCodeFeatures.LiteralBackticks | MNNMarkdownCodeFeatures.HexFloat;
                case MNNMarkdownCodeKind.Rust: return slash | MNNMarkdownCodeFeatures.NestedComments;
                case MNNMarkdownCodeKind.Dart: return slash | triple | MNNMarkdownCodeFeatures.NestedComments;
                case MNNMarkdownCodeKind.Lua: return MNNMarkdownCodeFeatures.DashComments | MNNMarkdownCodeFeatures.HexFloat;
                case MNNMarkdownCodeKind.Ruby:
                case MNNMarkdownCodeKind.R: return MNNMarkdownCodeFeatures.HashComments | MNNMarkdownCodeFeatures.Backticks |
                    MNNMarkdownCodeFeatures.MultilineStrings;
                case MNNMarkdownCodeKind.Php: return slash | MNNMarkdownCodeFeatures.HashComments |
                    MNNMarkdownCodeFeatures.Backticks | MNNMarkdownCodeFeatures.MultilineStrings;
                case MNNMarkdownCodeKind.Css: return MNNMarkdownCodeFeatures.BlockComments;
                case MNNMarkdownCodeKind.Yaml: return MNNMarkdownCodeFeatures.HashComments | MNNMarkdownCodeFeatures.HashNeedsSpace |
                    MNNMarkdownCodeFeatures.MultilineStrings | MNNMarkdownCodeFeatures.DoubledQuotes;
                case MNNMarkdownCodeKind.Toml: return MNNMarkdownCodeFeatures.HashComments | triple | MNNMarkdownCodeFeatures.LiteralSingleQuote;
                default: return MNNMarkdownCodeFeatures.None;
            }
        }

        internal static MNNMarkdownCodeLanguage Find(string info)
        {
            if (string.IsNullOrWhiteSpace(info)) return null;
            int start = 0;
            while (start < info.Length && char.IsWhiteSpace(info[start])) ++start;
            int end = start;
            while (end < info.Length && !char.IsWhiteSpace(info[end])) ++end;
            string name = start == 0 && end == info.Length ? info : info.Substring(start, end - start);
            return Languages.TryGetValue(name, out var language) ? language : null;
        }

        private static Dictionary<string, MNNMarkdownCodeLanguage> CreateLanguages()
        {
            var result = new Dictionary<string, MNNMarkdownCodeLanguage>(StringComparer.OrdinalIgnoreCase);
            Add(result, "cs csharp c#", MNNMarkdownCodeKind.CSharp,
                "abstract as async await base bool break byte case catch char checked class const continue decimal default " +
                "delegate do double else enum event explicit extern false finally fixed float for foreach goto if implicit in " +
                "int interface internal is lock long namespace new null object operator out override params private protected " +
                "public readonly record ref required return sbyte sealed short sizeof stackalloc static string struct switch " +
                "this throw true try typeof uint ulong unchecked unsafe ushort using var virtual void volatile when where while yield nameof init get set");
            Add(result, "c h cpp c++ cc cxx hpp hxx hlsl glsl", MNNMarkdownCodeKind.Cpp,
                "alignas alignof asm auto bool break case catch char class const constexpr continue default delete do double " +
                "else enum explicit export extern false float for friend if inline int long namespace new noexcept nullptr " +
                "operator private protected public register return short signed sizeof static struct switch template this " +
                "thread_local throw true try typedef typename union unsigned using virtual void volatile while " +
                "float2 float3 float4 float4x4 half half2 half3 half4 vec2 vec3 vec4 mat4 uniform varying sampler2D Texture2D");
            Add(result, "js javascript mjs ts typescript", MNNMarkdownCodeKind.JavaScript,
                "abstract any as async await boolean break case catch class const constructor continue debugger declare " +
                "default delete do else enum export extends false finally for from function get if implements import in " +
                "instanceof interface keyof let module namespace never new null number of private protected public readonly " +
                "return set static string super switch symbol this throw true try type typeof undefined unknown var void while with yield");
            Add(result, "py python python3", MNNMarkdownCodeKind.Python,
                "and as assert async await break case class continue def del elif else except False finally for from global " +
                "if import in is lambda match None nonlocal not or pass raise return True try while with yield");
            Add(result, "json jsonc", MNNMarkdownCodeKind.Json, "true false null");
            Add(result, "sh bash shell zsh", MNNMarkdownCodeKind.Shell,
                "if then else elif fi case esac for while until do done in function select time coproc return exit export local readonly declare typeset true false");
            Add(result, "sql", MNNMarkdownCodeKind.Sql,
                "select from where and or not null true false as join inner outer left right full cross on group by having " +
                "order asc desc limit offset insert into values update set delete create table alter drop index view union " +
                "all distinct exists in is like between case when then else end with recursive over partition begin commit rollback " +
                "return declare function procedure call if loop");
            Add(result, "java", MNNMarkdownCodeKind.Java,
                "abstract assert boolean break byte case catch char class const continue default do double else enum extends final finally " +
                "float for if implements import instanceof int interface long native new package private protected public record return " +
                "sealed short static strictfp super switch synchronized this throw throws transient try var void volatile while true false null yield");
            Add(result, "kotlin kt kts", MNNMarkdownCodeKind.Kotlin,
                "abstract actual annotation as break by catch class companion const constructor continue crossinline data delegate do dynamic " +
                "else enum expect external false field final finally for fun get if import in infix init inline inner interface internal is " +
                "lateinit noinline null object open operator out override package param private property protected public receiver reified " +
                "return sealed set setparam super suspend tailrec this throw true try typealias typeof val var vararg when where while");
            Add(result, "swift", MNNMarkdownCodeKind.Swift,
                "actor any as associatedtype async await borrowing break case catch class consuming continue convenience copy default defer " +
                "deinit didSet distributed do dynamic each else enum extension fallthrough false fileprivate final for func get guard if import " +
                "in indirect infix init inout internal is isolated lazy let macro mutating nil nonisolated nonmutating open operator optional " +
                "override package postfix precedencegroup prefix private protocol public repeat required rethrows return self Self set some " +
                "static struct subscript super switch throws throw true try typealias unowned var weak where while willSet");
            Add(result, "go golang", MNNMarkdownCodeKind.Go,
                "break case chan const continue default defer else fallthrough for func go goto if import interface map package range return " +
                "select struct switch type var true false nil iota");
            Add(result, "rust rs", MNNMarkdownCodeKind.Rust,
                "as async await break const continue crate dyn else enum extern false fn for if impl in let loop match mod move mut pub ref " +
                "return self Self static struct super trait true type unsafe use where while abstract become box do final macro override priv typeof unsized virtual yield");
            Add(result, "dart", MNNMarkdownCodeKind.Dart,
                "abstract as assert async await base break case catch class const continue covariant default deferred do dynamic else enum " +
                "export extends extension external factory false final finally for Function get hide if implements import in interface is late " +
                "library mixin new null of on operator part required rethrow return sealed set show static super switch sync this throw true try typedef var void when while with yield");
            Add(result, "lua", MNNMarkdownCodeKind.Lua,
                "and break do else elseif end false for function goto if in local nil not or repeat return then true until while");
            Add(result, "ruby rb", MNNMarkdownCodeKind.Ruby,
                "BEGIN END alias and begin break case class def defined do else elsif end ensure false for if in module next nil not or redo rescue retry return self super then true undef unless until when while yield");
            Add(result, "php", MNNMarkdownCodeKind.Php,
                "abstract and array as break callable case catch class clone const continue declare default die do echo else elseif empty enddeclare " +
                "endfor endforeach endif endswitch endwhile enum eval exit extends final finally fn for foreach function global goto if implements " +
                "include include_once instanceof insteadof interface isset list match namespace new or print private protected public readonly " +
                "require require_once return static switch throw trait try unset use var while xor yield true false null");
            Add(result, "r", MNNMarkdownCodeKind.R,
                "if else repeat while function for in next break return TRUE FALSE NULL Inf NaN NA NA_integer_ NA_real_ NA_complex_ NA_character_");
            Add(result, "html htm xml xhtml svg", MNNMarkdownCodeKind.Markup, "");
            Add(result, "css", MNNMarkdownCodeKind.Css, "important inherit initial unset revert none auto");
            Add(result, "yaml yml", MNNMarkdownCodeKind.Yaml, "true false null True False Null TRUE FALSE NULL");
            Add(result, "toml", MNNMarkdownCodeKind.Toml, "true false inf nan");
            return result;
        }

        private static void Add(Dictionary<string, MNNMarkdownCodeLanguage> languages, string aliases,
            MNNMarkdownCodeKind kind, string keywords)
        {
            var language = new MNNMarkdownCodeLanguage(kind, keywords);
            foreach (string alias in aliases.Split(' ')) languages.Add(alias, language);
        }
    }
}
