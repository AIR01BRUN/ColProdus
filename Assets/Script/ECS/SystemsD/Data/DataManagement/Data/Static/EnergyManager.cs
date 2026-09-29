using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

/// <summary>
/// Énergie d'un bâtiment (Heat, Magie...). Un bâtiment qui a un EnergyId possède
/// le composant Energy :
///   - EnergyId      : type d'énergie gérée (Heat, Magie...).
///   - EnergyMax     : réserve maximale.
///   - EnergyActual  : réserve courante.
///   - EnergyConsume : énergie consommée par une étape de procédure qui en a besoin.
///   - EnergyProduct : énergie produite par l'building quand il est producteur.
/// L'énergie est apportée par des ITEMS porteurs du même type d'énergie
/// (ItemDefinition.EnergyId / EnergyValue) : un pawn les apporte jusqu'au bâtiment
/// (tâche "GetEnergy") et ils sont convertis en énergie à la livraison.
/// L'énergie ne varie jamais avec le temps : elle ne baisse que lorsqu'une étape de
/// procédure est terminée (Step.NeedEnergy).
/// </summary>
public static class EnergyManager
{
    /// <summary>Applique la définition énergétique d'un bâtiment sur son entité.</summary>
    public static void Create(EntityManager em, Entity building, string energyId, int energyMax, int energyConsume, int energyProduct)
    {
        if (em == null || building == Entity.Null || !em.Exists(building) || string.IsNullOrEmpty(energyId))
            return;

        if (energyMax <= 0)
            energyMax = 100;

        var energy = new Energy
        {
            EnergyId = new FixedString32Bytes(energyId.Trim()),
            EnergyMax = energyMax,
            EnergyActual = 0,
            EnergyConsume = energyConsume < 0 ? 0 : energyConsume,
            EnergyProduct = energyProduct < 0 ? 0 : energyProduct
        };

        if (em.HasComponent<Energy>(building))
            em.SetComponentData(building, energy);
        else
            em.AddComponentData(building, energy);
    }

    /// <summary>Ajoute de l'énergie (plafonnée à EnergyMax). Retourne l'énergie réellement ajoutée.</summary>
    public static int AddEnergy(EntityManager em, Entity building, int amount)
    {
        if (em == null || building == Entity.Null || !em.Exists(building) || amount <= 0 ||
            !em.HasComponent<Energy>(building))
            return 0;

        var energy = em.GetComponentData<Energy>(building);
        var missing = energy.EnergyMax - energy.EnergyActual;
        if (missing <= 0)
            return 0;

        var added = math.min(amount, missing);
        energy.EnergyActual += added;
        em.SetComponentData(building, energy);
        return added;
    }

    /// <summary>Retire de l'énergie (jamais sous 0). Retourne l'énergie réellement retirée.</summary>
    public static int RemoveEnergy(EntityManager em, Entity building, int amount)
    {
        if (em == null || building == Entity.Null || !em.Exists(building) || amount <= 0 ||
            !em.HasComponent<Energy>(building))
            return 0;

        var energy = em.GetComponentData<Energy>(building);
        if (energy.EnergyActual <= 0)
            return 0;

        var removed = math.min(amount, energy.EnergyActual);
        energy.EnergyActual -= removed;
        em.SetComponentData(building, energy);
        return removed;
    }

    /// <summary>
    /// Convertit en énergie tous les items porteurs du même type d'énergie déposés dans
    /// l'inventaire du bâtiment. Retourne l'énergie obtenue.
    /// </summary>
    public static int AbsorbEnergy(EntityManager em, Entity building, Entity inventory)
    {
        if (em == null || building == Entity.Null || !em.Exists(building) || !em.HasComponent<Energy>(building) ||
            inventory == Entity.Null || !em.Exists(inventory) || !em.HasBuffer<Items>(inventory))
            return 0;

        var energyId = em.GetComponentData<Energy>(building).EnergyId;
        var values = GetEnergyValueByItem(em, energyId);
        if (values.Count == 0)
            return 0;

        var toConsume = new List<Items>();
        var total = 0;
        var slots = em.GetBuffer<Items>(inventory);
        for (int i = 0; i < slots.Length; i++)
        {
            var slot = slots[i];
            if (slot.Quantity <= 0 || slot.ItemId.Length == 0)
                continue;

            if (!values.TryGetValue(slot.ItemId.ToString(), out var value))
                continue;

            toConsume.Add(new Items(slot.ItemId, slot.Quantity));
            total += slot.Quantity * value;
        }

        if (toConsume.Count == 0)
            return 0;

        foreach (var item in toConsume)
            InventoryManager.RemoveItem(em, inventory, item);

        AddEnergy(em, building, total);
        return total;
    }

    /// <summary>
    /// Calcule les items à rapporter pour remplir la réserve au maximum. Pour chaque item
    /// porteur du même type d'énergie, on prend le minimum d'items nécessaires
    /// (plafonné au stock réellement disponible) : le calcul s'arrête dès que le manque est
    /// couvert. Si le stock est insuffisant, on prend tout ce qui est possible.
    /// </summary>
    public static Dictionary<string, int> GetEnergyItemRequest(EntityManager em, FixedString32Bytes energyId, int missing)
    {
        var request = new Dictionary<string, int>();
        if (em == null || missing <= 0 || energyId.Length == 0)
            return request;

        var available = new Dictionary<string, int>();
        foreach (var item in StockInventoryStat.GetUsableQuantity(em))
        {
            if (item.Quantity > 0)
                available[item.ItemId.ToString()] = item.Quantity;
        }

        if (available.Count == 0)
            return request;

        var definitions = QuerryDB.QueryDefinitions<ItemDefinition>(DataType.Item)
            .Where(definition => definition != null && definition.EnergyValue > 0 &&
                                 string.Equals(definition.EnergyId, energyId.ToString(), StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(definition => definition.EnergyValue)
            .ToList();

        foreach (var definition in definitions)
        {
            if (missing <= 0)
                break;

            if (!available.TryGetValue(definition.Id, out var stock) || stock <= 0)
                continue;

            var needed = (missing + definition.EnergyValue - 1) / definition.EnergyValue;
            var take = needed < stock ? needed : stock;
            if (take <= 0)
                continue;

            request[definition.Id] = take;
            missing -= take * definition.EnergyValue;
        }

        return request;
    }

    /// <summary>Inventaire dans lequel le bâtiment reçoit son énergie (créé à la demande).</summary>
    public static Entity GetInventory(EntityManager em, Entity building)
    {
        if (em == null || building == Entity.Null || !em.Exists(building))
            return Entity.Null;

        if (em.HasComponent<InventoryLink>(building))
        {
            var inventory = em.GetComponentData<InventoryLink>(building).Inventory;
            if (inventory != Entity.Null && em.Exists(inventory))
                return inventory;
        }

        if (em.HasComponent<ProcedureInventoryLink>(building))
        {
            var procedureInventory = em.GetComponentData<ProcedureInventoryLink>(building).Inventory;
            if (procedureInventory != Entity.Null && em.Exists(procedureInventory))
                return procedureInventory;
        }

        var created = InventoryManager.Create(em, building, InventoryType.RequestInventory, 8);
        return created;
    }

    private static Dictionary<string, int> GetEnergyValueByItem(EntityManager em, FixedString32Bytes energyId)
    {
        var result = new Dictionary<string, int>();
        if (energyId.Length == 0)
            return result;

        var definitions = QuerryDB.QueryDefinitions<ItemDefinition>(DataType.Item);
        foreach (var definition in definitions)
        {
            if (definition == null || definition.EnergyValue <= 0 || string.IsNullOrEmpty(definition.Id))
                continue;

            if (!string.Equals(definition.EnergyId, energyId.ToString(), StringComparison.OrdinalIgnoreCase))
                continue;

            result[definition.Id] = definition.EnergyValue;
        }

        return result;
    }
}

public struct Energy : IComponentData
{
    public FixedString32Bytes EnergyId;
    public int EnergyActual;
    public int EnergyMax;
    public int EnergyConsume;
    public int EnergyProduct;
}
