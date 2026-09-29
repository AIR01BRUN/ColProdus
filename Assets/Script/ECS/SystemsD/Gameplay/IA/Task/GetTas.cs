using Unity.Collections;
using Unity.Entities;
using UnityEngine;


public partial struct GetTaskSystem : ISystem
{
    public void OnCreate(ref SystemState state)
    {
       
    }

    public void OnUpdate(ref SystemState state)
    {
        var em = state.EntityManager;
      
        var pawns = QuerryDB.QueryInstances<CurrentTask>(em, DataType.Pawn);

        foreach (var pawn in pawns)
        {
    
            var currentTask = em.GetComponentData<CurrentTask>(pawn);
            if (currentTask.Task != Entity.Null) continue;


            NativeList<Entity> TasksAvailables = new NativeList<Entity>(Allocator.Temp);

            // Tâche personnelle du pawn (tâche urgente directement assignée).
            if(em.HasComponent<TaskSend>(pawn) && em.GetComponentData<TaskSend>(pawn).Task != Entity.Null)
            {
                TasksAvailables.Add(em.GetComponentData<TaskSend>(pawn).Task);
            }

            // Tâches publiées sur la checkroom de la zone du pawn (buffer TaskAvailable,
            // rempli par TaskAvailableSystem dans l'ordre d'envoi).
            if(em.HasComponent<WorkIn>(pawn) && em.GetComponentData<WorkIn>(pawn).Building != Entity.Null)
            {
                var checkroom = em.GetComponentData<WorkIn>(pawn).Building;
                if (em.Exists(checkroom) && em.HasBuffer<TaskAvailable>(checkroom))
                {
                    var buffer = em.GetBuffer<TaskAvailable>(checkroom);
                    for (int i = 0; i < buffer.Length; i++)
                    {
                        var task = buffer[i].Task;
                        if (task != Entity.Null && em.Exists(task) && em.HasComponent<Task>(task))
                            TasksAvailables.Add(task);
                    }
                }
            }

            Entity bestTask = Entity.Null;
            int highestPriority = 5;


           foreach(var task in TasksAvailables)
            {
                if (task == Entity.Null || !em.Exists(task) || !em.HasComponent<Task>(task))
                    continue;

                var taskInfo = em.GetComponentData<Task>(task);
                var priority = taskInfo.Priority;
                // Une tâche déjà réservée appartient uniquement au pawn qui la possède.
                if(!taskInfo.RequiresPawn) continue;
                if (taskInfo.Pawn != Entity.Null) continue;
                if (!taskInfo.CanBeDone) continue;
                // Tâche déjà prise (tag Submit) : pas disponible.
                if (em.HasComponent<Submit>(task)) continue;
                // Ordre d'exécution : la première tâche listée gagne à priorité égale
                // (le buffer respecte l'ordre d'envoi, donc la plus ancienne d'abord).
                if (priority < highestPriority)
                {
                    highestPriority = priority;
                    bestTask = task;
                }
            }
            if (bestTask != Entity.Null)
            {
                currentTask.Task = bestTask;
                em.SetComponentData(pawn, currentTask);

                var taskData = em.GetComponentData<Task>(bestTask);
                taskData.Pawn = pawn;
                em.SetComponentData(bestTask, taskData);

                // Marque la tâche comme prise : elle n'est plus publiée dans les TaskAvailable.
                if (!em.HasComponent<Submit>(bestTask))
                    em.AddComponentData(bestTask, new Submit());

            }
            else
            {
            }
            TasksAvailables.Dispose();
        }
    }

    private static string GetTaskName(EntityManager em, Entity task)
    {
        if (em.HasComponent<ID>(task))
            return em.GetComponentData<ID>(task).Id.ToString();
        return task.ToString();
    }

  
}