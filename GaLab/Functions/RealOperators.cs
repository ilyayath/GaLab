using GeneticSharp;

namespace GaLab.Functions;

// Інтервальний кросовер BLX-α для дійсних хромосом.
// Для кожного гена: якщо в батьків значення a і b, то нащадок отримує випадкове число
// з інтервалу [min(a,b) − α·d; max(a,b) + α·d], де d = |a − b|.
public class BlxCrossover : CrossoverBase
{
    private readonly double _alpha;

    public BlxCrossover(double alpha) : base(2, 2)
    {
        _alpha = alpha;
    }

    protected override IList<IChromosome> PerformCross(IList<IChromosome> parents)
    {
        var parent1 = (RealChromosome)parents[0];
        var parent2 = (RealChromosome)parents[1];
        var child1 = parent1.CreateNew();
        var child2 = parent1.CreateNew();

        for (var i = 0; i < parent1.Length; i++)
        {
            var a = (double)parent1.GetGene(i).Value;
            var b = (double)parent2.GetGene(i).Value;
            var low = Math.Min(a, b) - _alpha * Math.Abs(a - b);
            var high = Math.Max(a, b) + _alpha * Math.Abs(a - b);

            child1.ReplaceGene(i, new Gene(RandomInRange(low, high, parent1)));
            child2.ReplaceGene(i, new Gene(RandomInRange(low, high, parent1)));
        }

        return new List<IChromosome> { child1, child2 };
    }

    private static double RandomInRange(double low, double high, RealChromosome c)
    {
        var value = low + RandomizationProvider.Current.GetDouble() * (high - low);
        return Math.Clamp(value, c.Min, c.Max);
    }
}

// Гаусова мутація: кожен ген з імовірністю probability отримує додаток sigma·N(0, 1)
public class GaussianMutation : MutationBase
{
    private readonly double _sigma;

    public GaussianMutation(double sigma)
    {
        _sigma = sigma;
    }

    protected override void PerformMutate(IChromosome chromosome, float probability)
    {
        var c = (RealChromosome)chromosome;
        var random = RandomizationProvider.Current;

        for (var i = 0; i < c.Length; i++)
        {
            if (random.GetDouble() > probability) continue;

            // нормальний розподіл з двох рівномірних (перетворення Бокса - Мюллера)
            var u1 = 1.0 - random.GetDouble();
            var u2 = random.GetDouble();
            var normal = Math.Sqrt(-2 * Math.Log(u1)) * Math.Cos(2 * Math.PI * u2);

            var value = (double)c.GetGene(i).Value + _sigma * normal;
            c.ReplaceGene(i, new Gene(Math.Clamp(value, c.Min, c.Max)));
        }
    }
}
