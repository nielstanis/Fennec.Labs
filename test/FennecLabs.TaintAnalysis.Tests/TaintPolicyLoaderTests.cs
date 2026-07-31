using FennecLabs.TaintAnalysis.Models;

namespace FennecLabs.TaintAnalysis.Tests;

public class TaintPolicyLoaderTests
{
    private static readonly string[] ExpectedSourceCategories =
    [
        "network-input",
        "file-input",
        "environment",
        "deserialization",
        "database-read",
    ];

    private static readonly string[] ExpectedSinkCategories =
    [
        "sql-injection",
        "command-injection",
        "path-traversal",
        "xss",
        "ssrf",
        "log-injection",
    ];

    [Fact]
    public void Load_BuiltIn_ContainsAllRequiredSourceCategories()
    {
        var policy = TaintPolicyLoader.Load();

        var actualCategories = policy.Sources.Select(r => r.Category).Distinct().ToList();

        foreach (var category in ExpectedSourceCategories)
        {
            Assert.Contains(category, actualCategories);
        }
    }

    [Fact]
    public void Load_BuiltIn_ContainsAllRequiredSinkCategories()
    {
        var policy = TaintPolicyLoader.Load();

        var actualCategories = policy.Sinks.Select(r => r.Category).Distinct().ToList();

        foreach (var category in ExpectedSinkCategories)
        {
            Assert.Contains(category, actualCategories);
        }
    }

    [Fact]
    public void Load_BuiltIn_RulesHaveTypedRequiredFields()
    {
        var policy = TaintPolicyLoader.Load();

        Assert.NotEmpty(policy.Rules);
        foreach (var rule in policy.Sources.Concat(policy.Sinks))
        {
            Assert.False(string.IsNullOrWhiteSpace(rule.Id));
            Assert.False(string.IsNullOrWhiteSpace(rule.Assembly));
            Assert.False(string.IsNullOrWhiteSpace(rule.TypeName));
            Assert.False(string.IsNullOrWhiteSpace(rule.MemberName));
            Assert.False(string.IsNullOrWhiteSpace(rule.Category));
        }

        foreach (var sink in policy.Sinks)
        {
            Assert.NotNull(sink.Severity);
        }

        foreach (var source in policy.Sources)
        {
            Assert.NotNull(source.Confidence);
        }
    }

    [Fact]
    public void Load_WithUserPolicy_AppendsNewIds()
    {
        var userPolicyPath = WriteTempPolicy("""
        {
          "$schema": "fennec.taint.policy.v1",
          "schemaVersion": "1.0.0",
          "policyId": "user-override",
          "rules": [
            {
              "id": "src-custom-header",
              "kind": "source",
              "assembly": "MyOrg.Http",
              "typeName": "MyOrg.Http.MyRequest",
              "memberName": "GetCustomHeader",
              "category": "network-input",
              "confidence": 0.7
            }
          ]
        }
        """);

        try
        {
            var builtIn = TaintPolicyLoader.LoadBuiltIn();
            var merged = TaintPolicyLoader.Load(userPolicyPath);

            Assert.Equal(builtIn.Rules.Count + 1, merged.Rules.Count);
            Assert.Contains(merged.Rules, r => r.Id == "src-custom-header");
            // Built-in rules must still be present, untouched.
            Assert.Contains(merged.Rules, r => r.Id == "src-env-var");
        }
        finally
        {
            File.Delete(userPolicyPath);
        }
    }

    [Fact]
    public void Load_WithUserPolicy_OverridesExistingIdInPlace()
    {
        var userPolicyPath = WriteTempPolicy("""
        {
          "$schema": "fennec.taint.policy.v1",
          "schemaVersion": "1.0.0",
          "policyId": "user-override",
          "rules": [
            {
              "id": "snk-process-start",
              "kind": "sink",
              "assembly": "System.Diagnostics.Process",
              "typeName": "System.Diagnostics.Process",
              "memberName": "Start",
              "argIndices": [0],
              "category": "command-injection",
              "severity": "low",
              "description": "Overridden by user policy"
            }
          ]
        }
        """);

        try
        {
            var builtIn = TaintPolicyLoader.LoadBuiltIn();
            var merged = TaintPolicyLoader.Load(userPolicyPath);

            Assert.Equal(builtIn.Rules.Count, merged.Rules.Count);
            var overridden = merged.Rules.Single(r => r.Id == "snk-process-start");
            Assert.Equal(TaintSeverity.Low, overridden.Severity);
            Assert.Equal("Overridden by user policy", overridden.Description);
        }
        finally
        {
            File.Delete(userPolicyPath);
        }
    }

    [Fact]
    public void Load_WithMalformedUserPolicy_ThrowsValidationErrorWithFilePath()
    {
        var userPolicyPath = WriteTempPolicy("{ not valid json ");

        try
        {
            var ex = Assert.Throws<TaintPolicyValidationException>(() => TaintPolicyLoader.Load(userPolicyPath));
            Assert.Equal(userPolicyPath, ex.FilePath);
            Assert.Contains(userPolicyPath, ex.Message);
        }
        finally
        {
            File.Delete(userPolicyPath);
        }
    }

    [Fact]
    public void Load_WithUserPolicyMissingRequiredField_ThrowsValidationErrorWithFieldName()
    {
        var userPolicyPath = WriteTempPolicy("""
        {
          "$schema": "fennec.taint.policy.v1",
          "schemaVersion": "1.0.0",
          "policyId": "user-override",
          "rules": [
            {
              "id": "snk-missing-severity",
              "kind": "sink",
              "assembly": "MyOrg",
              "typeName": "MyOrg.Thing",
              "memberName": "Do",
              "category": "command-injection"
            }
          ]
        }
        """);

        try
        {
            var ex = Assert.Throws<TaintPolicyValidationException>(() => TaintPolicyLoader.Load(userPolicyPath));
            Assert.Equal("rules[].severity", ex.FieldName);
        }
        finally
        {
            File.Delete(userPolicyPath);
        }
    }

    [Fact]
    public void Find_MatchesCaseInsensitively()
    {
        var policy = TaintPolicyLoader.Load();

        var match = policy.Find("system.runtime", "system.environment", "getenvironmentvariable");

        Assert.NotNull(match);
        Assert.Equal("src-env-var", match!.Id);
    }

    private static string WriteTempPolicy(string content)
    {
        var path = Path.Combine(Path.GetTempPath(), $"taint-policy-{Guid.NewGuid():N}.json");
        File.WriteAllText(path, content);
        return path;
    }
}
