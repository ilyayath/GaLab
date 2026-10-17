using GaLab.Common;
using GeneticSharp;

namespace GaLab.Functions;

// Експеримент 1: ГА (дійсне і бінарне кодування, 3 конфігурації) проти градієнтного спуску
public static class FunctionExperiment
{
    public static readonly GaSettings[] Configs =
    {
        new("F1", populationSize: 30, generations: 200, crossoverProbability: 1.0f, mutationProbability: 0.2f, tournamentSize: 2),
        new("F2", populationSize: 80, generations: 250, crossoverProbability: 0.9f, mutationProbability: 0.1f, tournamentSize: 3),
        new("F3", populationSize: 160, generations: 250, crossoverProbability: 0.8f, mutationProbability: 0.1f, tournamentSize: 4),
    };

    public static readonly int[] Dimensions = { 2, 5, 10 };
    public const int Bits = 16;          // бітів на одну змінну в бінарному кодуванні
    public const double Alpha = 0.5;     // параметр BLX-α
    public const double Sigma = 0.5;     // σ гаусової мутації
    public const double Target = 0.01;   // запуск успішний, якщо знайдено f <= 0.01
    public const int GdBudget = 40000;   // бюджет обчислень f для градієнтного спуску

    public static void Run(string outDir, int runs)
    {
        Directory.CreateDirectory(outDir);
        DrawFunction(outDir);

        var summary = new Csv();
        summary.Add("function", "n", "method", "config", "runs", "mean_f", "std_f", "median_f", "best_f",
            "success_rate", "mean_evals_to_target", "evaluations", "mean_seconds");

        var tasks = new List<(ITestFunction Function, int N, string Key)>();
        foreach (var n in Dimensions) tasks.Add((new NonContinuousRastrigin(), n, "rastrigin"));
        tasks.Add((new Sphere(), 5, "sphere"));

        foreach (var (function, n, key) in tasks)
        {
            Console.WriteLine($"  {function.Name}, n = {n}");
            var lines = new List<Charts.Line>();
            var barLabels = new List<string>();
            var barMeans = new List<double>();
            var barStds = new List<double>();

            foreach (var encoding in new[] { "real", "binary" })
            {
                for (var c = 0; c < Configs.Length; c++)
                {
                    var config = Configs[c];
                    var finals = new List<double>();
                    var evalsToTarget = new List<double>();
                    var curves = new List<List<double>>();
                    var seconds = 0.0;

                    for (var run = 0; run < runs; run++)
                    {
                        var result = RunGa(function, n, encoding, config);
                        var curve = result.BestFitnessHistory.Select(fit => -fit).ToList(); // назад до f
                        finals.Add(curve[^1]);
                        curves.Add(curve);
                        seconds += result.Seconds;

                        var generation = curve.FindIndex(v => v <= Target);
                        if (generation >= 0) evalsToTarget.Add((generation + 1) * config.PopulationSize);
                    }

                    var method = encoding == "real" ? "ГА, дійсне" : "ГА, бінарне";
                    summary.Add(key, n, method, config.Name, runs, Statistics.Mean(finals), Statistics.StdDev(finals),
                        Statistics.Median(finals), finals.Min(), finals.Count(v => v <= Target) / (double)runs,
                        Statistics.Mean(evalsToTarget), config.PopulationSize * config.Generations, seconds / runs);

                    // крива збіжності: середнє log10(f) по запусках; по осі X - кількість обчислень f
                    var mean = Statistics.MeanCurve(curves.Select(cv => cv.Select(v => Math.Log10(Math.Max(v, 1e-6))).ToList()).ToList());
                    var x = Enumerable.Range(1, mean.Length).Select(g => (double)g * config.PopulationSize).ToArray();
                    lines.Add(new Charts.Line($"{method}, {config.Name}", x, mean, c, encoding == "binary"));

                    barLabels.Add($"{(encoding == "real" ? "real" : "bin")} {config.Name}");
                    barMeans.Add(Statistics.Mean(finals));
                    barStds.Add(Statistics.StdDev(finals));
                }
            }

            // градієнтний спуск
            var gdFinals = new List<double>();
            var gdEvals = new List<double>();
            for (var run = 0; run < runs; run++)
            {
                var gd = new GradientDescent();
                gd.Run(function, n, GdBudget, Target);
                gdFinals.Add(gd.BestValue);
                if (gd.EvaluationsToTarget > 0) gdEvals.Add(gd.EvaluationsToTarget);
            }

            summary.Add(key, n, "Градієнтний спуск", "GD", runs, Statistics.Mean(gdFinals), Statistics.StdDev(gdFinals),
                Statistics.Median(gdFinals), gdFinals.Min(), gdFinals.Count(v => v <= Target) / (double)runs,
                Statistics.Mean(gdEvals), GdBudget, 0.0);
            barLabels.Add("GD");
            barMeans.Add(Statistics.Mean(gdFinals));
            barStds.Add(Statistics.StdDev(gdFinals));

            Charts.LinePlot(Path.Combine(outDir, $"convergence_{key}_n{n}.png"),
                $"{function.Name}, n = {n}: збіжність ГА (середнє за {runs} запусками)",
                "Кількість обчислень функції", "log10 найкращого f", lines);
            Charts.BarPlot(Path.Combine(outDir, $"final_{key}_n{n}.png"),
                $"{function.Name}, n = {n}: підсумкове f (середнє ± std)", "найкраще f", barLabels, barMeans, barStds);
        }

        summary.Save(Path.Combine(outDir, "summary.csv"));
    }

    private static GaResult RunGa(ITestFunction function, int n, string encoding, GaSettings config)
    {
        if (encoding == "real")
        {
            return GaRunner.Run(config, new RealChromosome(n, function.Min, function.Max), new FunctionFitness(function),
                new BlxCrossover(Alpha), new GaussianMutation(Sigma));
        }

        // Для бінарного кодування - готові оператори GeneticSharp.
        // Імовірність мутації ділимо на кількість бітів змінної, щоб у середньому
        // змінювалось стільки ж змінних, скільки і в дійсному кодуванні.
        var binaryConfig = new GaSettings(config.Name, config.PopulationSize, config.Generations,
            config.CrossoverProbability, config.MutationProbability / Bits, config.TournamentSize);
        return GaRunner.Run(binaryConfig, new BinaryChromosome(n, Bits, function.Min, function.Max),
            new FunctionFitness(function), new TwoPointCrossover(), new UniformMutation(true));
    }

    // Графіки самої функції: переріз уздовж x1 і теплова карта при n = 2
    private static void DrawFunction(string outDir)
    {
        var f = new NonContinuousRastrigin();

        var xs = new double[2000];
        var ys = new double[2000];
        for (var i = 0; i < xs.Length; i++)
        {
            xs[i] = f.Min + (f.Max - f.Min) * i / (xs.Length - 1);
            ys[i] = f.Calculate(new[] { xs[i] });
        }

        Charts.LinePlot(Path.Combine(outDir, "function_profile.png"), $"{f.Name}, n = 1", "x", "f(x)",
            new List<Charts.Line> { new("f(x)", xs, ys, 0) });

        const int size = 400;
        var grid = new double[size, size];
        for (var row = 0; row < size; row++)
        for (var col = 0; col < size; col++)
        {
            var x1 = f.Min + (f.Max - f.Min) * col / (size - 1);
            var x2 = f.Max - (f.Max - f.Min) * row / (size - 1); // рядок 0 - верх картинки
            grid[row, col] = f.Calculate(new[] { x1, x2 });
        }

        Charts.Heatmap(Path.Combine(outDir, "function_heatmap.png"), $"{f.Name}, n = 2", grid, f.Min, f.Max);
    }
}
