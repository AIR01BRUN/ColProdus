using Unity.Burst;
    using Unity.Entities;
    using Unity.Mathematics;
    using UnityEngine;
    using Unity.Collections;
using System.Collections.Generic;
using System.Linq;


public partial struct PlaceOnBoardSysetem : ISystem
{

    public void OnUpdate(ref SystemState state)
    {
        var em = state.EntityManager;
        var input = SystemAPI.GetSingleton<PlaceOnBoard_Input>();

        if (!SystemAPI.TryGetSingletonEntity<PlacedOnBoard>(out Entity requetsEntity)) return;
        var beingPlacedRW = SystemAPI.GetSingletonRW<PlacedOnBoard>();
        var dataType = beingPlacedRW.ValueRO.dataType ;

        
        Definition def = null;
        int2 size = new int2(1,1);
        var zone =  beingPlacedRW.ValueRO.Zone;
        var targetZone = beingPlacedRW.ValueRO.TargetEntity;
        if(dataType  == DataType.WorkZone)
        {
            var defWZ = QuerryDB.QueryDefinitions<WorkZoneDefinition>(DataType.WorkZone, beingPlacedRW.ValueRO.Id.ToString()).FirstOrDefault();
            size = defWZ != null ? defWZ.BaseSize : new int2(5,5);
            def = defWZ;
        }
        else
        {
            var defB = QuerryDB.QueryDefinitions<BuildingDefinition>(DataType.Building, beingPlacedRW.ValueRO.Id.ToString()).FirstOrDefault();
            size = defB != null ? defB.Size : new int2(1,1);
            def = defB;
        }
        if(def == null) return;
        if(targetZone != Entity.Null && em.Exists(targetZone) && em.HasComponent<Size>(targetZone))
            size = em.GetComponentData<Size>(targetZone).Value;
        var rotation = beingPlacedRW.ValueRW.Rotation;
       
        if (!em.HasComponent<GraphicLink>(requetsEntity))
        {
            var meshPreview = MeshFactory.CreateSquareMesh(size);
            var materialPreview =  Resources.Load<UnityEngine.Material>("Materials/Preview");
            var preview = EntityGraphicsUtility.CreateGraphic(meshPreview,materialPreview,em, "Preview");
            em.AddComponentData(requetsEntity,new GraphicLink
            {
                 GraphicEntity = preview,
            });
        }
       
        if (input.RotateBuildingLeft)
        {
            EntityGraphicsUtility.RotateMinus90(requetsEntity,em);
            beingPlacedRW.ValueRW.Rotation -= 90;
            rotation -= 90;
            if( beingPlacedRW.ValueRW.Rotation < 0) beingPlacedRW.ValueRW.Rotation = 270;
            
        }
            
        if (input.RotateBuildingRight)
        {
            EntityGraphicsUtility.Rotate90(requetsEntity,em);
            beingPlacedRW.ValueRW.Rotation += 90;
            rotation += 90;
            if( beingPlacedRW.ValueRW.Rotation > 270) beingPlacedRW.ValueRW.Rotation = 0;

        }

       
        if ( SystemAPI.TryGetSingleton<Mouse_Input>(out var mouse) && mouse.BoardSelect != Entity.Null){
        var board = mouse.BoardSelect;
        int2 position =  mouse.PositionSelect;
        
        Vector3 worldPos =   EntityGraphicsUtility.DefaultPositionInWorld(position,size, 0.05f);
        EntityGraphicsUtility.Move(requetsEntity,worldPos,em);
       
        // === POSITIONS OCCUPÉES (selon rotation) ===
        NativeList<int2> positions = BoardUtility.PositionTarget(position,size);

        bool CanPlace =  BoardUtility.CellsIsFree(em,board,positions);
        if(zone != Entity.Null)
            {
                
                CanPlace = CanPlace && WorkZoneManager.IsIn(em,zone ,positions);
            }

        if (input.PlaceBuilding && CanPlace)
        {
            Entity newBuilding;
            Entity oldBuilding = Entity.Null;
            if( dataType  == DataType.Building)
                {
                     if (beingPlacedRW.ValueRO.NumId == -1)
                    {
                        newBuilding = BuildingManager.Create(em, beingPlacedRW.ValueRO.Id.ToString());
                    }
                    else
                    {
                        oldBuilding = QuerryDB.QueryInstances(DataType.Building, beingPlacedRW.ValueRO.Id.ToString(), beingPlacedRW.ValueRO.NumId).FirstOrDefault();
                        newBuilding = BuildingManager.CreateCopyForMove(em, oldBuilding);
                    }
                }
else
                {
                    if (targetZone != Entity.Null && em.Exists(targetZone))
                    {
                        newBuilding = targetZone;
                    }
                    else
                    {
                        newBuilding = WorkZoneManager.Create(em, beingPlacedRW.ValueRO.Id.ToString());
                    }
                }
            

            bool placed = newBuilding != Entity.Null;
            if (placed)
            {
                if( dataType  == DataType.WorkZone)
                {
                    WorkZoneManager.PlaceZoneBackOnBoard(em, newBuilding, board, position, rotation);
                }
                else
                {
                    BoardManager.AddOn(em, newBuilding, board, position, rotation, true);
                }
                if( dataType  == DataType.Building)
                    {
                           BuildingManager.Build(em, newBuilding, oldBuilding);
                    if (oldBuilding != Entity.Null && em.Exists(oldBuilding))
                        BuildingManager.Deconstruct(em, oldBuilding);

                    if (zone != Entity.Null && em.Exists(zone))
                        WorkZoneManager.AddOn(em, zone, newBuilding);
                    }
              
            }

            if (placed)
            {
                em.DestroyEntity(em.GetComponentData<GraphicLink>(requetsEntity).GraphicEntity);
                em.DestroyEntity(requetsEntity);
            }
        }
        }

        if (input.CancelBuilding)
        {
            if (beingPlacedRW.ValueRO.dataType == DataType.WorkZone &&
                targetZone != Entity.Null && em.Exists(targetZone) &&
                !em.HasComponent<OnBoard>(targetZone))
            {
                if (!em.HasComponent<ZoneRubble>(targetZone))
                    em.AddComponentData(targetZone, new ZoneRubble());
            }

            if (em.HasComponent<GraphicLink>(requetsEntity))
                em.DestroyEntity(em.GetComponentData<GraphicLink>(requetsEntity).GraphicEntity);
            em.DestroyEntity(requetsEntity);
        } 
        
    }

        // === FONCTION STATIQUE DEMANDÉE ===
    public static void StartPlacingBuilding(EntityManager em, Definition def,int numdId = -1,Entity zone = default)
    {   
        ClearPendingPlacement(em);
        
        var entity = em.CreateEntity();
        em.AddComponentData(entity, new PlacedOnBoard
        {
            Id =  def.Id,
            NumId =   numdId ,
            dataType = def.DataType,
            Rotation = 0,
            Position = new int2(0, 0),
            Zone = zone,
        });
    }

    /// <summary>
    /// Annule un placement déjà lancé : l'entité PlacedOnBoard est détruite, et son
    /// graphique de prévisualisation (GraphicLink.GraphicEntity) aussi, sinon le
    /// fantôme du placement précédent reste affiché sur le plateau.
    /// </summary>
    private static void ClearPendingPlacement(EntityManager em)
    {
        var query = em.CreateEntityQuery(ComponentType.ReadOnly<PlacedOnBoard>());
        if (query.CalculateEntityCount() > 0)
        {
            query.TryGetSingletonEntity<PlacedOnBoard>(out Entity existingEntity);

            if (em.HasComponent<GraphicLink>(existingEntity))
            {
                var graphic = em.GetComponentData<GraphicLink>(existingEntity).GraphicEntity;
                if (graphic != Entity.Null && em.Exists(graphic))
                    em.DestroyEntity(graphic);
            }

            em.DestroyEntity(existingEntity);
        }
        query.Dispose();
    }

    /// <summary>
    /// Lance le placement interactif d'une zone déjà existante (zone détruite/reconstruite
    /// ou déplacée). La zone doit avoir été retirée du plateau au préalable.
    /// </summary>
    public static void StartPlacingZone(EntityManager em, Entity zone)
    {
        if (zone == Entity.Null || !em.Exists(zone) || !em.HasComponent<ID>(zone))
            return;

        ClearPendingPlacement(em);

        var id = em.GetComponentData<ID>(zone);
        var entity = em.CreateEntity();
        em.AddComponentData(entity, new PlacedOnBoard
        {
            Id = id.Id,
            NumId = id.NumId,
            dataType = DataType.WorkZone,
            Rotation = 0,
            Position = new int2(0, 0),
            TargetEntity = zone,
        });
    }

   


    public struct PlacedOnBoard : IComponentData //SINGLETON PLACE ITEM SUR LE BOARD
    {
        public FixedString64Bytes Id;
        public int NumId;
        public DataType dataType;
        public int Rotation;
        public int2 Position;
        public int2 Size;

        public Entity Zone;
        public Entity TargetEntity;
    }
    
    
    }
