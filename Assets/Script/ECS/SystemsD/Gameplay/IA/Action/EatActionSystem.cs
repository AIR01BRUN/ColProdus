using Unity.Collections;
using Unity.Entities;
using UnityEngine;

/// <summary>Action "Eat" : consomme des items nourriture jusqu'à atteindre 90% de faim.</summary>
public partial struct EatActionSystem : ISystem
{
    private const float EatDuration = 2f;
    private const float EatTargetPercentage = 0.90f;

    private static int GetItemQuantity(EntityManager em, Entity inventory, string itemId)
    {
        var index = InventoryManager.GetItemIndex(em, inventory, itemId);
        return index != -1 ? em.GetBuffer<Items>(inventory)[index].Quantity : 0;
    }

    public void OnUpdate(ref SystemState state)
    {
        var em = state.EntityManager;
        var actions = QuerryDB.QueryInstances<Eat>(em, DataType.Action);

        foreach (var actionEntity in actions)
        {
            var action = em.GetComponentData<Eat>(actionEntity);
            action.Elapsed += Time.deltaTime;
            if (action.Elapsed < EatDuration)
            {
                em.SetComponentData(actionEntity, action);
                continue;
            }

            var link = em.GetComponentData<ActionLink>(actionEntity);
            var pawn = link.Pawn;
            if (pawn != Entity.Null && em.Exists(pawn) && em.HasComponent<InventoryLink>(pawn))
            {
                var inventory = em.GetComponentData<InventoryLink>(pawn).Inventory;
                var itemId = action.Item.ItemId.ToString();
                if (GetItemQuantity(em, inventory, itemId) > 0)
                {
                    InventoryManager.RemoveItem(em, inventory, action.Item);
                    PawnNeedManager.RestoreNeed(em, pawn, PawnNeedManager.Faim, ItemManager.GetItemNutrition(itemId));

                    // Tant que la faim est en dessous de 90% et qu'il reste de la nourriture, on continue de manger.
                    if (PawnNeedManager.GetPercentage(em, pawn, new FixedString32Bytes(PawnNeedManager.Faim)) < EatTargetPercentage &&
                        GetItemQuantity(em, inventory, itemId) > 0)
                    {
                        action.Elapsed = 0f;
                        em.SetComponentData(actionEntity, action);
                        continue;
                    }
                }
            }

            ActionManager.Finish(em, actionEntity);
        }
    }
}

public struct Eat : IComponentData
{
    public float Elapsed;
    public Items Item;
}