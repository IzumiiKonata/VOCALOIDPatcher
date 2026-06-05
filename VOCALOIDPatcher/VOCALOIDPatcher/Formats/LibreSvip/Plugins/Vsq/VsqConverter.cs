using System.Text;
using VOCALOIDPatcher.Formats.LibreSvip.Core;
using VOCALOIDPatcher.Formats.LibreSvip.Framework;
using VOCALOIDPatcher.Formats.LibreSvip.Model;
using VOCALOIDPatcher.Formats.LibreSvip.Serialization;

namespace VOCALOIDPatcher.Formats.LibreSvip.Plugins.Vsq;

public enum VsqBreathOption
{
    Ignore,
    Keep,
}

public sealed class VsqConverter : FormatConverter
{
    public bool ImportPitch { get; set; } = true;
    public bool ImportVolume { get; set; } = true;
    public bool ImportBreath { get; set; } = true;
    public bool ImportGender { get; set; } = true;
    public bool ImportStrength { get; set; } = true;
    public VsqBreathOption Breath { get; set; } = VsqBreathOption.Ignore;
    public int TicksPerBeat { get; set; } = Constants.TicksInBeat;

    public override bool CanLoad => true;
    public override bool CanDump => true;

    public override Project Load(byte[] content)
    {
        var midi = MidiFile.Parse(content);
        var parser = new VsqParser(ImportPitch, ImportVolume, ImportBreath, ImportGender,
            ImportStrength, Breath, TextHelper.ShiftJis());
        return parser.Parse(midi);
    }

    public override byte[] Dump(Project project)
    {
        var generator = new VsqGenerator(TicksPerBeat, TextHelper.ShiftJis());
        return generator.Generate(project);
    }
}
