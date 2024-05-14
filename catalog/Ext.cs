using System;
using System.Collections.Generic;
using System.Linq;

namespace Catalog;

public static class Ext
{
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