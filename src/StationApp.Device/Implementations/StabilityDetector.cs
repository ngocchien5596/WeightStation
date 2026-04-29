namespace StationApp.Device.Implementations;

public sealed class StabilityDetector
{
    private readonly decimal _threshold;
    private readonly int _requiredCycles;
    private readonly Queue<decimal> _readings = new();

    public StabilityDetector(decimal threshold = 5m, int requiredCycles = 3)
    {
        _threshold = threshold;
        _requiredCycles = requiredCycles;
    }

    public bool AddReading(decimal weight)
    {
        _readings.Enqueue(weight);
        if (_readings.Count > _requiredCycles)
            _readings.Dequeue();

        if (_readings.Count < _requiredCycles)
            return false;

        var max = _readings.Max();
        var min = _readings.Min();
        return (max - min) <= _threshold;
    }

    public void Reset() => _readings.Clear();
}
