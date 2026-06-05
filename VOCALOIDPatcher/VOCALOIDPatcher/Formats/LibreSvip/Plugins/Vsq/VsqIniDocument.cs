using System;
using System.Collections.Generic;
using System.Globalization;

namespace VOCALOIDPatcher.Formats.LibreSvip.Plugins.Vsq;

internal sealed class VsqIniSection
{
    private readonly List<KeyValuePair<string, string>> _items = new();
    private readonly Dictionary<string, int> _index = new(StringComparer.Ordinal);

    public string Name { get; }

    public VsqIniSection(string name)
    {
        Name = name;
    }

    public IReadOnlyList<KeyValuePair<string, string>> Items => _items;

    public void Set(string key, string value)
    {
        string normalized = key.ToLowerInvariant();
        if (_index.TryGetValue(normalized, out int existing))
        {
            _items[existing] = new KeyValuePair<string, string>(normalized, value);
        }
        else
        {
            _index[normalized] = _items.Count;
            _items.Add(new KeyValuePair<string, string>(normalized, value));
        }
    }

    public string? Get(string key)
    {
        string normalized = key.ToLowerInvariant();
        return _index.TryGetValue(normalized, out int idx) ? _items[idx].Value : null;
    }

    public string GetString(string key, string fallback)
    {
        var value = Get(key);
        return value ?? fallback;
    }

    public int? GetInt(string key)
    {
        var value = Get(key);
        if (value == null)
            return null;
        return int.TryParse(value.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int v) ? v : null;
    }

    public int GetInt(string key, int fallback)
    {
        return GetInt(key) ?? fallback;
    }
}

internal sealed class VsqIniDocument
{
    private readonly List<VsqIniSection> _sections = new();
    private readonly Dictionary<string, int> _index = new(StringComparer.Ordinal);

    public IReadOnlyList<VsqIniSection> Sections => _sections;

    public bool HasSection(string name) => _index.ContainsKey(name);

    public VsqIniSection? GetSection(string name) =>
        _index.TryGetValue(name, out int idx) ? _sections[idx] : null;

    public VsqIniSection AddSection(string name)
    {
        if (_index.TryGetValue(name, out int existing))
            return _sections[existing];
        var section = new VsqIniSection(name);
        _index[name] = _sections.Count;
        _sections.Add(section);
        return section;
    }

    public string GetString(string section, string key, string fallback)
    {
        var sec = GetSection(section);
        return sec?.Get(key) ?? fallback;
    }

    public int GetInt(string section, string key, int fallback)
    {
        var sec = GetSection(section);
        return sec?.GetInt(key) ?? fallback;
    }

    public static VsqIniDocument Parse(string text)
    {
        var doc = new VsqIniDocument();
        VsqIniSection? current = null;
        var lines = text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        foreach (var raw in lines)
        {
            string line = raw.TrimEnd();
            if (line.Length == 0)
                continue;
            char first = line.TrimStart().Length > 0 ? line.TrimStart()[0] : ' ';
            if (first == '#' || first == ';')
                continue;
            string trimmed = line.Trim();
            if (trimmed.StartsWith("[", StringComparison.Ordinal) && trimmed.EndsWith("]", StringComparison.Ordinal))
            {
                string name = trimmed.Substring(1, trimmed.Length - 2);
                current = doc.AddSection(name);
                continue;
            }
            if (current == null)
                continue;
            int sep = IndexOfSeparator(line);
            if (sep < 0)
                continue;
            string key = line.Substring(0, sep).Trim();
            string value = line.Substring(sep + 1).Trim();
            if (key.Length == 0)
                continue;
            current.Set(key, value);
        }
        return doc;
    }

    private static int IndexOfSeparator(string line)
    {
        int eq = line.IndexOf('=');
        int colon = line.IndexOf(':');
        if (eq < 0)
            return colon;
        if (colon < 0)
            return eq;
        return Math.Min(eq, colon);
    }
}
