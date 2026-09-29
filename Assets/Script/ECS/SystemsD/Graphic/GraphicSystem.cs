using System.Linq;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Rendering;
using Unity.Transforms;
using UnityEngine;


public partial struct GraphicSystem : ISystem
{
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<GraphicUpdate>();
    }

    public void OnUpdate(ref SystemState state)
    {


        var em = state.EntityManager;
        var entityToUpdate = QuerryDB.QueryInstances<GraphicUpdate>(em);
        foreach(var entity in entityToUpdate)
        {
            var id = em.GetComponentData<ID>(entity);
            var graphicEntity = Entity.Null;
        
            if(em.HasComponent<GraphicLink>(entity))
            {
                graphicEntity = em.GetComponentData<GraphicLink>(entity).GraphicEntity;
            }
            else
            {
           
                var definition = QuerryDB.QueryDefinitions<Definition>(id.DataType, id.Name.ToString()).FirstOrDefault();
                var mesh = definition.Mesh;
                var material = definition.Material;
            
                graphicEntity = EntityGraphicsUtility.CreateGraphic(mesh,material,em, $"ressource_GRAPH_{id.Name}_{id.NumId}");

                em.AddComponentData(graphicEntity, new GraphicOf { Entity = entity });
                em.AddComponentData(entity, new GraphicLink { GraphicEntity = graphicEntity });

            }
           
            Vector3 worldPos = Vector3.zero;
            int rotation = 0;
            int2 size  = new int2(1,1);

            if (em.HasComponent<Size>(entity))
            {
                size = em.GetComponentData<Size>(entity).Value;
            }

            if (em.HasComponent<OnBoard>(entity))
            {
                var onBoard = em.GetComponentData<OnBoard>(entity);
                worldPos =  EntityGraphicsUtility.DefaultPositionInWorld(onBoard.Position,size );
                rotation = onBoard.Rotation;
            }

            EntityGraphicsUtility.Move(graphicEntity, worldPos, em);
            EntityGraphicsUtility.RotateY(graphicEntity, rotation, em);
            em.RemoveComponent<GraphicUpdate>(entity);

        }

          
        }

     
}


public struct GraphicUpdate : IComponentData
{
}
public struct GraphicOf :  IComponentData
{
    public Entity Entity;
}
public struct GraphicLink:  IComponentData
{
    public Entity GraphicEntity;
}


