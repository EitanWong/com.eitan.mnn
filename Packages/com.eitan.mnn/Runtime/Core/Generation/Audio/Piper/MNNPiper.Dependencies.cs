using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;

namespace MNN.Unity
{
    public sealed partial class MNNPiper
    {
        internal static string PhonemizerUnsupportedReason(UnityEngine.RuntimePlatform platform)
        {
            switch (platform)
            {
                case UnityEngine.RuntimePlatform.Android:
                case UnityEngine.RuntimePlatform.IPhonePlayer:
                case UnityEngine.RuntimePlatform.WebGLPlayer:
                    return "Piper's current eSpeak-NG command-line backend is unavailable on " + platform +
                        ". This platform needs an in-process phonemizer backend; specifying executable or data paths cannot enable it.";
                default:
                    return null;
            }
        }

        internal static readonly string[] RequiredDataFiles = {"phontab", "phondata", "phonindex", "intonations", "en_dict", "lang/gmw/en"};
        public static string ResolveDataDirectory(string directory, string configured = null, string selected = null, bool requireEnglish = true)
        {
            var required = requireEnglish ? RequiredDataFiles : RequiredDataFiles.Take(4).ToArray();
            if (!string.IsNullOrWhiteSpace(selected))
            {
                if (Directory.Exists(selected) && required.All(file => File.Exists(Path.Combine(selected, file))))
                    return Path.GetFullPath(selected);
                throw new DirectoryNotFoundException("Choose the complete espeak-ng-data folder containing phontab, phondata, phonindex, intonations and English language data.");
            }

            var candidates = new[]{string.IsNullOrWhiteSpace(configured) ? null : Path.Combine(directory, configured),
                string.IsNullOrWhiteSpace(configured) ? null : Path.Combine(directory, configured, "espeak-ng-data"), Path.Combine(directory, "espeak-ng-data")};
            foreach (string path in candidates.Where(p => !string.IsNullOrWhiteSpace(p)).Distinct())
                if (Directory.Exists(path) && required.All(file => File.Exists(Path.Combine(path, file))))
                    return Path.GetFullPath(path);
            foreach (string path in DependencyDirectories(directory))
                if (string.Equals(Path.GetFileName(path), "espeak-ng-data", StringComparison.OrdinalIgnoreCase) &&
                    required.All(file => File.Exists(Path.Combine(path, file))))
                    return path;
            throw new FileNotFoundException("Piper's espeak-ng-data is missing or incomplete. Download the complete model repository; the data folder is found automatically.", Path.Combine(directory, "espeak-ng-data"));
        }

        internal static string FindVoiceMetadata(string directory, string voice)
        {
            foreach (string folder in new[]{Path.Combine(directory, "assets"), directory}.Concat(DependencyDirectories(directory, skipData: true)).Distinct())
                foreach (string name in new[]{voice + ".onnx.json", voice + ".mnn.json"})
                {
                    string path = Path.Combine(folder, name);
                    if (File.Exists(path)) return path;
                }
            return null;
        }

        public static string FindEspeak(string directory = null)
        {
            if (!string.IsNullOrWhiteSpace(directory))
            {
                foreach (string relative in new[]{"run-espeak-ng.sh", "espeak-ng", "bin/run-espeak-ng.sh", "bin/espeak-ng", "espeak-ng.exe", "bin/espeak-ng.exe"})
                    if (IsEspeakExecutable(Path.Combine(directory, relative)))
                        return Path.GetFullPath(Path.Combine(directory, relative));
                foreach (string folder in DependencyDirectories(directory, skipData: true))
                    foreach (string name in new[]{"run-espeak-ng.sh", "espeak-ng", "espeak-ng.exe"})
                    {
                        string path = Path.Combine(folder, name);
                        if (IsEspeakExecutable(path))
                            return path;
                    }
            }
            string env = Environment.GetEnvironmentVariable("MNN_ESPEAK_NG_PATH");
            if (IsEspeakExecutable(env))
                return Path.GetFullPath(env);
#if UNITY_EDITOR
            // This belongs in the loader: Editor callers, including background
            // sample providers, must resolve the same existing project tools.
            foreach (string project in ProjectDirectories(directory))
                foreach (string relative in new[]{"TestArtifacts~/Dependencies/espeak-ng/run-espeak-ng.sh", "TestArtifacts~/GenerationImplementation/Dependencies/espeak-ng/run-espeak-ng.sh"})
                {
                    string path = Path.Combine(project, relative);
                    if (IsEspeakExecutable(path))
                        return Path.GetFullPath(path);
                }
#endif
            foreach (string path in EspeakInstallLocations())
                if (IsEspeakExecutable(path))
                    return path;
            foreach (string folder in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator).Where(p => !string.IsNullOrWhiteSpace(p)))
                foreach (string name in new[]{"espeak-ng", "espeak-ng.exe"})
                    if (IsEspeakExecutable(Path.Combine(folder, name)))
                        return Path.GetFullPath(Path.Combine(folder, name));
            return null;
        }

        private static IEnumerable<string> EspeakInstallLocations()
        {
            foreach (string path in new[]{"/opt/homebrew/bin/espeak-ng", "/usr/local/bin/espeak-ng", "/usr/bin/espeak-ng", "/bin/espeak-ng", "/snap/bin/espeak-ng"})
                yield return path;
            foreach (var folder in new[]{Environment.SpecialFolder.ProgramFiles, Environment.SpecialFolder.ProgramFilesX86, Environment.SpecialFolder.LocalApplicationData})
            {
                string root = Environment.GetFolderPath(folder);
                if (string.IsNullOrWhiteSpace(root))
                    continue;
                yield return Path.Combine(root, "eSpeak NG", "espeak-ng.exe");
                yield return Path.Combine(root, "Programs", "eSpeak NG", "espeak-ng.exe");
            }
        }

        private static IEnumerable<string> DependencyDirectories(string directory, bool skipData = false)
        {
            if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
                yield break;
            // Search the model tree only, nearest folders first, in a stable
            // order. Do not follow directory links into cycles or other models.
            var pending = new Queue<string>();
            pending.Enqueue(Path.GetFullPath(directory));
            while (pending.Count != 0)
            {
                string current = pending.Dequeue();
                yield return current;
                string[] children;
                try
                {
                    children = Directory.GetDirectories(current);
                }
                catch (Exception error) when (error is IOException || error is UnauthorizedAccessException)
                {
                    continue;
                }
                Array.Sort(children, StringComparer.Ordinal);
                foreach (string child in children)
                {
                    string name = Path.GetFileName(child);
                    if (name.StartsWith(".", StringComparison.Ordinal) ||
                        (skipData && string.Equals(name, "espeak-ng-data", StringComparison.OrdinalIgnoreCase)))
                        continue;
                    try
                    {
                        if ((File.GetAttributes(child) & FileAttributes.ReparsePoint) != 0)
                            continue;
                    }
                    catch (Exception error) when (error is IOException || error is UnauthorizedAccessException)
                    {
                        continue;
                    }
                    pending.Enqueue(child);
                }
            }
        }

#if UNITY_EDITOR
        private static IEnumerable<string> ProjectDirectories(string directory)
        {
            if (!string.IsNullOrWhiteSpace(directory))
                for (var current = new DirectoryInfo(directory); current != null; current = current.Parent)
                    if (current.Name == "Assets" && current.Parent != null)
                    {
                        yield return current.Parent.FullName;
                        break;
                    }
            // Unity's working directory is the project root, even when models
            // live in persistentDataPath. No main-thread Unity API is needed.
            yield return Directory.GetCurrentDirectory();
            if (!string.IsNullOrWhiteSpace(directory))
                for (var current = new DirectoryInfo(directory); current != null; current = current.Parent)
                    if (current.Name == "TestArtifacts~" && current.Parent != null)
                        yield return current.Parent.FullName;
        }
#endif

        public static bool IsEspeakExecutable(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                return false;
            try
            {
                // Dictionaries and language data must never become process targets,
                // even if an older Studio preference or environment variable names one.
                string full = Path.GetFullPath(path);
                if (full.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar).Any(part => string.Equals(part, "espeak-ng-data", StringComparison.OrdinalIgnoreCase)))
                    return false;
                using (var stream = File.OpenRead(full))
                {
                    var header = new byte[4];
                    if (stream.Read(header, 0, header.Length) != header.Length)
                        return false;
                    var platform = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? UnityEngine.RuntimePlatform.WindowsPlayer :
                        RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? UnityEngine.RuntimePlatform.OSXPlayer : UnityEngine.RuntimePlatform.LinuxPlayer;
                    return IsEspeakHeaderForPlatform(header, platform);
                }
            }
            catch (Exception error)when (error is IOException || error is UnauthorizedAccessException || error is ArgumentException || error is NotSupportedException)
            {
                return false;
            }
        }

        internal static bool IsEspeakHeaderForPlatform(byte[] header, UnityEngine.RuntimePlatform platform)
        {
            if (header == null || header.Length < 4 || PhonemizerUnsupportedReason(platform) != null)
                return false;
            bool windows = platform == UnityEngine.RuntimePlatform.WindowsPlayer || platform == UnityEngine.RuntimePlatform.WindowsEditor;
            if (windows)
                return header[0] == 'M' && header[1] == 'Z';
            if (header[0] == '#' && header[1] == '!')
                return true;
            bool linux = platform == UnityEngine.RuntimePlatform.LinuxPlayer || platform == UnityEngine.RuntimePlatform.LinuxEditor;
            if (linux)
                return header[0] == 0x7f && header[1] == 'E' && header[2] == 'L' && header[3] == 'F';
            uint magic = ((uint)header[0] << 24) | ((uint)header[1] << 16) | ((uint)header[2] << 8) | header[3];
            return magic == 0xfeedface || magic == 0xcefaedfe || magic == 0xfeedfacf || magic == 0xcffaedfe || magic == 0xcafebabe || magic == 0xbebafeca || magic == 0xcafebabf || magic == 0xbfbafeca;
        }

        public static string ResolveEspeak(string requested, string directory = null)
        {
            if (!string.IsNullOrWhiteSpace(requested))
            {
                if (Directory.Exists(requested))
                    throw new ArgumentException("Select the eSpeak-NG executable file. The model's espeak-ng-data folder is found automatically.", nameof(requested));
                if (!File.Exists(requested))
                    throw new FileNotFoundException("The selected eSpeak-NG executable does not exist.", requested);
                if (!IsEspeakExecutable(requested))
                    throw new ArgumentException("The eSpeak-NG tool path must name a program for the current OS (or a supported script wrapper), not an espeak-ng-data dictionary or language file. Omit it to use automatic discovery.", nameof(requested));
                return Path.GetFullPath(requested);
            }

            return FindEspeak(directory) ?? throw new FileNotFoundException("Piper's data folder was found, but no eSpeak-NG executable was found in the model tree, environment, installed tools or existing project dependencies. Supply espeakExecutable or install eSpeak-NG.");
        }
    }
}
