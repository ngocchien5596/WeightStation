namespace StationApp.Domain.Constants;

public static class ClayWeighingModes
{
    public const string TwoWeigh = "TWO_WEIGH";
    public const string SingleWithStandardTare = "SINGLE_WITH_STANDARD_TARE";
}

public static class ClayStationOperationSettingKeys
{
    public const string ClaySingleWeighEnabled = "clay_single_weigh_enabled";
    public const string ClayDefaultWeighMode = "clay_default_weigh_mode";
    public const string ClayRequireStandardTareForSingleWeigh = "clay_require_standard_tare_for_single_weigh";
    public const string ClayStandardTareToleranceKg = "clay_standard_tare_tolerance_kg";
    public const string ClayDefaultProductCode = "clay_default_product_code";
    public const string ClayDefaultCustomerCode = "clay_default_customer_code";
}

public static class ClayDefaults
{
    public const string ProductCode = "Set";
    public const string ProductName = "Sét";
    public const string CustomerCode = "NCC2";
    public const string CustomerName = "Nhà cung cấp 2";
}

