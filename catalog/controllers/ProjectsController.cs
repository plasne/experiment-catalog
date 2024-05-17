using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;

namespace Catalog;

[ApiController]
[Route("api/projects")]
public class ProjectsController : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IList<Project>>> List(
        [FromServices] IStorageService storageService,
        CancellationToken cancellationToken)
    {
        var projects = await storageService.GetProjectsAsync(cancellationToken);
        return Ok(projects);
    }

    [HttpPost]
    public async Task<IActionResult> Add(
        [FromServices] IStorageService storageService,
        [FromBody] Project project,
        CancellationToken cancellationToken)
    {
        if (project is null)
        {
            return BadRequest("a project (as body) is required.");
        }

        if (string.IsNullOrEmpty(project.Name))
        {
            return BadRequest("an project name is required.");
        }

        await storageService.AddProjectAsync(project, cancellationToken);
        return Ok();
    }
}
