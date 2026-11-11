using VirtoCommerce.Platform.Caching;
using VirtoCommerce.Platform.Core.Modularity;
using VirtoCommerce.SystemOperations.Core.Diagnostics;

namespace VirtoCommerce.SystemOperations.Data.Diagnostics;

// The same calls as the back office's Reset cache and Restart (api/platform-cache/reset, api/platform/modules/restart).
public class PlatformMaintenanceService(IPlatformRestarter platformRestarter) : IPlatformMaintenanceService
{
    public virtual void ResetCache()
    {
        GlobalCacheRegion.ExpireRegion();
    }

    public virtual void Restart()
    {
        platformRestarter.Restart();
    }
}
