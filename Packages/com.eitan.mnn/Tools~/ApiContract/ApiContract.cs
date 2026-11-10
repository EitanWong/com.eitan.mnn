using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

// Uses the installed SDK's Roslyn. It never loads MNN/Unity assemblies into the process.
internal static class ApiContract
{
    private static readonly string[] Defines = { "UNITY_EDITOR", "UNITY_EDITOR_OSX", "UNITY_STANDALONE_OSX",
        "ENABLE_MONO", "UNITY_2021_3", "UNITY_2021_3_OR_NEWER", "UNITY_2020_1_OR_NEWER",
        "UNITY_2019_1_OR_NEWER", "UNITY_2018_1_OR_NEWER", "NET_STANDARD_2_1", "UNITY_INCLUDE_TESTS" };
    private static readonly SymbolDisplayFormat Format = new SymbolDisplayFormat(
        typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces,
        propertyStyle: SymbolDisplayPropertyStyle.ShowReadWriteDescriptor,
        kindOptions: SymbolDisplayKindOptions.IncludeTypeKeyword,
        genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters | SymbolDisplayGenericsOptions.IncludeTypeConstraints,
        memberOptions: SymbolDisplayMemberOptions.IncludeAccessibility | SymbolDisplayMemberOptions.IncludeModifiers |
            SymbolDisplayMemberOptions.IncludeType | SymbolDisplayMemberOptions.IncludeParameters |
            SymbolDisplayMemberOptions.IncludeConstantValue | SymbolDisplayMemberOptions.IncludeContainingType,
        parameterOptions: SymbolDisplayParameterOptions.IncludeType | SymbolDisplayParameterOptions.IncludeName |
            SymbolDisplayParameterOptions.IncludeDefaultValue | SymbolDisplayParameterOptions.IncludeParamsRefOut |
            SymbolDisplayParameterOptions.IncludeExtensionThis,
        miscellaneousOptions: SymbolDisplayMiscellaneousOptions.UseSpecialTypes | SymbolDisplayMiscellaneousOptions.EscapeKeywordIdentifiers);

    private sealed record ApiEntry(string Assembly, string Domain, string Id, string Signature, string Summary);

    public static int Main(string[] args)
    {
        if (args.Length == 1 && args[0] == "--self-test") return SelfTest();
        if (args.Length != 4) throw new ArgumentException("Expected package, Unity Contents, output, write|check.");
        string package = Path.GetFullPath(args[0]), unity = args[1], output = args[2];
        Directory.CreateDirectory(output);
        var references = References(unity);
        var errors = new SortedSet<string>(StringComparer.Ordinal);
        var entries = new List<ApiEntry>();
        string runtime = Path.Combine(package, "Runtime");
        var folders = Directory.GetFiles(runtime, "*.asmdef", SearchOption.AllDirectories)
            .ToDictionary(path => Path.GetDirectoryName(path)!, path => JsonDocument.Parse(File.ReadAllText(path)).RootElement.GetProperty("name").GetString()!);
        var sources = Directory.GetFiles(runtime, "*.cs", SearchOption.AllDirectories)
            .Where(path => !path.Contains(Path.DirectorySeparatorChar + "Plugins" + Path.DirectorySeparatorChar))
            .GroupBy(path => Owner(path, folders));
        foreach (var group in sources.Where(group => group.Key != "MNN.Unity.Burst").OrderBy(group => group.Key, StringComparer.Ordinal))
        {
            var trees = group.OrderBy(path => path, StringComparer.Ordinal).Select(Parse).ToArray();
            var compilation = CSharpCompilation.Create(group.Key, trees, references,
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, allowUnsafe: true));
            foreach (var diagnostic in compilation.GetDiagnostics().Where(item => item.Severity == DiagnosticSeverity.Error))
                errors.Add(diagnostic.ToString());
            CheckLayers(compilation, errors);
            Collect(compilation.Assembly.GlobalNamespace, package, group.Key, entries, errors);
        }
        var ordered = entries.OrderBy(entry => entry.Domain, StringComparer.Ordinal).ThenBy(entry => entry.Id, StringComparer.Ordinal).ToArray();
        string manifest = JsonSerializer.Serialize(ordered, new JsonSerializerOptions { WriteIndented = true }) + "\n";
        string markdown = Markdown(ordered);
        File.WriteAllText(Path.Combine(output, "api-surface.json"), manifest);
        File.WriteAllText(Path.Combine(output, "PublicAPI.md"), markdown);
        string docs = Path.Combine(package, "Documentation~");
        foreach (var (file, contents) in new[] { ("api-surface.json", manifest), ("PublicAPI.md", markdown) })
        {
            string path = Path.Combine(docs, file);
            if (args[3] == "write" && errors.Count == 0) File.WriteAllText(path, contents);
            else if (!File.Exists(path) || File.ReadAllText(path) != contents) errors.Add("API reference is stale: " + path);
        }
        var report = new { passed = errors.Count == 0, entries = ordered.Length,
            publicTypes = ordered.Count(entry => entry.Id.StartsWith("T:", StringComparison.Ordinal)),
            documentedEntries = ordered.Count(entry => entry.Summary.Length != 0),
            configuration = "Unity 2021.3 Editor macOS Mono; optional Burst excluded",
            semantic_boundaries_verified = true, native_inference_verified = false, errors };
        File.WriteAllText(Path.Combine(output, "summary.json"), JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }) + "\n");
        foreach (string error in errors) Console.Error.WriteLine(error);
        Console.WriteLine($"API contract: {(errors.Count == 0 ? "PASS" : "FAIL")}; {report.publicTypes} types; {ordered.Length} entries; {errors.Count} errors");
        return errors.Count == 0 ? 0 : 1;
    }

    private static SyntaxTree Parse(string path) => CSharpSyntaxTree.ParseText(File.ReadAllText(path),
        new CSharpParseOptions(LanguageVersion.CSharp9, DocumentationMode.Parse, preprocessorSymbols: Defines), path);

    private static MetadataReference[] References(string unity) => new[] { "NetStandard/ref/2.1.0", "NetStandard/compat/2.1.0/shims/netfx", "Managed/UnityEngine" }
        .SelectMany(folder => Directory.GetFiles(Path.Combine(unity, folder), "*.dll"))
        .Distinct(StringComparer.Ordinal).Select(path => MetadataReference.CreateFromFile(path)).ToArray();

    private static string Owner(string path, Dictionary<string, string> folders)
    {
        for (string? parent = Path.GetDirectoryName(path); parent != null; parent = Path.GetDirectoryName(parent))
            if (folders.TryGetValue(parent, out string? name)) return name;
        throw new InvalidOperationException("No assembly owner: " + path);
    }

    private static bool Public(ISymbol symbol)
    {
        if (symbol.DeclaredAccessibility != Accessibility.Public && symbol.DeclaredAccessibility != Accessibility.Protected &&
            symbol.DeclaredAccessibility != Accessibility.ProtectedOrInternal) return false;
        return symbol.ContainingType == null || Public(symbol.ContainingType);
    }

    private static void CheckLayers(CSharpCompilation compilation, ISet<string> errors)
    {
        foreach (var tree in compilation.SyntaxTrees)
        {
            var model = compilation.GetSemanticModel(tree);
            string path = tree.FilePath.Replace('\\', '/');
            foreach (var attribute in tree.GetRoot().DescendantNodes().OfType<AttributeSyntax>())
            {
                if (!(model.GetSymbolInfo(attribute).Symbol is IMethodSymbol constructor) ||
                    constructor.ContainingType.ToDisplayString() != "System.Runtime.CompilerServices.InternalsVisibleToAttribute") continue;
                string? friend = attribute.ArgumentList?.Arguments.FirstOrDefault()?.Expression is LiteralExpressionSyntax value ? value.Token.ValueText : null;
                if (compilation.AssemblyName == "MNN.Unity" && friend != "MNN.Unity.Tests" && friend != "MNN.Unity.Runtime.Tests" &&
                    friend != "MNN.Unity.Integration.Tests" && friend != "MNN.Unity.Burst") errors.Add("Unapproved runtime friend: " + friend);
            }
            if (!path.Contains("/Runtime/Core/")) continue;
            foreach (var name in tree.GetRoot().DescendantNodes().OfType<SimpleNameSyntax>())
            {
                ISymbol? symbol = model.GetSymbolInfo(name).Symbol;
                if (symbol == null || !SymbolEqualityComparer.Default.Equals(symbol.ContainingAssembly, compilation.Assembly)) continue;
                if (symbol is ILocalSymbol || symbol is IParameterSymbol || symbol is ITypeParameterSymbol || symbol is INamespaceSymbol) continue;
                foreach (var declaration in symbol.DeclaringSyntaxReferences)
                {
                    string target = declaration.SyntaxTree.FilePath.Replace('\\', '/');
                    if (target.Contains("/Runtime/Components/"))
                        errors.Add($"Core depends on component: {name.GetLocation().GetLineSpan()} -> {symbol.ToDisplayString()}");
                }
            }
        }
        CheckComponentConsumer(compilation, errors);
    }

    private static void CheckComponentConsumer(CSharpCompilation compilation, ISet<string> errors)
    {
        var components = compilation.SyntaxTrees.Where(tree =>
            tree.FilePath.Replace('\\', '/').Contains("/Runtime/Components/")).ToArray();
        if (components.Length == 0) return;
        var api = compilation.RemoveSyntaxTrees(components);
        var consumer = CSharpCompilation.Create("MNN.PublicApi.ComponentConsumer", components,
            compilation.References.Append(api.ToMetadataReference()), compilation.Options);
        foreach (var diagnostic in consumer.GetDiagnostics().Where(item => item.Severity == DiagnosticSeverity.Error))
            errors.Add("Component cannot compile against public API: " + diagnostic);
    }

    private static void Collect(INamespaceOrTypeSymbol container, string package, string assembly,
        List<ApiEntry> entries, ISet<string> errors)
    {
        foreach (ISymbol symbol in container.GetMembers())
        {
            if (symbol is INamespaceSymbol ns) { Collect(ns, package, assembly, entries, errors); continue; }
            bool implicitConstructor = symbol is IMethodSymbol constructor && constructor.MethodKind == MethodKind.Constructor;
            if (!Public(symbol) || symbol.IsImplicitlyDeclared && !implicitConstructor) continue;
            if (symbol is IMethodSymbol method && method.AssociatedSymbol != null) continue;
            string? id = symbol.GetDocumentationCommentId();
            if (id == null) continue;
            string signature = symbol.ToDisplayString(Format);
            string source = symbol.DeclaringSyntaxReferences.FirstOrDefault()?.SyntaxTree.FilePath ??
                symbol.ContainingType?.DeclaringSyntaxReferences.FirstOrDefault()?.SyntaxTree.FilePath ?? "";
            string relative = Path.GetRelativePath(package, source).Replace('\\', '/');
            if (relative.StartsWith("Runtime/Interop/", StringComparison.Ordinal) ||
                symbol.ContainingNamespace?.ToDisplayString().StartsWith("MNN.Unity.Interop", StringComparison.Ordinal) == true)
                errors.Add("Exported interop symbol: " + id);
            var types = SignatureTypes(symbol);
            if (types.Any(ContainsNativeType)) errors.Add("Native type in public signature: " + signature);
            if (symbol.GetAttributes().Any(attribute => attribute.AttributeClass?.ToDisplayString() == "System.Runtime.InteropServices.DllImportAttribute"))
                errors.Add("Public native import: " + id);
            string domain = relative.StartsWith("Runtime/Core/", StringComparison.Ordinal) ? relative.Split('/')[2] : "Components";
            entries.Add(new ApiEntry(assembly, domain, id, signature, Summary(symbol)));
            if (symbol is INamedTypeSymbol type) Collect(type, package, assembly, entries, errors);
        }
    }

    private static IEnumerable<ITypeSymbol> SignatureTypes(ISymbol symbol)
    {
        if (symbol is IMethodSymbol method) return method.Parameters.Select(parameter => parameter.Type).Append(method.ReturnType)
            .Concat(method.TypeParameters.SelectMany(parameter => parameter.ConstraintTypes));
        if (symbol is IPropertySymbol property) return property.Parameters.Select(parameter => parameter.Type).Append(property.Type);
        if (symbol is IFieldSymbol field) return new[] { field.Type };
        if (symbol is IEventSymbol ev) return new[] { ev.Type };
        if (symbol is INamedTypeSymbol callback && callback.TypeKind == TypeKind.Delegate && callback.DelegateInvokeMethod != null)
            return SignatureTypes(callback.DelegateInvokeMethod);
        if (symbol is INamedTypeSymbol type) return type.Interfaces.Cast<ITypeSymbol>()
            .Concat(type.BaseType == null ? Array.Empty<ITypeSymbol>() : new[] { type.BaseType })
            .Concat(type.TypeParameters.SelectMany(parameter => parameter.ConstraintTypes));
        return Array.Empty<ITypeSymbol>();
    }

    private static bool ContainsNativeType(ITypeSymbol type)
    {
        if (type is IPointerTypeSymbol || type is IFunctionPointerTypeSymbol) return true;
        if (type is IArrayTypeSymbol array) return ContainsNativeType(array.ElementType);
        if (type.SpecialType == SpecialType.System_IntPtr || type.SpecialType == SpecialType.System_UIntPtr) return true;
        if (type.ContainingNamespace?.ToDisplayString().StartsWith("MNN.Unity.Interop", StringComparison.Ordinal) == true) return true;
        if (type is INamedTypeSymbol named && named.TypeArguments.Any(ContainsNativeType)) return true;
        for (var parent = type.BaseType; parent != null; parent = parent.BaseType)
            if (parent.ToDisplayString() == "System.Runtime.InteropServices.SafeHandle") return true;
        return false;
    }

    private static string Summary(ISymbol symbol)
    {
        string? xml = symbol.GetDocumentationCommentXml();
        if (string.IsNullOrWhiteSpace(xml)) return "";
        try
        {
            var element = XElement.Parse(xml).Element("summary");
            return element == null ? "" : string.Join(" ", element.Value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        }
        catch (System.Xml.XmlException) { return ""; }
    }

    private static string Markdown(ApiEntry[] entries)
    {
        var text = new StringBuilder("# 公共 API 参考\n\n此文件由源码与 Roslyn 语义模型生成。请在对应 C# XML 注释中修改说明，再运行 `validate_public_api.py --write`。\n\n");
        text.Append("范围：Unity 2021.3 Editor macOS Mono 的 Runtime 与独立 Harness 契约；可选 Burst 未包含。类型/方法的存在不代表模型、平台或设备已验证。使用步骤与线程、所有权约定见 [统一 API 层](PublicApiLayer.md)。\n\n");
        foreach (var group in entries.GroupBy(entry => entry.Domain))
        {
            text.Append("## ").Append(group.Key).Append("\n\n");
            foreach (var entry in group)
            {
                text.Append("```csharp\n").Append(entry.Signature).Append("\n```\n\n");
                if (entry.Summary.Length != 0) text.Append(entry.Summary.Replace("<", "&lt;").Replace(">", "&gt;")).Append("\n\n");
            }
        }
        return text.ToString();
    }

    private static int SelfTest()
    {
        string[] fixtures = {
            "namespace MNN.Unity { public class Api { public int Run(int input) => input + 1; } }",
            "namespace MNN.Unity.Interop { public class Native { public static void Run() {} } }",
            "namespace MNN.Unity { public class Api { public System.IntPtr Raw { get; } } }",
            "namespace MNN.Unity { internal class Hidden { public void Run() {} } }",
            "namespace MNN.Unity { public delegate void Callback(System.IntPtr value); }"
        };
        for (int i = 0; i < fixtures.Length; ++i)
        {
            var tree = CSharpSyntaxTree.ParseText(fixtures[i], path: "/package/Runtime/Core/Common/Api.cs");
            var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(Path.PathSeparator)
                .Select(path => MetadataReference.CreateFromFile(path));
            var compilation = CSharpCompilation.Create("MNN.Unity", new[] { tree }, references,
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
            var entries = new List<ApiEntry>(); var errors = new SortedSet<string>();
            Collect(compilation.Assembly.GlobalNamespace, "/package", "MNN.Unity", entries, errors);
            bool rejected = errors.Count != 0;
            if (rejected != (i == 1 || i == 2 || i == 4) || i == 3 && entries.Count != 0) throw new Exception("Contract fixture failed: " + i);
        }
        LayerFixture("internal static class Core { internal static void Run() {} }", "public static class Component { public static void Run() => Core.Run(); }", true);
        LayerFixture("public static class Core { public static void Run() {} }", "public static class Component { public static void Run() => Core.Run(); }", false);
        LayerFixture("public static class Core { public static void Run() => Component.Run(); }", "public static class Component { public static void Run() {} }", true);
        LayerFixture("public class Core { internal Core() {} }", "public static class Component { public static void Run() { var value = new Core(); } }", true);
        LayerFixture("public class Core { public static int Value { get; internal set; } }", "public static class Component { public static void Run() { Core.Value = 1; } }", true);
        Console.WriteLine("API contract self-test: PASS (surface export, native types, component/API and Core/component boundaries)");
        return 0;
    }

    private static void LayerFixture(string core, string component, bool rejected)
    {
        var trees = new[] {
            CSharpSyntaxTree.ParseText(core, path: "/package/Runtime/Core/Common/Core.cs"),
            CSharpSyntaxTree.ParseText(component, path: "/package/Runtime/Components/Component.cs")
        };
        var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(Path.PathSeparator)
            .Select(path => MetadataReference.CreateFromFile(path));
        var compilation = CSharpCompilation.Create("MNN.Unity", trees, references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        if (compilation.GetDiagnostics().Any(item => item.Severity == DiagnosticSeverity.Error))
            throw new Exception("Invalid layer test fixture.");
        var errors = new SortedSet<string>();
        CheckLayers(compilation, errors);
        if ((errors.Count != 0) != rejected) throw new Exception("Layer fixture failed: " + core);
    }
}
