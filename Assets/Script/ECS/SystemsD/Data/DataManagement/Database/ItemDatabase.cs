using System.Collections.Generic;
using System.IO;
using Unity.Entities;
using UnityEngine;

public class ItemDatabase : DatabaseBase
{
    protected override DataType DataType => DataType.Item;
    protected override string Folder => "Data/Items";

    public override void LoadDefinition()
    {

        var assets = Resources.LoadAll<TextAsset>(Folder);
        foreach (var asset in assets)
        {
            if (asset == null)
                continue;

            var json = JsonUtility.FromJson<ItemDataJSON>(asset.text);
            if (json == null)
                continue;

            if (string.IsNullOrEmpty(json.id))
                json.id = asset.name;

            var prefabPath = string.IsNullOrEmpty(json.idGo) ? asset.name : json.idGo;
            var prefab = Resources.Load<GameObject>("Prefabs/" + prefabPath);
            var meshRenderer = prefab != null ? prefab.GetComponentInChildren<MeshRenderer>() : null;
            var meshFilter = prefab != null ? prefab.GetComponentInChildren<MeshFilter>() : null;

            Database.AddDefinition(DataType.Item, new ItemDefinition
            {
                DataType = DataType.Item,
                Id = json.id,
                MaxStack = json.maxStack,
                Type = string.IsNullOrWhiteSpace(json.type) ? "misc" : json.type.Trim().ToLowerInvariant(),
                SubType = string.IsNullOrWhiteSpace(json.subType) ? null : json.subType.Trim().ToLowerInvariant(),
                Nutrition = json.nutrition,
                Value = json.value,
                EnergyId = string.IsNullOrWhiteSpace(json.energyId) ? null : json.energyId.Trim(),
                EnergyValue = json.energyValue,
                Material = meshRenderer != null ? meshRenderer.sharedMaterial : null,
                Mesh = meshFilter != null ? meshFilter.sharedMesh : null
            });
        }
    }

    public override void Save(EntityManager em, string saveName, string subSaveName = null)
    {
        var folder = Path.Combine(Application.persistentDataPath, "Save", saveName);
        Directory.CreateDirectory(folder);
        var fileName = string.IsNullOrEmpty(subSaveName) ? "Items.json" : subSaveName + ".json";
        var path = Path.Combine(folder, fileName);
        File.WriteAllText(path, JsonUtility.ToJson(new ItemSaveFile(), true));
    }

    public override void Load(EntityManager em, string saveName, string subSaveName = null)
    {
        var folder = Path.Combine(Application.persistentDataPath, "Save", saveName);
        var fileName = string.IsNullOrEmpty(subSaveName) ? "Items.json" : subSaveName + ".json";
        var path = Path.Combine(folder, fileName);

        if (!File.Exists(path))
            return;

        var payload = JsonUtility.FromJson<ItemSaveFile>(File.ReadAllText(path));
        if (payload == null)
            return;
    }
}

public class ItemDefinition : Definition
{
    public int MaxStack;
    public string Type;
    public string SubType;
    public float Nutrition;
    public int Value;
    public string EnergyId;
    public int EnergyValue;
}

[System.Serializable]
public class ItemSaveFile
{
    public List<ItemSaveEntry> Items = new List<ItemSaveEntry>();
}

[System.Serializable]
public class ItemSaveEntry
{
    public string id;
    public int numId;
}

