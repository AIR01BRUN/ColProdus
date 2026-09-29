using System.Collections.Generic;
using System.Linq;
using Unity.Entities;

/// <summary>
/// Exécute les tâches "GetEnergy" : un pawn va chercher les items porteurs du même type
/// d'énergie que le bâtiment cible et les lui apporte pour recharger sa réserve.
/// Avant toute chose, comme TaskProcedureSystem, on vérifie que les items sont
/// disponibles : stock global, inventaires de la zone du bâtiment et inventaire du pawn.
/// Le calcul demande le minimum d'items pour remplir la réserve au maximum, plafonné au
/// stock réellement disponible (si le stock est insuffisant, le pawn prend tout).
/// La tâche n'est donc proposed (CanBeDone) que si la zone ou le pawn peuvent fournir
/// les items. Les items déposés sont convertis en énergie à chaque update, puis la tâche
/// se termine une fois la réserve pleine.
/// </summary>
public partial struct TaskGetEnergySystem : ISystem
{
    public void OnCreate(ref SystemState state)
    {
    }

    public void OnUpdate(ref SystemState state)
    {
        var em = state.EntityManager;
        var tasks = QuerryDB.QueryInstances<Task>(em, DataType.Task);

        foreach (var taskEntity in tasks)
        {
            if (!em.HasComponent<ID>(taskEntity) || em.GetComponentData<ID>(taskEntity).Id != "GetEnergy")
                continue;

            var taskData = em.GetComponentData<Task>(taskEntity);
            var building = taskData.Object;
            if (building == Entity.Null || !em.Exists(building) || !em.HasComponent<Energy>(building))
            {
                TaskManager.Finish(em, taskEntity);
                continue;
            }

            var inventory = EnergyManager.GetInventory(em, building);
            if (inventory == Entity.Null)
            {
                TaskManager.Finish(em, taskEntity);
                continue;
            }

            EnergyManager.AbsorbEnergy(em, building, inventory);

            var energy = em.GetComponentData<Energy>(building);
            if (energy.EnergyActual >= energy.EnergyMax)
            {
                TaskManager.Finish(em, taskEntity);
                continue;
            }

            var pawn = taskData.Pawn;
            if (taskData.ActualAction != Entity.Null)
                continue;

            // Items d'énergie manquants pour remplir la réserve au maximum.
            var request = EnergyManager.GetEnergyItemRequest(em, energy.EnergyId, energy.EnergyMax - energy.EnergyActual);
            if (request.Count == 0)
            {
                TaskManager.Release(em, taskEntity);
                SetWaiting(em, taskEntity);
                continue;
            }

            // Les items que la zone du bâtiment (stock + inventaires de zone) et le pawn
            // possèdent réellement.
            var itemOwned = new Dictionary<string, int>();
            if (em.HasComponent<WorkZoneLink>(building))
            {
                var gateWorkZone = em.GetComponentData<WorkZoneLink>(building).WorkZone;
                if (gateWorkZone != Entity.Null && em.Exists(gateWorkZone))
                {
                    var inventoryZones = WorkZoneManager.GetBuildingWithInventory(em, gateWorkZone);
                    itemOwned = InventoryManager.GetAllStockAvailableItems(em, request.Keys.ToList(), inventoryZones);
                    foreach (var inventoryZoneB in inventoryZones)
                    {
                        if (!em.HasComponent<InventoryLink>(inventoryZoneB))
                            continue;

                        var zoneInventory = em.GetComponentData<InventoryLink>(inventoryZoneB).Inventory;
                        itemOwned = InventoryManager.Merge(itemOwned,
                            InventoryManager.GetItems(em, zoneInventory, request.Keys.ToList()));
                    }
                }
            }

            if (pawn != Entity.Null && em.HasComponent<InventoryLink>(pawn))
            {
                var pawnInventory = em.GetComponentData<InventoryLink>(pawn).Inventory;
                itemOwned = InventoryManager.Merge(itemOwned,
                    InventoryManager.GetItems(em, pawnInventory, request.Keys.ToList()));
            }

            // On ne demande que ce qui est réellement accessible : si la zone n'a pas
            // assez, le pawn prend ce qu'il y a (le calcul de EnergyManager plafonne déjà
            // sur le stock global disponible).
            foreach (var itemId in request.Keys.ToList())
            {
                var owned = itemOwned.TryGetValue(itemId, out var quantity) ? quantity : 0;
                if (owned <= 0)
                {
                    request.Remove(itemId);
                    continue;
                }

                if (owned < request[itemId])
                    request[itemId] = owned;
            }

            taskData.RequiresPawn = true;
            taskData.CanBeDone = request.Count > 0 && InventoryManager.HasEnougthItem(request, itemOwned);
            em.SetComponentData(taskEntity, taskData);

            if (!taskData.CanBeDone)
            {
                TaskManager.Release(em, taskEntity);
                continue;
            }

            if (pawn == Entity.Null)
                continue;

            if (TaskManager.DoGetItemNeed(em, taskEntity, request)) continue;
            TaskManager.DoTransfereItem(em, taskEntity, pawn, inventory, true, request);
        }
    }

    private static void SetWaiting(EntityManager em, Entity task)
    {
        var waiting = em.GetComponentData<Task>(task);
        waiting.CanBeDone = false;
        em.SetComponentData(task, waiting);
    }
}
