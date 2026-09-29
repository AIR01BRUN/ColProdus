using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

public static class BuildingManager
{
    public static Entity Create(EntityManager em, string id)
    {
        var definition = QuerryDB.QueryDefinitions<BuildingDefinition>(DataType.Building, id).FirstOrDefault();

        var entity = em.CreateEntity();
        var number = Database.GetNextNumber(DataType.Building, id);

        em.AddComponentData(entity, new ID
        {
            DataType = DataType.Building,
            Id = id,
            NumId = number,
            Name = id
        });

        var size = definition.Size;
        em.AddComponentData(entity, new Size { Value = size });
        em.AddComponentData(entity, new RequiredToConstruction { PtsWorkRequis =  definition.PointsConstructionNeed });
        em.AddComponentData(entity, new StateBuilding { State = StateBuild.StateUpdate });
        em.AddComponentData(entity, new CurrentRecipe());

        if (id == "market")
        {
            em.AddBuffer<MarketSelection>(entity);
            em.AddComponentData(entity, new MarketInfo { Type = MarketType.SELL, CoinsToGive = 0 });
        }

        if (id == "checkroom")
            em.AddBuffer<TaskAvailable>(entity);

        if (id == "seedbag")
        {
            em.AddComponentData(entity, new SpawnResourceSelection { ResourceId = "tree" });
            ProcedureManager.AddStep(em, entity, new ProcuredCreationInfo
            {
                PtsWorkNeed = 20f,
                RequiresWorker = false,
                RequireActivation = false,
                ActionId = "SpawnResourceInZone",
                ItemOut = new List<Items>(),
                ItemIn = new List<Items>(),
                AttributesId = new List<FixedString32Bytes>()
            });
        }

        if (id == "mine")
            MineManager.Initialize(em, entity);

        if (id == SalaryManager.BuildingId && !em.HasComponent<SalaryAdministrator>(entity))
            em.AddComponent<SalaryAdministrator>(entity);

        if (!string.IsNullOrEmpty(definition.EnergyId))
            EnergyManager.Create(em, entity, definition.EnergyId, definition.EnergyMax, definition.EnergyConsume, definition.EnergyProduct);


        if (definition.Worker) WorkerManager.AddOn(em, entity);

        if (definition.RequiredItems != null && definition.RequiredItems.Count > 0)
        {
            var itemBuffer = em.AddBuffer<ItemRequiedToConstruction>(entity);
            foreach (var item in definition.RequiredItems)
            {
                if (item.ItemId == default)
                    continue;

                var itemEntity = FindItemDefinitionEntity(em, item.ItemId.ToString());
                if (itemEntity == Entity.Null)
                    continue;

                itemBuffer.Add(new ItemRequiedToConstruction
                {
                    Item = itemEntity,
                    Quantity = item.Quantity
                });
            }
        }

        Database.AddInstance(DataType.Building, id, number, entity,em);
        return entity;
    }

   

    private static Entity FindItemDefinitionEntity(EntityManager em, string itemId)
    {
        var query = em.CreateEntityQuery(ComponentType.ReadOnly<ID>());
        using var entities = query.ToEntityArray(Allocator.Temp);

        for (int i = 0; i < entities.Length; i++)
        {
            var entity = entities[i];
            if (!em.HasComponent<ID>(entity))
                continue;

            var id = em.GetComponentData<ID>(entity);
            if (id.DataType == DataType.Item && id.Id == itemId)
                return entity;
        }

        return Entity.Null;
    }

    public static void Build(EntityManager em, Entity building, Entity conditionEntity = default)
    {
             var stateBuilding = em.GetComponentData<StateBuilding>(building);
            var id = em.GetComponentData<ID>(building);
            var def = QuerryDB.QueryDefinitions<BuildingDefinition>(DataType.Building,id.Id.ToString()).FirstOrDefault();

            if(def.PointsConstructionNeed > 0)
            {
                stateBuilding.State = StateBuild.UnderConstruct;
                List<Items> itemNeeds = new List<Items>();

foreach(var reqItem in def.RequiredItems)
                {
                    itemNeeds.Add(reqItem);
                }

                ProcedureManager.AddStep(em, building, new ProcuredCreationInfo
                {
                    ItemIn = itemNeeds,
                    ItemOut = new List<Items>(),
                    PtsWorkNeed = def.PointsConstructionNeed,
                    RequiresWorker = true,
                    ActionId = "FinishConstruction",
                    ConditionId = conditionEntity != Entity.Null ? "NoExiste" : default,
                    ConditionEntity = conditionEntity,
                    AttributesId = def.Attributes
                });
            }
            else
            {
                stateBuilding.State = StateBuild.Available;

            }

            em.SetComponentData(building, stateBuilding);
    }



    public static void Deconstruct(EntityManager em,Entity building)
    {
        var state = em.GetComponentData<StateBuilding>(building);

        if(state.State == StateBuild.UnderConstruct)
        {
             ActionRegistry.Destroy(em, building);
            return;
        }
        if(state.State == StateBuild.Available)
        {
            var idInfo = em.GetComponentData<ID>(building);
            var def = QuerryDB.QueryDefinitions<BuildingDefinition>(DataType.Building, idInfo.Id.ToString()).FirstOrDefault();
            var itemOut = new List<Items>();
            foreach (var item in def.RequiredItems)
                itemOut.Add(item);

            if (em.HasComponent<InventoryLink>(building))
                foreach (var kvp in InventoryManager.GetInventoryDictionary(em, em.GetComponentData<InventoryLink>(building).Inventory))
                    if (kvp.Value > 0)
                        itemOut.Add(new Items(kvp.Key, kvp.Value));

            ProcedureManager.AddStep(em, building, new ProcuredCreationInfo
            {
                ActionId = "Destroy",
                ItemOut = itemOut,
                PtsWorkNeed = def.PointsConstructionNeed,
                RequiresWorker = true,
                AttributesId = def.Attributes
            }, true);
         

            ActionRegistry.RemoveFromBoard(em, building);
            state.State = StateBuild.Deconstruct;
            em.SetComponentData(building, state);
        }
    }

    public static Entity CreateCopyForMove(EntityManager em, Entity source)
    {
        if (em == null || source == Entity.Null || !em.Exists(source) || !em.HasComponent<ID>(source))
            return Entity.Null;

        var id = em.GetComponentData<ID>(source);
        var copy = Create(em, id.Id.ToString());
        if (copy != Entity.Null && em.HasComponent<MineState>(source) && em.HasComponent<MineState>(copy))
        {
            var mineState = em.GetComponentData<MineState>(source);
            mineState.LastResourceId = default;
            em.SetComponentData(copy, mineState);
        }

        return copy;
    }
    public static void CancelDeconstruct(EntityManager em,Entity building)
    {
        
    }
    public static void FinishConstruction(EntityManager em, Entity targetEntity)
    {
        if (em == null || targetEntity == Entity.Null || !em.Exists(targetEntity) ||
            !em.HasComponent<ID>(targetEntity) || !em.HasComponent<StateBuilding>(targetEntity))
            return;

        var buildingId = em.GetComponentData<ID>(targetEntity).Id.ToString();
        var definition = QuerryDB.QueryDefinitions<BuildingDefinition>(DataType.Building, buildingId).FirstOrDefault();
        if (definition == null)
            return;

        // Les éléments propres au bâtiment ne deviennent disponibles qu'après sa construction.
        if (definition.Inventory)
        {
            var hasValidInventory = em.HasComponent<InventoryLink>(targetEntity) &&
                                    em.GetComponentData<InventoryLink>(targetEntity).Inventory != Entity.Null &&
                                    em.Exists(em.GetComponentData<InventoryLink>(targetEntity).Inventory);

            if (!hasValidInventory)
            {
                if (em.HasComponent<InventoryLink>(targetEntity))
                    em.RemoveComponent<InventoryLink>(targetEntity);

                InventoryManager.Create(em, targetEntity, InventoryType.StockInventory, 8);
            }
        }

        var stateBuilding = em.GetComponentData<StateBuilding>(targetEntity);
        stateBuilding.State = StateBuild.Available;
        em.SetComponentData(targetEntity, stateBuilding);

        // La procédure de construction est la première étape ; les éventuelles
        // procédures suivantes (notamment une recette) doivent être conservées.
        if (definition.PointsConstructionNeed > 0)
            ProcedureManager.RemoveFirstStep(em, targetEntity);

if (buildingId == "farm")
            RecipeManager.SetRecipe(em, targetEntity, "farm_wheat_recipe");
        else if (buildingId == "grindstone")
            RecipeManager.SetRecipe(em, targetEntity, "grindstone_recipe");
        else if (buildingId == "kitchencounter")
            RecipeManager.SetRecipe(em, targetEntity, "kitchencounter_recipe");
        else if (buildingId == "furnace")
            RecipeManager.SetRecipe(em, targetEntity, "furnace_recipe");
        else if (buildingId == "market")
        {
            // La sélection de vente permanente est reconstruite dès que le bâtiment est opérationnel.
            if (em.HasBuffer<MarketSelection>(targetEntity) && em.GetBuffer<MarketSelection>(targetEntity).Length > 0)
            {
                var selection = em.GetBuffer<MarketSelection>(targetEntity)[0];
                var type = em.HasComponent<MarketInfo>(targetEntity)
                    ? em.GetComponentData<MarketInfo>(targetEntity).Type
                    : MarketType.SELL;
            if (selection.Item.ItemId.Length > 0)
                MarketManager.SetMarket(em, targetEntity, type, selection.Item);
            }
        }

        // Un bâtiment de salaire vient d'être opérationnel : sa demande de currency
        // doit valoir la masse salariale de la colonie.
        SalaryManager.RefreshDemand(em);
    }


    
}



// ------- Data -------
//CONSTRUCTION
public struct Size : IComponentData
{
    public int2 Value;
}

public struct ItemRequiedToConstruction : IBufferElementData
{
    public Entity Item;
    public int Quantity;
}

public struct RequiredToConstruction : IComponentData
{
    public float PtsWorkRequis;
}

// ------- IN GAME -------
public struct StateBuilding : IComponentData
{
    public StateBuild State;
}

public struct BuildingWorkZoneLink : IBufferElementData
{
    public Entity Zone;
}

public struct CurrentRecipe : IComponentData
{
    public FixedString64Bytes RecipeId;
}

public enum StateBuild
{
    StateUpdate,
    Available,
    UnderConstruct,
    Deconstruct,
}
