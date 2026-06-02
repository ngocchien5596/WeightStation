using StationApp.Application.Services;
using StationApp.Application.DTOs;
using System.Text.Json;
using Xunit;

namespace StationApp.Application.Tests;

public sealed class AppUpdateVersionComparerTests
{
    [Theory]
    [InlineData("1.0.0", "1.0.1", -1)]
    [InlineData("1.0.2", "1.0.1", 1)]
    [InlineData("2026.601.1030", "2026.601.1030", 0)]
    [InlineData("1.0", "1.0.0.0", 0)]
    public void Compare_ReturnsExpectedResult(string left, string right, int expectedSign)
    {
        var result = AppUpdateVersionComparer.Compare(left, right);

        Assert.Equal(expectedSign, Math.Sign(result));
    }

    [Fact]
    public void TryParse_NormalizesMissingBuildAndRevision()
    {
        var ok = AppUpdateVersionComparer.TryParse("1.2", out var version);

        Assert.True(ok);
        Assert.NotNull(version);
        Assert.Equal(new Version(1, 2, 0, 0), version);
    }

    [Fact]
    public void TryParse_IgnoresBuildMetadataSuffix()
    {
        var ok = AppUpdateVersionComparer.TryParse("2026.601.1245+abc123", out var version);

        Assert.True(ok);
        Assert.NotNull(version);
        Assert.Equal(new Version(2026, 601, 1245, 0), version);
    }

    [Fact]
    public void NormalizeString_RemovesBuildMetadataWithoutAddingTrailingZeroSegments()
    {
        var normalized = AppUpdateVersionComparer.NormalizeString("1.2+local");

        Assert.Equal("1.2", normalized);
    }

    [Fact]
    public void NormalizeString_PreservesThreePartReleaseFormat()
    {
        var normalized = AppUpdateVersionComparer.NormalizeString("2026.601.1305");

        Assert.Equal("2026.601.1305", normalized);
    }

    [Fact]
    public void AppUpdateManifest_DeserializesCamelCaseJson()
    {
        const string json = """
            {
              "version": "1.0.2",
              "packageName": "StationApp_1.0.2_20260601_162708.zip",
              "packagePath": "\\\\10.0.0.3\\17. data dung chung\\Chienbn\\Phan_mem_can\\StationApp_1.0.2_20260601_162708.zip",
              "sha256": "BB437E81E5391FF3CF1EDBD411492C13518B2E58A1CB8F249DC73B6911F2B3B5",
              "publishedAt": "2026-06-01T16:28:51.8160062+07:00",
              "releaseNotes": "Fix bug can xuat khau",
              "dbMigratorRequired": true,
              "minSupportedVersion": "1.0.0"
            }
            """;

        var manifest = JsonSerializer.Deserialize<AppUpdateManifest>(json);

        Assert.NotNull(manifest);
        Assert.Equal("1.0.2", manifest!.Version);
        Assert.Equal("StationApp_1.0.2_20260601_162708.zip", manifest.PackageName);
        Assert.True(manifest.DbMigratorRequired);
        Assert.Equal("1.0.0", manifest.MinSupportedVersion);
    }

}
