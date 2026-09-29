using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.Rendering;

public static class EntityGraphicsUtility
{
    public static void DestroyGraphic(EntityManager em, Entity graphicEntity)
    {
        if (em == null || graphicEntity == Entity.Null || !em.Exists(graphicEntity))
        {
            return;
        }

        em.DestroyEntity(graphicEntity);
    }

    public static void DestroyGraphicFromParent(EntityManager em, Entity parentEntity)
    {
        if (em == null || parentEntity == Entity.Null || !em.Exists(parentEntity))
        {
            return;
        }

        if (!em.HasComponent<GraphicLink>(parentEntity))
        {
            return;
        }

        var graphicEntity = em.GetComponentData<GraphicLink>(parentEntity).GraphicEntity;
        DestroyGraphic(em, graphicEntity);
    }

    public static Entity CreateLineGraphic(EntityManager em, string name = "")
    {
        var material = Resources.Load<Material>("Materials/Highlight");
        if (material == null)
        {
            return Entity.Null;
        }

        return CreateGraphic(MeshFactory.CreateLine(), material, em, "LINE_" + name);
    }

    public static void UpdateLine(Entity lineEntity, float3 start, float3 end, EntityManager em)
    {
        if (lineEntity == Entity.Null || !em.Exists(lineEntity))
        {
            return;
        }

        var transform = em.GetComponentData<LocalTransform>(lineEntity);
        float3 delta = end - start;
        float length = math.length(new float2(delta.x, delta.z));

        if (length <= 0.001f)
        {
            transform.Scale = 0f;
            em.SetComponentData(lineEntity, transform);
            return;
        }

        float angle = -math.atan2(delta.z, delta.x);
        transform.Position = (start + end) * 0.5f + new float3(0f, 0.025f, 0f);
        transform.Rotation = quaternion.RotateY(angle);
        transform.Scale = length;
        em.SetComponentData(lineEntity, transform);
    }

    public static Entity CreateEmptyGraphic(EntityManager em, string name = "")
    {
        var graphicEntity = em.CreateEntity();
        em.SetName(graphicEntity, "GRAPHIC_" + name);

        em.AddComponentData(graphicEntity, new LocalTransform
        {
            Position = float3.zero,
            Rotation = quaternion.identity,
            Scale = 1f
        });

        return graphicEntity;
    }


    public static Entity CreateGraphic(Mesh mesh ,Material material ,EntityManager em, string name = "")
    {
        
        var graphicEntity = em.CreateEntity();
        em.SetName(graphicEntity,"G_"+name);
        
        var desc = new RenderMeshDescription(
            shadowCastingMode: ShadowCastingMode.Off,
            receiveShadows: false);

        var renderMeshArray = new RenderMeshArray(
            new[] { material },
            new[] { mesh });
        
          var materialMeshInfo =
            MaterialMeshInfo.FromRenderMeshArrayIndices(
                0, // material index
                0  // mesh index
            );
    
        RenderMeshUtility.AddComponents(
            graphicEntity,
            em,
            desc,
            renderMeshArray,
            materialMeshInfo);
        
          em.AddComponentData(graphicEntity , new LocalTransform
        {
            Position = float3.zero,
            Rotation = quaternion.identity,
            Scale = 1f
            });
       
       return graphicEntity;
    }

    public static void AddCombinedGraphicItem(Entity graphicEntity, Entity sourceEntity, float3 position, quaternion rotation, float3 scale, EntityManager em)
    {
        if (!em.HasBuffer<BoardGraphicItem>(graphicEntity))
        {
            em.AddBuffer<BoardGraphicItem>(graphicEntity);
        }

        var buffer = em.GetBuffer<BoardGraphicItem>(graphicEntity);
        buffer.Add(new BoardGraphicItem
        {
            SourceEntity = sourceEntity,
            Position = position,
            Rotation = rotation,
            Scale = scale
        });
    }


    public static Vector3 GetPosition(Entity entity,EntityManager em)
    {
        var entityG = em.GetComponentData<GraphicLink>(entity).GraphicEntity;
        var localToWorld = em.GetComponentData<LocalTransform>(entityG);
        return localToWorld.Position;
    }

   
    public static void Move(Entity entity, float3 position, EntityManager em)
    {
        if (!em.HasComponent<LocalTransform>(entity))
        {
            entity = em.GetComponentData<GraphicLink>(entity).GraphicEntity;
        }
        var localToWorld = em.GetComponentData<LocalTransform>( entity);
        localToWorld.Position = position;
        em.SetComponentData( entity,localToWorld);
    }
    public static Vector3 DefaultPositionInWorld(int2 pos, int2 size, float offsetZ = 0.01f)
    {
        // pos = coin bas-gauche de la zone occupée (grille en unités de 1f)
        // On place l'objet au centre de sa zone (pivot au centre)
        return new Vector3(
            pos.x + size.x * 0.5f,
            offsetZ,
            pos.y + size.y * 0.5f
        );
    }

    public static void MoveGraphicEntity(Entity graphicEntity, float3 position, EntityManager em)
    {
        var localToWorld = em.GetComponentData<LocalTransform>(graphicEntity);
        localToWorld.Position = position;
        em.SetComponentData(graphicEntity, localToWorld);
    }

   public static void RotateY(Entity entity, float degrees, EntityManager em)
    {
       if (!em.HasComponent<LocalTransform>(entity))
        {
            entity = em.GetComponentData<GraphicLink>(entity).GraphicEntity;
        }
        var graphicEntity = entity;
        var localTransform = em.GetComponentData<LocalTransform>(graphicEntity);
        quaternion additionalRot = quaternion.RotateY(math.radians(degrees));
        localTransform.Rotation = math.mul(localTransform.Rotation, additionalRot);
        em.SetComponentData(graphicEntity, localTransform);
    }


    
    public static void RotateMinus90(Entity entity, EntityManager em)
    {
        RotateY(entity, -90f, em);
    }
    public static void Rotate90(Entity entity, EntityManager em)
    {
        RotateY(entity, 90f, em);
    }

    public static int GetRotationY(Entity entity, EntityManager em)
    {

        var graphicLink = em.GetComponentData<GraphicLink>(entity);
        Entity graphicEntity = graphicLink.GraphicEntity;
        var localTransform = em.GetComponentData<LocalTransform>(graphicEntity);
        float angle = math.degrees(math.atan2(
            2.0f * (localTransform.Rotation.value.w * localTransform.Rotation.value.y + 
                    localTransform.Rotation.value.x * localTransform.Rotation.value.z),
            1.0f - 2.0f * (localTransform.Rotation.value.y * localTransform.Rotation.value.y + 
                           localTransform.Rotation.value.z * localTransform.Rotation.value.z)));

        // Normaliser entre 0 et 359.999°
        angle = angle % 360f;
        if (angle < 0f)
            angle += 360f;

        return (int)angle;
    }
  
 

    
}

public struct DataTag :  IComponentData
{}



public struct BoardGraphicItem : IBufferElementData
{
    public Entity SourceEntity;
    public float3 Position;
    public quaternion Rotation;
    public float3 Scale;
}

