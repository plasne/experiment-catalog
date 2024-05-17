using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;

namespace Evaluator;

public class Maintenance(IConfig config) : BackgroundService
{
    private readonly IConfig config = config;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (this.config.MINUTES_BETWEEN_RESTORE_AFTER_BUSY == 0)
        {
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(TimeSpan.FromMinutes(this.config.MINUTES_BETWEEN_RESTORE_AFTER_BUSY), stoppingToken);
            var proposed = this.config.MS_BETWEEN_DEQUEUE_CURRENT - this.config.MS_TO_ADD_ON_BUSY;
            this.config.MS_BETWEEN_DEQUEUE_CURRENT = proposed < this.config.MS_BETWEEN_DEQUEUE
                ? this.config.MS_BETWEEN_DEQUEUE
                : proposed;
        }
    }
}