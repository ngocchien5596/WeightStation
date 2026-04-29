using Microsoft.EntityFrameworkCore;
using StationApp.Domain.Entities;

namespace StationApp.Infrastructure.Persistence;

public sealed class WeighTicketPrimaryRepairService
{
    private readonly StationDbContext _dbContext;

    public WeighTicketPrimaryRepairService(StationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task ExecuteAsync(CancellationToken ct)
    {
        var registrations = await _dbContext.VehicleRegistrations
            .Where(vr => vr.CurrentPrimaryWeighTicketId != null || vr.HasOverweightCase)
            .ToListAsync(ct);

        if (registrations.Count == 0)
        {
            return;
        }

        var registrationIds = registrations.Select(r => r.Id).ToList();
        var weighTickets = await _dbContext.WeighTickets
            .Where(wt => registrationIds.Contains(wt.VehicleRegistrationId))
            .ToListAsync(ct);

        var hasChanges = false;

        foreach (var registration in registrations)
        {
            var relatedTickets = weighTickets
                .Where(wt => wt.VehicleRegistrationId == registration.Id && string.Equals(wt.RecordRole, "WORKING", StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (relatedTickets.Count == 0)
            {
                continue;
            }

            var canonicalPrimary = ResolveCanonicalPrimary(relatedTickets);
            if (canonicalPrimary == null)
            {
                continue;
            }

            foreach (var ticket in relatedTickets)
            {
                var shouldBePrimary = ticket.Id == canonicalPrimary.Id;
                if (ticket.IsPrimaryDisplay != shouldBePrimary)
                {
                    ticket.IsPrimaryDisplay = shouldBePrimary;
                    hasChanges = true;
                }
            }

            if (registration.CurrentPrimaryWeighTicketId != canonicalPrimary.Id)
            {
                registration.CurrentPrimaryWeighTicketId = canonicalPrimary.Id;
                hasChanges = true;
            }
        }

        if (hasChanges)
        {
            await _dbContext.SaveChangesAsync(ct);
        }
    }

    private static WeighTicket? ResolveCanonicalPrimary(IReadOnlyCollection<WeighTicket> weighTickets)
    {
        if (weighTickets.Count == 0)
        {
            return null;
        }

        var splitTickets = weighTickets.Where(t => t.SplitSequence.HasValue).ToList();
        if (splitTickets.Count > 0)
        {
            return splitTickets
                .OrderBy(t => t.SplitSequence ?? byte.MaxValue)
                .ThenBy(t => t.IsOverWeight ? 1 : 0)
                .ThenByDescending(t => t.UpdatedAt ?? t.CreatedAt)
                .FirstOrDefault();
        }

        return weighTickets
            .OrderByDescending(t => t.IsPrimaryDisplay)
            .ThenByDescending(t => t.UpdatedAt ?? t.CreatedAt)
            .FirstOrDefault();
    }
}
