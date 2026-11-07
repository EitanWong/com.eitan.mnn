using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using MNN.Unity.Interop;
using NUnit.Framework;
using UnityEditor.PackageManager;

namespace MNN.Unity.Tests
{
    public class PlatformPackagingTests
    {
        private static string Root => PackageInfo.FindForAssembly(typeof(MNNInterop).Assembly).resolvedPath;
        [Test]
        public void NativeImports_UseOnlyOfficialCppAndSystemSymbols()
        {
            var imports = typeof(MNNInterop).GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic).Select(method => method.GetCustomAttribute<DllImportAttribute>()).Where(import => import != null && (import.Value == "MNN" || import.Value == "/usr/lib/libc++.1.dylib")).ToArray();
            if (!MNNPlatformSupport.IsSupported)
            {
                Assert.IsEmpty(imports, "Unsupported targets must not import Apple C++ symbols.");
                return;
            }

#if UNITY_EDITOR_WIN || UNITY_EDITOR_LINUX
            Assert.AreEqual(27, imports.Length, "Desktop core calls only the audited official Interpreter/Tensor/version APIs.");
#else
            Assert.AreEqual(70, imports.Length, "66 bundled MNN/STL and 4 system libc++ imports, including direct model loads and Express graph operations.");
#endif
            foreach (var import in imports)
            {
                Assert.AreEqual(CallingConvention.Cdecl, import.CallingConvention, import.EntryPoint);
                if (import.Value == MNNNative.LibraryName)
#if UNITY_EDITOR_WIN
                    StringAssert.StartsWith("?", import.EntryPoint, "MNN must expose official MSVC C++ APIs.");
#else
                    StringAssert.StartsWith("_ZN", import.EntryPoint, "MNN must expose official Itanium C++ APIs.");
#endif
                else
                    Assert.AreEqual("/usr/lib/libc++.1.dylib", import.Value, "Unexpected native dependency.");
                Assert.IsFalse(import.EntryPoint.StartsWith("MNN_", StringComparison.Ordinal));
            }
        }

        [Test]
        public void LinuxX64Importer_DoesNotEnableAnArm64Binary()
        {
            string path = Path.Combine(Root, "Runtime/Plugins/Linux/x86_64/libMNN.so");
            byte[] header = File.ReadAllBytes(path).Take(20).ToArray();
            CollectionAssert.AreEqual(new byte[]{0x7f, 0x45, 0x4c, 0x46}, header.Take(4));
            Assert.AreEqual(2, header[4], "Expected ELF64.");
            Assert.AreEqual(1, header[5], "Expected little-endian ELF.");
            int machine = header[18] | (header[19] << 8);
            // EM_X86_64=62. The legacy artifact is EM_AARCH64=183 and must
            // remain disabled until a matching official build is provided.
            if (machine != 62)
            {
                string meta = File.ReadAllText(path + ".meta");
                StringAssert.IsMatch(@"Editor: Editor\s+second:\s+enabled: 0", meta);
                StringAssert.IsMatch(@"Standalone: Linux64\s+second:\s+enabled: 0", meta);
                Assert.IsFalse(Regex.IsMatch(meta, @"enabled: 1"), "Wrong-architecture library is enabled.");
            }
        }

        [TestCase("Android/libs/armeabi-v7a/libMNN.so")]
        public void UnsupportedAbi_PluginImporterIsDisabled(string relativePath)
        {
            string meta = File.ReadAllText(Path.Combine(Root, "Runtime/Plugins", relativePath) + ".meta");
            Assert.IsFalse(Regex.IsMatch(meta, @"enabled: 1"), "No managed ABI is implemented for " + relativePath);
        }

        [TestCase("Windows/x86_64/MNN.dll", "Win64", "Windows")]
        [TestCase("Linux/x86_64/libMNN.so", "Linux64", "Linux")]
        public void DesktopMono_PluginImporterEnablesMatchingEditorAndPlayer(string relativePath, string player, string editor)
        {
            string meta = File.ReadAllText(Path.Combine(Root, "Runtime/Plugins", relativePath) + ".meta");
            StringAssert.IsMatch(@"Editor: Editor\s+second:\s+enabled: 1", meta);
            StringAssert.IsMatch(@"Standalone: " + player + @"\s+second:\s+enabled: 1", meta);
            StringAssert.Contains("OS: " + editor, meta);
            StringAssert.Contains("CPU: x86_64", meta);
        }

        [Test]
        public void PlayerTests_HaveNoEditorAssemblyDependency()
        {
            string asmdef = File.ReadAllText(Path.Combine(Root, "Tests/Runtime/MNN.Unity.Runtime.Tests.asmdef"));
            StringAssert.DoesNotContain("UnityEditor", asmdef);
            StringAssert.DoesNotContain("MNN.Unity.Editor", asmdef);
            StringAssert.Contains("UNITY_INCLUDE_TESTS", asmdef);
            StringAssert.Contains("\"includePlatforms\": []", asmdef);
        }
    }
}
