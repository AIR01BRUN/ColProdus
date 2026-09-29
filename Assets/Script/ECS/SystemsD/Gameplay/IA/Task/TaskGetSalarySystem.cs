using Unity.Entities;

/// <summary>
/// Exécute les tâches "GetSalary" : le pawn va d'abord chercher la caisse du bâtiment
/// SalaryAdministrator (TaskManager.DoMovePawn), puis une fois sur place l'action
/// "GetSalary" (SalaryManager.GetSalary) retire les pièces dues de l'inventaire du
/// bâtiment, les donne au pawn et publie l'impact sur sa satisfaction. Si le bâtiment
/// n'existe plus (détruit, en ruine ou pas encore construit), le pawn est considéré
/// comme n'ayant rien reçu.
/// </summary>
public partial struct TaskGetSalarySystem : ISystem
{
    public void OnCreate(ref SystemState state)
    {
    }

    public void OnUpdate(ref SystemState state)
    {
        var em = state.EntityManager;
        var tasks = QuerryDB.QueryInstances<Task>(em, DataType.Task);

        foreach (var taskEntity in tasks)
        {
            if (!em.HasComponent<ID>(taskEntity) || em.GetComponentData<ID>(taskEntity).Id != SalaryManager.TaskId)
                continue;

            if (!em.HasComponent<SalaryTask>(taskEntity))
            {
                TaskManager.Finish(em, taskEntity);
                continue;
            }

            var taskData = em.GetComponentData<Task>(taskEntity);

            // Le pawn est en train de se déplacer vers le bâtiment.
            if (taskData.ActualAction != Entity.Null)
                continue;

            var pawn = taskData.Pawn;
            if (pawn == Entity.Null)
                continue;

            var salary = em.GetComponentData<SalaryTask>(taskEntity);
            if (!SalaryManager.TryGetInventory(em, salary.Building, out _))
            {
                SalaryManager.Apply(em, pawn, 0);
                TaskManager.Finish(em, taskEntity);
                continue;
            }

            // Tant que le pawn n'est pas arrivé à côté du bâtiment, on le déplace.
            if (TaskManager.DoMovePawn(em, taskEntity, salary.Building))
                continue;

            // Sur place : l'action règle la paie et publie l'effet sur la satisfaction.
            if (ActionRegistry.TryExecute(em, SalaryManager.ActionId, taskEntity))
                TaskManager.Finish(em, taskEntity);
        }
    }
}
