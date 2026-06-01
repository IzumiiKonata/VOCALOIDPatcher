using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Xml;
using VOCALOIDPatcher.Formats.Exceptions;

namespace VOCALOIDPatcher.Formats.Util;

public static class Xml
{
    public static List<XmlElement> GetElementListByTagName(this XmlElement element, string name, bool allowEmpty = true)
    {
        var nodes = element.GetElementsByTagName(name);
        if (!allowEmpty && nodes.Count == 0)
            throw new IllegalFileException.XmlElementNotFound(name);
        return nodes.OfType<XmlElement>().ToList();
    }

    public static XmlElement GetSingleElementByTagName(this XmlElement element, string name) =>
        element.GetElementsByTagName(name).OfType<XmlElement>().FirstOrDefault()
        ?? throw new IllegalFileException.XmlElementNotFound(name);

    public static XmlElement? GetSingleElementByTagNameOrNull(this XmlElement element, string name) =>
        element.GetElementsByTagName(name).OfType<XmlElement>().FirstOrDefault();

    public static string? GetAttributeOrNull(this XmlElement element, string name) =>
        element.HasAttribute(name) ? element.GetAttribute(name) : null;

    public static int GetRequiredAttributeAsInteger(this XmlElement element, string attribute)
    {
        var value = element.GetAttributeOrNull(attribute);
        if (value != null && int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var result))
            return result;
        throw new IllegalFileException.XmlElementAttributeValueIllegal(attribute, element.Name);
    }

    public static long GetRequiredAttributeAsLong(this XmlElement element, string attribute)
    {
        var value = element.GetAttributeOrNull(attribute);
        if (value != null && long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var result))
            return result;
        throw new IllegalFileException.XmlElementAttributeValueIllegal(attribute, element.Name);
    }

    public static string GetRequiredAttribute(this XmlElement element, string attribute) =>
        element.GetAttributeOrNull(attribute) ?? throw new IllegalFileException.XmlElementAttributeValueIllegal(attribute, element.Name);

    public static string InnerValue(this XmlElement element)
    {
        var value = element.FirstChild?.Value;
        if (value == null)
            throw new IllegalFileException.XmlElementValueIllegal(element.Name);
        return value;
    }

    public static string? InnerValueOrNull(this XmlElement element) => element.FirstChild?.Value;

    public static void SetSingleChildValue(this XmlElement element, string name, object value)
    {
        var child = element.GetSingleElementByTagName(name);
        if (child.FirstChild != null)
            child.FirstChild.Value = value.ToString();
    }

    public static void InsertAfterThis(this XmlElement element, XmlElement child) =>
        element.ParentNode?.InsertAfter(child, element);

    public static XmlElement CloneElement(this XmlElement element) => (XmlElement)element.CloneNode(true);

    public static XmlElement AppendNewChildTo(this XmlDocument document, XmlNode node, string localName, Action<XmlElement> handler)
    {
        var element = document.CreateElement(localName);
        handler(element);
        node.AppendChild(element);
        return element;
    }

    public static void AppendText(this XmlElement element, string text) =>
        element.AppendChild(element.OwnerDocument!.CreateTextNode(text));

    public static string ToFixed(this double value, int digits) =>
        value.ToString("F" + digits, CultureInfo.InvariantCulture);
}
