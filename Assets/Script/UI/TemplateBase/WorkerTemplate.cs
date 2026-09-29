using System;
using Unity.Entities;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Case d'un emplacement de worker (Worker_0.uxml) :
/// LabelName      : nom du pawn assigné à l'emplacement,
/// LabelTask      : tâche en cours du pawn,
/// LabelAction    : action en cours du pawn,
/// ProgressBarFill: avancement de l'action (masqué si l'action n'a pas de durée),
/// ButtonRemove   : visible seulement si un pawn est assigné, le retire du worker.
/// </summary>
public class WorkerTemplate : TemplateEntityUi
{
    // Durées des actions temporisées (alignées sur les constantes de leurs systèmes).
    private const float SleepDuration = 6f;
    private const float EatDuration = 2f;
    private const float TransferDuration = 2f;

    public VisualElement Image { get; private set; }
    public Label NameLabel { get; private set; }
    public Label TaskLabel { get; private set; }
    public Label ActionLabel { get; private set; }
    public VisualElement ProgressBar { get; private set; }
    public VisualElement ProgressBarFill { get; private set; }
    public Button RemoveButton { get; private set; }

    private Entity _building = Entity.Null;
    private Entity _pawn = Entity.Null;
    private int _index = -1;
    private bool _pawnMode;
    private Action _onRemove;

    public WorkerTemplate()
    {
        Template = UiTemplateLoader.Get("Worker_0");

        Image = Template.Q<VisualElement>("Image");
        NameLabel = Template.Q<Label>("LabelName");
        TaskLabel = Template.Q<Label>("LabelTask");
        ActionLabel = Template.Q<Label>("LabelAction");
        ProgressBarFill = Template.Q<VisualElement>("ProgressBarFill");
        ProgressBar = ProgressBarFill?.parent;
        RemoveButton = Template.Q<Button>("ButtonRemove");

        if (RemoveButton != null)
            RemoveButton.clicked += OnRemoveClicked;

        Refresh();
    }

    /// <summary>Emplacement d'un worker : le pawn est lu dans le buffer WorkerAssignment.</summary>
    public void Setup(Entity building, int index, Action onRemove = null)
    {
        _pawnMode = false;
        _building = building;
        _index = index;
        _onRemove = onRemove;
        base.Setup(building);
    }

    /// <summary>Affiche directement un pawn (pas d'emplacement worker derrière).</summary>
    public void SetupPawn(Entity pawn)
    {
        _pawnMode = true;
        _pawn = pawn;
        _building = Entity.Null;
        _index = -1;
        _onRemove = null;
        base.Setup(pawn);
    }

    public override void Refresh()
    {
        var em = World.DefaultGameObjectInjectionWorld.EntityManager;
        var pawn = GetAssignedPawn(em);

        if (pawn == Entity.Null || !em.Exists(pawn))
        {
            SetTexts("-", "-", "-");
            SetProgress(Entity.Null);
            SetRemoveVisible(false);
            return;
        }

        var task = em.HasComponent<CurrentTask>(pawn)
            ? em.GetComponentData<CurrentTask>(pawn).Task
            : Entity.Null;
        var action = em.HasComponent<CurrentAction>(pawn)
            ? em.GetComponentData<CurrentAction>(pawn).Action
            : Entity.Null;

        SetTexts(GetPawnName(em, pawn), GetTaskLabel(em, task), GetActionLabel(em, action));
        SetProgress(action);
        SetRemoveVisible(_onRemove != null);
    }

    private Entity GetAssignedPawn(EntityManager em)
    {
        if (_pawnMode)
            return _pawn != Entity.Null && em.Exists(_pawn) ? _pawn : Entity.Null;

        if (_building == Entity.Null || !em.Exists(_building) || !em.HasBuffer<WorkerAssignment>(_building))
            return Entity.Null;

        var assignments = em.GetBuffer<WorkerAssignment>(_building);
        if (_index < 0 || _index >= assignments.Length)
            return Entity.Null;

        var pawn = assignments[_index].Pawn;
        return pawn != Entity.Null && em.Exists(pawn) ? pawn : Entity.Null;
    }

    private void OnRemoveClicked()
    {
        _onRemove?.Invoke();
    }

    private static string GetPawnName(EntityManager em, Entity pawn)
    {
        if (!em.HasComponent<ID>(pawn))
            return $"Pawn {pawn.Index}";

        var id = em.GetComponentData<ID>(pawn);
        return $"{id.Id}_{id.NumId}";
    }

    private static string GetTaskLabel(EntityManager em, Entity task)
    {
        if (task == Entity.Null || !em.Exists(task))
            return "-";

        return DetailUiUtility.GetTaskName(em, task);
    }

    private static string GetActionLabel(EntityManager em, Entity action)
    {
        if (action == Entity.Null || !em.Exists(action))
            return "-";

        if (em.HasComponent<ID>(action))
            return em.GetComponentData<ID>(action).Id.ToString();

        return "-";
    }

    private void SetTexts(string name, string task, string action)
    {
        if (NameLabel != null)
            NameLabel.text = name;
        if (TaskLabel != null)
            TaskLabel.text = task;
        if (ActionLabel != null)
            ActionLabel.text = action;
    }

    private void SetRemoveVisible(bool visible)
    {
        if (RemoveButton != null)
            RemoveButton.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
    }

    /// <summary>Barre d'avancement de l'action. Masquée si l'action ne donne pas de durée.</summary>
    private void SetProgress(Entity action)
    {
        var em = World.DefaultGameObjectInjectionWorld.EntityManager;
        var ratio = 0f;

        if (action != Entity.Null && em.Exists(action) && TryGetProgress(em, action, out ratio))
        {
            SetProgressBar(true, ratio);
            return;
        }

        SetProgressBar(false, 0f);
    }

    private void SetProgressBar(bool visible, float ratio)
    {
        if (ProgressBar != null)
            ProgressBar.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;

        if (ProgressBarFill == null)
            return;

        ProgressBarFill.style.width = Length.Percent(Mathf.Clamp01(ratio) * 100f);
    }

    private static bool TryGetProgress(EntityManager em, Entity action, out float ratio)
    {
        ratio = 0f;

        // Travail sur une procédure : ce qui reste sur l'étape en cours.
        if (em.HasComponent<WorkAtProcedure>(action))
        {
            var procedure = em.GetComponentData<WorkAtProcedure>(action).Procedure;
            if (procedure == Entity.Null || !em.Exists(procedure) ||
                !em.HasComponent<ProcedureState>(procedure) || !em.HasBuffer<ProcedureStep>(procedure))
                return false;

            var state = em.GetComponentData<ProcedureState>(procedure);
            var steps = em.GetBuffer<ProcedureStep>(procedure);
            if (state.CurrentStep < 0 || state.CurrentStep >= steps.Length)
                return false;

            var stepEntity = steps[state.CurrentStep].Value;
            if (stepEntity == Entity.Null || !em.Exists(stepEntity) || !em.HasComponent<Step>(stepEntity))
                return false;

            var need = em.GetComponentData<Step>(stepEntity).PtsWorkNeed;
            if (need <= 0f)
                return false;

            ratio = 1f - state.PtsWorkActual / need;
            return true;
        }

        // Actions temporisées : le temps écoulé sur la durée de l'action.
        if (em.HasComponent<Sleep>(action))
        {
            ratio = em.GetComponentData<Sleep>(action).Elapsed / SleepDuration;
            return true;
        }

        if (em.HasComponent<Eat>(action))
        {
            ratio = em.GetComponentData<Eat>(action).Elapsed / EatDuration;
            return true;
        }

        if (em.HasComponent<TransferTo>(action))
        {
            ratio = em.GetComponentData<TransferTo>(action).Elapsed / TransferDuration;
            return true;
        }

        // Les déplacements n'ont pas de durée connue : pas de barre.
        return false;
    }
}
