namespace StationApp.Domain.Constants;

public static class WeighTicketRecordRoles
{
    public const string MasterSession = "MASTER_SESSION";
    public const string CutOrderDerived = "CUT_ORDER_DERIVED";
    public const string SplitDerived = "SPLIT_DERIVED";
}

public static class DeliveryTicketRecordRoles
{
    public const string Master = "DELIVERY_MASTER";
    public const string Normal = "NORMAL";
    public const string SplitDerived = "SPLIT_DERIVED";
}
