using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using FennecLabs.NuGet;

namespace FennecLabs.Scorecard;

public class ScorecardClient
{
    private readonly HttpClient _httpClient;
    private readonly NuGetService? _nugetService;
    private const string BaseUrl = "https://api.securityscorecards.dev";

    public ScorecardClient()
    {
        _httpClient = new HttpClient
        {
            BaseAddress = new Uri(BaseUrl)
        };
        _nugetService = new NuGetService();
    }

    public async Task<ScorecardResult?> GetScorecardResultAsync(
        string platform,
        string org,
        string repo,
        string? commit = null,
        CancellationToken cancellationToken = default)
    {
        var url = $"/projects/{Uri.EscapeDataString(platform)}/{Uri.EscapeDataString(org)}/{Uri.EscapeDataString(repo)}";
        
        if (!string.IsNullOrEmpty(commit))
        {
            url += $"?commit={Uri.EscapeDataString(commit)}";
        }

        try
        {
            var response = await _httpClient.GetAsync(url, cancellationToken);
            response.EnsureSuccessStatusCode();

            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };

            return await response.Content.ReadFromJsonAsync<ScorecardResult>(options, cancellationToken);
        }
        catch (HttpRequestException ex)
        {
            throw new InvalidOperationException($"Failed to retrieve scorecard result: {ex.Message}", ex);
        }
    }

    public async Task<ScorecardResult?> GetScorecardResultFromPackageAsync(
        string packageId,
        string? version = null,
        CancellationToken cancellationToken = default)
    {
        // Try to get repository URL from NuGet package metadata
        if (_nugetService != null)
        {
            var metadata = await _nugetService.GetPackageMetadataAsync(packageId, version, cancellationToken);
            if (metadata?.ProjectUrl != null)
            {
                var projectUrl = metadata.ProjectUrl.ToString();
                var (platform, org, repo) = ParseRepositoryUrl(projectUrl);
                if (platform != null && org != null && repo != null)
                {
                    return await GetScorecardResultAsync(platform, org, repo, commit: null, cancellationToken);
                }
            }

            // Fallback: Try to get repository URL from nuspec file
            var nuspecContent = await _nugetService.GetPackageNuspecContentAsync(packageId, version, cancellationToken);
            if (!string.IsNullOrWhiteSpace(nuspecContent))
            {
                var repositoryUrl = NuGetService.ExtractRepositoryUrlFromNuspec(nuspecContent);
                if (!string.IsNullOrWhiteSpace(repositoryUrl))
                {
                    var (platform, org, repo) = ParseRepositoryUrl(repositoryUrl);
                    if (platform != null && org != null && repo != null)
                    {
                        return await GetScorecardResultAsync(platform, org, repo, commit: null, cancellationToken);
                    }
                }
            }
        }

        // Fallback: Try to infer from package ID (heuristic)
        var parts = packageId.Split('.');
        if (parts.Length < 2)
        {
            throw new ArgumentException($"Cannot determine repository from package ID: {packageId}. Package metadata may not contain repository URL.");
        }

        // Common pattern: Microsoft.AspNetCore.App -> microsoft/aspnetcore
        var inferredOrg = parts[0].ToLowerInvariant();
        var inferredRepo = string.Join(".", parts.Skip(1)).ToLowerInvariant();
        
        return await GetScorecardResultAsync("github.com", inferredOrg, inferredRepo, commit: null, cancellationToken);
    }

    private static (string? platform, string? org, string? repo) ParseRepositoryUrl(string? url)
    {
        if (string.IsNullOrEmpty(url))
        {
            return (null, null, null);
        }

        // Match GitHub URLs: https://github.com/org/repo
        var githubMatch = Regex.Match(url, @"github\.com[/:]([^/]+)/([^/]+)", RegexOptions.IgnoreCase);
        if (githubMatch.Success)
        {
            var org = githubMatch.Groups[1].Value;
            var repo = githubMatch.Groups[2].Value.TrimEnd('/').Replace(".git", "");
            return ("github.com", org, repo);
        }

        return (null, null, null);
    }
}

