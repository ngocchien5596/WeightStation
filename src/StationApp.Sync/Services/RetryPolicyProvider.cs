namespace StationApp.Sync.Services;

public static class RetryPolicyProvider
{
    private static readonly int[] RetryDelaysSeconds = { 30, 120, 600, 1800, 7200 };

    public static DateTime GetNextRetryAt(int retryCount, DateTime now)
    {
        var index = Math.Min(retryCount, RetryDelaysSeconds.Length - 1);
        return now.AddSeconds(RetryDelaysSeconds[index]);
    }

    public static bool IsMaxRetryReached(int retryCount)
        => retryCount >= RetryDelaysSeconds.Length;
}
