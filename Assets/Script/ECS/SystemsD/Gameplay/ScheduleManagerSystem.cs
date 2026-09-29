using Unity.Entities;

public static class ScheduleManager
{
    public static Entity Create(EntityManager entityManager)
    {
        var query = entityManager.CreateEntityQuery(typeof(ScheduleData));
        if (!query.IsEmpty)
        {
            return query.GetSingletonEntity();
        }

        var scheduleEntity = entityManager.CreateEntity(typeof(ScheduleData));
        var schedule = entityManager.AddBuffer<ScheduleHour>(scheduleEntity);
        for (var hour = 0; hour < 24; hour++)
        {
            schedule.Add(new ScheduleHour { Hour = hour, Tag = ScheduleTag.FreeTime });
        }

        entityManager.SetName(scheduleEntity, "WORLD SCHEDULE");
        return scheduleEntity;
    }

    public static void SetTag(EntityManager entityManager, int hour, ScheduleTag tag)
    {
        if (hour < 0 || hour >= 24) return;

        var scheduleEntity = Create(entityManager);
        var schedule = entityManager.GetBuffer<ScheduleHour>(scheduleEntity);
        schedule[hour] = new ScheduleHour { Hour = hour, Tag = tag };
    }
}

public partial struct ScheduleManagerSystem : ISystem
{
    public void OnUpdate(ref SystemState state)
    {
        if(SystemAPI.TryGetSingletonEntity<ScheduleData>(out var scheduleEntity ))
        {
            
        var hour = SystemAPI.GetSingleton<WorldTime>().Hour;
        var schedule = state.EntityManager.GetBuffer<ScheduleHour>(scheduleEntity);
        foreach (var pawnState in SystemAPI.Query<RefRW<PawnScheduleState>>())
        {
            pawnState.ValueRW.CurrentTag = schedule[hour].Tag;
        }
        }
    }
}

public enum ScheduleTag : byte
{
    Work,
    FreeTime,
    Sleep,
    Lunch,

}

public struct ScheduleHour : IBufferElementData
{
    public int Hour;
    public ScheduleTag Tag;
}

public struct PawnScheduleState : IComponentData
{
    public ScheduleTag CurrentTag;
}

public struct ScheduleData : IComponentData
{
}