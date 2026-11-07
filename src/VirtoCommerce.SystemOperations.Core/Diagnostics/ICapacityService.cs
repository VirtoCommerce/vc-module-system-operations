using System;
using System.Threading;
using System.Threading.Tasks;

namespace VirtoCommerce.SystemOperations.Core.Diagnostics;

public interface ICapacityService
{
    // Measures this instance over the sample, so the CPU is the use of that time, not of the process lifetime.
    Task<CapacityReport> MeasureAsync(TimeSpan sample, CancellationToken cancellationToken = default);
}
