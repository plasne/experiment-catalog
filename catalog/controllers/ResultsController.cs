using System.ComponentModel.DataAnnotations;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;

namespace Catalog;

[ApiController]
[Route("api/projects/{projectName}/experiments/{experimentName}/results")]
public class ResultsController : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Add(
        [FromServices] IStorageServiceFactory storageServiceFactory,
        [FromRoute, Required, ValidName, ValidProjectName] string projectName,
        [FromRoute, Required, ValidName, ValidExperimentName] string experimentName,
        [FromBody] AddResultRequest request,
        CancellationToken cancellationToken)
    {
        if (projectName is null || experimentName is null || request is null)
        {
            return BadRequest("a project name, experiment name, and result (as body) are required.");
        }

        if (
            (request.Annotations is null || request.Annotations.Count == 0) &&
            (request.Ref is null || request.Set is null || request.Metrics is null))
        {
            return BadRequest("ref, set, and metrics are required when there is not an annotation.");
        }

        var result = new Result
        {
            Ref = request.Ref,
            Set = request.Set,
            InferenceUri = request.InferenceUri,
            EvaluationUri = request.EvaluationUri,
            Metrics = request.ToMetrics(),
            Annotations = request.Annotations,
        };

        var storageService = await storageServiceFactory.GetStorageServiceAsync(cancellationToken);
        await storageService.AddResultAsync(projectName, experimentName, result, cancellationToken);
        return Ok();
    }
}
