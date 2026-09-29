using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

public static class WorkZoneManager
{
    public static Entity Create(EntityManager em, string id)
    {
        var definition = QuerryDB.QueryDefinitions<WorkZoneDefinition>(DataType.WorkZone, id).FirstOrDefault();
        if (definition == null)
            return Entity.Null;

        var entity = em.CreateEntity();
        var number = Database.GetNextNumber(DataType.WorkZone, id);

        em.AddComponentData(entity, new ID
        {
            DataType = DataType.WorkZone,
            Id = id,
            NumId = number,
            Name = id
        });

        em.AddComponentData(entity, new WorkZone
        { 
            Level = definition.Level
        });
em.AddComponentData(entity,new Size{ Value = definition.BaseSize});
     
        em.AddBuffer<WorkZoneEntity>(entity);

        Database.AddInstance(DataType.WorkZone, id, number, entity, em);
        return entity;
    }

    public static Entity GetInstance(EntityManager em, string id)
    {
        var instances = QuerryDB.QueryInstances(DataType.WorkZone, id);
        return instances.FirstOrDefault();
    }
    public static bool TryGetCamp(out Entity camp)
    {
       camp = QuerryDB.QueryInstances(DataType.WorkZone, "camp").FirstOrDefault();
       if(camp == default ) return false;
       return true;
    }

    public static void AddOn(EntityManager em, Entity workZone, Entity building)
    {
        if (workZone == Entity.Null || building == Entity.Null || !em.Exists(workZone) || !em.Exists(building))
            return;

        var buffer = em.GetBuffer<WorkZoneEntity>(workZone);
        for (int i = 0; i < buffer.Length; i++)
        {
            if (buffer[i].Entity == building)
            {
                SetInZonePosition(em, building);
                return;
            }
        }

        buffer.Add(new WorkZoneEntity { Entity = building });

        var link = new WorkZoneLink { WorkZone = workZone };
        if (em.HasComponent<OnBoard>(building) && em.HasComponent<OnBoard>(workZone))
        {
            link.InZonePosition = em.GetComponentData<OnBoard>(building).Position - em.GetComponentData<OnBoard>(workZone).Position;
            link.Rotation = em.GetComponentData<OnBoard>(building).Rotation;
        }

        if (em.HasComponent<WorkZoneLink>(building))
            em.SetComponentData(building, link);
        else
            em.AddComponentData(building, link);
    }

    /// <summary>Recalcule la position/rotation du building relative à sa zone (après un déplacement).</summary>
    public static void SetInZonePosition(EntityManager em, Entity building)
    {
        if (!em.HasComponent<WorkZoneLink>(building) || !em.HasComponent<OnBoard>(building))
            return;

        var link = em.GetComponentData<WorkZoneLink>(building);
        var workZone = link.WorkZone;
        if (workZone == Entity.Null || !em.Exists(workZone) || !em.HasComponent<OnBoard>(workZone))
            return;

        var onBoard = em.GetComponentData<OnBoard>(building);
        link.InZonePosition = onBoard.Position - em.GetComponentData<OnBoard>(workZone).Position;
        link.Rotation = onBoard.Rotation;
        em.SetComponentData(building, link);
    }

    /// <summary>
    /// Retire une zone du plateau (graphique supprimée, plus sur le plateau) sans détruire
    /// l'entité. Chaque graphique de la zone ET de tous ses buildings est détruite, et leurs
    /// cellules sont libérées (via BoardManager.RemoveOn). Si <paramref name="addRubble"/> est
    /// vrai, la zone reçoit le tag ZoneRubble (onglet ZoneStock) ; sinon le tag est retiré.
    /// </summary>
    public static void RemoveZoneFromBoard(EntityManager em, Entity zone, bool addRubble)
    {
        if (zone == Entity.Null || !em.Exists(zone))
            return;

        if (em.HasBuffer<WorkZoneEntity>(zone))
        {
            var buildings = new List<Entity>();
            foreach (var entry in em.GetBuffer<WorkZoneEntity>(zone))
                buildings.Add(entry.Entity);

            foreach (var building in buildings)
            {
                if (building == Entity.Null || !em.Exists(building))
                    continue;
                BoardManager.RemoveOn(em, building);
            }
        }

        BoardManager.RemoveOn(em, zone);

        if (addRubble)
        {
            if (!em.HasComponent<ZoneRubble>(zone))
                em.AddComponentData(zone, new ZoneRubble());
        }
        else if (em.HasComponent<ZoneRubble>(zone))
        {
            em.RemoveComponent<ZoneRubble>(zone);
        }
    }

    /// <summary>
    /// Replace une zone sur le plateau (ainsi que tous ses buildings en position relative InZonePosition).
    /// Retire le tag ZoneRubble si présent.
    /// </summary>
    public static void PlaceZoneBackOnBoard(EntityManager em, Entity zone, Entity board, int2 position, int rotation)
    {
        if (zone == Entity.Null || !em.Exists(zone) || board == Entity.Null || !em.Exists(board))
            return;

        if (em.HasComponent<ZoneRubble>(zone))
            em.RemoveComponent<ZoneRubble>(zone);

        BoardManager.AddOn(em, zone, board, position, rotation, true);

        if (!em.HasBuffer<WorkZoneEntity>(zone))
            return;
        var workZoneEntityBuffer =   em.GetBuffer<WorkZoneEntity>(zone).ToNativeArray(Allocator.Temp);
        foreach (var entry in  workZoneEntityBuffer )
        {
            var building = entry.Entity;
            if (building == Entity.Null || !em.Exists(building))
                continue;
            if (!em.HasComponent<WorkZoneLink>(building))
                continue;

            var link = em.GetComponentData<WorkZoneLink>(building);
            BoardManager.AddOn(em, building, board, position + link.InZonePosition, link.Rotation, true);
        }
        workZoneEntityBuffer.Dispose();
    }
public static bool TryGetBuildingWorker(EntityManager em, Entity workZone, out Entity building)
    {
        var entitysHas = em.GetBuffer<WorkZoneEntity>(workZone);
        building = Entity.Null;
        foreach (var entity in  entitysHas)
        {
            if (entity.Entity == Entity.Null || !em.Exists(entity.Entity) || !em.HasComponent<ID>(entity.Entity))
                continue;

            var id  = em.GetComponentData<ID>(entity.Entity);
            if(id.Id == "checkroom")
            {
                if (IsUnderConstruction(em, entity.Entity))
                    continue;
                building = entity.Entity;
                return true;
            }
        }
        return false;
    }

/// <summary>Retourne un bâtiment construit de la zone possédant un inventaire utilisable.</summary>
    public static List<Entity> GetBuildingWithInventory(EntityManager em, Entity workZone)
    {
        List<Entity> building = new();

        if (workZone == Entity.Null || !em.Exists(workZone) || !em.HasBuffer<WorkZoneEntity>(workZone))
            return building;

        var entities = em.GetBuffer<WorkZoneEntity>(workZone);
        foreach (var entry in entities)
        {
            if (entry.Entity == Entity.Null || !em.Exists(entry.Entity) || !em.HasComponent<ID>(entry.Entity))
                continue;

            var id  = em.GetComponentData<ID>( entry.Entity);
            if(id.Id == "inventory")
            {
                if (IsUnderConstruction(em, entry.Entity))
                    continue;
                building.Add(entry.Entity);
            }
        }

        return building;
    }

    private static bool IsUnderConstruction(EntityManager em, Entity building)
    {
        if (building == Entity.Null || !em.Exists(building) || !em.HasComponent<StateBuilding>(building))
            return false;
        return em.GetComponentData<StateBuilding>(building).State == StateBuild.UnderConstruct;
    }

    /// <summary>Retourne le premier bâtiment de la zone qui possède un inventaire utilisable.</summary>
    public static bool TryGetBuildingWithInventory(EntityManager em, Entity workZone, out Entity building)
    {
        building = GetBuildingWithInventory(em, workZone).FirstOrDefault();
        return building != Entity.Null;
    }

    /// <summary>
    /// Remplit le buffer ItemNeed de l'inventaire de la zone (InventoryStock) à partir des
    /// items requis par chaque étape de chaque procédure/recette des bâtiments de la zone.
    /// Quantité de base 30 (limite de stock) ; une entrée déjà présente est conservée.
    /// </summary>
    public static void RefreshStockItemNeed(EntityManager em, Entity workZone)
    {
        if (workZone == Entity.Null || !em.Exists(workZone) || !em.HasBuffer<WorkZoneEntity>(workZone))
            return;

        if (!TryGetBuildingWithInventory(em, workZone, out var inventoryBuilding)
            || !em.HasComponent<InventoryLink>(inventoryBuilding))
            return;

        var stockInventory = em.GetComponentData<InventoryLink>(inventoryBuilding).Inventory;
        if (!em.Exists(stockInventory))
            return;

        const int defaultQuantity = 30;
        var entities = em.GetBuffer<WorkZoneEntity>(workZone);
        foreach (var entry in entities)
        {
            var building = entry.Entity;
            if (!em.Exists(building) || !em.HasBuffer<ProcedureStep>(building))
                continue;

            var steps = em.GetBuffer<ProcedureStep>(building);
            foreach (var stepEntry in steps)
            {
                var step = stepEntry.Value;
                if (!em.Exists(step) || !em.HasBuffer<StepItemIn>(step))
                    continue;

                var inputs = em.GetBuffer<StepItemIn>(step).ToNativeArray(Allocator.Temp);
                for (int i = 0; i < inputs.Length; i++)
                {
                    var input = inputs[i];
                    if (input.Item.ItemId.Length == 0)
                        continue;
                    InventoryManager.AddItemNeed(em, stockInventory, new Items(input.Item.ItemId, defaultQuantity));
                }
                inputs.Dispose();
            }
        }
    }
    
    public static bool IsIn(EntityManager em, Entity workZone,int2 position)
    {
        if (workZone == Entity.Null || !em.Exists(workZone) || !em.HasComponent<OnBoard>(workZone))
            return false;
        if (!em.HasBuffer<CellsOccupy>(workZone))
            return false;
        var cellOcupe = em.GetBuffer<CellsOccupy>(workZone);
        var board = em.GetComponentData<OnBoard>(workZone).Board;
         var index = BoardUtility.PosToIndex(em,board,position);
        foreach (var cell in cellOcupe)
        {
            if(index == cell.IndexCell) return true;
        }
        return false;
    }
    public static bool IsIn(EntityManager em, Entity workZone,NativeList<int2> positions)
    {
        foreach(var pos in positions)
        {
            if(!IsIn(em,workZone,pos))return false;
        }
        return true;
    }
       

    
}

public struct WorkZone : IComponentData
{
    public int Level;
}


public struct WorkZoneEntity : IBufferElementData
{
    public Entity Entity;
}
public struct WorkZoneLink :  IComponentData
{
    public Entity WorkZone;
    public int2 InZonePosition;
    public int Rotation;
}

/// <summary>Tag posé sur une zone détruite (visible dans l'onglet ZoneStock de la sélection de construction).</summary>
public struct ZoneRubble : IComponentData
{
}

/// <summary>
/// Tag posé sur un building retiré du plateau (zone détruite/déplacée) :
/// il ne peut plus créer de tâches ni être interactif. Retiré dès qu'il est replacé.
/// </summary>
public struct BuildingRubble : IComponentData
{
}

/// <summary>
/// Zone qui fait apparaître une ressource (ex: défricheur scierie) : id de la
/// ressource à générer dans la zone quand sa procédure de spawn se termine.
/// </summary>
public struct SpawnResourceSelection : IComponentData
{
    public FixedString64Bytes ResourceId;
}
