using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Inventaire de stock d'un bâtiment ou d'une zone : un CaseTemplate par item,
/// rempli de droite à gauche, avec retour à la ligne quand la largeur est atteinte
/// et un scroll vertical si la liste est trop haute.
/// Un item à zéro reste affiché s'il est présent dans ItemNeed.
/// Le bouton CHOOSE ITEM ouvre la SelectionTemplate qui gère les ItemNeed
/// (sélection d'un item + quantité).
/// </summary>
public class ItemsTemplate : TemplateEntityUi
{
    private const float CellSize = 110f;

    private class ItemCase
    {
        public CaseTemplate View;
        public string ItemId;
    }

    private readonly Label _title;
    private readonly ScrollView _scroll;
    private readonly VisualElement _grid;
    private readonly ButtonTemplate _chooseButton;
    private readonly Label _infoLabel;

    private readonly List<ItemCase> _cases = new List<ItemCase>();
    private SelectionTemplate _itemSelection;
    private SelectionTemplate.Choice _itemChoice;
    private string _selectedItemId;

    public ItemsTemplate()
    {
        Template = UITheme.Root();

        _title = UITheme.Title("STOCK");

        _scroll = UITheme.ScrollList();

        _grid = new VisualElement();
        _grid.style.position = Position.Relative;
        _grid.style.width = Length.Percent(100);
        _grid.RegisterCallback<GeometryChangedEvent>(_ => LayoutCases());
        _scroll.Add(_grid);

        _infoLabel = UITheme.MutedText(string.Empty);

        var footer = UITheme.Card();
        var chooseRow = UITheme.Row();
        chooseRow.style.justifyContent = Justify.FlexEnd;

        _chooseButton = new ButtonTemplate(1);
        _chooseButton.Setup("CHOOSE ITEM", OpenItemSelection);

        chooseRow.Add(_chooseButton.Template);
        footer.Add(UITheme.Section("ITEM NEED (à réserver)"));
        footer.Add(chooseRow);

        Template.Add(_title);
        Template.Add(_scroll);
        Template.Add(_infoLabel);
        Template.Add(footer);
    }

    private Entity ResolveInventory(EntityManager em)
    {
        if (em.HasComponent<InventoryLink>(Entity))
            return em.GetComponentData<InventoryLink>(Entity).Inventory;

        if (em.HasComponent<WorkZone>(Entity))
        {
            if (WorkZoneManager.TryGetBuildingWithInventory(em, Entity, out var building) &&
                em.Exists(building) && em.HasComponent<InventoryLink>(building))
                return em.GetComponentData<InventoryLink>(building).Inventory;
        }
        return Entity.Null;
    }

    public override void Refresh()
    {
        var em = World.DefaultGameObjectInjectionWorld.EntityManager;
        if (Entity == Entity.Null || !em.Exists(Entity))
        {
            ClearCases();
            _infoLabel.text = "";
            return;
        }

        _title.text = em.HasComponent<WorkZone>(Entity)
            ? $"STOCK ZONE {DetailUiUtility.GetEntityName(em, Entity)}"
            : $"STOCK {DetailUiUtility.GetEntityName(em, Entity)}";

        var inventory = ResolveInventory(em);
        if (inventory == Entity.Null || !em.Exists(inventory) || !em.HasBuffer<Items>(inventory))
        {
            ClearCases();
            _infoLabel.text = "Aucun inventaire de stock.";
            return;
        }

        var slots = em.GetBuffer<Items>(inventory);
        var quantities = CollectQuantities(em, inventory, slots);

        ClearCases();
        foreach (var entry in quantities)
            AddItemCase(em, entry.Key, entry.Value);

        var needs = em.HasBuffer<ItemNeed>(inventory)
            ? InventoryManager.GetItemNeedDictionary(em, inventory)
            : new Dictionary<string, int>();

        _infoLabel.text = quantities.Count == 0
            ? (needs.Count == 0 ? "Stock vide, aucun besoin." : "Stock vide.")
            : "";

        LayoutCases();
    }

    /// <summary>Un case par item : ce qui est en stock, plus chaque item demandé (même à zéro).</summary>
    private static Dictionary<string, ItemEntry> CollectQuantities(EntityManager em, Entity inventory, DynamicBuffer<Items> slots)
    {
        var result = new Dictionary<string, ItemEntry>();

        for (var i = 0; i < slots.Length; i++)
        {
            if (slots[i].Quantity <= 0)
                continue;

            var itemId = slots[i].ItemId.ToString();
            if (string.IsNullOrEmpty(itemId))
                continue;

            result[itemId] = new ItemEntry { Quantity = slots[i].Quantity };
        }

        if (em.HasBuffer<ItemNeed>(inventory))
        {
            var needs = em.GetBuffer<ItemNeed>(inventory);
            for (var i = 0; i < needs.Length; i++)
            {
                var itemId = needs[i].Item.ItemId.ToString();
                if (string.IsNullOrEmpty(itemId) || needs[i].Item.Quantity <= 0)
                    continue;

                if (result.TryGetValue(itemId, out var entry))
                    entry.Need = needs[i].Item.Quantity;
                else
                    result[itemId] = new ItemEntry { Quantity = 0, Need = needs[i].Item.Quantity };
            }
        }

        return result;
    }

    private class ItemEntry
    {
        public int Quantity;
        public int Need;
    }

    private void AddItemCase(EntityManager em, string itemId, ItemEntry entry)
    {
        var view = new CaseTemplate();
        view.SetName(itemId);
        view.SetNumber(entry.Quantity, entry.Need > 0 ? entry.Need : (int?)null);
        view.SetResearchInfo("");

        // L'item actuellement demandé reste surligné.
        view.Select(itemId == _selectedItemId);

        _grid.Add(view.Template);
        _cases.Add(new ItemCase { View = view, ItemId = itemId });
    }

    private void ClearCases()
    {
        _grid.Clear();
        _cases.Clear();
    }

    /// <summary>Sélection d'un item dans une SelectionTemplate : recherche sur l'id, filtre sur le type.
    /// La quantité n'est disponible qu'en TwoStep (une seule sélection) : le VALIDER écrit l'ItemNeed.</summary>
    private void OpenItemSelection()
    {
        if (_itemSelection == null)
        {
            _itemSelection = new SelectionTemplate();
            _itemSelection.SetupDefinitions<ItemDefinition>("ITEM", DataType.Item, 1,
                SelectionTemplate.ValidationMode.SelectionTwoStep, OnItemChosen, null);
            _itemSelection.SetSearch(choice => choice.GetId(), "id");
            _itemSelection.AddChoiceFilterDropdown("Type", AllItemTypes(), GetItemType, GetItemValue);
            _itemSelection.OnChoiceSelected = OnItemChoiceSelected;
            _itemSelection.EnableQuantity(0);
        }

        _itemSelection.Refresh();

        var screen = UiEntityRegistry.GetSingleton<ObjectDetailScreen>();
        if (screen != null)
            screen.SwapAction(_itemSelection);
    }

    private void OnItemChoiceSelected(SelectionTemplate.Choice choice)
    {
        if (choice?.Definition == null)
            return;

        _itemChoice = choice;
        _selectedItemId = choice.GetId();

        // Le besoin déjà demandé sert de valeur par défaut.
        _itemSelection.SetQuantity(choice, GetCurrentNeed(_selectedItemId), notify: false);
        Refresh();
    }

    private void OnItemChosen(Definition definition)
    {
        if (definition == null)
            return;

        var em = World.DefaultGameObjectInjectionWorld.EntityManager;
        var inventory = ResolveInventory(em);
        if (inventory == Entity.Null || !em.Exists(inventory))
            return;

        var quantity = _itemChoice != null ? _itemSelection.GetQuantity(_itemChoice) : 0;
        SetItemNeed(em, inventory, definition.Id, quantity);
    }

    private int GetCurrentNeed(string itemId)
    {
        var em = World.DefaultGameObjectInjectionWorld.EntityManager;
        var inventory = ResolveInventory(em);
        if (inventory == Entity.Null || !em.Exists(inventory) || string.IsNullOrEmpty(itemId))
            return 0;

        return InventoryManager.GetItemNeedDictionary(em, inventory).TryGetValue(itemId, out var need) ? need : 0;
    }

    private static IEnumerable<string> AllItemTypes()
    {
        var types = new List<string>();
        foreach (var definition in QuerryDB.QueryDefinitions<ItemDefinition>(DataType.Item))
        {
            if (string.IsNullOrEmpty(definition.Type) || types.Contains(definition.Type))
                continue;
            types.Add(definition.Type);
        }

        types.Sort();
        return types;
    }

    private static string GetItemType(SelectionTemplate.Choice choice)
    {
        return choice.Definition is ItemDefinition item ? item.Type : "";
    }

    /// <summary>Info affichée dans la case quand le filtre Type est actif : la valeur de l'item.</summary>
    private static string GetItemValue(SelectionTemplate.Choice choice)
    {
        if (choice.Definition is not ItemDefinition item || item.Value <= 0)
            return "";

        return item.Value.ToString();
    }

    /// <summary>Remplit de droite à gauche, retour à la ligne quand le bord gauche est atteint.</summary>
    private void LayoutCases()
    {
        if (_cases.Count == 0)
            return;

        var width = _grid.resolvedStyle.width;
        if (width <= 0f)
            return;

        var x = width - CellSize;
        var y = 0f;

        foreach (var itemCase in _cases)
        {
            if (x < 0f)
            {
                y += CellSize;
                x = width - CellSize;
            }

            var element = itemCase.View.Template;
            element.style.position = Position.Absolute;
            element.style.width = CellSize;
            element.style.height = CellSize;
            element.style.left = x;
            element.style.top = y;
            x -= CellSize;
        }

        _grid.style.height = y + CellSize;
    }

    private void SetItemNeed(EntityManager em, Entity inventory, string itemId, int qty)
    {
        if (inventory == Entity.Null || !em.Exists(inventory) || string.IsNullOrEmpty(itemId))
            return;

        if (!em.HasBuffer<ItemNeed>(inventory))
            em.AddBuffer<ItemNeed>(inventory);

        var needs = em.GetBuffer<ItemNeed>(inventory);
        var fixedId = new FixedString64Bytes(itemId);
        var found = false;

        for (var i = 0; i < needs.Length; i++)
        {
            if (!needs[i].Item.ItemId.Equals(fixedId))
                continue;

            found = true;
            if (qty <= 0)
                needs.RemoveAt(i);
            else
                needs[i] = new ItemNeed { Item = new Items(itemId, Mathf.Max(0, qty)) };
            break;
        }

        if (!found && qty > 0)
            needs.Add(new ItemNeed { Item = new Items(itemId, Mathf.Max(0, qty)) });

        if (em.HasComponent<ID>(inventory) &&
            em.GetComponentData<ID>(inventory).Id.ToString() == InventoryType.StockInventory.ToString())
            StockInventoryStat.RefreshStockItemBook(em);

        _selectedItemId = qty > 0 ? itemId : "";
        Refresh();
    }
}
