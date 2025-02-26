// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.

using System;
using System.Threading;
using System.Threading.Tasks;

namespace Evaluator;

public class TaskRunner(int concurrency) : IDisposable
{
    private readonly SemaphoreSlim max = new(concurrency, concurrency);
    private readonly CancellationTokenSource cts = new();
    private bool disposed = false;

    public async Task StartAsync<T>(Func<Task<T>> func, Func<T, Task>? onSuccess = null, Func<Exception, Task>? onFailure = null)
    {
        await max.WaitAsync();
#pragma warning disable CS4014 // we want this to run in the background
        Task.Run(async () =>
        {
            try
            {
                var v = await func();
                if (onSuccess is not null) await onSuccess(v);
            }
            catch (Exception ex)
            {
                if (onFailure is not null) await onFailure(ex);
            }
            finally
            {
                max.Release();
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