namespace Catalog;

public class MetricDefinition
{
    public required decimal Min { get; set; }
    public required decimal Max { get; set; }

    public bool TryNormalize(decimal value, out decimal normalized)
    {
        if (this.Max > this.Min)
        {
            normalized = (value - this.Min) / (this.Max - this.Min);
            return true;
        }
        else if (this.Min > this.Max)
        {
            normalized = (this.Min - value) / (this.Min - this.Max);
            return true;
        }
        normalized = default;
        return false;
    }
}