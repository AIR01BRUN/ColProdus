using Unity.Collections;
using Unity.Entities;
using UnityEngine;

public partial struct TransferItemSystem : ISystem
{
    private const float TransferDuration = 2f;

    public void OnUpdate(ref SystemState state)
    {
        var em = state.EntityManager;
        var actions = QuerryDB.QueryInstances<TransferTo>(em, DataType.Action);

        foreach (var actionEntity in actions)
        {
            var action = em.GetComponentData<TransferTo>(actionEntity);
            var items = em.GetBuffer<TransferItem>(actionEntity);
            action.Elapsed += GetTransferSpeed(em, actionEntity) * Time.deltaTime;
            em.SetComponentData(actionEntity, action);
            if (action.Elapsed < TransferDuration) continue;

            foreach (var item in items)
            {
                InventoryManager.RemoveItem(em, action.Source, item.Item);
                InventoryManager.AddItem(em, action.Destination, item.Item);
            }

            // Le building n'est plus occupé : on retire le marqueur TaskBusy.
            if (em.Exists(action.Source) && em.HasComponent<TaskBusy>(action.Source))
                em.RemoveComponent<TaskBusy>(action.Source);
            if (em.Exists(action.Destination) && em.HasComponent<TaskBusy>(action.Destination))
                em.RemoveComponent<TaskBusy>(action.Destination);

            // Le pawn a terminé sa récupération : on retire le marqueur Busy des buildings propriétaires.
            var pawn = em.HasComponent<ActionLink>(actionEntity)
                ? em.GetComponentData<ActionLink>(actionEntity).Pawn
                : Entity.Null;
            RemoveBusyIfOwner(em, action.Source, pawn);
            RemoveBusyIfOwner(em, action.Destination, pawn);

            ActionManager.Finish(em, actionEntity);
        }
    }

    private static void RemoveBusyIfOwner(EntityManager em, Entity inventory, Entity pawn)
    {
        if (!em.Exists(inventory) || !em.HasComponent<InventoryOwnerLink>(inventory))
            return;

        var building = em.GetComponentData<InventoryOwnerLink>(inventory).Owner;
        if (building == Entity.Null || !em.Exists(building) || !em.HasComponent<Busy>(building))
            return;

        var busy = em.GetComponentData<Busy>(building);
        if (busy.Pawn == pawn || busy.Pawn == Entity.Null || pawn == Entity.Null)
            em.RemoveComponent<Busy>(building);
    }

    private static float GetTransferSpeed(EntityManager em, Entity actionEntity)
    {
        if (!em.HasComponent<ActionLink>(actionEntity))
            return 1f;

        var pawn = em.GetComponentData<ActionLink>(actionEntity).Pawn;
        if (pawn == Entity.Null || !em.Exists(pawn))
            return 1f;
        if (!em.HasComponent<PawnAttributes>(pawn) || !em.HasBuffer<PawnAttributeEntry>(pawn))
            return 1f;

        var attributes = em.GetBuffer<PawnAttributeEntry>(pawn);
        var speed = 0f;

        foreach (var attributeId in new[] { "DEX", "AGI" })
        {
            var fs32 = new FixedString32Bytes(attributeId);
            var definition = AttributeDatabase.Get(fs32);
            if (definition != null)
                speed += definition.Base + PawnAttributeGenerator.GetValue(attributes, fs32) * definition.PerLevel;
        }

        if (speed <= 0f)
            speed = 1f;

        speed *= AttributeManager.GetActual(em, pawn, AttributeManager.WorkPerformance);

        return speed;
    }
}

public struct TransferTo : IComponentData
{
    public Entity Source;
    public Entity Destination;

    public float Elapsed;
}
public struct TransferItem : IBufferElementData
{
    public Items Item;
}