using System;
using System.IO;
using System.Linq;
using VOCALOIDPatcher.Formats.LibreSvip.Model;

namespace VOCALOIDPatcher.Formats.LibreSvip.Framework;

public static class SvipProjectLoader
{
    public static Project Load(SvipFormatInfo info, string[] paths)
    {
        if (paths.Length == 0)
            throw new ArgumentException("No input file", nameof(paths));
        var project = info.Converter.LoadFile(paths[0]);
        if (!info.MultipleFile || paths.Length == 1)
            return project;
        foreach (string path in paths.Skip(1))
        {
            var additional = info.Converter.LoadFile(path);
            project.TrackList.AddRange(additional.TrackList);
        }
        return project;
    }
}
