using System;
using System.Runtime.InteropServices;
using System.Threading;

namespace VOCALOIDPatcher.Utils.Audio;

public sealed class WasapiLoopbackCapture : IDisposable
{
    private const int AudclntSharemodeShared = 0;
    private const uint AudclntStreamflagsLoopback = 0x00020000;
    private const uint ClsctxAll = 23;

    private const int WaveFormatPcm = 1;
    private const int WaveFormatIeeeFloat = 3;
    private const int WaveFormatExtensible = 0xFFFE;

    private static readonly Guid ClsidMmDeviceEnumerator = new("BCDE0395-E52F-467C-8E3D-C4579291692E");
    private static readonly Guid IidAudioClient = new("1CB9AD4C-DBFA-4C32-B178-C2F568A703B2");
    private static readonly Guid IidAudioCaptureClient = new("C8ADBD64-E71E-48A0-A4DE-185C395CD317");
    private static readonly Guid SubtypeIeeeFloat = new("00000003-0000-0010-8000-00AA00389B71");

    private readonly object _lock = new();
    private readonly float[] _ring;
    private int _writeIndex;

    private Thread? _thread;
    private volatile bool _running;

    public WasapiLoopbackCapture(int ringSize = 8192)
    {
        _ring = new float[ringSize];
    }

    public int SampleRate { get; private set; } = 48000;

    public bool IsRunning => _running;

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

    private void CaptureLoop()
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
                    2_000_000, 0, formatPtr, IntPtr.Zero) != 0) return;

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
                    Thread.Sleep(2);
                    continue;
                }

                while (packetFrames != 0)
                {
                    if (captureClient.GetBuffer(out var dataPtr, out var framesAvailable,
                            out var flags, out _, out _) != 0)
                        break;

                    if (framesAvailable > 0)
                    {
                        var silent = (flags & 0x2) != 0;
                        Append(dataPtr, (int)framesAvailable, format, silent);
                    }

                    captureClient.ReleaseBuffer(framesAvailable);

                    if (captureClient.GetNextPacketSize(out packetFrames) != 0)
                        break;
                }
            }

            audioClient.Stop();
        }
        catch
        {
            // capture unavailable (no endpoint, exclusive ASIO output); leave ring silent
        }
        finally
        {
            if (formatPtr != IntPtr.Zero) Marshal.FreeCoTaskMem(formatPtr);
            if (captureClient != null) Marshal.ReleaseComObject(captureClient);
            if (audioClient != null) Marshal.ReleaseComObject(audioClient);
        }
    }

    private unsafe void Append(IntPtr dataPtr, int frames, AudioFormat format, bool silent)
    {
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
            return *(float*)p;

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
