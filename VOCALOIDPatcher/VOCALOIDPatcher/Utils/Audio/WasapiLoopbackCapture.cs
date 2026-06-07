using System;
using System.Runtime.InteropServices;
using System.Threading;

namespace VOCALOIDPatcher.Utils.Audio;

using Debug = VOCALOIDPatcher.Utils.Debug;

public sealed class WasapiLoopbackCapture : IDisposable
{
    private const int AudclntSharemodeShared = 0;
    private const uint AudclntStreamflagsLoopback = 0x00020000;
    private const uint AudclntStreamflagsEventCallback = 0x00040000;
    private const uint ClsctxAll = 23;

    private const int WaveFormatPcm = 1;
    private const int WaveFormatIeeeFloat = 3;
    private const int WaveFormatExtensible = 0xFFFE;

    private const string ProcessLoopbackPath = "VAD\\Process_Loopback";

    private const int FftFlushSamples = 1024;

    private const long RestartStallMs = 1500;

    private static readonly Guid ClsidMmDeviceEnumerator = new("BCDE0395-E52F-467C-8E3D-C4579291692E");
    private static readonly Guid IidAudioClient = new("1CB9AD4C-DBFA-4C32-B178-C2F568A703B2");
    private static readonly Guid IidAudioCaptureClient = new("C8ADBD64-E71E-48A0-A4DE-185C395CD317");
    private static readonly Guid SubtypeIeeeFloat = new("00000003-0000-0010-8000-00AA00389B71");

    private readonly object _lock = new();
    private readonly float[] _ring;
    private int _writeIndex;
    private long _lastBufferTick;

    private Thread? _thread;
    private volatile bool _running;
    private bool _loggedIsolated;

    public WasapiLoopbackCapture(int ringSize = 8192)
    {
        _ring = new float[ringSize];
    }

    public int SampleRate { get; private set; } = 48000;

    public bool IsRunning => _running;

    public bool ProcessIsolated { get; private set; }

    public void Start()
    {
        if (_running) return;

        _running = true;
        _thread = new Thread(CaptureLoop)
        {
            IsBackground = true,
            Name = "VOCALOIDPatcher.SpectrumCapture"
        };
        _thread.SetApartmentState(ApartmentState.MTA);
        _thread.Start();
    }

    public void Stop()
    {
        _running = false;
        var thread = _thread;
        _thread = null;
        if (thread != null && thread.IsAlive && thread != Thread.CurrentThread)
            thread.Join(500);

        lock (_lock)
        {
            Array.Clear(_ring, 0, _ring.Length);
            _writeIndex = 0;
        }
    }

    public void ReadLatest(float[] destination)
    {
        lock (_lock)
        {
            var count = destination.Length;
            var start = _writeIndex - count;
            for (var i = 0; i < count; i++)
            {
                var idx = start + i;
                idx %= _ring.Length;
                if (idx < 0) idx += _ring.Length;
                destination[i] = _ring[idx];
            }
        }
    }

    private enum SessionResult
    {
        CannotStart,
        Stalled,
        Stopped
    }

    private void CaptureLoop()
    {
        try
        {
            var started = false;
            var restartFailures = 0;

            while (_running)
            {
                var result = RunProcessLoopbackSession();

                if (result == SessionResult.Stopped)
                    return;

                if (result == SessionResult.Stalled)
                {
                    started = true;
                    restartFailures = 0;
                    if (!_running) return;
                    Thread.Sleep(40);
                    continue;
                }

                if (started && restartFailures < 8)
                {
                    restartFailures++;
                    Thread.Sleep(200);
                    continue;
                }

                break;
            }

            if (!_running) return;

            ProcessIsolated = false;
            Debug.Print("频谱: 进程环回不可用, 回退到默认设备环回 (会捕获系统全部声音)");
            RunDefaultEndpoint();
        }
        catch (Exception e)
        {
            Debug.Print($"频谱: 捕获线程异常: {e.Message}");
        }
    }

    private SessionResult RunProcessLoopbackSession()
    {
        IAudioClient? audioClient = null;
        IAudioCaptureClient? captureClient = null;
        var formatPtr = IntPtr.Zero;
        var eventHandle = IntPtr.Zero;

        try
        {
            formatPtr = BuildFloatFormat(48000, 2);
            var format = ParseFormat(formatPtr);
            SampleRate = format.SampleRate;

            foreach (var bufferDuration in new long[] { 200_000, 0 })
            {
                var client = ActivateProcessLoopbackClient();
                if (client == null)
                    return SessionResult.CannotStart;

                var hr = client.Initialize(AudclntSharemodeShared,
                    AudclntStreamflagsLoopback | AudclntStreamflagsEventCallback,
                    bufferDuration, 0, formatPtr, IntPtr.Zero);

                if (hr == 0)
                {
                    audioClient = client;
                    break;
                }

                Debug.Print($"频谱: 进程环回 Initialize 失败 (buffer={bufferDuration}, hr=0x{hr:X8})");
                Marshal.ReleaseComObject(client);
            }

            if (audioClient == null) return SessionResult.CannotStart;

            eventHandle = CreateEventEx(IntPtr.Zero, IntPtr.Zero, 0, 0x1F0003);
            if (eventHandle == IntPtr.Zero || audioClient.SetEventHandle(eventHandle) != 0)
                return SessionResult.CannotStart;

            var captureIid = IidAudioCaptureClient;
            if (audioClient.GetService(ref captureIid, out var captureObj) != 0) return SessionResult.CannotStart;
            captureClient = captureObj as IAudioCaptureClient;
            if (captureClient == null) return SessionResult.CannotStart;

            if (audioClient.Start() != 0) return SessionResult.CannotStart;

            ProcessIsolated = true;
            _lastBufferTick = Environment.TickCount64;
            if (!_loggedIsolated)
            {
                _loggedIsolated = true;
                Debug.Print("频谱: 进程环回已启用 (仅捕获编辑器)");
            }

            while (_running)
            {
                WaitForSingleObject(eventHandle, 20);
                if (!_running) break;

                DrainPackets(captureClient, format);

                if (Environment.TickCount64 - _lastBufferTick > RestartStallMs)
                    return SessionResult.Stalled;

                FeedSilenceIfStalled(60);
            }

            return SessionResult.Stopped;
        }
        catch (Exception e)
        {
            Debug.Print($"频谱: 进程环回异常: {e.Message}");
            return SessionResult.CannotStart;
        }
        finally
        {
            if (audioClient != null)
            {
                try { audioClient.Stop(); } catch { /* ignore */ }
            }

            if (eventHandle != IntPtr.Zero) CloseHandle(eventHandle);
            if (formatPtr != IntPtr.Zero) Marshal.FreeHGlobal(formatPtr);
            if (captureClient != null) Marshal.ReleaseComObject(captureClient);
            if (audioClient != null) Marshal.ReleaseComObject(audioClient);
        }
    }

    private IAudioClient? ActivateProcessLoopbackClient()
    {
        var paramsPtr = IntPtr.Zero;
        var propVariantPtr = IntPtr.Zero;

        try
        {
            var activationParams = new AudioClientActivationParams
            {
                ActivationType = 1,
                TargetProcessId = (uint)Environment.ProcessId,
                ProcessLoopbackMode = 0
            };

            paramsPtr = Marshal.AllocHGlobal(Marshal.SizeOf<AudioClientActivationParams>());
            Marshal.StructureToPtr(activationParams, paramsPtr, false);

            var propVariant = new ActivationPropVariant
            {
                Vt = 65,
                CbSize = (uint)Marshal.SizeOf<AudioClientActivationParams>(),
                BlobData = paramsPtr
            };

            propVariantPtr = Marshal.AllocHGlobal(Marshal.SizeOf<ActivationPropVariant>());
            Marshal.StructureToPtr(propVariant, propVariantPtr, false);

            var iid = IidAudioClient;
            var handler = new ActivationHandler();
            ActivateAudioInterfaceAsync(ProcessLoopbackPath, ref iid, propVariantPtr, handler, out var operation);

            if (operation == null) return null;

            try
            {
                if (!handler.Completed.WaitOne(2000))
                    return null;

                var result = operation.GetActivateResult(out var hr, out var clientObj);
                if (result != 0 || hr != 0)
                {
                    Debug.Print($"频谱: 进程环回 GetActivateResult 失败 (hr=0x{hr:X8})");
                    return null;
                }

                return clientObj as IAudioClient;
            }
            finally
            {
                Marshal.ReleaseComObject(operation);
            }
        }
        catch (Exception e)
        {
            Debug.Print($"频谱: ActivateAudioInterfaceAsync 异常: {e.Message}");
            return null;
        }
        finally
        {
            if (propVariantPtr != IntPtr.Zero) Marshal.FreeHGlobal(propVariantPtr);
            if (paramsPtr != IntPtr.Zero) Marshal.FreeHGlobal(paramsPtr);
        }
    }

    private void RunDefaultEndpoint()
    {
        IAudioClient? audioClient = null;
        IAudioCaptureClient? captureClient = null;
        var formatPtr = IntPtr.Zero;

        try
        {
            var enumType = Type.GetTypeFromCLSID(ClsidMmDeviceEnumerator, false);
            if (enumType == null) return;
            if (Activator.CreateInstance(enumType) is not IMMDeviceEnumerator enumerator) return;

            if (enumerator.GetDefaultAudioEndpoint(0, 0, out var device) != 0 || device == null) return;

            var iid = IidAudioClient;
            if (device.Activate(ref iid, ClsctxAll, IntPtr.Zero, out var clientObj) != 0) return;
            audioClient = clientObj as IAudioClient;
            if (audioClient == null) return;

            if (audioClient.GetMixFormat(out formatPtr) != 0 || formatPtr == IntPtr.Zero) return;

            var format = ParseFormat(formatPtr);
            SampleRate = format.SampleRate;

            if (audioClient.Initialize(AudclntSharemodeShared, AudclntStreamflagsLoopback,
                    1_000_000, 0, formatPtr, IntPtr.Zero) != 0) return;

            var captureIid = IidAudioCaptureClient;
            if (audioClient.GetService(ref captureIid, out var captureObj) != 0) return;
            captureClient = captureObj as IAudioCaptureClient;
            if (captureClient == null) return;

            if (audioClient.Start() != 0) return;

            while (_running)
            {
                if (captureClient.GetNextPacketSize(out var packetFrames) != 0) break;

                if (packetFrames == 0)
                {
                    FeedSilenceIfStalled(40);
                    Thread.Sleep(2);
                    continue;
                }

                DrainPackets(captureClient, format);
            }

            audioClient.Stop();
        }
        finally
        {
            if (formatPtr != IntPtr.Zero) Marshal.FreeCoTaskMem(formatPtr);
            if (captureClient != null) Marshal.ReleaseComObject(captureClient);
            if (audioClient != null) Marshal.ReleaseComObject(audioClient);
        }
    }

    private void DrainPackets(IAudioCaptureClient captureClient, AudioFormat format)
    {
        while (true)
        {
            if (captureClient.GetNextPacketSize(out var packetFrames) != 0 || packetFrames == 0)
                break;

            if (captureClient.GetBuffer(out var dataPtr, out var framesAvailable,
                    out var flags, out _, out _) != 0)
                break;

            if (framesAvailable > 0)
            {
                var silent = (flags & 0x2) != 0;
                Append(dataPtr, (int)framesAvailable, format, silent);
            }

            captureClient.ReleaseBuffer(framesAvailable);
        }
    }

    private void FeedSilenceIfStalled(long thresholdMs)
    {
        if (Environment.TickCount64 - _lastBufferTick < thresholdMs)
            return;

        lock (_lock)
        {
            for (var i = 0; i < FftFlushSamples; i++)
            {
                _ring[_writeIndex] = 0f;
                _writeIndex++;
                if (_writeIndex >= _ring.Length) _writeIndex = 0;
            }
        }
    }

    private unsafe void Append(IntPtr dataPtr, int frames, AudioFormat format, bool silent)
    {
        _lastBufferTick = Environment.TickCount64;

        lock (_lock)
        {
            var channels = format.Channels;
            var ptr = (byte*)dataPtr;

            for (var frame = 0; frame < frames; frame++)
            {
                float mono = 0f;

                if (!silent)
                {
                    for (var ch = 0; ch < channels; ch++)
                    {
                        var sampleBase = ptr + (frame * channels + ch) * format.BytesPerSample;
                        mono += ReadSample(sampleBase, format);
                    }

                    mono /= channels;
                }

                _ring[_writeIndex] = mono;
                _writeIndex++;
                if (_writeIndex >= _ring.Length) _writeIndex = 0;
            }
        }
    }

    private static unsafe float ReadSample(byte* p, AudioFormat format)
    {
        if (format.IsFloat)
        {
            var value = *(float*)p;
            return float.IsFinite(value) ? value : 0f;
        }

        switch (format.BytesPerSample)
        {
            case 2:
                return *(short*)p / 32768f;
            case 3:
            {
                var sample = p[0] | (p[1] << 8) | (p[2] << 16);
                if ((sample & 0x800000) != 0) sample |= unchecked((int)0xFF000000);
                return sample / 8388608f;
            }
            case 4:
                return *(int*)p / 2147483648f;
            default:
                return 0f;
        }
    }

    private static IntPtr BuildFloatFormat(int sampleRate, int channels)
    {
        var ptr = Marshal.AllocHGlobal(18);
        var blockAlign = (ushort)(channels * 4);
        Marshal.WriteInt16(ptr, 0, WaveFormatIeeeFloat);
        Marshal.WriteInt16(ptr, 2, (short)channels);
        Marshal.WriteInt32(ptr, 4, sampleRate);
        Marshal.WriteInt32(ptr, 8, sampleRate * blockAlign);
        Marshal.WriteInt16(ptr, 12, (short)blockAlign);
        Marshal.WriteInt16(ptr, 14, 32);
        Marshal.WriteInt16(ptr, 16, 0);
        return ptr;
    }

    private static AudioFormat ParseFormat(IntPtr ptr)
    {
        var tag = (ushort)Marshal.ReadInt16(ptr, 0);
        var channels = (ushort)Marshal.ReadInt16(ptr, 2);
        var sampleRate = Marshal.ReadInt32(ptr, 4);
        var bitsPerSample = (ushort)Marshal.ReadInt16(ptr, 14);

        var isFloat = tag == WaveFormatIeeeFloat;

        if (tag == WaveFormatExtensible)
        {
            var subFormat = Marshal.PtrToStructure<Guid>(ptr + 24);
            isFloat = subFormat == SubtypeIeeeFloat;
        }
        else if (tag == WaveFormatPcm)
        {
            isFloat = false;
        }

        return new AudioFormat
        {
            Channels = channels < 1 ? 1 : channels,
            SampleRate = sampleRate <= 0 ? 48000 : sampleRate,
            BytesPerSample = bitsPerSample / 8,
            IsFloat = isFloat
        };
    }

    public void Dispose() => Stop();

    private struct AudioFormat
    {
        public int Channels;
        public int SampleRate;
        public int BytesPerSample;
        public bool IsFloat;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct AudioClientActivationParams
    {
        public int ActivationType;
        public uint TargetProcessId;
        public int ProcessLoopbackMode;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct ActivationPropVariant
    {
        public ushort Vt;
        public ushort Reserved1;
        public ushort Reserved2;
        public ushort Reserved3;
        public uint CbSize;
        public IntPtr BlobData;
    }

    [DllImport("Mmdevapi.dll", ExactSpelling = true, PreserveSig = false)]
    private static extern void ActivateAudioInterfaceAsync(
        [MarshalAs(UnmanagedType.LPWStr)] string deviceInterfacePath,
        ref Guid riid,
        IntPtr activationParams,
        IActivateAudioInterfaceCompletionHandler completionHandler,
        out IActivateAudioInterfaceAsyncOperation operation);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr CreateEventEx(IntPtr eventAttributes, IntPtr name, uint flags, uint desiredAccess);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern uint WaitForSingleObject(IntPtr handle, uint milliseconds);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CloseHandle(IntPtr handle);

    private sealed class ActivationHandler : IActivateAudioInterfaceCompletionHandler
    {
        public readonly ManualResetEvent Completed = new(false);

        public int ActivateCompleted(IntPtr operation)
        {
            Completed.Set();
            return 0;
        }
    }

    [ComImport]
    [Guid("41D949AB-9862-444A-80F6-C261334DA5EB")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IActivateAudioInterfaceCompletionHandler
    {
        [PreserveSig] int ActivateCompleted(IntPtr operation);
    }

    [ComImport]
    [Guid("72A22D78-CDE4-431D-B8CC-843A71199B6D")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IActivateAudioInterfaceAsyncOperation
    {
        [PreserveSig]
        int GetActivateResult(out int activateResult,
            [MarshalAs(UnmanagedType.IUnknown)] out object activatedInterface);
    }

    [ComImport]
    [Guid("A95664D2-9614-4F35-A746-DE8DB63617E6")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMMDeviceEnumerator
    {
        [PreserveSig] int EnumAudioEndpoints(int dataFlow, int stateMask, out IntPtr devices);

        [PreserveSig]
        int GetDefaultAudioEndpoint(int dataFlow, int role,
            [MarshalAs(UnmanagedType.Interface)] out IMMDevice? device);

        [PreserveSig] int GetDevice([MarshalAs(UnmanagedType.LPWStr)] string id, out IntPtr device);
        [PreserveSig] int RegisterEndpointNotificationCallback(IntPtr client);
        [PreserveSig] int UnregisterEndpointNotificationCallback(IntPtr client);
    }

    [ComImport]
    [Guid("D666063F-1587-4E43-81F1-B948E807363F")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMMDevice
    {
        [PreserveSig]
        int Activate(ref Guid iid, uint clsCtx, IntPtr activationParams,
            [MarshalAs(UnmanagedType.IUnknown)] out object instance);

        [PreserveSig] int OpenPropertyStore(uint access, out IntPtr properties);
        [PreserveSig] int GetId([MarshalAs(UnmanagedType.LPWStr)] out string id);
        [PreserveSig] int GetState(out uint state);
    }

    [ComImport]
    [Guid("1CB9AD4C-DBFA-4C32-B178-C2F568A703B2")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IAudioClient
    {
        [PreserveSig]
        int Initialize(int shareMode, uint streamFlags, long bufferDuration, long periodicity,
            IntPtr format, IntPtr audioSessionGuid);

        [PreserveSig] int GetBufferSize(out uint bufferFrames);
        [PreserveSig] int GetStreamLatency(out long latency);
        [PreserveSig] int GetCurrentPadding(out uint paddingFrames);
        [PreserveSig] int IsFormatSupported(int shareMode, IntPtr format, out IntPtr closestMatch);
        [PreserveSig] int GetMixFormat(out IntPtr format);
        [PreserveSig] int GetDevicePeriod(out long defaultPeriod, out long minimumPeriod);
        [PreserveSig] int Start();
        [PreserveSig] int Stop();
        [PreserveSig] int Reset();
        [PreserveSig] int SetEventHandle(IntPtr eventHandle);
        [PreserveSig] int GetService(ref Guid iid, [MarshalAs(UnmanagedType.IUnknown)] out object instance);
    }

    [ComImport]
    [Guid("C8ADBD64-E71E-48A0-A4DE-185C395CD317")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IAudioCaptureClient
    {
        [PreserveSig]
        int GetBuffer(out IntPtr data, out uint framesToRead, out uint flags,
            out ulong devicePosition, out ulong qpcPosition);

        [PreserveSig] int ReleaseBuffer(uint framesRead);
        [PreserveSig] int GetNextPacketSize(out uint frames);
    }
}
