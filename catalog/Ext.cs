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

    public static decimal StdDev<TSource>(
        this IList<TSource> values,
        Func<TSource, decimal?> selector)
    {
        var selectedValues = values.Select(v => selector(v)).OfType<decimal>().ToList();
        double avg = Convert.ToDouble(selectedValues.Average());
        double stddev = Math.Sqrt(selectedValues.Average(v => Math.Pow(Convert.ToDouble(v) - avg, 2)));
        return Convert.ToDecimal(stddev);
    }
}