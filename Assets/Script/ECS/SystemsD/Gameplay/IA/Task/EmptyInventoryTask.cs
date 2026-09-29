using System.Collections.Generic;
using System.Linq;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

/// <summary>Met à jour et exécute les tâches qui vident un inventaire de pawn.</summary>
public partial struct EmptyInventoryTaskSystem : ISystem
{
    public void OnUpdate(ref SystemState state)
    {
        var em = state.EntityManager;
        var tasks = QuerryDB.QueryInstances<Task>(em, DataType.Task);

        foreach (var taskEntity in tasks)
        {
            if (!em.HasComponent<ID>(taskEntity) ||
                em.GetComponentData<ID>(taskEntity).Id != "EmptyInventoryTask")
                continue;

            var taskDataCurrent = em.GetComponentData<Task>(taskEntity);
            var objectTarget = taskDataCurrent.Object;
            if (objectTarget == Entity.Null || !em.Exists(objectTarget) || !em.HasComponent<InventoryLink>(objectTarget))
            {
                TaskManager.Finish(em, taskEntity);
                continue;
            }

            // Object est le pawn dont l'inventaire doit être vidé.
            var objectInventory = em.GetComponentData<InventoryLink>(objectTarget).Inventory;
            var items = GetItemsToSend(em, objectInventory);
            if (items.Count == 0)
            {
                TaskManager.Finish(em, taskEntity);
                continue;
            }

            taskDataCurrent.Priority = GetSendPriority(em, objectInventory);

            if (!TryGetDestination(em, objectTarget, out var building))
            {
                taskDataCurrent.CanBeDone = false;
                taskDataCurrent.BuildingTodo = Entity.Null;
                em.SetComponentData(taskEntity, taskDataCurrent);
                continue;
            }

            // Si le stock de destination est proche (moins de 5 unités), on passe directement à 3.
            if (IsNearInventory(em, objectTarget, building, 5))
                taskDataCurrent.Priority = 3;

            taskDataCurrent.CanBeDone = true;
            taskDataCurrent.BuildingTodo = building;
            em.SetComponentData(taskEntity, taskDataCurrent);

            // Pawn identifie celui qui exécute réellement la tâche : il peut être nul tant
            // qu'aucun pawn ne l'a sélectionnée.
            var pawn = taskDataCurrent.Pawn;
            if (pawn == Entity.Null)
                continue;

            if (em.HasComponent<CurrentTask>(pawn) &&
                em.GetComponentData<CurrentTask>(pawn).Task == taskEntity &&
                taskDataCurrent.ActualAction == Entity.Null)
                    TaskManager.DoTransfereItem(em, taskEntity, pawn, em.GetComponentData<InventoryLink>(building).Inventory, true, items.ToDictionary(i => i.ItemId.ToString(), i => i.Quantity));
        }
    }

    private static bool TryGetDestination(EntityManager em, Entity pawn, out Entity building)
    {
        building = Entity.Null;
        if (em.HasComponent<WorkIn>(pawn))
        {
            var workBuilding = em.GetComponentData<WorkIn>(pawn).Building;
            if (workBuilding != Entity.Null && em.Exists(workBuilding) && em.HasComponent<WorkZoneLink>(workBuilding) &&
                WorkZoneManager.TryGetBuildingWithInventory(em, em.GetComponentData<WorkZoneLink>(workBuilding).WorkZone, out building))
                return true;
        }
        Entity camp;
        return WorkZoneManager.TryGetCamp(out camp) &&
               WorkZoneManager.TryGetBuildingWithInventory(em, camp, out building);
    }

    /// <summary>
    /// Vérifie si le pawn est à moins de maxDistance (Manhattan) du rectangle du bâtiment cible
    /// qui possède l'inventaire de destination.
    /// </summary>
    private static bool IsNearInventory(EntityManager em, Entity pawn, Entity building, int maxDistance)
    {
        if (pawn == Entity.Null || building == Entity.Null || !em.Exists(pawn) || !em.Exists(building) ||
            !em.HasComponent<OnBoard>(pawn) || !em.HasComponent<OnBoard>(building))
            return false;

        var pawnPosition = em.GetComponentData<OnBoard>(pawn).Position;
        var targetPosition = em.GetComponentData<OnBoard>(building).Position;
        var size = em.HasComponent<Size>(building) ? em.GetComponentData<Size>(building).Value : new int2(1, 1);

        var minX = targetPosition.x;
        var minY = targetPosition.y;
        var maxX = minX + size.x - 1;
        var maxY = minY + size.y - 1;

        var distanceX = Unity.Mathematics.math.max(minX - pawnPosition.x, 0) + Unity.Mathematics.math.max(pawnPosition.x - maxX, 0);
        var distanceY = Unity.Mathematics.math.max(minY - pawnPosition.y, 0) + Unity.Mathematics.math.max(pawnPosition.y - maxY, 0);

        return distanceX + distanceY < maxDistance;
    }

    private static List<Items> GetItemsToSend(EntityManager em, Entity inventory)
    {
        var result = new List<Items>();
        if (inventory == Entity.Null || !em.Exists(inventory) || !em.HasBuffer<Items>(inventory))
            return result;

        foreach (var slot in em.GetBuffer<Items>(inventory))
        {
            if (slot.Quantity <= 0)
                continue;

            // La monnaie reste au pawn (salaire), elle n'est jamais vidée dans un stock.
            if (ItemManager.IsCurrency(slot.ItemId.ToString()))
                continue;

            result.Add(new Items(slot.ItemId, slot.Quantity));
        }
        return result;
    }

    // 4 au début, 3 à partir de 50 % et 2 lorsque l'inventaire atteint 90 %.
    private static int GetSendPriority(EntityManager em, Entity inventory)
    {
        var slots = em.GetBuffer<Items>(inventory);
        if (slots.Length == 0) return 4;

        var occupied = 0;
        foreach (var slot in slots)
        {
            if (slot.Quantity > 0 && !ItemManager.IsCurrency(slot.ItemId.ToString()))
                occupied++;
        }

        var fillRatio = (float)occupied / slots.Length;
        return fillRatio >= .9f ? 2 : fillRatio >= .5f ? 3 : 4;
    }
}
