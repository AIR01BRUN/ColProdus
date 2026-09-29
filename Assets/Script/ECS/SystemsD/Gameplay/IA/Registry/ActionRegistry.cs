using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Entities;
using Unity.Mathematics;

public delegate void ActionHandler(EntityManager em, Entity targetEntity);

public static class ActionRegistry
{
    private static readonly Dictionary<string, ActionHandler> Actions = new Dictionary<string, ActionHandler>(StringComparer.OrdinalIgnoreCase)
    {
        ["Destroy"] = Destroy,
        ["RemoveFromBoard"] = RemoveFromBoard,
        ["FinishConstruction"] =  BuildingManager.FinishConstruction,
        ["SpawnResourceInZone"] = SpawnResourceInZone,
        ["FinishExcavation"] = MineManager.FinishExcavation,
        ["FinishMine"] = MineManager.FinishMine,
        ["MarketSell"] = MarketManager.MarketSell,
        ["MarketBuy"] = MarketManager.MarketBuy,
        ["MarketUrgentSell"] = MarketManager.MarketUrgentSell,
        ["MarketUrgentBuy"] = MarketManager.MarketUrgentBuy,
        ["GetSalary"] = SalaryManager.GetSalary
    };

    public static bool TryExecute(EntityManager em, string actionId, Entity targetEntity)
    {
        if (string.IsNullOrEmpty(actionId))
            return false;

        if (Actions.TryGetValue(actionId, out var action))
        {
            action(em, targetEntity);
            return true;
        }

        return false;
    }

    public static void Destroy(EntityManager em, Entity targetEntity)
    {
        if (em == null || targetEntity == Entity.Null || !em.Exists(targetEntity))
            return;

        RemoveFromBoard(em, targetEntity);
        ProcedureManager.Destroy(em, targetEntity);

        if (em.HasComponent<InventoryLink>(targetEntity))
        {
            var inventory = em.GetComponentData<InventoryLink>(targetEntity).Inventory;
            InventoryManager.Destroy(em, inventory);
            em.RemoveComponent<InventoryLink>(targetEntity);
        }

        if (em.HasComponent<TaskSend>(targetEntity))
        {
            var task = em.GetComponentData<TaskSend>(targetEntity).Task;
            if (task != Entity.Null && em.Exists(task))
                TaskManager.Finish(em, task);
        }

        EntityGraphicsUtility.DestroyGraphicFromParent(em, targetEntity);
        Database.DeleteInstance(em, targetEntity);
        em.DestroyEntity(targetEntity);
    }

    public static void RemoveFromBoard(EntityManager em, Entity targetEntity)
    {
        if (targetEntity == Entity.Null || !em.Exists(targetEntity))
            return;

        if (!em.HasComponent<OnBoard>(targetEntity))
            return;

        var onBoard = em.GetComponentData<OnBoard>(targetEntity);
        if (onBoard.Board == Entity.Null || !em.Exists(onBoard.Board) || !em.HasBuffer<Cell>(onBoard.Board))
            return;

        var cells = em.GetBuffer<Cell>(onBoard.Board);
        for (int index = 0; index < cells.Length; index++)
        {
            var cell = cells[index];
            if (cell.ObjectOn == targetEntity)
            {
                cell.ObjectOn = Entity.Null;
                cells[index] = cell;
            }
        }
    }

    /// <summary>
    /// Fait apparaître une ressource (par défaut "tree") à une position libre dans la
    /// zone. Cible possible : la zone elle-même (WorkZone) ou un building possédant un
    /// WorkZoneLink (la ressource apparaît alors dans la zone du building). La ressource
    /// est placée sur le plateau avec sa ProcedureState.Activation passée à true.
    /// </summary>
    public static void SpawnResourceInZone(EntityManager em, Entity target)
    {
        var resourceId = "tree";
        if (target != Entity.Null && em.Exists(target) && em.HasComponent<SpawnResourceSelection>(target))
            resourceId = em.GetComponentData<SpawnResourceSelection>(target).ResourceId.ToString();

        TrySpawnResourceInZone(em, target, resourceId);
    }

    public static bool TrySpawnResourceInZone(EntityManager em, Entity target, string resourceId)
    {
        if (em == null || target == Entity.Null || !em.Exists(target) || !em.HasComponent<ID>(target) ||
            string.IsNullOrWhiteSpace(resourceId))
            return false;

        var targetId = em.GetComponentData<ID>(target);
        Entity zone = target;
        if (targetId.DataType == DataType.Building)
        {
            if (!em.HasComponent<WorkZoneLink>(target))
                return false;
            zone = em.GetComponentData<WorkZoneLink>(target).WorkZone;
        }
        else if (targetId.DataType != DataType.WorkZone)
        {
            return false;
        }

        if (zone == Entity.Null || !em.Exists(zone) || !em.HasComponent<OnBoard>(zone))
            return false;

        resourceId = resourceId.Trim().ToLowerInvariant();
        var definition = QuerryDB.QueryDefinitions<RessourceDefinition>(DataType.Ressource, resourceId).FirstOrDefault();
        if (definition == null)
            return false;

        var onBoard = em.GetComponentData<OnBoard>(zone);
        if (onBoard.Board == Entity.Null || !em.Exists(onBoard.Board))
            return false;

        var cells = em.GetBuffer<Cell>(onBoard.Board);
        var boardSize = em.GetComponentData<BoardSize>(onBoard.Board).GridSize;
        int minX = boardSize.x, maxX = -1, minY = boardSize.y, maxY = -1;
        for (int index = 0; index < cells.Length; index++)
        {
            if (cells[index].WorkZone != zone)
                continue;
            int x = index % boardSize.x;
            int y = index / boardSize.x;
            if (x < minX) minX = x;
            if (x > maxX) maxX = x;
            if (y < minY) minY = y;
            if (y > maxY) maxY = y;
        }

        if (maxX < minX || maxY < minY)
            return false;

        var center = new int2((minX + maxX) / 2, (minY + maxY) / 2);
        int2? chosen = null;
        int bestDistance = int.MaxValue;
        int found = 0;
        for (int y = minY; y <= maxY; y++)
        {
            for (int x = minX; x <= maxX; x++)
            {
                var candidate = new int2(x, y);
                var candidateCell = cells[y * boardSize.x + x];
                if (candidateCell.WorkZone != zone || candidateCell.ObjectOn != Entity.Null)
                    continue;

                int distance = (x - center.x) * (x - center.x) + (y - center.y) * (y - center.y);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    chosen = candidate;
                    found = 1;
                }
                else if (distance == bestDistance)
                {
                    found++;
                    if (UnityEngine.Random.Range(0, found) == 0)
                        chosen = candidate;
                }
            }
        }

        if (chosen == null)
            return false;

        var ressource = RessourceManager.Create(em, resourceId);
        if (ressource == Entity.Null)
            return false;

        if (em.HasComponent<WorkZoneLink>(ressource))
            em.SetComponentData(ressource, new WorkZoneLink { WorkZone = zone });
        else
            em.AddComponentData(ressource, new WorkZoneLink { WorkZone = zone });

        BoardManager.AddOn(em, ressource, onBoard.Board, chosen.Value, 0, true);
        if (em.HasComponent<ProcedureState>(ressource))
        {
            var state = em.GetComponentData<ProcedureState>(ressource);
            state.Activation = true;
            em.SetComponentData(ressource, state);
        }

        return true;
    }

    
}