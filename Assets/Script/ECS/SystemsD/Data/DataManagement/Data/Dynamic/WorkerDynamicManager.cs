using Unity.Collections;
using Unity.Entities;
using UnityEngine;

public static class WorkerManager
{
    public static void AddOn(EntityManager em, Entity entity, int number = 2)
    {
     
        if (!em.HasBuffer<WorkerAssignment>(entity)) em.AddBuffer<WorkerAssignment>(entity);
        var workerAsigns = em.GetBuffer<WorkerAssignment>(entity);

        for (int i = 0; i < number; i++)
        {
            workerAsigns.Add(new WorkerAssignment { Pawn = Entity.Null });
        }
    }
    public static void AddEmplacement(EntityManager em, Entity entity, int number = 1)
    {
        if (!em.HasBuffer<WorkerAssignment>(entity))
        {
             AddOn(em, entity, number);
        }else
        {
            var workerAsigns = em.GetBuffer<WorkerAssignment>(entity);

            for (int i = 0; i < number; i++)
            {
                workerAsigns.Add(new WorkerAssignment { Pawn = Entity.Null });
            }
        }
    }
    public static void AssignWorker(EntityManager em, Entity entity, Entity pawn, int index)
    {
        var workerAsigns = em.GetBuffer<WorkerAssignment>(entity);
        if (index < 0 || index >= workerAsigns.Length) return;
        var workInC = new WorkIn { Building = entity, IndexWorker = index };
        if (em.HasComponent<WorkIn>(pawn))
        {
            var workIn = em.GetComponentData<WorkIn>(pawn);

            // Le pawn peut déjà travailler ailleurs : on libère son ancien emplacement
            // sans toucher à son WorkIn, remplacé juste après.
            if (workIn.Building != Entity.Null && (workIn.Building != entity || workIn.IndexWorker != index))
                ClearSlot(em, workIn.Building, workIn.IndexWorker);

            em.SetComponentData(pawn, workInC);
        }else
        {
            em.AddComponentData(pawn, workInC);
        }
        workerAsigns = em.GetBuffer<WorkerAssignment>(entity);
        workerAsigns[index] = new WorkerAssignment { Pawn = pawn };
    }
    public static void AssignWorker(EntityManager em, Entity entity, Entity pawn)
    {
        var workerAsigns = em.GetBuffer<WorkerAssignment>(entity);
        var index = -1;
        for (int i = 0; i < workerAsigns.Length; i++)
        {
            if (workerAsigns[i].Pawn == Entity.Null)
            {
                index = i;
                break;
            }
        }
        if (index != -1)
        {
            AssignWorker(em, entity, pawn, index);
        }
    }
    public static void RemoveWorker(EntityManager em, Entity entity, int index)
    {
        if (entity == Entity.Null || !em.Exists(entity) || !em.HasBuffer<WorkerAssignment>(entity))
            return;

        var workerAsigns = em.GetBuffer<WorkerAssignment>(entity);
        if (index < 0 || index >= workerAsigns.Length) return;

        var pawn = workerAsigns[index].Pawn;
        ClearSlot(em, entity, index);

        // Le pawn n'est plus dans ce worker : il ne doit plus porter son WorkIn.
        if (pawn != Entity.Null && em.Exists(pawn) && em.HasComponent<WorkIn>(pawn))
        {
            var workIn = em.GetComponentData<WorkIn>(pawn);
            if (workIn.Building == entity && workIn.IndexWorker == index)
                em.RemoveComponent<WorkIn>(pawn);
        }
    }
    public static void RemoveWorker(EntityManager em, Entity entity, Entity pawn)
    {
        if (pawn == Entity.Null || !em.Exists(pawn) || !em.HasComponent<WorkIn>(pawn)) return;

        var workIn = em.GetComponentData<WorkIn>(pawn);
        RemoveWorker(em, entity == Entity.Null ? workIn.Building : entity, workIn.IndexWorker);
    }

    /// <summary>Vide un emplacement sans toucher au WorkIn du pawn.</summary>
    private static void ClearSlot(EntityManager em, Entity building, int index)
    {
        if (building == Entity.Null || !em.Exists(building) || !em.HasBuffer<WorkerAssignment>(building))
            return;

        var slots = em.GetBuffer<WorkerAssignment>(building);
        if (index < 0 || index >= slots.Length) return;

        slots[index] = new WorkerAssignment { Pawn = Entity.Null };
    }
 

    
}
public struct WorkerAssignment: IBufferElementData  //On BUILDING
{
    public Entity Pawn; 
}
public struct WorkIn : IComponentData //On PAWN
{
    public Entity Building;
    public int IndexWorker;
}

