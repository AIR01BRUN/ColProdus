using System;
using System.Collections.Generic;

[Serializable]
public class BuildingDataJSON : IdJson
{
    public ItemQuantityJson[] itemRequis;
    public Int2Json size; 
    public int ptsConstructionNeed;
    public bool inventory = false;
    public bool worker = false; 
    public bool deco = false;
    public string[] workZone;
    public string[] attributes;
    public MineLevelDataJSON[] mineLevels;
    public string energyId;
    public int energyMax = 100;
    public int energyConsume = 20;
    public int energyProduct;
}
[Serializable]
public class MineLevelDataJSON
{
    public int level;
}
[Serializable]
public class BuildingSaveFolder
{
    public  List<BuildingSaveJSON> folder;
}
[Serializable]
public class BuildingSaveJSON : IdJson
{
    public List<ItemQuantityJson> itemRequis;
    public float ptsConstructionActual;
    public List<WorkerSave> workers;
    public PositionOnBoardJSON position;
    public int mineDeepestLevel;
    public string mineLastResourceId;

    
}
[Serializable]
public class WorkerSave
{
    public string id;
    public int idNum;
    public int numPlacement;
}
