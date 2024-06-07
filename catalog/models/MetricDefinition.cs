namespace Catalog;

public class MetricDefinition
{
    public required decimal Min { get; set; }
    public required decimal Max { get; set; }

    public bool TryNormalize(decimal? value, out decimal normalized)
    {
        if (value is not null && this.Max > this.Min)
        {
            normalized = ((decimal)value - this.Min) / (this.Max - this.Min);
            return true;
        }
        else if (value is not null && this.Min > this.Max)
        {
            normalized = (this.Min - (decimal)value) / (this.Min - this.Max);
            return true;
        }
        normalized = default;
        return false;
    }
}