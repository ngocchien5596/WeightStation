namespace StationApp.Domain.Constants;

public static class CrusherWeighingModes
{
    public const string TwoWeigh = "TWO_WEIGH";
    public const string SingleWithStandardTare = "SINGLE_WITH_STANDARD_TARE";
}

public static class NetWeightCalculationModes
{
    public const string Weight2Diff = "WEIGHT2_DIFF";
    public const string Weight1MinusStandardTare = "WEIGHT1_MINUS_STANDARD_TARE";
}

public static class StationOperationSettingKeys
{
    public const string CrusherSingleWeighEnabled = "crusher_single_weigh_enabled";
    public const string CrusherDefaultWeighMode = "crusher_default_weigh_mode";
    public const string CrusherRequireStandardTareForSingleWeigh = "crusher_require_standard_tare_for_single_weigh";
    public const string CrusherStandardTareToleranceKg = "crusher_standard_tare_tolerance_kg";
    public const string CrusherDefaultProductCode = "crusher_default_product_code";
    public const string CrusherDefaultCustomerCode = "crusher_default_customer_code";
}
