namespace StationApp.Domain.Constants;

public static class SyncAggregateTypes
{
    public const string CutOrder = nameof(Entities.CutOrder);
    public const string WeighTicket = nameof(Entities.WeighTicket);
    public const string DeliveryTicket = nameof(Entities.DeliveryTicket);
    public const string WeighingSession = nameof(Entities.WeighingSession);
    public const string WeighingSessionLine = nameof(Entities.WeighingSessionLine);
    public const string Station = nameof(Entities.Station);
    public const string Vehicle = nameof(Entities.Vehicle);
    public const string Customer = nameof(Entities.Customer);
    public const string Product = nameof(Entities.Product);
}

