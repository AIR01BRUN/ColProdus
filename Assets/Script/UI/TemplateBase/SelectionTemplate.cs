using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Unity.Entities;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Sélection d'éléments en trois parties :
/// 1. filtres (boutons / listes déroulantes) + champ de recherche configurable,
/// 2. tous les éléments trouvés affichés en CaseTemplate (clic = sélectionner),
/// 3. les cases sélectionnées (clic = désélectionner) + boutons valider / cancel.
///
/// Deux modes de validation :
/// SelectionOnStep : le clic fait l'action et ferme le template.
/// SelectionTwoStep : le clic sélectionne, c'est le bouton VALIDER qui applique.
/// MultiSelection (maxSelection > 1) impose SelectionTwoStep.
/// </summary>
public class SelectionTemplate : TemplateUI
{
    public enum ValidationMode
    {
        SelectionOnStep,
        SelectionTwoStep
    }

    /// <summary>Un élément proposé : une entité du jeu ou une definition.</summary>
    public class Choice
    {
        public Entity Entity = Entity.Null;
        public Definition Definition;

        public Choice(Entity entity)
        {
            Entity = entity;
        }

        public Choice(Definition definition)
        {
            Definition = definition;
        }

        public bool IsEntity => Entity != Entity.Null;

        public string GetId()
        {
            if (Definition != null)
                return Definition.Id;

            if (Entity == Entity.Null)
                return "";

            var em = World.DefaultGameObjectInjectionWorld.EntityManager;
            if (em.Exists(Entity) && em.HasComponent<ID>(Entity))
                return em.GetComponentData<ID>(Entity).Id.ToString();

            return $"Entity {Entity.Index}";
        }
    }

    private sealed class FilterState
    {
        public string Name;
        public Func<Choice, bool> Match;
        public Func<Choice, string> Info;
        public bool Active;
        public ButtonTemplate Button;
        public DropdownTemplate Dropdown;

        public VisualElement Element => (VisualElement)Dropdown?.Template ?? Button?.Template;
    }

    public VisualElement FilterContainer { get; private set; }
    public VisualElement ContentChoices { get; private set; }
    public VisualElement ContentSelection { get; private set; }
    public VisualElement SearchZone { get; private set; }
    public VisualElement QuantityZone { get; private set; }
    public Label TitleLabel { get; private set; }
    public Label CounterLabel { get; private set; }
    public ButtonTemplate BtnValidate { get; private set; }
    public ButtonTemplate BtnCancel { get; private set; }
    public FieldTemplate SearchField { get; private set; }
    public FieldTemplate QuantityField { get; private set; }

    /// <summary>Appelé à chaque changement de quantité d'un élément sélectionné.</summary>
    public Action<Choice, int> OnQuantityChanged { get; set; }

    /// <summary>Appelé dès qu'un élément est sélectionné (avant toute validation).</summary>
    public Action<Choice> OnChoiceSelected { get; set; }

    private readonly List<Choice> _choices = new();
    private readonly List<Choice> _selected = new();
    private readonly List<FilterState> _filters = new();
    private readonly Dictionary<Choice, int> _quantities = new();
    private FilterState _activeFilter;
    private Func<Choice, string> _searchProvider;
    private Choice _quantityTarget;
    private int _quantityDefault = 1;
    private bool _quantityEnabled;
    private bool _updatingQuantity;
    private string _title = "Selection";
    private int _maxSelection = 1;
    private ValidationMode _validationMode = ValidationMode.SelectionTwoStep;
    private Action<Entity> _onValidate;
    private Action<Entity> _onCancel;
    private Action<Definition> _onDefinitionSelect;
    private Action<Definition> _onDefinitionCancel;
    private Action _onClosed;

    public SelectionTemplate()
    {
        Template = UiTemplateLoader.Get("SelectionTemplate");
        TitleLabel = Template.Q<Label>("Title");
        CounterLabel = Template.Q<Label>("Counter");
        SearchZone = Template.Q<VisualElement>("SearchZone");
        QuantityZone = Template.Q<VisualElement>("QuantityZone");
        FilterContainer = Template.Q<VisualElement>("FilterContainer");
        ContentChoices = Template.Q<VisualElement>("ContentChoices");
        ContentSelection = Template.Q<VisualElement>("ContentSelection");

        SearchField = new FieldTemplate(0);
        SearchField.Setup("SEARCH");
        SearchField.SetValue("");
        SearchField.SetActionUpdateValie(Refresh);
        SearchZone?.Add(SearchField.Template);
        SearchField.SetVisible(false);

        QuantityField = new FieldTemplate(0);
        QuantityField.Setup("QTY");
        QuantityField.SetValue("1");
        QuantityField.SetActionUpdateValie(OnQuantityFieldChanged);
        QuantityZone?.Add(QuantityField.Template);
        QuantityField.SetVisible(false);

        BtnValidate = new ButtonTemplate(1);
        BtnValidate.Setup("VALIDER", Validate);
        BtnCancel = new ButtonTemplate(1);
        BtnCancel.Setup("CANCEL", Cancel);
        BtnValidate.Template.style.marginLeft = 6;
        BtnCancel.Template.style.marginLeft = 6;

        var buttons = Template.Q<VisualElement>("Buttons");
        buttons?.Add(BtnCancel.Template);
        buttons?.Add(BtnValidate.Template);

        UiEntityRegistry.RegisterSingleton(this);
        Refresh();
    }

    /// <summary>Options : MultiSelection (maxSelection > 1) et le mode de validation.</summary>
    public bool MultiSelection => _maxSelection > 1;

    public bool SelectionOnStep => _validationMode == ValidationMode.SelectionOnStep;

    public bool SelectionTwoStep => _validationMode == ValidationMode.SelectionTwoStep;

    public int MaxSelection => _maxSelection;

    /// <summary>Une multi sélection impose la validation en deux temps.</summary>
    public void SetValidationMode(ValidationMode mode)
    {
        _validationMode = MultiSelection ? ValidationMode.SelectionTwoStep : mode;
        Refresh();
    }

    /// <summary>
    /// Option quantité : disponible uniquement en SelectionTwoStep et avec une seule
    /// sélection possible. Le champ n'apparaît que si un élément est sélectionné.
    /// </summary>
    public bool CanUseQuantity => SelectionTwoStep && _maxSelection == 1;

    public bool QuantityEnabled => _quantityEnabled;

    public bool EnableQuantity(int defaultValue = 1)
    {
        if (!CanUseQuantity)
        {
            DisableQuantity();
            return false;
        }

        _quantityEnabled = true;
        _quantityDefault = Mathf.Max(1, defaultValue);
        Refresh();
        return true;
    }

    public void DisableQuantity()
    {
        _quantityEnabled = false;
        _quantityTarget = null;
        _quantities.Clear();
        if (QuantityField != null)
            QuantityField.SetVisible(false);
    }

    public int GetQuantity(Choice choice)
    {
        return choice != null && _quantities.TryGetValue(choice, out var quantity) ? quantity : 0;
    }

    public Dictionary<Choice, int> GetQuantities()
    {
        return new Dictionary<Choice, int>(_quantities);
    }

    public void SetQuantity(Choice choice, int quantity, bool notify = true)
    {
        if (!QuantityEnabled || choice == null)
            return;

        quantity = Mathf.Max(0, quantity);
        _quantities[choice] = quantity;
        _quantityTarget = choice;
        SetQuantityField(quantity.ToString());

        if (notify)
            OnQuantityChanged?.Invoke(choice, quantity);
    }

    private void OnQuantityFieldChanged()
    {
        if (_updatingQuantity || !QuantityEnabled || _quantityTarget == null)
            return;

        var quantity = 0;
        int.TryParse(QuantityField?.GetValue(), out quantity);
        SetQuantity(_quantityTarget, quantity);
    }

    private void SetQuantityField(string value)
    {
        if (QuantityField == null)
            return;

        _updatingQuantity = true;
        QuantityField.SetValue(value);
        _updatingQuantity = false;
    }

    public void SetSearch(Func<Choice, string> provider, string placeholder = "SEARCH")
    {
        _searchProvider = provider;
        if (SearchField == null)
            return;

        SearchField.Setup(placeholder);
        SearchField.SetVisible(provider != null);
    }

    /// <summary>Recherche sur une variable de composant, ex : SetSearchVariable&lt;WorkIn&gt;("IndexWorker").</summary>
    public void SetSearchVariable<TComponent>(string variableName, string placeholder = "SEARCH")
        where TComponent : unmanaged, IComponentData
    {
        SetSearch(choice => GetComponentVariableText<TComponent>(choice.Entity, variableName), placeholder);
    }

    public void DisableSearch()
    {
        SetSearch(null);
    }

    private void ResetSelection()
    {
        _selected.Clear();
        _quantities.Clear();
        _quantityTarget = null;
        _filters.Clear();
        _activeFilter = null;
        _searchProvider = null;

        if (SearchField != null)
            SearchField.SetVisible(false);
    }

    public void Setup(
        string title,
        DataType dataType,
        bool inGame = false,
        int maxSelection = 1,
        ValidationMode validationMode = ValidationMode.SelectionTwoStep,
        Action<Entity> onValidate = null,
        Action<Entity> onCancel = null,
        List<Entity> entitySelected = null,
        Action onClosed = null)
    {
        _choices.Clear();
        ResetSelection();

        foreach (var entity in QuerryDB.QueryInstances(dataType))
            _choices.Add(new Choice(entity));

        if (entitySelected != null)
        {
            foreach (var entity in entitySelected)
                _selected.Add(new Choice(entity));
        }

        _title = title;
        _maxSelection = Mathf.Max(1, maxSelection);
        _validationMode = _maxSelection > 1 ? ValidationMode.SelectionTwoStep : validationMode;
        _onValidate = onValidate;
        _onCancel = onCancel;
        _onDefinitionSelect = null;
        _onDefinitionCancel = null;
        _onClosed = onClosed;

        Refresh();
    }

    /// <summary>Sélection à partir des definitions (aucune entité nécessaire).
    /// <paramref name="filter"/> permet de ne charger que les definitions autorisées par l'appelant.</summary>
    public void SetupDefinitions<TDefinition>(
        string title,
        DataType dataType,
        int maxSelection = 1,
        ValidationMode validationMode = ValidationMode.SelectionTwoStep,
        Action<Definition> onSelect = null,
        Action<Definition> onCancel = null,
        Action onClosed = null,
        Func<TDefinition, bool> filter = null)
        where TDefinition : Definition
    {
        _choices.Clear();
        ResetSelection();

        foreach (var definition in QuerryDB.QueryDefinitions<TDefinition>(dataType))
        {
            if (filter != null && !filter(definition))
                continue;

            _choices.Add(new Choice(definition));
        }

        _title = title;
        _maxSelection = Mathf.Max(1, maxSelection);
        _validationMode = _maxSelection > 1 ? ValidationMode.SelectionTwoStep : validationMode;
        _onValidate = null;
        _onCancel = null;
        _onDefinitionSelect = onSelect;
        _onDefinitionCancel = onCancel;
        _onClosed = onClosed;

        Refresh();
    }

    public ButtonTemplate AddFilterButton(string label, Func<Entity, bool> predicate, Func<Entity, string> info = null)
    {
        return AddChoiceFilterButton(label,
            choice => choice.IsEntity && predicate(choice.Entity),
            choice => choice.IsEntity ? info?.Invoke(choice.Entity) : null);
    }

    /// <summary>Filtre sur n'importe quel choix (entité ou definition).</summary>
    public ButtonTemplate AddChoiceFilterButton(string label, Func<Choice, bool> predicate, Func<Choice, string> info = null)
    {
        var state = new FilterState
        {
            Name = label,
            Match = predicate,
            Info = info,
            Active = false
        };

        var button = new ButtonTemplate(1);
        button.Setup(label, () =>
        {
            SetActiveFilter(state);
            Refresh();
        });

        state.Button = button;
        _filters.RemoveAll(filter => filter.Name == label);
        _filters.Add(state);
        RefreshFilterButtons();
        Refresh();
        return button;
    }

    /// <summary>Filtre sous forme de liste déroulante (type, état, ...).</summary>
    public DropdownTemplate AddChoiceFilterDropdown(
        string label,
        IEnumerable<string> values,
        Func<Choice, string> keyProvider,
        Func<Choice, string> info = null)
    {
        var dropdown = new DropdownTemplate(0);
        dropdown.Setup(label);
        dropdown.ClearValues();
        dropdown.AddValues(values);

        var state = new FilterState
        {
            Name = label,
            Match = choice => string.Equals(keyProvider(choice), dropdown.GetValue(), StringComparison.OrdinalIgnoreCase),
            Info = info,
            Active = false,
            Dropdown = dropdown
        };

        dropdown.Value.RegisterValueChangedCallback(_ =>
        {
            SetActiveFilter(state);
            Refresh();
        });

        _filters.RemoveAll(filter => filter.Name == label);
        _filters.Add(state);
        FilterContainer?.Add(dropdown.Template);
        Refresh();
        return dropdown;
    }

    public DropdownTemplate AddFilterDropdown(
        string label,
        IEnumerable<string> values,
        Func<Entity, string> keyProvider,
        Func<Entity, string> info = null)
    {
        return AddChoiceFilterDropdown(label, values,
            choice => choice.IsEntity ? keyProvider(choice.Entity) : "",
            choice => choice.IsEntity ? info?.Invoke(choice.Entity) : null);
    }

    public ButtonTemplate AddHasComponentFilter<TComponent>(string label = null, bool mustHave = true)
        where TComponent : unmanaged, IComponentData
    {
        label ??= mustHave ? $"Has {typeof(TComponent).Name}" : $"No {typeof(TComponent).Name}";
        return AddFilterButton(label, entity =>
        {
            var em = World.DefaultGameObjectInjectionWorld.EntityManager;
            return em.Exists(entity) && em.HasComponent<TComponent>(entity) == mustHave;
        }, _ => mustHave ? "YES" : "NO");
    }

    public DropdownTemplate AddVariableDropdownFilter<TComponent>(
        string componentLabel,
        string variableName,
        IEnumerable<string> compareValues,
        bool compareAsEnum = false)
        where TComponent : unmanaged, IComponentData
    {
        return AddFilterDropdown(componentLabel, compareValues,
            entity => GetComponentVariableText<TComponent>(entity, variableName),
            entity => GetComponentVariableText<TComponent>(entity, variableName));
    }

    public void ClearFilters()
    {
        _activeFilter = null;
        foreach (var filter in _filters)
            filter.Active = false;

        Refresh();
    }

    public override void Refresh()
    {
        if (Template == null)
            return;

        TitleLabel.text = _title;
        CounterLabel.text = BuildCounterText();
        RefreshFilterButtons();
        DrawChoices();
        DrawSelection();
        RefreshQuantityField();

        // En OnStep le clic applique et ferme : il n'y a rien à valider.
        BtnValidate.SetVisible(SelectionTwoStep);
    }

    private string BuildCounterText()
    {
        if (!QuantityEnabled || _quantities.Count == 0)
            return $"{_selected.Count} / {_maxSelection}";

        var total = 0;
        foreach (var quantity in _quantities.Values)
            total += quantity;

        return $"{_selected.Count} / {_maxSelection}  ({total})";
    }

    private void RefreshQuantityField()
    {
        if (QuantityField == null)
            return;

        // Le champ n'est utile que si un élément est sélectionné (un seul ou plusieurs).
        QuantityField.SetVisible(QuantityEnabled && _selected.Count > 0);

        if (_quantityTarget == null || !_selected.Contains(_quantityTarget))
            _quantityTarget = _selected.Count > 0 ? _selected[_selected.Count - 1] : null;

        SetQuantityField(_quantityTarget != null ? GetQuantity(_quantityTarget).ToString() : _quantityDefault.ToString());
    }

    private void DrawChoices()
    {
        ContentChoices?.Clear();

        foreach (var choice in GetVisibleChoices())
        {
            var view = new CaseTemplate();
            view.SetName(choice.GetId());

            var captured = choice;
            view.SetOnClick(() => SelectChoice(captured));
            view.Select(_selected.Contains(choice));
            view.SetMatchHighlight(_activeFilter == null || _activeFilter.Match(choice));
            view.SetResearchInfo(GetFilterInfo(choice));

            // Pas de nombre dans les cases de sélection.
            view.SetNumberVisible(false);

            ContentChoices.Add(view.Template);
        }
    }

    private void DrawSelection()
    {
        ContentSelection?.Clear();

        foreach (var choice in _selected)
        {
            var view = new CaseTemplate();
            view.SetName(choice.GetId());

            var captured = choice;
            view.SetOnClick(() => UnselectChoice(captured));
            view.Select(true);
            view.SetMatchHighlight(true);
            view.SetResearchInfo(GetFilterInfo(choice));

            // Pas de nombre dans les cases de sélection.
            view.SetNumberVisible(false);

            ContentSelection.Add(view.Template);
        }
    }

    private string GetFilterInfo(Choice choice)
    {
        if (_activeFilter == null || _activeFilter.Info == null)
            return "";

        return _activeFilter.Info(choice) ?? "";
    }

    private IEnumerable<Choice> GetVisibleChoices()
    {
        var search = SearchField?.GetValue() ?? "";
        IEnumerable<Choice> result = _choices;

        if (_searchProvider != null && !string.IsNullOrEmpty(search))
        {
            result = result.Where(choice =>
                (_searchProvider(choice) ?? "").IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0);
        }

        if (_activeFilter != null)
            result = result.OrderByDescending(choice => _activeFilter.Match(choice));

        return result;
    }

    public void SelectChoice(Choice choice)
    {
        if (choice == null || _selected.Contains(choice))
            return;

        if (MultiSelection)
        {
            if (_selected.Count >= _maxSelection)
                return;
        }
        else
        {
            foreach (var previous in _selected.ToList())
            {
                _selected.Remove(previous);
                _quantities.Remove(previous);
                Notify(previous, false);
            }
        }

        _selected.Add(choice);

        if (QuantityEnabled)
        {
            _quantities[choice] = _quantityDefault;
            _quantityTarget = choice;
            SetQuantityField(_quantityDefault.ToString());
        }

        OnChoiceSelected?.Invoke(choice);

        // En OnStep le clic fait l'action et ferme le template.
        if (SelectionOnStep)
        {
            Notify(choice, true);
            _onClosed?.Invoke();
        }

        Refresh();
    }

    public void UnselectChoice(Choice choice)
    {
        if (choice == null || !_selected.Contains(choice))
            return;

        _selected.Remove(choice);
        _quantities.Remove(choice);

        if (_quantityTarget == choice)
        {
            _quantityTarget = _selected.Count > 0 ? _selected[_selected.Count - 1] : null;
            SetQuantityField(_quantityTarget != null ? GetQuantity(_quantityTarget).ToString() : _quantityDefault.ToString());
        }

        if (SelectionOnStep)
            Notify(choice, false);

        Refresh();
    }

    private void Validate()
    {
        // En OnStep l'action a déjà été faite au clic : il ne reste qu'à fermer.
        if (SelectionTwoStep)
        {
            foreach (var choice in _selected.ToList())
            {
                Notify(choice, true);
                if (QuantityEnabled)
                    OnQuantityChanged?.Invoke(choice, GetQuantity(choice));
            }
        }

        _onClosed?.Invoke();
    }

    private void Cancel()
    {
        if (SelectionTwoStep)
        {
            foreach (var choice in _selected.ToList())
                Notify(choice, false);
        }

        _selected.Clear();
        _quantities.Clear();
        _quantityTarget = null;
        ClearFilters();
        _onClosed?.Invoke();
    }

    private void Notify(Choice choice, bool selected)
    {
        if (choice.IsEntity)
        {
            if (selected)
                _onValidate?.Invoke(choice.Entity);
            else
                _onCancel?.Invoke(choice.Entity);
            return;
        }

        if (choice.Definition == null)
            return;

        if (selected)
            _onDefinitionSelect?.Invoke(choice.Definition);
        else
            _onDefinitionCancel?.Invoke(choice.Definition);
    }

    private void SetActiveFilter(FilterState filter)
    {
        foreach (var item in _filters)
            item.Active = false;

        if (filter != null)
        {
            filter.Active = true;
            _activeFilter = filter;
        }
        else
        {
            _activeFilter = null;
        }
    }

    private void RefreshFilterButtons()
    {
        if (FilterContainer == null)
            return;

        var children = FilterContainer.Children().ToList();
        foreach (var child in children)
            child.RemoveFromHierarchy();

        foreach (var filter in _filters)
        {
            var element = filter.Element;
            if (element == null)
                continue;

            filter.Button?.SetActive(filter.Active);
            FilterContainer.Add(element);
        }

        var clearButton = new ButtonTemplate(1);
        clearButton.Setup("Clear filters", ClearFilters);
        FilterContainer.Add(clearButton.Template);
    }

    private string GetComponentVariableText<TComponent>(Entity entity, string variableName)
        where TComponent : unmanaged, IComponentData
    {
        if (entity == Entity.Null)
            return "";

        var em = World.DefaultGameObjectInjectionWorld.EntityManager;
        if (!em.Exists(entity) || !em.HasComponent<TComponent>(entity))
            return "";

        var component = em.GetComponentData<TComponent>(entity);
        var field = typeof(TComponent).GetField(variableName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        if (field == null)
            return "";

        return field.GetValue(component)?.ToString() ?? "";
    }

    public void SetVisible(bool visible)
    {
        if (Template != null)
            Template.visible = visible;
    }
}
