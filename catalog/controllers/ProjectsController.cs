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
    public async Task<ActionResult<IList<Project>>> ListProjects(
        [FromServices] IStorageService storageService,
        CancellationToken cancellationToken)
    {
        var projects = await storageService.GetProjectsAsync(cancellationToken);
        return Ok(projects);
    }

    [HttpPost]
    public async Task<IActionResult> AddProject(
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

    [HttpGet("{projectName}/tags")]
    public async Task<ActionResult<IList<Tag>>> ListTagsInProject(
        [FromServices] IStorageService storageService,
        [FromRoute] string projectName,
        CancellationToken cancellationToken)
    {
        var tags = await storageService.ListTagsAsync(projectName, cancellationToken);
        return Ok(tags);
    }

    [HttpPut("{projectName}/tags")]
    public async Task<IActionResult> AddTagToProject(
        [FromServices] IStorageService storageService,
        [FromRoute] string projectName,
        [FromBody] Tag tag,
        CancellationToken cancellationToken)
    {
        if (tag is null)
        {
            return BadRequest("a tag (as body) is required.");
        }

        if (string.IsNullOrEmpty(tag.Name))
        {
            return BadRequest("a tag name is required.");
        }

        await storageService.AddTagAsync(projectName, tag, cancellationToken);
        return Ok();
    }
}
