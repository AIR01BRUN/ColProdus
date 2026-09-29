using Unity.Entities;

/// <summary>
/// Surveille tous les bâtiments qui possèdent une énergie (Energy). L'énergie ne baisse
/// jamais avec le temps : elle ne perd de la valeur que lorsqu'une étape de procédure se
/// termine (TaskProcedureSystem). Ici on ne fait que surveiller la réserve : un bâtiment
/// consommateur dont la réserve est inférieure à ce qu'une étape consomme
/// (EnergyActual < EnergyConsume) prend le slot TaskSend du bâtiment : si une tâche est
/// déjà envoyée, elle est remplacée par une tâche "GetEnergy" (libérée dans la foulée).
/// Comme ProcedureGameplaySystem, la procédure ne repart qu'une fois la tâche d'énergie
/// terminée (TaskSend remis à vide).
/// </summary>
public partial struct EnergySystem : ISystem
{
    public void OnCreate(ref SystemState state)
    {
    }

    public void OnUpdate(ref SystemState state)
    {
        var em = state.EntityManager;

        foreach (var building in QuerryDB.QueryInstances<Energy>(em, DataType.Building))
        {
            var energy = em.GetComponentData<Energy>(building);
            if (energy.EnergyMax <= 0 || energy.EnergyConsume <= 0 || energy.EnergyActual >= energy.EnergyConsume)
                continue;

            if (!em.HasComponent<TaskSend>(building))
                em.AddComponentData(building, new TaskSend { Task = Entity.Null });

            var taskSend = em.GetComponentData<TaskSend>(building);
            if (taskSend.Task != Entity.Null)
            {
                if (em.Exists(taskSend.Task) && em.HasComponent<ID>(taskSend.Task) &&
                    em.GetComponentData<ID>(taskSend.Task).Id == "GetEnergy")
                    continue;

                // Tâche existante (procédure ou autre) : elle est remplacée.
                if (em.Exists(taskSend.Task))
                    TaskManager.Finish(em, taskSend.Task);
                else
                {
                    taskSend.Task = Entity.Null;
                    em.SetComponentData(building, taskSend);
                }
            }

            var taskEntity = TaskManager.Create(em, "GetEnergy", building, Entity.Null, true);
            var taskData = em.GetComponentData<Task>(taskEntity);
            taskData.Priority = 2;
            em.SetComponentData(taskEntity, taskData);

            taskSend = em.GetComponentData<TaskSend>(building);
            taskSend.Task = taskEntity;
            em.SetComponentData(building, taskSend);
        }
    }
}
