using System.Diagnostics;
using GeneticSharp;

namespace GaLab.Common;

// Набір параметрів генетичного алгоритму
public class GaSettings
{
    public string Name = "";
    public int PopulationSize;
    public int Generations;
    public float CrossoverProbability;
    public float MutationProbability; // імовірність мутації одного гена
    public int TournamentSize;
    public bool UseRepair; // ремонт розкладу після мутації (тільки для розкладу)

    public GaSettings(string name, int populationSize, int generations, float crossoverProbability,
        float mutationProbability, int tournamentSize, bool useRepair = false)
    {
        Name = name;
        PopulationSize = populationSize;
        Generations = generations;
        CrossoverProbability = crossoverProbability;
        MutationProbability = mutationProbability;
        TournamentSize = tournamentSize;
        UseRepair = useRepair;
    }
}

public class GaResult
{
    public IChromosome Best = null!;
    // Найкраща пристосованість, знайдена до кожного покоління включно
    public List<double> BestFitnessHistory = new();
    public double Seconds;
}

public static class GaRunner
{
    // Збирає генетичний алгоритм з GeneticSharp і запускає його.
    // afterGeneration викликається після кожного покоління (для збору статистики).
    public static GaResult Run(GaSettings settings, IChromosome adam, IFitness fitness,
        ICrossover crossover, IMutation mutation, Action<IList<IChromosome>>? afterGeneration = null)
    {
        var population = new Population(settings.PopulationSize, settings.PopulationSize, adam);
        var selection = new TournamentSelection(settings.TournamentSize);

        var ga = new GeneticAlgorithm(population, fitness, selection, crossover, mutation);
        ga.CrossoverProbability = settings.CrossoverProbability;
        ga.MutationProbability = settings.MutationProbability;
        ga.Termination = new GenerationNumberTermination(settings.Generations);

        var result = new GaResult();
        ga.GenerationRan += (sender, e) =>
        {
            // ga.BestChromosome - найкраща особина поточного покоління.
            // Вона може бути гіршою за знайдену раніше, тому найкращу за весь запуск зберігаємо окремо.
            var currentBest = ga.BestChromosome;
            if (result.Best == null || currentBest.Fitness > result.Best.Fitness)
                result.Best = currentBest.Clone();

            result.BestFitnessHistory.Add(result.Best.Fitness!.Value);
            afterGeneration?.Invoke(ga.Population.CurrentGeneration.Chromosomes);
        };

        var timer = Stopwatch.StartNew();
        ga.Start();
        result.Seconds = timer.Elapsed.TotalSeconds;
        return result;
    }
}
