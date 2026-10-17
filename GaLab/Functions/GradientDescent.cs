namespace GaLab.Functions;

// Градієнтний спуск з кількома стартами - метод для порівняння з ГА.
// Градієнт рахується чисельно: df/dx_i ≈ (f(x + h·e_i) − f(x − h·e_i)) / 2h.
public class GradientDescent
{
    public double LearningRate = 0.05;
    public double H = 1e-4;
    public int MaxStepsPerStart = 200;

    public double BestValue;
    public int EvaluationsToTarget = -1; // через скільки обчислень вперше досягли f <= target

    private int _evaluations;

    public void Run(ITestFunction f, int n, int budget, double target)
    {
        var random = new Random();
        BestValue = double.MaxValue;
        _evaluations = 0;

        while (_evaluations + 2 * n + 1 <= budget)
        {
            // новий старт з випадкової точки
            var x = new double[n];
            for (var i = 0; i < n; i++) x[i] = f.Min + random.NextDouble() * (f.Max - f.Min);
            Evaluate(f, x, target);

            for (var step = 0; step < MaxStepsPerStart && _evaluations + 2 * n + 1 <= budget; step++)
            {
                var gradient = new double[n];
                for (var i = 0; i < n; i++)
                {
                    var old = x[i];
                    x[i] = old + H;
                    var fPlus = Evaluate(f, x, target);
                    x[i] = old - H;
                    var fMinus = Evaluate(f, x, target);
                    x[i] = old;
                    gradient[i] = (fPlus - fMinus) / (2 * H);
                }

                var moved = false;
                for (var i = 0; i < n; i++)
                {
                    var newValue = Math.Clamp(x[i] - LearningRate * gradient[i], f.Min, f.Max);
                    if (Math.Abs(newValue - x[i]) > 1e-9) moved = true;
                    x[i] = newValue;
                }

                Evaluate(f, x, target);
                if (!moved) break; // градієнт нульовий - далі не рухаємось, робимо новий старт
            }
        }
    }

    private double Evaluate(ITestFunction f, double[] x, double target)
    {
        _evaluations++;
        var value = f.Calculate(x);
        if (value < BestValue) BestValue = value;
        if (BestValue <= target && EvaluationsToTarget < 0) EvaluationsToTarget = _evaluations;
        return value;
    }
}
