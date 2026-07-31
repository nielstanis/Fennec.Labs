using System.Reflection;
using System.Text.Json;
using FennecLabs.TaintAnalysis.Models;

namespace FennecLabs.TaintAnalysis;

/// <summary>
/// Loads and validates the built-in <c>fennec.taint.policy.v1</c> policy, optionally merging it
/// with a user-supplied policy file (<c>--taint-policy &lt;path&gt;</c>). Per policy resolution
/// rules: a user rule with a new <c>id</c> is appended; a user rule with an existing <c>id</c>
/// replaces the built-in entry in place.
/// </summary>
public static class TaintPolicyLoader
{
    private const string BuiltInResourceName = "FennecLabs.TaintAnalysis.Resources.taint-policy.v1.json";
    private const string BuiltInLabel = "<built-in>";

    private static readonly JsonSerializerOptions SerializerOptions = BuildSerializerOptions();

    /// <summary>
    /// Loads the built-in policy and, when <paramref name="userPolicyPath"/> is supplied, merges
    /// a user-provided policy file on top of it.
    /// </summary>
    /// <param name="userPolicyPath">
    /// Optional path to a user policy JSON file (<c>--taint-policy</c>). When <c>null</c> or
    /// whitespace, only the built-in policy is returned.
    /// </param>
    /// <exception cref="TaintPolicyValidationException">
    /// Thrown when either the built-in or user policy fails to parse or fails validation.
    /// </exception>
    public static TaintPolicy Load(string? userPolicyPath = null)
    {
        var builtIn = LoadBuiltIn();

        if (string.IsNullOrWhiteSpace(userPolicyPath))
            return builtIn;

        if (!File.Exists(userPolicyPath))
        {
            throw new TaintPolicyValidationException(
                userPolicyPath, fieldName: null, message: "File not found.");
        }

        string userJson;
        try
        {
            userJson = File.ReadAllText(userPolicyPath);
        }
        catch (IOException ex)
        {
            throw new TaintPolicyValidationException(
                userPolicyPath, fieldName: null, message: "Unable to read file.", ex);
        }

        var userPolicy = Parse(userJson, userPolicyPath);
        return Merge(builtIn, userPolicy);
    }

    /// <summary>Loads only the embedded built-in policy, without merging any user overrides.</summary>
    public static TaintPolicy LoadBuiltIn()
    {
        var assembly = Assembly.GetExecutingAssembly();
        using var stream = assembly.GetManifestResourceStream(BuiltInResourceName)
            ?? throw new TaintPolicyValidationException(
                BuiltInLabel, fieldName: null,
                message: $"Embedded resource '{BuiltInResourceName}' not found.");
        using var reader = new StreamReader(stream);
        var json = reader.ReadToEnd();
        return Parse(json, BuiltInLabel);
    }

    /// <summary>
    /// Merges <paramref name="overrides"/> onto <paramref name="baseline"/>: rules with a new
    /// <c>id</c> are appended; rules with an existing <c>id</c> replace the baseline entry in place.
    /// </summary>
    public static TaintPolicy Merge(TaintPolicy baseline, TaintPolicy overrides)
    {
        var merged = new List<TaintRule>(baseline.Rules);
        var indexById = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < merged.Count; i++)
            indexById[merged[i].Id] = i;

        foreach (var rule in overrides.Rules)
        {
            if (indexById.TryGetValue(rule.Id, out var existingIndex))
                merged[existingIndex] = rule;
            else
            {
                indexById[rule.Id] = merged.Count;
                merged.Add(rule);
            }
        }

        return baseline with { Rules = merged };
    }

    private static TaintPolicy Parse(string json, string sourceLabel)
    {
        TaintPolicy? policy;
        try
        {
            policy = JsonSerializer.Deserialize<TaintPolicy>(json, SerializerOptions);
        }
        catch (JsonException ex)
        {
            throw new TaintPolicyValidationException(sourceLabel, fieldName: null, message: ex.Message, ex);
        }

        if (policy is null)
        {
            throw new TaintPolicyValidationException(
                sourceLabel, fieldName: null, message: "Policy document is empty or null.");
        }

        Validate(policy, sourceLabel);
        return policy;
    }

    private static void Validate(TaintPolicy policy, string sourceLabel)
    {
        if (string.IsNullOrWhiteSpace(policy.Schema))
            throw new TaintPolicyValidationException(sourceLabel, "$schema", "must be non-empty.");
        if (string.IsNullOrWhiteSpace(policy.SchemaVersion))
            throw new TaintPolicyValidationException(sourceLabel, "schemaVersion", "must be non-empty.");
        if (string.IsNullOrWhiteSpace(policy.PolicyId))
            throw new TaintPolicyValidationException(sourceLabel, "policyId", "must be non-empty.");

        var seenIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var rule in policy.Rules)
        {
            if (string.IsNullOrWhiteSpace(rule.Id))
                throw new TaintPolicyValidationException(sourceLabel, "rules[].id", "must be non-empty.");
            if (!seenIds.Add(rule.Id))
            {
                throw new TaintPolicyValidationException(
                    sourceLabel, "rules[].id", $"duplicate rule id '{rule.Id}'.");
            }

            if (rule.Kind is TaintRuleKind.Source or TaintRuleKind.Sink &&
                string.IsNullOrWhiteSpace(rule.Category))
            {
                throw new TaintPolicyValidationException(
                    sourceLabel, "rules[].category", $"rule '{rule.Id}' of kind '{rule.Kind}' must have a category.");
            }

            if (rule.Kind == TaintRuleKind.Sink && rule.Severity is null)
            {
                throw new TaintPolicyValidationException(
                    sourceLabel, "rules[].severity", $"sink rule '{rule.Id}' must have a severity.");
            }
        }
    }

    private static JsonSerializerOptions BuildSerializerOptions()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true,
            Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
        };
        return options;
    }
}
