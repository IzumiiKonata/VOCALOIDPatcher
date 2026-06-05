using System.Collections.Generic;
using System.Xml.Linq;

namespace VOCALOIDPatcher.Formats.LibreSvip.Plugins.Ccs;

internal sealed class CcsProject
{
    public string? Code { get; set; } = "7251BC4B6168E7B2992FA620BD3E1E77";
    public CcsGeneration Generation { get; set; } = new();
    public CcsSequence Sequence { get; set; } = new();

    public static CcsProject FromXml(XElement root)
    {
        var p = new CcsProject
        {
            Code = (string?)root.Attribute("Code"),
        };
        var gen = root.Element("Generation");
        if (gen != null) p.Generation = CcsGeneration.FromXml(gen);
        var seq = root.Element("Sequence");
        if (seq != null) p.Sequence = CcsSequence.FromXml(seq);
        return p;
    }

    public XElement ToXml()
    {
        var root = new XElement("Scenario");
        if (Code != null) root.Add(new XAttribute("Code", Code));
        root.Add(Generation.ToXml());
        root.Add(Sequence.ToXml());
        return root;
    }
}

internal sealed class CcsGeneration
{
    public CcsSvss Svss { get; set; } = new();

    public static CcsGeneration FromXml(XElement el)
    {
        var g = new CcsGeneration();
        var svss = el.Element("SVSS");
        if (svss != null) g.Svss = CcsSvss.FromXml(svss);
        return g;
    }

    public XElement ToXml()
    {
        var el = new XElement("Generation");
        el.Add(new XElement("Author", new XAttribute("Version", "3.2.21.2")));
        el.Add(new XElement("TTS",
            new XAttribute("Version", "3.1.0"),
            new XElement("Dictionary", new XAttribute("Version", "1.0.0"),
                new XElement("Extension", new XAttribute("Version", "1.0.0"), new XAttribute("Language", "English"))),
            new XElement("SoundSources")));
        el.Add(Svss.ToXml());
        return el;
    }
}

internal sealed class CcsSvss
{
    public string Version { get; set; } = "3.0.5";
    public List<CcsSoundSource> SoundSources { get; set; } = new();

    public static CcsSvss FromXml(XElement el)
    {
        var s = new CcsSvss { Version = (string?)el.Attribute("Version") ?? "3.0.5" };
        var ss = el.Element("SoundSources");
        if (ss != null)
            foreach (var src in ss.Elements("SoundSource"))
                s.SoundSources.Add(CcsSoundSource.FromXml(src));
        return s;
    }

    public XElement ToXml()
    {
        var el = new XElement("SVSS", new XAttribute("Version", Version),
            new XElement("Dictionary", new XAttribute("Version", "1.0.0"),
                new XElement("Extension", new XAttribute("Version", "1.0.0"), new XAttribute("Language", "Japanese"))));
        var ss = new XElement("SoundSources");
        foreach (var src in SoundSources) ss.Add(src.ToXml());
        el.Add(ss);
        return el;
    }
}

internal sealed class CcsSoundSource
{
    public string? Id { get; set; }
    public string? Name { get; set; }
    public string Version { get; set; } = "1.0.0";

    public static CcsSoundSource FromXml(XElement el) =>
        new()
        {
            Id = (string?)el.Attribute("Id"),
            Name = (string?)el.Attribute("Name"),
            Version = (string?)el.Attribute("Version") ?? "1.0.0",
        };

    public XElement ToXml()
    {
        var el = new XElement("SoundSource",
            new XAttribute("Version", Version));
        if (Id != null) el.Add(new XAttribute("Id", Id));
        if (Name != null) el.Add(new XAttribute("Name", Name));
        return el;
    }
}

internal sealed class CcsSequence
{
    public string? Id { get; set; }
    public CcsScene Scene { get; set; } = new();

    public static CcsSequence FromXml(XElement el)
    {
        var s = new CcsSequence { Id = (string?)el.Attribute("Id") };
        var scene = el.Element("Scene");
        if (scene != null) s.Scene = CcsScene.FromXml(scene);
        return s;
    }

    public XElement ToXml()
    {
        var el = new XElement("Sequence");
        if (Id != null) el.Add(new XAttribute("Id", Id));
        el.Add(Scene.ToXml());
        return el;
    }
}

internal sealed class CcsScene
{
    public string? Id { get; set; }
    public List<CcsUnit> Units { get; set; } = new();
    public List<CcsGroup> Groups { get; set; } = new();
    public CcsSoundSetting SoundSetting { get; set; } = new();

    public static CcsScene FromXml(XElement el)
    {
        var s = new CcsScene { Id = (string?)el.Attribute("Id") };
        var units = el.Element("Units");
        if (units != null)
            foreach (var u in units.Elements("Unit"))
                s.Units.Add(CcsUnit.FromXml(u));
        var groups = el.Element("Groups");
        if (groups != null)
            foreach (var g in groups.Elements("Group"))
                s.Groups.Add(CcsGroup.FromXml(g));
        var ss = el.Element("SoundSetting");
        if (ss != null) s.SoundSetting = CcsSoundSetting.FromXml(ss);
        return s;
    }

    public XElement ToXml()
    {
        var el = new XElement("Scene");
        if (Id != null) el.Add(new XAttribute("Id", Id));
        var unitsEl = new XElement("Units");
        foreach (var u in Units) unitsEl.Add(u.ToXml());
        el.Add(unitsEl);
        var groupsEl = new XElement("Groups");
        foreach (var g in Groups) groupsEl.Add(g.ToXml());
        el.Add(groupsEl);
        el.Add(SoundSetting.ToXml());
        return el;
    }
}

internal sealed class CcsSoundSetting
{
    public string Rhythm { get; set; } = "4/4";
    public double Tempo { get; set; } = 120.0;

    public static CcsSoundSetting FromXml(XElement el) =>
        new()
        {
            Rhythm = (string?)el.Attribute("Rhythm") ?? "4/4",
            Tempo = (double?)el.Attribute("Tempo") ?? 120.0,
        };

    public XElement ToXml() =>
        new XElement("SoundSetting",
            new XAttribute("Rhythm", Rhythm),
            new XAttribute("Tempo", Tempo));
}

internal sealed class CcsGroup
{
    public string Version { get; set; } = "1.0";
    public string? Id { get; set; }
    public string Category { get; set; } = "SingerSong";
    public string? Name { get; set; }
    public string? Color { get; set; }
    public double? Volume { get; set; }
    public double? Pan { get; set; }
    public bool IsSolo { get; set; }
    public bool IsMuted { get; set; }
    public string? CastId { get; set; }
    public string Language { get; set; } = "Japanese";

    public static CcsGroup FromXml(XElement el) =>
        new()
        {
            Version = (string?)el.Attribute("Version") ?? "1.0",
            Id = (string?)el.Attribute("Id"),
            Category = (string?)el.Attribute("Category") ?? "SingerSong",
            Name = (string?)el.Attribute("Name"),
            Color = (string?)el.Attribute("Color"),
            Volume = (double?)el.Attribute("Volume"),
            Pan = (double?)el.Attribute("Pan"),
            IsSolo = (bool?)el.Attribute("IsSolo") ?? false,
            IsMuted = (bool?)el.Attribute("IsMuted") ?? false,
            CastId = (string?)el.Attribute("CastId"),
            Language = (string?)el.Attribute("Language") ?? "Japanese",
        };

    public XElement ToXml()
    {
        var el = new XElement("Group",
            new XAttribute("Version", Version));
        if (Id != null) el.Add(new XAttribute("Id", Id));
        el.Add(new XAttribute("Category", Category));
        if (Name != null) el.Add(new XAttribute("Name", Name));
        if (Color != null) el.Add(new XAttribute("Color", Color));
        if (Volume != null) el.Add(new XAttribute("Volume", Volume.Value));
        if (Pan != null) el.Add(new XAttribute("Pan", Pan.Value));
        el.Add(new XAttribute("IsSolo", IsSolo.ToString().ToLowerInvariant()));
        el.Add(new XAttribute("IsMuted", IsMuted.ToString().ToLowerInvariant()));
        if (CastId != null) el.Add(new XAttribute("CastId", CastId));
        el.Add(new XAttribute("Language", Language));
        return el;
    }
}

internal sealed class CcsUnit
{
    public string Version { get; set; } = "1.0";
    public string? Id { get; set; }
    public string? Group { get; set; }
    public string? StartTime { get; set; }
    public string? Duration { get; set; }
    public string Category { get; set; } = "SingerSong";
    public string? Text { get; set; }
    public string? CastId { get; set; }
    public string Language { get; set; } = "Japanese";
    public string? FilePath { get; set; }
    public CcsSong? Song { get; set; }

    public static CcsUnit FromXml(XElement el)
    {
        var u = new CcsUnit
        {
            Version = (string?)el.Attribute("Version") ?? "1.0",
            Id = (string?)el.Attribute("Id"),
            Group = (string?)el.Attribute("Group"),
            StartTime = (string?)el.Attribute("StartTime"),
            Duration = (string?)el.Attribute("Duration"),
            Category = (string?)el.Attribute("Category") ?? "SingerSong",
            Text = (string?)el.Attribute("Text"),
            CastId = (string?)el.Attribute("CastId"),
            Language = (string?)el.Attribute("Language") ?? "Japanese",
            FilePath = (string?)el.Attribute("FilePath"),
        };
        var song = el.Element("Song");
        if (song != null) u.Song = CcsSong.FromXml(song);
        return u;
    }

    public XElement ToXml()
    {
        var el = new XElement("Unit",
            new XAttribute("Version", Version));
        if (Id != null) el.Add(new XAttribute("Id", Id));
        if (Group != null) el.Add(new XAttribute("Group", Group));
        if (StartTime != null) el.Add(new XAttribute("StartTime", StartTime));
        if (Duration != null) el.Add(new XAttribute("Duration", Duration));
        el.Add(new XAttribute("Category", Category));
        if (Text != null) el.Add(new XAttribute("Text", Text));
        if (CastId != null) el.Add(new XAttribute("CastId", CastId));
        el.Add(new XAttribute("Language", Language));
        if (FilePath != null) el.Add(new XAttribute("FilePath", FilePath));
        if (Song != null) el.Add(Song.ToXml());
        return el;
    }
}

internal sealed class CcsSong
{
    public string Version { get; set; } = "1.02";
    public CcsTempo? Tempo { get; set; }
    public CcsBeat? Beat { get; set; }
    public CcsScore Score { get; set; } = new();
    public CcsParameters Parameter { get; set; } = new();

    public static CcsSong FromXml(XElement el)
    {
        var s = new CcsSong { Version = (string?)el.Attribute("Version") ?? "1.02" };
        var tempo = el.Element("Tempo");
        if (tempo != null) s.Tempo = CcsTempo.FromXml(tempo);
        var beat = el.Element("Beat");
        if (beat != null) s.Beat = CcsBeat.FromXml(beat);
        var score = el.Element("Score");
        if (score != null) s.Score = CcsScore.FromXml(score);
        var param = el.Element("Parameter");
        if (param != null) s.Parameter = CcsParameters.FromXml(param);
        return s;
    }

    public XElement ToXml()
    {
        var el = new XElement("Song", new XAttribute("Version", Version));
        if (Tempo != null) el.Add(Tempo.ToXml());
        if (Beat != null) el.Add(Beat.ToXml());
        el.Add(Score.ToXml());
        el.Add(Parameter.ToXml());
        return el;
    }
}

internal sealed class CcsTempo
{
    public List<CcsSound> Sounds { get; set; } = new();

    public static CcsTempo FromXml(XElement el)
    {
        var t = new CcsTempo();
        foreach (var s in el.Elements("Sound"))
            t.Sounds.Add(CcsSound.FromXml(s));
        return t;
    }

    public XElement ToXml()
    {
        var el = new XElement("Tempo");
        foreach (var s in Sounds) el.Add(s.ToXml());
        return el;
    }
}

internal sealed class CcsSound
{
    public int? Clock { get; set; }
    public double? Tempo { get; set; }

    public static CcsSound FromXml(XElement el) =>
        new()
        {
            Clock = (int?)el.Attribute("Clock"),
            Tempo = (double?)el.Attribute("Tempo"),
        };

    public XElement ToXml()
    {
        var el = new XElement("Sound");
        if (Clock != null) el.Add(new XAttribute("Clock", Clock.Value));
        if (Tempo != null) el.Add(new XAttribute("Tempo", Tempo.Value));
        return el;
    }
}

internal sealed class CcsBeat
{
    public List<CcsTime> Times { get; set; } = new();

    public static CcsBeat FromXml(XElement el)
    {
        var b = new CcsBeat();
        foreach (var t in el.Elements("Time"))
            b.Times.Add(CcsTime.FromXml(t));
        return b;
    }

    public XElement ToXml()
    {
        var el = new XElement("Beat");
        foreach (var t in Times) el.Add(t.ToXml());
        return el;
    }
}

internal sealed class CcsTime
{
    public int Clock { get; set; }
    public int? Beats { get; set; }
    public int? BeatType { get; set; }

    public static CcsTime FromXml(XElement el) =>
        new()
        {
            Clock = (int?)el.Attribute("Clock") ?? 0,
            Beats = (int?)el.Attribute("Beats"),
            BeatType = (int?)el.Attribute("BeatType"),
        };

    public XElement ToXml()
    {
        var el = new XElement("Time", new XAttribute("Clock", Clock));
        if (Beats != null) el.Add(new XAttribute("Beats", Beats.Value));
        if (BeatType != null) el.Add(new XAttribute("BeatType", BeatType.Value));
        return el;
    }
}

internal sealed class CcsScore
{
    public List<CcsNote> Notes { get; set; } = new();

    public static CcsScore FromXml(XElement el)
    {
        var s = new CcsScore();
        foreach (var n in el.Elements("Note"))
            s.Notes.Add(CcsNote.FromXml(n));
        return s;
    }

    public XElement ToXml()
    {
        var el = new XElement("Score");
        foreach (var n in Notes) el.Add(n.ToXml());
        return el;
    }
}

internal sealed class CcsNote
{
    public int? Clock { get; set; }
    public int? PitchStep { get; set; }
    public int? PitchOctave { get; set; }
    public int? Duration { get; set; }
    public string? Lyric { get; set; }
    public string? Phonetic { get; set; }
    public bool? DoReMi { get; set; }

    public static CcsNote FromXml(XElement el) =>
        new()
        {
            Clock = (int?)el.Attribute("Clock"),
            PitchStep = (int?)el.Attribute("PitchStep"),
            PitchOctave = (int?)el.Attribute("PitchOctave"),
            Duration = (int?)el.Attribute("Duration"),
            Lyric = (string?)el.Attribute("Lyric"),
            Phonetic = (string?)el.Attribute("Phonetic"),
            DoReMi = (bool?)el.Attribute("DoReMi"),
        };

    public XElement ToXml()
    {
        var el = new XElement("Note");
        if (Clock != null) el.Add(new XAttribute("Clock", Clock.Value));
        if (PitchStep != null) el.Add(new XAttribute("PitchStep", PitchStep.Value));
        if (PitchOctave != null) el.Add(new XAttribute("PitchOctave", PitchOctave.Value));
        if (Duration != null) el.Add(new XAttribute("Duration", Duration.Value));
        if (Lyric != null) el.Add(new XAttribute("Lyric", Lyric));
        if (Phonetic != null) el.Add(new XAttribute("Phonetic", Phonetic));
        if (DoReMi != null) el.Add(new XAttribute("DoReMi", DoReMi.Value.ToString().ToLowerInvariant()));
        return el;
    }
}

internal sealed class CcsParameters
{
    public CcsParameter? Timing { get; set; }
    public CcsParameter? LogF0 { get; set; }
    public CcsParameter? C0 { get; set; }
    public CcsParameter? VibAmp { get; set; }
    public CcsParameter? VibFrq { get; set; }
    public CcsParameter? Alpha { get; set; }
    public CcsParameter? Husky { get; set; }

    public static CcsParameters FromXml(XElement el)
    {
        var p = new CcsParameters();
        var timing = el.Element("Timing");
        if (timing != null) p.Timing = CcsParameter.FromXml(timing);
        var logf0 = el.Element("LogF0");
        if (logf0 != null) p.LogF0 = CcsParameter.FromXml(logf0);
        var c0 = el.Element("C0");
        if (c0 != null) p.C0 = CcsParameter.FromXml(c0);
        var vibAmp = el.Element("VibAmp");
        if (vibAmp != null) p.VibAmp = CcsParameter.FromXml(vibAmp);
        var vibFrq = el.Element("VibFrq");
        if (vibFrq != null) p.VibFrq = CcsParameter.FromXml(vibFrq);
        var alpha = el.Element("Alpha");
        if (alpha != null) p.Alpha = CcsParameter.FromXml(alpha);
        var husky = el.Element("Husky");
        if (husky != null) p.Husky = CcsParameter.FromXml(husky);
        return p;
    }

    public XElement ToXml()
    {
        var el = new XElement("Parameter");
        if (Timing != null) el.Add(Timing.ToXml("Timing"));
        if (LogF0 != null) el.Add(LogF0.ToXml("LogF0"));
        if (C0 != null) el.Add(C0.ToXml("C0"));
        if (VibAmp != null) el.Add(VibAmp.ToXml("VibAmp"));
        if (VibFrq != null) el.Add(VibFrq.ToXml("VibFrq"));
        if (Alpha != null) el.Add(Alpha.ToXml("Alpha"));
        if (Husky != null) el.Add(Husky.ToXml("Husky"));
        return el;
    }
}

internal sealed class CcsParameter
{
    public int? Length { get; set; }
    public List<CcsDataPoint> DataPoints { get; set; } = new();

    public static CcsParameter FromXml(XElement el)
    {
        var p = new CcsParameter { Length = (int?)el.Attribute("Length") };
        foreach (var child in el.Elements())
        {
            var localName = child.Name.LocalName;
            if (localName == "Data")
            {
                double? val = null;
                var valAttr = (string?)child.Attribute("Value");
                var valText = child.Value;
                var rawStr = valAttr ?? (valText.Length > 0 ? valText : null);
                if (rawStr != null && double.TryParse(rawStr,
                        System.Globalization.NumberStyles.Any,
                        System.Globalization.CultureInfo.InvariantCulture, out var parsed))
                    val = parsed;
                p.DataPoints.Add(new CcsDataPoint
                {
                    IsNoData = false,
                    Index = (int?)child.Attribute("Index"),
                    Repeat = (int?)child.Attribute("Repeat"),
                    Value = val,
                });
            }
            else if (localName == "NoData")
            {
                p.DataPoints.Add(new CcsDataPoint
                {
                    IsNoData = true,
                    Index = (int?)child.Attribute("Index"),
                    Repeat = (int?)child.Attribute("Repeat"),
                    Value = null,
                });
            }
        }
        return p;
    }

    public XElement ToXml(string name)
    {
        var el = new XElement(name);
        if (Length != null) el.Add(new XAttribute("Length", Length.Value));
        foreach (var dp in DataPoints)
        {
            XElement child;
            if (dp.IsNoData)
            {
                child = new XElement("NoData");
                if (dp.Index != null) child.Add(new XAttribute("Index", dp.Index.Value));
                if (dp.Repeat != null) child.Add(new XAttribute("Repeat", dp.Repeat.Value));
            }
            else
            {
                child = new XElement("Data");
                if (dp.Index != null) child.Add(new XAttribute("Index", dp.Index.Value));
                if (dp.Repeat != null) child.Add(new XAttribute("Repeat", dp.Repeat.Value));
                if (dp.Value != null) child.Value = dp.Value.Value.ToString("R", System.Globalization.CultureInfo.InvariantCulture);
            }
            el.Add(child);
        }
        return el;
    }
}

internal sealed class CcsDataPoint
{
    public bool IsNoData { get; set; }
    public int? Index { get; set; }
    public int? Repeat { get; set; }
    public double? Value { get; set; }
}
