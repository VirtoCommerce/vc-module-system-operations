namespace VirtoCommerce.SystemOperations.Core.Diagnostics;

public interface IPlatformMaintenanceService
{
    // Expires every entry of the platform memory cache, as the back office's Reset cache does.
    void ResetCache();

    // Restarts the application gracefully, as the back office's Restart does.
    void Restart();
}
