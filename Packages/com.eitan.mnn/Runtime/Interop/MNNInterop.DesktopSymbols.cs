namespace MNN.Unity.Interop
{
    // Official core API symbols for the bundled, compiler-specific native libraries.
    // Windows uses the MSVC x64 C++ ABI; Linux uses the GNU Itanium C++ ABI.
    // These names do not establish a portable upstream C# interface.
    internal static class MNNCoreSymbols
    {
#if UNITY_EDITOR_WIN || (UNITY_STANDALONE_WIN && !UNITY_EDITOR)
        internal const string Version = "?getVersion@MNN@@YAPEBDXZ";
        internal const string InterpreterFromFile = "?createFromFile@Interpreter@MNN@@SAPEAV12@PEBD@Z";
        internal const string InterpreterFromBuffer = "?createFromBuffer@Interpreter@MNN@@SAPEAV12@PEBX_K@Z";
        internal const string InterpreterDestroy = "?destroy@Interpreter@MNN@@SAXPEAV12@@Z";
        internal const string CreateSession = "?createSession@Interpreter@MNN@@QEAAPEAVSession@2@AEBUScheduleConfig@2@@Z";
        internal const string ReleaseSession = "?releaseSession@Interpreter@MNN@@QEAA_NPEAVSession@2@@Z";
        internal const string RunSession = "?runSession@Interpreter@MNN@@QEBA?AW4ErrorCode@2@PEAVSession@2@@Z";
        internal const string ReleaseModel = "?releaseModel@Interpreter@MNN@@QEAAXXZ";
        internal const string GetSessionInput = "?getSessionInput@Interpreter@MNN@@QEAAPEAVTensor@2@PEBVSession@2@PEBD@Z";
        internal const string GetSessionOutput = "?getSessionOutput@Interpreter@MNN@@QEAAPEAVTensor@2@PEBVSession@2@PEBD@Z";
        internal const string ResizeSession = "?resizeSession@Interpreter@MNN@@QEAAXPEAVSession@2@@Z";
        internal const string SetCacheFile = "?setCacheFile@Interpreter@MNN@@QEAAXPEBD_K@Z";
        internal const string SetExternalFile = "?setExternalFile@Interpreter@MNN@@QEAAXPEBD_K@Z";
        internal const string UpdateCacheFile = "?updateCacheFile@Interpreter@MNN@@QEAA?AW4ErrorCode@2@PEAVSession@2@H@Z";
        internal const string GetSessionBackendInfo = "?getSessionInfo@Interpreter@MNN@@QEAA_NPEBVSession@2@W4SessionInfoCode@12@PEAX@Z";
        internal const string SetSessionHint = "?setSessionHint@Interpreter@MNN@@QEAAXW4HintMode@12@H@Z";
        internal const string TensorMap = "?map@Tensor@MNN@@QEAAPEAXW4MapType@12@W4DimensionType@12@@Z";
        internal const string TensorUnmap = "?unmap@Tensor@MNN@@QEAAXW4MapType@12@W4DimensionType@12@PEAX@Z";
        internal const string TensorDimensionType = "?getDimensionType@Tensor@MNN@@QEBA?AW4DimensionType@12@XZ";
        internal const string TensorType = "?getType@Tensor@MNN@@QEBA?AUhalide_type_t@@XZ";
        internal const string TensorDimensions = "?dimensions@Tensor@MNN@@QEBAHXZ";
        internal const string TensorLength = "?length@Tensor@MNN@@QEBAHH@Z";
        internal const string TensorElementSize = "?elementSize@Tensor@MNN@@QEBAHXZ";
        internal const string TensorSize = "?size@Tensor@MNN@@QEBAHXZ";
        internal const string TensorDestroy = "?destroy@Tensor@MNN@@SAXPEAV12@@Z";
        internal const string TensorWait = "?wait@Tensor@MNN@@QEAAHW4MapType@12@_N@Z";
        internal const string ResizeTensor = "?resizeTensor@Interpreter@MNN@@QEAAXPEAVTensor@2@AEBV?$vector@HV?$allocator@H@std@@@std@@@Z";
#else
        internal const string Version = "_ZN3MNN10getVersionEv";
        internal const string InterpreterFromFile = "_ZN3MNN11Interpreter14createFromFileEPKc";
        internal const string InterpreterFromBuffer = "_ZN3MNN11Interpreter16createFromBufferEPKvm";
        internal const string InterpreterDestroy = "_ZN3MNN11Interpreter7destroyEPS0_";
        internal const string CreateSession = "_ZN3MNN11Interpreter13createSessionERKNS_14ScheduleConfigE";
        internal const string ReleaseSession = "_ZN3MNN11Interpreter14releaseSessionEPNS_7SessionE";
        internal const string RunSession = "_ZNK3MNN11Interpreter10runSessionEPNS_7SessionE";
        internal const string ReleaseModel = "_ZN3MNN11Interpreter12releaseModelEv";
        internal const string GetSessionInput = "_ZN3MNN11Interpreter15getSessionInputEPKNS_7SessionEPKc";
        internal const string GetSessionOutput = "_ZN3MNN11Interpreter16getSessionOutputEPKNS_7SessionEPKc";
        internal const string ResizeSession = "_ZN3MNN11Interpreter13resizeSessionEPNS_7SessionE";
        internal const string SetCacheFile = "_ZN3MNN11Interpreter12setCacheFileEPKcm";
        internal const string SetExternalFile = "_ZN3MNN11Interpreter15setExternalFileEPKcm";
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
#if UNITY_EDITOR_LINUX || (UNITY_STANDALONE_LINUX && !UNITY_EDITOR)
        internal const string ResizeTensor = "_ZN3MNN11Interpreter12resizeTensorEPNS_6TensorERKSt6vectorIiSaIiEE";
#elif UNITY_ANDROID && !UNITY_EDITOR
        internal const string ResizeTensor = "_ZN3MNN11Interpreter12resizeTensorEPNS_6TensorERKNSt6__ndk16vectorIiNS3_9allocatorIiEEEE";
#else
        internal const string ResizeTensor = "_ZN3MNN11Interpreter12resizeTensorEPNS_6TensorERKNSt3__16vectorIiNS3_9allocatorIiEEEE";
#endif
#endif
    }
}
