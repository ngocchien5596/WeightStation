using System;

namespace StationApp.Domain.Entities;

public class DocumentCounter
{
    public string CounterKey { get; set; } = string.Empty;
    public int LastValue { get; set; }
    public DateTime UpdatedAt { get; set; }
}
