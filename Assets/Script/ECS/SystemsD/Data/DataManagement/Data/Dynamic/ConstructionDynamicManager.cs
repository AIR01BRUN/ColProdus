using Unity.Entities;
using Unity.Collections;
using UnityEngine;

public static class ConstructionDynamicManager
{
    public static void AddOn(Entity entity, float ptsWorkActual, NativeList<int> quantityItems)
    {
        var world = World.DefaultGameObjectInjectionWorld;
        if (world == null || entity == Entity.Null || !world.EntityManager.Exists(entity))
            return;

        var em = world.EntityManager;
        var itemNeeds = em.GetBuffer<ItemRequiedToConstruction>(entity);

        var underConstruction = em.AddBuffer<UnderConstructionItem>(entity);
        underConstruction.Clear();

        for (int i = 0; i < itemNeeds.Length; i++)
        {
            var quantity = quantityItems.Length > i ? quantityItems[i] : 0;
            underConstruction.Add(new UnderConstructionItem
            {
                Item = itemNeeds[i].Item,
                QuantityCurrent = quantity,
                QuantityNeed = itemNeeds[i].Quantity
            });
        }

        em.AddComponentData(entity, new UnderConstruction { PtsWorkActual = ptsWorkActual });
    }

    public static void AddOn(Entity entity)
    {
        AddOn(entity, 0f, new NativeList<int>(Allocator.Temp));
    }
}
public struct UnderConstructionItemRequest : IComponentData
{
    public int QuantityCurrent;
   
}
public struct UnderConstructionItem : IBufferElementData
{
    public Entity Item; 
    public int QuantityCurrent;
    public int QuantityNeed;
}

public struct UnderConstruction :  IComponentData
{
    public float PtsWorkActual;
}
