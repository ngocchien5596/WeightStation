using System.Threading;
using System.Threading.Tasks;

namespace StationApp.Application.Interfaces;

public interface IDocumentCounterService
{
    Task<int> GetNextSequenceAsync(string counterKey, CancellationToken ct);
}
