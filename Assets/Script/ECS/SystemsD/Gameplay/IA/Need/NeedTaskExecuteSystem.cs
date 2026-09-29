using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;

/// <summary>Exécute les tâches de besoin des pawns (manger / dormir).</summary>
public partial struct NeedTaskExecuteSystem : ISystem
{
    public void OnUpdate(ref SystemState state)
    {
        var em = state.EntityManager;
        var tasks = QuerryDB.QueryInstances<Task>(em, DataType.Task);

        foreach (var taskEntity in tasks)
        {
            if (!em.HasComponent<ID>(taskEntity) ||
                em.GetComponentData<ID>(taskEntity).Id.ToString() != "PawnNeedTask")
                continue;

            if (!em.HasComponent<NeedTaskRef>(taskEntity) || HasNoNeeds(em, taskEntity))
            {
                TaskManager.Finish(em, taskEntity);
                continue;
            }

            var taskData = em.GetComponentData<Task>(taskEntity);
            taskData.CanBeDone = true;
            em.SetComponentData(taskEntity, taskData);

            var pawn = taskData.Pawn;
            if (pawn == Entity.Null)
                continue;

            if (!em.HasComponent<CurrentTask>(pawn) ||
                em.GetComponentData<CurrentTask>(pawn).Task != taskEntity)
                continue;

            if (taskData.ActualAction != Entity.Null)
                continue;

            var needRef = em.GetComponentData<NeedTaskRef>(taskEntity);
            var needId = needRef.NeedId.ToString();
            if (needId == PawnNeedManager.Faim)
                ExecuteEat(em, taskEntity, pawn);
            else if (needId == PawnNeedManager.Sommeil)
                ExecuteSleep(em, taskEntity, pawn);
        }
    }

    private static bool HasNoNeeds(EntityManager em, Entity taskEntity)
    {
        if (!em.HasComponent<NeedTaskRef>(taskEntity))
            return true;

        var pawn = em.GetComponentData<NeedTaskRef>(taskEntity).Pawn;
        return pawn == Entity.Null || !em.Exists(pawn) || !em.HasBuffer<PawnNeed>(pawn);
    }

    private static void ExecuteEat(EntityManager em, Entity task, Entity pawn)
    {
        var foodId = ItemManager.GetFirstFoodItemId();
        if (string.IsNullOrEmpty(foodId) || !em.HasComponent<InventoryLink>(pawn))
            return;

        var pawnInventory = em.GetComponentData<InventoryLink>(pawn).Inventory;
        var index = InventoryManager.GetItemIndex(em, pawnInventory, foodId);
        if (index == -1 || em.GetBuffer<Items>(pawnInventory)[index].Quantity <= 0)
        {
            if (!InventoryManager.CanAddItem(em, pawnInventory, foodId))
                return;

            var itemNeed = new Dictionary<string, int> { { foodId, 1 } };
            TaskManager.DoGetItemNeed(em, task, itemNeed);
            return;
        }

        var action = ActionManager.Create(em, "Eat", task, pawn);
        em.AddComponentData(action, new Eat { Elapsed = 0f, Item = new Items(foodId, 1) });
        SetActionOnPawn(em, task, pawn, action);
    }

    private static void ExecuteSleep(EntityManager em, Entity task, Entity pawn)
    {
        if (!TryGetCheckroom(em, pawn, out var checkroom) || checkroom == Entity.Null ||
            !em.Exists(checkroom) || !em.HasComponent<OnBoard>(checkroom))
            return;

        if (TaskManager.DoMovePawn(em, task, checkroom))
            return;

        var action = ActionManager.Create(em, "Sleep", task, pawn);
        em.AddComponentData(action, new Sleep { Elapsed = 0f });
        SetActionOnPawn(em, task, pawn, action);
    }

    private static void SetActionOnPawn(EntityManager em, Entity task, Entity pawn, Entity action)
    {
        var taskData = em.GetComponentData<Task>(task);
        taskData.ActualAction = action;
        em.SetComponentData(task, taskData);

        var currentAction = em.GetComponentData<CurrentAction>(pawn);
        currentAction.Action = action;
        em.SetComponentData(pawn, currentAction);
    }

    private static bool TryGetCheckroom(EntityManager em, Entity pawn, out Entity checkroom)
    {
        checkroom = Entity.Null;
        if (em.HasComponent<WorkIn>(pawn))
        {
            var building = em.GetComponentData<WorkIn>(pawn).Building;
            if (building != Entity.Null && em.Exists(building) && em.HasComponent<WorkZoneLink>(building) &&
                WorkZoneManager.TryGetBuildingWorker(em, em.GetComponentData<WorkZoneLink>(building).WorkZone, out checkroom) &&
                checkroom != Entity.Null)
                return true;
        }

        if (WorkZoneManager.TryGetCamp(out var camp) &&
            WorkZoneManager.TryGetBuildingWorker(em, camp, out checkroom))
            return true;

        return false;
    }
}