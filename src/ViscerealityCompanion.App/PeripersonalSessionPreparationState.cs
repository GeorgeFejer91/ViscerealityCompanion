using System.IO;
using System.Text.Json;
using ViscerealityCompanion.Core.Services;

namespace ViscerealityCompanion.App;

internal sealed record PeripersonalSessionPreparationState(
    int SchemaVersion,
    string StudyId,
    string ParticipantName,
    string? ParticipantNumber,
    string Age,
    string SessionId,
    string Handedness,
    string LanguageCode,
    string? InitialConditionId,
    DateTimeOffset UpdatedAtUtc)
{
    public const int CurrentSchemaVersion = 2;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    public static string StateDirectory => Path.Combine(CompanionOperatorDataLayout.SessionRootPath, "peripersonal");
    public static string StatePath => Path.Combine(StateDirectory, "session-preparation.json");

    public static PeripersonalSessionPreparationState Create(
        string studyId,
        string participantNumber,
        string participantName,
        string age,
        string sessionId,
        string handedness,
        string languageCode,
        string? initialConditionId)
        => new(
            CurrentSchemaVersion,
            studyId.Trim(),
            participantName.Trim(),
            participantNumber.Trim(),
            age.Trim(),
            sessionId.Trim(),
            handedness.Trim(),
            languageCode.Trim(),
            string.IsNullOrWhiteSpace(initialConditionId) ? null : initialConditionId.Trim(),
            DateTimeOffset.UtcNow);

    public void Save(string? path = null)
    {
        var targetPath = string.IsNullOrWhiteSpace(path) ? StatePath : path;
        var targetDirectory = Path.GetDirectoryName(targetPath);
        if (!string.IsNullOrWhiteSpace(targetDirectory))
        {
            Directory.CreateDirectory(targetDirectory);
        }

        var json = JsonSerializer.Serialize(this, JsonOptions);
        File.WriteAllText(targetPath, json);
    }

    public static PeripersonalSessionPreparationState? Load(string? path = null)
    {
        var sourcePath = string.IsNullOrWhiteSpace(path) ? StatePath : path;
        if (!File.Exists(sourcePath))
        {
            return null;
        }

        var json = File.ReadAllText(sourcePath);
        return JsonSerializer.Deserialize<PeripersonalSessionPreparationState>(json, JsonOptions);
    }
}
