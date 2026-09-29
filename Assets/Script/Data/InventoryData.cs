using Unity.Entities;

[System.Serializable]
public class InventoryData 
{
    public int maxSlots = 5;
    public ItemStackData[] itemStacks;
}
[System.Serializable]
public class ItemStackData 
{
    public string itemID;
    public int quantity = 0;
    public int quantityMax = 20;
    public float chance = 100f;
}