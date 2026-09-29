using System.Collections.Generic;
using System.Linq;
using Unity.Entities;
using Unity.Mathematics;

public static class RessourceManager
{
    public static Entity Create(EntityManager em, string id)
    {
      

        var definition = QuerryDB.QueryDefinitions<RessourceDefinition>(DataType.Ressource, id).FirstOrDefault();
        if (definition == null)
            return Entity.Null;

        var entity = em.CreateEntity();
        var number = Database.GetNextNumber(DataType.Ressource, id);

        em.AddComponentData(entity, new ID
        {
            DataType = DataType.Ressource,
            Id = id,
            NumId = number,
            Name = id
        });

        var size =  definition.Size;
        em.AddComponentData(entity, new Size { Value = size });

     /*   var lootTable = em.AddBuffer<LootTable>(entity);
        var items =  definition.LootTable;*/

        var loot = new List<Items>(definition.LootTable);
       
        ProcedureManager.AddStep(em, entity, new ProcuredCreationInfo
        {
            PtsWorkNeed = definition.GrowthRate,
            RequiresWorker = false,
            RequireActivation = false,
            ActionId = null,
            ItemOut = new List<Items>(),
            ItemIn = new List<Items>(),
            AttributesId = definition.Attributes
        });
        ProcedureManager.AddStep(em, entity, new ProcuredCreationInfo
        {
            PtsWorkNeed = definition.GrowthRate,
            RequiresWorker = true,
            RequireActivation = true,
            ActionId = "Destroy",
            ItemOut = loot,
            ItemIn = new List<Items>(),
            AttributesId = definition.Attributes
        });

        Database.AddInstance(DataType.Ressource, id, number, entity,em);
        return entity;
    }

  
}
public struct LootTable : IBufferElementData
{
    public Entity Item;
    public int Quantity;
}
