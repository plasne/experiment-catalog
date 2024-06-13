using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Evaluator;

public class TaskRunner(string name, int concurrency, ILogger<TaskRunner> logger) : IDisposable
{
    private readonly string name = name;
    private readonly SemaphoreSlim max = new(concurrency, concurrency);
    private readonly CancellationTokenSource cts = new();
    private readonly ILogger<TaskRunner> logger = logger;
    private bool disposed = false;

    public async Task StartAsync<T>(Func<Task<T>> func, Func<T, Task>? onSuccess = null, Func<Exception, Task>? onFailure = null)
    {
        this.logger.LogDebug("waiting for a slot in task runner '{n}' / {i}...", name, max.CurrentCount);
        await max.WaitAsync();
        this.logger.LogDebug("slot assigned in task runner '{n}'.", name);
#pragma warning disable CS4014 // we want this to run in the background
        Task.Run(async () =>
        {
            try
            {
                var v = await func();
                if (onSuccess is not null) await onSuccess(v);
                this.logger.LogDebug("task was successful in task runner '{n}'.", name);
            }
            catch (Exception ex)
            {
                if (onFailure is not null) await onFailure(ex);
                this.logger.LogDebug("task failed in task runner '{n}'.", name);
            }
            finally
            {
                max.Release();
                this.logger.LogDebug("slot released in task runner '{n}' / {i}.", name, max.CurrentCount);
            }
        }, this.cts.Token);
#pragma warning restore CS4014
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!disposed)
        {
            if (disposing)
            {
                // dispose managed resources.
                max.Dispose();
                cts.Dispose();
            }

            // dispose unmanaged resources

            disposed = true;
        }
    }
}