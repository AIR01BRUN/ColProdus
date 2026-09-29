using System;
using System.Collections.Generic;
using Unity.Entities;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Panneau de détail d'un objet sélectionné (bâtiment ou zone). Un bouton par
/// composant / buffer : Items, Recipe, Procedure, Workers, Tasks, Market, Stats.
/// Pour une zone, un bouton agrégé est créé selon les composants de ses bâtiments.
/// La moitié gauche (ActionContent) reçoit les actions (BUILD / DÉPLACER / DÉTRUIRE /
/// ACTIVATE / DEACTIVATE) et les panneaux secondaires (sélection de pawn, build zone).
/// </summary>
public class ObjectDetailScreen : TemplateEntityUi
{
    public VisualElement ButtonContent { get; private set; }
    public VisualElement Container { get; private set; }
    public VisualElement ActionContent { get; private set; }

    private static readonly Dictionary<Type, TemplateEntityUi> _templateByComponentType = new();
    private static readonly Dictionary<Type, string> _templateTitles = new();

    private const float ActionButtonSize = 64f;
    private VisualElement _actionBar;
    private SelectionTemplate _buildSelection;
    private Entity _buildZone = Entity.Null;

    public ObjectDetailScreen()
    {
        Template = UiTemplateLoader.Get("ObjectDetailScreen");

        ButtonContent = Template.Q<VisualElement>("ButtonScollView");
        Container = Template.Q<VisualElement>("Content");
        ActionContent = Template.Q<VisualElement>("ActionContent");

        RegisterTemplate<WorkZone>("Zone", new WorkZoneTemplate());
        RegisterTemplate<InventoryLink>("Items", new ItemsTemplate());
        RegisterTemplate<CurrentRecipe>("Recipe", new CurrentRecipeTemplate());
        RegisterTemplate<MineState>("Levels", new MineLevelTemplate());
        RegisterTemplate<ProcedureState>("Procedure", new ProcedureTemplate());
        RegisterTemplateBuffer<WorkerAssignment>("Workers", new WorkerAssignmentTemplate());
        RegisterTemplateBuffer<TaskAvailable>("Tasks", new TaskAvailableTemplate());
        RegisterTemplateBuffer<MarketSelection>("Market", new MarketTemplate());
        RegisterTemplate<PawnAttributes>("Stats", new PawnAttributesTemplate());
        RegisterTemplate<SalaryAdministrator>("Salaries", new SalaryTemplate());

        UiEntityRegistry.RegisterSingleton(this);
        Refresh();
    }

    public static void RegisterTemplate<TComponent>(string title, TemplateEntityUi template)
        where TComponent : unmanaged, IComponentData
    {
        _templateByComponentType[typeof(TComponent)] = template;
        _templateTitles[typeof(TComponent)] = title;
    }

    public static void RegisterTemplateBuffer<TComponent>(string title, TemplateEntityUi template)
        where TComponent : unmanaged, IBufferElementData
    {
        _templateByComponentType[typeof(TComponent)] = template;
        _templateTitles[typeof(TComponent)] = title;
    }

    public void AddTemplateButton(string label, TemplateUI template)
    {
        if (template?.Template == null)
            return;

        var button = new ButtonTemplate(1);
        button.Template.style.height = Length.Percent(100);
        button.Setup(label, () => ShowTemplate(template));
        ButtonContent?.Add(button.Template);

        if (Container != null && Container.childCount == 0)
            ShowTemplate(template);
    }

    public void ShowTemplate(TemplateUI template)
    {
        if (template?.Template == null)
            return;

        Container?.Clear();
        Container?.Add(template.Template);
        template.Refresh();
    }

    public override void Refresh()
    {
        Container?.Clear();
        ButtonContent?.Clear();
        ActionContent?.Clear();

        if (Entity == Entity.Null)
            return;

        var em = World.DefaultGameObjectInjectionWorld.EntityManager;
        if (!em.Exists(Entity))
            return;

        // Building en construction : on n'affiche que sa procédure (ni actions, ni composants).
        if (em.HasComponent<StateBuilding>(Entity) &&
            em.GetComponentData<StateBuilding>(Entity).State == StateBuild.UnderConstruct)
        {
            AddConstructionProcedure(em, Entity);
            return;
        }

        RefreshActions(em, Entity);

        if (em.HasComponent<WorkZone>(Entity) && em.HasBuffer<WorkZoneEntity>(Entity))
            AddZoneButtons(em);
        else
            AddComponentButtons(em, Entity, null);
    }

    private void AddConstructionProcedure(EntityManager em, Entity target)
    {
        if (!em.HasComponent<ProcedureState>(target) || !em.HasBuffer<ProcedureStep>(target))
            return;

        var procedure = new ProcedureTemplate();
        procedure.Setup(target);
        AddTemplateButton("Procedure", procedure);
    }

    private void AddZoneButtons(EntityManager em)
    {
        var overview = new WorkZoneTemplate();
        overview.Setup(Entity);
        AddTemplateButton("Overview", overview);

        var hasInventory = false;
        var hasProcedure = false;
        var hasWorkers = false;
        var hasMarket = false;
        var hasMine = false;
        var hasSalary = false;

        var buffer = em.GetBuffer<WorkZoneEntity>(Entity);
        foreach (var entry in buffer)
        {
            var building = entry.Entity;
            if (building == Entity.Null || !em.Exists(building))
                continue;

            if (em.HasComponent<InventoryLink>(building))
                hasInventory = true;
            if (em.HasComponent<ProcedureState>(building) && em.HasBuffer<ProcedureStep>(building))
                hasProcedure = true;
            if (em.HasBuffer<WorkerAssignment>(building))
                hasWorkers = true;
            if (em.HasBuffer<MarketSelection>(building))
                hasMarket = true;
            if (em.HasComponent<MineState>(building))
                hasMine = true;
            if (em.HasComponent<SalaryAdministrator>(building))
                hasSalary = true;
        }

        if (hasInventory)
        {
            var items = new ItemsTemplate();
            items.Setup(Entity);
            AddTemplateButton("Items", items);
        }

        if (hasProcedure)
        {
            var procedure = new ProcedureTemplate();
            procedure.Setup(Entity);
            AddTemplateButton("Procedure", procedure);
        }

        if (hasWorkers)
        {
            var workers = new WorkerAssignmentTemplate();
            workers.Setup(Entity);
            AddTemplateButton("Workers", workers);
        }

        if (hasMine)
        {
            var mineLevels = new MineLevelTemplate();
            mineLevels.Setup(Entity);
            AddTemplateButton("Levels", mineLevels);
        }

        if (WorkZoneManager.TryGetBuildingWorker(em, Entity, out var checkroom) &&
            em.Exists(checkroom) && em.HasBuffer<TaskAvailable>(checkroom))
        {
            var tasks = new TaskAvailableTemplate();
            tasks.Setup(checkroom);
            AddTemplateButton("Tasks", tasks);
        }

        if (hasMarket)
        {
            var market = new MarketTemplate();
            market.Setup(Entity);
            AddTemplateButton("Market", market);
        }

        if (hasSalary)
        {
            var salaries = new SalaryTemplate();
            salaries.Setup(Entity);
            AddTemplateButton("Salaries", salaries);
        }
    }

    private void AddComponentButtons(EntityManager em, Entity target, string prefix)
    {
        foreach (var pair in _templateByComponentType)
        {
            if (!em.HasComponent(target, ComponentType.ReadOnly(pair.Key)))
                continue;

            var title = _templateTitles.TryGetValue(pair.Key, out var registeredTitle)
                ? registeredTitle
                : pair.Key.Name;

            var label = string.IsNullOrEmpty(prefix) ? title : $"{prefix} \u2022 {title}";
            var template = CloneTemplate(pair.Value);
            template.Setup(target);
            AddTemplateButton(label, template);
        }
    }

    private static TemplateEntityUi CloneTemplate(TemplateEntityUi source)
    {
        try
        {
            return (TemplateEntityUi)Activator.CreateInstance(source.GetType());
        }
        catch
        {
            return source;
        }
    }

    private void RefreshActions(EntityManager em, Entity target)
    {
        ActionContent.style.flexDirection = FlexDirection.Column;

        // Barre d'actions en bas : grands carrés alignés à droite, lus de gauche à droite.
        _actionBar = new VisualElement();
        _actionBar.style.flexDirection = FlexDirection.Row;
        _actionBar.style.justifyContent = Justify.FlexEnd;
        _actionBar.style.alignItems = Align.FlexEnd;
        _actionBar.style.flexGrow = 0;
        _actionBar.style.flexShrink = 0;
        _actionBar.style.marginTop = StyleKeyword.Auto;
        _actionBar.style.marginRight = 6;
        _actionBar.style.marginBottom = 4;
        ActionContent.Add(_actionBar);

        if (em.HasComponent<WorkZone>(target) && !em.HasComponent<ZoneRubble>(target))
            AddActionButton("BUILD", () => BuildInZone(target));

        if (em.HasComponent<MineState>(target))
            AddActionButton("CREUSER", () => MineManager.StartExcavation(em, target));

        if (em.HasComponent<ProcedureState>(target) && em.HasBuffer<ProcedureStep>(target))
        {
            var state = em.GetComponentData<ProcedureState>(target);
            var steps = em.GetBuffer<ProcedureStep>(target);
            if (state.CurrentStep >= 0 && state.CurrentStep < steps.Length)
            {
                var stepEntity = steps[state.CurrentStep].Value;
                if (stepEntity != Entity.Null && em.Exists(stepEntity) && em.HasComponent<Step>(stepEntity))
                {
                    var step = em.GetComponentData<Step>(stepEntity);
                    if (step.RequireActivation)
                        AddActionButton(
                            state.Activation ? "DEACTIVATE" : "ACTIVATE",
                            () => ToggleProcedureActivation(target));
                }
            }
        }

        if (em.HasComponent<ID>(target))
        {
            var dataType = em.GetComponentData<ID>(target).DataType;
            if (dataType == DataType.Building)
            {
                AddActionButton("DÉPLACER", () => MoveBuilding(target));
                AddActionButton("DÉTRUIRE", () => BuildingManager.Deconstruct(em, target));
            }
            else if (dataType == DataType.WorkZone)
            {
                AddActionButton("DÉPLACER", () => MoveZone(target));
                AddActionButton("DÉTRUIRE", () => WorkZoneManager.RemoveZoneFromBoard(em, target, true));
            }
        }
    }

    private void AddActionButton(string label, Action onClick)
    {
        if (_actionBar == null)
            return;

        var button = new ButtonTemplate(1);
        button.Setup(label, onClick);

        // Carré compact aligné en bas, lus de gauche à droite.
        button.Template.style.width = ActionButtonSize;
        button.Template.style.height = ActionButtonSize;
        button.Template.style.minWidth = ActionButtonSize;
        button.Template.style.minHeight = ActionButtonSize;
        button.Template.style.flexShrink = 0;
        button.Template.style.marginLeft = 0;
        button.Template.style.marginRight = 6;
        button.Template.style.marginTop = 0;
        button.Template.style.marginBottom = 0;

        if (button.Text != null)
        {
            button.Text.style.whiteSpace = WhiteSpace.Normal;
            button.Text.style.fontSize = 11;
            button.Text.style.unityTextAlign = TextAnchor.MiddleCenter;
        }

        _actionBar.Add(button.Template);
    }

    private void BuildInZone(Entity target)
    {
        var em = World.DefaultGameObjectInjectionWorld.EntityManager;
        if (target == Entity.Null || !em.Exists(target) || !em.HasComponent<ID>(target))
            return;

        var zoneId = em.GetComponentData<ID>(target).Id.ToString();
        _buildZone = target;

        if (_buildSelection == null)
            _buildSelection = new SelectionTemplate();

        // Un clic sur une case lance directement le placement du bâtiment dans la zone.
        _buildSelection.SetupDefinitions<BuildingDefinition>(
            $"BUILD IN {DetailUiUtility.GetEntityName(em, target)}",
            DataType.Building,
            1,
            SelectionTemplate.ValidationMode.SelectionOnStep,
            OnBuildingToPlace,
            null,
            null,
            def => IsBuildingAllowedInZone(def, zoneId));

        // SetSearch après Setup : celui-ci réinitialise le champ de recherche.
        _buildSelection.SetSearch(choice => choice.GetId(), "id");

        SwapAction(_buildSelection);
    }

    private static bool IsBuildingAllowedInZone(BuildingDefinition def, string zoneId)
    {
        if (def == null || def.WorkZoneIds == null || def.WorkZoneIds.Count == 0)
            return false;

        return def.WorkZoneIds.Contains(zoneId) || def.WorkZoneIds.Contains("all");
    }

    private void OnBuildingToPlace(Definition definition)
    {
        if (definition == null)
            return;

        var em = World.DefaultGameObjectInjectionWorld.EntityManager;
        PlaceOnBoardSysetem.StartPlacingBuilding(em, definition, -1, _buildZone);
    }

    private void ToggleProcedureActivation(Entity target)
    {
        var em = World.DefaultGameObjectInjectionWorld.EntityManager;
        if (target == Entity.Null || !em.Exists(target) || !em.HasComponent<ProcedureState>(target))
            return;

        var state = em.GetComponentData<ProcedureState>(target);
        state.Activation = !state.Activation;
        em.SetComponentData(target, state);
        Refresh();
    }

    private void MoveBuilding(Entity target)
    {
        var em = World.DefaultGameObjectInjectionWorld.EntityManager;
        if (target == Entity.Null || !em.Exists(target) || !em.HasComponent<ID>(target))
            return;

        var id = em.GetComponentData<ID>(target);
        var zone = Entity.Null;
        if (em.HasComponent<WorkZoneLink>(target))
        {
            var linkZone = em.GetComponentData<WorkZoneLink>(target).WorkZone;
            if (linkZone != Entity.Null && em.Exists(linkZone))
                zone = linkZone;
        }

        ActionRegistry.RemoveFromBoard(em, target);
        PlaceOnBoardSysetem.StartPlacingBuilding(em, id.ToDefinition(), id.NumId, zone);
    }

    private void MoveZone(Entity target)
    {
        var em = World.DefaultGameObjectInjectionWorld.EntityManager;
        if (target == Entity.Null || !em.Exists(target))
            return;

        WorkZoneManager.RemoveZoneFromBoard(em, target, false);
        PlaceOnBoardSysetem.StartPlacingZone(em, target);
    }

    public void SwapAction(TemplateUI template)
    {
        ActionContent.Clear();
        ActionContent.Add(template.Template);
    }
}