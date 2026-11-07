using System;
using System.Diagnostics;
using System.Globalization;
using System.Net.NetworkInformation;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Profiling;

namespace MNN.Unity
{
    /// <summary>Main-thread Unity sampling; network enumeration runs independently without probing servers.</summary>
    public sealed partial class MNNSystemDiagnostics : IDisposable
    {
        public const string Unavailable = "Unavailable · platform / runtime does not expose this metric";
        public string Performance { get; private set; } = "Collecting a one-second frame window…";
        public string Memory { get; private set; }
        public string Device { get; }
        public string Graphics { get; }
        public string Display { get; private set; }
        public string ApplicationInfo { get; private set; }
        public string Network => "Reachability: " + Application.internetReachability +
            "\nA reachable interface does not verify internet or API availability.\n" + Volatile.Read(ref _network);
        public string SampleTime { get; private set; }
        private string _network = "Collecting interfaces…";
        private Process _process;
        private double _previousCpu, _previousTime, _nextSample, _nextNetwork;
        private int _frames, _gc0, _gc1, _gc2;
        private double _frameSeconds, _worstFrame;
        private static readonly string[] ByteUnits = { "B", "KiB", "MiB", "GiB", "TiB" };
        private readonly double[] _frameTimes = new double[2048];
        private readonly double[] _sortedTimes = new double[2048];
        private int _storedFrames, _frameIndex;
        private readonly FrameTiming[] _timing = new FrameTiming[1];
        private Task _networkTask;
        private long _received, _sent;
        private string _interfaceKey;
        private double _networkTime;
        private bool _disposed;

        public MNNSystemDiagnostics()
        {
            Device = "Device: " + SystemInfo.deviceName + "\nModel: " + SystemInfo.deviceModel +
                "\nType: " + SystemInfo.deviceType + "\nOS: " + SystemInfo.operatingSystem +
                "\nCPU: " + SystemInfo.processorType + "\nLogical cores: " + SystemInfo.processorCount +
                "\nReported CPU frequency: " + (SystemInfo.processorFrequency > 0 ? SystemInfo.processorFrequency + " MHz" : Unavailable) +
                "\nProcess: " + (IntPtr.Size * 8) + " bit\nPhysical core count / per-core load / CPU temperature: " + Unavailable;
            Graphics = "GPU: " + SystemInfo.graphicsDeviceName + "\nVendor: " + SystemInfo.graphicsDeviceVendor +
                "\nRender API: " + SystemInfo.graphicsDeviceType + "\nDriver: " + SystemInfo.graphicsDeviceVersion +
                "\nDevice/vendor ID: " + SystemInfo.graphicsDeviceID.ToString("X4") + " / " + SystemInfo.graphicsDeviceVendorID.ToString("X4") +
                "\nReported graphics memory: " + Megabytes(SystemInfo.graphicsMemorySize) +
                "\nThis is reported capacity; integrated GPUs may share system RAM.\nShader level: " + SystemInfo.graphicsShaderLevel +
                "\nCompute shaders: " + SystemInfo.supportsComputeShaders + " · instancing: " + SystemInfo.supportsInstancing +
                "\nMultithreaded rendering: " + SystemInfo.graphicsMultiThreaded + "\nMax texture: " + SystemInfo.maxTextureSize +
                "\nDevice-wide GPU load / total VRAM in use: " + Unavailable;
#if !UNITY_WEBGL || UNITY_EDITOR
            try { _process = Process.GetCurrentProcess(); }
            catch (Exception) { /* The OS/runtime may deny process counters. */ }
#endif
            _gc0 = GC.CollectionCount(0); _gc1 = GC.CollectionCount(1); _gc2 = GC.CollectionCount(2);
            double now = Time.realtimeSinceStartupAsDouble;
            Sample(now);
            _nextSample = now + 1;
        }

        public bool Tick(float delta, double now)
        {
            if (_disposed) return false;
            if (delta > 0 && !float.IsNaN(delta) && !float.IsInfinity(delta))
            {
                ++_frames; _frameSeconds += delta; _worstFrame = Math.Max(_worstFrame, delta);
                _frameTimes[_frameIndex] = delta;
                _frameIndex = (_frameIndex + 1) % _frameTimes.Length;
                _storedFrames = Math.Min(_storedFrames + 1, _frameTimes.Length);
            }
            FrameTimingManager.CaptureFrameTimings();
            if (now < _nextSample) return false;
            Sample(now);
            _nextSample = now + 1;
            return true;
        }

        private void Sample(double now)
        {
            SampleTime = DateTimeOffset.Now.ToString("yyyy-MM-dd HH:mm:ss zzz", CultureInfo.InvariantCulture);
            string processCpu = Unavailable, resident = Unavailable;
            if (_process != null)
            {
                try
                {
                    _process.Refresh();
                    double cpu = _process.TotalProcessorTime.TotalSeconds;
                    if (_previousTime > 0 && now > _previousTime && now - _previousTime < 5)
                        processCpu = Percent((cpu - _previousCpu) / (now - _previousTime) / Math.Max(1, SystemInfo.processorCount)) +
                            " of all logical cores";
                    else processCpu = "Collecting CPU time delta…";
                    _previousCpu = cpu; _previousTime = now;
                    resident = MeasuredBytes(_process.WorkingSet64);
                }
                catch (Exception) { _process.Dispose(); _process = null; }
            }
            string frame = "Collecting a one-second frame window…";
            if (_frames > 0 && _frameSeconds > 0)
            {
                Array.Copy(_frameTimes, _sortedTimes, _storedFrames);
                Array.Sort(_sortedTimes, 0, _storedFrames);
                double p95 = _sortedTimes[Math.Max(0, (int)Math.Ceiling(_storedFrames * .95) - 1)];
                frame = F(_frames / _frameSeconds) + " FPS · " + F(1000 * _frameSeconds / _frames) + " ms average\n" +
                    "P95 frame: " + F(p95 * 1000) + " ms · worst: " + F(_worstFrame * 1000) + " ms\n" +
                    _frames + " frames in " + F(_frameSeconds) + " s (unscaled; P95 retains up to 2048 frames per window)";
            }
            string timings = "CPU/GPU frame timings: unavailable · enable Frame Timing Stats in Player settings and use a supported graphics API";
            if (FrameTimingManager.GetLatestTimings(1, _timing) > 0)
                timings = "CPU frame: " + Milliseconds(_timing[0].cpuFrameTime) + " · GPU frame: " + Milliseconds(_timing[0].gpuFrameTime);
            Performance = frame + "\n" + timings + "\nApp CPU: " + processCpu + "\nSystem CPU: " + ReadSystemCpu() +
                "\nTarget FPS: " + (Application.targetFrameRate > 0 ? Application.targetFrameRate.ToString() : "Platform default") +
                " · VSync: " + QualitySettings.vSyncCount;
            _frames = _storedFrames = _frameIndex = 0; _frameSeconds = _worstFrame = 0;
            long allocated = Profiler.GetTotalAllocatedMemoryLong(), reserved = Profiler.GetTotalReservedMemoryLong();
            long gfx = Profiler.GetAllocatedMemoryForGraphicsDriver();
            int gc0 = GC.CollectionCount(0), gc1 = GC.CollectionCount(1), gc2 = GC.CollectionCount(2);
            Memory = "System RAM (reported capacity): " + Megabytes(SystemInfo.systemMemorySize) +
                "\nSystem memory: " + ReadSystemMemory() + "\nApp resident set (RSS): " + resident +
                "\nUnity tracked allocated: " + MeasuredBytes(allocated) + "\nUnity tracked reserved: " + MeasuredBytes(reserved) +
                "\nUnity unused reserved: " + MeasuredBytes(Profiler.GetTotalUnusedReservedMemoryLong()) +
                "\nUnity managed heap used / reserved: " + MeasuredBytes(Profiler.GetMonoUsedSizeLong()) + " / " + MeasuredBytes(Profiler.GetMonoHeapSizeLong()) +
                "\nGC managed bytes: " + Bytes(GC.GetTotalMemory(false)) +
                "\nGC collections since last sample (gen 0/1/2): " + (gc0 - _gc0) + "/" + (gc1 - _gc1) + "/" + (gc2 - _gc2) +
                "\nUnity graphics driver allocation estimate: " + MeasuredBytes(gfx) +
                "\nUnity counters cover tracked allocations, not every native/model allocation. Graphics estimate is not total device VRAM usage.";
            _gc0 = gc0; _gc1 = gc1; _gc2 = gc2;
            Display = "App render size: " + Screen.width + " × " + Screen.height +
                "\nDisplay mode: " + Screen.currentResolution + "\nWindow mode: " + Screen.fullScreenMode +
                "\nOrientation: " + Screen.orientation + "\nDPI: " + (Screen.dpi > 0 ? F(Screen.dpi) : Unavailable) +
                "\nSafe area: " + Screen.safeArea + "\nRender scale: " + F(ScalableBufferManager.widthScaleFactor) + " × " + F(ScalableBufferManager.heightScaleFactor) +
                "\nColor space: " + QualitySettings.activeColorSpace + "\nQuality: " + QualitySettings.names[QualitySettings.GetQualityLevel()];
            ApplicationInfo = "Application: " + Application.productName + " " + Application.version +
                "\nIdentifier: " + Application.identifier + "\nUnity: " + Application.unityVersion + "\nPlatform: " + Application.platform +
                "\nScripting runtime: " + ScriptingRuntime + "\nEditor: " + Application.isEditor + " · development build: " + UnityEngine.Debug.isDebugBuild +
                "\nFocused: " + Application.isFocused + " · run in background: " + Application.runInBackground +
                "\nUptime: " + TimeSpan.FromSeconds(Time.realtimeSinceStartupAsDouble).ToString(@"d\.hh\:mm\:ss") +
                "\nTime scale: " + F(Time.timeScale) + "\nLanguage: " + Application.systemLanguage +
                "\nBattery: " + (SystemInfo.batteryLevel >= 0 ? Percent(SystemInfo.batteryLevel) : Unavailable) + " · " + SystemInfo.batteryStatus +
                "\nAudio: " + AudioSettings.outputSampleRate + " Hz · " + AudioSettings.speakerMode;
            if (now >= _nextNetwork && (_networkTask == null || _networkTask.IsCompleted))
            {
                _nextNetwork = now + 5;
                _networkTask = Task.Run(() => SampleNetwork(now));
            }
        }

        private void SampleNetwork(double now)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            Volatile.Write(ref _network, "Interface counters / local addresses: unavailable in the browser sandbox");
#else
            try
            {
                var text = new StringBuilder();
                long received = 0, sent = 0;
                bool completeCounters = true;
                int activeInterfaces = 0;
                var key = new StringBuilder();
                foreach (var network in NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (network.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;
                    text.Append(network.Name).Append(" · ").Append(network.NetworkInterfaceType).Append(" · ").Append(network.OperationalStatus);
                    if (network.OperationalStatus == OperationalStatus.Up)
                    {
                        ++activeInterfaces;
                        key.Append(network.Id).Append('|');
                        text.Append("\n  Link rate (not internet throughput): ").Append(network.Speed > 0 ? F(network.Speed / 1000000.0) + " Mbps" : "Unknown");
                        try
                        {
                            var stats = network.GetIPStatistics(); received += stats.BytesReceived; sent += stats.BytesSent;
                        }
                        catch (Exception) { completeCounters = false; text.Append("\n  Traffic counters unavailable"); }
                        try
                        {
                            foreach (var address in network.GetIPProperties().UnicastAddresses)
                                text.Append("\n  Local address: ").Append(address.Address);
                        }
                        catch (Exception) { text.Append("\n  Local addresses unavailable"); }
                    }
                    text.Append('\n');
                }
                string currentKey = key.ToString();
                if (completeCounters && activeInterfaces > 0 && _networkTime > 0 && currentKey == _interfaceKey && now > _networkTime && received >= _received && sent >= _sent)
                    text.Append("All active interfaces: ↓ ").Append(Bytes((long)((received - _received) / (now - _networkTime))))
                        .Append("/s · ↑ ").Append(Bytes((long)((sent - _sent) / (now - _networkTime)))).Append("/s\n");
                else text.Append(activeInterfaces == 0 ? "Traffic rate: no active interfaces\n" :
                    !completeCounters ? "Traffic rate: unavailable · interface counters not exposed\n" : "Traffic rate: collecting a stable interface window…\n");
                text.Append("Interface traffic includes other applications; VPNs may count traffic twice.\nLatency / packet loss / server GPU: not measured.");
                _networkTime = completeCounters && activeInterfaces > 0 ? now : 0; _received = received; _sent = sent; _interfaceKey = currentKey;
                Volatile.Write(ref _network, text.ToString());
            }
            catch (Exception error) { Volatile.Write(ref _network, "Interface details unavailable · " + error.GetType().Name); }
#endif
        }

        public static string Bytes(long value)
        {
            double size = Math.Max(0, value);
            var units = ByteUnits;
            int unit = 0;
            while (size >= 1024 && unit < units.Length - 1) { size /= 1024; ++unit; }
            return size.ToString("0.0", CultureInfo.InvariantCulture) + " " + units[unit];
        }
        private static string MeasuredBytes(long value) => value > 0 ? Bytes(value) : Unavailable;
        private static string Megabytes(int value) => value > 0 ? value + " MB" : Unavailable;
        private static string F(double value) => value.ToString("0.0", CultureInfo.InvariantCulture);
        private static string Milliseconds(double value) => value > 0 ? F(value) + " ms" : "Unavailable";
        private static string Percent(double ratio) => F(Math.Max(0, Math.Min(1, ratio)) * 100) + "%";
        private static string ScriptingRuntime =>
#if ENABLE_IL2CPP
            "IL2CPP";
#elif ENABLE_MONO
            "Mono";
#else
            "Unknown";
#endif
        public void Dispose() { _disposed = true; _process?.Dispose(); _process = null; }
    }
}
