using System.IO;
using System.Text.Json;
using ViscerealityCompanion.Core.Services;

namespace ViscerealityCompanion.App;

internal sealed record PeripersonalParticipantLedgerEntry(
    int SchemaVersion,
    string StudyId,
    string ParticipantNumber,
    string ParticipantName,
    string Age,
    string Handedness,
    string SessionId,
    string LanguageCode,
    string? InitialConditionId,
    DateTimeOffset UpdatedAtUtc)
{
    public const int CurrentSchemaVersion = 1;
}

internal static class PeripersonalParticipantLedger
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = false,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    public static string LedgerPath => Path.Combine(
        CompanionOperatorDataLayout.SessionRootPath,
        "peripersonal",
        "participant-ledger.jsonl");

    public static void Append(PeripersonalSessionPreparationState state, string? path = null)
    {
        var participantNumber = string.IsNullOrWhiteSpace(state.ParticipantNumber)
            ? state.ParticipantName
            : state.ParticipantNumber;
        if (string.IsNullOrWhiteSpace(participantNumber))
        {
            return;
        }

        var entry = new PeripersonalParticipantLedgerEntry(
            PeripersonalParticipantLedgerEntry.CurrentSchemaVersion,
            state.StudyId.Trim(),
            participantNumber.Trim(),
            state.ParticipantName.Trim(),
            state.Age.Trim(),
            state.Handedness.Trim(),
            state.SessionId.Trim(),
            state.LanguageCode.Trim(),
            state.InitialConditionId,
            DateTimeOffset.UtcNow);
        Append(entry, path);
    }

    public static void Append(PeripersonalParticipantLedgerEntry entry, string? path = null)
    {
        var targetPath = string.IsNullOrWhiteSpace(path) ? LedgerPath : path;
        var directory = Path.GetDirectoryName(targetPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.AppendAllText(targetPath, JsonSerializer.Serialize(entry, JsonOptions) + Environment.NewLine);
    }

    public static PeripersonalParticipantLedgerEntry? FindLatest(string participantNumber, string? studyId = null, string? path = null)
    {
        if (string.IsNullOrWhiteSpace(participantNumber))
        {
            return null;
        }

        var normalizedNumber = participantNumber.Trim();
        return Load(path)
            .Where(entry => string.Equals(entry.ParticipantNumber, normalizedNumber, StringComparison.OrdinalIgnoreCase))
            .Where(entry => string.IsNullOrWhiteSpace(studyId) || string.Equals(entry.StudyId, studyId.Trim(), StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(entry => entry.UpdatedAtUtc)
            .FirstOrDefault();
    }

    public static IReadOnlyList<PeripersonalParticipantLedgerEntry> Load(string? path = null)
    {
        var sourcePath = string.IsNullOrWhiteSpace(path) ? LedgerPath : path;
        if (!File.Exists(sourcePath))
        {
            return [];
        }

        var entries = new List<PeripersonalParticipantLedgerEntry>();
        foreach (var line in File.ReadLines(sourcePath))
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            try
            {
                var entry = JsonSerializer.Deserialize<PeripersonalParticipantLedgerEntry>(line, JsonOptions);
                if (entry is not null)
                {
                    entries.Add(entry);
                }
            }
            catch (JsonException)
            {
                // Keep loading later entries even if an older manual edit left one malformed line.
            }
        }

        return entries;
    }
}
