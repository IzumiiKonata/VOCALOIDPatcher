using System;
using System.Collections.Generic;

namespace VOCALOIDPatcher.Formats.LibreSvip;

public sealed class NotesOverlapExportException : Exception
{
    public IReadOnlyList<int> Bars { get; }

    public NotesOverlapExportException(IReadOnlyList<int> bars)
    {
        Bars = bars;
    }
}
