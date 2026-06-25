using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using ViscerealityCompanion.Core.Models;

namespace ViscerealityCompanion.App.ViewModels;

internal static class PeripersonalRuntimeStateInterpreter
{
    private static readonly Regex LooseStateKeyValueRegex = new(
        @"(?<key>[A-Za-z0-9_.-]+)\s*[:=]\s*(?<value>[^;,\|\r\n]+)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public static bool TryParseParticleVisibility(LslMonitorReading reading, out bool visible)
    {
        foreach (var sample in EnumerateTextSamples(reading))
        {
            if (TryReadJsonParticleVisibility(sample, out visible)
                || TryReadLooseParticleVisibility(sample, out visible))
            {
                return true;
            }
        }

        visible = false;
        return false;
    }

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

        foreach (var sample in reading.SampleValues)
        {
            if (!string.IsNullOrWhiteSpace(sample)
                && !string.Equals(sample, reading.TextValue, StringComparison.Ordinal))
            {
                yield return sample;
            }
        }
    }

    private static bool TryReadJsonParticleVisibility(string sample, out bool visible)
    {
        visible = false;
        if (string.IsNullOrWhiteSpace(sample) || !sample.TrimStart().StartsWith("{", StringComparison.Ordinal))
        {
            return false;
        }

        JsonObject? root;
        try
        {
            root = JsonNode.Parse(sample) as JsonObject;
        }
        catch (JsonException)
        {
            return false;
        }

        if (root is null)
        {
            return false;
        }

        foreach (var obj in EnumerateStateObjects(root))
        {
            if (TryReadBool(obj, out visible, "particles_visible", "study.particles.visible"))
            {
                return true;
            }

            if (TryReadString(obj, out var mode, "visibility_mode", "study.particles.visibility_mode"))
            {
                if (mode.Contains("particles_visible", StringComparison.OrdinalIgnoreCase))
                {
                    visible = true;
                    return true;
                }

                if (mode.Contains("anchor_only", StringComparison.OrdinalIgnoreCase)
                    || mode.Contains("hidden", StringComparison.OrdinalIgnoreCase)
                    || mode.Contains("off", StringComparison.OrdinalIgnoreCase))
                {
                    visible = false;
                    return true;
                }
            }
        }

        return TryReadLastCommandParticleVisibility(root, out visible);
    }

    private static IEnumerable<JsonObject> EnumerateStateObjects(JsonObject root)
    {
        yield return root;

        foreach (var name in new[] { "observed_state", "observedState", "state", "payload", "data" })
        {
            if (root.TryGetPropertyValue(name, out var node) && node is JsonObject obj)
            {
                yield return obj;
            }
        }
    }

    private static bool TryReadBool(JsonObject obj, out bool visible, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (obj.TryGetPropertyValue(key, out var node)
                && node is not null
                && TryConvertBool(node, out visible))
            {
                return true;
            }
        }

        visible = false;
        return false;
    }

    private static bool TryReadString(JsonObject obj, out string value, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (obj.TryGetPropertyValue(key, out var node) && node is JsonValue jsonValue)
            {
                if (jsonValue.TryGetValue<string>(out var parsed)
                    && !string.IsNullOrWhiteSpace(parsed))
                {
                    value = parsed;
                    return true;
                }
            }
        }

        value = string.Empty;
        return false;
    }

    private static bool TryConvertBool(JsonNode node, out bool value)
    {
        if (node is JsonValue jsonValue)
        {
            if (jsonValue.TryGetValue<bool>(out value))
            {
                return true;
            }

            if (jsonValue.TryGetValue<string>(out var text)
                && bool.TryParse(text, out value))
            {
                return true;
            }
        }

        value = false;
        return false;
    }

    private static bool TryReadLastCommandParticleVisibility(JsonObject root, out bool visible)
    {
        visible = false;
        if (!root.TryGetPropertyValue("last_command", out var node)
            && !root.TryGetPropertyValue("lastCommand", out node))
        {
            return false;
        }

        if (node is not JsonObject command)
        {
            return false;
        }

        if (TryReadBool(command, out var accepted, "accepted") && !accepted)
        {
            return false;
        }

        if (!TryReadString(command, out var commandName, "command_name", "commandName", "action", "command"))
        {
            return false;
        }

        if (commandName.Contains("set_particles_visible", StringComparison.OrdinalIgnoreCase)
            || commandName.Contains("particles_visible", StringComparison.OrdinalIgnoreCase)
            || commandName.Contains("particles_on", StringComparison.OrdinalIgnoreCase))
        {
            visible = true;
            return true;
        }

        if (commandName.Contains("set_particles_hidden", StringComparison.OrdinalIgnoreCase)
            || commandName.Contains("set_particles_invisible", StringComparison.OrdinalIgnoreCase)
            || commandName.Contains("particles_hidden", StringComparison.OrdinalIgnoreCase)
            || commandName.Contains("particles_off", StringComparison.OrdinalIgnoreCase))
        {
            visible = false;
            return true;
        }

        return false;
    }

    private static bool TryReadLooseParticleVisibility(string sample, out bool visible)
    {
        foreach (Match match in LooseStateKeyValueRegex.Matches(sample))
        {
            var key = match.Groups["key"].Value;
            var rawValue = match.Groups["value"].Value.Trim().Trim('"');
            if (string.Equals(key, "particles_visible", StringComparison.OrdinalIgnoreCase)
                || string.Equals(key, "study.particles.visible", StringComparison.OrdinalIgnoreCase))
            {
                if (bool.TryParse(rawValue, out visible))
                {
                    return true;
                }
            }

            if (string.Equals(key, "visibility_mode", StringComparison.OrdinalIgnoreCase)
                || string.Equals(key, "study.particles.visibility_mode", StringComparison.OrdinalIgnoreCase))
            {
                if (rawValue.Contains("particles_visible", StringComparison.OrdinalIgnoreCase))
                {
                    visible = true;
                    return true;
                }

                if (rawValue.Contains("anchor_only", StringComparison.OrdinalIgnoreCase)
                    || rawValue.Contains("hidden", StringComparison.OrdinalIgnoreCase)
                    || rawValue.Contains("off", StringComparison.OrdinalIgnoreCase))
                {
                    visible = false;
                    return true;
                }
            }
        }

        visible = false;
        return false;
    }
}
