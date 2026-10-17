namespace GaLab.Schedule;

// Таблиці зайнятості: скільки сесій має кожен доповідач / трек / зала в кожному слоті.
// Ними користуються і штрафна функція, і ремонт розкладу.
public class Occupancy
{
    public readonly int[,] Speaker;
    public readonly int[,] Track;
    public readonly int[,] Hall;
    private readonly ScheduleData _data;

    public Occupancy(ScheduleData data)
    {
        _data = data;
        Speaker = new int[data.Speakers.Count, data.SlotCount];
        Track = new int[data.Tracks.Count, data.SlotCount];
        Hall = new int[data.HallCount, data.SlotCount];
    }

    // Поставити (delta = 1) або прибрати (delta = −1) сесію з місця gene
    public void Add(int sessionIndex, int gene, int delta)
    {
        var session = _data.Sessions[sessionIndex];
        var slot = _data.GetSlot(gene);
        Hall[_data.GetHall(gene), slot] += delta;
        Speaker[session.Speaker, slot] += delta;
        foreach (var t in session.Tracks) Track[t, slot] += delta;
    }

    // Чи вільні в місці gene зала, доповідач і всі треки сесії
    public bool IsFree(Session session, int gene)
    {
        var slot = _data.GetSlot(gene);
        return Hall[_data.GetHall(gene), slot] == 0
               && Speaker[session.Speaker, slot] == 0
               && session.Tracks.All(t => Track[t, slot] == 0);
    }
}

// Розшифровка штрафу розкладу за класами обмежень
public class Penalty
{
    // Ваги штрафів
    public const double HardWeight = 1000;
    public const double WindowWeight = 3;
    public const double OverloadWeight = 4;
    public const double UnevenWeight = 1;

    // Жорсткі обмеження (кількість порушень)
    public int SpeakerConflicts; // доповідач має дві сесії одночасно
    public int TrackConflicts;   // у треку дві сесії одночасно
    public int HallConflicts;    // у залі дві сесії одночасно
    public int WrongHall;        // зала не того типу або замала
    public int WrongTime;        // сесія не в той час, який зафіксували організатори (відкриття, закриття)

    // М'які обмеження
    public int Windows;          // «вікна» в програмі треку
    public int Overload;         // сесії треку понад MaxSessionsPerDay за день
    public double Uneven;        // нерівномірність програми треку по днях

    public int HardCount => SpeakerConflicts + TrackConflicts + HallConflicts + WrongHall + WrongTime;

    public double Soft => WindowWeight * Windows + OverloadWeight * Overload + UnevenWeight * Uneven;

    public double Total => HardWeight * HardCount + Soft;

    public bool IsFeasible => HardCount == 0;

    // Обчислення штрафу для розкладу. genes[i] - ген сесії i, або -1 якщо сесія ще не розміщена.
    public static Penalty Calculate(ScheduleData data, int[] genes)
    {
        var p = new Penalty();
        var busy = new Occupancy(data);

        for (var i = 0; i < genes.Length; i++)
        {
            if (genes[i] < 0) continue;
            var session = data.Sessions[i];
            busy.Add(i, genes[i], 1);
            if (!data.HallFits(session, data.GetHall(genes[i]))) p.WrongHall++;
            if (!session.TimeFits(data.GetSlot(genes[i]))) p.WrongTime++;
        }

        p.SpeakerConflicts = CountConflicts(busy.Speaker);
        p.TrackConflicts = CountConflicts(busy.Track);
        p.HallConflicts = CountConflicts(busy.Hall);

        // Вікна, перевантаження та нерівномірність для кожного треку
        for (var t = 0; t < data.Tracks.Count; t++)
        {
            var loads = new int[data.Days.Count];
            for (var day = 0; day < data.Days.Count; day++)
            {
                var (windows, sessions) = DayInfo(busy.Track, t, day, data.SlotsPerDay);
                p.Windows += windows;
                p.Overload += Math.Max(0, sessions - data.MaxSessionsPerDay);
                loads[day] = sessions;
            }

            var average = loads.Average();
            foreach (var load in loads) p.Uneven += Math.Abs(load - average);
        }

        return p;
    }

    // Сума «зайвих» сесій: якщо в слоті 3 сесії одного доповідача - це 2 конфлікти
    private static int CountConflicts(int[,] busy)
    {
        var conflicts = 0;
        foreach (var count in busy)
            if (count > 1) conflicts += count - 1;
        return conflicts;
    }

    // Для одного дня треку: кількість вільних слотів між першою та останньою сесією і кількість зайнятих слотів
    private static (int Windows, int Sessions) DayInfo(int[,] busy, int track, int day, int slotsPerDay)
    {
        int first = -1, last = -1, sessions = 0;
        for (var k = 0; k < slotsPerDay; k++)
        {
            if (busy[track, day * slotsPerDay + k] == 0) continue;
            if (first < 0) first = k;
            last = k;
            sessions++;
        }

        if (sessions == 0) return (0, 0);
        return (last - first + 1 - sessions, sessions);
    }

    // Рядки для таблиці розшифровки: назва, вид, кількість, вага
    public List<(string Name, string Kind, double Count, double Weight)> Breakdown()
    {
        return new List<(string, string, double, double)>
        {
            ("Конфлікт доповідача", "жорстке", SpeakerConflicts, HardWeight),
            ("Конфлікт треку", "жорстке", TrackConflicts, HardWeight),
            ("Конфлікт зали", "жорстке", HallConflicts, HardWeight),
            ("Зала не підходить", "жорстке", WrongHall, HardWeight),
            ("Фіксований час сесії", "жорстке", WrongTime, HardWeight),
            ("Вікна в програмі треку", "м’яке", Windows, WindowWeight),
            ("Перевантаження дня треку", "м’яке", Overload, OverloadWeight),
            ("Нерівномірність по днях", "м’яке", Uneven, UnevenWeight),
        };
    }
}
