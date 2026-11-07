using System;
using System.Collections.Generic;
using System.IO;

namespace MNN.Unity
{
    public static class MNNAcousticModelResources
    {
        internal static void Release<T>(ref T model, ref List<Exception> errors) where T : class, IDisposable
        {
            if (model == null) return;
            try { model.Dispose(); model = null; }
            catch (Exception error) { (errors ?? (errors = new List<Exception>())).Add(error); }
        }
        internal static void ThrowIfFailed(List<Exception> errors)
        {
            if (errors != null) throw new AggregateException("Acoustic model cleanup failed; failed owners are retained for retry.", errors);
        }
        public static string ResolvePath(string root, string path)
        {
            if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("Choose an installed acoustic model directory.");
            if (!Path.IsPathRooted(path) && string.IsNullOrWhiteSpace(root))
                throw new ArgumentException("Choose a shared Models directory to resolve relative acoustic paths.");
            return Path.GetFullPath(Path.IsPathRooted(path) ? path : Path.Combine(root, path));
        }
        public static string Resolve(string root, string path)
        {
            string result = ResolvePath(root, path);
            if (!Directory.Exists(result)) throw new DirectoryNotFoundException("Install the acoustic model before starting: " + result);
            return result;
        }
        public static void DisposeAll(params IDisposable[] models)
        {
            List<Exception> failures = null;
            foreach (var model in models)
            {
                try { model?.Dispose(); }
                catch (Exception error) { (failures ?? (failures = new List<Exception>())).Add(error); }
            }
            if (failures != null) throw new AggregateException("Acoustic model cleanup failed.", failures);
        }
    }
}
