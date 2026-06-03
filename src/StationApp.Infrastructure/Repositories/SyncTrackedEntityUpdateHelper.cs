using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using StationApp.Domain.Entities;
using StationApp.Domain.Enums;

namespace StationApp.Infrastructure.Repositories;

internal static class SyncTrackedEntityUpdateHelper
{
    private static readonly HashSet<string> CutOrderSyncOnlyProperties = new(StringComparer.Ordinal)
    {
        nameof(CutOrder.SyncStatus),
        nameof(CutOrder.LastSyncAttemptAt),
        nameof(CutOrder.LastSyncError),
        nameof(CutOrder.UpdatedAt)
    };

    private static readonly HashSet<string> WeighTicketSyncOnlyProperties = new(StringComparer.Ordinal)
    {
        nameof(WeighTicket.SyncStatus),
        nameof(WeighTicket.UpdatedAt)
    };

    private static readonly HashSet<string> DeliveryTicketSyncOnlyProperties = new(StringComparer.Ordinal)
    {
        nameof(DeliveryTicket.SyncStatus),
        nameof(DeliveryTicket.UpdatedAt)
    };

    public static void PrepareForAdd(CutOrder entity)
    {
        entity.SyncStatus = SyncStatus.SYNC_QUEUED;
        entity.LastSyncError = null;
    }

    public static void PrepareForAdd(WeighTicket entity)
    {
        entity.SyncStatus = SyncStatus.SYNC_QUEUED;
    }

    public static void PrepareForAdd(DeliveryTicket entity)
    {
        entity.SyncStatus = SyncStatus.SYNC_QUEUED;
    }

    public static void PrepareForUpdate(DbContext db, CutOrder entity)
    {
        db.ChangeTracker.DetectChanges();
        var entry = db.Entry(entity);
        if (HasBusinessChanges(entry, CutOrderSyncOnlyProperties))
        {
            entity.SyncStatus = SyncStatus.SYNC_QUEUED;
            entity.LastSyncError = null;
        }
    }

    public static void PrepareForUpdate(DbContext db, WeighTicket entity)
    {
        db.ChangeTracker.DetectChanges();
        var entry = db.Entry(entity);
        if (HasBusinessChanges(entry, WeighTicketSyncOnlyProperties))
        {
            entity.SyncStatus = SyncStatus.SYNC_QUEUED;
        }
    }

    public static void PrepareForUpdate(DbContext db, DeliveryTicket entity)
    {
        db.ChangeTracker.DetectChanges();
        var entry = db.Entry(entity);
        if (HasBusinessChanges(entry, DeliveryTicketSyncOnlyProperties))
        {
            entity.SyncStatus = SyncStatus.SYNC_QUEUED;
        }
    }

    private static bool HasBusinessChanges(EntityEntry entry, HashSet<string> syncOnlyProperties)
    {
        if (entry.State == EntityState.Detached || entry.State == EntityState.Added)
        {
            return true;
        }

        foreach (var property in entry.Properties)
        {
            if (property.Metadata.IsPrimaryKey())
            {
                continue;
            }

            if (syncOnlyProperties.Contains(property.Metadata.Name))
            {
                continue;
            }

            if (!AreEqual(property.OriginalValue, property.CurrentValue))
            {
                return true;
            }
        }

        return false;
    }

    private static bool AreEqual(object? left, object? right)
    {
        if (left is byte[] leftBytes && right is byte[] rightBytes)
        {
            return leftBytes.AsSpan().SequenceEqual(rightBytes);
        }

        return Equals(left, right);
    }
}
