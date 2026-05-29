namespace StationApp.Domain.Constants;

public static class AppConfigKeys
{
    public const string OverweightSplitStepWeight = "OverweightSplitStepWeight";
    public const string ToleranceKgPerBag = "tolerance_kg_per_bag";
    public const string DeviceComPort = "device_com_port";
    public const string DeviceBaudrate = "device_baudrate";
    public const string DeviceParity = "device_parity";
    public const string DeviceDataBits = "device_data_bits";
    public const string DeviceStopBits = "device_stop_bits";
    public const string DeviceParserType = "device_parser_type";
    public const string DeviceFrameEndChar = "device_frame_end_char";
    public const string DeviceStableCycles = "device_stable_cycles";
    public const string WeightSubstringStart = "weight_substring_start";
    public const string WeightSubstringLength = "weight_substring_length";
    public const string DefaultWeighTicketPrinter = "default_weigh_ticket_printer";
    public const string DefaultDeliveryTicketPrinter = "default_delivery_ticket_printer";
    public const string Camera1Enabled = "camera_1_enabled";
    public const string Camera1Name = "camera_1_name";
    public const string Camera1RtspUrl = "camera_1_rtsp_url";
    public const string Camera1PreviewRtspUrl = "camera_1_preview_rtsp_url";
    public const string Camera2Enabled = "camera_2_enabled";
    public const string Camera2Name = "camera_2_name";
    public const string Camera2RtspUrl = "camera_2_rtsp_url";
    public const string Camera2PreviewRtspUrl = "camera_2_preview_rtsp_url";
    public const string CameraC6_1Enabled = "camera_c6_1_enabled";
    public const string CameraC6_1Name = "camera_c6_1_name";
    public const string CameraC6_1RtspUrl = "camera_c6_1_rtsp_url";
    public const string CameraC6_1PreviewRtspUrl = "camera_c6_1_preview_rtsp_url";
    public const string CameraC6_2Enabled = "camera_c6_2_enabled";
    public const string CameraC6_2Name = "camera_c6_2_name";
    public const string CameraC6_2RtspUrl = "camera_c6_2_rtsp_url";
    public const string CameraC6_2PreviewRtspUrl = "camera_c6_2_preview_rtsp_url";
    public const string CameraPreviewDefault = "camera_preview_default";
    public const string CameraCaptureTimeoutMs = "camera_capture_timeout_ms";
    public const string CameraCaptureJpegQuality = "camera_capture_jpeg_quality";
    public const string CameraCaptureWarmupFrames = "camera_capture_warmup_frames";
}

public static class AppConfigDefaults
{
    public const decimal DefaultOverweightSplitStepWeight = 0.0025m;
    public const decimal DefaultToleranceKgPerBag = 1.75m;
    public const string DefaultDeviceComPort = "COM6";
    public const string DefaultDeviceBaudrate = "9600";
    public const string DefaultDeviceParity = "None";
    public const string DefaultDeviceDataBits = "8";
    public const string DefaultDeviceStopBits = "One";
    public const string DefaultDeviceParserType = "AUTO";
    public const string DefaultDeviceFrameEndChar = "ETX";
    public const string DefaultDeviceStableCycles = "3";
    public const string DefaultWeightSubstringStart = "0";
    public const string DefaultWeightSubstringLength = "7";
    public const string DefaultWeighTicketPrinter = "";
    public const string DefaultDeliveryTicketPrinter = "";
    public const string DefaultCamera1Enabled = "false";
    public const string DefaultCamera1Name = "Camera 1";
    public const string DefaultCamera1RtspUrl = "";
    public const string DefaultCamera1PreviewRtspUrl = "";
    public const string DefaultCamera2Enabled = "false";
    public const string DefaultCamera2Name = "Camera 2";
    public const string DefaultCamera2RtspUrl = "";
    public const string DefaultCamera2PreviewRtspUrl = "";
    public const string DefaultCameraC6_1Enabled = "false";
    public const string DefaultCameraC6_1Name = "Camera C6-1";
    public const string DefaultCameraC6_1RtspUrl = "";
    public const string DefaultCameraC6_1PreviewRtspUrl = "";
    public const string DefaultCameraC6_2Enabled = "false";
    public const string DefaultCameraC6_2Name = "Camera C6-2";
    public const string DefaultCameraC6_2RtspUrl = "";
    public const string DefaultCameraC6_2PreviewRtspUrl = "";
    public const string DefaultCameraPreview = "CAM1";
    public const string DefaultCameraCaptureTimeoutMs = "3000";
    public const string DefaultCameraCaptureJpegQuality = "85";
    public const string DefaultCameraCaptureWarmupFrames = "5";
}
