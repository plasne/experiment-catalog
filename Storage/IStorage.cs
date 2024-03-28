using Microsoft.AspNetCore.Mvc;

public interface IStorage
{
    public Task<IEnumerable<Experiment>> GetExperiments(string projectName);
    public Task AddExperiment(string projectName, Experiment experiment);
    public Task SetExperimentAsBaseline(string projectName, string experimentName);
    public Task AddResult(string projectName, string experimentName, Result result);
    public Task<Experiment> GetProjectBaseline(string projectName);
    public Task<Experiment> GetExperiment(string projectName, string experimentName);
}