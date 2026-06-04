using System;

namespace VOCALOIDPatcher.Formats.LibreSvip.Core;

public class InvalidFileTypeException : Exception
{
    public InvalidFileTypeException() { }
    public InvalidFileTypeException(string message) : base(message) { }
}

public class UnsupportedProjectVersionException : InvalidFileTypeException
{
    public UnsupportedProjectVersionException() { }
    public UnsupportedProjectVersionException(string message) : base(message) { }
}

public class NoTrackException : Exception
{
    public NoTrackException() { }
    public NoTrackException(string message) : base(message) { }
}

public class NotesOverlappedException : Exception
{
    public NotesOverlappedException() { }
    public NotesOverlappedException(string message) : base(message) { }
}

public class ParamsException : Exception
{
    public ParamsException() { }
    public ParamsException(string message) : base(message) { }
}
