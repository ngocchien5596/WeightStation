using System.Windows.Media;

namespace StationApp.UI.Services;

public interface IWeighingDeviceHost
{
    decimal CurrentWeight { get; set; }
    bool IsStable { get; set; }
    bool IsDeviceConnected { get; set; }
    string StabilityText { get; set; }
    string DeviceStatusText { get; set; }
    Brush StabilityBrush { get; set; }

    string CameraPreviewStatusText { get; set; }
    ImageSource? CameraPreviewSource { get; set; }
    string SelectedPreviewCameraCode { get; set; }
    bool IsCameraPreviewAvailable { get; set; }
    bool IsCamera1PreviewAvailable { get; set; }
    bool IsCamera2PreviewAvailable { get; set; }
    string Camera1PreviewName { get; set; }
    string Camera2PreviewName { get; set; }

    bool IsAutoMode { get; }

    void RaisePropertyChanged(string propertyName);
}
