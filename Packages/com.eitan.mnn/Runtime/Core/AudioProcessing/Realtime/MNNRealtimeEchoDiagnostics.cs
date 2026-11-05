using System;

namespace MNN.Unity
{
    /// <summary>Optional public object-shaped APM diagnostics adapter. Call on the control thread and publish
    /// the value result through MNNRealtimeEchoHealth; never reflect or box plugin values on PCM callbacks.</summary>
    public static class MNNRealtimeEchoDiagnostics
    {
        public static bool IsQualified(bool recording, bool requested, object diagnostics)
        {
            if (!recording || diagnostics == null) return false;
            object native = PublicValue(diagnostics, "NativeDiagnostics");
            if (native == null) return false;
            if (PublicBool(native, "HardwareAecEnabled") || PublicBool(native, "PlatformVoiceProcessingEnabled")) return true;
            return requested && PublicBool(native, "SoftwareAecEnabled") && PublicFalse(native, "SoftwareAecBypassed") &&
                PublicFalse(native, "ReverseReferenceMissing") && PublicBool(native, "ReverseStreamHealthy") && PublicBool(native, "RenderReferenceMatchesOutput");
        }
        public static object PublicValue(object value, string name)
        {
            if (value == null) return null;
            var type = value.GetType(); return type.GetProperty(name)?.GetValue(value) ?? type.GetField(name)?.GetValue(value);
        }
        public static bool PublicBool(object value, string name) => PublicValue(value, name) is bool flag && flag;
        public static bool PublicFalse(object value, string name) => PublicValue(value, name) is bool flag && !flag;
    }
}
