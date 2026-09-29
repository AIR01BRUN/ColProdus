
using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;

public static class Database
{
    public static readonly Dictionary<DataType, Dictionary<string, Definition>> Definitions = new Dictionary<DataType, Dictionary<string, Definition>>();
    public static readonly Dictionary<DataType, Dictionary<string, Dictionary<int, Entity>>> RuntimeInstances = new Dictionary<DataType, Dictionary<string, Dictionary<int, Entity>>>();

    public static string DataFolder = "Data";
    public static string SaveFolder => "Save";

    public static void AddDefinition(DataType type, Definition definition)
    {
        if (definition == null || string.IsNullOrEmpty(definition.Id))
            return;

        if (!Definitions.ContainsKey(type))
            Definitions[type] = new Dictionary<string, Definition>();

        Definitions[type][definition.Id] = definition;
    }

 
    public static bool DeleteDefinition(DataType type, string id)
    {
        if (string.IsNullOrEmpty(id))
            return false;

        if (!Definitions.TryGetValue(type, out var byId))
            return false;

        return byId.Remove(id);
    }

    public static void AddInstance(DataType type, string id, int number, Entity entity, EntityManager em)
    {
        if (string.IsNullOrEmpty(id) || entity == Entity.Null)
            return;

        if (!RuntimeInstances.ContainsKey(type))
            RuntimeInstances[type] = new Dictionary<string, Dictionary<int, Entity>>();

        if (!RuntimeInstances[type].ContainsKey(id))
            RuntimeInstances[type][id] = new Dictionary<int, Entity>();

        em.AddComponentData(entity, new ID
        {
            DataType = type,
            Id = id,
            NumId = number,
            Name = id
        });
        em.SetName(entity, $"{type}_{id}_{number}");
        RuntimeInstances[type][id][number] = entity;
    }

    private static Dictionary<int, Entity> GetInstancesByTypeAndId(DataType type, string id)
    {
        if (string.IsNullOrEmpty(id))
            return new Dictionary<int, Entity>();

        if (!RuntimeInstances.TryGetValue(type, out var byId))
            return new Dictionary<int, Entity>();

        if (!byId.TryGetValue(id, out var byNumber))
            return new Dictionary<int, Entity>();

        return byNumber;
    }
    public static int GetNextNumber(DataType type, string id)
    {
        if (string.IsNullOrEmpty(id))
            return 0;

        var byNumber = GetInstancesByTypeAndId(type, id);
        for (int i = 0; i < int.MaxValue; i++)
        {
            
            if (!byNumber.ContainsKey(i))
                return i;
        }

        return byNumber.Count;
    }

    public static void SaveInstance(EntityManager em, string saveName, string subSaveName = null)
    {
        new BoardDatabase().Save(em, saveName, subSaveName);
        new BuildingDatabase().Save(em, saveName, subSaveName);
        new PawnDatabase().Save(em, saveName, subSaveName);
        new ItemDatabase().Save(em, saveName, subSaveName);
        new RessourceDatabase().Save(em, saveName, subSaveName);
        new WorkZoneDatabase().Save(em, saveName, subSaveName);
       
    }

    public static void LoadInstance(EntityManager em, string saveName, string subSaveName = null)
    {
        new BoardDatabase().Load(em, saveName, subSaveName);
        new BuildingDatabase().Load(em, saveName, subSaveName);
        new PawnDatabase().Load(em, saveName, subSaveName);
        new ItemDatabase().Load(em, saveName, subSaveName);
        new RessourceDatabase().Load(em, saveName, subSaveName);
        new WorkZoneDatabase().Load(em, saveName, subSaveName);
    }
    public static void ClearAllInstances(EntityManager em)
    {
        foreach (var byType in RuntimeInstances.Values)
        {
            foreach (var byId in byType.Values)
            {
                foreach (var entity in byId.Values)
                {
                    if (entity != Entity.Null && em.Exists(entity))
                        em.DestroyEntity(entity);
                }
            }
        }

        RuntimeInstances.Clear();
    }

    public static bool DeleteInstance(DataType type, string id, int number)
    {
        if (string.IsNullOrEmpty(id))
            return false;

        if (!RuntimeInstances.TryGetValue(type, out var byId))
            return false;

        if (!byId.TryGetValue(id, out var byNumber))
            return false;

        if (!byNumber.TryGetValue(number, out var entity))
            return false;

        byNumber.Remove(number);
        if (byNumber.Count == 0)
            byId.Remove(id);

        if (byId.Count == 0)
            RuntimeInstances.Remove(type);

        return entity != Entity.Null;
    }

    public static bool DeleteInstance(EntityManager em, Entity entity)
    {
        if (em == null || entity == Entity.Null || !em.Exists(entity) || !em.HasComponent<ID>(entity))
            return false;

        var id = em.GetComponentData<ID>(entity);
        return DeleteInstance(id.DataType, id.Id.ToString(), id.NumId);
    }
}

[Serializable]
public class Definition
{
    public DataType DataType;
    public string Id;

    public Material Material;
    public Mesh Mesh;
}

[Serializable]
public class RecipeDefinition : Definition
{
    public string BuildingId;
    public string Name;
    public bool NeedEnergy;
    public List<RecipeStepDefinition> Steps = new List<RecipeStepDefinition>();
}



public struct ID :  IComponentData
{
    //Exemple ID : pawn_3
    public DataType DataType;
    public FixedString64Bytes Id; // pawn
    public int NumId; // 3
    public FixedString64Bytes Name;

    public Definition ToDefinition()
    {
        return new Definition{ DataType = DataType, Id = Id.ToString()};
    }
}




public enum DataType 
{
    Board,
    Building ,
    Pawn,
    Item,
    Ressource,
    Action,
    Task,
    Inventory,
    Recipe,
    Attribute,
    WorkZone,
    // ajoute ce que tu veux ici
}
