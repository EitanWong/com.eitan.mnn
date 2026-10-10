using System;
using System.Collections;
using System.Runtime.InteropServices;
using System.Reflection;
using System.Linq;
using MNN.Unity.Interop;
using NUnit.Framework;
using UnityEngine;

namespace MNN.Unity.Tests
{
    [Category("PlatformContract")]
    public class PlatformContractTests
    {
        // Enumerate Unity's entire platform enum so new targets default to
        // rejection until their ABI is implemented. This is policy coverage,
        // not evidence that inference ran on those operating systems.
        public static IEnumerable PlatformCases
        {
            get
            {
                foreach (RuntimePlatform platform in Enum.GetValues(typeof(RuntimePlatform)))
                {
                    foreach (bool il2cpp in new[]{false, true})
                    {
                        bool supported = !il2cpp && (platform == RuntimePlatform.OSXEditor || platform == RuntimePlatform.OSXPlayer);
                        yield return new TestCaseData(platform, il2cpp, supported).SetName($"AbiContract_{platform}_{(il2cpp ? "IL2CPP" : "Mono")}");
                    }
                }
            }
        }

        [TestCaseSource(nameof(PlatformCases))]
        public void PlatformMatrix_RejectsUnimplementedAbi(RuntimePlatform platform, bool il2cpp, bool supported)
        {
            string reason = MNNPlatformSupport.GetUnsupportedReason(platform, il2cpp, 8, true);
            if (supported)
                Assert.IsNull(reason);
            else
                Assert.That(reason, Is.Not.Null.And.Not.Empty);
        }

        [TestCase(4, true), TestCase(8, false), TestCase(4, false)]
        public void UnsupportedMemoryLayout_IsRejected(int pointerSize, bool littleEndian) => Assert.IsNotNull(MNNPlatformSupport.GetUnsupportedReason(RuntimePlatform.OSXPlayer, false, pointerSize, littleEndian));
        [Test]
        public void CurrentRuntime_RejectsBeforeResolvingNativeSymbols()
        {
#if ENABLE_IL2CPP && !UNITY_EDITOR
            const string backend = "IL2CPP";
#else
            const string backend = "Mono2x";
#endif
            TestContext.WriteLine($"Platform={Application.platform}; Unity={Application.unityVersion}; " + $"Backend={backend}; Architecture={RuntimeInformation.ProcessArchitecture}; " + $"PointerSize={IntPtr.Size}; Supported={MNNPlatformSupport.IsSupported}; " + $"Reason={MNNPlatformSupport.UnsupportedReason}");
            if (MNNPlatformSupport.IsSupported)
            {
                Assert.IsNull(MNNPlatformSupport.UnsupportedReason);
                return;
            }

            // An unsupported runtime must not produce DllNotFoundException,
            // EntryPointNotFoundException, or execute an incompatible STL ABI.
            Assert.Throws<PlatformNotSupportedException>(() => MNNInterop.RequireAbi());
            Assert.IsFalse(typeof(MNNInterop).GetMethods(BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public).Any(method => method.IsDefined(typeof(DllImportAttribute), false)), "Unsupported Players must not compile Apple-specific native imports.");
            Assert.Throws<PlatformNotSupportedException>(() => MNNVersion.GetVersion());
            Assert.IsFalse(MNNVersion.IsLoaded());
            Assert.Throws<PlatformNotSupportedException>(() => MNNInterpreter.CreateFromBuffer(AffineFixture.Bytes));
            Assert.Throws<PlatformNotSupportedException>(() => MNNInterop.MNN_Llm_create("unused", "{}"));
            Assert.Throws<PlatformNotSupportedException>(() => MNNInterop.MNN_Embedding_create("unused", "{}"));
            Assert.Throws<PlatformNotSupportedException>(() => MNNLlm.Load("unused"));
            Assert.Throws<PlatformNotSupportedException>(() => MNNEmbedding.Load("unused"));
            Assert.Throws<PlatformNotSupportedException>(() => MNNReranker.Load("unused"));
        }

        [Test]
        public void PlatformGuard_DoesNotInitializeNativeCallbackPointers() => Assert.IsNull(typeof(MNNInterop).TypeInitializer, "Native callback initialization must be deferred until a supported inference call.");
        [Test]
        public void AppleAbi_PublicLayoutsMatchNativeContract()
        {
            if (!MNNPlatformSupport.IsSupported)
                Assert.Ignore("Apple ABI layout assertions require the implemented 64-bit runtime.");
            Assert.AreEqual(24, Marshal.SizeOf<MNNInterop.CppString>());
            Assert.AreEqual(24, Marshal.SizeOf<MNNInterop.CppVector>());
            Assert.AreEqual(16, Marshal.SizeOf<MNNInterop.CppShared>());
            Assert.AreEqual(8, Marshal.SizeOf<MNNInterop.HalideType>());
            Assert.AreEqual(4, Marshal.OffsetOf<MNNInterop.HalideType>("Bits").ToInt32());
            Assert.AreEqual(6, Marshal.OffsetOf<MNNInterop.HalideType>("Lanes").ToInt32());
            Assert.AreEqual(24, Marshal.SizeOf<BackendConfig>());
            Assert.AreEqual(16, Marshal.OffsetOf<BackendConfig>("sharedContext").ToInt32());
            Assert.AreEqual(96, Marshal.SizeOf<ScheduleConfig>());
            Assert.AreEqual(88, Marshal.OffsetOf<ScheduleConfig>("backendConfig").ToInt32());
        }
    }
}
