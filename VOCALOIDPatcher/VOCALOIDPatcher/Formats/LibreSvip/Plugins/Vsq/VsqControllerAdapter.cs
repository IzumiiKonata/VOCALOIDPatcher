using System.Collections.Generic;
using System.Globalization;
using VOCALOIDPatcher.Formats.LibreSvip.Model;

namespace VOCALOIDPatcher.Formats.LibreSvip.Plugins.Vsq;

internal sealed class VsqControllerAdapter
{
    private static readonly Dictionary<string, string[]> ParamSections = new()
    {
        ["pitch_bend"] = new[] { "PitchBendBPList" },
        ["pitch_bend_sens"] = new[] { "PitchBendSensBPList" },
        ["dynamics"] = new[] { "DynamicsBPList" },
        ["gender"] = new[] { "GenderFactorBPList" },
        ["breathiness"] = new[] { "BreathinessBPList", "EpRResidualBPList" },
        ["brightness"] = new[] { "BrightnessBPList", "EpRESlopeBPList" },
    };

    private static readonly Dictionary<string, (int Default, int Min, int Max)> ParamDefs = new()
    {
        ["pitch_bend"] = (0, -8192, 8191),
        ["pitch_bend_sens"] = (2, 1, 24),
        ["dynamics"] = (64, 0, 127),
        ["breathiness"] = (0, 0, 127),
        ["brightness"] = (0, 0, 127),
        ["gender"] = (64, 0, 127),
    };

    private readonly int _tickPrefix;

    public VsqControllerAdapter(int tickPrefix = 0)
    {
        _tickPrefix = tickPrefix;
    }

    private static string? FindSection(VsqIniDocument doc, string paramName)
    {
        if (!ParamSections.TryGetValue(paramName, out var candidates))
            return null;
        foreach (var name in candidates)
            if (doc.HasSection(name))
                return name;
        return null;
    }

    public ControllerCurve? Extract(VsqIniDocument doc, string paramName)
    {
        var sectionName = FindSection(doc, paramName);
        if (sectionName == null)
            return null;
        return ExtractFromSection(doc, sectionName, paramName);
    }

    private ControllerCurve? ExtractFromSection(VsqIniDocument doc, string sectionName, string paramName)
    {
        var section = doc.GetSection(sectionName);
        if (section == null)
            return null;
        var events = new List<ControllerEvent>();
        foreach (var item in section.Items)
        {
            if (!int.TryParse(item.Key.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int pos))
                continue;
            if (!int.TryParse(item.Value.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int val))
                continue;
            events.Add(new ControllerEvent(pos - _tickPrefix, val));
        }
        if (events.Count == 0)
            return null;
        var def = ParamDefs.TryGetValue(paramName, out var d) ? d : (0, -127, 127);
        return new ControllerCurve(paramName, events, def.Item1, def.Item2, def.Item3);
    }
}
