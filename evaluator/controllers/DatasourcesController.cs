using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("api/datasources")]
public class DatasourcesController() : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<Datasource>>> List(
        [FromServices] IBlobStorageService blobService,
        CancellationToken cancellationToken)
    {
        var datasources = await blobService.ListDatasources(cancellationToken);
        return Ok(datasources);
    }
}
