using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StationApp.Domain.Entities;

namespace StationApp.UI.ViewModels.Dialogs;

public sealed partial class CameraImageHistoryViewModel : ObservableObject
{
    [ObservableProperty] private string _title;
    [ObservableProperty] private ObservableCollection<CameraImageItemViewModel> _images = new();
    [ObservableProperty] private CameraImageItemViewModel? _selectedImage;

    public event EventHandler<bool>? CloseRequested;

    public CameraImageHistoryViewModel(IReadOnlyList<WeighingSessionImage> sessionImages, string vehiclePlate)
    {
        _title = $"Ảnh lịch sử cân - Xe: {vehiclePlate}";
        foreach (var img in sessionImages)
        {
            try
            {
                var imageSource = ByteArrayToImageSource(img.ImageBytes);
                if (imageSource != null)
                {
                    _images.Add(new CameraImageItemViewModel(
                        img.CameraName ?? img.CameraCode,
                        img.CaptureStage == Domain.Enums.CameraCaptureStage.WEIGHT1 ? "Cân lần 1" : "Cân lần 2",
                        img.CapturedAt.ToString("dd/MM/yyyy HH:mm:ss"),
                        imageSource
                    ));
                }
            }
            catch
            {
                // Ignore conversion errors for single corrupted images
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

    private static ImageSource? ByteArrayToImageSource(byte[] bytes)
    {
        if (bytes == null || bytes.Length == 0)
            return null;

        try
        {
            using var ms = new MemoryStream(bytes);
            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.StreamSource = ms;
            image.EndInit();
            image.Freeze(); // Crucial for thread safety and performance
            return image;
        }
        catch
        {
            return null;
        }
    }
}

public sealed class CameraImageItemViewModel
{
    public string CameraName { get; }
    public string Stage { get; }
    public string CapturedAt { get; }
    public ImageSource Image { get; }

    public string DisplayTitle => $"{Stage} - {CameraName} ({CapturedAt})";

    public CameraImageItemViewModel(string cameraName, string stage, string capturedAt, ImageSource image)
    {
        CameraName = cameraName;
        Stage = stage;
        CapturedAt = capturedAt;
        Image = image;
    }
}
