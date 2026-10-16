namespace GaLab.Functions;

// Функція для мінімізації на області [Min; Max]^n
public interface ITestFunction
{
    string Name { get; }
    double Min { get; }
    double Max { get; }
    double Calculate(double[] x);
}

// Розривна функція Растригіна:
//   f(x) = Σ [ y_i² − 10·cos(2π·y_i) + 10 ],
//   y_i = x_i,               якщо |x_i| < 0.5
//   y_i = round(2·x_i) / 2,  якщо |x_i| >= 0.5
// Область [-5.12; 5.12]^n, глобальний мінімум f(0) = 0.

public class NonContinuousRastrigin : ITestFunction
{
    public string Name => "Розривна функція Растригіна";
    public double Min => -5.12;
    public double Max => 5.12;

    public double Calculate(double[] x)
    {
        double sum = 0;
        foreach (var xi in x)
        {
            var y = Math.Abs(xi) < 0.5 ? xi : Math.Round(2 * xi, MidpointRounding.AwayFromZero) / 2;
            sum += y * y - 10 * Math.Cos(2 * Math.PI * y) + 10;
        }

        return sum;
    }
}

// Гладка опукла функція для контролю: на ній градієнтний спуск має бути кращим за ГА
public class Sphere : ITestFunction
{
    public string Name => "Сфера (гладка)";
    public double Min => -5.12;
    public double Max => 5.12;

    public double Calculate(double[] x)
    {
        double sum = 0;
        foreach (var xi in x) sum += xi * xi;
        return sum;
    }
}
