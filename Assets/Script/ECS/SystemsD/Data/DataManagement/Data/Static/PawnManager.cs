using Unity.Entities;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

public static class PawnManager
{
    public static Entity Create(EntityManager em)
    {
        var id = "pawn";
        var entity = em.CreateEntity();
        var number = Database.GetNextNumber(DataType.Pawn, id);
        em.SetName(entity,$"Pawn_{number}");
        em.AddComponentData(entity, new ID
        {
            DataType = DataType.Pawn,
            Id = id,
            NumId = number,
            Name = ""
        });


        em.AddComponentData(entity, new Size { Value = new int2(1, 1) });
        var pawnAttributes = PawnAttributeGenerator.GenerateRandom();
        em.AddComponentData(entity, pawnAttributes);
        var attributeBuffer = em.AddBuffer<PawnAttributeEntry>(entity);
        PawnAttributeGenerator.InitializeRandomAttributes(ref pawnAttributes, attributeBuffer);
        em.SetComponentData(entity, pawnAttributes);
        em.AddBuffer<PathNode>(entity);

        if (!em.HasComponent<CurrentAction>(entity))
            em.AddComponentData(entity, new CurrentAction { Action = Entity.Null });

        if (!em.HasComponent<CurrentTask>(entity))
            em.AddComponentData(entity, new CurrentTask { Task = Entity.Null });
        if(!em.HasComponent<TaskSend>(entity)) em.AddComponentData(entity, new  TaskSend { Task = Entity.Null});

        InventoryManager.Create(em, entity,InventoryType.PawnInventory,3);
        PawnNeedManager.Initialize(em, entity);
        AttributeManager.Create(em, entity);
        PawnValueManager.Initialize(em, entity);
        SalaryManager.Initialize(em, entity);
        PawnValueManager.Refresh(em, entity);
        SatisfactionManager.Initialize(em, entity);
        em.AddComponentData(entity, new PawnScheduleState { CurrentTag = ScheduleTag.FreeTime });

        Database.AddInstance(DataType.Pawn, id, number, entity,em);
        return entity;
    }
    
}
public struct PathNode : IBufferElementData
{
    public int2 Position;
}
public struct PawnAttributes : IComponentData
{
    public int Level;
    public int Experience;
    public int ExperienceMax;
}

public struct PawnAttributeEntry : IBufferElementData
{
    public FixedString32Bytes Attribute;
    public int Level;
    public int Flames;

    public static PawnAttributeEntry Create(FixedString32Bytes attribute, int level, int flames)
    {
        return new PawnAttributeEntry
        {
            Attribute = attribute,
            Level = level,
            Flames = flames
        };
    }
}

public static class PawnAttributeGenerator
{
    private const int MaxLevel = 50;

    public static PawnAttributes GenerateRandom()
    {
        return new PawnAttributes { Level = 1, Experience = 0, ExperienceMax = 100 };
    }

    public static void InitializeRandomAttributes(ref PawnAttributes pawnAttributes, DynamicBuffer<PawnAttributeEntry> attributes)
    {
        attributes.Clear();
        foreach (var definition in AttributeDatabase.GetAll())
            attributes.Add(PawnAttributeEntry.Create(new FixedString32Bytes(definition.Id), UnityEngine.Random.Range(0, 6), UnityEngine.Random.Range(0, 3)));

        var randomLevel = UnityEngine.Random.Range(1, 7);
        for (var level = 1; level < randomLevel; level++)
            LevelUp(ref pawnAttributes, attributes);
    }

    public static bool LevelUp(EntityManager em, Entity pawn)
    {
        if (em == null || pawn == Entity.Null || !em.Exists(pawn) || !em.HasComponent<PawnAttributes>(pawn) || !em.HasBuffer<PawnAttributeEntry>(pawn))
            return false;

        var attributes = em.GetComponentData<PawnAttributes>(pawn);
        if (!LevelUp(ref attributes, em.GetBuffer<PawnAttributeEntry>(pawn)))
            return false;

        em.SetComponentData(pawn, attributes);
        PawnValueManager.Refresh(em, pawn);
        return true;
    }

    public static bool LevelUp(ref PawnAttributes attributes, DynamicBuffer<PawnAttributeEntry> entries)
    {
        if (attributes.Level >= MaxLevel)
            return false;

        attributes.Level++;
        if (attributes.ExperienceMax <= 0)
            attributes.ExperienceMax = 100;
        attributes.ExperienceMax = Mathf.CeilToInt(attributes.ExperienceMax * 1.2f);
        for (var point = 0; point < 3; point++)
        {
            var attribute = GetRandomAttribute(entries);
            AddPoint(entries, attribute);
            var flames = GetEntry(entries, attribute).Flames;
            var guaranteedBonus = flames / 2;
            for (var bonus = 0; bonus < guaranteedBonus; bonus++)
                AddPoint(entries, attribute);

            if (flames % 2 == 1 && UnityEngine.Random.value < 0.5f)
                AddPoint(entries, attribute);
        }

        return true;
    }

    public static bool AddExp(EntityManager em, Entity pawn, int experience)
    {
        if (em == null || pawn == Entity.Null || !em.Exists(pawn) || !em.HasComponent<PawnAttributes>(pawn) || !em.HasBuffer<PawnAttributeEntry>(pawn))
            return false;

        var attributes = em.GetComponentData<PawnAttributes>(pawn);
        if (!AddExp(ref attributes, em.GetBuffer<PawnAttributeEntry>(pawn), experience))
            return false;

        em.SetComponentData(pawn, attributes);
        return true;
    }

    public static bool AddExp(ref PawnAttributes attributes, DynamicBuffer<PawnAttributeEntry> entries, int experience)
    {
        if (experience <= 0)
            return false;

        if (attributes.ExperienceMax <= 0)
            attributes.ExperienceMax = 100;

        if (attributes.Level >= MaxLevel)
        {
            attributes.Experience = Mathf.Min(attributes.Experience + experience, attributes.ExperienceMax);
            return true;
        }

        attributes.Experience += experience;
        while (attributes.Experience >= attributes.ExperienceMax && attributes.Level < MaxLevel)
        {
            attributes.Experience -= attributes.ExperienceMax;
            LevelUp(ref attributes, entries);
        }

        return true;
    }

    private static FixedString32Bytes GetRandomAttribute(DynamicBuffer<PawnAttributeEntry> entries)
    {
        if (entries.Length == 0)
            return default;

        return entries[UnityEngine.Random.Range(0, entries.Length)].Attribute;
    }

    private static PawnAttributeEntry GetEntry(DynamicBuffer<PawnAttributeEntry> entries, FixedString32Bytes attribute)
    {
        for (var index = 0; index < entries.Length; index++)
            if (entries[index].Attribute == attribute)
                return entries[index];
        return PawnAttributeEntry.Create(attribute, 0, 0);
    }

    public static int GetValue(DynamicBuffer<PawnAttributeEntry> entries, FixedString32Bytes attribute)
    {
        return GetEntry(entries, attribute).Level;
    }

    public static int GetFlames(DynamicBuffer<PawnAttributeEntry> entries, FixedString32Bytes attribute)
    {
        return GetEntry(entries, attribute).Flames;
    }

    public static void AddPoint(DynamicBuffer<PawnAttributeEntry> entries, FixedString32Bytes attribute)
    {
        AddPointInternal(entries, attribute);
    }

    private static void AddPointInternal(DynamicBuffer<PawnAttributeEntry> entries, FixedString32Bytes attribute)
    {
        for (var index = 0; index < entries.Length; index++)
        {
            var entry = entries[index];
            if (entry.Attribute != attribute)
                continue;
            entry.Level = Mathf.Min(entry.Level + 1, 10);
            entries[index] = entry;
            return;
        }
    }
}
