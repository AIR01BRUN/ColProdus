using Unity.Entities;

public static class WorldManager
{
    public static void Create(EntityManager em)
    {
        var query = em.CreateEntityQuery(typeof(WorldTime));
        if (query.IsEmpty)
        {
            var entity = em.CreateEntity();
            em.AddComponentData(entity,CreateTime());
            em.AddComponent<DebugWorldTime>(entity);
            em.SetName(entity,"WORLD TIME");
        }
    }
      private static WorldTime CreateTime(int day =1,int year = 1,int hour = 8, int minute = 0)
        {
            return new WorldTime
            {
            Day = 1,
            Year = 1,
            Hour = 0,
            Minute = 0,
            ElapsedSeconds = 0f,
            LastDay = 1
            };
        }
}

public partial struct WorldManagerSystem : ISystem
{
    private const float SecondsPerGameDay = 600f;

    public void OnCreate(ref SystemState state)
    {
        WorldManager.Create(state.EntityManager);
    }

    public void OnUpdate(ref SystemState state)
    {
        var worldTime = SystemAPI.GetSingletonRW<WorldTime>();
        worldTime.ValueRW.ElapsedSeconds += SystemAPI.Time.DeltaTime;

        var minutesPerSecond = 24f * 60f / SecondsPerGameDay;
        var totalMinutes = (int)(worldTime.ValueRO.ElapsedSeconds * minutesPerSecond);
        var minutesPerYear = 365 * 24 * 60;
        var totalMinutesInYear = (worldTime.ValueRO.Day - 1) * 24 * 60 + totalMinutes;

        worldTime.ValueRW.Year = 1 + totalMinutesInYear / minutesPerYear;
        var minuteOfYear = totalMinutesInYear % minutesPerYear;
        worldTime.ValueRW.Day = 1 + minuteOfYear / (24 * 60);
        var minuteOfDay = minuteOfYear % (24 * 60);
        worldTime.ValueRW.Hour = minuteOfDay / 60;
        worldTime.ValueRW.Minute = minuteOfDay % 60;

        if (worldTime.ValueRO.ElapsedSeconds >= SecondsPerGameDay)
        {
            worldTime.ValueRW.ElapsedSeconds %= SecondsPerGameDay;
        }

        // Debug : NextDay on => on passe au jour suivant, heure remise à 0.
        // NextWeek on => on passe la semaine suivante (7 jours d'un coup).
        var debugWorldTime = SystemAPI.GetSingletonRW<DebugWorldTime>();
        var payday = false;
        if (debugWorldTime.ValueRO.NextDay || debugWorldTime.ValueRO.NextWeek)
        {
            var skipWeek = debugWorldTime.ValueRO.NextWeek;
            debugWorldTime.ValueRW.NextDay = false;
            debugWorldTime.ValueRW.NextWeek = false;
            worldTime.ValueRW.Day += skipWeek ? SalaryManager.DaysPerWeek : 1;
            worldTime.ValueRW.Hour = 0;
            worldTime.ValueRW.Minute = 0;
            worldTime.ValueRW.ElapsedSeconds = 0f;
            payday = skipWeek;
            worldTime.ValueRW.LastDay = worldTime.ValueRO.Day;
        }
        else if (worldTime.ValueRO.Day > worldTime.ValueRO.LastDay)
        {
            // Le jour change naturellement : la paie tombe sur les multiples de 7.
            payday = worldTime.ValueRO.Day % SalaryManager.DaysPerWeek == 0;
            worldTime.ValueRW.LastDay = worldTime.ValueRO.Day;
        }

        // Une semaine s'est écoulée : on déclenche la paie (une tâche GetSalary par pawn).
        if (payday)
            SalaryManager.CreatePaydayTasks(state.EntityManager);
    }

  
}

public struct WorldTime : IComponentData
{
    public int Day;
    public int Year;
    public int Hour;
    public int Minute;
    public float ElapsedSeconds;
    public int LastDay;
}

/// <summary>
/// Composant de debug : NextDay on => le monde passe au jour suivant à 00h00,
/// NextWeek on => le monde passe la semaine suivante (7 jours) et déclenche la paie.
/// </summary>
public struct DebugWorldTime : IComponentData
{
    public bool NextDay;
    public bool NextWeek;
}