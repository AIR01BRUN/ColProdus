using System.Collections.Generic;
using System.IO;
using System.Linq;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

public class WorkZoneDatabase : DatabaseBase
{
    protected override DataType DataType => DataType.WorkZone;
    protected override string Folder => "Data/WorkZone";

    public override void LoadDefinition()
    {
        var assets = Resources.LoadAll<TextAsset>(Folder);
        foreach (var asset in assets)
        {
            if (asset == null)
                continue;

            try
            {
                var json = JsonUtility.FromJson<WorkZoneDataJSON>(asset.text);
                if (json == null)
                {
                    continue;
                }

                if (string.IsNullOrEmpty(json.id))
                    json.id = asset.name;
                else
                    json.id = json.id.Trim().ToLowerInvariant();

                var mesh = MeshFactory.CreateSquareMesh(json.baseSize.ToInt2());
                var material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                Resources.Load<UnityEngine.Material>("Materials/Preview");
                material.SetColor("_BaseColor", json.color.ToColor());
                var definition = new WorkZoneDefinition
                {
                    DataType = DataType.WorkZone,
                    Id = json.id,
                    BaseSize = new int2(json.baseSize != null ? json.baseSize.x : 5, json.baseSize != null ? json.baseSize.y : 5),
                    Level = json.level,
                    WorkerAssignment = json.workerAssignment,
                    TaskAvailable = json.taskAvailable,
                    Mesh = mesh,
                    Material = material,
                };
                Database.AddDefinition(DataType.WorkZone, definition);
            }
            catch (System.Exception)
            {
            }
        }
    }

    public override void Save(EntityManager em, string saveName, string subSaveName = null)
    {
        var folder = Path.Combine(Application.persistentDataPath, "Save", saveName);
        Directory.CreateDirectory(folder);

        var fileName = string.IsNullOrEmpty(subSaveName) ? "WorkZones.json" : subSaveName + ".json";
        var path = Path.Combine(folder, fileName);

        var payload = new WorkZoneSaveFile { WorkZones = new List<WorkZoneSaveEntry>() };
        if (em == null)
        {
            File.WriteAllText(path, JsonUtility.ToJson(payload, true));
            return;
        }

        var allInstances = QuerryDB.QueryInstances(DataType.WorkZone);
        foreach (var workZoneEntity in allInstances)
        {
            if (!em.Exists(workZoneEntity) || !em.HasComponent<ID>(workZoneEntity))
                continue;

            var id = em.GetComponentData<ID>(workZoneEntity);
            var size = em.GetComponentData<Size>(workZoneEntity);
            var data = em.GetComponentData<WorkZone>(workZoneEntity);

            payload.WorkZones.Add(new WorkZoneSaveEntry
            {
                id = id.Id.ToString(),
                numId = id.NumId,
                baseSize = new Int2Json(size.Value),
                level = data.Level
            });
        }

        File.WriteAllText(path, JsonUtility.ToJson(payload, true));
    }

    public override void Load(EntityManager em, string saveName, string subSaveName = null)
    {
        var folder = Path.Combine(Application.persistentDataPath, "Save", saveName);
        var fileName = string.IsNullOrEmpty(subSaveName) ? "WorkZones.json" : subSaveName + ".json";
        var path = Path.Combine(folder, fileName);

        if (!File.Exists(path) || em == null)
            return;

        var payload = JsonUtility.FromJson<WorkZoneSaveFile>(File.ReadAllText(path));
        if (payload == null || payload.WorkZones == null)
            return;

        foreach (var workZone in payload.WorkZones)
        {
            if (workZone == null)
                continue;

            var id = string.IsNullOrEmpty(workZone.id) ? "workzone" : workZone.id;
            if (QuerryDB.QueryInstances(DataType.WorkZone, id, workZone.numId).FirstOrDefault() == Entity.Null)
            {
               
                
            }
        }
    }
}

public class WorkZoneDefinition : Definition
{
    public int2 BaseSize;
    public int Level;
    public bool WorkerAssignment;
    public bool TaskAvailable;
}

[System.Serializable]
public class WorkZoneSaveFile
{
    public List<WorkZoneSaveEntry> WorkZones = new List<WorkZoneSaveEntry>();
}

[System.Serializable]
public class WorkZoneSaveEntry
{
    public string id;
    public int numId;
    public Int2Json baseSize;
    public int level;
}
