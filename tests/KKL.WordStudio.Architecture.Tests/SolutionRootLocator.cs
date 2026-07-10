namespace KKL.WordStudio.Architecture.Tests;

internal static class SolutionRootLocator
{
    public static string Find()
    {
        foreach (var start in new[] { AppContext.BaseDirectory, Directory.GetCurrentDirectory() }.Distinct())
        {
            var directory = new DirectoryInfo(start);
            while (directory is not null)
            {
                if (File.Exists(Path.Combine(directory.FullName, "KKL.WordStudio.sln"))
                    && Directory.Exists(Path.Combine(directory.FullName, "src"))
                    && Directory.Exists(Path.Combine(directory.FullName, "tests")))
                {
                    return directory.FullName;
                }

                directory = directory.Parent;
            }
        }

        throw new DirectoryNotFoundException(
            $"Could not locate KKL.WordStudio.sln by walking upward from '{AppContext.BaseDirectory}' or '{Directory.GetCurrentDirectory()}'.");
    }
}
