using Unity.Entities;
using UnityEngine;

/// <summary>Action "Sleep" : répond au besoin sommeil (restaure à 100%).</summary>
public partial struct SleepActionSystem : ISystem
{
    private const float SleepDuration = 6f;

    public void OnUpdate(ref SystemState state)
    {
        var em = state.EntityManager;
        var actions = QuerryDB.QueryInstances<Sleep>(em, DataType.Action);

        foreach (var actionEntity in actions)
        {
            var action = em.GetComponentData<Sleep>(actionEntity);
            action.Elapsed += Time.deltaTime;
            if (action.Elapsed < SleepDuration)
            {
                em.SetComponentData(actionEntity, action);
                continue;
            }

            var link = em.GetComponentData<ActionLink>(actionEntity);
            if (link.Pawn != Entity.Null && em.Exists(link.Pawn))
                PawnNeedManager.RestoreNeed(em, link.Pawn, PawnNeedManager.Sommeil, 100f);

            ActionManager.Finish(em, actionEntity);
        }
    }
}

public struct Sleep : IComponentData
{
    public float Elapsed;
}