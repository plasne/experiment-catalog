using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Linq;

namespace Evaluator;

public class HistogramDefinition
{
    public HistogramDefinition(string connectionString)
    {
        var keyValuePairs = connectionString.Split(';')
            .Select(part => part.Split('='))
            .Where(part => part.Length == 2)
            .ToDictionary(split => split[0].ToLower(), split => split[1]);

        this.Type = keyValuePairs.TryGetValue("type", out string? type)
            ? type
            : null;
        this.Name = keyValuePairs.TryGetValue("name", out string? value0)
            ? value0
            : null;
        this.Unit = keyValuePairs.TryGetValue("unit", out string? value1)
            ? value1
            : null;
        this.Description = keyValuePairs.TryGetValue("description", out string? value2)
            ? value2
            : null;
        this.Value = keyValuePairs.TryGetValue("value", out string? value) && decimal.TryParse(value, out var dvalue)
            ? dvalue
            : null;
    }

    public string? Type { get; }
    public string? Name { get; }
    public string? Unit { get; }
    public string? Description { get; }
    public decimal? Value { get; }

    private TagList CreateTags(PipelineRequest request)
    {
        var tags = new TagList
        {
            { "run_id", request.RunId.ToString() },
            { "id", request.Id },
            { "project", request.Project },
            { "experiment", request.Experiment },
            { "ref", request.Ref },
            { "set", request.Set },
            { "is_baseline", request.IsBaseline.ToString() }
        };

        if (Activity.Current is not null)
        {
            foreach (var bag in Activity.Current.Baggage)
            {
                tags.Add(bag.Key, bag.Value);
            }
        }

        return tags;
    }

    public bool TryRecord(Meter meter, PipelineRequest request)
    {
        if (string.IsNullOrEmpty(this.Type)) return false;
        if (string.IsNullOrEmpty(this.Name)) return false;
        if (this.Value is null) return false;

        switch (this.Type.ToLower())
        {
            case "int":
                {
                    var histogram = meter.CreateHistogram<int>(name: this.Name, unit: this.Unit, description: this.Description);
                    histogram.Record((int)this.Value, this.CreateTags(request));
                    return true;
                }
            case "double":
                {
                    var histogram = meter.CreateHistogram<double>(name: this.Name, unit: this.Unit, description: this.Description);
                    histogram.Record((double)this.Value, this.CreateTags(request));
                    return true;
                }
            case "decimal":
                {
                    var histogram = meter.CreateHistogram<decimal>(name: this.Name, unit: this.Unit, description: this.Description);
                    histogram.Record((decimal)this.Value, this.CreateTags(request));
                    return true;
                }
            default:
                return false;
        }
    }
}