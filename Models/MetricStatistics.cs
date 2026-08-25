namespace OutOfMemoryWorkbook.Models;

public sealed record MetricStatistics(
    double Median,
    double Minimum,
    double Maximum)
{
    public static MetricStatistics From(IEnumerable<double> values)
    {
        var orderedValues = values.Order().ToArray();

        if (orderedValues.Length == 0)
        {
            throw new ArgumentException("Ao menos um valor é necessário.", nameof(values));
        }

        var middle = orderedValues.Length / 2;
        var median = orderedValues.Length % 2 == 0
            ? (orderedValues[middle - 1] + orderedValues[middle]) / 2d
            : orderedValues[middle];

        return new MetricStatistics(
            Math.Round(median, 2),
            Math.Round(orderedValues[0], 2),
            Math.Round(orderedValues[^1], 2));
    }
}
