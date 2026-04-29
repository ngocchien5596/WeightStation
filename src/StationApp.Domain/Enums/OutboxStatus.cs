namespace StationApp.Domain.Enums;

public enum OutboxStatus
{
    PENDING = 1,
    PROCESSING = 2,
    SUCCESS = 3,
    FAILED_RETRYABLE = 4,
    FAILED_FINAL = 5
}
