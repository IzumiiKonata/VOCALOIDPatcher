using System;
using System.IO;

namespace VOCALOIDPatcher.Formats.LibreSvip.Plugins.Dv;

internal static class DvAudio
{
    public static double? GetDurationSecs(string? filePath)
    {
        if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
            return null;
        try
        {
            using var stream = File.OpenRead(filePath);
            using var reader = new BinaryReader(stream);
            if (new string(reader.ReadChars(4)) != "RIFF")
                return null;
            reader.ReadInt32();
            if (new string(reader.ReadChars(4)) != "WAVE")
                return null;

            int byteRate = 0;
            long dataSize = 0;
            bool fmtFound = false;
            bool dataFound = false;
            while (stream.Position + 8 <= stream.Length)
            {
                string chunkId = new string(reader.ReadChars(4));
                uint chunkSize = reader.ReadUInt32();
                long chunkStart = stream.Position;
                if (chunkId == "fmt ")
                {
                    reader.ReadUInt16();
                    reader.ReadUInt16();
                    reader.ReadUInt32();
                    byteRate = reader.ReadInt32();
                    fmtFound = true;
                }
                else if (chunkId == "data")
                {
                    dataSize = chunkSize;
                    dataFound = true;
                }
                long next = chunkStart + chunkSize + (chunkSize % 2);
                if (next <= chunkStart)
                    break;
                stream.Position = next;
                if (fmtFound && dataFound)
                    break;
            }
            if (!fmtFound || !dataFound || byteRate <= 0)
                return null;
            return (double)dataSize / byteRate;
        }
        catch (Exception)
        {
            return null;
        }
    }
}
