using System.Threading;
using System.Threading.Tasks;

namespace VirtoCommerce.SystemOperations.Core.Diagnostics;

public interface IEnvironmentProfileService
{
    Task<EnvironmentProfile> GetProfileAsync(CancellationToken cancellationToken = default);
}
