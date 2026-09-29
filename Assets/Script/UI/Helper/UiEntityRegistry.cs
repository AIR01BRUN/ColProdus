using System;
using System.Collections.Generic;
using Unity.Entities;

public static class UiEntityRegistry
{
    private static readonly Dictionary<Entity, List<TemplateUI>> _map = new Dictionary<Entity, List<TemplateUI>>();
    private static readonly Dictionary<Type, object> _singletons = new Dictionary<Type, object>();
   
    public static TemplateUI GetTemplate(Entity key)
    {
        if (_map.TryGetValue(key, out var list) && list.Count > 0)
        {
            return list[0];
        }
        return null;
    }

    public static void RegisterSingleton<T>(T instance) where T : class
    {
        if (instance == null) return;
        _singletons[typeof(T)] = instance;
    }

    public static T GetSingleton<T>() where T : class
    {
        if (_singletons.TryGetValue(typeof(T), out var inst)) return inst as T;
        return null;
    }

    public static void Register(Entity key, TemplateUI template)
    {
        if (template == null) return;
        if (!_map.TryGetValue(key, out var list))
        {
            list = new List<TemplateUI>();
            _map[key] = list;
        }

        if (!list.Contains(template)) list.Add(template);
    }
    


    

    public static void Unregister(Entity key, TemplateUI template)
    {
        if (template == null) return;
        if (_map.TryGetValue(key, out var list))
        {
            list.Remove(template);
            if (list.Count == 0) _map.Remove(key);
        }
    }


    public static void Notify(Entity key)
    {
        if (!_map.TryGetValue(key, out var list)) return;

        for (int i = 0; i < list.Count; i++)
        {
            try
            {
                list[i]?.Refresh();
            }
            catch
            {
                // ignore refresh errors
            }
        }
    }

    

   
}
