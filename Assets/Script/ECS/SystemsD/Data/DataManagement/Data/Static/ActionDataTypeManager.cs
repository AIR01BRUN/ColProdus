using Unity.Entities;

public static class ActionManager
{
    public static Entity Create(EntityManager em, string id, Entity task = default,Entity pawn = default)
    {
        if (em == null || string.IsNullOrEmpty(id))
            return Entity.Null;

        var entity = em.CreateEntity();
        var number = Database.GetNextNumber(DataType.Action, id);

        em.AddComponentData(entity, new ID
        {
            DataType = DataType.Action,
            Id = id,
            NumId = number,
            Name = id
        });
        em.AddComponentData(entity, new ActionLink
        {
            Pawn = pawn,
            Task = task
        });

        Database.AddInstance(DataType.Action, id, number, entity, em);
        return entity;
    }

    public static void Finish(EntityManager em, Entity action)
    {
        if (!em.Exists(action)) return;
        var actionData = em.GetComponentData<ActionLink>(action);
        if(actionData.Pawn != Entity.Null)
        {
            var currentAction = em.GetComponentData<CurrentAction>(actionData.Pawn);
            currentAction.Action = Entity.Null;
            em.SetComponentData(actionData.Pawn, currentAction);
            
        }
        if(actionData.Task != Entity.Null)
        {
            var taskData = em.GetComponentData<Task>(actionData.Task);
           
            taskData.ActualAction = Entity.Null;
            em.SetComponentData(actionData.Task, taskData);
            
        }



        var id = em.GetComponentData<ID>(action);
        Database.DeleteInstance(DataType.Action, id.Id.ToString(), id.NumId);
        em.DestroyEntity(action);
    }
}
public struct ActionLink : IComponentData
{
    public Entity Pawn;
    public Entity Task;


}
public struct CurrentAction : IComponentData
{
    public Entity Action;
}
