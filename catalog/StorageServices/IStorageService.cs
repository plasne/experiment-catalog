public interface IStorageService
{
    public Task<IEnumerable<Experiment>> GetExperiments(string projectName, CancellationToken cancellationToken = default);
    public Task AddExperiment(string projectName, Experiment experiment, CancellationToken cancellationToken = default);
    public Task SetExperimentAsBaseline(string projectName, string experimentName, CancellationToken cancellationToken = default);
    public Task AddResult(string projectName, string experimentName, Result result, CancellationToken cancellationToken = default);
    public Task<Experiment> GetProjectBaseline(string projectName, CancellationToken cancellationToken = default);
    public Task<Experiment> GetExperiment(string projectName, string experimentName, CancellationToken cancellationToken = default);
}