namespace StationApp.Application.Services;

public static class AppUpdateVersionComparer
{
    private const string DefaultVersion = "1.0.0";

    public static bool TryParse(string? raw, out Version? version)
    {
        version = null;
        if (string.IsNullOrWhiteSpace(raw))
        {
            return false;
        }

        var normalizedRaw = StripMetadata(raw);
        if (Version.TryParse(normalizedRaw, out var parsed))
        {
            version = Normalize(parsed);
            return true;
        }

        return false;
    }

    public static string NormalizeString(string? raw, string fallback = DefaultVersion)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return fallback;
        }

        if (!TryParse(raw, out var version))
        {
            return fallback;
        }

        var normalizedRaw = StripMetadata(raw!);
        var componentCount = normalizedRaw
            .Split('.', StringSplitOptions.RemoveEmptyEntries)
            .Length;

        return componentCount switch
        {
            >= 4 => version!.ToString(4),
            3 => version!.ToString(3),
            2 => version!.ToString(2),
            _ => version!.ToString()
        };
    }

    public static int Compare(string? left, string? right)
    {
        var hasLeft = TryParse(left, out var leftVersion);
        var hasRight = TryParse(right, out var rightVersion);

        if (!hasLeft && !hasRight)
        {
            return 0;
        }

        if (!hasLeft)
        {
            return -1;
        }

        if (!hasRight)
        {
            return 1;
        }

        return leftVersion!.CompareTo(rightVersion);
    }

    private static Version Normalize(Version version)
    {
        var build = version.Build < 0 ? 0 : version.Build;
        var revision = version.Revision < 0 ? 0 : version.Revision;
        return new Version(version.Major, version.Minor, build, revision);
    }

    private static string StripMetadata(string raw)
    {
        var normalized = raw.Trim();

        var plusIndex = normalized.IndexOf('+');
        if (plusIndex >= 0)
        {
            normalized = normalized[..plusIndex];
        }

        return normalized.Trim();
    }
}
