using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using ViscerealityCompanion.Core.Models;

namespace ViscerealityCompanion.App.ViewModels;

internal sealed record PeripersonalQuestionnaireHeadsetEvent(
    string BlockId,
    string Status,
    string RequestId,
    string CommandId,
    string Action,
    bool IsLaunch,
    bool IsCompletion);

internal static class PeripersonalHeadsetEventInterpreter
{
    private static readonly Regex LooseKeyValueRegex = new(
        @"(?<key>[A-Za-z0-9_.-]+)\s*[:=]\s*(?<value>[^;,\|\r\n]+)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public static bool TryParseQuestionnaireEvent(
        LslMonitorReading reading,
        out PeripersonalQuestionnaireHeadsetEvent update)
    {
        foreach (var sample in EnumerateTextSamples(reading))
        {
            if (TryParseQuestionnaireEvent(sample, out update))
            {
                return true;
            }
        }

        update = EmptyEvent;
        return false;
    }

    internal static bool TryParseQuestionnaireEvent(
        string sample,
        out PeripersonalQuestionnaireHeadsetEvent update)
    {
        if (string.IsNullOrWhiteSpace(sample))
        {
            update = EmptyEvent;
            return false;
        }

        var trimmed = sample.Trim();
        if (trimmed.StartsWith("{", StringComparison.Ordinal)
            && TryParseQuestionnaireJson(trimmed, out update))
        {
            return true;
        }

        return TryParseLooseQuestionnaireText(trimmed, out update);
    }

    public static bool IsCompletionStatus(string status)
    {
        if (string.IsNullOrWhiteSpace(status))
        {
            return false;
        }

        var normalized = status.Trim().ToLowerInvariant();
        return normalized.Contains("complete", StringComparison.Ordinal)
            || normalized.Contains("submitted", StringComparison.Ordinal)
            || normalized.Contains("submit", StringComparison.Ordinal)
            || normalized.Contains("finished", StringComparison.Ordinal)
            || normalized.Contains("result_received", StringComparison.Ordinal)
            || normalized.Contains("result_recorded", StringComparison.Ordinal)
            || normalized.Contains("callback_received", StringComparison.Ordinal)
            || normalized.Contains("returned_to_unity", StringComparison.Ordinal)
            || normalized.Contains("saved", StringComparison.Ordinal);
    }

    public static bool IsLaunchStatus(string status)
    {
        if (string.IsNullOrWhiteSpace(status))
        {
            return false;
        }

        var normalized = status.Trim().ToLowerInvariant();
        return normalized.Contains("launch_requested", StringComparison.Ordinal)
            || normalized.Contains("launch requested", StringComparison.Ordinal)
            || normalized.Contains("open_requested", StringComparison.Ordinal)
            || normalized.Contains("opened", StringComparison.Ordinal)
            || normalized.Contains("on_screen", StringComparison.Ordinal)
            || normalized.Contains("shown", StringComparison.Ordinal)
            || normalized.Contains("displayed", StringComparison.Ordinal)
            || normalized.Contains("visible", StringComparison.Ordinal);
    }

    public static bool TryNormalizeBlockId(string? value, out string blockId)
    {
        blockId = string.Empty;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var normalized = value.Trim().ToLowerInvariant();
        if (normalized.Contains("block-1", StringComparison.Ordinal)
            || normalized.Contains("block_1", StringComparison.Ordinal)
            || normalized.Contains("block1", StringComparison.Ordinal)
            || normalized.Contains("language_selection", StringComparison.Ordinal)
            || normalized.Contains("demographics", StringComparison.Ordinal)
            || normalized.Contains("maia2", StringComparison.Ordinal)
            || normalized == "1")
        {
            blockId = "block-1";
            return true;
        }

        if (normalized.Contains("block-2", StringComparison.Ordinal)
            || normalized.Contains("block_2", StringComparison.Ordinal)
            || normalized.Contains("block2", StringComparison.Ordinal)
            || normalized.Contains("spatial_frame_reference_1", StringComparison.Ordinal)
            || normalized == "2")
        {
            blockId = "block-2";
            return true;
        }

        if (normalized.Contains("block-3", StringComparison.Ordinal)
            || normalized.Contains("block_3", StringComparison.Ordinal)
            || normalized.Contains("block3", StringComparison.Ordinal)
            || normalized.Contains("spatial_frame_reference_2", StringComparison.Ordinal)
            || normalized == "3")
        {
            blockId = "block-3";
            return true;
        }

        return false;
    }

    private static PeripersonalQuestionnaireHeadsetEvent EmptyEvent { get; } = new(
        string.Empty,
        string.Empty,
        string.Empty,
        string.Empty,
        string.Empty,
        IsLaunch: false,
        IsCompletion: false);

    private static IEnumerable<string> EnumerateTextSamples(LslMonitorReading reading)
    {
        if (!string.IsNullOrWhiteSpace(reading.TextValue))
        {
            yield return reading.TextValue;
        }

        if (reading.SampleValues is null)
        {
            yield break;
        }

        foreach (var value in reading.SampleValues)
        {
            if (!string.IsNullOrWhiteSpace(value)
                && !string.Equals(value, reading.TextValue, StringComparison.Ordinal))
            {
                yield return value;
            }
        }
    }

    private static bool TryParseQuestionnaireJson(
        string sample,
        out PeripersonalQuestionnaireHeadsetEvent update)
    {
        JsonObject root;
        try
        {
            root = JsonNode.Parse(sample) as JsonObject
                ?? throw new JsonException("Root was not an object.");
        }
        catch (JsonException)
        {
            update = EmptyEvent;
            return false;
        }

        var observedState = ReadObject(root, "observed_state", "observedState", "state");
        var lastResult = ReadObject(root, "last_result", "lastResult", "questionnaire_result", "questionnaireResult", "result");
        var lastCommand = ReadObject(root, "last_command", "lastCommand");
        var payload = ReadObject(root, "payload", "data");
        var objects = new[] { observedState, lastResult, lastCommand, root, payload };
        var action = ReadFirstString(objects, "action", "event", "event_type", "eventType", "type");
        var commandId = ReadFirstString(objects, "command_id", "commandId", "last_command_id", "lastCommandId");
        var status = ReadFirstString(
            objects,
            "questionnaire_status",
            "questionnaire.status",
            "panel_status",
            "panel.status",
            "event_status",
            "eventStatus",
            "status");
        var requestId = ReadFirstString(
            objects,
            "questionnaire_request_id",
            "questionnaire.request_id",
            "request_id",
            "requestId");
        var rawBlock = ReadFirstString(
            objects,
            "operator_questionnaire_block_id",
            "questionnaire_block_id",
            "questionnaire.block_id",
            "block_id",
            "blockId",
            "block",
            "active_block",
            "current_block",
            "open_stage",
            "questionnaire_open_stage",
            "questionnaire.stage",
            "stage",
            "condition_number",
            "conditionNumber");

        if (string.IsNullOrWhiteSpace(status) && LooksLikeQuestionnaireStatus(action))
        {
            status = action;
        }

        if (string.IsNullOrWhiteSpace(status)
            || !LooksQuestionnaireRelated(sample, action, rawBlock, requestId, status))
        {
            update = EmptyEvent;
            return false;
        }

        if (!TryNormalizeBlockId(rawBlock, out var blockId)
            && !TryNormalizeBlockId(requestId, out blockId)
            && !TryNormalizeBlockId(action, out blockId))
        {
            blockId = string.Empty;
        }

        update = BuildEvent(blockId, status, requestId, commandId, action);
        return true;
    }

    private static bool TryParseLooseQuestionnaireText(
        string sample,
        out PeripersonalQuestionnaireHeadsetEvent update)
    {
        if (!LooksQuestionnaireRelated(sample, string.Empty, string.Empty, string.Empty, string.Empty))
        {
            update = EmptyEvent;
            return false;
        }

        var values = LooseKeyValueRegex
            .Matches(sample)
            .Cast<Match>()
            .GroupBy(
                match => match.Groups["key"].Value,
                StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => group.Last().Groups["value"].Value.Trim(),
                StringComparer.OrdinalIgnoreCase);

        var status = ReadLooseValue(
            values,
            "questionnaire_status",
            "panel_status",
            "status",
            "event_status",
            "event");
        if (string.IsNullOrWhiteSpace(status))
        {
            status = IsCompletionStatus(sample)
                ? "completed"
                : IsLaunchStatus(sample)
                    ? "launch_requested"
                    : string.Empty;
        }

        if (string.IsNullOrWhiteSpace(status))
        {
            update = EmptyEvent;
            return false;
        }

        var rawBlock = ReadLooseValue(
            values,
            "operator_questionnaire_block_id",
            "questionnaire_block_id",
            "block_id",
            "block",
            "open_stage",
            "stage",
            "condition_number");
        var requestId = ReadLooseValue(values, "questionnaire_request_id", "request_id");
        var commandId = ReadLooseValue(values, "command_id", "last_command_id");
        var action = ReadLooseValue(values, "action", "event", "event_type");

        if (!TryNormalizeBlockId(rawBlock, out var blockId)
            && !TryNormalizeBlockId(requestId, out blockId)
            && !TryNormalizeBlockId(sample, out blockId))
        {
            blockId = string.Empty;
        }

        update = BuildEvent(blockId, status, requestId, commandId, action);
        return true;
    }

    private static PeripersonalQuestionnaireHeadsetEvent BuildEvent(
        string blockId,
        string status,
        string requestId,
        string commandId,
        string action)
    {
        var normalizedStatus = status?.Trim() ?? string.Empty;
        return new(
            blockId?.Trim() ?? string.Empty,
            normalizedStatus,
            requestId?.Trim() ?? string.Empty,
            commandId?.Trim() ?? string.Empty,
            action?.Trim() ?? string.Empty,
            IsLaunchStatus(normalizedStatus),
            IsCompletionStatus(normalizedStatus));
    }

    private static bool LooksLikeQuestionnaireStatus(string value)
        => value.Contains("questionnaire", StringComparison.OrdinalIgnoreCase)
            || value.Contains("panel", StringComparison.OrdinalIgnoreCase)
            || IsCompletionStatus(value)
            || IsLaunchStatus(value);

    private static bool LooksQuestionnaireRelated(
        string sample,
        string action,
        string block,
        string requestId,
        string status)
        => ContainsQuestionnaireHint(sample)
            || ContainsQuestionnaireHint(action)
            || ContainsQuestionnaireHint(block)
            || ContainsQuestionnaireHint(requestId)
            || ContainsQuestionnaireHint(status);

    private static bool ContainsQuestionnaireHint(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        return value.Contains("questionnaire", StringComparison.OrdinalIgnoreCase)
            || value.Contains("panel", StringComparison.OrdinalIgnoreCase)
            || value.Contains("block", StringComparison.OrdinalIgnoreCase)
            || value.Contains("maia", StringComparison.OrdinalIgnoreCase)
            || value.Contains("spatial_frame", StringComparison.OrdinalIgnoreCase);
    }

    private static JsonObject? ReadObject(JsonObject obj, params string[] names)
    {
        foreach (var name in names)
        {
            if (obj.TryGetPropertyValue(name, out var node) && node is JsonObject child)
            {
                return child;
            }
        }

        return null;
    }

    private static string ReadFirstString(IEnumerable<JsonObject?> objects, params string[] names)
    {
        foreach (var obj in objects)
        {
            if (obj is null)
            {
                continue;
            }

            foreach (var name in names)
            {
                if (obj.TryGetPropertyValue(name, out var node)
                    && node is not null)
                {
                    return NodeToString(node);
                }
            }
        }

        return string.Empty;
    }

    private static string ReadLooseValue(
        IReadOnlyDictionary<string, string> values,
        params string[] names)
    {
        foreach (var name in names)
        {
            if (values.TryGetValue(name, out var value)
                && !string.IsNullOrWhiteSpace(value))
            {
                return value.Trim().Trim('"');
            }
        }

        return string.Empty;
    }

    private static string NodeToString(JsonNode node)
    {
        if (node is JsonValue value)
        {
            return value.GetValueKind() == JsonValueKind.String
                ? value.GetValue<string>()
                : value.ToJsonString();
        }

        return node.ToJsonString();
    }
}
