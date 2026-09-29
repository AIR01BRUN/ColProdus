using Unity.Collections;
using Unity.Entities;

/// <summary>
/// Crée / met à jour / remplace les tâches de besoin (PawnNeedTask).
/// - Besoin < 50%  → tâche priorité 4
/// - Besoin < 35%  → priorité 3
/// - Besoin < 20%  → priorité 2 (max urgence)
/// Si le TaskSend est pris par une tâche moins prioritaire, elle est remplacée (bonus).
/// </summary>
public partial struct PawnNeedTaskSystem : ISystem
{
    private const float ThresholdStart = 0.50f; // en dessous → on crée la tâche
    private const float ThresholdPriority3 = 0.35f;
    private const float ThresholdPriority2 = 0.20f;

    public void OnUpdate(ref SystemState state)
    {
        var em = state.EntityManager;
        var pawns = QuerryDB.QueryInstances<CurrentTask>(em, DataType.Pawn);

        foreach (var pawn in pawns)
        {
            if (pawn == Entity.Null || !em.Exists(pawn) || !em.HasBuffer<PawnNeed>(pawn))
                continue;

            var needs = em.GetBuffer<PawnNeed>(pawn);

            // Besoin le plus urgent sous le seuil (nombre le plus petit = plus prioritaire).
            var hasUrgent = false;
            var bestPriority = 5;
            var bestNeedId = new FixedString32Bytes();
            foreach (var need in needs)
            {
                var pct = need.GetPercentage();
                if (pct >= ThresholdStart)
                    continue;
                hasUrgent = true;
                var priority = PriorityFor(pct);
                if (priority < bestPriority)
                {
                    bestPriority = priority;
                    bestNeedId = need.Id;
                }
            }

            var slotTaken = em.HasComponent<TaskSend>(pawn) &&
                            em.GetComponentData<TaskSend>(pawn).Task != Entity.Null;
            if (slotTaken)
            {
                var existing = em.GetComponentData<TaskSend>(pawn).Task;
                if (!em.Exists(existing) || !em.HasComponent<ID>(existing))
                    continue;

                var existingId = em.GetComponentData<ID>(existing).Id.ToString();
                if (existingId == "PawnNeedTask" && em.HasComponent<NeedTaskRef>(existing))
                {
                    var needRef = em.GetComponentData<NeedTaskRef>(existing);
                    var existingPct = PawnNeedManager.GetPercentage(em, pawn, needRef.NeedId);

                    // Besoin satisfait → on supprime la tâche puis on retombe dans la création si utile.
                    if (existingPct >= ThresholdStart)
                    {
                        TaskManager.Finish(em, existing);
                    }
                    else
                    {
                        // Mise à jour de la priorité de la tâche existante.
                        var taskData = em.GetComponentData<Task>(existing);
                        var newPriority = PriorityFor(existingPct);
                        if (taskData.Priority != newPriority)
                        {
                            taskData.Priority = newPriority;
                            em.SetComponentData(existing, taskData);
                        }

                        // Si un autre besoin est encore plus urgent, on remplace.
                        if (hasUrgent && bestPriority < taskData.Priority)
                            TaskManager.Finish(em, existing);
                        else
                            continue;
                    }
                }
                else
                {
                    // Bonus : une tâche moins prioritaire cède sa place à un besoin urgent.
                    if (!hasUrgent || !em.HasComponent<Task>(existing))
                        continue;

                    var existingPriority = em.GetComponentData<Task>(existing).Priority;
                    if (bestPriority >= existingPriority)
                        continue;
                    TaskManager.Finish(em, existing);
                }
            }
            else if (!hasUrgent)
            {
                continue;
            }

            if (!hasUrgent)
                continue;

            // Slot libre → création de la tâche de besoin.
            var task = TaskManager.Create(em, "PawnNeedTask", pawn, requiresPawn: true);
            var taskCompo = em.GetComponentData<Task>(task);
            taskCompo.Priority = bestPriority;
            taskCompo.CanBeDone = true;
            em.SetComponentData(task, taskCompo);
            em.AddComponentData(task, new NeedTaskRef { Pawn = pawn, NeedId = bestNeedId });

            var taskSend = em.GetComponentData<TaskSend>(pawn);
            taskSend.Task = task;
            em.SetComponentData(pawn, taskSend);
        }
    }

    private static int PriorityFor(float percentage)
    {
        if (percentage < ThresholdPriority2) return 2;
        if (percentage < ThresholdPriority3) return 3;
        return 4;
    }
}