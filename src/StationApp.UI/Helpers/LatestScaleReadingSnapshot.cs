using System;

namespace StationApp.UI.Helpers;

public class LatestScaleReadingSnapshot
{
    public decimal Weight { get; set; }
    public bool IsStable { get; set; }
    public DateTime ReceivedAt { get; set; }
}
