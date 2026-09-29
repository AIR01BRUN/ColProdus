using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;

public static class MineManager
{
    private const float WorkPerLevel = 10f;
    private const float RarityExponent = 2.5f;
    private const string MineId = "mine";
    private const string ExcavationActionId = "FinishExcavation";
    private const string MineActionId = "FinishMine";

    public static void Initialize(EntityManager em, Entity mine, int deepestLevel = 0)
    {
        if (em == null || mine == Entity.Null || !em.Exists(mine))
            return;

        if (!em.HasComponent<MineState>(mine))
        {
            em.AddComponentData(mine, new MineState
            {
                DeepestLevel = 0,
                LevelToMine = 0,
                LastResourceId = default
            });
            em.AddComponentData(mine, new SpawnResourceSelection());
        }

        if (!em.HasBuffer<DepthLevel>(mine))
            em.AddBuffer<DepthLevel>(mine);

        var mineState = em.GetComponentData<MineState>(mine);
        if (mineState.DeepestLevel >= deepestLevel)
            return;

        var definition = QuerryDB.QueryDefinitions<BuildingDefinition>(DataType.Building, MineId).FirstOrDefault();
        var maxDepth = 0;
        if (definition != null && definition.MineLevels.Count > 0)
            maxDepth = Mathf.Min(deepestLevel, definition.MineLevels.Max(level => level.Level));

        var buffer = em.GetBuffer<DepthLevel>(mine);
        buffer.Clear();
        buffer.Add(new DepthLevel { Level = 0, ResourceId = default, Percentage = 0f });

        for (var level = 1; level <= maxDepth; level++)
            CreateLevel(em, mine, level);

        mineState.DeepestLevel = maxDepth;
        mineState.LevelToMine = 0;
        em.SetComponentData(mine, mineState);
    }

    public static bool CreateLevel(EntityManager em, Entity mine, int level)
    {
        if (em == null || mine == Entity.Null || !em.Exists(mine) || !em.HasBuffer<DepthLevel>(mine) || level <= 0)
            return false;

        var resources = QuerryDB.QueryDefinitions<RessourceDefinition>(DataType.Ressource)
            .Where(resource => resource.Quarry)
            .ToList();

        var total = resources.Sum(resource => 100f / Mathf.Pow(Mathf.Max(1, resource.Rarity), RarityExponent));
        if (total <= 0f)
            return false;

        var buffer = em.GetBuffer<DepthLevel>(mine);
        foreach (var resource in resources)
        {
            var percentage = 100f / Mathf.Pow(Mathf.Max(1, resource.Rarity), RarityExponent);
            buffer.Add(new DepthLevel
            {
                Level = level,
                ResourceId = resource.Id,
                Percentage = percentage / total * 100f
            });
        }

        return true;
    }

    public static string GetRandomResourceId(EntityManager em, Entity mine, int level)
    {
        if (em == null || mine == Entity.Null || !em.Exists(mine) || !em.HasBuffer<DepthLevel>(mine) || level <= 0)
            return string.Empty;

        var entries = new List<DepthLevel>();
        var total = 0f;
        foreach (var entry in em.GetBuffer<DepthLevel>(mine))
        {
            if (entry.Level != level || entry.ResourceId.Length == 0)
                continue;

            entries.Add(entry);
            total += entry.Percentage;
        }

        if (entries.Count == 0 || total <= 0f)
            return string.Empty;

        var roll = Random.value * total;
        var resourceId = entries[entries.Count - 1].ResourceId;
        foreach (var entry in entries)
        {
            roll -= entry.Percentage;
            if (roll <= 0f)
            {
                resourceId = entry.ResourceId;
                break;
            }
        }

        return resourceId.ToString();
    }

    public static bool StartExcavation(EntityManager em, Entity mine)
    {
        if (em == null || mine == Entity.Null || !em.Exists(mine) || !em.HasComponent<ID>(mine) ||
            em.GetComponentData<ID>(mine).Id != MineId || !em.HasComponent<MineState>(mine) ||
            !em.HasBuffer<DepthLevel>(mine))
            return false;

        if (em.HasComponent<StateBuilding>(mine) &&
            em.GetComponentData<StateBuilding>(mine).State != StateBuild.Available)
            return false;

        var definition = QuerryDB.QueryDefinitions<BuildingDefinition>(DataType.Building, MineId).FirstOrDefault();
        if (definition == null || definition.MineLevels.Count == 0)
            return false;

        var mineState = em.GetComponentData<MineState>(mine);
        var level = mineState.DeepestLevel + 1;
        if (level > definition.MineLevels.Max(depth => depth.Level))
            return false;

        if (em.HasComponent<TaskSend>(mine))
        {
            var task = em.GetComponentData<TaskSend>(mine).Task;
            if (task != Entity.Null && em.Exists(task))
            {
                var taskData = em.GetComponentData<Task>(task);
                if (taskData.ActualAction != Entity.Null && em.Exists(taskData.ActualAction))
                    ActionManager.Finish(em, taskData.ActualAction);

                TaskManager.Finish(em, task);
            }
        }

        ProcedureManager.Destroy(em, mine);

        mineState.LevelToMine = level;
        em.SetComponentData(mine, mineState);

        ProcedureManager.AddStep(em, mine, new ProcuredCreationInfo
        {
            PtsWorkNeed = WorkPerLevel * level,
            RequiresWorker = true,
            RequireActivation = false,
            ActionId = ExcavationActionId,
            ItemOut = new List<Items>(),
            ItemIn = new List<Items>(),
            AttributesId = definition.Attributes
        }, true);

        return em.HasBuffer<ProcedureStep>(mine) && em.GetBuffer<ProcedureStep>(mine).Length > 0;
    }

    public static void FinishExcavation(EntityManager em, Entity mine)
    {
        if (em == null || mine == Entity.Null || !em.Exists(mine) || !em.HasComponent<ID>(mine) ||
            em.GetComponentData<ID>(mine).Id != MineId || !em.HasComponent<MineState>(mine) ||
            !em.HasBuffer<DepthLevel>(mine))
            return;

        if (em.HasComponent<StateBuilding>(mine) &&
            em.GetComponentData<StateBuilding>(mine).State != StateBuild.Available)
            return;

        var mineState = em.GetComponentData<MineState>(mine);
        var level = mineState.DeepestLevel + 1;
        if (!CreateLevel(em, mine, level))
            return;

        mineState.DeepestLevel = level;
        mineState.LevelToMine = 0;
        em.SetComponentData(mine, mineState);

        ProcedureManager.Destroy(em, mine);
    }

    public static bool SelectionLevel(EntityManager em, Entity mine, int level)
    {
        if (em == null || mine == Entity.Null || !em.Exists(mine) || !em.HasComponent<ID>(mine) ||
            em.GetComponentData<ID>(mine).Id != MineId || !em.HasComponent<MineState>(mine) ||
            !em.HasBuffer<DepthLevel>(mine) || level <= 0)
            return false;

        if (em.HasComponent<StateBuilding>(mine) &&
            em.GetComponentData<StateBuilding>(mine).State != StateBuild.Available)
            return false;

        var unlocked = false;
        foreach (var entry in em.GetBuffer<DepthLevel>(mine))
        {
            if (entry.Level == level && entry.ResourceId.Length > 0)
            {
                unlocked = true;
                break;
            }
        }

        if (!unlocked)
            return false;

        var definition = QuerryDB.QueryDefinitions<BuildingDefinition>(DataType.Building, MineId).FirstOrDefault();
        if (definition == null)
            return false;

        if (em.HasComponent<TaskSend>(mine))
        {
            var task = em.GetComponentData<TaskSend>(mine).Task;
            if (task != Entity.Null && em.Exists(task))
            {
                var taskData = em.GetComponentData<Task>(task);
                if (taskData.ActualAction != Entity.Null && em.Exists(taskData.ActualAction))
                    ActionManager.Finish(em, taskData.ActualAction);

                TaskManager.Finish(em, task);
            }
        }

        ProcedureManager.Destroy(em, mine);

        var mineState = em.GetComponentData<MineState>(mine);
        mineState.LevelToMine = level;
        em.SetComponentData(mine, mineState);

        ProcedureManager.AddStep(em, mine, new ProcuredCreationInfo
        {
            PtsWorkNeed = WorkPerLevel * level,
            RequiresWorker = true,
            RequireActivation = false,
            ActionId = MineActionId,
            ItemOut = new List<Items>(),
            ItemIn = new List<Items>(),
            AttributesId = definition.Attributes
        }, true);

        return em.HasBuffer<ProcedureStep>(mine) && em.GetBuffer<ProcedureStep>(mine).Length > 0;
    }

    public static void FinishMine(EntityManager em, Entity mine)
    {
        if (em == null || mine == Entity.Null || !em.Exists(mine) || !em.HasComponent<ID>(mine) ||
            em.GetComponentData<ID>(mine).Id != MineId || !em.HasComponent<MineState>(mine) ||
            !em.HasBuffer<DepthLevel>(mine) || !em.HasComponent<WorkZoneLink>(mine))
            return;

        if (em.HasComponent<StateBuilding>(mine) &&
            em.GetComponentData<StateBuilding>(mine).State != StateBuild.Available)
            return;

        var mineState = em.GetComponentData<MineState>(mine);
        var level = mineState.LevelToMine;
        if (level <= 0)
            return;

        var resourceId = GetRandomResourceId(em, mine, level);
        if (string.IsNullOrEmpty(resourceId))
            return;

        var zone = em.GetComponentData<WorkZoneLink>(mine).WorkZone;
        if (zone == Entity.Null || !em.Exists(zone) || !ActionRegistry.TrySpawnResourceInZone(em, zone, resourceId))
            return;

        mineState.LastResourceId = resourceId;
        em.SetComponentData(mine, mineState);
    }
}

public struct MineState : IComponentData
{
    public int DeepestLevel;
    public int LevelToMine;
    public FixedString64Bytes LastResourceId;
}

public struct DepthLevel : IBufferElementData
{
    public int Level;
    public FixedString32Bytes ResourceId;
    public float Percentage;
}
