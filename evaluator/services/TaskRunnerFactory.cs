using Microsoft.Extensions.Logging;

namespace Evaluator;

public class TaskRunnerFactory(ILogger<TaskRunner> logger)
{
    private readonly ILogger<TaskRunner> logger = logger;

    public TaskRunner Create(string name, int concurrency)
    {
        return new TaskRunner(name, concurrency, this.logger);
    }
}