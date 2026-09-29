using System;
using System.Collections.Generic;
using Unity.Entities;

public delegate bool ConditionHandler(EntityManager em, Entity targetEntity);

public static class ConditionRegistry
{
    private static readonly Dictionary<string, ConditionHandler> Conditions = new Dictionary<string, ConditionHandler>(StringComparer.OrdinalIgnoreCase)
    {
        ["Existe"] = Exists,
        ["NoExiste"] = NoExists,
        ["IsNextDay"] = IsNextDay
    };

    public static void RegisterCondition(string conditionId, ConditionHandler condition)
    {
        if (string.IsNullOrWhiteSpace(conditionId) || condition == null)
            return;

        Conditions[conditionId] = condition;
    }

    public static bool CheckCondition(EntityManager em, Entity targetEntity, string conditionId)
    {
        if (string.IsNullOrWhiteSpace(conditionId))
            return true;

        if (!Conditions.TryGetValue(conditionId, out var condition))
            return false;

        return condition(em, targetEntity);
    }

    private static bool Exists(EntityManager em, Entity targetEntity)
    {
        return em != null && targetEntity != Entity.Null && em.Exists(targetEntity);
    }private static bool NoExists(EntityManager em, Entity targetEntity)
    {
        return !Exists(em, targetEntity);
    }

    /// <summary>
    /// Condition "IsNextDay" : vraie quand une nouvelle journée a commencé
    /// (il est 00:00:00, l'heure du monde est remise à 0).
    /// </summary>
    private static bool IsNextDay(EntityManager em, Entity targetEntity)
    {
        if (em == null || !em.World.IsCreated)
            return false;

        using (var query = em.CreateEntityQuery(ComponentType.ReadOnly<WorldTime>()))
        {
            if (query.IsEmptyIgnoreFilter)
                return false;

            var worldTime = query.GetSingleton<WorldTime>();
            return worldTime.Hour == 0 && worldTime.Minute == 0;
        }
    }

}