#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using MNN.Unity.Interop;

namespace MNN.Unity
{
    internal sealed class MNNWindowsSecretStore : IMNNSecretStore
    {
        private readonly string directory;
        public string Protection => "Windows DPAPI / current user";
        public bool CanPersist => true;
        public MNNWindowsSecretStore(string directory) { this.directory = directory; }
        private string PathFor(string alias) => Path.Combine(directory, MNNSecretStore.AliasHash(alias) + ".dpapi");
        public string Read(string alias)
        {
            if (!File.Exists(PathFor(alias))) return null;
            byte[] clear = Transform(File.ReadAllBytes(PathFor(alias)), false);
            try { return Encoding.UTF8.GetString(clear); }
            finally { Array.Clear(clear, 0, clear.Length); }
        }
        public void Write(string alias, string secret)
        {
            if (secret == null || secret.Length > 16384) throw new ArgumentException("Invalid credential.");
            byte[] clear = Encoding.UTF8.GetBytes(secret);
            try { MNNSecretStore.WriteAtomic(PathFor(alias), Transform(clear, true)); }
            finally { Array.Clear(clear, 0, clear.Length); }
        }
        public void Delete(string alias) { if (File.Exists(PathFor(alias))) File.Delete(PathFor(alias)); }
        private static byte[] Transform(byte[] bytes, bool protect)
        {
            if (bytes.Length > 131072) throw new InvalidOperationException("Credential too large.");
            var input = new MNNInterop.DataBlob { Length = bytes.Length, Data = Marshal.AllocHGlobal(bytes.Length) };
            var output = new MNNInterop.DataBlob();
            try
            {
                Marshal.Copy(bytes, 0, input.Data, bytes.Length);
                bool success = protect ? MNNInterop.WindowsCryptProtectData(ref input, out output) :
                    MNNInterop.WindowsCryptUnprotectData(ref input, out output);
                if (!success) throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
                if (output.Length < 0 || output.Length > 131072) throw new InvalidOperationException("Invalid protected credential size.");
                var result = new byte[output.Length];
                Marshal.Copy(output.Data, result, 0, result.Length);
                return result;
            }
            finally
            {
                for (int i = 0; i < bytes.Length; i++) Marshal.WriteByte(input.Data, i, 0);
                Marshal.FreeHGlobal(input.Data);
                if (output.Data != IntPtr.Zero)
                {
                    if (!protect && output.Length >= 0 && output.Length <= 131072)
                        for (int i = 0; i < output.Length; ++i) Marshal.WriteByte(output.Data, i, 0);
                    MNNInterop.WindowsLocalFree(output.Data);
                }
            }
        }
    }
}
#endif
