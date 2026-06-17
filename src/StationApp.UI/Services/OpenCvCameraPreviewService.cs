using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using OpenCvSharp;
using StationApp.Application.DTOs;

namespace StationApp.UI.Services;

public sealed class OpenCvCameraPreviewService : ICameraPreviewService
{
    private static readonly TimeSpan OpenRetryDelay = TimeSpan.FromSeconds(3);
    private static readonly TimeSpan RenderInterval = TimeSpan.FromMilliseconds(100);
    private readonly object _syncRoot = new();
    private readonly object _frameLock = new();

    private CancellationTokenSource? _previewCts;
    private Task? _captureTask;
    private Task? _renderTask;
    private Mat? _latestFrame;
    private Guid _previewSessionId;
    private long _latestCapturedSequence;
    private long _lastRaisedSequence;
    private long _droppedByRenderLoop;
    private DateTimeOffset _latestCapturedAt;
    private bool _isPreviewRunning;
    private bool _isDisposed;

    static OpenCvCameraPreviewService()
    {
        try
        {
            Environment.SetEnvironmentVariable(
                "OPENCV_FFMPEG_CAPTURE_OPTIONS",
                "rtsp_transport;tcp|stimeout;5000000|fflags;nobuffer|flags;low_delay|max_delay;500000|reorder_queue_size;0");
        }
        catch
        {
            // ignore environment setup failures
        }
    }

    public event EventHandler<CameraPreviewStatusChangedEventArgs>? StatusChanged;
    public event EventHandler<CameraPreviewFrameReceivedEventArgs>? FrameReceived;

    public string? ActiveCameraCode { get; private set; }
    public Guid? ActivePreviewSessionId => _previewSessionId == Guid.Empty ? null : _previewSessionId;
    public bool IsPreviewRunning => _isPreviewRunning;

    public async Task StartPreviewAsync(CameraEndpointSettings camera, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        await StopPreviewAsync().ConfigureAwait(false);

        var previewSessionId = Guid.NewGuid();
        var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct);

        lock (_syncRoot)
        {
            _previewCts = linkedCts;
            ActiveCameraCode = camera.CameraCode;
            _previewSessionId = previewSessionId;
            _latestCapturedSequence = 0;
            _lastRaisedSequence = 0;
            _droppedByRenderLoop = 0;
            _latestCapturedAt = default;
            DisposeLatestFrameUnsafe();

            _captureTask = Task.Run(
                () => CaptureLoopAsync(camera.CameraCode, RtspUrlHelper.SanitizeRtspUrl(camera.EffectivePreviewRtspUrl), linkedCts.Token),
                linkedCts.Token);
            _renderTask = Task.Run(
                () => RenderLoopAsync(camera.CameraCode, previewSessionId, linkedCts.Token),
                linkedCts.Token);
        }
    }

    public async Task StopPreviewAsync()
    {
        CancellationTokenSource? ctsToCancel;
        Task? captureTask;
        Task? renderTask;

        lock (_syncRoot)
        {
            ctsToCancel = _previewCts;
            captureTask = _captureTask;
            renderTask = _renderTask;

            _previewCts = null;
            _captureTask = null;
            _renderTask = null;
            ActiveCameraCode = null;
            _previewSessionId = Guid.Empty;
            _latestCapturedSequence = 0;
            _lastRaisedSequence = 0;
            _droppedByRenderLoop = 0;
            _latestCapturedAt = default;
            _isPreviewRunning = false;
        }

        if (ctsToCancel != null)
        {
            try
            {
                ctsToCancel.Cancel();
            }
            catch
            {
                // ignore cancellation failures
            }
        }

        try
        {
            var tasks = new[] { captureTask, renderTask };
            foreach (var task in tasks)
            {
                if (task == null)
                {
                    continue;
                }

                try
                {
                    await task.ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    // expected during stop
                }
                catch
                {
                    // ignore task stop failures
                }
            }
        }
        finally
        {
            lock (_frameLock)
            {
                DisposeLatestFrameUnsafe();
            }

            ctsToCancel?.Dispose();
        }
    }

    private async Task CaptureLoopAsync(
        string cameraCode,
        string rtspUrl,
        CancellationToken token)
    {
        var reconnectCount = 0;
        var emptyFrameCount = 0;

        using var capture = new VideoCapture();

        while (!token.IsCancellationRequested)
        {
            try
            {
                if (!capture.IsOpened())
                {
                    PublishStatus(cameraCode, "Đang kết nối");
                    _isPreviewRunning = false;

                    var opened = false;
                    try
                    {
                        opened = capture.Open(rtspUrl, VideoCaptureAPIs.FFMPEG);
                        capture.Set(VideoCaptureProperties.BufferSize, 1);
                    }
                    catch
                    {
                        // handled below by open status check
                    }

                    if (!opened || !capture.IsOpened())
                    {
                        reconnectCount++;
                        PublishStatus(cameraCode, $"Không kết nối được camera (Thử lại lần {reconnectCount})");
                        await Task.Delay(OpenRetryDelay, token).ConfigureAwait(false);
                        continue;
                    }

                    PublishStatus(cameraCode, "Đang preview");
                    _isPreviewRunning = true;
                    emptyFrameCount = 0;
                }

                using var frame = new Mat();
                var readSuccess = false;
                try
                {
                    readSuccess = capture.Read(frame);
                }
                catch
                {
                    // handled below as failed read
                }

                if (!readSuccess || frame.Empty())
                {
                    emptyFrameCount++;
                    if (emptyFrameCount > 30)
                    {
                        reconnectCount++;
                        PublishStatus(cameraCode, "Mất tín hiệu camera (Đang kết nối lại...)");
                        capture.Release();
                        _isPreviewRunning = false;
                        emptyFrameCount = 0;
                        await Task.Delay(OpenRetryDelay, token).ConfigureAwait(false);
                    }

                    await Task.Delay(1, token).ConfigureAwait(false);
                    continue;
                }

                emptyFrameCount = 0;
                Interlocked.Increment(ref _latestCapturedSequence);
                var capturedAt = DateTimeOffset.Now;
                lock (_frameLock)
                {
                    DisposeLatestFrameUnsafe();
                    _latestFrame = frame.Clone();
                    _latestCapturedAt = capturedAt;
                }

                await Task.Delay(1, token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch
            {
                reconnectCount++;
                _isPreviewRunning = false;
                PublishStatus(cameraCode, "Lỗi camera (Đang kết nối lại...)");
                capture.Release();
                await Task.Delay(OpenRetryDelay, token).ConfigureAwait(false);
            }
        }

        _isPreviewRunning = false;
        capture.Release();
    }

    private async Task RenderLoopAsync(
        string cameraCode,
        Guid previewSessionId,
        CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            Mat? frameToRender = null;
            long sequenceToRender = 0;
            DateTimeOffset capturedAt = default;

            try
            {
                lock (_frameLock)
                {
                    if (_latestFrame != null)
                    {
                        var latestSequence = Interlocked.Read(ref _latestCapturedSequence);
                        var lastRaisedSequence = Interlocked.Read(ref _lastRaisedSequence);
                        if (latestSequence > lastRaisedSequence)
                        {
                            frameToRender = _latestFrame.Clone();
                            sequenceToRender = latestSequence;
                            capturedAt = _latestCapturedAt;
                        }
                    }
                }

                if (frameToRender != null)
                {
                    var lastRaisedBeforeRender = Interlocked.Read(ref _lastRaisedSequence);
                    var droppedCount = Math.Max(0, sequenceToRender - lastRaisedBeforeRender - 1);
                    if (droppedCount > 0)
                    {
                        Interlocked.Add(ref _droppedByRenderLoop, droppedCount);
                    }

                    using var processedFrame = ResizeForPreviewIfNeeded(frameToRender, 640);
                    var bitmapSource = MatToBitmapSource(processedFrame);
                    if (bitmapSource != null)
                    {
                        Interlocked.Exchange(ref _lastRaisedSequence, sequenceToRender);
                        FrameReceived?.Invoke(this, new CameraPreviewFrameReceivedEventArgs
                        {
                            CameraCode = cameraCode,
                            PreviewSessionId = previewSessionId,
                            Sequence = sequenceToRender,
                            CapturedAt = capturedAt,
                            FrameWidth = processedFrame.Width,
                            FrameHeight = processedFrame.Height,
                            Frame = bitmapSource
                        });
                    }
                }

                await Task.Delay(RenderInterval, token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch
            {
                await Task.Delay(RenderInterval, token).ConfigureAwait(false);
            }
            finally
            {
                frameToRender?.Dispose();
            }
        }
    }

    private static Mat ResizeForPreviewIfNeeded(Mat frame, int maxWidth)
    {
        if (frame.Width <= maxWidth)
        {
            return frame.Clone();
        }

        var newWidth = maxWidth;
        var newHeight = (frame.Height * newWidth) / frame.Width;
        var resized = new Mat();
        Cv2.Resize(frame, resized, new OpenCvSharp.Size(newWidth, newHeight));
        return resized;
    }

    private static BitmapSource? MatToBitmapSource(Mat mat)
    {
        if (mat.IsDisposed || mat.Empty())
        {
            return null;
        }

        var width = mat.Width;
        var height = mat.Height;
        var stride = (int)mat.Step();

        var pixelFormat = PixelFormats.Bgr24;
        if (mat.Channels() == 1)
        {
            pixelFormat = PixelFormats.Gray8;
        }
        else if (mat.Channels() == 4)
        {
            pixelFormat = PixelFormats.Bgra32;
        }

        var bitmapSource = BitmapSource.Create(
            width,
            height,
            96.0,
            96.0,
            pixelFormat,
            null,
            mat.Data,
            stride * height,
            stride);

        bitmapSource.Freeze();
        return bitmapSource;
    }

    private void PublishStatus(string? cameraCode, string statusText)
    {
        try
        {
            StatusChanged?.Invoke(this, new CameraPreviewStatusChangedEventArgs
            {
                CameraCode = cameraCode,
                StatusText = statusText
            });
        }
        catch
        {
            // ignore status callback failures
        }
    }

    private void DisposeLatestFrameUnsafe()
    {
        _latestFrame?.Dispose();
        _latestFrame = null;
    }

    public void AttachHostWindow(IntPtr hostHandle, int width, int height)
    {
        // No-op for OpenCV in-process preview.
    }

    public void ResizeHostWindow(int width, int height)
    {
        // No-op for OpenCV in-process preview.
    }

    public void DetachHostWindow()
    {
        // No-op for OpenCV in-process preview.
    }

    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;
        try
        {
            _ = StopPreviewAsync();
        }
        catch
        {
            // ignore dispose failures
        }
    }
}
