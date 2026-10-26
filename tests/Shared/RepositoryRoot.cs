using System.Globalization;

namespace Zerox.Tests.Shared;

internal static class RepositoryRoot
{
    // the folder that holds zerox.csharp.core.slnx, found walking up from AppContext.BaseDirectory
    public static string Folder { get; } = Find();

    public static string File(params string[] parts) => Path.Combine([Folder, .. parts]);

    public static int VersionBuild()
        => int.Parse(System.IO.File.ReadAllText(File("version.build")).Trim(), CultureInfo.InvariantCulture);

    private static string Find()
    {
        var folder = new DirectoryInfo(AppContext.BaseDirectory);
        while (folder is not null && !System.IO.File.Exists(Path.Combine(folder.FullName, "zerox.csharp.core.slnx")))
            folder = folder.Parent;
        return folder?.FullName ?? throw new DirectoryNotFoundException("zerox.csharp.core.slnx not found walking up from the test output");
    }
}
