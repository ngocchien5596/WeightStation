using StationApp.Device.Abstractions;
using StationApp.Device.Models;
using StationApp.Domain.Enums;

namespace StationApp.Device.Implementations;

public sealed class SimulatorScaleDevice : IScaleDevice
{
    public event EventHandler<ScaleReading>? WeightReceived;
    public bool IsConnected { get; private set; }

    private CancellationTokenSource? _cts;
    private Task? _emitTask;
    private readonly Random _random = new();
    private decimal _currentWeight = 0m;
    private int _stableCounter = 0;
    private decimal _targetWeight;
    private bool _isRamping = true;

    public Task ConnectAsync(CancellationToken ct)
    {
        IsConnected = true;
        _targetWeight = _random.Next(15000, 45000);
        return Task.CompletedTask;
    }

    public Task DisconnectAsync(CancellationToken ct)
    {
        IsConnected = false;
        return Task.CompletedTask;
    }

    public Task StartAsync(CancellationToken ct)
    {
        _cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        _emitTask = EmitLoop(_cts.Token);
        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken ct)
    {
        _cts?.Cancel();
        if (_emitTask != null)
        {
            try { await _emitTask; } catch (OperationCanceledException) { }
        }
    }

    private async Task EmitLoop(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            if (_isRamping)
            {
                var step = (_targetWeight - _currentWeight) * 0.3m + _random.Next(-50, 50);
                _currentWeight += step;
                if (Math.Abs(_currentWeight - _targetWeight) < 100)
                {
                    _currentWeight = _targetWeight;
                    _isRamping = false;
                    _stableCounter = 0;
                }
            }
            else
            {
                _currentWeight = _targetWeight + _random.Next(-5, 6);
                _stableCounter++;
            }

            var isStable = _stableCounter >= 3;
            
            TimeZoneInfo vnTimeZone;
            try { vnTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Ho_Chi_Minh"); }
            catch (TimeZoneNotFoundException) { vnTimeZone = TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time"); }
            var capturedAt = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, vnTimeZone);

            WeightReceived?.Invoke(this, new ScaleReading
            {
                Weight = Math.Round(Math.Max(0, _currentWeight), 3),
                IsStable = isStable,
                Mode = WeightMode.AUTO,
                CapturedAt = capturedAt,
                RawPayload = $"SIM:{_currentWeight:F3}"
            });

            await Task.Delay(500, ct);
        }
    }

    public void SetTargetWeight(decimal target)
    {
        _targetWeight = target;
        _isRamping = true;
        _stableCounter = 0;
    }
}
