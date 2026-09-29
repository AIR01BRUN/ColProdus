using System.Collections.Generic;
using System.IO;
using System.Linq;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

public class RessourceDatabase : DatabaseBase
{
    protected override DataType DataType => DataType.Ressource;
    protected override string Folder => "Data/Ressources";

    public override void LoadDefinition()
    {
        
        var assets = Resources.LoadAll<TextAsset>(Folder);
        foreach (var asset in assets)
        {
            if (asset == null)
                continue;

            var json = JsonUtility.FromJson<RessourceDataJSON>(asset.text);
            if (json == null)
                continue;

            if (string.IsNullOrEmpty(json.id))
                json.id = asset.name;

                var prefabPath = string.IsNullOrEmpty(json.idGo) ? asset.name : json.idGo;
                var prefab = Resources.Load<GameObject>("Prefabs/" + prefabPath);
                var meshRenderer = prefab != null ? prefab.GetComponentInChildren<MeshRenderer>() : null;
                var meshFilter = prefab != null ? prefab.GetComponentInChildren<MeshFilter>() : null;

                var quarry = json.quarry || HasQuarryAttribute(json.attributes);
                var rarity = Mathf.Max(1, json.rarity);

                Database.AddDefinition(DataType.Ressource, new RessourceDefinition
                {
                    DataType = DataType.Ressource,
                    Id = json.id,
                    GrowthRate = json.growthRate,
                    Quarry = quarry,
                    Rarity = rarity,
                    QuarryOnly = json.quarryOnly || (quarry && rarity > 1),
                    Size = new int2(1, 1),
                    LootTable = BuildLootTable(json.lootTable),
                    Attributes = PawnAttributeRequirements.Parse(
                        StripQuarryAttribute(json.attributes), new FixedString32Bytes("VIT")),
                    Material = meshRenderer != null ? meshRenderer.sharedMaterial : null,
                    Mesh = meshFilter != null ? meshFilter.sharedMesh : null
                });
        }
    }

    public override void Save(EntityManager em, string saveName, string subSaveName = null)
    {
        var folder = Path.Combine(Application.persistentDataPath, "Save", saveName);
        Directory.CreateDirectory(folder);

        var fileName = string.IsNullOrEmpty(subSaveName) ? "Ressources.json" : subSaveName + ".json";
        var path = Path.Combine(folder, fileName);

        var file = new RessourceSaveFile();
        if (em == null)
        {
            File.WriteAllText(path, JsonUtility.ToJson(file, true));
            return;
        }

        var allInstances = QuerryDB.QueryInstances(DataType.Ressource);
        foreach (var resourceEntity in allInstances)
        {

                if (!em.Exists(resourceEntity) || !em.HasComponent<ID>(resourceEntity))
                    continue;

                var id = em.GetComponentData<ID>(resourceEntity);
                var save = new RessourceSaveJSON
                {
                    id = id.Id.ToString(),
                    numId = id.NumId,
                    boardNumId = -1,
                    x = -1,
                    y = -1,
                    rotation = 0,
                    currentStep = 0,
                    ptsWorkActual = 0f,
                    activation = false,
                    currentLoop = -1
                };

                if (em.HasComponent<ProcedureState>(resourceEntity))
                {
                    var procedureState = em.GetComponentData<ProcedureState>(resourceEntity);
                    save.currentStep = procedureState.CurrentStep;
                    save.ptsWorkActual = procedureState.PtsWorkActual;
                    save.activation = procedureState.Activation;
                }

                if (em.HasComponent<OnBoard>(resourceEntity))
                {
                    var onBoard = em.GetComponentData<OnBoard>(resourceEntity);
                    if (em.HasComponent<ID>(onBoard.Board))
                    {
                        save.boardNumId = em.GetComponentData<ID>(onBoard.Board).NumId;
                        save.x = onBoard.Position.x;
                        save.y = onBoard.Position.y;
                        save.rotation = onBoard.Rotation;
                    }
                }

                file.Ressources.Add(save);
            
        }

        File.WriteAllText(path, JsonUtility.ToJson(file, true));
    }

    public override void Load(EntityManager em, string saveName, string subSaveName = null)
    {
        var folder = Path.Combine(Application.persistentDataPath, "Save", saveName);
        var fileName = string.IsNullOrEmpty(subSaveName) ? "Ressources.json" : subSaveName + ".json";
        var path = Path.Combine(folder, fileName);

        if (!File.Exists(path) || em == null)
            return;

        var payload = JsonUtility.FromJson<RessourceSaveFile>(File.ReadAllText(path));
        if (payload == null || payload.Ressources == null)
            return;

        foreach (var resource in payload.Ressources)
        {
            if (resource == null)
                continue;

            var id = string.IsNullOrEmpty(resource.id) ? "ressource" : resource.id;
            var def = QuerryDB.QueryDefinitions<RessourceDefinition>(DataType.Ressource, id);
            if (def != null)
            {
                var newRessource = RessourceManager.Create(em, id);
                var board = QuerryDB.QueryInstances(DataType.Board, "board", resource.boardNumId).FirstOrDefault();
                BoardManager.AddOn(em, newRessource, board, new int2(resource.x, resource.y), resource.rotation, true);
            }
        }
    }

    private static bool HasQuarryAttribute(string[] attributes)
    {
        if (attributes == null)
            return false;

        foreach (var attribute in attributes)
        {
            if (!string.IsNullOrEmpty(attribute) && attribute.Trim().Equals("quarry", System.StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    private static string[] StripQuarryAttribute(string[] attributes)
    {
        if (attributes == null)
            return null;

        var result = new List<string>();
        foreach (var attribute in attributes)
        {
            if (string.IsNullOrEmpty(attribute) || attribute.Trim().Equals("quarry", System.StringComparison.OrdinalIgnoreCase))
                continue;

            result.Add(attribute);
        }

        return result.ToArray();
    }

    private static List<Items> BuildLootTable(LootTableRessource[] lootTable)
    {
        var result = new List<Items>();
        if (lootTable == null)
            return result;

        foreach (var loot in lootTable)
        {
            if (loot == null)
                continue;

            result.Add(new Items(loot.idItem, loot.quantity));
        }

        return result;
    }
}

public class RessourceDefinition : Definition
{
    public int2 Size;
    public float GrowthRate;
    public bool Quarry;
    public int Rarity = 1;
    public bool QuarryOnly;
    public List<Items> LootTable = new List<Items>();
    public List<FixedString32Bytes> Attributes = new List<FixedString32Bytes>();
}
[System.Serializable]
public class RessourceSaveJSON : IdJson
{
    public int x;
    public int y;
    public int rotation;
    public int boardNumId;
    public int currentStep;
    public float ptsWorkActual;
    public WorkerSave worker;
    public bool activation;
    public int currentLoop;
}

[System.Serializable]
public class RessourceSaveFile
{
    public List<RessourceSaveJSON> Ressources = new();
}

