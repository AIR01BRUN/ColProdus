using System;
using System.Collections.Generic;
using Unity.Entities;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Workers d'un bâtiment (ou de tous les bâtiments d'une zone) : un WorkerTemplate
/// par emplacement, affichés de gauche à droite dans un scroll horizontal.
/// Chaque case affiche le pawn, sa tâche, son action et son avancement, et permet
/// de le retirer. Le bouton ASSIGN ouvre une SelectionTemplate avec tous les pawns
/// (filtre "a un travail" + recherche sur l'id).
/// </summary>
public class WorkerAssignmentTemplate : TemplateEntityUi
{
    private const float SlotMargin = 6f;

    private readonly Label _title;
    private readonly ScrollView _scroll;
    private readonly VisualElement _row;
    private readonly List<Entity> _buildings = new();
    private SelectionTemplate _selection;
    private Action _onChanged;

    public WorkerAssignmentTemplate()
    {
        Template = UITheme.Root();

        _title = UITheme.Title("WORKERS");

        _scroll = new ScrollView(ScrollViewMode.Horizontal);
        _scroll.style.flexGrow = 1;
        _scroll.style.backgroundColor = new Color(0, 0, 0, 0);

        _row = new VisualElement();
        _row.style.flexDirection = FlexDirection.Row;
        _row.style.alignItems = Align.FlexStart;
        _row.RegisterCallback<GeometryChangedEvent>(_ => LayoutSlots());
        _scroll.Add(_row);

        var footer = UITheme.Card();
        var assignRow = UITheme.Row();
        assignRow.style.justifyContent = Justify.FlexEnd;

        var assignButton = new ButtonTemplate(1);
        assignButton.Setup("ASSIGN", OpenSelection);
        assignRow.Add(assignButton.Template);
        footer.Add(assignRow);

        Template.Add(_title);
        Template.Add(_scroll);
        Template.Add(footer);
    }

    /// <summary>Appelé après une modification pour rafraichir la liste des workers.</summary>
    public void SetOnChanged(Action onChanged)
    {
        _onChanged = onChanged;
    }

    public override void Refresh()
    {
        _row.Clear();
        _buildings.Clear();

        var em = World.DefaultGameObjectInjectionWorld.EntityManager;
        if (Entity == Entity.Null || !em.Exists(Entity))
        {
            _title.text = "WORKERS";
            return;
        }

        var isZone = em.HasComponent<WorkZone>(Entity) && em.HasBuffer<WorkZoneEntity>(Entity);
        _title.text = isZone
            ? $"WORKERS ZONE {DetailUiUtility.GetEntityName(em, Entity)}"
            : $"WORKERS {DetailUiUtility.GetEntityName(em, Entity)}";

        if (isZone)
        {
            foreach (var entry in em.GetBuffer<WorkZoneEntity>(Entity))
            {
                if (entry.Entity == Entity.Null || !em.Exists(entry.Entity))
                    continue;
                if (em.HasBuffer<WorkerAssignment>(entry.Entity))
                    _buildings.Add(entry.Entity);
            }
        }
        else if (em.HasBuffer<WorkerAssignment>(Entity))
        {
            _buildings.Add(Entity);
        }

        if (_buildings.Count == 0)
        {
            _row.Add(BuildInfoLabel("Aucun worker."));
            return;
        }

        var selected = new List<Entity>();
        for (var b = 0; b < _buildings.Count; b++)
        {
            var building = _buildings[b];
            if (isZone)
                _row.Add(BuildSectionTitle(DetailUiUtility.GetEntityName(em, building)));

            var assignments = em.GetBuffer<WorkerAssignment>(building);
            for (var i = 0; i < assignments.Length; i++)
            {
                if (assignments[i].Pawn != Entity.Null)
                    selected.Add(assignments[i].Pawn);
                AddSlot(building, i);
            }
        }

        SetupSelection(selected);
        LayoutSlots();
    }

    private void AddSlot(Entity building, int index)
    {
        var worker = new WorkerTemplate();
        worker.Setup(building, index, () => RemoveSlot(building, index));
        worker.Template.style.marginRight = SlotMargin;
        _row.Add(worker.Template);
    }

    /// <summary>De gauche à droite : retour à la ligne quand la largeur est dépassée.</summary>
    private void LayoutSlots()
    {
        if (_row.childCount == 0)
            return;

        var width = _scroll.resolvedStyle.width;
        if (width <= 0f)
            width = _row.resolvedStyle.width;
        if (width <= 0f)
            return;

        var x = 0f;
        var y = 0f;
        var lineHeight = 0f;

        foreach (var child in _row.Children())
        {
            var isSlot = !(child is Label);
            var childWidth = child.resolvedStyle.width;
            if (childWidth <= 0f)
                childWidth = 70f;

            if (isSlot && x + childWidth > width && x > 0f)
            {
                x = 0f;
                y += lineHeight;
                lineHeight = 0f;
            }

            child.style.marginLeft = x;
            child.style.marginTop = y;

            if (isSlot)
            {
                x += childWidth + SlotMargin;
                lineHeight = Mathf.Max(lineHeight, child.resolvedStyle.height > 0f ? child.resolvedStyle.height : 150f);
            }
            else
            {
                x += childWidth;
                lineHeight = Mathf.Max(lineHeight, child.resolvedStyle.height);
            }
        }
    }

    private int TotalSlots(EntityManager em)
    {
        var total = 0;
        foreach (var building in _buildings)
            total += em.GetBuffer<WorkerAssignment>(building).Length;
        return total;
    }

    private void SetupSelection(List<Entity> selected)
    {
        if (_selection == null)
            _selection = new SelectionTemplate();

        var em = World.DefaultGameObjectInjectionWorld.EntityManager;
        _selection.Setup("PAWN", DataType.Pawn, true, Mathf.Max(1, TotalSlots(em)),
            SelectionTemplate.ValidationMode.SelectionTwoStep, OnSelectPawn, OnDeselectPawn, selected);
        _selection.SetSearch(choice => GetPawnLabel(choice.Entity), "pawn id");
        _selection.AddHasComponentFilter<WorkIn>("Free", false);
        _selection.AddHasComponentFilter<WorkIn>("Working", true);
    }

    private static string GetPawnLabel(Entity pawn)
    {
        var em = World.DefaultGameObjectInjectionWorld.EntityManager;
        if (pawn == Entity.Null || !em.Exists(pawn))
            return "";

        if (!em.HasComponent<ID>(pawn))
            return $"Pawn {pawn.Index}";

        var id = em.GetComponentData<ID>(pawn);
        return $"{id.Id}_{id.NumId}";
    }

    private void RemoveSlot(Entity building, int index)
    {
        var em = World.DefaultGameObjectInjectionWorld.EntityManager;
        WorkerManager.RemoveWorker(em, building, index);
        _onChanged?.Invoke();
        Refresh();
    }

    private void OnSelectPawn(Entity pawn)
    {
        var em = World.DefaultGameObjectInjectionWorld.EntityManager;
        if (pawn == Entity.Null || !em.Exists(pawn))
            return;

        if (em.HasComponent<WorkZone>(Entity))
            AssignInZone(em, pawn);
        else if (_buildings.Count > 0)
            WorkerManager.AssignWorker(em, _buildings[0], pawn);

        _onChanged?.Invoke();
        Refresh();
    }

    private void OnDeselectPawn(Entity pawn)
    {
        var em = World.DefaultGameObjectInjectionWorld.EntityManager;
        if (pawn == Entity.Null || !em.Exists(pawn))
            return;

        if (em.HasComponent<WorkZone>(Entity))
            UnassignInZone(em, pawn);
        else if (_buildings.Count > 0)
            WorkerManager.RemoveWorker(em, _buildings[0], pawn);

        _onChanged?.Invoke();
        Refresh();
    }

    private void AssignInZone(EntityManager em, Entity pawn)
    {
        foreach (var building in _buildings)
        {
            var slots = em.GetBuffer<WorkerAssignment>(building);
            for (var i = 0; i < slots.Length; i++)
            {
                if (slots[i].Pawn != Entity.Null)
                    continue;
                WorkerManager.AssignWorker(em, building, pawn, i);
                return;
            }
        }
    }

    private void UnassignInZone(EntityManager em, Entity pawn)
    {
        if (!em.HasComponent<WorkIn>(pawn))
            return;

        var workIn = em.GetComponentData<WorkIn>(pawn);
        if (workIn.Building == Entity.Null || !_buildings.Contains(workIn.Building))
            return;

        WorkerManager.RemoveWorker(em, workIn.Building, workIn.IndexWorker);
    }

    private void OpenSelection()
    {
        if (_selection == null || _selection.Template == null)
            return;
        UiEntityRegistry.GetSingleton<ObjectDetailScreen>()?.SwapAction(_selection);
    }

    private static Label BuildSectionTitle(string text)
    {
        var label = UITheme.Section(text);
        label.style.unityTextAlign = TextAnchor.MiddleCenter;
        label.style.marginRight = 6;
        return label;
    }

    private static Label BuildInfoLabel(string text)
    {
        return UITheme.MutedText(text);
    }
}
