using GeneticSharp;

namespace GaLab.Functions;

// Дійсне кодування: ген i - це просто значення x_i
public class RealChromosome : ChromosomeBase
{
    public readonly double Min;
    public readonly double Max;

    public RealChromosome(int n, double min, double max) : base(n)
    {
        Min = min;
        Max = max;
        CreateGenes();
    }

    public override Gene GenerateGene(int geneIndex)
    {
        return new Gene(Min + RandomizationProvider.Current.GetDouble() * (Max - Min));
    }

    public override IChromosome CreateNew() => new RealChromosome(Length, Min, Max);

    public double[] GetValues()
    {
        var x = new double[Length];
        for (var i = 0; i < Length; i++) x[i] = (double)GetGene(i).Value;
        return x;
    }
}

// Бінарне кодування: кожна змінна - Bits бітів, ціле число лінійно переводиться в [Min; Max]
public class BinaryChromosome : BinaryChromosomeBase
{
    public readonly int N;
    public readonly int Bits;
    public readonly double Min;
    public readonly double Max;

    public BinaryChromosome(int n, int bits, double min, double max) : base(n * bits)
    {
        N = n;
        Bits = bits;
        Min = min;
        Max = max;
        CreateGenes();
    }

    public override IChromosome CreateNew() => new BinaryChromosome(N, Bits, Min, Max);

    public double[] GetValues()
    {
        var x = new double[N];
        var maxInt = Math.Pow(2, Bits) - 1;
        for (var v = 0; v < N; v++)
        {
            long number = 0;
            for (var b = 0; b < Bits; b++)
                number = number * 2 + (int)GetGene(v * Bits + b).Value;
            x[v] = Min + (Max - Min) * number / maxInt;
        }

        return x;
    }
}

// Пристосованість = −f(x), бо GeneticSharp шукає максимум, а нам потрібен мінімум f
public class FunctionFitness : IFitness
{
    private readonly ITestFunction _function;

    public FunctionFitness(ITestFunction function)
    {
        _function = function;
    }

    public double Evaluate(IChromosome chromosome)
    {
        return -_function.Calculate(GetValues(chromosome));
    }

    public static double[] GetValues(IChromosome chromosome)
    {
        if (chromosome is RealChromosome real) return real.GetValues();
        return ((BinaryChromosome)chromosome).GetValues();
    }
}
