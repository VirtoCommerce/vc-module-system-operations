using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace VirtoCommerce.SystemOperations.Core.Diagnostics;

// Production-readiness rules over the configuration the platform runs with: findings, most severe first.
public interface IConfigurationCheckService
{
    Task<IList<Finding>> CheckAsync(CancellationToken cancellationToken = default);
}
