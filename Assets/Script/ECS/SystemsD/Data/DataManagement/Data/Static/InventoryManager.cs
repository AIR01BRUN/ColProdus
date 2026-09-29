using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using Unity.Entities;
using Unity.VectorGraphics;

public static class InventoryManager
{
    public static void Destroy(EntityManager em, Entity inventory)
    {
        if (em == null || inventory == Entity.Null || !em.Exists(inventory))
            return;

        if (em.HasComponent<InventoryOwnerLink>(inventory))
        {
            var owner = em.GetComponentData<InventoryOwnerLink>(inventory).Owner;
            if (owner != Entity.Null && em.Exists(owner) && em.HasComponent<InventoryLink>(owner))
            {
                var ownerInventory = em.GetComponentData<InventoryLink>(owner).Inventory;
                if (ownerInventory == inventory)
                    em.RemoveComponent<InventoryLink>(owner);
            }
        }

        Database.DeleteInstance(em, inventory);
        em.DestroyEntity(inventory);
    }

    public static Entity Create(EntityManager em, InventoryType id, int maxSlots = 3)
    {
        return Create(em, Entity.Null, id, maxSlots, addOwnerLink: false);
    }

    public static Entity Create(EntityManager em, Entity owner,InventoryType id, int maxSlots = 3)
    {
        return Create(em, owner, id, maxSlots, addOwnerLink: true);
    }

    private static Entity Create(EntityManager em, Entity owner, InventoryType id, int maxSlots, bool addOwnerLink)
    {
        var entity = em.CreateEntity();
        var number = Database.GetNextNumber(DataType.Inventory, id.ToString());

        em.AddComponentData(entity, new ID
        {
            DataType = DataType.Inventory,
            Id = id.ToString(),
            NumId = number,
            Name = id.ToString()
        });

        em.AddComponentData(entity, new Inventory
        {
            MaxSlots = maxSlots,
        });

        var slots = em.AddBuffer<Items>(entity);
        for (int slotIndex = 0; slotIndex < maxSlots; slotIndex++)
            slots.Add(new Items("None", -1));

        if (addOwnerLink && owner != Entity.Null)
        {
            em.AddComponentData(entity, new InventoryOwnerLink { Owner = owner });
            em.AddComponentData(owner, new InventoryLink { Inventory = entity });
        }

        Database.AddInstance(DataType.Inventory, id.ToString(), number, entity, em);
        return entity;
    }

    public static void AddItem(EntityManager em, Entity inventory, Items item)
    {
        var slots = em.GetBuffer<Items>(inventory);
        var index = GetItemIndex(em, inventory, item.ItemId.ToString());
        if (index != -1)
        {
            slots[index] = new Items(item.ItemId, slots[index].Quantity + item.Quantity);
            if (IsStockInventory(em, inventory))
                StockInventoryStat.Add(em, new Items(item.ItemId, item.Quantity));
            return;
        }
        else
        {
            for (int i = 0; i < slots.Length; i++)
            {
                if (slots[i].Quantity == -1)
                {
                    slots[i] = new Items(item.ItemId, item.Quantity);
                    if (IsStockInventory(em, inventory))
                        StockInventoryStat.Add(em, new Items(item.ItemId, item.Quantity));
                    return;
                }
            }
        }
    }
    public static void AddItemToInventory(EntityManager em, Entity entity, Items item)
    {
        var inventory = em.GetComponentData<InventoryLink>(entity).Inventory;
        AddItem(em, inventory, item); 
        
    }

    public static void RemoveItem(EntityManager em, Entity inventory, Items item)
    {
        if (item.Quantity <= 0) return;
        var slots = em.GetBuffer<Items>(inventory);
        var index = GetItemIndex(em, inventory, item.ItemId.ToString());
        if (index != -1)
        {
            if (slots[index].Quantity < item.Quantity) return;
            var newQuantity = slots[index].Quantity - item.Quantity;
            if (newQuantity <= 0)
            {
                slots[index] = new Items(default, -1);
            }
            else
            {
                slots[index] = new Items(item.ItemId, newQuantity);
            }
            if (IsStockInventory(em, inventory)) StockInventoryStat.Remove(em, new Items(item.ItemId, item.Quantity));
        }
    }
    public static int GetItemIndex(EntityManager em, Entity inventory, string itemId)
    {
        var slots = em.GetBuffer<Items>(inventory);
        for (int i = 0; i < slots.Length; i++)
        {
            if (slots[i].ItemId.Equals(new FixedString64Bytes(itemId)))
                return i;
        }
     

        return -1; // Item not found
    }
  
    public static bool TryGetStockWithItem(EntityManager em, Items item, out Entity stock)
    {
        var inventorys = QuerryDB.QueryInstances(DataType.Inventory, InventoryType.StockInventory.ToString());
        stock = default;
        foreach(var inventory in inventorys)
        {
            // Seul le surplus (quantité - ItemNeed) est prenable : si un stock
            // réserve autant qu'il possède, on ne peut rien lui prendre.
            if (GetAvailableItemQuantity(em, inventory, item.ItemId.ToString()) >= item.Quantity)
            {
                stock = inventory;
                return true;
            }
        }
        return false;
    }



    //Convertion:
    public static Dictionary<string,int> GetInventoryDictionary(EntityManager em, Entity inventory)
    {
        var res = new Dictionary<string, int>();
        var slots = em.GetBuffer<Items>(inventory);
        for (int i = 0; i < slots.Length; i++)
            res[slots[i].ItemId.ToString()] = slots[i].Quantity;
        return res;
    }
    public static Dictionary<string,int> GetItemNeedDictionary(EntityManager em, Entity inventory)
    {
        var res = new Dictionary<string, int>();
        if(!em.HasBuffer<ItemNeed>(inventory)) return res;
        var needs = em.GetBuffer<ItemNeed>(inventory);
        for (int i = 0; i < needs.Length; i++)
            res[needs[i].Item.ItemId.ToString()] = needs[i].Item.Quantity;
        return res;
    }
  
  
    public static int GetAvailableItemQuantity(EntityManager em, Entity inventory, string itemId)
    {
        GetInventoryDictionary(em,inventory).TryGetValue(itemId,out int valueInventory);
        GetItemNeedDictionary(em,inventory).TryGetValue(itemId,out int valueNeed);
        var res =  valueInventory - valueNeed;
        if(res < 0) res = -1;
        return res;
    }
      public static Dictionary<string,int> GetMissingItem(EntityManager em, Entity inventory,Dictionary<string,int> ItemObjectif = default )
    {
        Dictionary<string,int> res = new Dictionary<string, int>();
        var inventoryDic = GetInventoryDictionary(em,inventory);
        var needItemDic = ItemObjectif;
        if(ItemObjectif == default) needItemDic = GetItemNeedDictionary(em,inventory);
        foreach(var itemNeed in needItemDic)
        {
             var quantity = itemNeed.Value;
            if(inventoryDic.TryGetValue(itemNeed.Key,out int quantityHas))
            {
                quantity -= quantityHas;
            }
            if(quantity <= 0)continue;
            res.Add(itemNeed.Key,quantity);
        }
        return res;
    }
    public static List<string> AllItemId()
    {
        return QuerryDB.QueryDefinitions<ItemDefinition>(DataType.Item).Select(def => def.Id).ToList();
    }
    public static Dictionary<string,int> GetItems(EntityManager em, Entity inventory,List<string> itemIds = default)
    {
      
         Dictionary<string, int> maxQuantities = default;
        if (itemIds != default)
        {
            maxQuantities = new Dictionary<string, int>();
            foreach (var id in itemIds)
            {
                maxQuantities[id] = -1; // -1 = aucune limite de quantité
            }
        }
        return GetItems(em, inventory, maxQuantities);
    }
    public static Dictionary<string,int> GetItems(EntityManager em, Entity inventory,Dictionary<string,int> items = default)
    {
        Dictionary<string,int> res = new Dictionary<string, int>();
        var inventoryDic = GetInventoryDictionary(em,inventory);
        foreach(var itemInventory in inventoryDic)
        {
            
            var quantity = itemInventory.Value;
            if(quantity <= 0) continue; 
            if(items != default)
            {
                if(items.TryGetValue(itemInventory.Key, out var quantityNeed))
                {
                    if(quantityNeed != -1 && quantity >= quantityNeed)
                    {
                         quantity =quantityNeed;
                    }
                }else continue;
            }
            
            res.Add(itemInventory.Key,quantity);
        }
        return res;
    }
    public static Dictionary<string,int> GetAvailableItems(EntityManager em, Entity inventory,List<string> itemIds = default)
    {
          Dictionary<string, int> maxQuantities = default;
        if (itemIds != default)
        {
            maxQuantities = new Dictionary<string, int>();
            foreach (var id in itemIds)
            {
                maxQuantities[id] = -1; // -1 = aucune limite de quantité
            }
        }
        return GetAvailableItems(em, inventory, maxQuantities);
    }
     public static Dictionary<string,int> GetAvailableItems(EntityManager em, Entity inventory,Dictionary<string,int> items = default)
    {
        Dictionary<string,int> res = new Dictionary<string, int>();
        var inventoryDic = GetItems(em,inventory, items.Keys.ToList());
        var needItemDic =  GetItemNeedDictionary(em,inventory);

        foreach(var itemInventory in inventoryDic)
        {
            
            var quantity = itemInventory.Value;
            if (needItemDic.TryGetValue(itemInventory.Key , out var itemNeedQuantity))
            {
                quantity  -= itemNeedQuantity;
            }
            if(quantity <= 0) continue; 
             if(items != default)
            {
                if(items.TryGetValue(itemInventory.Key, out var quantityNeed))
                {
                    if(quantityNeed != -1 && quantity >= quantityNeed)
                    {
                         quantity =quantityNeed;
                    }
                }else continue;
            }
            res.Add(itemInventory.Key,quantity);
        }
        return res;
    }
    

    public static Dictionary<string,int> GetAllStockAvailableItems(EntityManager em,List<string> itemIds = default,List<Entity> inventoryToSkip =  default)
    {
        Dictionary<string,int> res = new Dictionary<string, int>();
        var stocks = QuerryDB.QueryInstances(DataType.Inventory, InventoryType.StockInventory.ToString());

        foreach(var inventory in stocks)
        {
           
            if (inventoryToSkip != null && inventoryToSkip.Contains(inventory)) continue;
            var availableItem = GetAvailableItems(em,inventory,itemIds);
            
            foreach (var item in availableItem )
            {
                if(res.TryGetValue(item.Key, out int current))
                {
                    res[item.Key] = current + item.Value;
                }
                else
                {
                    res.Add(item.Key,item.Value);
                }
                
            }  
            
        }

        return res;
    }
   

    private static bool IsOwnerBuildingBusy(EntityManager em, Entity inventory)
    {
        if (!em.Exists(inventory) || !em.HasComponent<InventoryOwnerLink>(inventory))
            return false;

        var owner = em.GetComponentData<InventoryOwnerLink>(inventory).Owner;
        return owner != Entity.Null && em.Exists(owner) && em.HasComponent<Busy>(owner);
    }

    private static bool IsOwnerBuildingUnderConstruction(EntityManager em, Entity inventory)
    {
        if (inventory == Entity.Null || !em.Exists(inventory) || !em.HasComponent<InventoryOwnerLink>(inventory))
            return false;

        var owner = em.GetComponentData<InventoryOwnerLink>(inventory).Owner;
        if (owner == Entity.Null || !em.Exists(owner) || !em.HasComponent<StateBuilding>(owner))
            return false;
        return em.GetComponentData<StateBuilding>(owner).State == StateBuild.UnderConstruct;
    }

    public static bool TryGetBusyBuildingInventory(EntityManager em, Entity pawn, out Entity inventory)
    {
        inventory = Entity.Null;
        var busyBuildings = QuerryDB.QueryInstances<Busy>(em);
        foreach (var building in busyBuildings)
        {
            if (building == Entity.Null || !em.Exists(building) || !em.HasComponent<Busy>(building))
                continue;
            if (em.GetComponentData<Busy>(building).Pawn != pawn)
                continue;
            if (!em.HasComponent<InventoryLink>(building))
                continue;

            inventory = em.GetComponentData<InventoryLink>(building).Inventory;
            return true;
        }
        return false;
    }

    public static bool TryResearchInventory(EntityManager em , Dictionary<string,int> itemNeed , List<Entity> inventoryPriority, out Entity res, out Dictionary<string,int> content)
    {   
        res = Entity.Null;
        var stocks = QuerryDB.QueryInstances(DataType.Inventory, InventoryType.StockInventory.ToString());
        content = new Dictionary<string, int>();

        if (inventoryPriority != null)
        {
            foreach(var inventoryPrio in inventoryPriority)
            {
                // Un inventaire dont le building propriétaire est Busy ou en construction est ignoré.
                if (IsOwnerBuildingBusy(em, inventoryPrio)) continue;
                if (IsOwnerBuildingUnderConstruction(em, inventoryPrio)) continue;
                var inventoryDic = GetItems(em,inventoryPrio,itemNeed);
                if(inventoryDic.Count > 0)
                {
                    res  = inventoryPrio;
                    content = inventoryDic;
                    return true;
                }
            }
        }
        foreach(var inventoryStock in stocks)
        {
            if (IsOwnerBuildingBusy(em, inventoryStock)) continue;
            if (IsOwnerBuildingUnderConstruction(em, inventoryStock)) continue;
            var inventoryDic = GetAvailableItems(em,inventoryStock,itemNeed);
            if(inventoryDic.Count > 0)
            {
                res  = inventoryStock;
                content = inventoryDic;
                return true;
            }
        }
        return false;

    }
   
    public static bool HasEnougthItem(Dictionary<string,int> itemNeed,  Dictionary<string,int> inventoryToCompare)
    {  
       foreach(var item in itemNeed)
        {
            if(!inventoryToCompare.TryGetValue(item.Key, out int quantity))
            {
                return false;
            }
            else if(quantity < item.Value) return false;
        }
        return true;
    }
     public static bool HasItem(List<string> itemId,  Dictionary<string,int> inventoryToCompare)
    {  
       foreach(var item in itemId)
        {
            if(!inventoryToCompare.TryGetValue(item, out int quantity))
            {
                return false;
            }
            else if(quantity < 1) return false;
        }
        return true;
    }
    public static Dictionary<string, int> Merge(Dictionary<string, int> inventory1, Dictionary<string, int> inventory2)
    {
        Dictionary<string, int> result = new Dictionary<string, int>(inventory1);

        foreach (var kvp in inventory2)
        {
            if (result.TryGetValue(kvp.Key, out int currentAmount))
            {
                result[kvp.Key] = currentAmount + kvp.Value;
            }
            else
            {
                result[kvp.Key] = kvp.Value;
            }
        }

        return result;
    }

    public static string FormatItems(Dictionary<string, int> items)
    {
        if (items == null || items.Count == 0)
            return "none";

        var values = new List<string>();
        foreach (var item in items)
            values.Add($"{item.Key}={item.Value}");
        return string.Join(", ", values);
    }




    

    public static bool CanAddItem(EntityManager em, Entity inventory, string itemId)
    {
        if (inventory == Entity.Null || !em.Exists(inventory) || !em.HasBuffer<Items>(inventory))
            return false;
        return GetItemIndex(em, inventory, itemId) != -1 || HasSlotEmpty(em,inventory);
    }
    public static bool HasSlotEmpty(EntityManager em, Entity inventory)
    {
        var slots = em.GetBuffer<Items>(inventory).ToNativeArray(Allocator.Temp);
        bool res = false;
        if(slots.Any(slot => slot.Quantity == -1))
        {
            res= true;
        }
        slots.Dispose();
        return res;
    }

    private static bool IsStockInventory(EntityManager em, Entity inventory)
    {
        return em.Exists(inventory) &&
               em.HasComponent<ID>(inventory) &&
               em.GetComponentData<ID>(inventory).Id.ToString() == InventoryType.StockInventory.ToString();
    }

    public static void Clear(EntityManager em, Entity inventory)
    {
        var slots = em.GetBuffer<Items>(inventory);

        for(int i = 0; i < slots.Length; i++)
        {
            slots[i] = new Items("None", -1);
        }
    }

    public static void ClearItemNeed(EntityManager em, Entity inventory)
    {
        if (inventory == Entity.Null || !em.Exists(inventory) || !em.HasBuffer<ItemNeed>(inventory))
            return;
        em.GetBuffer<ItemNeed>(inventory).Clear();
        StockInventoryStat.RefreshStockItemBook(em);
    }

    public static void AddItemNeed(EntityManager em, Entity inventory, Items item)
    {
        if (inventory == Entity.Null || !em.Exists(inventory) || item.ItemId.Length == 0)
            return;

        if (!em.HasBuffer<ItemNeed>(inventory))
            em.AddBuffer<ItemNeed>(inventory);

        var buffer = em.GetBuffer<ItemNeed>(inventory);
        for (int i = 0; i < buffer.Length; i++)
        {
            if (buffer[i].Item.ItemId.Equals(item.ItemId))
            {
                // Déjà présent : on conserve la quantité (config utilisateur à venir).
                return;
            }
        }
        buffer.Add(new ItemNeed { Item = new Items(item.ItemId, item.Quantity) });
        StockInventoryStat.RefreshStockItemBook(em);
    }
}


public struct Inventory : IComponentData
{
    public int MaxSlots;
}

public struct InventoryLink : IComponentData
{
    public Entity Inventory;
}

public struct InventoryOwnerLink : IComponentData
{
    public Entity Owner;
}

public struct ItemNeed : IBufferElementData
{
    public Items Item; // -1 = illimité (autant qu'on veut)
}
public enum InventoryType
{
    PawnInventory,
    StockInventory,
    RequestInventory
}