namespace StationApp.Domain.Constants;

public static class AppConfigKeys
{
    public const string OverweightSplitStepWeight = "OverweightSplitStepWeight";
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
}

public static class AppConfigDefaults
{
    public const decimal DefaultOverweightSplitStepWeight = 0.0025m;
}
