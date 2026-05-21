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
}
