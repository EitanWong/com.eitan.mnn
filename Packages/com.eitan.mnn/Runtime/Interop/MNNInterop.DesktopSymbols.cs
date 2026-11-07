namespace MNN.Unity.Interop
{
    // Official core API symbols for the bundled, compiler-specific native libraries.
    // Windows MinGW and Linux GCC use GNU C++ mangling with platform data models.
    // These names do not establish a portable upstream C# interface.
    internal static class MNNCoreSymbols
    {
        internal const string Version = "_ZN3MNN10getVersionEv";
        internal const string InterpreterFromFile = "_ZN3MNN11Interpreter14createFromFileEPKc";
#if UNITY_EDITOR_WIN || (UNITY_STANDALONE_WIN && !UNITY_EDITOR)
        internal const string InterpreterFromBuffer = "_ZN3MNN11Interpreter16createFromBufferEPKvy";
        internal const string SetCacheFile = "_ZN3MNN11Interpreter12setCacheFileEPKcy";
        internal const string SetExternalFile = "_ZN3MNN11Interpreter15setExternalFileEPKcy";
#else
        internal const string InterpreterFromBuffer = "_ZN3MNN11Interpreter16createFromBufferEPKvm";
        internal const string SetCacheFile = "_ZN3MNN11Interpreter12setCacheFileEPKcm";
        internal const string SetExternalFile = "_ZN3MNN11Interpreter15setExternalFileEPKcm";
#endif
        internal const string InterpreterDestroy = "_ZN3MNN11Interpreter7destroyEPS0_";
        internal const string CreateSession = "_ZN3MNN11Interpreter13createSessionERKNS_14ScheduleConfigE";
        internal const string ReleaseSession = "_ZN3MNN11Interpreter14releaseSessionEPNS_7SessionE";
        internal const string RunSession = "_ZNK3MNN11Interpreter10runSessionEPNS_7SessionE";
        internal const string ReleaseModel = "_ZN3MNN11Interpreter12releaseModelEv";
        internal const string GetSessionInput = "_ZN3MNN11Interpreter15getSessionInputEPKNS_7SessionEPKc";
        internal const string GetSessionOutput = "_ZN3MNN11Interpreter16getSessionOutputEPKNS_7SessionEPKc";
        internal const string ResizeSession = "_ZN3MNN11Interpreter13resizeSessionEPNS_7SessionE";
        internal const string UpdateCacheFile = "_ZN3MNN11Interpreter15updateCacheFileEPNS_7SessionEi";
        internal const string GetSessionBackendInfo = "_ZN3MNN11Interpreter14getSessionInfoEPKNS_7SessionENS0_15SessionInfoCodeEPv";
        internal const string SetSessionHint = "_ZN3MNN11Interpreter14setSessionHintENS0_8HintModeEi";
        internal const string TensorMap = "_ZN3MNN6Tensor3mapENS0_7MapTypeENS0_13DimensionTypeE";
        internal const string TensorUnmap = "_ZN3MNN6Tensor5unmapENS0_7MapTypeENS0_13DimensionTypeEPv";
        internal const string TensorDimensionType = "_ZNK3MNN6Tensor16getDimensionTypeEv";
        internal const string TensorType = "_ZNK3MNN6Tensor7getTypeEv";
        internal const string TensorDimensions = "_ZNK3MNN6Tensor10dimensionsEv";
        internal const string TensorLength = "_ZNK3MNN6Tensor6lengthEi";
        internal const string TensorElementSize = "_ZNK3MNN6Tensor11elementSizeEv";
        internal const string TensorSize = "_ZNK3MNN6Tensor4sizeEv";
        internal const string TensorDestroy = "_ZN3MNN6Tensor7destroyEPS0_";
        internal const string TensorWait = "_ZN3MNN6Tensor4waitENS0_7MapTypeEb";
#if UNITY_EDITOR_WIN || UNITY_EDITOR_LINUX || ((UNITY_STANDALONE_WIN || UNITY_STANDALONE_LINUX) && !UNITY_EDITOR)
        internal const string ResizeTensor = "_ZN3MNN11Interpreter12resizeTensorEPNS_6TensorERKSt6vectorIiSaIiEE";
#elif UNITY_ANDROID && !UNITY_EDITOR
        internal const string ResizeTensor = "_ZN3MNN11Interpreter12resizeTensorEPNS_6TensorERKNSt6__ndk16vectorIiNS3_9allocatorIiEEEE";
#else
        internal const string ResizeTensor = "_ZN3MNN11Interpreter12resizeTensorEPNS_6TensorERKNSt3__16vectorIiNS3_9allocatorIiEEEE";
#endif
    }
}
