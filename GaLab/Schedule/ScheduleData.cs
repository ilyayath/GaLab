using System.Text.Json;

namespace GaLab.Schedule;

// Зала конференції
public class Hall
{
    public string Name = "";
    public int Capacity;
    public string Type = ""; // Головна зала, Секційна зала або Воркшоп-зала
}

// Трек - тематичний напрям конференції зі своєю аудиторією.
// Сесії одного треку не можна ставити одночасно: слухачі треку мають встигнути на всі.
public class Track
{
    public string Name = "";
    public int Size; // скільки учасників зареєструвалось на трек
}

// Одна сесія програми = один ген хромосоми.
// Назва, тип, доповідач і треки задає програмний комітет, ГА обирає тільки час і залу.
public class Session
{
    public string Title = "";
    public string Type = "";               // Пленарна, Панель, Доповідь або Воркшоп
    public int Speaker;                    // індекс доповідача
    public List<int> Tracks = new();       // індекси треків (пленарна - для всіх треків)
    public int Attendees;                  // очікувана кількість слухачів
    public List<int> AllowedSlots = new(); // зафіксований час; порожній список - будь-який слот

    public bool TimeFits(int slot) => AllowedSlots.Count == 0 || AllowedSlots.Contains(slot);
}

// Вхідні дані задачі розкладу конференції (завантажуються з JSON-файлу)
public class ScheduleData
{
    public string Name = "";
    public string Description = "";
    public List<string> Days = new();
    public int SlotsPerDay;
    public List<string> SlotTimes = new(); // час початку кожного слоту дня
    public int MaxSessionsPerDay;
    public List<Hall> Halls = new();
    public Dictionary<string, List<string>> HallTypes = new(); // тип сесії -> типи зал, де її можна проводити
    public List<Track> Tracks = new();
    public List<string> Speakers = new();
    public List<Session> Sessions = new();

    public int SlotCount => Days.Count * SlotsPerDay;
    public int HallCount => Halls.Count;

    // Ген = слот * кількість_зал + зала
    public int GeneCount => SlotCount * HallCount;
    public int GetSlot(int gene) => gene / HallCount;
    public int GetHall(int gene) => gene % HallCount;
    public int GetDay(int slot) => slot / SlotsPerDay;
    public int GetSlotOfDay(int slot) => slot % SlotsPerDay; // з нуля

    // Зала підходить, якщо її тип дозволений для типу сесії і всі слухачі вміщуються
    public bool HallFits(Session session, int hall)
    {
        return HallTypes[session.Type].Contains(Halls[hall].Type) && Halls[hall].Capacity >= session.Attendees;
    }

    public static ScheduleData Load(string path)
    {
        var options = new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true };
        using var doc = JsonDocument.Parse(File.ReadAllText(path), options);
        var root = doc.RootElement;
        var data = new ScheduleData();

        data.Name = root.GetProperty("name").GetString()!;
        data.Description = root.GetProperty("description").GetString()!;
        foreach (var d in root.GetProperty("days").EnumerateArray()) data.Days.Add(d.GetString()!);
        data.SlotsPerDay = root.GetProperty("slotsPerDay").GetInt32();
        foreach (var t in root.GetProperty("slotTimes").EnumerateArray()) data.SlotTimes.Add(t.GetString()!);
        data.MaxSessionsPerDay = root.GetProperty("maxSessionsPerDay").GetInt32();

        foreach (var h in root.GetProperty("halls").EnumerateArray())
        {
            data.Halls.Add(new Hall
            {
                Name = h.GetProperty("name").GetString()!,
                Capacity = h.GetProperty("capacity").GetInt32(),
                Type = h.GetProperty("type").GetString()!,
            });
        }

        foreach (var type in root.GetProperty("hallTypes").EnumerateObject())
            data.HallTypes[type.Name] = type.Value.EnumerateArray().Select(x => x.GetString()!).ToList();

        foreach (var t in root.GetProperty("tracks").EnumerateArray())
            data.Tracks.Add(new Track { Name = t.GetProperty("name").GetString()!, Size = t.GetProperty("size").GetInt32() });

        foreach (var json in root.GetProperty("sessions").EnumerateArray())
            data.Sessions.Add(data.ParseSession(json));

        return data;
    }

    private Session ParseSession(JsonElement json)
    {
        var session = new Session
        {
            Title = json.GetProperty("title").GetString()!,
            Type = json.GetProperty("type").GetString()!,
        };
        if (!HallTypes.ContainsKey(session.Type)) throw new Exception("Невідомий тип сесії: " + session.Type);

        // Список доповідачів будується з самих сесій: нове ім'я - новий доповідач
        var speaker = json.GetProperty("speaker").GetString()!;
        if (!Speakers.Contains(speaker)) Speakers.Add(speaker);
        session.Speaker = Speakers.IndexOf(speaker);

        // "Усі" - сесія для всіх треків (пленарна)
        foreach (var name in json.GetProperty("tracks").EnumerateArray().Select(x => x.GetString()!))
        {
            if (name == "Усі")
            {
                session.Tracks.AddRange(Enumerable.Range(0, Tracks.Count));
                continue;
            }

            var index = Tracks.FindIndex(t => t.Name == name);
            if (index < 0) throw new Exception("Невідомий трек: " + name);
            session.Tracks.Add(index);
        }

        // Слухачі - усі учасники треків сесії; для воркшопу місць менше, їх задано явно
        session.Attendees = json.TryGetProperty("attendees", out var attendees)
            ? attendees.GetInt32()
            : session.Tracks.Sum(t => Tracks[t].Size);

        // Час, зафіксований організаторами: "День 2:3" - третій слот другого дня
        if (json.TryGetProperty("slots", out var slots))
        {
            foreach (var item in slots.EnumerateArray())
            {
                var parts = item.GetString()!.Split(':');
                var day = Days.IndexOf(parts[0]);
                if (day < 0) throw new Exception("Невідомий день: " + parts[0]);
                session.AllowedSlots.Add(day * SlotsPerDay + int.Parse(parts[1]) - 1);
            }
        }

        return session;
    }
}
