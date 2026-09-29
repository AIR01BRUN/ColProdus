using System.Linq;
using Unity.Entities;

public static class ItemManager
{
    public static Entity Create(EntityManager em, string id)
    {
        
        var definition = QuerryDB.QueryDefinitions<ItemDefinition>(DataType.Item, id).FirstOrDefault();
        if (definition == null)
            return Entity.Null;

        var entity = em.CreateEntity();
        var number = Database.GetNextNumber(DataType.Item, id);

        em.AddComponentData(entity, new ID
        {
            DataType = DataType.Item,
            Id = id,
            NumId = number,
            Name = id
        });

        Database.AddInstance(DataType.Item, id, number, entity,em);
        return entity;
    }

    /// <summary>Id du premier item dont le type est "food" (nourriture consommable).</summary>
    public static string GetFirstFoodItemId()
    {
        foreach (var definition in QuerryDB.QueryDefinitions<ItemDefinition>(DataType.Item))
            if (definition.Type == "food")
                return definition.Id;
        return null;
    }

    /// <summary>Stat nutrition d'un item (en pourcentage de besoin restauré).</summary>
    public static float GetItemNutrition(string itemId)
    {
        var definition = QuerryDB.QueryDefinitions<ItemDefinition>(DataType.Item, itemId).FirstOrDefault();
        return definition != null ? definition.Nutrition : 0f;
    }

    /// <summary>Un item de type "currency" est de la monnaie : un pawn la conserve au lieu de la vider.</summary>
    public static bool IsCurrency(string itemId)
    {
        var definition = QuerryDB.QueryDefinitions<ItemDefinition>(DataType.Item, itemId).FirstOrDefault();
        return definition != null && definition.Type == "currency";
    }
  
}
