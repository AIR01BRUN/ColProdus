using Unity.Entities;

/// <summary>
/// Petits helpers partagés par les templates du panneau de détail d'objet.
/// </summary>
public static class DetailUiUtility
{
    public static string GetEntityName(EntityManager em, Entity entity)
    {
        if (entity == Entity.Null || !em.Exists(entity))
            return "none";

        if (em.HasComponent<ID>(entity))
        {
            var id = em.GetComponentData<ID>(entity);
            return $"{id.Id} {id.NumId}";
        }
        return $"Entity {entity.Index}";
    }

    public static string GetTaskName(EntityManager em, Entity task)
    {
        if (task == Entity.Null || !em.Exists(task))
            return "none";
        if (em.HasComponent<ID>(task))
            return em.GetComponentData<ID>(task).Id.ToString();
        return $"Task {task.Index}";
    }

    public static string DescribeTask(EntityManager em, Entity task)
    {
        if (task == Entity.Null || !em.Exists(task) || !em.HasComponent<Task>(task))
            return "no task";

        var taskData = em.GetComponentData<Task>(task);
        var name = GetTaskName(em, task);
        var target = GetEntityName(em, taskData.Object);
        var pawn = taskData.Pawn == Entity.Null ? "free" : GetEntityName(em, taskData.Pawn);
        return $"{name} [P{taskData.Priority}] -> {target} | {pawn}";
    }

    public static Entity GetWorkingPawn(EntityManager em, Entity procedure)
    {
        if (procedure == Entity.Null || !em.Exists(procedure) || !em.HasComponent<TaskSend>(procedure))
            return Entity.Null;

        var task = em.GetComponentData<TaskSend>(procedure).Task;
        if (task == Entity.Null || !em.Exists(task) || !em.HasComponent<Task>(task))
            return Entity.Null;

        return em.GetComponentData<Task>(task).Pawn;
    }
}