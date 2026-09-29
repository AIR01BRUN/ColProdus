using System.Collections.Generic;
using System.IO;
using System.Linq;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

public class BuildingDatabase : DatabaseBase
{
    public bool IsInitialized { get; private set; }

    protected override DataType DataType => DataType.Building;
    protected override string Folder => "Data/Buildings";

    public void Initialize()
    {
        if (IsInitialized)
            return;

        LoadDefinition();
        IsInitialized = true;
    }

    public override void LoadDefinition()
    {

        var assets = Resources.LoadAll<TextAsset>(Folder);
        foreach (var asset in assets)
        {
            if (asset == null)
                continue;

            try
            {
                var json = JsonUtility.FromJson<BuildingDataJSON>(asset.text);
                if (json == null)
                {
                    continue;
                }

                if (string.IsNullOrEmpty(json.id))
                    json.id = asset.name;
                else
                    json.id = json.id.Trim().ToLowerInvariant();

                var prefabPath = string.IsNullOrEmpty(json.idGo) ? asset.name : json.idGo;
                var prefab = Resources.Load<GameObject>("Prefabs/" + prefabPath);
                var meshRenderer = prefab != null ? prefab.GetComponentInChildren<MeshRenderer>() : null;
                var meshFilter = prefab != null ? prefab.GetComponentInChildren<MeshFilter>() : null;

                var definition = new BuildingDefinition
                {
                    DataType = DataType.Building,
                    Id = json.id,
                    IdGo = json.idGo,
                    Name = json.id,
                    Size = new int2(json.size != null ? json.size.x : 1, json.size != null ? json.size.y : 1),
                    PointsConstructionNeed = json.ptsConstructionNeed,
                    Worker = json.worker,
                    Inventory = json.inventory,
                    Deco = json.deco,
                    WorkZoneIds = json.workZone != null ? new List<string>(json.workZone) : new List<string>(),
                    RequiredItems = BuildRequirements(json.itemRequis),
                    Attributes = PawnAttributeRequirements.Parse(json.attributes, new FixedString32Bytes("STR")),
                    MineLevels = BuildMineLevels(json.mineLevels),
                    EnergyId = string.IsNullOrWhiteSpace(json.energyId) ? null : json.energyId.Trim(),
                    EnergyMax = json.energyMax,
                    EnergyConsume = json.energyConsume,
                    EnergyProduct = json.energyProduct,
                    Material = meshRenderer != null ? meshRenderer.sharedMaterial : null,
                    Mesh = meshFilter != null ? meshFilter.sharedMesh : null
                };
                Database.AddDefinition(DataType.Building, definition);
               
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

        var fileName = string.IsNullOrEmpty(subSaveName) ? "Build.json" : subSaveName + ".json";
        var path = Path.Combine(folder, fileName);

        var payload = new BuildingSaveFolder { folder = new List<BuildingSaveJSON>() };
        if (em == null)
        {
            File.WriteAllText(path, JsonUtility.ToJson(payload, true));
            return;
        }

        var allInstances = QuerryDB.QueryInstances(DataType.Building);
        foreach (var buildingEntity in allInstances)
        {
           
                if (!em.Exists(buildingEntity) || !em.HasComponent<ID>(buildingEntity))
                    continue;

                var id = em.GetComponentData<ID>(buildingEntity);
                float ptsConstruction = -1f;
                var itemRequis = new List<ItemQuantityJson>();

                if (em.HasComponent<UnderConstruction>(buildingEntity))
                {
                    ptsConstruction = em.GetComponentData<UnderConstruction>(buildingEntity).PtsWorkActual;
                    var items = em.GetBuffer<UnderConstructionItem>(buildingEntity);
                    foreach (var item in items)
                    {
                        if (!em.HasComponent<ID>(item.Item))
                            continue;

                        var itemId = em.GetComponentData<ID>(item.Item);
                        itemRequis.Add(new ItemQuantityJson { id = itemId.Id.ToString(), quantity = item.QuantityCurrent });
                    }
                }

                var position = new PositionOnBoardJSON();
                if (em.HasComponent<OnBoard>(buildingEntity))
                {
                    var onBoard = em.GetComponentData<OnBoard>(buildingEntity);
                    if (em.HasComponent<ID>(onBoard.Board))
                    {
                        var boardId = em.GetComponentData<ID>(onBoard.Board);
                        position = new PositionOnBoardJSON
                        {
                            idNumBoard = boardId.NumId,
                            position = new Int2Json(onBoard.Position),
                            rotation = onBoard.Rotation
                        };
                    }
                }

                var workers = new List<WorkerSave>();
                if (em.HasBuffer<WorkerAssignment>(buildingEntity))
                {
                    var assignments = em.GetBuffer<WorkerAssignment>(buildingEntity);
                    for (int j = 0; j < assignments.Length; j++)
                    {
                        var worker = assignments[j].Pawn;
                        if (worker == Entity.Null || !em.Exists(worker) || !em.HasComponent<ID>(worker))
                            continue;

                        var workerId = em.GetComponentData<ID>(worker);
                        workers.Add(new WorkerSave { id = workerId.Id.ToString(), idNum = workerId.NumId, numPlacement = j });
                    }
                }

                var mineDeepestLevel = 0;
                var mineLastResourceId = string.Empty;
                if (em.HasComponent<MineState>(buildingEntity))
                {
                    var mineState = em.GetComponentData<MineState>(buildingEntity);
                    mineDeepestLevel = mineState.DeepestLevel;
                    mineLastResourceId = mineState.LastResourceId.ToString();
                }

                payload.folder.Add(new BuildingSaveJSON
                {
                    id = id.Id.ToString(),
                    numId = id.NumId,
                    ptsConstructionActual = ptsConstruction,
                    itemRequis = itemRequis,
                    workers = workers,
                    position = position,
                    mineDeepestLevel = mineDeepestLevel,
                    mineLastResourceId = mineLastResourceId
                });
            
        }

        File.WriteAllText(path, JsonUtility.ToJson(payload, true));
    }

    public override void Load(EntityManager em, string saveName, string subSaveName = null)
    {
        var folder = Path.Combine(Application.persistentDataPath, "Save", saveName);
        var fileName = string.IsNullOrEmpty(subSaveName) ? "Build.json" : subSaveName + ".json";
        var path = Path.Combine(folder, fileName);

        if (!File.Exists(path) || em == null)
            return;

        var payload = JsonUtility.FromJson<BuildingSaveFolder>(File.ReadAllText(path));
        if (payload == null || payload.folder == null)
            return;

        foreach (var building in payload.folder)
        {
            if (building == null)
                continue;

            var id = string.IsNullOrEmpty(building.id) ? "building" : building.id;
            if (QuerryDB.QueryInstances(DataType.Building, id, building.numId).FirstOrDefault() == Entity.Null)
            {
                var entity = BuildingManager.Create(em, id);
                if (entity != Entity.Null && em.HasComponent<MineState>(entity))
                {
                    MineManager.Initialize(em, entity, building.mineDeepestLevel);
                    var mineState = em.GetComponentData<MineState>(entity);
                    mineState.LastResourceId = building.mineLastResourceId ?? string.Empty;
                    em.SetComponentData(entity, mineState);
                }

                if (building.position != null && building.position.idNumBoard >= 0)
                {
                    var board = QuerryDB.QueryInstances(DataType.Board, "board", building.position.idNumBoard).FirstOrDefault();
                    if (board != Entity.Null)
                    {
                        BoardManager.AddOn(em, entity, board, building.position.position.ToInt2(), building.position.rotation, true);
                    }
                }
            }
        }
    }

    private static List<MineLevelDefinition> BuildMineLevels(MineLevelDataJSON[] levels)
    {
        var result = new List<MineLevelDefinition>();
        if (levels == null)
            return result;

        foreach (var level in levels)
        {
            if (level == null || level.level <= 0)
                continue;

            var definition = new MineLevelDefinition
            {
                Level = level.level
            };

            if (result.Any(existing => existing.Level == definition.Level))
                continue;

            result.Add(definition);
        }

        result.Sort((left, right) => left.Level.CompareTo(right.Level));
        return result;
    }

    private static List<Items> BuildRequirements(ItemQuantityJson[] items)
    {
        var result = new List<Items>();
        if (items == null)
            return result;

        for (int i = 0; i < items.Length; i++)
        {
            if (items[i] == null)
                continue;

            result.Add(new Items(items[i].id, items[i].quantity));
        }

        return result;
    }

 
}

public class BuildingDefinition : Definition
{
    public string IdGo;
    public string Name;
    public int2 Size;
    public float PointsConstructionNeed;
    public bool Worker;
    public bool Inventory;
    public bool Deco;
    public List<string> WorkZoneIds;
    public List<Items> RequiredItems;
    public List<FixedString32Bytes> Attributes = new List<FixedString32Bytes>();
    public List<MineLevelDefinition> MineLevels = new List<MineLevelDefinition>();
    public string EnergyId;
    public int EnergyMax;
    public int EnergyConsume;
    public int EnergyProduct;
}

public class MineLevelDefinition
{
    public int Level;
}

