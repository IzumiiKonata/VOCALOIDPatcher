using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using VOCALOIDPatcher.Formats.LibreSvip.Core;
using VOCALOIDPatcher.Formats.LibreSvip.Framework;
using VOCALOIDPatcher.Formats.LibreSvip.Model;
using VOCALOIDPatcher.Formats.LibreSvip.Serialization;

namespace VOCALOIDPatcher.Formats.LibreSvip.Plugins.Vpr;

public sealed class VprConverter : FormatConverter
{
    private const double BpmRate = 100.0;

    private const string PitchBendName = "pitchBend";
    private const string PitchBendSensName = "pitchBendSens";
    private const string DynamicsName = "dynamics";
    private const string BrightnessName = "brightness";
    private const string BreathinessName = "breathiness";
    private const string CharacterName = "character";

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        NumberHandling = JsonNumberHandling.AllowReadingFromString,
    };

    public bool ImportPitch { get; set; } = true;
    public bool ImportInstrumental { get; set; } = true;
    public bool ImportVolume { get; set; } = true;
    public bool ImportBreath { get; set; } = true;
    public bool ImportGender { get; set; } = true;
    public bool ImportStrength { get; set; } = true;

    public bool IsAiSinger { get; set; } = false;
    public VprLanguage DefaultLangId { get; set; } = VprLanguage.SimplifiedChinese;
    public string DefaultCompId { get; set; } = "BL8CEAM5N4XN3LFK";
    public string DefaultSingerName { get; set; } = "Luo_Tianyi_Wan";

    public override bool CanLoad => true;
    public override bool CanDump => true;

    public override Project Load(byte[] content)
    {
        VprProject vpr;
        using (var archive = new ZipArchive(new MemoryStream(content), ZipArchiveMode.Read))
        {
            var entry = archive.GetEntry("Project/sequence.json")
                ?? throw new InvalidDataException("vpr archive missing Project/sequence.json");
            using var entryStream = entry.Open();
            using var ms = new MemoryStream();
            entryStream.CopyTo(ms);
            vpr = JsonSerializer.Deserialize<VprProject>(ms.ToArray(), JsonOpts)
                ?? throw new InvalidDataException("Failed to parse sequence.json");
        }

        var compId2Name = new Dictionary<string, string>();
        foreach (var voice in vpr.Voices)
            if (voice.CompId != null && voice.Name != null)
                compId2Name[voice.CompId] = voice.Name;

        var songTempoList = ParseTempos(vpr.MasterTrack.Tempo.Events);
        var synchronizer = new TimeSynchronizer(songTempoList);
        var timeSignatures = ParseTimeSignatures(vpr.MasterTrack.TimeSig.Events);
        int firstBarLength = (int)Math.Round(timeSignatures[0].BarLength());

        return new Project
        {
            SongTempoList = songTempoList,
            TimeSignatureList = timeSignatures,
            TrackList = ParseTracks(vpr.Tracks, compId2Name, synchronizer, timeSignatures, firstBarLength),
        };
    }

    private static List<SongTempo> ParseTempos(List<VprOutPoint> events)
    {
        var result = events
            .Select(e => new SongTempo((int)e.Pos, e.Value / BpmRate))
            .ToList();
        if (result.Count == 0)
            result.Add(new SongTempo(0, Constants.DefaultBpm));
        return result;
    }

    private static List<TimeSignature> ParseTimeSignatures(List<VprTimeSig> events)
    {
        var result = events
            .Select(e => new TimeSignature(e.Bar, e.Numer, e.Denom))
            .ToList();
        if (result.Count == 0)
            result.Add(new TimeSignature());
        return result;
    }

    private List<Track> ParseTracks(
        List<VprRawTrack> tracks,
        Dictionary<string, string> compId2Name,
        TimeSynchronizer synchronizer,
        List<TimeSignature> timeSignatures,
        int firstBarLength)
    {
        var trackList = new List<Track>();
        foreach (var rawTrack in tracks)
        {
            if (rawTrack.Parts == null || rawTrack.Parts.Count == 0)
                continue;

            if (rawTrack.Type == 1)
            {
                if (!ImportInstrumental)
                    continue;
                foreach (var partElem in rawTrack.Parts)
                {
                    var wavPart = JsonSerializer.Deserialize<VprWavPart>(partElem.GetRawText(), JsonOpts);
                    if (wavPart?.Wav == null)
                        continue;
                    var audioPath = wavPart.Wav.OriginalName ?? wavPart.Wav.Name;
                    trackList.Add(new InstrumentalTrack
                    {
                        Title = wavPart.Name ?? rawTrack.Name ?? "",
                        Mute = rawTrack.IsMuted,
                        Solo = rawTrack.IsSoloMode,
                        AudioFilePath = audioPath,
                        Offset = wavPart.Pos,
                    });
                }
            }
            else
            {
                foreach (var partElem in rawTrack.Parts)
                {
                    var part = JsonSerializer.Deserialize<VprInVoicePart>(partElem.GetRawText(), JsonOpts);
                    if (part == null)
                        continue;

                    string compId = "";
                    var supportedLangIds = new List<VprLanguage>();
                    if (part.Voice != null)
                    {
                        compId = part.Voice.CompId ?? "";
                        if (part.Voice.LangId.HasValue)
                            supportedLangIds.Add((VprLanguage)part.Voice.LangId.Value);
                    }
                    else if (part.AiVoice != null)
                    {
                        compId = part.AiVoice.CompId ?? "";
                        supportedLangIds.AddRange(part.AiVoice.LangIds
                            .Where(l => l.LangId.HasValue)
                            .Select(l => (VprLanguage)l.LangId!.Value));
                    }

                    var mainLang = supportedLangIds.Count > 0 ? supportedLangIds[0] : VprLanguage.SimplifiedChinese;
                    string defaultLyric = mainLang switch
                    {
                        VprLanguage.Japanese => Constants.DefaultJapaneseLyric,
                        VprLanguage.Korean => Constants.DefaultKoreanLyric,
                        VprLanguage.SimplifiedChinese => Constants.DefaultChineseLyric,
                        _ => Constants.DefaultEnglishLyric,
                    };

                    var (noteList, directPitchPoints) = ParseNotes(part.Notes, part.Pos, defaultLyric, firstBarLength);
                    var singingTrack = new SingingTrack
                    {
                        Title = part.Name ?? rawTrack.Name ?? "",
                        Mute = rawTrack.IsMuted,
                        Solo = rawTrack.IsSoloMode,
                        NoteList = noteList,
                        AiSingerName = compId2Name.TryGetValue(compId, out var n) ? n : "",
                    };

                    if (ImportPitch)
                    {
                        if (directPitchPoints.Count > 0)
                        {
                            singingTrack.EditedParams.Pitch.Points.AddRange(directPitchPoints);
                        }
                        else
                        {
                            var pitEvents = GetControllerEvents(part, PitchBendName);
                            if (pitEvents.Count > 0)
                            {
                                var pbsEvents = GetControllerEvents(part, PitchBendSensName);
                                var pit = new ControllerCurve("pitch_bend", pitEvents, 0, -8192, 8191);
                                var pbs = new ControllerCurve("pitch_bend_sens", pbsEvents, 2, 1, 24);
                                var handler = new VocaloidPitchHandler(synchronizer, singingTrack.NoteList, timeSignatures, firstBarLength);
                                var absResult = handler.ToAbsolutePitch(
                                    new List<PitchBendData> { new(pit, pbs) },
                                    new List<int> { part.Pos });
                                if (absResult != null)
                                    singingTrack.EditedParams.Pitch = absResult;
                            }
                        }
                    }

                    ParseParams(part, singingTrack.EditedParams, part.Pos, firstBarLength);
                    trackList.Add(singingTrack);
                }
            }
        }
        return trackList;
    }

    private static List<ControllerEvent> GetControllerEvents(VprInVoicePart part, string name)
    {
        if (part.Controllers == null)
            return new List<ControllerEvent>();
        var result = new List<ControllerEvent>();
        foreach (var ctrl in part.Controllers)
        {
            if (ctrl.Name != name)
                continue;
            foreach (var ev in ctrl.Events)
            {
                double? val = ParseVprPointValue(ev.Value);
                if (val.HasValue)
                    result.Add(new ControllerEvent(ev.Pos, (int)val.Value));
            }
        }
        return result;
    }

    private static double? ParseVprPointValue(JsonElement? elem)
    {
        if (elem == null)
            return null;
        var e = elem.Value;
        if (e.ValueKind == JsonValueKind.Number)
            return e.GetDouble();
        if (e.ValueKind == JsonValueKind.String)
        {
            var s = e.GetString();
            if (s == "ZeroPitch")
                return 0;
            if (double.TryParse(s, System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out double d))
                return d;
        }
        return null;
    }

    private (List<Note>, List<Point>) ParseNotes(
        List<VprNote> notes,
        int partPos,
        string defaultLyric,
        int firstBarLength)
    {
        var noteList = new List<Note>();
        var pitchPoints = new List<Point>();

        if (notes.Count == 0)
            return (noteList, pitchPoints);

        int? nextPos = null;
        for (int i = notes.Count - 1; i >= 0; i--)
        {
            var note = notes[i];
            int normalizedDuration = note.Duration ?? 0;

            if (ImportPitch && note.DirectPitches != null && note.DirectPitches.Count > 0)
            {
                var notePitchPoints = new List<Point>();
                foreach (var pitch in note.DirectPitches)
                {
                    if (pitch.Pos < 0 || (note.Duration.HasValue && pitch.Pos > note.Duration.Value))
                        continue;
                    int absPos = note.Pos + partPos + pitch.Pos + firstBarLength;
                    bool isZeroPitch = pitch.Value.HasValue
                        && pitch.Value.Value.ValueKind == JsonValueKind.String
                        && pitch.Value.Value.GetString() == "ZeroPitch";
                    if (!isZeroPitch)
                    {
                        double? numVal = pitch.Value.HasValue && pitch.Value.Value.ValueKind == JsonValueKind.Number
                            ? pitch.Value.Value.GetDouble()
                            : null;
                        if (numVal.HasValue)
                        {
                            if (notePitchPoints.Count == 0 || notePitchPoints[^1].Y == -100)
                                notePitchPoints.Add(new Point(absPos, -100));
                            notePitchPoints.Add(new Point(absPos, (int)numVal.Value + note.Number * 100));
                        }
                    }
                    else
                    {
                        notePitchPoints.Add(new Point(absPos, note.Number * 100));
                        notePitchPoints.Add(new Point(absPos, -100));
                    }
                }
                pitchPoints.InsertRange(0, notePitchPoints);
            }

            if (nextPos.HasValue)
            {
                int distance = nextPos.Value - note.Pos;
                if (distance < normalizedDuration)
                    normalizedDuration = distance;
            }

            if (normalizedDuration > 0)
            {
                noteList.Insert(0, new Note
                {
                    StartPos = note.Pos + partPos,
                    Length = normalizedDuration,
                    KeyNumber = note.Number,
                    Lyric = string.IsNullOrEmpty(note.Lyric) ? defaultLyric : note.Lyric,
                });
            }
            nextPos = note.Pos;
        }

        if (pitchPoints.Count > 0)
        {
            pitchPoints.Insert(0, Point.StartPoint());
            pitchPoints.Add(Point.EndPoint());
        }

        return (noteList, pitchPoints);
    }

    private void ParseParams(VprInVoicePart part, Params editedParams, int partPos, int firstBarLength)
    {
        int offset = partPos + firstBarLength;

        if (ImportVolume)
        {
            var events = GetControllerEvents(part, DynamicsName);
            if (events.Count > 0)
            {
                var curve = new ControllerCurve("dynamics", events, 64, 0, 127);
                editedParams.Volume.Points.AddRange(ConvertVocaloidCurveToParamPoints(curve, offset));
            }
        }

        if (ImportBreath)
        {
            var events = GetControllerEvents(part, BreathinessName);
            if (events.Count > 0)
            {
                var curve = new ControllerCurve("breathiness", events, 0, 0, 127);
                editedParams.Breath.Points.AddRange(ConvertVocaloidCurveToParamPoints(curve, offset));
            }
        }

        if (ImportGender)
        {
            var events = GetControllerEvents(part, CharacterName);
            if (events.Count > 0)
            {
                var curve = new ControllerCurve("gender", events, 0, -64, 63);
                editedParams.Gender.Points.AddRange(ConvertVocaloidCurveToParamPoints(curve, offset));
            }
        }

        if (ImportStrength)
        {
            var events = GetControllerEvents(part, BrightnessName);
            if (events.Count > 0)
            {
                var curve = new ControllerCurve("brightness", events, 0, 0, 127);
                editedParams.Strength.Points.AddRange(ConvertVocaloidCurveToParamPoints(curve, offset));
            }
        }
    }

    private static List<Point> ConvertVocaloidCurveToParamPoints(ControllerCurve curve, int positionOffset)
    {
        if (curve.IsEmpty)
            return new List<Point>();

        const int internalMin = -1000;
        const int internalMax = 1000;
        const int internalDefault = 0;

        int MapToInternal(int value)
        {
            if (value >= curve.DefaultValue)
            {
                int denom = curve.MaxValue - curve.DefaultValue;
                if (denom == 0) return internalDefault;
                return (int)Math.Round((double)(value - curve.DefaultValue) / denom * (internalMax - internalDefault) + internalDefault);
            }
            else
            {
                int denom = curve.DefaultValue - curve.MinValue;
                if (denom == 0) return internalDefault;
                return (int)Math.Round((double)(value - curve.DefaultValue) / denom * (internalDefault - internalMin) + internalDefault);
            }
        }

        var points = new List<Point>();
        int previousValue = MapToInternal(curve.DefaultValue);

        foreach (var ev in curve.Events)
        {
            int currentPos = ev.Pos + positionOffset;
            int currentValue = MapToInternal(ev.Value);
            int boundaryPos = currentPos - 1;
            if (currentValue != previousValue && boundaryPos >= 0)
            {
                var bp = new Point(boundaryPos, previousValue);
                if (points.Count == 0 || points[^1] != bp)
                    points.Add(bp);
            }
            var cp = new Point(currentPos, currentValue);
            if (points.Count == 0 || points[^1] != cp)
                points.Add(cp);
            previousValue = currentValue;
        }
        return points;
    }

    public override byte[] Dump(Project project)
    {
        int firstBarLength = (int)Math.Round(project.TimeSignatureList.Count > 0
            ? project.TimeSignatureList[0].BarLength()
            : new TimeSignature().BarLength());
        var synchronizer = new TimeSynchronizer(project.SongTempoList.Count > 0
            ? project.SongTempoList
            : new List<SongTempo> { new() });

        int endTick = 0;

        var timeSigEvents = GenerateTimeSigs(project.TimeSignatureList, ref endTick);
        var tempoEvents = GenerateTempos(project.SongTempoList, ref endTick);

        var outProject = new VprOutProject
        {
            Voices = new List<VprVoices>
            {
                new() { CompId = DefaultCompId, Name = DefaultSingerName },
            },
        };
        outProject.MasterTrack.TimeSig.Events = timeSigEvents;
        outProject.MasterTrack.Tempo.Events = tempoEvents;
        outProject.MasterTrack.Volume.Events.Add(new VprOutPoint { Pos = 0, Value = 0 });

        foreach (var track in project.TrackList)
        {
            if (track is InstrumentalTrack inst)
            {
                var wavPart = new VprWavPart
                {
                    Pos = inst.Offset,
                    Name = inst.Title,
                    Wav = new VprWav
                    {
                        Name = System.IO.Path.GetFileName(inst.AudioFilePath),
                        OriginalName = System.IO.Path.GetFileName(inst.AudioFilePath),
                    },
                    Region = new VprRegion { Begin = 0, End = 7680 },
                };
                var audioTrack = new VprOutAudioTrack
                {
                    IsMuted = inst.Mute,
                    IsSoloMode = inst.Solo,
                    Name = inst.Title,
                    Parts = new List<VprWavPart> { wavPart },
                };
                outProject.Tracks.Add(audioTrack);
            }
            else if (track is SingingTrack singing)
            {
                int duration = singing.NoteList.Count > 0 ? singing.NoteList[^1].EndPos : 0;
                if (duration > 0)
                    endTick = Math.Max(endTick, duration);

                var notes = GenerateNotes(singing.NoteList);
                var controllers = GeneratePitchControllers(singing, synchronizer, project.TimeSignatureList, firstBarLength);
                controllers.AddRange(GenerateParamControllers(singing.EditedParams, firstBarLength));

                var voicePart = new VprVoicePart
                {
                    Pos = 0,
                    Name = singing.Title,
                    Duration = duration > 0 ? duration : null,
                    Notes = notes,
                    Controllers = controllers.Count > 0 ? controllers : null,
                    StyleName = "No Effect",
                };

                if (IsAiSinger)
                {
                    voicePart.AiVoice = new VprAIVoice
                    {
                        CompId = DefaultCompId,
                        LangIds = new List<VprLangId> { new() { LangId = (int)DefaultLangId } },
                    };
                }
                else
                {
                    voicePart.Voice = new VprVoice
                    {
                        CompId = DefaultCompId,
                        LangId = (int)DefaultLangId,
                    };
                }

                int trackType = IsAiSinger ? 2 : 0;
                var voiceTrack = new VprOutVoiceTrack
                {
                    Type = trackType,
                    IsMuted = singing.Mute,
                    IsSoloMode = singing.Solo,
                    Name = singing.Title,
                    Parts = duration > 0 ? new List<VprVoicePart> { voicePart } : new List<VprVoicePart>(),
                };
                voiceTrack.Panpot.Events.Add(new VprOutPoint { Pos = 0, Value = 0 });
                outProject.Tracks.Add(voiceTrack);
            }
        }

        outProject.MasterTrack.Loop.End = endTick;

        string json = JsonSerializer.Serialize(outProject, JsonOpts);
        using var ms = new MemoryStream();
        using (var archive = new ZipArchive(ms, ZipArchiveMode.Create, true))
        {
            var entry = archive.CreateEntry("Project/sequence.json");
            using var entryStream = entry.Open();
            var bytes = TextHelper.EncodeUtf8(json);
            entryStream.Write(bytes, 0, bytes.Length);
        }
        return ms.ToArray();
    }

    private static List<VprTimeSig> GenerateTimeSigs(List<TimeSignature> timeSignatures, ref int endTick)
    {
        var events = new List<VprTimeSig>();
        double outputTick = 0.0;
        foreach (var ts in timeSignatures)
        {
            if (events.Count == 0)
                outputTick += Constants.TicksInBeat * 4 * ts.BarIndex;
            else
            {
                var prev = events[^1];
                outputTick += Constants.TicksInBeat * 4.0 * ((double)prev.Numer / prev.Denom) * (ts.BarIndex - prev.Bar);
            }
            events.Add(new VprTimeSig { Bar = ts.BarIndex, Denom = ts.Denominator, Numer = ts.Numerator });
        }
        if (events.Count > 0)
            endTick = Math.Max(endTick, (int)Math.Round(outputTick));
        return events;
    }

    private static List<VprOutPoint> GenerateTempos(List<SongTempo> tempoList, ref int endTick)
    {
        var events = tempoList.Select(t => new VprOutPoint
        {
            Pos = t.Position,
            Value = (int)(t.Bpm * BpmRate),
        }).ToList();
        if (events.Count > 0)
            endTick = Math.Max(endTick, events.Max(e => e.Pos));
        return events;
    }

    private List<VprNote> GenerateNotes(List<Note> noteList)
    {
        return noteList.Select(n => new VprNote
        {
            Pos = n.StartPos,
            Duration = n.Length,
            Number = n.KeyNumber,
            Lyric = n.Lyric,
            LangId = (int)DefaultLangId,
            Velocity = 64,
            IsProtected = false,
            Exp = new VprExp { Opening = 127 },
            SingingSkill = new VprSingingSkill { Duration = 158, Weight = new VprWeight { Pre = 64, Post = 64 } },
            Vibrato = new VprVibrato { TypeValue = 0, Duration = 0 },
            IsAiVibratoEnabled = false,
        }).ToList();
    }

    private List<VprOutControllers> GeneratePitchControllers(
        SingingTrack track,
        TimeSynchronizer synchronizer,
        List<TimeSignature> timeSignatures,
        int firstBarLength)
    {
        var controllers = new List<VprOutControllers>();
        if (track.EditedParams.Pitch.Points.Count == 0 || track.NoteList.Count == 0)
            return controllers;

        var handler = new VocaloidPitchHandler(synchronizer, track.NoteList, timeSignatures, firstBarLength);
        var result = handler.FromAbsolutePitch(track.EditedParams.Pitch);

        if (result.IsEmpty)
            return controllers;

        if (result.Pbs.Events.Count > 0)
            controllers.Add(new VprOutControllers
            {
                Name = PitchBendSensName,
                Events = result.Pbs.Events.Select(e => new VprOutPoint { Pos = e.Pos, Value = e.Value }).ToList(),
            });

        if (result.Pit.Events.Count > 0)
            controllers.Add(new VprOutControllers
            {
                Name = PitchBendName,
                Events = result.Pit.Events.Select(e => new VprOutPoint { Pos = e.Pos, Value = e.Value }).ToList(),
            });

        return controllers;
    }

    private List<VprOutControllers> GenerateParamControllers(Params editedParams, int firstBarLength)
    {
        var controllers = new List<VprOutControllers>();
        var paramMap = new[]
        {
            (Points: editedParams.Volume.Points, VprName: DynamicsName, Default: 64, Min: 0, Max: 127),
            (Points: editedParams.Breath.Points, VprName: BreathinessName, Default: 0, Min: 0, Max: 127),
            (Points: editedParams.Gender.Points, VprName: CharacterName, Default: 0, Min: -64, Max: 63),
            (Points: editedParams.Strength.Points, VprName: BrightnessName, Default: 0, Min: 0, Max: 127),
        };

        foreach (var (points, vprName, defVal, minVal, maxVal) in paramMap)
        {
            var events = ConvertParamPointsToVocaloidCurve(points, defVal, minVal, maxVal, firstBarLength);
            if (events.Count > 0)
                controllers.Add(new VprOutControllers { Name = vprName, Events = events });
        }
        return controllers;
    }

    private static List<VprOutPoint> ConvertParamPointsToVocaloidCurve(
        List<Point> points,
        int defaultValue,
        int minValue,
        int maxValue,
        int firstBarLength)
    {
        const int internalMin = -1000;
        const int internalMax = 1000;
        const int internalDefault = 0;

        int MapToExternal(int value)
        {
            int clamped = Math.Max(internalMin, Math.Min(internalMax, value));
            if (clamped >= internalDefault)
            {
                int denom = internalMax - internalDefault;
                if (denom == 0) return defaultValue;
                return (int)Math.Round((double)(clamped - internalDefault) / denom * (maxValue - defaultValue) + defaultValue);
            }
            else
            {
                int denom = internalDefault - internalMin;
                if (denom == 0) return defaultValue;
                return (int)Math.Round((double)(clamped - internalDefault) / denom * (defaultValue - minValue) + defaultValue);
            }
        }

        static bool IsRealPoint(Point p) =>
            p.X != Point.StartX && p.X != Point.EndX;

        var realPoints = points.Where(IsRealPoint).ToList();
        realPoints = CollapseStepPoints(realPoints);

        var events = new List<VprOutPoint>();
        foreach (var point in realPoints)
        {
            int externalValue = Math.Max(minValue, Math.Min(maxValue, MapToExternal(point.Y)));
            events.Add(new VprOutPoint
            {
                Pos = point.X - firstBarLength,
                Value = externalValue,
            });
        }
        return events;
    }

    private static List<Point> CollapseStepPoints(List<Point> points)
    {
        if (points.Count <= 2)
            return points;
        var result = new List<Point>();
        int i = 0;
        while (i < points.Count)
        {
            var point = points[i];
            var next = i + 1 < points.Count ? (Point?)points[i + 1] : null;
            if (next.HasValue
                && next.Value.X == point.X + 1
                && next.Value.Y != point.Y
                && (i == 0 && point.Y == 0 || result.Any(p => p.Y == point.Y)))
            {
                result.Add(next.Value);
                i += 2;
                continue;
            }
            if (result.Count == 0 || result[^1] != point)
                result.Add(point);
            i++;
        }
        return result;
    }
}

public sealed class VprInVoicePart
{
    [JsonPropertyName("pos")] public int Pos { get; set; }
    [JsonPropertyName("name")] public string? Name { get; set; }
    [JsonPropertyName("duration")] public int? Duration { get; set; }
    [JsonPropertyName("notes")] public List<VprNote> Notes { get; set; } = new();
    [JsonPropertyName("voice")] public VprVoice? Voice { get; set; }
    [JsonPropertyName("aiVoice")] public VprAIVoice? AiVoice { get; set; }
    [JsonPropertyName("controllers")] public List<VprControllers>? Controllers { get; set; }
}
