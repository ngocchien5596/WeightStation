namespace StationApp.Application.Interfaces;

public interface IClock
{
    DateTime NowLocal { get; }
    DateTime TodayLocal { get; }
}
