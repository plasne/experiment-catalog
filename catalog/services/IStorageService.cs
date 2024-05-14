using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Catalog;

public interface IStorageService
{
    Task<IList<Project>> GetProjectsAsync(CancellationToken cancellationToken = default);
    Task AddProjectAsync(Project project, CancellationToken cancellationToken = default);
    Task<IList<Experiment>> GetExperimentsAsync(string projectName, CancellationToken cancellationToken = default);
    Task AddExperimentAsync(string projectName, Experiment experiment, CancellationToken cancellationToken = default);
    Task SetExperimentAsBaselineAsync(string projectName, string experimentName, CancellationToken cancellationToken = default);
    Task AddResultAsync(string projectName, string experimentName, Result result, CancellationToken cancellationToken = default);
    Task<Experiment> GetProjectBaselineAsync(string projectName, CancellationToken cancellationToken = default);
    Task<Experiment> GetExperimentAsync(string projectName, string experimentName, CancellationToken cancellationToken = default);
    Task OptimizeExperimentAsync(string projectName, string experimentName, CancellationToken cancellationToken = default);
}