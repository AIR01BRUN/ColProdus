using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.UIElements;

public class BuildingProgressScreen : TemplateUI
{
    private const float BarWidth = 80f;
    private const float AboveOffset = 38f;
    private const long RefreshIntervalMs = 100;

    private readonly Dictionary<Entity, ProgressBarTemplate> _bars = new();
    private readonly List<Entity> _stale = new();

    private EntityQuery _query;
    private World _queryWorld;

    public BuildingProgressScreen()
    {
        Template = new VisualElement();
        Template.style.position = Position.Absolute;
        Template.style.left = 0;
        Template.style.top = 0;
        Template.style.width = 0;
        Template.style.height = 0;
        Template.pickingMode = PickingMode.Ignore;

        Template.schedule.Execute(Refresh).Every(RefreshIntervalMs);

        UiEntityRegistry.RegisterSingleton(this);
    }

    public override void Refresh()
    {
        var world = World.DefaultGameObjectInjectionWorld;
        if (world == null)
        {
            Clear();
            return;
        }

        var camera = Camera.main;
        if (camera == null)
            return;

        var em = world.EntityManager;
        EnsureQuery(em, world);

        using var buildings = _query.ToEntityArray(Allocator.Temp);

        _stale.Clear();
        foreach (var pair in _bars)
            _stale.Add(pair.Key);

        for (var i = 0; i < buildings.Length; i++)
        {
            var building = buildings[i];
            _stale.Remove(building);

            var bar = GetOrCreate(building);
            UpdateBar(em, camera, building, bar);
        }

        for (var i = 0; i < _stale.Count; i++)
        {
            var entity = _stale[i];
            if (_bars.TryGetValue(entity, out var bar))
                bar.Template.RemoveFromHierarchy();

            _bars.Remove(entity);
        }
    }

    private void UpdateBar(EntityManager em, Camera camera, Entity building, ProgressBarTemplate bar)
    {
        var state = em.GetComponentData<ProcedureState>(building);
        var max = 0f;

        if (em.HasBuffer<ProcedureStep>(building))
        {
            var steps = em.GetBuffer<ProcedureStep>(building);
            var index = state.CurrentStep;
            if (index >= 0 && index < steps.Length)
            {
                var stepEntity = steps[index].Value;
                if (stepEntity != Entity.Null && em.Exists(stepEntity) && em.HasComponent<Step>(stepEntity))
                    max = em.GetComponentData<Step>(stepEntity).PtsWorkNeed;
            }
        }

        bar.InvertFill = true;
        bar.Setup(max, state.PtsWorkActual);
        Place(bar, camera, em, building);
    }

    private void Place(ProgressBarTemplate bar, Camera camera, EntityManager em, Entity building)
    {
        if (!em.HasComponent<OnBoard>(building))
        {
            bar.SetVisible(false);
            return;
        }

        var onBoard = em.GetComponentData<OnBoard>(building);
        var size = em.HasComponent<Size>(building) ? em.GetComponentData<Size>(building).Value : new int2(1, 1);
        var worldPosition = EntityGraphicsUtility.DefaultPositionInWorld(onBoard.Position, size);
        var screenPosition = camera.WorldToScreenPoint(worldPosition);

        var panel = Template.panel;
        if (screenPosition.z <= 0f || panel == null)
        {
            bar.SetVisible(false);
            return;
        }

        var panelPosition = RuntimePanelUtils.ScreenToPanel(panel, new Vector2(screenPosition.x, screenPosition.y));

        bar.SetVisible(true);
        bar.Template.style.left = screenPosition.x;
        bar.Template.style.top = screenPosition.y;
    }

    private ProgressBarTemplate GetOrCreate(Entity building)
    {
        if (_bars.TryGetValue(building, out var bar))
            return bar;

        bar = new ProgressBarTemplate();
        bar.Template.pickingMode = PickingMode.Ignore;
        Template.Add(bar.Template);
        _bars[building] = bar;
        return bar;
    }

    private void Clear()
    {
        foreach (var bar in _bars.Values)
            bar.Template.RemoveFromHierarchy();

        _bars.Clear();
    }

    private void EnsureQuery(EntityManager em, World world)
    {
        if (_queryWorld == world)
            return;

        _query = em.CreateEntityQuery(
            ComponentType.ReadOnly<ProcedureState>(),
            ComponentType.ReadOnly<StateBuilding>());

        _queryWorld = world;
    }
}
