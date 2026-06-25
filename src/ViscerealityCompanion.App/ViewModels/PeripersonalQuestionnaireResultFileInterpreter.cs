using System.Text.Json;
using System.Text.Json.Nodes;

namespace ViscerealityCompanion.App.ViewModels;

internal static class PeripersonalQuestionnaireResultFileInterpreter
{
    public static IReadOnlyList<PeripersonalQuestionnaireHeadsetEvent> ParseCompletionEvents(string jsonl)
    {
        if (string.IsNullOrWhiteSpace(jsonl))
        {
            return [];
        }

        var events = new List<PeripersonalQuestionnaireHeadsetEvent>();
        foreach (var line in jsonl.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
        {
            if (TryParseCompletionEvent(line, out var headsetEvent))
            {
                events.Add(headsetEvent);
            }
        }

        return events;
    }

    private static bool TryParseCompletionEvent(
        string line,
        out PeripersonalQuestionnaireHeadsetEvent headsetEvent)
    {
        headsetEvent = EmptyEvent;
        JsonObject outer;
        try
        {
            outer = JsonNode.Parse(line) as JsonObject
                ?? throw new JsonException("Questionnaire result line was not an object.");
        }
        catch (JsonException)
        {
            return false;
        }

        var result = ReadEmbeddedResultObject(outer);
        var status = ReadFirstString(result, "status", "terminal.reason");
        if (string.IsNullOrWhiteSpace(status))
        {
            status = ReadFirstString(outer, "status", "result_status", "resultStatus");
        }

        if (!PeripersonalHeadsetEventInterpreter.IsCompletionStatus(status))
        {
            return false;
        }

        var requestId = ReadFirstString(outer, "request_id", "requestId");
        if (string.IsNullOrWhiteSpace(requestId))
        {
            requestId = ReadFirstString(result, "request_id", "requestId");
        }

        var rawBlock = ReadFirstString(
            outer,
            "stage",
            "open_stage",
            "completed_stage",
            "condition_number",
            "conditionNumber");
        if (string.IsNullOrWhiteSpace(rawBlock))
        {
            rawBlock = ReadFirstString(
                result,
                "stage",
                "open_stage",
                "completed_stage",
                "answers.completed_stage",
                "answers.open_stage",
                "terminal.current_stage",
                "condition_number",
                "conditionNumber");
        }

        if (!PeripersonalHeadsetEventInterpreter.TryNormalizeBlockId(rawBlock, out var blockId)
            && !PeripersonalHeadsetEventInterpreter.TryNormalizeBlockId(requestId, out blockId))
        {
            return false;
        }

        headsetEvent = new PeripersonalQuestionnaireHeadsetEvent(
            blockId,
            status,
            requestId,
            string.Empty,
            "questionnaire_results.jsonl",
            IsLaunch: false,
            IsCompletion: true);
        return true;
    }

    private static PeripersonalQuestionnaireHeadsetEvent EmptyEvent { get; } = new(
        string.Empty,
        string.Empty,
        string.Empty,
        string.Empty,
        string.Empty,
        IsLaunch: false,
        IsCompletion: false);

    private static JsonObject? ReadEmbeddedResultObject(JsonObject outer)
    {
        if (outer.TryGetPropertyValue("result", out var resultNode) && resultNode is JsonObject result)
        {
            return result;
        }

        if (outer.TryGetPropertyValue("result_json", out var jsonNode)
            && jsonNode is JsonValue jsonValue
            && jsonValue.TryGetValue<string>(out var resultJson)
            && !string.IsNullOrWhiteSpace(resultJson))
        {
            try
            {
                return JsonNode.Parse(resultJson) as JsonObject;
            }
            catch (JsonException)
            {
                return null;
            }
        }

        return null;
    }

    private static string ReadFirstString(JsonObject? obj, params string[] paths)
    {
        if (obj is null)
        {
            return string.Empty;
        }

        foreach (var path in paths)
        {
            var value = ReadStringPath(obj, path);
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }

        return string.Empty;
    }

    private static string ReadStringPath(JsonObject obj, string path)
    {
        JsonNode? current = obj;
        foreach (var segment in path.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (current is not JsonObject currentObject
                || !currentObject.TryGetPropertyValue(segment, out current))
            {
                return string.Empty;
            }
        }

        return current switch
        {
            JsonValue value when value.TryGetValue<string>(out var text) => text,
            JsonValue value => value.ToJsonString(),
            _ => string.Empty
        };
    }
}
