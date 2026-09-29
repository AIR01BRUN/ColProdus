using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

public partial struct MovePawnSystem : ISystem
{
    public void OnCreate(ref SystemState state)
    {
        var em = state.EntityManager;
    }

    public void OnUpdate(ref SystemState state)
    {
        var em = state.EntityManager;

        var ActionToMove = QuerryDB.QueryInstances<MoveTo>(em, DataType.Action);

        foreach(var action in ActionToMove )
        {
            
            var moveTo = em.GetComponentData<MoveTo>(action);
            var actionToDo =  em.GetComponentData<ActionLink>(action);
            if(actionToDo .Pawn == Entity.Null) continue;
            //if(moveTo.Finish) continue;
            var pawn = actionToDo .Pawn;
            if(!em.HasComponent<OnBoard>(pawn)) continue;
            var onBoard = em.GetComponentData<OnBoard>(pawn);
            var board = onBoard.Board;
            var position = onBoard.Position;
            var positionTarget = moveTo.Position;

            var graphicEntity = em.GetComponentData<GraphicLink>(pawn).GraphicEntity;
            var transform = em.GetComponentData<LocalTransform>(graphicEntity);
            var positionWorld = transform.Position;
            var positionTargetWorld =  BoardUtility.PosToWorld(positionTarget,0.01f,0.5f);

            float distance = math.distance(positionWorld,positionTargetWorld );
   
            if(distance > 0.01f)
            {
                var pathNode = em.GetBuffer<PathNode>(pawn);
                if(pathNode.Length == 0)
                {
                    if(BoardUtility.FindPath(em,board,position,positionTarget, out List<int2> path))
                    {
                        foreach(var node in path)
                        {
                            pathNode.Add(new PathNode{ Position = node});
                        }
                    }
                    else
                    {
                        // Aucun chemin vers la cible : on termine l'action plutôt que
                        // de laisser le pawn bloqué.
                        moveTo.NotFind = true;
                        em.SetComponentData(action, moveTo);
                        HideMovementPath(em, pawn);
                        ActionManager.Finish(em, action);
                        continue;
                    }
                    
                }
                else
                {
                    pathNode = em.GetBuffer<PathNode>(pawn);
                    var nodePosition = pathNode[0].Position;
                    var nodePositionWorld = BoardUtility.PosToWorld(nodePosition,0.01f,0.5f);
                    Vector3 newWorldPosition = Vector3.MoveTowards( positionWorld,nodePositionWorld, AttributeManager.GetActual(em, pawn, AttributeManager.MoveSpeed) * Time.deltaTime);
                    EntityGraphicsUtility.Move(graphicEntity,newWorldPosition ,em);

                    var distanceNode = math.distance(newWorldPosition,nodePositionWorld);
                    if(distanceNode < 0.01f)
                    {
                        BoardManager.AddOn(em, pawn, board, nodePosition, onBoard.Rotation,true);
                        pathNode = em.GetBuffer<PathNode>(pawn);
                        pathNode.RemoveAt(0);
                    }
                }
            }
            else
            {
                if (!onBoard.Position.Equals(positionTarget))
                {
                    BoardManager.AddOn(em, pawn, board, positionTarget, onBoard.Rotation,true);
                    continue;
                }

                ActionManager.Finish(em, action);
            }

            UpdateMovementPath(em, pawn, positionWorld, positionTargetWorld);
        }


    }

    private static void UpdateMovementPath(EntityManager em, Entity pawn, float3 currentPosition, float3 targetPosition)
    {
        var pathNodes = em.GetBuffer<PathNode>(pawn);
        if (pathNodes.Length == 0)
        {
            HideMovementPath(em, pawn);
            return;
        }


        EnsureLineCount(em, pawn, pathNodes.Length);
        var lineSegments = GetMovementLineSegments(em, pawn);
        pathNodes = em.GetBuffer<PathNode>(pawn);
        float3 segmentStart = currentPosition;
        for (int index = 0; index < pathNodes.Length; index++)
        {
            float3 segmentEnd = BoardUtility.PosToWorld(pathNodes[index].Position, 0.01f, 0.5f);
            EntityGraphicsUtility.UpdateLine(lineSegments[index].LineEntity, segmentStart, segmentEnd, em);
            segmentStart = segmentEnd;
        }

        for (int index = pathNodes.Length; index < lineSegments.Length; index++)
        {
            EntityGraphicsUtility.UpdateLine(lineSegments[index].LineEntity, segmentStart, segmentStart, em);
        }
    }

    private static void HideMovementPath(EntityManager em, Entity pawn)
    {
        if (!em.HasBuffer<MovementLineSegment>(pawn)) return;

        var lineSegments = em.GetBuffer<MovementLineSegment>(pawn);
        foreach (var segment in lineSegments)
        {
            EntityGraphicsUtility.UpdateLine(segment.LineEntity, float3.zero, float3.zero, em);
        }
    }

    private static DynamicBuffer<MovementLineSegment> GetMovementLineSegments(EntityManager em, Entity pawn)
    {
        if (!em.HasBuffer<MovementLineSegment>(pawn))
        {
            em.AddBuffer<MovementLineSegment>(pawn);
        }

        return em.GetBuffer<MovementLineSegment>(pawn);
    }

    private static void EnsureLineCount(EntityManager em, Entity pawn, int count)
    {
        var lineSegments = GetMovementLineSegments(em, pawn);
        while (lineSegments.Length < count)
        {
      
            var lineEntity = EntityGraphicsUtility.CreateLineGraphic(em, $"Pawn_{pawn.Index}_{lineSegments.Length}");
            if (lineEntity == Entity.Null) return;
                  lineSegments = GetMovementLineSegments(em, pawn);
            lineSegments.Add(new MovementLineSegment { LineEntity = lineEntity });
        }
    }
}
public struct MoveTo :IComponentData
{
    
    public int2 Position;
    public bool NotFind;
}

public struct MovementLineSegment : IBufferElementData
{
    public Entity LineEntity;
}
