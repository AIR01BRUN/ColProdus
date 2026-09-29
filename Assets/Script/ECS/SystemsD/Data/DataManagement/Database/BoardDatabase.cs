using System.Collections.Generic;
using System.IO;
using System.Linq;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

public class BoardDatabase : DatabaseBase
{
    protected override DataType DataType => DataType.Board;
    protected override string Folder => "Data/Boards";

    public override void LoadDefinition()
    {

        var assets = Resources.LoadAll<TextAsset>(Folder);
        foreach (var asset in assets)
        {
            if (asset == null)
                continue;

            var json = JsonUtility.FromJson<BoardSaveJSON>(asset.text);
            if (json == null)
                continue;

            if (string.IsNullOrEmpty(json.id))
                json.id = asset.name;

            var prefabPath = string.IsNullOrEmpty(json.id) ? asset.name : json.id;
            var prefab = Resources.Load<GameObject>("Prefabs/" + prefabPath);
            var meshRenderer = prefab != null ? prefab.GetComponentInChildren<MeshRenderer>() : null;
            var meshFilter = prefab != null ? prefab.GetComponentInChildren<MeshFilter>() : null;

            Database.AddDefinition(DataType.Board, new BoardDefinition
            {
                DataType = DataType.Board,
                Id = json.id,
                Size = new int2(1, 1),
                Material = meshRenderer != null ? meshRenderer.sharedMaterial : null,
                Mesh = meshFilter != null ? meshFilter.sharedMesh : null
            });
        }
    }

    public override void Save(EntityManager em, string saveName, string subSaveName = null)
    {
        var folder = Path.Combine(Application.persistentDataPath, "Save", saveName);
        Directory.CreateDirectory(folder);

        var fileName = string.IsNullOrEmpty(subSaveName) ? "Board.json" : subSaveName + ".json";
        var path = Path.Combine(folder, fileName);

        var payload = new BoardSaveFolder { folder = new List<BoardSaveJSON>() };
        if (em == null)
        {
            File.WriteAllText(path, JsonUtility.ToJson(payload, true));
            return;
        }

        var allInstances = QuerryDB.QueryInstances(DataType.Board);
        foreach (var entity in allInstances)
        {
           
                if (entity == Entity.Null || !em.Exists(entity) || !em.HasComponent<ID>(entity) || !em.HasComponent<BoardSize>(entity))
                    continue;

                var id = em.GetComponentData<ID>(entity);
                var size = em.GetComponentData<BoardSize>(entity).GridSize;
                payload.folder.Add(new BoardSaveJSON
                {
                    id = id.Id.ToString(),
                    numId = id.NumId,
                    size = new Int2Json(size)
                });
            
        }

        File.WriteAllText(path, JsonUtility.ToJson(payload, true));
    }

    public override void Load(EntityManager em, string saveName, string subSaveName = null)
    {
        var folder = Path.Combine(Application.persistentDataPath, "Save", saveName);
        var fileName = string.IsNullOrEmpty(subSaveName) ? "Board.json" : subSaveName + ".json";
        var path = Path.Combine(folder, fileName);

        if (!File.Exists(path) || em == null)
            return;

        var payload = JsonUtility.FromJson<BoardSaveFolder>(File.ReadAllText(path));
        if (payload == null || payload.folder == null)
            return;

        foreach (var data in payload.folder)
        {
            if (data == null)
                continue;

            var id = string.IsNullOrEmpty(data.id) ? "board" : data.id;
            var size = data.size != null ? new int2(data.size.x, data.size.y) : new int2(1, 1);
            if (QuerryDB.QueryInstances(DataType.Board, id, data.numId).FirstOrDefault() == Entity.Null)
            {
                BoardManager.Create(em, id, size);
            }
        }
    }
}

public class BoardDefinition : Definition
{
    public int2 Size;
}

