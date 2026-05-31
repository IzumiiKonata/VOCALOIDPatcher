using VOCALOIDPatcher.Formats.Model;

namespace VOCALOIDPatcher.Formats.Util;

public sealed class TickCounter
{
    private readonly double _tickRate;
    private readonly long _ticksInFullNote;

    public TickCounter(double tickRate = 1.0, long ticksInFullNote = Constants.TicksInFullNote)
    {
        _tickRate = tickRate;
        _ticksInFullNote = ticksInFullNote;
    }

    public long Tick { get; private set; }

    public long OutputTick => (long)(Tick * _tickRate);

    public int Measure { get; private set; }

    public int Numerator { get; private set; } = Constants.DefaultMeterHigh;

    public int Denominator { get; private set; } = Constants.DefaultMeterLow;

    public long TicksInMeasure => _ticksInFullNote * Numerator / Denominator;

    public void GoToTick(long newTick, int? newNumerator = null, int? newDenominator = null)
    {
        double normalizedNewTick = newTick / _tickRate;
        double tickDiff = normalizedNewTick - Tick;
        double measureDiff = tickDiff / TicksInMeasure;
        Measure += (int)measureDiff;
        Tick = (long)normalizedNewTick;
        Numerator = newNumerator ?? Numerator;
        Denominator = newDenominator ?? Denominator;
    }

    public void GoToMeasure(TimeSignature timeSignature) =>
        GoToMeasure(timeSignature.MeasurePosition, timeSignature.Numerator, timeSignature.Denominator);

    public void GoToMeasure(int newMeasure, int? newNumerator = null, int? newDenominator = null)
    {
        long measureDiff = newMeasure - Measure;
        long tickDiff = measureDiff * TicksInMeasure;
        Tick += tickDiff;
        Measure = newMeasure;
        if (newNumerator.HasValue) Numerator = newNumerator.Value;
        if (newDenominator.HasValue) Denominator = newDenominator.Value;
    }
}
