# GaLab — Genetic Algorithms (Lab 4)

C# / .NET 10 console app built on [GeneticSharp](https://github.com/giacomelli/GeneticSharp) and [ScottPlot](https://scottplot.net/).

## Tasks

1. **Function optimization** — minimizing a non-continuous Rastrigin function (n = 2, 5, 10) and Sphere with real-coded (BLX-α crossover, Gaussian mutation) and binary chromosomes; gradient descent used as a baseline.
2. **IT-conference scheduling** — assigning sessions to halls and time slots with a penalty-based fitness (hard and soft constraints), repair operator and a greedy baseline. Three datasets: `small`, `medium`, `large`.

## Project structure

```
GaLab/
  Common/      GA runner, settings, CSV and plot output
  Functions/   test functions, chromosomes, operators, experiment
  Schedule/    data model, penalty, schedule GA, experiment
  Data/        schedule datasets (JSON)
results/       generated tables (CSV/MD) and plots (PNG)
Звіт_ЛР4_Генетичні_алгоритми.docx   lab report
```

## Run

```
dotnet run -c Release --project GaLab -- [all | func | sched] [runs]
```

Defaults: `all`, 20 runs. Output is written to `results/`.
