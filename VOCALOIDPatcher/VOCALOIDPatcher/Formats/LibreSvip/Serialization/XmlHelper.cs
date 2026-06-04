using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;

namespace VOCALOIDPatcher.Formats.LibreSvip.Serialization;

public static class XmlHelper
{
    public static XElement? Child(this XElement element, string localName) =>
        element.Elements().FirstOrDefault(e => e.Name.LocalName == localName);

    public static IEnumerable<XElement> Children(this XElement element, string localName) =>
        element.Elements().Where(e => e.Name.LocalName == localName);

    public static string? ChildText(this XElement element, string localName) =>
        element.Child(localName)?.Value;

    public static XElement? Descendant(this XElement element, string localName) =>
        element.Descendants().FirstOrDefault(e => e.Name.LocalName == localName);
}
