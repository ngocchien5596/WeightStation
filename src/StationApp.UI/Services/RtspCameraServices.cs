using OpenCvSharp;
using StationApp.Application.DTOs;
using StationApp.Application.Interfaces;

namespace StationApp.UI.Services;

public sealed class RtspCameraCaptureService : ICameraCaptureService
{
    public async Task<IReadOnlyList<CameraCaptureImageResult>> CaptureAsync(
        IReadOnlyList<CameraEndpointSettings> cameras,
        int timeoutMs,
        int jpegQuality,
        int maxDimension,
        int warmupFrames,
        CancellationToken ct)
    {
        var results = new List<CameraCaptureImageResult>();
        foreach (var camera in cameras.Where(x => x.IsEnabled && !string.IsNullOrWhiteSpace(x.CaptureRtspUrl)))
        {
            results.Add(await Task.Run(() => CaptureSingle(camera, timeoutMs, jpegQuality, maxDimension, warmupFrames, ct), ct));
        }

        return results;
    }

    private static CameraCaptureImageResult CaptureSingle(
        CameraEndpointSettings camera,
        int timeoutMs,
        int jpegQuality,
        int maxDimension,
        int warmupFrames,
        CancellationToken ct)
    {
        var startedAt = DateTime.Now;
        try
        {
            using var capture = new VideoCapture();
            var sanitizedUrl = RtspUrlHelper.SanitizeRtspUrl(camera.CaptureRtspUrl);
            if (!capture.Open(sanitizedUrl, VideoCaptureAPIs.FFMPEG))
            {
                return Failed(camera, "Khong mo duoc luong RTSP.");
            }

            using var frame = new Mat();
            var attempts = Math.Max(1, warmupFrames + 1);
            for (var i = 0; i < attempts; i++)
            {
                ct.ThrowIfCancellationRequested();
                if ((DateTime.Now - startedAt).TotalMilliseconds > timeoutMs)
                {
                    return Failed(camera, "Het thoi gian chup anh.");
                }

                if (!capture.Read(frame) || frame.Empty())
                {
                    Thread.Sleep(100);
                    continue;
                }
            }

            if (frame.Empty())
            {
                return Failed(camera, "Khong lay duoc frame hop le.");
            }

            using var processedFrame = new Mat();
            var finalFrame = frame;

            if (maxDimension > 0 && (frame.Width > maxDimension || frame.Height > maxDimension))
            {
                double scale = (double)maxDimension / Math.Max(frame.Width, frame.Height);
                int newWidth = (int)Math.Round(frame.Width * scale);
                int newHeight = (int)Math.Round(frame.Height * scale);
                Cv2.Resize(frame, processedFrame, new Size(newWidth, newHeight), 0, 0, InterpolationFlags.Area);
                finalFrame = processedFrame;
            }

            Cv2.ImEncode(".jpg", finalFrame, out var imageBytes, [(int)ImwriteFlags.JpegQuality, jpegQuality]);
            return new CameraCaptureImageResult(
                camera.CameraCode,
                camera.DisplayName,
                camera.CaptureRtspUrl,
                "jpg",
                imageBytes,
                DateTime.Now);
        }
        catch (Exception ex)
        {
            return Failed(camera, ex.Message);
        }
    }

    private static CameraCaptureImageResult Failed(CameraEndpointSettings camera, string error)
    {
        return new CameraCaptureImageResult(
            camera.CameraCode,
            camera.DisplayName,
            camera.CaptureRtspUrl,
            "jpg",
            Array.Empty<byte>(),
            DateTime.Now,
            error);
    }
}

internal static class RtspUrlHelper
{
    public static string SanitizeRtspUrl(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return url;
        }

        try
        {
            const string rtspPrefix = "rtsp://";
            if (!url.StartsWith(rtspPrefix, StringComparison.OrdinalIgnoreCase))
            {
                return url;
            }

            var authorityStart = rtspPrefix.Length;
            var authorityEnd = url.IndexOfAny(new[] { '/', '?' }, authorityStart);
            if (authorityEnd == -1)
            {
                authorityEnd = url.Length;
            }

            var authority = url[authorityStart..authorityEnd];
            var lastAtSign = authority.LastIndexOf('@');
            if (lastAtSign == -1)
            {
                return url;
            }

            var credentials = authority[..lastAtSign];
            var hostPort = authority[(lastAtSign + 1)..];

            string escapedCredentials;
            var colonIndex = credentials.IndexOf(':');
            if (colonIndex == -1)
            {
                var username = Uri.UnescapeDataString(credentials);
                escapedCredentials = Uri.EscapeDataString(username);
            }
            else
            {
                var username = Uri.UnescapeDataString(credentials[..colonIndex]);
                var password = Uri.UnescapeDataString(credentials[(colonIndex + 1)..]);
                escapedCredentials = $"{Uri.EscapeDataString(username)}:{Uri.EscapeDataString(password)}";
            }

            var pathAndQuery = url[authorityEnd..];
            return $"{rtspPrefix}{escapedCredentials}@{hostPort}{pathAndQuery}";
        }
        catch
        {
            return url;
        }
    }
}
