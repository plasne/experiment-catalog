using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using NetBricks;

namespace Evaluator;

public class Maintenance(IConfigFactory<IConfig> configFactory) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var config = await configFactory.GetAsync(stoppingToken);
        if (config.MINUTES_BETWEEN_RESTORE_AFTER_BUSY == 0)
        {
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(TimeSpan.FromMinutes(config.MINUTES_BETWEEN_RESTORE_AFTER_BUSY), stoppingToken);
            var proposed = config.MS_BETWEEN_DEQUEUE_CURRENT - config.MS_TO_ADD_ON_BUSY;
            config.MS_BETWEEN_DEQUEUE_CURRENT = proposed < config.MS_BETWEEN_DEQUEUE
                ? config.MS_BETWEEN_DEQUEUE
                : proposed;
        }
    }
}