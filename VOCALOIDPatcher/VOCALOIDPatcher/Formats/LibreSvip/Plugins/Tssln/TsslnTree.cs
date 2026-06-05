using System.Collections.Generic;

namespace VOCALOIDPatcher.Formats.LibreSvip.Plugins.Tssln;

public sealed class TsslnTree
{
    public JuceNode Node { get; }

    public TsslnTree(JuceNode node)
    {
        Node = node;
    }

    public List<TsslnTree> Children(string name)
    {
        var result = new List<TsslnTree>();
        foreach (var child in Node.Children)
            if (child.Name == name)
                result.Add(new TsslnTree(child));
        return result;
    }

    public TsslnTree? FirstChild(string name)
    {
        foreach (var child in Node.Children)
            if (child.Name == name)
                return new TsslnTree(child);
        return null;
    }

    private JuceVariant? FindAttr(string name)
    {
        foreach (var attr in Node.Attrs)
            if (attr.Name == name && attr.Data.Type != null)
                return attr.Data;
        return null;
    }

    public List<JuceVariant> AttrList(string name)
    {
        var result = new List<JuceVariant>();
        foreach (var attr in Node.Attrs)
            if (attr.Name == name && attr.Data.Type != null)
                result.Add(attr.Data);
        return result;
    }

    public bool? GetBool(string name)
    {
        var attr = FindAttr(name);
        return attr?.Value is bool b ? b : (bool?)null;
    }

    public int? GetInt(string name)
    {
        var attr = FindAttr(name);
        if (attr == null)
            return null;
        return attr.Value switch
        {
            int i => i,
            long l => (int)l,
            double d => (int)d,
            _ => null,
        };
    }

    public long? GetInt64(string name)
    {
        var attr = FindAttr(name);
        if (attr == null)
            return null;
        return attr.Value switch
        {
            long l => l,
            int i => i,
            double d => (long)d,
            _ => null,
        };
    }

    public double? GetDouble(string name)
    {
        var attr = FindAttr(name);
        if (attr == null)
            return null;
        return attr.Value switch
        {
            double d => d,
            int i => i,
            long l => l,
            _ => null,
        };
    }

    public string? GetString(string name)
    {
        var attr = FindAttr(name);
        return attr?.Value as string;
    }

    public TsslnTree? GetBinaryTree(string name)
    {
        var attr = FindAttr(name);
        if (attr is { Type: JuceVarType.Binary, Value: byte[] bytes } && bytes.Length > 0)
            return new TsslnTree(JuceBinary.Parse(bytes));
        return null;
    }
}

public sealed class TsslnTreeBuilder
{
    private readonly JuceNode _node;

    public TsslnTreeBuilder(string name)
    {
        _node = new JuceNode { Name = name };
    }

    public JuceNode Node => _node;

    public TsslnTreeBuilder AddChild(JuceNode child)
    {
        _node.Children.Add(child);
        return this;
    }

    public TsslnTreeBuilder AddBool(string name, bool value)
    {
        _node.Attrs.Add(new JuceNamedVariant { Name = name, Data = JuceVariant.OfBool(value) });
        return this;
    }

    public TsslnTreeBuilder AddInt(string name, int value)
    {
        _node.Attrs.Add(new JuceNamedVariant { Name = name, Data = JuceVariant.OfInt(value) });
        return this;
    }

    public TsslnTreeBuilder AddInt64(string name, long value)
    {
        _node.Attrs.Add(new JuceNamedVariant { Name = name, Data = JuceVariant.OfInt64(value) });
        return this;
    }

    public TsslnTreeBuilder AddDouble(string name, double value)
    {
        _node.Attrs.Add(new JuceNamedVariant { Name = name, Data = JuceVariant.OfDouble(value) });
        return this;
    }

    public TsslnTreeBuilder AddString(string name, string value)
    {
        _node.Attrs.Add(new JuceNamedVariant { Name = name, Data = JuceVariant.OfString(value) });
        return this;
    }

    public TsslnTreeBuilder AddBinary(string name, byte[] value)
    {
        _node.Attrs.Add(new JuceNamedVariant { Name = name, Data = JuceVariant.OfBinary(value) });
        return this;
    }
}
