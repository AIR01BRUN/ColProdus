using System;
using System.Collections.Generic;
using Unity.Entities;

/// <summary>
/// Point central des besoins liés aux inventaires. Pour l'instant, il crée la
/// tâche de vidage lorsqu'un inventaire de pawn contient plus d'un item, et la
/// tâche TaskNeedItem lorsqu'un stock de zone manque d'items par rapport à son buffer ItemNeed.
/// </summary>
public partial struct InventorySystem : ISystem
{
    public void OnUpdate(ref SystemState state)
    {
        var em = state.EntityManager;
        var inventories = QuerryDB.QueryInstances<Inventory>(em, DataType.Inventory);

        foreach (var inventory in inventories)
        {
            if (!em.HasComponent<InventoryOwnerLink>(inventory))
                continue;

            if (IsStockInventory(em, inventory))
            {
                if(!CreateStockNeedTask(em, inventory)) continue;
            }

            var pawn = em.GetComponentData<InventoryOwnerLink>(inventory).Owner;
            if (!IsPawn(em, pawn) || !em.HasComponent<TaskSend>(pawn) || GetItemCount(em, inventory) < 1)
                continue;

            var taskSend = em.GetComponentData<TaskSend>(pawn);
            if (taskSend.Task != Entity.Null && em.Exists(taskSend.Task))
                continue;

            // Un besoin urgent doit garder le slot TaskSend : on ne crée pas de tâche de vidage.
            if (HasUrgentNeed(em, pawn))
                continue;

            var task = TaskManager.Create(em, "EmptyInventoryTask", pawn, requiresPawn: true);
            var taskData = em.GetComponentData<Task>(task);
            taskData.Priority = 4;
            // Pawn reste nul : il sera renseigné seulement quand un pawn prendra la tâche.
            em.SetComponentData(task, taskData);

            taskSend.Task = task;
            em.SetComponentData(pawn, taskSend);
        }
    }

    /// <summary>
    /// Pour un inventaire de stock : si les items demandés par son buffer ItemNeed manquent,
    /// le bâtiment qui possède cet inventaire stocke une tâche TaskNeedItem (priorité 4)
    /// dans son TaskSend (récupérable par les pawns du camp).
    /// </summary>
    private static bool CreateStockNeedTask(EntityManager em, Entity inventory)
    {
        
        var ownerBuilding = em.GetComponentData<InventoryOwnerLink>(inventory).Owner;
        if (ownerBuilding != Entity.Null && em.HasComponent<BuildingRubble>(ownerBuilding))
            return false;
        if (!em.HasComponent<TaskSend>(ownerBuilding)) em.AddComponentData(ownerBuilding, new TaskSend());
        var ownerTaskSend = em.GetComponentData<TaskSend>(ownerBuilding);
        if (ownerTaskSend.Task != Entity.Null && em.Exists(ownerTaskSend.Task)) return false;

        if (!em.HasBuffer<ItemNeed>(inventory))
            return false;

        // La tâche est envoyée dès qu'il manque des items dans l'inventaire par rapport à
        // son buffer ItemNeed (item absent ou quantité insuffisante). On ne vérifie plus si
        // le stock global "a assez" : c'est TaskNeedItemSystem qui gère la disponibilité.
        var missing = InventoryManager.GetMissingItem(em, inventory);
        if (missing.Count == 0)
            return false;

        var task = TaskManager.Create(em, "TaskNeedItem", ownerBuilding, inventory, requiresPawn: true);
        var taskData = em.GetComponentData<Task>(task);
        taskData.Priority = 4;
        // Pawn reste nul : il sera renseigné seulement quand un pawn prendra la tâche.
        em.SetComponentData(task, taskData);

        ownerTaskSend.Task = task;
        em.SetComponentData(ownerBuilding, ownerTaskSend);
        return  true;
    }

    private static bool IsStockInventory(EntityManager em, Entity inventory)
    {
        return em.HasComponent<ID>(inventory) &&
               em.GetComponentData<ID>(inventory).Id == InventoryType.StockInventory.ToString();
    }

    private static bool IsPawn(EntityManager em, Entity entity)
    {
        return entity != Entity.Null && em.Exists(entity) && em.HasComponent<ID>(entity) &&
               em.GetComponentData<ID>(entity).DataType == DataType.Pawn;
    }

    private static bool HasUrgentNeed(EntityManager em, Entity pawn)
    {
        if (pawn == Entity.Null || !em.Exists(pawn) || !em.HasBuffer<PawnNeed>(pawn))
            return false;

        foreach (var need in em.GetBuffer<PawnNeed>(pawn))
            if (need.GetPercentage() < 0.5f)
                return true;
        return false;
    }

    private static int GetItemCount(EntityManager em, Entity inventory)
    {
        var count = 0;
        foreach (var slot in em.GetBuffer<Items>(inventory))
        {
            // La monnaie n'est jamais vidée dans un stock, elle ne compte donc pas.
            if (slot.Quantity > 0 && !ItemManager.IsCurrency(slot.ItemId.ToString()))
                count += slot.Quantity;
        }
        return count;
    }
}
