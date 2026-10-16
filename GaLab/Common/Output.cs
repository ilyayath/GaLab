using System.Globalization;
using System.Text;
using ScottPlot;

namespace GaLab.Common;

// Запис CSV-файлів у стандартному форматі (RFC 4180): роздільник - кома, десяткова крапка,
// значення з комою або лапками беруться в лапки. NaN (значення немає) записується як порожня клітинка.
public class Csv
{
    private readonly StringBuilder _text = new();

    public void Add(params object[] values)
    {
        var cells = new List<string>();
        foreach (var value in values)
        {
            var text = value switch
            {
                double d when double.IsNaN(d) => "",
                double d => d.ToString("G6", CultureInfo.InvariantCulture),
                _ => Convert.ToString(value, CultureInfo.InvariantCulture) ?? "",
            };
            cells.Add(Quote(text));
        }

        _text.AppendLine(string.Join(",", cells));
    }

    private static string Quote(string text)
    {
        if (!text.Contains(',') && !text.Contains('"') && !text.Contains('\n')) return text;
        return "\"" + text.Replace("\"", "\"\"") + "\"";
    }

    public void Save(string path)
    {
        File.WriteAllText(path, _text.ToString(), new UTF8Encoding(true));
    }
}

public static class Statistics
{
    public static double Mean(List<double> values) => values.Count == 0 ? double.NaN : values.Average();

    public static double StdDev(List<double> values)
    {
        if (values.Count < 2) return 0;
        var mean = values.Average();
        var sum = 0.0;
        foreach (var v in values) sum += (v - mean) * (v - mean);
        return Math.Sqrt(sum / (values.Count - 1));
    }

    public static double Median(List<double> values)
    {
        var sorted = values.OrderBy(v => v).ToList();
        var middle = sorted.Count / 2;
        return sorted.Count % 2 == 1 ? sorted[middle] : (sorted[middle - 1] + sorted[middle]) / 2;
    }

    // Середнє кількох рядів (наприклад, кривих збіжності різних запусків) по кожній точці
    public static double[] MeanCurve(List<List<double>> curves)
    {
        var length = curves.Min(c => c.Count);
        var result = new double[length];
        for (var i = 0; i < length; i++)
        {
            var sum = 0.0;
            foreach (var curve in curves) sum += curve[i];
            result[i] = sum / curves.Count;
        }

        return result;
    }
}

// Побудова графіків для звіту за допомогою ScottPlot
public static class Charts
{
    public class Line
    {
        public string Label;
        public double[] X;
        public double[] Y;
        public int ColorIndex;
        public bool Dashed;

        public Line(string label, double[] x, double[] y, int colorIndex, bool dashed = false)
        {
            Label = label;
            X = x;
            Y = y;
            ColorIndex = colorIndex;
            Dashed = dashed;
        }
    }

    private static readonly Color[] Colors =
    {
        Color.FromHex("#1f77b4"), Color.FromHex("#d62728"), Color.FromHex("#2ca02c"),
        Color.FromHex("#9467bd"), Color.FromHex("#ff7f0e"), Color.FromHex("#17becf"),
    };

    public static void LinePlot(string file, string title, string xLabel, string yLabel, List<Line> lines,
        double? referenceY = null, string referenceLabel = "")
    {
        var plot = CreatePlot(title, xLabel, yLabel);
        for (var i = 0; i < lines.Count; i++)
        {
            var scatter = plot.Add.ScatterLine(lines[i].X, lines[i].Y);
            scatter.LegendText = lines[i].Label;
            scatter.LineWidth = 2;
            scatter.Color = Colors[lines[i].ColorIndex % Colors.Length];
            if (lines[i].Dashed) scatter.LinePattern = LinePattern.Dashed;
        }

        if (referenceY != null)
        {
            var line = plot.Add.HorizontalLine(referenceY.Value);
            line.LegendText = referenceLabel;
            line.Color = ScottPlot.Colors.Black;
            line.LinePattern = LinePattern.Dotted;
        }

        plot.ShowLegend(Alignment.UpperRight);
        plot.SavePng(file, 900, 560);
    }

    // Стовпчики: середнє значення та стандартне відхилення («вуса»)
    public static void BarPlot(string file, string title, string yLabel, List<string> labels,
        List<double> means, List<double> stdDevs)
    {
        var plot = CreatePlot(title, "", yLabel);
        var bars = new List<Bar>();
        for (var i = 0; i < labels.Count; i++)
            bars.Add(new Bar { Position = i, Value = means[i], Error = stdDevs[i], FillColor = Colors[i % Colors.Length] });
        plot.Add.Bars(bars);

        var positions = Enumerable.Range(0, labels.Count).Select(i => (double)i).ToArray();
        plot.Axes.Bottom.TickGenerator = new ScottPlot.TickGenerators.NumericManual(positions, labels.ToArray());
        plot.Axes.Margins(bottom: 0);
        plot.SavePng(file, Math.Max(700, 130 * labels.Count), 560);
    }

    public static void Heatmap(string file, string title, double[,] values, double min, double max)
    {
        var plot = CreatePlot(title, "x1", "x2");
        var heatmap = plot.Add.Heatmap(values);
        heatmap.Extent = new CoordinateRect(min, max, min, max);
        heatmap.Colormap = new ScottPlot.Colormaps.Viridis();
        plot.Add.ColorBar(heatmap);
        plot.SavePng(file, 800, 700);
    }

    private static Plot CreatePlot(string title, string xLabel, string yLabel)
    {
        var plot = new Plot();
        plot.Font.Automatic(); // шрифт з підтримкою кирилиці
        plot.Title(title);
        plot.XLabel(xLabel);
        plot.YLabel(yLabel);
        return plot;
    }
}
