using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Entities;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Inventaire global du joueur : la quantité totale de chaque item de tous les
/// inventaires du jeu, avec deux affichages.
///   1. la liste complète : nom + quantité de chaque item possédé ;
///   2. des listes déroulantes : un groupe par type, un dropdown par sous-type,
///      qui liste les items de ce sous-type avec leur quantité.
/// Les dropdowns gardent l'item sélectionné d'un rafraîchissement à l'autre.
/// </summary>
public class AllItemTemplate : TemplateUI
{
    private class Entry
    {
        public ItemDefinition Def;
        public int Quantity;
    }

    /// <summary>Un dropdown de sous-type : les items qu'il propose et le label de quantité.</summary>
    private class SubTypeGroup
    {
        public DropdownTemplate Dropdown;
        public Label QuantityLabel;
    }

    private static readonly string ListMode = "LISTE";
    private static readonly string DropdownMode = "PAR TYPE";

    private readonly ScrollView _listContent;
    private readonly ScrollView _dropdownContent;
    private readonly VisualElement _headerButtons;
    private readonly Label _info;

    private readonly List<Entry> _all = new List<Entry>();
    private readonly Dictionary<string, SubTypeGroup> _groups = new Dictionary<string, SubTypeGroup>();
    private readonly Dictionary<string, string> _selectedByGroup = new Dictionary<string, string>();

    private readonly ButtonTemplate _btnList;
    private readonly ButtonTemplate _btnDropdown;

    private string _mode = ListMode;
    private int _totalQuantity;
    private bool _updating;

    public AllItemTemplate()
    {
        Template = UiTemplateLoader.Get("AllItemTemplate");

        _listContent = Template.Q<ScrollView>("ListContent");
        _dropdownContent = Template.Q<ScrollView>("DropdownContent");
        _headerButtons = Template.Q<VisualElement>("HeaderButtons");
        _info = Template.Q<Label>("LabelInfo");

        _btnList = new ButtonTemplate(1);
        _btnList.Setup(ListMode, () => SetMode(ListMode));
        _headerButtons.Add(_btnList.Template);

        _btnDropdown = new ButtonTemplate(1);
        _btnDropdown.Setup(DropdownMode, () => SetMode(DropdownMode));
        _headerButtons.Add(_btnDropdown.Template);

        RefreshModeVisibility();
        UiEntityRegistry.RegisterSingleton(this);
    }

    public override void Refresh()
    {
        var world = World.DefaultGameObjectInjectionWorld;
        if (world == null || !world.IsCreated)
        {
            SetVisible(false);
            return;
        }

        Refresh(world.EntityManager);
    }

    public void Refresh(EntityManager em)
    {
        if (em == null || !em.World.IsCreated)
        {
            SetVisible(false);
            return;
        }
        SetVisible(true);

        CollectEntries(em);

        _info.text = _all.Count > 0 ? $"{_all.Count} / {_totalQuantity}" : "";

        RenderList();
        RebuildDropdowns();
    }

    /// <summary>Total de chaque item sur tous les inventaires du jeu.</summary>
    private void CollectEntries(EntityManager em)
    {
        var totals = new Dictionary<string, int>();
        var inventories = QuerryDB.QueryInstances(DataType.Inventory);
        foreach (var inventory in inventories)
        {
            if (!em.Exists(inventory))
                continue;

            var dict = InventoryManager.GetInventoryDictionary(em, inventory);
            foreach (var kvp in dict)
            {
                if (kvp.Value <= 0)
                    continue;

                totals.TryGetValue(kvp.Key, out var quantity);
                totals[kvp.Key] = quantity + kvp.Value;
            }
        }

        _all.Clear();
        _totalQuantity = 0;
        foreach (var kvp in totals)
        {
            var def = QuerryDB.QueryDefinitions<ItemDefinition>(DataType.Item, kvp.Key).FirstOrDefault();
            if (def == null)
                continue;

            _all.Add(new Entry { Def = def, Quantity = kvp.Value });
            _totalQuantity += kvp.Value;
        }

        _all.Sort((a, b) =>
        {
            int c = string.CompareOrdinal(DefType(a), DefType(b));
            if (c != 0) return c;
            c = string.CompareOrdinal(DefSubType(a), DefSubType(b));
            if (c != 0) return c;
            return string.CompareOrdinal(a.Def.Id, b.Def.Id);
        });
    }

    /// <summary>Vue 1 : une ligne "nom xquantité" par item possédé, groupée par type.</summary>
    private void RenderList()
    {
        _listContent.Clear();

        if (_all.Count == 0)
        {
            _listContent.Add(MakeEmptyLabel("Aucun item"));
            return;
        }

        string currentType = null;
        foreach (var entry in _all)
        {
            var type = DefType(entry);
            if (type != currentType)
            {
                currentType = type;
                _listContent.Add(MakeTypeHeader(type));
            }

            _listContent.Add(MakeItemRow(entry));
        }
    }

    /// <summary>Vue 2 : un groupe par type, un dropdown par sous-type à l'intérieur.</summary>
    private void RebuildDropdowns()
    {
        // Un dropdown est reconstruit quand son sous-type change d'items, pas à chaque refresh.
        var wanted = new Dictionary<string, List<Entry>>();
        foreach (var entry in _all)
        {
            var key = GroupKey(entry);
            if (!wanted.TryGetValue(key, out var list))
            {
                list = new List<Entry>();
                wanted[key] = list;
            }
            list.Add(entry);
        }

        foreach (var key in _groups.Keys.ToList())
        {
            if (!wanted.ContainsKey(key))
            {
                _selectedByGroup.Remove(key);
                _groups.Remove(key);
            }
        }

        _dropdownContent.Clear();

        if (_all.Count == 0)
        {
            _dropdownContent.Add(MakeEmptyLabel("Aucun item"));
            return;
        }

        _updating = true;

        string currentType = null;
        var keys = wanted.Keys.ToList();
        keys.Sort(StringComparer.Ordinal);

        foreach (var key in keys)
        {
            var entries = wanted[key];
            var type = DefType(entries[0]);

            if (type != currentType)
            {
                currentType = type;
                _dropdownContent.Add(MakeTypeHeader(type));
            }

            _dropdownContent.Add(GetOrCreateGroup(key, entries));
        }

        _updating = false;
    }

    private VisualElement GetOrCreateGroup(string key, List<Entry> entries)
    {
        var choices = entries.Select(e => ChoiceText(e)).ToList();

        if (!_groups.TryGetValue(key, out var group))
        {
            var row = UITheme.Row();
            row.style.justifyContent = Justify.FlexEnd;

            var dropdown = new DropdownTemplate(0);
            dropdown.Value.RegisterValueChangedCallback(_ => OnGroupValueChanged(key));

            var quantity = UITheme.MutedText("");
            quantity.style.minWidth = 52;
            quantity.style.unityTextAlign = TextAnchor.MiddleRight;

            row.Add(dropdown.Template);
            row.Add(quantity);

            group = new SubTypeGroup { Dropdown = dropdown, QuantityLabel = quantity };
            _groups[key] = group;
        }

        var groupDropdown = group.Dropdown;
        if (!ChoicesMatch(groupDropdown, choices))
        {
            var previous = _selectedByGroup.TryGetValue(key, out var selected) ? selected : null;
            groupDropdown.ClearValues();
            groupDropdown.AddValues(choices);

            // La sélection est restaurée si l'item est toujours là, sinon premier item.
            var restore = previous != null && choices.Contains(previous) ? previous : choices[0];
            _selectedByGroup[key] = restore;
            groupDropdown.SetSelectedValue(restore);
        }

        groupDropdown.Setup(SubTypeLabel(entries[0]));

        // Le label affiche la quantité de l'item actuellement sélectionné.
        group.QuantityLabel.text = GetSelectedQuantity(entries, groupDropdown.GetSelectedValue());

        return groupDropdown.Template.parent;
    }

    private static bool ChoicesMatch(DropdownTemplate dropdown, List<string> choices)
    {
        if (dropdown.Value.choices.Count != choices.Count)
            return false;

        for (var i = 0; i < choices.Count; i++)
        {
            if (dropdown.Value.choices[i] != choices[i])
                return false;
        }

        return true;
    }

    private static string GetSelectedQuantity(List<Entry> entries, string selected)
    {
        foreach (var entry in entries)
        {
            if (ChoiceText(entry) == selected)
                return $"x{entry.Quantity}";
        }

        return "";
    }

    private void OnGroupValueChanged(string key)
    {
        if (_updating || !_groups.TryGetValue(key, out var group))
            return;

        var selected = group.Dropdown.GetSelectedValue();
        _selectedByGroup[key] = selected;

        // Seule la quantité du sous-type concerné change : pas de reconstruction.
        foreach (var entry in _all)
        {
            if (GroupKey(entry) != key)
                continue;

            if (ChoiceText(entry) == selected)
                group.QuantityLabel.text = $"x{entry.Quantity}";
            else if (group.QuantityLabel.text == $"x{entry.Quantity}")
                group.QuantityLabel.text = "";
        }
    }

    /// <summary>"id xquantité" : le dropdown montre l'item et ce qu'il en reste.</summary>
    private static string ChoiceText(Entry entry)
    {
        return $"{entry.Def.Id} x{entry.Quantity}";
    }

    private static string SubTypeLabel(Entry entry)
    {
        return DefSubType(entry).ToUpperInvariant();
    }

    private static string GroupKey(Entry entry)
    {
        return DefType(entry) + "/" + DefSubType(entry);
    }

    private static VisualElement MakeItemRow(Entry entry)
    {
        var row = UITheme.Row();
        row.style.paddingLeft = 4;
        row.style.paddingRight = 4;

        var name = UITheme.Body(entry.Def.Id);
        name.style.flexGrow = 1;
        row.Add(name);

        var quantity = UITheme.MutedText($"x{entry.Quantity}");
        quantity.style.unityTextAlign = TextAnchor.MiddleRight;
        quantity.style.minWidth = 48;
        row.Add(quantity);

        return row;
    }

    private static Label MakeTypeHeader(string type)
    {
        var header = UITheme.Section(Uppercase(type));
        header.style.marginTop = 6;
        header.style.marginBottom = 1;
        return header;
    }

    private static Label MakeEmptyLabel(string text)
    {
        var label = UITheme.MutedText(text);
        label.style.paddingTop = 8;
        label.style.paddingBottom = 8;
        return label;
    }

    private void SetMode(string mode)
    {
        _mode = mode;
        RefreshModeVisibility();
    }

    private void RefreshModeVisibility()
    {
        var list = _mode == ListMode;
        _listContent.style.display = list ? DisplayStyle.Flex : DisplayStyle.None;
        _dropdownContent.style.display = list ? DisplayStyle.None : DisplayStyle.Flex;

        _btnList.SetActive(!list);
        _btnDropdown.SetActive(list);
    }

    private static string DefType(Entry entry)
    {
        return string.IsNullOrEmpty(entry.Def.Type) ? "misc" : entry.Def.Type;
    }

    private static string DefSubType(Entry entry)
    {
        return string.IsNullOrEmpty(entry.Def.SubType) ? "none" : entry.Def.SubType;
    }

    private static string Uppercase(string value)
    {
        if (string.IsNullOrEmpty(value))
            return value;
        return char.ToUpperInvariant(value[0]) + value.Substring(1);
    }
}
