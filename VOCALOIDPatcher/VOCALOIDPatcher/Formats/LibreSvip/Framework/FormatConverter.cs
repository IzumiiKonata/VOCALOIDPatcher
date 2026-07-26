using System;
using System.IO;
using VOCALOIDPatcher.Formats.LibreSvip.Model;

namespace VOCALOIDPatcher.Formats.LibreSvip.Framework;

public abstract class FormatConverter
{
    public virtual bool CanLoad => false;
    public virtual bool CanDump => false;

    public virtual Project Load(byte[] content) =>
        throw new NotSupportedException($"{GetType().Name} 不支持导入");

    public virtual Project LoadFile(string path) => Load(File.ReadAllBytes(path));

    public virtual byte[] Dump(Project project) =>
        throw new NotSupportedException($"{GetType().Name} 不支持导出");
}
