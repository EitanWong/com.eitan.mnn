using System;
using System.IO;
using NUnit.Framework;

namespace MNN.Unity.Tests
{
    public class PiperDependencyTests
    {
        private string _root, _environment, _workingDirectory;

        [SetUp]
        public void SetUp()
        {
            _root = Path.GetFullPath("TestArtifacts~/PiperDependencies/" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_root);
            _environment = Environment.GetEnvironmentVariable("MNN_ESPEAK_NG_PATH");
            _workingDirectory = Directory.GetCurrentDirectory();
            Environment.SetEnvironmentVariable("MNN_ESPEAK_NG_PATH", null);
        }

        [TearDown]
        public void TearDown()
        {
            Directory.SetCurrentDirectory(_workingDirectory);
            Environment.SetEnvironmentVariable("MNN_ESPEAK_NG_PATH", _environment);
            Directory.Delete(_root, true);
        }

        [Test]
        public void NestedModelDependenciesAreFoundWithoutPaths()
        {
            string data = Data("assets/phonemizer/share/espeak-ng-data");
            string tool = Tool("assets/phonemizer/bin/espeak-ng");
            Assert.AreEqual(data, MNNPiper.ResolveDataDirectory(_root, "old-assets"));
            Assert.AreEqual(tool, MNNPiper.ResolveEspeak(null, _root));
        }

        [TestCase(UnityEngine.RuntimePlatform.OSXPlayer, false)]
        [TestCase(UnityEngine.RuntimePlatform.WindowsPlayer, false)]
        [TestCase(UnityEngine.RuntimePlatform.LinuxPlayer, false)]
        [TestCase(UnityEngine.RuntimePlatform.Android, true)]
        [TestCase(UnityEngine.RuntimePlatform.IPhonePlayer, true)]
        [TestCase(UnityEngine.RuntimePlatform.WebGLPlayer, true)]
        public void MissingPhonemizerBackendIsNotReportedAsMissingPath(UnityEngine.RuntimePlatform platform, bool unsupported)
        {
            Assert.AreEqual(unsupported, MNNPiper.PhonemizerUnsupportedReason(platform) != null);
        }

        [Test]
        public void ConfiguredAssetFolderCanContainDataFolder()
        {
            string nested = Data("assets/espeak-ng-data");
            Data("espeak-ng-data");
            Assert.AreEqual(nested, MNNPiper.ResolveDataDirectory(_root, "assets"));
        }

        [Test]
        public void IncompleteDataDoesNotHideCompleteNestedData()
        {
            string incomplete = Data("espeak-ng-data");
            File.Delete(Path.Combine(incomplete, "phondata"));
            string complete = Data("vendor/share/espeak-ng-data");
            Assert.AreEqual(complete, MNNPiper.ResolveDataDirectory(_root));
            Assert.Throws<DirectoryNotFoundException>(() => MNNPiper.ResolveDataDirectory(_root, selected: incomplete));
        }

        [Test]
        public void ModelWrapperTakesPriorityOverEnvironmentAndRawBinary()
        {
            string wrapper = Tool("vendor/run-espeak-ng.sh");
            Tool("vendor/espeak-ng");
            Environment.SetEnvironmentVariable("MNN_ESPEAK_NG_PATH", Tool("external-tool"));
            Assert.AreEqual(wrapper, MNNPiper.ResolveEspeak(null, _root));
        }

        [Test]
        public void RootAndBinToolsTakePriorityOverNestedTools()
        {
            Tool("assets/vendor/espeak-ng");
            string bin = Tool("bin/espeak-ng");
            Assert.AreEqual(bin, MNNPiper.FindEspeak(_root));
            string root = Tool("espeak-ng");
            Assert.AreEqual(root, MNNPiper.FindEspeak(_root));
        }

        [Test]
        public void DataFilesAndInvalidToolsCannotMaskEnvironmentTool()
        {
            Tool("espeak-ng-data/espeak-ng");
            string invalid = Tool("vendor/espeak-ng");
            File.WriteAllText(invalid, "not an executable");
            string external = Tool("external-tool");
            Environment.SetEnvironmentVariable("MNN_ESPEAK_NG_PATH", external);
            Assert.AreEqual(external, MNNPiper.FindEspeak(_root));
        }

        [TestCase(UnityEngine.RuntimePlatform.WindowsPlayer, "pe", true)]
        [TestCase(UnityEngine.RuntimePlatform.WindowsPlayer, "elf", false)]
        [TestCase(UnityEngine.RuntimePlatform.WindowsPlayer, "script", false)]
        [TestCase(UnityEngine.RuntimePlatform.OSXPlayer, "mach", true)]
        [TestCase(UnityEngine.RuntimePlatform.OSXPlayer, "pe", false)]
        [TestCase(UnityEngine.RuntimePlatform.OSXPlayer, "elf", false)]
        [TestCase(UnityEngine.RuntimePlatform.OSXPlayer, "script", true)]
        [TestCase(UnityEngine.RuntimePlatform.LinuxPlayer, "elf", true)]
        [TestCase(UnityEngine.RuntimePlatform.LinuxPlayer, "mach", false)]
        [TestCase(UnityEngine.RuntimePlatform.LinuxPlayer, "script", true)]
        [TestCase(UnityEngine.RuntimePlatform.Android, "elf", false)]
        [TestCase(UnityEngine.RuntimePlatform.IPhonePlayer, "mach", false)]
        [TestCase(UnityEngine.RuntimePlatform.WebGLPlayer, "script", false)]
        public void BundledProgramsMustMatchTheHostOS(UnityEngine.RuntimePlatform platform, string format, bool expected)
        {
            byte[] header = format == "pe" ? new byte[]{0x4d, 0x5a, 0, 0} :
                format == "elf" ? new byte[]{0x7f, 0x45, 0x4c, 0x46} :
                format == "mach" ? new byte[]{0xcf, 0xfa, 0xed, 0xfe} : new byte[]{0x23, 0x21, 0x2f, 0x62};
            Assert.AreEqual(expected, MNNPiper.IsEspeakHeaderForPlatform(header, platform));
        }

        [Test]
        public void ForeignProgramCannotHideAUsableEnvironmentTool()
        {
            string foreign = Tool("vendor/espeak-ng");
            bool windows = System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Windows);
            File.WriteAllBytes(foreign, windows ? new byte[]{0x7f, 0x45, 0x4c, 0x46} : new byte[]{0x4d, 0x5a, 0, 0});
            string external = Tool("external-tool");
            Environment.SetEnvironmentVariable("MNN_ESPEAK_NG_PATH", external);
            Assert.AreEqual(external, MNNPiper.FindEspeak(_root));
        }

        [Test]
        public void ExplicitPathsOverrideAutomaticDiscovery()
        {
            Tool("espeak-ng");
            string selected = Tool("chosen-tool");
            Assert.AreEqual(selected, MNNPiper.ResolveEspeak(selected, _root));
            Assert.Throws<FileNotFoundException>(() => MNNPiper.ResolveEspeak(Path.Combine(_root, "missing"), _root));
            string data = Data("chosen-data");
            Data("espeak-ng-data");
            Assert.AreEqual(data, MNNPiper.ResolveDataDirectory(_root, selected: data));
        }

        [TestCase("TestArtifacts~/Dependencies/espeak-ng/run-espeak-ng.sh")]
        [TestCase("TestArtifacts~/GenerationImplementation/Dependencies/espeak-ng/run-espeak-ng.sh")]
        public void RuntimeLoaderReusesExistingEditorProjectDependencies(string relative)
        {
            string tool = Tool(relative);
            string model = Path.Combine(_root, "Assets/StreamingAssets/MNN/Models/Piper");
            Directory.CreateDirectory(model);
            Assert.AreEqual(tool, MNNPiper.ResolveEspeak(null, model));
            // SiriAI can load its models from persistentDataPath on a worker.
            Directory.SetCurrentDirectory(_root);
            string resolved = System.Threading.Tasks.Task.Run(() =>
                MNNPiper.ResolveEspeak(null, Path.Combine(_root, "persistent/Piper"))).GetAwaiter().GetResult();
            Assert.AreEqual(tool, resolved);
        }

        private string Tool(string relative)
        {
            string path = Path.Combine(_root, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            if (System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Windows))
                File.WriteAllBytes(path, new byte[]{0x4d, 0x5a, 0, 0});
            else
                File.WriteAllText(path, "#!/bin/sh\nexit 0\n");
            return path;
        }

        private string Data(string relative)
        {
            string path = Path.Combine(_root, relative);
            foreach (string file in MNNPiper.RequiredDataFiles)
            {
                string target = Path.Combine(path, file);
                Directory.CreateDirectory(Path.GetDirectoryName(target));
                File.WriteAllText(target, "fixture");
            }
            return path;
        }
    }
}
