using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using StationApp.Domain.Entities;
using StationApp.UI.Services;

namespace StationApp.UI.ViewModels.Dialogs;

public sealed partial class CameraImageHistoryViewModel : ObservableObject
{
    [ObservableProperty] private string _title;
    [ObservableProperty] private ObservableCollection<CameraImageItemViewModel> _images = new();
    [NotifyCanExecuteChangedFor(nameof(SaveSelectedImageCommand))]
    [ObservableProperty] private CameraImageItemViewModel? _selectedImage;

    private readonly IToastService? _toastService;
    private readonly string _vehiclePlate;

    public event EventHandler<bool>? CloseRequested;

    public CameraImageHistoryViewModel(
        IReadOnlyList<WeighingSessionImage> sessionImages,
        string vehiclePlate,
        IToastService? toastService = null)
    {
        _toastService = toastService;
        _vehiclePlate = vehiclePlate;
        _title = $"\u1ea2nh l\u1ecbch s\u1eed c\u00e2n - Xe: {vehiclePlate}";

        foreach (var img in sessionImages)
        {
            try
            {
                var imageSource = ByteArrayToImageSource(img.ImageBytes);
                if (imageSource == null)
                {
                    continue;
                }

                _images.Add(new CameraImageItemViewModel(
                    img.CameraName ?? img.CameraCode,
                    img.CaptureStage == Domain.Enums.CameraCaptureStage.WEIGHT1 ? "C\u00e2n l\u1ea7n 1" : "C\u00e2n l\u1ea7n 2",
                    img.CapturedAt,
                    imageSource,
                    img.ImageBytes,
                    img.ImageFormat));
            }
            catch
            {
                // Ignore conversion errors for individual corrupted images.
            }
        }

        if (_images.Count > 0)
        {
            _selectedImage = _images[0];
        }
    }

    [RelayCommand]
    private void Close()
    {
        CloseRequested?.Invoke(this, true);
    }

    private bool CanSaveSelectedImage() => SelectedImage != null;

    [RelayCommand(CanExecute = nameof(CanSaveSelectedImage))]
    private void SaveSelectedImage()
    {
        if (SelectedImage == null)
        {
            return;
        }

        try
        {
            var extension = NormalizeImageExtension(SelectedImage.ImageFormat);
            var saveDialog = new SaveFileDialog
            {
                Title = "L\u01b0u \u1ea3nh l\u1ecbch s\u1eed",
                Filter = BuildFilter(extension),
                DefaultExt = $".{extension}",
                AddExtension = true,
                OverwritePrompt = true,
                InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures),
                FileName = BuildDefaultFileName(_vehiclePlate, SelectedImage, extension)
            };

            if (saveDialog.ShowDialog() != true)
            {
                return;
            }

            File.WriteAllBytes(saveDialog.FileName, SelectedImage.ImageBytes);
            _toastService?.ShowSuccess($"\u0110\u00e3 l\u01b0u \u1ea3nh v\u00e0o {saveDialog.FileName}");
        }
        catch
        {
            _toastService?.ShowError("Kh\u00f4ng th\u1ec3 l\u01b0u \u1ea3nh l\u1ecbch s\u1eed.");
        }
    }

    private static ImageSource? ByteArrayToImageSource(byte[] bytes)
    {
        if (bytes == null || bytes.Length == 0)
        {
            return null;
        }

        try
        {
            using var ms = new MemoryStream(bytes);
            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.StreamSource = ms;
            image.EndInit();
            image.Freeze();
            return image;
        }
        catch
        {
            return null;
        }
    }

    private static string BuildDefaultFileName(string vehiclePlate, CameraImageItemViewModel image, string extension)
    {
        var safePlate = SanitizeFileNamePart(string.IsNullOrWhiteSpace(vehiclePlate) ? "khong-bien-so" : vehiclePlate.Trim());
        var safeStage = image.Stage.Contains("1", StringComparison.Ordinal) ? "can-lan-1" : "can-lan-2";
        var safeCamera = SanitizeFileNamePart(string.IsNullOrWhiteSpace(image.CameraName) ? "camera" : image.CameraName.Trim());
        return $"{safePlate}_{safeStage}_{safeCamera}_{image.CapturedAtValue:yyyyMMdd_HHmmss}.{extension}";
    }

    private static string SanitizeFileNamePart(string value)
    {
        var sanitized = value;
        foreach (var invalidChar in Path.GetInvalidFileNameChars())
        {
            sanitized = sanitized.Replace(invalidChar, '-');
        }

        return sanitized.Replace(' ', '-');
    }

    private static string NormalizeImageExtension(string? imageFormat)
    {
        if (string.IsNullOrWhiteSpace(imageFormat))
        {
            return "jpg";
        }

        var normalized = imageFormat.Trim().TrimStart('.').ToLowerInvariant();
        return normalized switch
        {
            "jpeg" => "jpg",
            "png" => "png",
            "bmp" => "bmp",
            _ => "jpg"
        };
    }

    private static string BuildFilter(string extension)
    {
        return extension switch
        {
            "png" => "PNG image (*.png)|*.png|All files (*.*)|*.*",
            "bmp" => "Bitmap image (*.bmp)|*.bmp|All files (*.*)|*.*",
            _ => "JPEG image (*.jpg)|*.jpg|All files (*.*)|*.*"
        };
    }
}

public sealed class CameraImageItemViewModel
{
    public string CameraName { get; }
    public string Stage { get; }
    public string CapturedAt => CapturedAtValue.ToString("dd/MM/yyyy HH:mm:ss");
    public DateTime CapturedAtValue { get; }
    public ImageSource Image { get; }
    public byte[] ImageBytes { get; }
    public string ImageFormat { get; }

    public string DisplayTitle => $"{Stage} - {CameraName} ({CapturedAt})";

    public CameraImageItemViewModel(
        string cameraName,
        string stage,
        DateTime capturedAt,
        ImageSource image,
        byte[] imageBytes,
        string imageFormat)
    {
        CameraName = cameraName;
        Stage = stage;
        CapturedAtValue = capturedAt;
        Image = image;
        ImageBytes = imageBytes;
        ImageFormat = imageFormat;
    }
}
