using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

public class PawnDatabase : DatabaseBase
{
    protected override DataType DataType => DataType.Pawn;
    protected override string Folder => "Data/Pawns";

    public override void LoadDefinition()
    {
        var prefab = Resources.Load<GameObject>("Prefabs/pawn");
        var meshRenderer = prefab != null ? prefab.GetComponentInChildren<MeshRenderer>() : null;
        var meshFilter = prefab != null ? prefab.GetComponentInChildren<MeshFilter>() : null;

        Database.AddDefinition(DataType.Pawn, new PawnDefinition
        {
            DataType = DataType.Pawn,
            Id = "pawn",
            Name = "pawn",
            MaxInventorySlots = 3,
            Material = meshRenderer != null ? meshRenderer.sharedMaterial : null,
            Mesh = meshFilter != null ? meshFilter.sharedMesh : null,
        });
    }

    public override void Save(EntityManager em, string saveName, string subSaveName = null)
    {
        var folder = Path.Combine(Application.persistentDataPath, "Save", saveName);
        Directory.CreateDirectory(folder);

        var fileName = string.IsNullOrEmpty(subSaveName) ? "Pawns.json" : subSaveName + ".json";
        var path = Path.Combine(folder, fileName);

        var file = new PawnSaveFile();
        if (em == null)
        {
            File.WriteAllText(path, JsonUtility.ToJson(file, true));
            return;
        }

        var allInstances = QuerryDB.QueryInstances(DataType.Pawn);
        foreach (var pawnEntity in allInstances)
        {
            
                if (!em.Exists(pawnEntity) || !em.HasComponent<ID>(pawnEntity))
                    continue;

                var id = em.GetComponentData<ID>(pawnEntity);
                var pawn = new PawnSaveJSON
                {
                    id = id.Id.ToString(),
                    numId = id.NumId,
                    boardNumId = -1,
                    x = -1,
                    y = -1,
                    rotation = -1
                };

                if (em.HasComponent<OnBoard>(pawnEntity))
                {
                    var onBoard = em.GetComponentData<OnBoard>(pawnEntity);
                    if (em.HasComponent<ID>(onBoard.Board))
                    {
                        var boardId = em.GetComponentData<ID>(onBoard.Board);
                        pawn.boardNumId = boardId.NumId;
                        pawn.x = onBoard.Position.x;
                        pawn.y = onBoard.Position.y;
                        pawn.rotation = onBoard.Rotation;
                    }
                }

                file.Pawns.Add(pawn);
            
        }

        File.WriteAllText(path, JsonUtility.ToJson(file, true));
    }

    public override void Load(EntityManager em, string saveName, string subSaveName = null)
    {
        var folder = Path.Combine(Application.persistentDataPath, "Save", saveName);
        var fileName = string.IsNullOrEmpty(subSaveName) ? "Pawns.json" : subSaveName + ".json";
        var path = Path.Combine(folder, fileName);

        if (!File.Exists(path) || em == null)
            return;

        var payload = JsonUtility.FromJson<PawnSaveFile>(File.ReadAllText(path));
        if (payload == null || payload.Pawns == null)
            return;

        foreach (var pawn in payload.Pawns)
        {
            if (pawn == null)
                continue;

            var id = string.IsNullOrEmpty(pawn.id) ? "pawn" : pawn.id;
            if (QuerryDB.QueryInstances(DataType.Pawn, id, pawn.numId).FirstOrDefault() == Entity.Null)
            {
                var entity = PawnManager.Create(em);
                if (pawn.boardNumId >= 0)
                {
                    var board = QuerryDB.QueryInstances(DataType.Board, "board", pawn.boardNumId).FirstOrDefault();
                    if (board != Entity.Null)
                    {
                        BoardManager.AddOn(em, entity, board, new int2(pawn.x, pawn.y), pawn.rotation, true);
                    }
                }
            }
        }
    }
}
public class PawnDefinition : Definition
{
    public string Name;
    public int MaxInventorySlots;
   
}

[Serializable]
public class PawnSaveJSON
{
    public string id;
    public int numId;
    public int x;
    public int y;
    public int rotation;
    public int boardNumId;
}

[Serializable]
public class PawnSaveFile
{
    public List<PawnSaveJSON> Pawns = new();
}

