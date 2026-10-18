using GeneticSharp;

namespace GaLab.Schedule;

// Хромосома розкладу (цілочисельне кодування).
// Ген i відповідає сесії i, значення гена = слот * кількість_зал + зала.
public class ScheduleChromosome : ChromosomeBase
{
    public readonly ScheduleData Data;

    public ScheduleChromosome(ScheduleData data) : base(data.Sessions.Count)
    {
        Data = data;
        CreateGenes();
    }

    public override Gene GenerateGene(int geneIndex)
    {
        return new Gene(RandomizationProvider.Current.GetInt(0, Data.GeneCount));
    }

    public override IChromosome CreateNew() => new ScheduleChromosome(Data);

    public int[] ToArray()
    {
        var genes = new int[Length];
        for (var i = 0; i < Length; i++) genes[i] = (int)GetGene(i).Value;
        return genes;
    }
}

// Пристосованість = −(вага_жорстких · кількість_жорстких + м'який штраф)
public class ScheduleFitness : IFitness
{
    private readonly ScheduleData _data;
    private readonly double _hardWeight;

    public ScheduleFitness(ScheduleData data, double hardWeight = Penalty.HardWeight)
    {
        _data = data;
        _hardWeight = hardWeight;
    }

    public double Evaluate(IChromosome chromosome)
    {
        var penalty = Penalty.Calculate(_data, ((ScheduleChromosome)chromosome).ToArray());
        return -(_hardWeight * penalty.HardCount + penalty.Soft);
    }
}

// Мутація: кожен ген з імовірністю p отримує нове випадкове значення (UniformMutation з GeneticSharp).
// Якщо useRepair = true, після мутації розклад «ремонтується».
public class ScheduleMutation : MutationBase
{
    private readonly UniformMutation _uniform = new(true);
    private readonly bool _useRepair;

    public ScheduleMutation(bool useRepair)
    {
        _useRepair = useRepair;
    }

    protected override void PerformMutate(IChromosome chromosome, float probability)
    {
        _uniform.Mutate(chromosome, probability);
        if (_useRepair) Repair((ScheduleChromosome)chromosome);
    }

    // Ремонт: кожну сесію, яка порушує жорстке обмеження, переносимо в таке місце (слот + зала),
    // де вільні її доповідач, треки й сама зала, зала підходить, а слот не суперечить зафіксованому часу.
    // Якщо такого місця немає - сесія лишається на місці.
    public static void Repair(ScheduleChromosome chromosome)
    {
        var data = chromosome.Data;
        var random = RandomizationProvider.Current;
        var busy = new Occupancy(data);
        for (var i = 0; i < chromosome.Length; i++) busy.Add(i, (int)chromosome.GetGene(i).Value, 1);

        // перебираємо сесії у випадковому порядку
        foreach (var i in Enumerable.Range(0, chromosome.Length).OrderBy(_ => random.GetDouble()).ToList())
        {
            var session = data.Sessions[i];
            var gene = (int)chromosome.GetGene(i).Value;

            // знімаємо сесію з її місця: тепер у таблицях зайнятості лише інші сесії
            busy.Add(i, gene, -1);
            if (!Fits(data, busy, session, gene))
            {
                // шукаємо нове місце, починаючи з випадкового гена
                var start = random.GetInt(0, data.GeneCount);
                for (var k = 0; k < data.GeneCount; k++)
                {
                    var candidate = (start + k) % data.GeneCount;
                    if (!Fits(data, busy, session, candidate)) continue;
                    gene = candidate;
                    chromosome.ReplaceGene(i, new Gene(gene));
                    break;
                }
            }

            busy.Add(i, gene, 1);
        }
    }

    // Чи можна поставити сесію в місце gene, не порушивши жодного жорсткого обмеження
    private static bool Fits(ScheduleData data, Occupancy busy, Session session, int gene)
    {
        return data.HallFits(session, data.GetHall(gene))
               && session.TimeFits(data.GetSlot(gene))
               && busy.IsFree(session, gene);
    }
}

// Жадібний алгоритм для порівняння.
// Спочатку розміщуємо «найважчі» сесії (найменше підходящих зал, найбільше треків),
// кожну ставимо туди, де загальний штраф зростає найменше. Рішення потім не змінюються.
public static class GreedyScheduler
{
    public static int[] Build(ScheduleData data)
    {
        var genes = Enumerable.Repeat(-1, data.Sessions.Count).ToArray();

        var order = Enumerable.Range(0, data.Sessions.Count)
            .OrderBy(i => Enumerable.Range(0, data.HallCount).Count(h => data.HallFits(data.Sessions[i], h)))
            .ThenByDescending(i => data.Sessions[i].Tracks.Count)
            .ToList();

        foreach (var i in order)
        {
            var bestGene = 0;
            var bestPenalty = double.MaxValue;
            for (var gene = 0; gene < data.GeneCount; gene++)
            {
                genes[i] = gene;
                var penalty = Penalty.Calculate(data, genes).Total;
                if (penalty < bestPenalty)
                {
                    bestPenalty = penalty;
                    bestGene = gene;
                }
            }

            genes[i] = bestGene;
        }

        return genes;
    }
}
