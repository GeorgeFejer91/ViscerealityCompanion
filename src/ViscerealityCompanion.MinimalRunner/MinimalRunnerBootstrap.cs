using System.IO;
using ViscerealityCompanion.Core.Models;
using ViscerealityCompanion.Core.Services;

namespace ViscerealityCompanion.MinimalRunner;

internal static class MinimalRunnerBootstrap
{
    private const string PeripersonalStudyId = "peripersonal-space";

    public static async Task<StudyShellDefinition> LoadPeripersonalStudyAsync(CancellationToken cancellationToken = default)
    {
        var catalogRoot = ResolveStudyShellCatalogRoot();
        var catalog = await new StudyShellCatalogLoader()
            .LoadAsync(catalogRoot, cancellationToken)
            .ConfigureAwait(false);

        return catalog.Studies.FirstOrDefault(study =>
                string.Equals(study.Id, PeripersonalStudyId, StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidDataException($"Study shell `{PeripersonalStudyId}` was not found in {catalogRoot}.");
    }

    private static string ResolveStudyShellCatalogRoot()
    {
        foreach (var candidate in EnumerateCatalogCandidates().Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var manifestPath = Path.Combine(candidate, "manifest.json");
            if (File.Exists(manifestPath))
            {
                return candidate;
            }
        }

        throw new DirectoryNotFoundException("Could not find samples\\study-shells\\manifest.json for the minimal runner.");
    }

    private static IEnumerable<string> EnumerateCatalogCandidates()
    {
        yield return Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "samples", "study-shells"));
        yield return Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "samples", "study-shells"));

        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            yield return Path.Combine(directory.FullName, "samples", "study-shells");
            directory = directory.Parent;
        }
    }
}
