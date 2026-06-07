using System;

namespace VOCALOIDPatcher.Utils.Audio;

public sealed class SimpleFft
{
    private readonly int _size;
    private readonly int _bits;
    private readonly int[] _reverse;
    private readonly float[] _cos;
    private readonly float[] _sin;
    private readonly float[] _window;
    private readonly float[] _re;
    private readonly float[] _im;

    public SimpleFft(int size)
    {
        if (size < 2 || (size & (size - 1)) != 0)
            throw new ArgumentException("FFT size must be a power of two.", nameof(size));

        _size = size;
        _bits = (int)Math.Log2(size);

        _reverse = new int[size];
        for (var i = 0; i < size; i++)
            _reverse[i] = ReverseBits(i, _bits);

        _cos = new float[size / 2];
        _sin = new float[size / 2];
        for (var i = 0; i < size / 2; i++)
        {
            var angle = -2.0 * Math.PI * i / size;
            _cos[i] = (float)Math.Cos(angle);
            _sin[i] = (float)Math.Sin(angle);
        }

        _window = new float[size];
        for (var i = 0; i < size; i++)
            _window[i] = (float)(0.5 - 0.5 * Math.Cos(2.0 * Math.PI * i / (size - 1)));

        _re = new float[size];
        _im = new float[size];
    }

    public int Size => _size;

    public void MagnitudeSpectrum(float[] samples, float[] magnitudes)
    {
        var re = _re;
        var im = _im;

        for (var i = 0; i < _size; i++)
        {
            var j = _reverse[i];
            re[i] = samples[j] * _window[j];
            im[i] = 0f;
        }

        for (var step = 1; step < _size; step <<= 1)
        {
            var jump = step << 1;
            var twiddleStep = _size / jump;
            for (var group = 0; group < step; group++)
            {
                var twiddle = group * twiddleStep;
                var wr = _cos[twiddle];
                var wi = _sin[twiddle];
                for (var pair = group; pair < _size; pair += jump)
                {
                    var match = pair + step;
                    var tr = wr * re[match] - wi * im[match];
                    var ti = wr * im[match] + wi * re[match];
                    re[match] = re[pair] - tr;
                    im[match] = im[pair] - ti;
                    re[pair] += tr;
                    im[pair] += ti;
                }
            }
        }

        var bins = _size / 2;
        for (var i = 0; i < bins && i < magnitudes.Length; i++)
            magnitudes[i] = (float)Math.Sqrt(re[i] * re[i] + im[i] * im[i]);
    }

    private static int ReverseBits(int value, int bits)
    {
        var result = 0;
        for (var i = 0; i < bits; i++)
        {
            result = (result << 1) | (value & 1);
            value >>= 1;
        }

        return result;
    }
}
