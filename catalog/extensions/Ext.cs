using System;
using System.Collections.Generic;
using System.Linq;
using Azure.Monitor.OpenTelemetry.Exporter;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace Catalog;

public static class Ext
{
    public static void AddOpenTelemetry(
        this ILoggingBuilder builder,
        string openTelemetryConnectionString)
    {
        builder.AddOpenTelemetry(logging =>
        {
            logging.AddAzureMonitorLogExporter(o => o.ConnectionString = openTelemetryConnectionString);
        });
    }

    public static void AddOpenTelemetry(
        this IServiceCollection serviceCollection,
        string sourceName,
        string applicationName,
        string openTelemetryConnectionString)
    {
        serviceCollection.AddOpenTelemetry()
            .ConfigureResource(resource => resource.AddService(serviceName: applicationName))
            .WithMetrics(metrics =>
            {
                metrics.AddAspNetCoreInstrumentation();
                metrics.AddHttpClientInstrumentation();
                metrics.AddMeter(sourceName);
                metrics.AddAzureMonitorMetricExporter(o => o.ConnectionString = openTelemetryConnectionString);
            })
            .WithTracing(tracing =>
            {
                tracing.AddAspNetCoreInstrumentation();
                tracing.AddHttpClientInstrumentation(o =>
                {
                    o.FilterHttpRequestMessage = (request) =>
                    {
                        return request.RequestUri is not null && !request.RequestUri.ToString().Contains(".queue.core.windows.net");
                    };
                });
                tracing.AddSource(sourceName);
                tracing.AddAzureMonitorTraceExporter(o => o.ConnectionString = openTelemetryConnectionString);
            });
    }

    public static decimal? StdDev<TSource>(
        this IList<TSource> values,
        Func<TSource, decimal?> selector)
    {
        var selectedValues = values.Select(v => selector(v)).OfType<decimal>();
        if (!selectedValues.Any())
        {
            return null;
        }
        double avg = Convert.ToDouble(selectedValues.Average());
        double stddev = Math.Sqrt(selectedValues.Average(v => Math.Pow(Convert.ToDouble(v) - avg, 2)));
        return Convert.ToDecimal(stddev);
    }

    public static decimal DivBy(this int dividend, int divisor)
    {
        return divisor == 0 ? 0m : (decimal)dividend / (decimal)divisor;
    }

    /// <summary>
    /// Validates an Azure blob container name per Azure naming rules.
    /// Rules: 3-63 chars, lowercase letters/numbers/hyphens, starts with letter or number,
    /// no consecutive hyphens, cannot end with hyphen.
    /// </summary>
    public static void ValidateAzureContainerName(this string containerName)
    {
        if (string.IsNullOrEmpty(containerName))
        {
            throw new HttpException(400, "container name cannot be null or empty.");
        }

        if (containerName.Length < 3 || containerName.Length > 63)
        {
            throw new HttpException(400, $"container name '{containerName}' must be between 3 and 63 characters.");
        }

        if (!char.IsLetterOrDigit(containerName[0]))
        {
            throw new HttpException(400, $"container name '{containerName}' must start with a letter or number.");
        }

        if (containerName.EndsWith('-'))
        {
            throw new HttpException(400, $"container name '{containerName}' cannot end with a hyphen.");
        }

        for (int i = 0; i < containerName.Length; i++)
        {
            char c = containerName[i];
            if (!char.IsLower(c) && !char.IsDigit(c) && c != '-')
            {
                throw new HttpException(400, $"container name '{containerName}' contains invalid character '{c}'. Only lowercase letters, numbers, and hyphens are allowed.");
            }

            if (c == '-' && i > 0 && containerName[i - 1] == '-')
            {
                throw new HttpException(400, $"container name '{containerName}' cannot have consecutive hyphens.");
            }
        }
    }

    /// <summary>
    /// Validates an Azure blob name per Azure naming rules.
    /// Rules: 1-1024 chars, cannot end with dot or forward slash.
    /// </summary>
    public static void ValidateAzureBlobName(this string blobName)
    {
        if (string.IsNullOrEmpty(blobName))
        {
            throw new HttpException(400, "blob name cannot be null or empty.");
        }

        if (blobName.Length > 1024)
        {
            throw new HttpException(400, $"blob name must be 1024 characters or fewer (was {blobName.Length}).");
        }

        if (blobName.EndsWith('.') || blobName.EndsWith('/'))
        {
            throw new HttpException(400, $"blob name '{blobName}' cannot end with a dot or forward slash.");
        }
    }

    /// <summary>
    /// Validates that a name contains only letters, digits, hyphens, underscores, periods, and colons,
    /// and is between 3 and 50 characters.
    /// </summary>
    public static bool IsValidName(this string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return false;
        }

        if (name.Length > 50)
        {
            return false;
        }

        foreach (char c in name)
        {
            if (!char.IsLetterOrDigit(c) && c != '-' && c != '_' && c != '.' && c != ':')
            {
                return false;
            }
        }

        return true;
    }
}