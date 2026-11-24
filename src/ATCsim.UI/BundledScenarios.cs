using System.IO;
using ATCsim.Data.Context;

namespace ATCsim.UI;

internal static class BundledScenarios
{
    public static string GetImportDirectory(bool preset)
    {
        string category = preset ? "Presets" : "Saves";
        string buildDirectory = Path.Combine(AppContext.BaseDirectory, category);
        if (Directory.Exists(buildDirectory)) return buildDirectory;

        string directory = Path.Combine(DatabaseContext.LocalDataDirectory, "Examples", category);
        Directory.CreateDirectory(directory);
        string prefix = $"Examples/{category}/";
        var assembly = typeof(BundledScenarios).Assembly;
        foreach (string resource in assembly.GetManifestResourceNames())
        {
            if (!resource.StartsWith(prefix, StringComparison.Ordinal)) continue;
            string path = Path.Combine(directory, resource[prefix.Length..]);
            if (File.Exists(path)) continue;

            string temporaryPath = Path.Combine(directory, Path.GetRandomFileName());
            try
            {
                using var source = assembly.GetManifestResourceStream(resource)!;
                using (var target = File.Open(temporaryPath, FileMode.CreateNew, FileAccess.Write))
                    source.CopyTo(target);

                try
                {
                    File.Move(temporaryPath, path);
                }
                catch (IOException) when (File.Exists(path))
                {
                    // Another instance already extracted this example.
                }
            }
            finally
            {
                File.Delete(temporaryPath);
            }
        }
        return directory;
    }
}
