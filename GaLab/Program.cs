using GaLab.Functions;
using GaLab.Schedule;

// Запуск: dotnet run -c Release -- [all | func | sched] [кількість запусків]
// Результати (таблиці CSV, графіки PNG, розклади) записуються в папку results.
Console.OutputEncoding = System.Text.Encoding.UTF8;

var mode = args.Length > 0 ? args[0] : "all";
var runs = args.Length > 1 ? int.Parse(args[1]) : 20;
var outDir = Path.GetFullPath("results");
var dataDir = Path.Combine(AppContext.BaseDirectory, "Data");

if (mode == "all" || mode == "func")
{
    Console.WriteLine("Задача 1: розривна функція Растригіна");
    FunctionExperiment.Run(Path.Combine(outDir, "functions"), runs);
}

if (mode == "all" || mode == "sched")
{
    Console.WriteLine("Задача 2: складання розкладу IT-конференції");
    ScheduleExperiment.Run(dataDir, Path.Combine(outDir, "schedule"), runs);

    Console.WriteLine("Невдалі налаштування (набір medium)");
    ScheduleExperiment.RunBadSettings(dataDir, Path.Combine(outDir, "schedule"), runs);
}

Console.WriteLine("Результати збережено в " + outDir);
