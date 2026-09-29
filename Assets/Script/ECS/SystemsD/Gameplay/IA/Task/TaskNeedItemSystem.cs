using System.Collections.Generic;
using System.Linq;
using Unity.Entities;
using UnityEngine;

/// <summary>
/// Exécute les tâches TaskNeedItem : un pawn du camp va chercher les items manquants
/// demandés par le buffer ItemNeed d'un stock de zone et les dépose dans ce stock.
/// La tâche se termine uniquement quand le besoin est satisfait (rien ne manque)
/// ou quand plus aucun item n'est disponible dans le stock global (available = 0).
/// </summary>
public partial struct TaskNeedItemSystem : ISystem
{
    public void OnUpdate(ref SystemState state)
    {
        var em = state.EntityManager;
        var tasks = QuerryDB.QueryInstances<Task>(em, DataType.Task);

        foreach (var taskEntity in tasks)
        {
            if (!em.HasComponent<ID>(taskEntity) ||
                em.GetComponentData<ID>(taskEntity).Id != "TaskNeedItem")
                continue;

            var taskData = em.GetComponentData<Task>(taskEntity);
            var targetStock = taskData.BuildingTodo != Entity.Null ? taskData.BuildingTodo : taskData.Object;
            if (targetStock == Entity.Null || !em.Exists(targetStock) || !em.HasBuffer<ItemNeed>(targetStock))
            {
                TaskManager.Finish(em, taskEntity);
                continue;
            }

            var pawn = taskData.Pawn;
            if (pawn == Entity.Null)
            {
                taskData.CanBeDone = true;
                em.SetComponentData(taskEntity, taskData);
                continue;
            }

            if (taskData.ActualAction != Entity.Null)
            {
                continue;
            }

            var buildingInventory = em.GetComponentData<InventoryLink>(taskData.Object).Inventory;
            var pawnInventory = em.HasComponent<InventoryLink>(pawn)
                ? em.GetComponentData<InventoryLink>(pawn).Inventory
                : Entity.Null;

            // FIN : le stock cible ne manque plus de rien -> la tâche est remplie.
            var need = InventoryManager.GetMissingItem(em, buildingInventory);
            if (need.Count == 0)
            {
                TaskManager.Finish(em, taskEntity);
                continue;
            }

            // FIN : plus aucun item utilisable dans le stock global pour couvrir le
            // besoin -> la tâche ne peut plus progresser pour l'instant. On la garde en
            // attente : on retire le pawn (s'il était assigné) et CanBeDone passe à false.
            var available = StockInventoryStat.GetUsableQuantity(em, need.Select(kvp => new Items(kvp.Key, kvp.Value)).ToList())
                .ToDictionary(i => i.ItemId.ToString(), i => i.Quantity);
            var pawnAvailable = pawnInventory != Entity.Null
                ? InventoryManager.GetItems(em,pawnInventory,need)
                : new Dictionary<string, int>();
            if (available.Count <= 0 && pawnAvailable.Count <= 0 )
            {
                TaskManager.Release(em, taskEntity);
                var taskDataEnAttente = em.GetComponentData<Task>(taskEntity);
                taskDataEnAttente.CanBeDone = false;
                em.SetComponentData(taskEntity, taskDataEnAttente);
                continue;
            }

            // Le pawn va chercher les items manquants dans un stock qui en a de surplus.
            if (TaskManager.DoGetItemNeed(em, taskEntity, available))   continue;
            TaskManager.DoTransfereItem(em, taskEntity, pawn, buildingInventory, true, pawnAvailable );
        }
    }

    private static int GetTotalQuantity(List<Items> items)
    {
        if (items == null) return 0;

        var total = 0;
        foreach (var item in items)
            total += item.Quantity;
        return total;
    }

    private static List<Items> GetPawnItemsMatchingItemNeed(EntityManager em, Entity pawnInventory, Entity targetStock)
    {
        var result = new List<Items>();
        if (pawnInventory == Entity.Null || !em.Exists(pawnInventory) || !em.HasBuffer<Items>(pawnInventory) ||
            targetStock == Entity.Null || !em.Exists(targetStock) || !em.HasBuffer<ItemNeed>(targetStock))
            return result;

        var needs = em.GetBuffer<ItemNeed>(targetStock);
        for (int i = 0; i < needs.Length; i++)
        {
            var need = needs[i];
            if (need.Item.ItemId.Length == 0)
                continue;

            var index = InventoryManager.GetItemIndex(em, pawnInventory, need.Item.ItemId.ToString());
            var quantity = index != -1 ? em.GetBuffer<Items>(pawnInventory)[index].Quantity : 0;
            if (quantity > 0)
                result.Add(new Items(need.Item.ItemId, quantity));
        }
        return result;
    }

    private static string ToLog(List<Items> items)
    {
        if (items == null || items.Count == 0)
            return "vide";

        var values = new List<string>();
        foreach (var item in items)
            values.Add($"{item.ItemId}={item.Quantity}");
        return string.Join(", ", values);
    }
}