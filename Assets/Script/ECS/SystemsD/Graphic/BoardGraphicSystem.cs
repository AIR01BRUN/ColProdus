using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Rendering;
using Unity.Transforms;
using UnityEngine;


public partial struct BoardGraphicSystem : ISystem
{
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<BoardGraphicUpdate>();
    }

    public void OnUpdate(ref SystemState state)
    {


        var em = state.EntityManager;

        var material = Resources.Load<UnityEngine.Material>("Materials/GridMaterial");
        var materialHighlight = Resources.Load<UnityEngine.Material>("Materials/Highlight");
        var boardsUpdate = QuerryDB.QueryInstances<BoardGraphicUpdate>(em, DataType.Board);
        foreach(var board in boardsUpdate )
        {
            var id = em.GetComponentData<ID>(board);
            var size = em.GetComponentData<BoardSize>(board);
            var chunkMesh = MeshFactory.CreateBoard(size.GridSize.x, size.GridSize.y, size.CellSize);
            var chunkGraphicEntity = EntityGraphicsUtility.CreateGraphic(chunkMesh,material,em, $"board_GRAPH_{id.NumId}_{size.GridSize.x}_{size.GridSize.y}");
             em.AddComponentData(chunkGraphicEntity, new LocalTransform
            {
                Position = new float3(0,0,0),
                Rotation = quaternion.identity,
                Scale = 1f
            });

              var colliderBlob = Unity.Physics.MeshCollider.Create(
                chunkMesh,
                CollisionFilter.Default,
                Unity.Physics.Material.Default);

                
            em.AddComponentData(chunkGraphicEntity, new PhysicsCollider { Value = colliderBlob });
            em.AddSharedComponent(chunkGraphicEntity, new PhysicsWorldIndex { Value = 0 });
            em.AddComponentData(chunkGraphicEntity, new GraphicOf { Entity = board  });
            em.AddComponentData( board , new GraphicLink { GraphicEntity = chunkGraphicEntity });

        

           
       
            var highlightMesh = MeshFactory.CreateTile();
            var highlightGraphicEntity = EntityGraphicsUtility.CreateGraphic(highlightMesh,materialHighlight ,em,$"board_{id.NumId}_HighlightTile");
            em.AddComponentData(highlightGraphicEntity, new GraphicOf { Entity = board });
            em.AddComponentData(board, new HighlightRef{ GraphicEntity = highlightGraphicEntity });

            em.RemoveComponent<BoardGraphicUpdate>(board);

        }

          
        }

     
}

public struct BoardGraphicUpdate : IComponentData
{
    
}
public struct HighlightRef : IComponentData
{
   public Entity GraphicEntity;
}
