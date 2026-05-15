using System.IO.Ports;
using Microsoft.Extensions.Logging;
using StationApp.Device.Abstractions;
using StationApp.Device.Models;
using StationApp.Domain.Enums;

namespace StationApp.Device.Implementations;

public sealed class SerialScaleDevice : IScaleDevice, IDisposable
{
    public event EventHandler<ScaleReading>? WeightReceived;
    public event EventHandler<string>? DiagnosticsReceived;
    public event EventHandler<string>? ErrorOccurred;
    public bool IsConnected { get; private set; }
    public string ConnectionState { get; private set; } = "Disconnected";
    public DateTimeOffset? NextReconnectAtUtc { get; private set; }

    private SerialPort? _port;
    private readonly IWeightFrameParser _parser;
    private readonly StabilityDetector _stabilityDetector;
    private readonly ILogger<SerialScaleDevice>? _logger;
    private readonly Func<CancellationToken, Task<SerialScaleDeviceConfiguration?>>? _configurationProvider;

    private string _comPort;
    private int _baudRate;
    private Parity _parity;
    private int _dataBits;
    private StopBits _stopBits;

    private CancellationTokenSource? _reconnectCts;
    private Task? _reconnectTask;
    private readonly SemaphoreSlim _connectionGate = new(1, 1);
    private int _reconnectAttempts;
    private bool _lastFailureCanAutoReconnect = true;
    private const int MaxReconnectAttempts = 10;
    private const int ReconnectBaseDelayMs = 2000;
    private static readonly TimeSpan ReconnectCooldown = TimeSpan.FromMinutes(5);

    private string _lastRawFrame = "";
    public string LastRawFrame => _lastRawFrame;
    public string ParserTypeName => _parser.GetType().Name;
    public string? LastError { get; private set; }

    public SerialScaleDevice(
        string comPort, int baudRate, IWeightFrameParser parser,
        StabilityDetector stabilityDetector,
        ILogger<SerialScaleDevice>? logger = null,
        Func<CancellationToken, Task<SerialScaleDeviceConfiguration?>>? configurationProvider = null,
        Parity parity = Parity.None, int dataBits = 8, StopBits stopBits = StopBits.One)
    {
        _comPort = comPort;
        _baudRate = baudRate;
        _parser = parser;
        _stabilityDetector = stabilityDetector;
        _logger = logger;
        _configurationProvider = configurationProvider;
        _parity = parity;
        _dataBits = dataBits;
        _stopBits = stopBits;
    }

    public async Task ConnectAsync(CancellationToken ct)
    {
        CancelReconnect();

        if (IsConnected)
        {
            return;
        }

        var connected = await TryOpenPortAsync(isReconnect: false, ct);
        if (!connected && _lastFailureCanAutoReconnect)
        {
            ScheduleReconnect();
        }
    }

    public async Task DisconnectAsync(CancellationToken ct)
    {
        CancelReconnect();
        await _connectionGate.WaitAsync(ct);
        try
        {
            ClosePort();
            IsConnected = false;
            NextReconnectAtUtc = null;
            ConnectionState = "Disconnected";
        }
        finally
        {
            _connectionGate.Release();
        }

        IsConnected = false;
        _logger?.LogInformation("Serial port {Port} disconnected", _comPort);
    }

    public Task StartAsync(CancellationToken ct) => Task.CompletedTask;
    public Task StopAsync(CancellationToken ct) => DisconnectAsync(ct);

    private void OnDataReceived(object sender, SerialDataReceivedEventArgs e)
    {
        try
        {
            var raw = _port?.ReadExisting();
            if (string.IsNullOrEmpty(raw)) return;

            _lastRawFrame = raw;
            DiagnosticsReceived?.Invoke(this, raw);

            var weight = _parser.TryParse(raw, out var frameStable);
            if (weight.HasValue)
            {
                var detectorStable = _stabilityDetector.AddReading(weight.Value);
                TimeZoneInfo vnTimeZone;
                try { vnTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Ho_Chi_Minh"); }
                catch (TimeZoneNotFoundException) { vnTimeZone = TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time"); }
                var capturedAt = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, vnTimeZone);

                WeightReceived?.Invoke(this, new ScaleReading
                {
                    Weight = weight.Value,
                    IsStable = frameStable || detectorStable,
                    Mode = WeightMode.AUTO,
                    CapturedAt = capturedAt,
                    RawPayload = raw
                });
            }
        }
        catch (Exception ex)
        {
            LastError = $"Read error: {ex.Message}";
            _logger?.LogWarning(ex, "Error reading from serial port {Port}", _comPort);
            ErrorOccurred?.Invoke(this, LastError);
        }
    }

    private void OnErrorReceived(object sender, SerialErrorReceivedEventArgs e)
    {
        LastError = $"Serial error: {e.EventType}";
        _logger?.LogError("Serial port error on {Port}: {EventType}", _comPort, e.EventType);
        ErrorOccurred?.Invoke(this, LastError);

        IsConnected = false;
        ConnectionState = "Disconnected";
        ScheduleReconnect();
    }

    private void ScheduleReconnect()
    {
        if (_reconnectTask is { IsCompleted: false }) return;

        _reconnectCts = new CancellationTokenSource();
        _reconnectTask = ReconnectLoopAsync(_reconnectCts.Token);
    }

    private async Task ReconnectLoopAsync(CancellationToken ct)
    {
        var rand = new Random();
        while (!ct.IsCancellationRequested)
        {
            _reconnectAttempts++;

            var exponent = Math.Min(_reconnectAttempts - 1, 5);
            var baseDelay = ReconnectBaseDelayMs * Math.Pow(2, exponent);
            if (baseDelay > 60000) baseDelay = 60000;
            var delay = (int)baseDelay + rand.Next(0, 500); // Add jitter
            NextReconnectAtUtc = DateTimeOffset.UtcNow.AddMilliseconds(delay);
            ConnectionState = "ReconnectWaiting";

            _logger?.LogInformation("Reconnect attempt {Attempt}/{Max} in {Delay}ms for {Port}",
                _reconnectAttempts, MaxReconnectAttempts, delay, _comPort);

            await Task.Delay(delay, ct);

            var connected = await TryOpenPortAsync(isReconnect: true, ct);
            if (connected)
            {
                return;
            }

            if (!_lastFailureCanAutoReconnect)
            {
                ConnectionState = "Faulted";
                NextReconnectAtUtc = null;
                return;
            }

            if (_reconnectAttempts < MaxReconnectAttempts)
            {
                continue;
            }

            LastError = $"Reconnect failed after {MaxReconnectAttempts} attempts. Entering cooldown.";
            ConnectionState = "Faulted";
            NextReconnectAtUtc = DateTimeOffset.UtcNow.Add(ReconnectCooldown);
            _logger?.LogCritical("Exhausted reconnect attempts for serial port {Port}. Entering cooldown until {UntilUtc}.", _comPort, NextReconnectAtUtc);
            ErrorOccurred?.Invoke(this, LastError);

            try
            {
                await Task.Delay(ReconnectCooldown, ct);
                _reconnectAttempts = 0;
                ConnectionState = "ReconnectWaiting";
            }
            catch (TaskCanceledException) { }
        }
    }

    private async Task<bool> TryOpenPortAsync(bool isReconnect, CancellationToken ct)
    {
        await _connectionGate.WaitAsync(ct);
        try
        {
            ConnectionState = isReconnect ? "Connecting" : "Connecting";
            await ApplyConfigurationAsync(ct);

            ClosePort();
            var openedPort = await Task.Run(() => CreateAndOpenPort(ct), ct);
            openedPort.DataReceived += OnDataReceived;
            openedPort.ErrorReceived += OnErrorReceived;
            _port = openedPort;

            IsConnected = true;
            ConnectionState = "Connected";
            LastError = null;
            NextReconnectAtUtc = null;
            _reconnectAttempts = 0;

            if (_parser is YaohuaWeightFrameParser yp)
            {
                yp.ResetBuffer();
            }

            _stabilityDetector.Reset();
            _logger?.LogInformation("Serial port {Port} opened at {BaudRate} baud", _comPort, _baudRate);
            return true;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _lastFailureCanAutoReconnect = CanAutoReconnect(ex);
            IsConnected = false;
            ConnectionState = _lastFailureCanAutoReconnect ? "Disconnected" : "Faulted";
            LastError = $"{(isReconnect ? "Reconnect" : "Connect")} failed: {ex.Message}";

            if (isReconnect)
            {
                _logger?.LogWarning(ex, "Reconnect attempt {Attempt} failed for {Port}", _reconnectAttempts, _comPort);
            }
            else
            {
                _logger?.LogError(ex, "Failed to open serial port {Port}", _comPort);
            }

            ErrorOccurred?.Invoke(this, LastError);
            return false;
        }
        finally
        {
            _connectionGate.Release();
        }
    }

    private SerialPort CreateAndOpenPort(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        var port = new SerialPort(_comPort, _baudRate, _parity, _dataBits, _stopBits)
        {
            ReadTimeout = 3000,
            WriteTimeout = 3000,
            ReadBufferSize = 4096
        };

        try
        {
            port.Open();
            return port;
        }
        catch
        {
            port.Dispose();
            throw;
        }
    }

    private static bool CanAutoReconnect(Exception ex)
    {
        return ex switch
        {
            UnauthorizedAccessException => false,
            FileNotFoundException => false,
            IOException ioEx when ioEx.Message.Contains("semaphore timeout", StringComparison.OrdinalIgnoreCase) => false,
            _ => true
        };
    }

    private async Task ApplyConfigurationAsync(CancellationToken ct)
    {
        if (_configurationProvider == null)
        {
            return;
        }

        var configuration = await _configurationProvider(ct);
        if (configuration == null)
        {
            return;
        }

        if (!string.IsNullOrWhiteSpace(configuration.ComPort))
        {
            _comPort = configuration.ComPort.Trim();
        }

        if (configuration.BaudRate > 0)
        {
            _baudRate = configuration.BaudRate;
        }

        _parity = ScaleConnectionSettings.ResolveParity(configuration.Parity, _parity);
        _dataBits = ScaleConnectionSettings.ResolveDataBits(configuration.DataBits, _dataBits);
        _stopBits = ScaleConnectionSettings.ResolveStopBits(configuration.StopBits, _stopBits);
        _stabilityDetector.Configure(requiredCycles: configuration.StableCycles);

        if (_parser is ConfigurableWeightFrameParser configurableParser)
        {
            configurableParser.ApplyConfiguration(
                configuration.ParserType,
                configuration.FrameEndChar,
                configuration.WeightSubstringStart,
                configuration.WeightSubstringLength);
        }
        else if (_parser is YaohuaWeightFrameParser parser)
        {
            parser.WeightSubstringStart = configuration.WeightSubstringStart;
            parser.WeightSubstringLength = configuration.WeightSubstringLength;
        }
    }


    private void CancelReconnect()
    {
        _reconnectCts?.Cancel();
        _reconnectCts?.Dispose();
        _reconnectCts = null;
    }

    private void ClosePort()
    {
        if (_port == null) return;
        try
        {
            _port.DataReceived -= OnDataReceived;
            _port.ErrorReceived -= OnErrorReceived;
            if (_port.IsOpen) _port.Close();
            _port.Dispose();
        }
        catch { }
        finally
        {
            _port = null;
        }
    }

    public void Dispose()
    {
        CancelReconnect();
        ClosePort();
        _connectionGate.Dispose();
    }
}

public sealed record SerialScaleDeviceConfiguration(
    string? ComPort,
    int BaudRate,
    string? Parity,
    string? DataBits,
    string? StopBits,
    string? ParserType,
    string? FrameEndChar,
    int? StableCycles,
    int? WeightSubstringStart,
    int? WeightSubstringLength);
