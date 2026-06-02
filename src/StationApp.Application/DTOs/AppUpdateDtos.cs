using System.Text.Json.Serialization;

namespace StationApp.Application.DTOs;

public sealed record AppUpdateManifest(
    [property: JsonPropertyName("version")]
    string Version,
    [property: JsonPropertyName("packageName")]
    string PackageName,
    [property: JsonPropertyName("packagePath")]
    string PackagePath,
    [property: JsonPropertyName("sha256")]
    string Sha256,
    [property: JsonPropertyName("publishedAt")]
    DateTimeOffset PublishedAt,
    [property: JsonPropertyName("releaseNotes")]
    string? ReleaseNotes,
    [property: JsonPropertyName("dbMigratorRequired")]
    bool DbMigratorRequired,
    [property: JsonPropertyName("minSupportedVersion")]
    string? MinSupportedVersion);

public sealed record AppUpdateCheckResult(
    string CurrentVersion,
    bool IsUpdateAvailable,
    AppUpdateManifest? Manifest = null,
    string? ErrorMessage = null)
{
    public bool Success => string.IsNullOrWhiteSpace(ErrorMessage);
}
