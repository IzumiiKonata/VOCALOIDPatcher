using System;
using VOCALOIDPatcher.Formats.Model;

namespace VOCALOIDPatcher.Formats.Exceptions;

public class CannotReadFileException : Exception
{
}

public class EmptyProjectException : Exception
{
    public EmptyProjectException() : base("This format could not take an empty project.")
    {
    }
}

public class NotesOverlappingException : Exception
{
    public NotesOverlappingException() : base("Failed to process because there are notes overlapping with each other.")
    {
    }
}

public class UnsupportedFileFormatError : Exception
{
}

public class UnsupportedLegacyPpsfError : UnsupportedFileFormatError
{
}

public class ValueTooLargeException : Exception
{
    public ValueTooLargeException(string value, string maxValue)
        : base($"Given value {value} is larger than the maximum: {maxValue}.")
    {
    }
}

public class IllegalNotePositionException : Exception
{
    public IllegalNotePositionException(Note note, int trackIndex)
        : base($"Failed to import because note with illegal position({note.TickOn}) exists in Track No.{trackIndex + 1}")
    {
    }
}

public abstract class IllegalFileException : Exception
{
    protected IllegalFileException(string message) : base(message)
    {
    }

    public sealed class UnknownVsqVersion : IllegalFileException
    {
        public UnknownVsqVersion() : base("Cannot identify the version of the loaded vsqx file.")
        {
        }
    }

    public sealed class XmlRootNotFound : IllegalFileException
    {
        public XmlRootNotFound() : base("The root element is not found in the xml file.")
        {
        }
    }

    public sealed class XmlElementNotFound : IllegalFileException
    {
        public XmlElementNotFound(string name) : base($"The required element <{name}> is not found in the xml file.")
        {
        }
    }

    public sealed class XmlElementValueIllegal : IllegalFileException
    {
        public XmlElementValueIllegal(string name) : base($"The required element <{name}> has an illegal value.")
        {
        }
    }

    public sealed class XmlElementAttributeValueIllegal : IllegalFileException
    {
        public XmlElementAttributeValueIllegal(string attribute, string elementName)
            : base($"The required attribute \"{attribute}\" in element <{elementName}> is missing or has in illegal value.")
        {
        }
    }

    public sealed class IllegalMidiFile : IllegalFileException
    {
        public IllegalMidiFile() : base("Cannot parse this file as a MIDI file.")
        {
        }
    }

    public sealed class IllegalTsslnFile : IllegalFileException
    {
        public IllegalTsslnFile() : base("Cannot parse this file as a tssln file.")
        {
        }
    }
}
