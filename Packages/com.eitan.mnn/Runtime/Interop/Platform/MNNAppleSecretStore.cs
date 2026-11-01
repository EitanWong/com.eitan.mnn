#if UNITY_EDITOR_OSX || UNITY_STANDALONE_OSX || (UNITY_IOS && !UNITY_EDITOR)
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using MNN.Unity.Interop;

namespace MNN.Unity
{
    /// <summary>Official Apple Security/CoreFoundation C APIs. No custom native plugin or C++ bridge.</summary>
    internal sealed class MNNAppleSecretStore : IMNNSecretStore
    {
        private readonly string service;
        public string Protection => "Apple Keychain / ThisDeviceOnly";
        public bool CanPersist => true;
        public MNNAppleSecretStore(string service) { this.service = service; }

        private static IntPtr Constant(string name)
        {
            return MNNInterop.AppleConstant(name);
        }

        // Dictionaries borrow values; each query explicitly owns its created CF objects.
        // Cleanup never resolves symbols or throws while handling another failure.
        private sealed class QueryResources : IDisposable
        {
            internal readonly IntPtr Dictionary;
            private readonly List<IntPtr> owned = new List<IntPtr>();
            internal QueryResources()
            {
                Dictionary = MNNInterop.AppleCFDictionaryCreateMutable(IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero);
                if (Dictionary == IntPtr.Zero) throw new OutOfMemoryException();
            }
            internal void Set(string key, IntPtr value) => MNNInterop.AppleCFDictionarySetValue(Dictionary, Constant(key), value);
            internal void SetString(string key, string value)
            {
                var text = MNNInterop.AppleCFStringCreateWithCString(IntPtr.Zero, value, 0x08000100);
                if (text == IntPtr.Zero) throw new OutOfMemoryException();
                owned.Add(text);
                Set(key, text);
            }
            public void Dispose()
            {
                MNNInterop.AppleCFRelease(Dictionary);
                foreach (var value in owned) MNNInterop.AppleCFRelease(value);
            }
        }
        private QueryResources Query(string alias)
        {
            MNNSecretStore.AliasHash(alias);
            var query = new QueryResources();
            try
            {
                query.Set("kSecClass", Constant("kSecClassGenericPassword"));
                query.SetString("kSecAttrService", service);
                query.SetString("kSecAttrAccount", alias);
                return query;
            }
            catch { query.Dispose(); throw; }
        }

        public string Read(string alias)
        {
            using (var query = Query(alias))
            {
            IntPtr result = IntPtr.Zero;
            try
            {
                query.Set("kSecReturnData", Constant("kCFBooleanTrue"));
                int status = MNNInterop.AppleSecItemCopyMatching(query.Dictionary, out result);
                if (status == -25300) return null;
                Check(status);
                long length = MNNInterop.AppleCFDataGetLength(result).ToInt64();
                if (length < 0 || length > 65536) throw new InvalidOperationException("Invalid Keychain credential length.");
                var data = new byte[(int)length];
                Marshal.Copy(MNNInterop.AppleCFDataGetBytePtr(result), data, 0, data.Length);
                try { return Encoding.UTF8.GetString(data); }
                finally { Array.Clear(data, 0, data.Length); }
            }
            finally { if (result != IntPtr.Zero) MNNInterop.AppleCFRelease(result); }
            }
        }

        public void Write(string alias, string secret)
        {
            if (secret == null || secret.Length > 16384) throw new ArgumentException("Invalid credential.");
            using (var query = Query(alias))
            using (var update = new QueryResources())
            {
                byte[] bytes = Encoding.UTF8.GetBytes(secret);
                IntPtr data;
                try { data = MNNInterop.AppleCFDataCreate(IntPtr.Zero, bytes, (IntPtr)bytes.Length); }
                finally { Array.Clear(bytes, 0, bytes.Length); }
                if (data == IntPtr.Zero) throw new OutOfMemoryException();
                try
                {
                    update.Set("kSecValueData", data);
                    update.Set("kSecAttrAccessible", Constant("kSecAttrAccessibleAfterFirstUnlockThisDeviceOnly"));
                    int status = MNNInterop.AppleSecItemUpdate(query.Dictionary, update.Dictionary);
                    if (status == -25300)
                    {
                        query.Set("kSecValueData", data);
                        query.Set("kSecAttrAccessible", Constant("kSecAttrAccessibleAfterFirstUnlockThisDeviceOnly"));
                        status = MNNInterop.AppleSecItemAdd(query.Dictionary, IntPtr.Zero);
                    }
                    Check(status);
                }
                finally { MNNInterop.AppleCFRelease(data); }
            }
        }
        public void Delete(string alias)
        {
            using (var query = Query(alias))
            {
                int status = MNNInterop.AppleSecItemDelete(query.Dictionary);
                if (status != -25300) Check(status);
            }
        }

        private static void Check(int status) { if (status != 0) throw new InvalidOperationException("Apple Keychain operation failed (OSStatus " + status + ")."); }
    }
}
#endif
