public interface IStorageService
{
    Task<IEnumerable<Project>> GetProjects(CancellationToken cancellationToken = default);
    Task<IEnumerable<Experiment>> GetExperiments(string projectName, CancellationToken cancellationToken = default);
    Task AddExperiment(string projectName, Experiment experiment, CancellationToken cancellationToken = default);
    Task SetExperimentAsBaseline(string projectName, string experimentName, CancellationToken cancellationToken = default);
    Task AddResult(string projectName, string experimentName, Result result, CancellationToken cancellationToken = default);
    Task<Experiment> GetProjectBaseline(string projectName, CancellationToken cancellationToken = default);
    Task<Experiment> GetExperiment(string projectName, string experimentName, CancellationToken cancellationToken = default);
    Task OptimizeExperiment(string projectName, string experimentName, CancellationToken cancellationToken = default);
}