// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Catalog;

public class AzureBlobStorageMaintenanceService : BackgroundService
{
    private readonly IConfig config;
    private readonly AzureBlobStorageService storageService;
    private readonly ILogger<AzureBlobStorageMaintenanceService> logger;

    public AzureBlobStorageMaintenanceService(
        IConfig config,
        IStorageService storageService,
        ILogger<AzureBlobStorageMaintenanceService> logger)
    {
        this.config = config;
        if (storageService is not AzureBlobStorageService azureBlobStorageService)
        {
            throw new Exception("AzureBlobStorageMaintenanceService can only be used in conjuction with AzureBlobStorageService.");
        }
        this.storageService = azureBlobStorageService;
        this.logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(TimeSpan.FromMinutes(this.config.OPTIMIZE_EVERY_X_MINUTES), stoppingToken);
            try
            {
                await this.storageService.OptimizeAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, "there was an error during the optimize step in AzureBlobStorageMaintenanceService...");
                // continue
            }
        }
    }
}