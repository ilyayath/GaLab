using GaLab.Functions;

// Запуск: dotnet run -c Release -- [all | func] [кількість запусків]
// Результати (таблиці CSV, графіки PNG, розклади) записуються в папку results.
Console.OutputEncoding = System.Text.Encoding.UTF8;

var mode = args.Length > 0 ? args[0] : "all";
var runs = args.Length > 1 ? int.Parse(args[1]) : 20;
var outDir = Path.GetFullPath("results");

if (mode == "all" || mode == "func")
{
    Console.WriteLine("Задача 1: розривна функція Растригіна");
    FunctionExperiment.Run(Path.Combine(outDir, "functions"), runs);
}

Console.WriteLine("Результати збережено в " + outDir);
