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
            var imports = typeof(MNNInterop).GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic).Select(method => method.GetCustomAttribute<DllImportAttribute>()).Where(import => import != null).ToArray();
            if (!MNNPlatformSupport.IsSupported)
            {
                Assert.IsEmpty(imports, "Unsupported targets must not import Apple C++ symbols.");
                return;
            }

            Assert.AreEqual(60, imports.Length, "56 bundled MNN/STL and 4 system libc++ imports, including public Express Variable and single-input Module calls.");
            foreach (var import in imports)
            {
                Assert.AreEqual(CallingConvention.Cdecl, import.CallingConvention, import.EntryPoint);
                if (import.Value == MNNNative.LibraryName)
                    StringAssert.StartsWith("_ZN", import.EntryPoint, "MNN must expose official C++ APIs.");
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
