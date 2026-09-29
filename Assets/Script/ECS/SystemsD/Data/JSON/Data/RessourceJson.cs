[System.Serializable]
public class RessourceDataJSON : IdJson
{
    public int ptsNeed;
    public float growthRate;
    public string[] attributes;
    public LootTableRessource[] lootTable;
    public bool quarry;
    public int rarity = 1;
    public bool quarryOnly;
}
[System.Serializable]
public class LootTableRessource
{
    public string idItem;
    public int quantity;
    public float chance = 100f;
}


