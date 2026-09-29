using System;
using System.Collections.Generic;
using Unity.Entities;

public static class QuerryDB
{
    public static List<T> QueryDefinitions<T>(DataType? type = null, string id = null)  where T : Definition
    {
        var result = new List<T>();

        foreach (var pairType in Database.Definitions)
        {
            if (type.HasValue && pairType.Key != type.Value)
                continue;

            foreach (var pairId in pairType.Value)
            {
                if (!string.IsNullOrEmpty(id) && !string.Equals(pairId.Key, id, StringComparison.Ordinal))
                    continue;

                result.Add(pairId.Value as T);
            }
        }

        return result;
    }
    public static List<Entity> QueryInstances(DataType? type = null, string id = null, int? numId = null)
    {
        var result = new List<Entity>();

        foreach (var pairType in Database.RuntimeInstances)
        {
            if (type.HasValue && pairType.Key != type.Value)
                continue;

            foreach (var pairId in pairType.Value)
            {
                if (!string.IsNullOrEmpty(id) && !string.Equals(pairId.Key, id, StringComparison.Ordinal))
                    continue;

                foreach (var pairNumId in pairId.Value)
                {
                    if (numId.HasValue && pairNumId.Key != numId.Value)
                        continue;

                    result.Add(pairNumId.Value);
                }
            }
        }

        return result;
    }


    public static List<Entity> QueryInstances<T>(EntityManager em, DataType? type = null, string id = null, T component = default, bool checkComponentValue = false)
        where T : unmanaged, IComponentData
    {
        var result = new List<Entity>();
        if (em == null)
            return result;

        foreach (var pairType in Database.RuntimeInstances)
        {
            if (type.HasValue && pairType.Key != type.Value)
                continue;

            foreach (var pairId in pairType.Value)
            {
                if (!string.IsNullOrEmpty(id) && !string.Equals(pairId.Key, id, StringComparison.Ordinal))
                    continue;

                foreach (var entity in pairId.Value.Values)
                {
                    if (entity == Entity.Null || !em.Exists(entity) || !em.HasComponent<T>(entity))
                        continue;

                    if (!checkComponentValue || EqualityComparer<T>.Default.Equals(em.GetComponentData<T>(entity), component))
                        result.Add(entity);
                }
            }
        }

        return result;
    }

   
}
