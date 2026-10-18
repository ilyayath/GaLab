using System.Diagnostics;
using System.Text;
using GaLab.Common;
using GeneticSharp;

namespace GaLab.Schedule;

// Експеримент 2: складання розкладу. 3 конфігурації ГА × 3 набори даних + жадібний алгоритм.
public static class ScheduleExperiment
{
    public static readonly GaSettings[] Configs =
    {
        new("S1", populationSize: 60, generations: 800, crossoverProbability: 1.0f, mutationProbability: 0.03f, tournamentSize: 2),
        new("S2", populationSize: 100, generations: 500, crossoverProbability: 0.9f, mutationProbability: 0.02f, tournamentSize: 3),
        new("S3", populationSize: 100, generations: 500, crossoverProbability: 0.9f, mutationProbability: 0.02f, tournamentSize: 3, useRepair: true),
    };

    public static readonly string[] Datasets = { "small", "medium", "large" };

    // Результат одного запуску ГА
    private class RunInfo
    {
        public string Config = "";
        public int Run;
        public int[] Genes = Array.Empty<int>();
        public Penalty Penalty = new();
        public double Seconds;
        public int FirstFeasibleGeneration = -1;
        public List<double> BestPenalty = new();   // найкращий штраф по поколіннях
        public List<double> FeasibleShare = new(); // частка допустимих особин у популяції
        public List<double> Diversity = new();     // різноманітність популяції
    }

    public static void Run(string dataDir, string outDir, int runs)
    {
        Directory.CreateDirectory(outDir);
        var summary = new Csv();
        summary.Add("dataset", "method", "runs", "mean_total", "std_total", "median_total", "best_total",
            "feasible_rate", "mean_first_feasible_gen", "mean_seconds");
        var runsCsv = new Csv();
        runsCsv.Add(new object[] { "dataset", "config", "run", "total", "hard", "soft", "seconds" }
            .Concat(new Penalty().Breakdown().Select(b => (object)b.Name)).ToArray());

        foreach (var key in Datasets)
        {
            var data = ScheduleData.Load(Path.Combine(dataDir, key + ".json"));
            Console.WriteLine($"  {data.Name}: {data.Sessions.Count} сесій, {data.SlotCount} слотів, {data.HallCount} зал");

            // жадібний алгоритм
            var timer = Stopwatch.StartNew();
            var greedyGenes = GreedyScheduler.Build(data);
            var greedySeconds = timer.Elapsed.TotalSeconds;
            var greedy = Penalty.Calculate(data, greedyGenes);
            Console.WriteLine($"    жадібний: штраф {greedy.Total:0.#}");
            summary.Add(key, "Жадібний", 1, greedy.Total, 0.0, greedy.Total, greedy.Total,
                greedy.IsFeasible ? 1.0 : 0.0, "", greedySeconds);

            var all = new List<RunInfo>();
            foreach (var config in Configs)
            {
                var infos = new List<RunInfo>();
                for (var run = 0; run < runs; run++)
                    infos.Add(RunOnce(data, config, run, Penalty.HardWeight));
                all.AddRange(infos);

                var totals = infos.Select(r => r.Penalty.Total).ToList();
                var firstFeasible = infos.Where(r => r.FirstFeasibleGeneration >= 0).Select(r => (double)r.FirstFeasibleGeneration).ToList();
                summary.Add(key, config.Name, runs, Statistics.Mean(totals), Statistics.StdDev(totals),
                    Statistics.Median(totals), totals.Min(), infos.Count(r => r.Penalty.IsFeasible) / (double)runs,
                    Statistics.Mean(firstFeasible), infos.Average(r => r.Seconds));
                Console.WriteLine($"    {config.Name}: середній штраф {Statistics.Mean(totals):0.#}, " +
                                  $"допустимих {infos.Count(r => r.Penalty.IsFeasible)}/{runs}");

                foreach (var r in infos)
                {
                    runsCsv.Add(new object[] { key, r.Config, r.Run, r.Penalty.Total, r.Penalty.HardCount, r.Penalty.Soft, r.Seconds }
                        .Concat(r.Penalty.Breakdown().Select(b => (object)b.Count)).ToArray());
                }
            }

            SavePlots(outDir, key, data, all, greedy, runs);
            SaveBest(outDir, key, data, all, greedyGenes, greedy);
        }

        summary.Save(Path.Combine(outDir, "summary.csv"));
        runsCsv.Save(Path.Combine(outDir, "runs.csv"));
    }

    private static RunInfo RunOnce(ScheduleData data, GaSettings config, int run, double hardWeight)
    {
        var info = new RunInfo { Config = config.Name, Run = run };

        var result = GaRunner.Run(config, new ScheduleChromosome(data), new ScheduleFitness(data, hardWeight),
            new UniformCrossover(), new ScheduleMutation(config.UseRepair),
            population =>
            {
                var feasible = 0;
                var best = population[0];
                foreach (var c in population)
                {
                    if (c.Fitness > best.Fitness) best = c;
                    if (Penalty.Calculate(data, ((ScheduleChromosome)c).ToArray()).IsFeasible) feasible++;
                }

                info.FeasibleShare.Add(feasible / (double)population.Count);
                info.Diversity.Add(Diversity(population, (ScheduleChromosome)best));
            });

        // історія найкращого штрафу по поколіннях
        foreach (var fitness in result.BestFitnessHistory) info.BestPenalty.Add(-fitness);
        info.Genes = ((ScheduleChromosome)result.Best).ToArray();
        info.Penalty = Penalty.Calculate(data, info.Genes);
        info.Seconds = result.Seconds;

        // перше покоління, в якому знайдено допустимий розклад
        for (var g = 0; g < info.FeasibleShare.Count; g++)
        {
            if (info.FeasibleShare[g] > 0)
            {
                info.FirstFeasibleGeneration = g + 1;
                break;
            }
        }

        return info;
    }

    // Різноманітність: середня частка генів, якими особини відрізняються від найкращої
    private static double Diversity(IList<IChromosome> population, ScheduleChromosome best)
    {
        var bestGenes = best.ToArray();
        var total = 0.0;
        foreach (var c in population)
        {
            var genes = ((ScheduleChromosome)c).ToArray();
            var different = 0;
            for (var i = 0; i < genes.Length; i++)
                if (genes[i] != bestGenes[i]) different++;
            total += different / (double)genes.Length;
        }

        return total / population.Count;
    }

    private static void SavePlots(string outDir, string key, ScheduleData data, List<RunInfo> all, Penalty greedy, int runs)
    {
        var convergence = new List<Charts.Line>();
        var feasible = new List<Charts.Line>();
        var diversity = new List<Charts.Line>();
        var labels = new List<string> { "Жадібний" };
        var means = new List<double> { greedy.Total };
        var stds = new List<double> { 0 };

        for (var c = 0; c < Configs.Length; c++)
        {
            var infos = all.Where(r => r.Config == Configs[c].Name).ToList();
            var label = $"{Configs[c].Name}{(Configs[c].UseRepair ? " (з ремонтом)" : "")}";

            var bestCurve = Statistics.MeanCurve(infos.Select(r => r.BestPenalty.Select(v => Math.Log10(1 + v)).ToList()).ToList());
            var x = Enumerable.Range(1, bestCurve.Length).Select(g => (double)g).ToArray();
            convergence.Add(new Charts.Line(label, x, bestCurve, c));
            feasible.Add(new Charts.Line(label, x, Statistics.MeanCurve(infos.Select(r => r.FeasibleShare).ToList()), c));
            diversity.Add(new Charts.Line(label, x, Statistics.MeanCurve(infos.Select(r => r.Diversity).ToList()), c));

            var totals = infos.Select(r => r.Penalty.Total).ToList();
            labels.Add(Configs[c].Name);
            means.Add(Statistics.Mean(totals));
            stds.Add(Statistics.StdDev(totals));
        }

        Charts.LinePlot(Path.Combine(outDir, $"convergence_{key}.png"),
            $"{data.Name}: найкращий штраф (середнє за {runs} запусками)", "Покоління", "log10(1 + штраф)",
            convergence, Math.Log10(1 + greedy.Total), "Жадібний алгоритм");
        Charts.LinePlot(Path.Combine(outDir, $"feasible_{key}.png"),
            $"{data.Name}: частка допустимих розкладів у популяції", "Покоління", "частка", feasible);
        Charts.LinePlot(Path.Combine(outDir, $"diversity_{key}.png"),
            $"{data.Name}: різноманітність популяції", "Покоління", "частка генів, відмінних від найкращої особини", diversity);
        Charts.BarPlot(Path.Combine(outDir, $"final_{key}.png"),
            $"{data.Name}: підсумковий штраф (середнє ± std)", "штраф", labels, means, stds);
    }

    private static void SaveBest(string outDir, string key, ScheduleData data, List<RunInfo> all, int[] greedyGenes, Penalty greedy)
    {
        var best = all.OrderBy(r => r.Penalty.Total).First();

        var text = $"# {data.Name}\n\n{data.Description}\n\n" +
                   $"Найкращий розклад ГА: конфігурація {best.Config}, запуск {best.Run + 1}.\n\n" +
                   "## Штрафи найкращого розкладу ГА\n\n" + ScheduleWriter.BreakdownTable(best.Penalty) +
                   "\n## Штрафи розкладу жадібного алгоритму\n\n" + ScheduleWriter.BreakdownTable(greedy) +
                   "\n## Найкращий розклад ГА\n\n";
        for (var t = 0; t < data.Tracks.Count; t++) text += ScheduleWriter.TrackTable(data, best.Genes, t);

        File.WriteAllText(Path.Combine(outDir, $"best_{key}.md"), text);
        ScheduleWriter.SaveScheduleCsv(Path.Combine(outDir, $"best_{key}.csv"), data, best.Genes);
        ScheduleWriter.SaveScheduleCsv(Path.Combine(outDir, $"greedy_{key}.csv"), data, greedyGenes);
    }

    // Приклади невдалих налаштувань на наборі medium (за основу взято S2)
    public static void RunBadSettings(string dataDir, string outDir, int runs)
    {
        var data = ScheduleData.Load(Path.Combine(dataDir, "medium.json"));
        var variants = new List<(string Name, GaSettings Config, double HardWeight)>
        {
            ("B0: як S2", Configs[1], Penalty.HardWeight),
            ("B1: вага жорстких = 1", Configs[1], 1),
            ("B2: мутація 0.3", new GaSettings("B2", 100, 500, 0.9f, 0.3f, 3), Penalty.HardWeight),
            ("B3: турнір 10, pc = 0.5", new GaSettings("B3", 100, 500, 0.5f, 0.02f, 10), Penalty.HardWeight),
        };

        var csv = new Csv();
        csv.Add("variant", "runs", "mean_hard", "feasible_rate", "mean_soft", "std_soft", "final_diversity", "mean_first_feasible_gen");
        var feasibleLines = new List<Charts.Line>();
        var diversityLines = new List<Charts.Line>();

        for (var v = 0; v < variants.Count; v++)
        {
            var infos = new List<RunInfo>();
            for (var run = 0; run < runs; run++)
                infos.Add(RunOnce(data, variants[v].Config, run, variants[v].HardWeight));

            var soft = infos.Select(r => r.Penalty.Soft).ToList();
            var firstFeasible = infos.Where(r => r.FirstFeasibleGeneration >= 0).Select(r => (double)r.FirstFeasibleGeneration).ToList();
            csv.Add(variants[v].Name, runs, infos.Average(r => r.Penalty.HardCount), infos.Count(r => r.Penalty.IsFeasible) / (double)runs,
                Statistics.Mean(soft), Statistics.StdDev(soft), infos.Average(r => r.Diversity[^1]), Statistics.Mean(firstFeasible));
            Console.WriteLine($"    {variants[v].Name}: жорстких {infos.Average(r => r.Penalty.HardCount):0.##}, " +
                              $"м'який штраф {Statistics.Mean(soft):0.#}, допустимих {infos.Count(r => r.Penalty.IsFeasible)}/{runs}");

            // крива «частка допустимих» та різноманітність по поколіннях
            var feasibleCurve = Statistics.MeanCurve(infos.Select(r => r.FeasibleShare).ToList());
            var x = Enumerable.Range(1, feasibleCurve.Length).Select(g => (double)g).ToArray();
            feasibleLines.Add(new Charts.Line(variants[v].Name, x, feasibleCurve, v));
            diversityLines.Add(new Charts.Line(variants[v].Name, x, Statistics.MeanCurve(infos.Select(r => r.Diversity).ToList()), v));
        }

        csv.Save(Path.Combine(outDir, "bad_settings.csv"));
        Charts.LinePlot(Path.Combine(outDir, "bad_settings_feasible.png"),
            $"{data.Name}: невдалі налаштування — частка допустимих розкладів", "Покоління", "частка", feasibleLines);
        Charts.LinePlot(Path.Combine(outDir, "bad_settings_diversity.png"),
            $"{data.Name}: невдалі налаштування — різноманітність популяції", "Покоління",
            "частка генів, відмінних від найкращої особини", diversityLines);
    }
}

// Виведення розкладу і штрафів у файли
public static class ScheduleWriter
{
    public static string BreakdownTable(Penalty penalty)
    {
        var sb = new StringBuilder();
        sb.AppendLine("| Обмеження | Вид | Кількість | Вага | Штраф |");
        sb.AppendLine("|---|---|---:|---:|---:|");
        foreach (var (name, kind, count, weight) in penalty.Breakdown())
            sb.AppendLine($"| {name} | {kind} | {count:0.##} | {weight} | {count * weight:0.##} |");
        sb.AppendLine($"| **Разом** | | | | **{penalty.Total:0.##}** |");
        return sb.ToString();
    }

    // Таблиця «слот × день» для одного треку
    public static string TrackTable(ScheduleData data, int[] genes, int track)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"#### {data.Tracks[track].Name}");
        sb.AppendLine();
        sb.AppendLine("| Слот | " + string.Join(" | ", data.Days) + " |");
        sb.AppendLine("|---|" + string.Concat(data.Days.Select(_ => "---|")));

        for (var k = 0; k < data.SlotsPerDay; k++)
        {
            var row = $"| {data.SlotTimes[k]} |";
            for (var day = 0; day < data.Days.Count; day++)
            {
                var slot = day * data.SlotsPerDay + k;
                var texts = new List<string>();
                for (var i = 0; i < genes.Length; i++)
                {
                    var session = data.Sessions[i];
                    if (data.GetSlot(genes[i]) != slot || !session.Tracks.Contains(track)) continue;
                    texts.Add($"{session.Title} ({session.Type}), {data.Speakers[session.Speaker]}, {data.Halls[data.GetHall(genes[i])].Name}");
                }

                row += " " + string.Join(" / ", texts) + " |";
            }

            sb.AppendLine(row);
        }

        sb.AppendLine();
        return sb.ToString();
    }

    public static void SaveScheduleCsv(string file, ScheduleData data, int[] genes)
    {
        var csv = new Csv();
        csv.Add("title", "type", "speaker", "tracks", "attendees", "day", "slot", "time", "hall", "capacity", "hall_type");
        var order = Enumerable.Range(0, genes.Length).OrderBy(i => genes[i]);
        foreach (var i in order)
        {
            var session = data.Sessions[i];
            var slot = data.GetSlot(genes[i]);
            var hall = data.Halls[data.GetHall(genes[i])];
            var tracks = string.Join(",", session.Tracks.Select(t => data.Tracks[t].Name));
            csv.Add(session.Title, session.Type, data.Speakers[session.Speaker], tracks, session.Attendees,
                data.Days[data.GetDay(slot)], data.GetSlotOfDay(slot) + 1, data.SlotTimes[data.GetSlotOfDay(slot)],
                hall.Name, hall.Capacity, hall.Type);
        }

        csv.Save(file);
    }
}
