namespace StationApp.Device.Implementations;

public sealed class StabilityDetector
{
    private decimal _threshold;
    private int _requiredCycles;
    private readonly Queue<decimal> _readings = new();

    public StabilityDetector(decimal threshold = 5m, int requiredCycles = 3)
    {
        _threshold = threshold;
        _requiredCycles = requiredCycles;
    }

    public void Configure(decimal? threshold = null, int? requiredCycles = null)
    {
        if (threshold.HasValue && threshold.Value > 0)
        {
            _threshold = threshold.Value;
        }

        if (requiredCycles.HasValue && requiredCycles.Value > 0)
        {
            _requiredCycles = requiredCycles.Value;
            while (_readings.Count > _requiredCycles)
            {
                _readings.Dequeue();
            }
        }
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
