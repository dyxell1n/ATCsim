using System.IO;
using ATCsim.Data.Context;

namespace ATCsim.UI;

internal static class BundledScenarios
{
    public static string GetImportDirectory(bool preset)
    {
        string category = preset ? "Presets" : "Saves";
        string extension = preset ? ".atcpreset" : ".atcsave";
        string buildDirectory = Path.Combine(AppContext.BaseDirectory, category);
        if (Directory.Exists(buildDirectory)) return buildDirectory;

        string directory = Path.GetFullPath(Path.Combine(DatabaseContext.LocalDataDirectory, "Examples", category));
        Directory.CreateDirectory(directory);
        string prefix = $"Examples/{category}/";
        var assembly = typeof(BundledScenarios).Assembly;
        foreach (string resource in assembly.GetManifestResourceNames())
        {
            if (!resource.StartsWith(prefix, StringComparison.Ordinal)) continue;
            string fileName = resource[prefix.Length..];
            if (fileName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 ||
                !string.Equals(Path.GetExtension(fileName), extension, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("A bundled scenario has an invalid file name.");
            string path = Path.Combine(directory, fileName);
            if (File.Exists(path)) continue;

            using var source = assembly.GetManifestResourceStream(resource)
                ?? throw new InvalidDataException("A bundled scenario could not be opened.");
            string temporaryPath = Path.Combine(directory, Path.GetRandomFileName());
            var target = File.Open(temporaryPath, FileMode.CreateNew, FileAccess.Write);
            bool moved = false;
            try
            {
                using (target)
                    source.CopyTo(target);

                try
                {
                    File.Move(temporaryPath, path);
                    moved = true;
                }
                catch (IOException) when (File.Exists(path))
                {
                    // Another instance already extracted this example.
                }
            }
            finally
            {
                if (!moved) File.Delete(temporaryPath);
            }
        }
        return directory;
    }
}
