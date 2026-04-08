using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Permissions = VirtoCommerce.SystemOperations.Core.ModuleConstants.Security.Permissions;

namespace VirtoCommerce.SystemOperations.Web.Controllers.Api;

[Authorize]
[Route("api/system-operations")]
public class SystemOperationsController : Controller
{
    // GET: api/system-operations
    /// <summary>
    /// Get message
    /// </summary>
    /// <remarks>Return "Hello world!" message</remarks>
    [HttpGet]
    [Route("")]
    [Authorize(Permissions.Read)]
    public ActionResult<string> Get()
    {
        return Ok(new { result = "Hello world!" });
    }
}
