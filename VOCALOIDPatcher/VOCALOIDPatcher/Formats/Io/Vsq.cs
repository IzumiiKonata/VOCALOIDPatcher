using System.Collections.Generic;
using VOCALOIDPatcher.Formats.Model;

namespace VOCALOIDPatcher.Formats.Io;

public static class Vsq
{
    public static Model.Project Parse(IReadOnlyList<ImportFile> files, ImportParams parms) =>
        VsqLike.Parse(files, Format.Vsq, parms);

    public static ExportResult Generate(Model.Project project, IReadOnlyList<FeatureConfig> features) =>
        VsqLike.Generate(project, features, Format.Vsq);
}
