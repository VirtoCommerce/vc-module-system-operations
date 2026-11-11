using System.Threading;
using System.Threading.Tasks;

namespace VirtoCommerce.SystemOperations.Core.Diagnostics;

// Runs the platform's health checks (database, cache, Redis, search and every check a module adds) on this instance.
public interface IPlatformHealthService
{
    Task<PlatformHealthReport> CheckAsync(CancellationToken cancellationToken = default);
}
