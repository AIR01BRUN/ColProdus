using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;

/// <summary>
/// Statistiques sur les inventaires de stock (InventoryType.StockInventory).
/// Toute la logique est calculée à la volée : on parcourt à chaque fois TOUS les
/// inventaires de stock. Deux notions sont toujours séparées :
///   - Possédé  : tout ce que les inventaires possèdent (GetItemTotals).
///   - Réservé  : ce que les ItemNeed réservent (GetReservedQuantity).
///   - Disponible/utilisable : possédé - réservé (GetUsableQuantity).
/// Toutes les quantités sont manipulées via le type unique Items.
/// </summary>
public static class StockInventoryStat
{
    // -------------------------------------------------------- Helpers

    private static int GetItemQuantity(EntityManager em, Entity inventory, string itemId)
    {
        var index = InventoryManager.GetItemIndex(em, inventory, itemId);
        return index != -1 ? em.GetBuffer<Items>(inventory)[index].Quantity : 0;
    }

    private static List<Entity> GetStockInventories(EntityManager em)
    {
        return QuerryDB.QueryInstances(DataType.Inventory, InventoryType.StockInventory.ToString());
    }

    // ------------ Possédé : somme de ce que possèdent tous les stocks de zones.

    public static List<Items> GetItemTotals(EntityManager em)
    {
        var totals = new List<Items>();
        var inventories = GetStockInventories(em);
        foreach (var inventory in inventories)
        {
            if (inventory == Entity.Null || !em.Exists(inventory) || !em.HasBuffer<Items>(inventory))
                continue;

            var slots = em.GetBuffer<Items>(inventory);
            for (int i = 0; i < slots.Length; i++)
            {
                var slot = slots[i];
                if (slot.Quantity <= 0 || slot.ItemId.Length == 0)
                    continue;

                var id = slot.ItemId.ToString();
                var current = FindQuantity(totals, id);
                if (current == -1)
                    totals.Add(new Items(slot.ItemId, slot.Quantity));
                else
                    totals[current] = new Items(totals[current].ItemId, totals[current].Quantity + slot.Quantity);
            }
        }
        return totals;
    }

    public static int GetItemTotals(EntityManager em, string itemId)
    {
        if (em == null || string.IsNullOrEmpty(itemId))
            return 0;

        var total = 0;
        var inventories = GetStockInventories(em);
        foreach (var inventory in inventories)
        {
            if (inventory == Entity.Null || !em.Exists(inventory) || !em.HasBuffer<Items>(inventory))
                continue;

            total += GetItemQuantity(em, inventory, itemId);
        }
        return total;
    }

    // ------------ Réservé : ce que les ItemNeed réservent dans tous les stocks.

    public static List<Items> GetReservedQuantity(EntityManager em)
    {
        var reserved = new List<Items>();
        var inventories = GetStockInventories(em);
        foreach (var inventory in inventories)
        {
            if (inventory == Entity.Null || !em.Exists(inventory) || !em.HasBuffer<ItemNeed>(inventory))
                continue;

            var needs = em.GetBuffer<ItemNeed>(inventory);
            for (int i = 0; i < needs.Length; i++)
            {
                var need = needs[i];
                if (need.Item.ItemId.Length == 0)
                    continue;

                var id = need.Item.ItemId.ToString();
                var possessed = GetItemQuantity(em, inventory, id);
                if (possessed <= 0)
                    continue;

                // -1 = illimité => tout le stock est réservé.
                var qty = need.Item.Quantity == -1 ? possessed : System.Math.Min(possessed, need.Item.Quantity);
                var current = FindQuantity(reserved, id);
                if (current == -1)
                    reserved.Add(new Items(need.Item.ItemId, qty));
                else
                    reserved[current] = new Items(reserved[current].ItemId, reserved[current].Quantity + qty);
            }
        }
        return reserved;
    }

    public static int GetReservedQuantity(EntityManager em, string itemId)
    {
        if (em == null || string.IsNullOrEmpty(itemId))
            return 0;

        var reserved = 0;
        var itemIdValue = new FixedString64Bytes(itemId);
        var inventories = GetStockInventories(em);
        foreach (var inventory in inventories)
        {
            if (inventory == Entity.Null || !em.Exists(inventory) || !em.HasBuffer<ItemNeed>(inventory))
                continue;

            var needs = em.GetBuffer<ItemNeed>(inventory);
            for (int i = 0; i < needs.Length; i++)
            {
                if (!needs[i].Item.ItemId.Equals(itemIdValue))
                    continue;

                var possessed = GetItemQuantity(em, inventory, itemId);
                if (possessed <= 0)
                    break;

                reserved += needs[i].Item.Quantity == -1 ? possessed : System.Math.Min(possessed, needs[i].Item.Quantity);
                break;
            }
        }
        return reserved;
    }

    // ------------ Utilisable : possédé - réservé (ce qu'on peut vraiment prendre).

    public static List<Items> GetUsableQuantity(EntityManager em)
    {
        var totals = GetItemTotals(em);
        var reserved = GetReservedQuantity(em);
        var result = new List<Items>();
        foreach (var item in totals)
        {
            var usable = item.Quantity - (FindQuantity(reserved, item.ItemId.ToString()) != -1 ? reserved[FindQuantity(reserved, item.ItemId.ToString())].Quantity : 0);
            if (usable <= 0)
                continue;

            result.Add(new Items(item.ItemId, usable));
        }
        return result;
    }

    public static int GetUsableQuantity(EntityManager em, string itemId)
    {
        var usable = GetItemTotals(em, itemId) - GetReservedQuantity(em, itemId);
        return usable < 0 ? 0 : usable;
    }

    public static List<Items> GetUsableQuantity(EntityManager em, List<Items> items)
    {
        var result = new List<Items>();
        if (items == null || items.Count == 0)
            return result;

        foreach (var item in items)
        {
            if (item.ItemId.Length == 0)
                continue;

            var usable = GetUsableQuantity(em, item.ItemId.ToString());
            if (usable > 0)
                result.Add(new Items(item.ItemId, usable));
        }
        return result;
    }

    // ------------ HasEnougth.

    public static bool HasEnougth(EntityManager em, string itemId, int quantity)
    {
        if (string.IsNullOrEmpty(itemId) || quantity < 0)
            return false;

        return GetUsableQuantity(em, itemId) >= quantity;
    }

    public static bool HasEnougth(EntityManager em, List<Items> requirements)
    {
        if (requirements == null || requirements.Count == 0)
            return false;

        foreach (var requirement in requirements)
        {
            if (requirement.ItemId.Length == 0 || requirement.Quantity < 0 ||
                !HasEnougth(em, requirement.ItemId.ToString(), requirement.Quantity))
            {
                return false;
            }
        }

        return true;
    }

    public static bool HasEnougth(EntityManager em, Entity inventory, List<Items> requirements)
    {
        if (inventory == Entity.Null || !em.Exists(inventory) || !em.HasBuffer<Items>(inventory) || requirements == null)
            return false;

        foreach (var requirement in requirements)
        {
            if (requirement.ItemId.Length == 0 || requirement.Quantity < 0)
                return false;

            var stockQuantity = GetItemTotals(em, requirement.ItemId.ToString());
            var inventoryQuantity = GetItemQuantity(em, inventory, requirement.ItemId.ToString());
            var totalQuantity = stockQuantity + inventoryQuantity;
            if (totalQuantity < requirement.Quantity)
                return false;
        }

        return true;
    }

    // ------------ Plus rien à maintenir : tout est calculé à la volée,
    // ces méthodes sont gardées uniquement pour la compatibilité des appels.

    public static void Add(EntityManager em, Items item)
    {
    }

    public static void Remove(EntityManager em, Items item)
    {
    }

    public static void RefreshStockItemBook(EntityManager em)
    {
    }

    // ------------ Helpers internes.

    private static int FindQuantity(List<Items> items, string itemId)
    {
        for (int i = 0; i < items.Count; i++)
        {
            if (items[i].ItemId.ToString() == itemId)
                return i;
        }
        return -1;
    }
}