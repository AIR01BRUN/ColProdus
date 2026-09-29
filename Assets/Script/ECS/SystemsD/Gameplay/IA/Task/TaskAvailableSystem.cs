using Unity.Entities;

/// <summary>
/// Remplit le buffer TaskAvailable de chaque checkroom avec les tâches à faire,
/// pour donner un sens d'exécution aux pawns (ordre d'envoi : les plus anciennes
/// d'abord, à priorité égale). Routage des TaskSend :
///   - bâtiment dans la même zone => checkroom de la zone
///   - bâtiment InventoryStock (id "inventory") => checkroom du camp
///   - ressource liée à une zone (WorkZoneLink) => checkroom de cette zone
///   - ressource sans zone (arbre) => scierie si disponible, sinon camp
/// Les tâches d'énergie ("GetEnergy") passent par le TaskSend du bâtiment : elles sont
/// donc routées comme les bâtiments, vers le checkroom de leur zone.
/// Une tâche n'est publiée que si son TaskSend est non null, l'entité task existe
/// et existe, et la tâche n'a pas le tag Submit (déjà prise).
/// </summary>
public partial struct TaskAvailableSystem : ISystem
{
    public void OnCreate(ref SystemState state)
    {
    }

    public void OnUpdate(ref SystemState state)
    {
        var em = state.EntityManager;

        // Vide le buffer de toutes les checkrooms avant de les re-remplir.
        var checkrooms = QuerryDB.QueryInstances(DataType.Building, "checkroom");
        foreach (var checkroom in checkrooms)
        {
            if (em.Exists(checkroom) && em.HasBuffer<TaskAvailable>(checkroom))
                em.GetBuffer<TaskAvailable>(checkroom).Clear();
        }

        Entity campCheckroom = Entity.Null;
        if (WorkZoneManager.TryGetCamp(out var campZone))
            WorkZoneManager.TryGetBuildingWorker(em, campZone, out campCheckroom);

        // Scierie avec checkroom : elle capte les tâches concernant les arbres.
        Entity sawmillCheckroom = Entity.Null;
        foreach (var zone in QuerryDB.QueryInstances(DataType.WorkZone, "sawmill"))
        {
            if (WorkZoneManager.TryGetBuildingWorker(em, zone, out var checkroom))
            {
                sawmillCheckroom = checkroom;
                break;
            }
        }

        // Ressources => checkroom de la zone du WorkZoneLink ; sinon la scierie pour les
        // arbres ; sinon le checkroom du camp par défaut.
        var resources = QuerryDB.QueryInstances(DataType.Ressource);
        foreach (var resource in resources)
        {
            Entity target = Entity.Null;
            if (em.HasComponent<WorkZoneLink>(resource))
            {
                var zone = em.GetComponentData<WorkZoneLink>(resource).WorkZone;
                if (zone != Entity.Null && em.Exists(zone))
                    WorkZoneManager.TryGetBuildingWorker(em, zone, out target);
            }

            if (target == Entity.Null && sawmillCheckroom != Entity.Null && em.HasComponent<ID>(resource) &&
                em.GetComponentData<ID>(resource).Id == "tree")
            {
                target = sawmillCheckroom;
            }

            if (target == Entity.Null)
                target = campCheckroom;

            if (target == Entity.Null || !em.Exists(target))
                continue;
            AddTask(em, target, resource);
        }

        // Bâtiments : même zone => checkroom de la zone ; inventory => camp.
        var buildings = QuerryDB.QueryInstances(DataType.Building);
        foreach (var building in buildings)
        {
            if (!em.Exists(building) || !em.HasComponent<TaskSend>(building))
                continue;

            Entity target = Entity.Null;
            if (em.HasComponent<WorkZoneLink>(building))
            {
                var zone = em.GetComponentData<WorkZoneLink>(building).WorkZone;
                if (zone != Entity.Null && em.Exists(zone))
                    WorkZoneManager.TryGetBuildingWorker(em, zone, out target);
            }

            if (em.HasComponent<ID>(building) &&
                em.GetComponentData<ID>(building).Id == "inventory")
            {
                target = campCheckroom;
            }

            if (target == Entity.Null || !em.Exists(target) || !em.HasBuffer<TaskAvailable>(target))
                continue;

            AddTask(em, target, building);
        }
    }

    private static void AddTask(EntityManager em, Entity checkroom, Entity sender)
    {
        if (sender == Entity.Null || !em.Exists(sender) || !em.HasComponent<TaskSend>(sender))
            return;

        var task = em.GetComponentData<TaskSend>(sender).Task;
        AddTaskEntity(em, checkroom, task);
    }

    private static void AddTaskEntity(EntityManager em, Entity checkroom, Entity task)
    {
        if (checkroom == Entity.Null || !em.Exists(checkroom) || !em.HasBuffer<TaskAvailable>(checkroom))
            return;

        if (task == Entity.Null || !em.Exists(task) || !em.HasComponent<Task>(task))
            return;

        // Tâche déjà prise par un pawn : on ne la publie pas.
        if (em.HasComponent<Submit>(task))
            return;

        var buffer = em.GetBuffer<TaskAvailable>(checkroom);
        // Pas de doublon : la même tâche ne doit apparaître qu'une fois.
        for (int i = 0; i < buffer.Length; i++)
        {
            if (buffer[i].Task == task)
                return;
        }

        buffer.Add(new TaskAvailable { Task = task });
    }
}