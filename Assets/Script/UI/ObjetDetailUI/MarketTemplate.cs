using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Entities;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Marché(s) : pour une zone, plusieurs marchés peuvent être présents (dropdown
/// de cible) ; pour un bâtiment type Market, un seul. On choisit le type d'ordre
/// (SELL / BUY / URGENT_BUY / URGENT_SELL), l'item et la quantité puis on applique.
/// </summary>
public class MarketTemplate : TemplateEntityUi
{
    private readonly Label _title;
    private readonly VisualElement _marketList;
    private readonly DropdownTemplate _targetDropdown;
    private readonly DropdownTemplate _typeDropdown;
    private readonly DropdownTemplate _itemDropdown;
    private readonly FieldTemplate _quantityField;
    private readonly Label _infoLabel;
    private readonly ButtonTemplate _applyButton;

    private readonly List<Entity> _targets = new();
    private readonly List<string> _itemIds = new();
    private Entity _target = Entity.Null;
    private bool _userEdited;

    private MarketType _marketType = MarketType.SELL;
    private string _selectedItem = "";
    private int _selectedQuantity = 1;

    public MarketTemplate()
    {
        Template = UITheme.Root();

        var header = UITheme.Card();
        _title = UITheme.Title("MARKET");
        _marketList = new VisualElement();
        _marketList.style.flexDirection = FlexDirection.Column;
        _marketList.style.marginTop = 4;
        header.Add(_title);
        header.Add(_marketList);

        var editorCard = UITheme.Card();
        editorCard.style.flexGrow = 1;

        _targetDropdown = new DropdownTemplate(0);
        _targetDropdown.Setup("Target");
        _targetDropdown.Template.style.marginBottom = 6;
        _targetDropdown.Value.RegisterValueChangedCallback(evt =>
        {
            var em = World.DefaultGameObjectInjectionWorld.EntityManager;
            foreach (var target in _targets)
            {
                if (DetailUiUtility.GetEntityName(em, target) != evt.newValue)
                    continue;
                LoadTarget(em, target);
                break;
            }
        });

        _typeDropdown = new DropdownTemplate(0);
        _typeDropdown.Setup("Type");
        _typeDropdown.Template.style.marginBottom = 6;
        foreach (var type in Enum.GetNames(typeof(MarketType)))
            _typeDropdown.AddValue(type);
        _typeDropdown.Value.RegisterValueChangedCallback(evt =>
        {
            _userEdited = true;
            if (Enum.TryParse(evt.newValue, out MarketType parsed))
                _marketType = parsed;
            UpdateInfo();
        });

        _itemDropdown = new DropdownTemplate(0);
        _itemDropdown.Setup("Item");
        _itemDropdown.Template.style.marginBottom = 6;
        _itemDropdown.Value.RegisterValueChangedCallback(evt =>
        {
            _userEdited = true;
            _selectedItem = evt.newValue;
            UpdateInfo();
        });

        _quantityField = new FieldTemplate(0);
        _quantityField.Setup("Quantity");
        _quantityField.Template.style.marginBottom = 6;
        _quantityField.Value.RegisterValueChangedCallback(evt =>
        {
            _userEdited = true;
            _selectedQuantity = int.TryParse(evt.newValue, out var parsed) ? parsed : 0;
            UpdateInfo();
        });

        _infoLabel = UITheme.MutedText(string.Empty);

        _applyButton = new ButtonTemplate(1);
        _applyButton.Setup("APPLY ORDER", ApplySelection);
        _applyButton.Template.style.marginTop = 6;

        editorCard.Add(_targetDropdown.Template);
        editorCard.Add(_typeDropdown.Template);
        editorCard.Add(_itemDropdown.Template);
        editorCard.Add(_quantityField.Template);
        editorCard.Add(_infoLabel);
        editorCard.Add(_applyButton.Template);

        Template.Add(header);
        Template.Add(editorCard);
    }

    public override void Refresh()
    {
        var em = World.DefaultGameObjectInjectionWorld.EntityManager;
        if (Entity == Entity.Null || !em.Exists(Entity))
            return;

        _title.text = em.HasComponent<WorkZone>(Entity)
            ? $"MARKET ZONE {DetailUiUtility.GetEntityName(em, Entity)}"
            : $"MARKET {DetailUiUtility.GetEntityName(em, Entity)}";

        BuildTargets(em);

        _marketList.Clear();
        foreach (var target in _targets)
        {
            var selection = GetCurrentSelection(em, target);
            var type = em.HasComponent<MarketInfo>(target)
                ? em.GetComponentData<MarketInfo>(target).Type.ToString()
                : "-";
            var row = UITheme.Row();
            row.style.marginTop = 2;

            var nameLabel = UITheme.Body($"{DetailUiUtility.GetEntityName(em, target)} : [{type}]");
            nameLabel.style.flexGrow = 1;

            var itemLabel = UITheme.MutedText($"{selection.Item.ItemId} x{selection.Item.Quantity}");
            itemLabel.style.unityTextAlign = TextAnchor.MiddleRight;

            row.Add(nameLabel);
            row.Add(itemLabel);
            _marketList.Add(row);
        }

        if (_target == Entity.Null || !_targets.Contains(_target))
            _target = _targets.Count > 0 ? _targets[0] : Entity.Null;

        RefreshTargetDropdown(em);
        if (_target == Entity.Null)
        {
            _infoLabel.text = "Aucun marché dans la zone.";
            return;
        }

        LoadItemChoices(em);

        if (!_userEdited)
            LoadEditorFromTarget(em);
    }

    private void BuildTargets(EntityManager em)
    {
        _targets.Clear();
        if (em.HasComponent<WorkZone>(Entity) && em.HasBuffer<WorkZoneEntity>(Entity))
        {
            foreach (var entry in em.GetBuffer<WorkZoneEntity>(Entity))
            {
                if (entry.Entity == Entity.Null || !em.Exists(entry.Entity))
                    continue;
                if (em.HasBuffer<MarketSelection>(entry.Entity))
                    _targets.Add(entry.Entity);
            }
        }
        else if (em.HasBuffer<MarketSelection>(Entity))
        {
            _targets.Add(Entity);
        }
    }

    private void LoadTarget(EntityManager em, Entity target)
    {
        _target = target;
        _userEdited = false;
        LoadEditorFromTarget(em);
    }

    private void LoadEditorFromTarget(EntityManager em)
    {
        if (_target == Entity.Null || !em.Exists(_target))
            return;

        _marketType = em.HasComponent<MarketInfo>(_target)
            ? em.GetComponentData<MarketInfo>(_target).Type
            : MarketType.SELL;
        _typeDropdown.SetSelectedValue(_marketType.ToString());

        var selection = GetCurrentSelection(em, _target);
        _selectedItem = selection.Item.ItemId.ToString();
        _selectedQuantity = selection.Item.Quantity > 0 ? selection.Item.Quantity : 1;

        if (_itemDropdown.Value.choices.Contains(_selectedItem))
            _itemDropdown.SetSelectedValue(_selectedItem);
        _quantityField.SetValue(_selectedQuantity.ToString());

        UpdateInfo();
    }

    private void LoadItemChoices(EntityManager em)
    {
        var defs = QuerryDB.QueryDefinitions<ItemDefinition>(DataType.Item)
            .Where(def => def.Id != "coin")
            .OrderBy(def => def.Id)
            .ToList();

        _itemIds.Clear();
        foreach (var def in defs)
            _itemIds.Add(def.Id);

        if (_itemIds.Count == 0)
            return;

        _itemDropdown.ClearValues();
        foreach (var itemId in _itemIds)
            _itemDropdown.AddValue(itemId);

        if (string.IsNullOrEmpty(_selectedItem) || !_itemIds.Contains(_selectedItem))
        {
            _selectedItem = _itemIds[0];
            _itemDropdown.SetSelectedValue(_selectedItem);
        }
        else
        {
            _itemDropdown.SetSelectedValue(_selectedItem);
        }
    }

    private void RefreshTargetDropdown(EntityManager em)
    {
        _targetDropdown.ClearValues();
        foreach (var target in _targets)
            _targetDropdown.AddValue(DetailUiUtility.GetEntityName(em, target));
        if (_target != Entity.Null)
            _targetDropdown.SetSelectedValue(DetailUiUtility.GetEntityName(em, _target));
    }

    private static MarketSelection GetCurrentSelection(EntityManager em, Entity market)
    {
        if (!em.HasBuffer<MarketSelection>(market))
            return new MarketSelection();

        var buffer = em.GetBuffer<MarketSelection>(market);
        return buffer.Length > 0 ? buffer[0] : default;
    }

    private void UpdateInfo()
    {
        if (_infoLabel == null)
            return;

        if (string.IsNullOrEmpty(_selectedItem) || _selectedQuantity <= 0)
        {
            _infoLabel.text = "Value : no item selected";
            return;
        }

        var totalValue = MarketManager.GetTotalValue(new Items(_selectedItem, _selectedQuantity));
        var text = $"Item: {_selectedItem} = {MarketManager.GetItemValue(_selectedItem)} | Qty: {_selectedQuantity}";
        switch (_marketType)
        {
            case MarketType.SELL:
                text += $" | Sell for {totalValue} coins";
                break;
            case MarketType.URGENT_SELL:
                text += $" | Urgent sell for {totalValue / 2} coins";
                break;
            case MarketType.BUY:
                text += $" | Buy for {totalValue} coins";
                break;
            case MarketType.URGENT_BUY:
                text += $" | Urgent buy for {totalValue * 2} coins";
                break;
        }
        _infoLabel.text = text;
    }

    private void ApplySelection()
    {
        var em = World.DefaultGameObjectInjectionWorld.EntityManager;
        if (_target == Entity.Null || !em.Exists(_target))
            return;

        if (string.IsNullOrEmpty(_selectedItem) || _selectedQuantity <= 0)
            return;

        if (MarketManager.SetMarket(em, _target, _marketType, new Items(_selectedItem, _selectedQuantity)))
        {
            _userEdited = true;
            Refresh();
        }
    }
}