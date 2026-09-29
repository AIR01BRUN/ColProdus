using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.UIElements;


public class SelectionBuildScreen : TemplateEntityUi
{
    public VisualElement ContentBase { get; private set; }

    public VisualElement TabBar { get; private set; }

    private readonly Dictionary<string, Button> _tabButtons = new();
    private string _currentTab = "ZONE";

    public SelectionBuildScreen()
    {
        Template = UiTemplateLoader.Get("SelectionBuildScreen");

        ContentBase = Template.Q<VisualElement>("ContentBase");
        TabBar = Template.Q<VisualElement>("TabBar");

        _tabButtons["ZONE"] = Template.Q<Button>("TabZone");
        _tabButtons["DECO"] = Template.Q<Button>("TabDeco");
        _tabButtons["ZONESTOCK"] = Template.Q<Button>("TabStock");

        foreach (var pair in _tabButtons)
        {
            if (pair.Value == null)
                continue;

            var captured = pair.Key;
            pair.Value.clicked += () => SetTab(captured);
        }

        UiEntityRegistry.RegisterSingleton(this);

        SetTab(_currentTab);
        Refresh();
    }

    public void SwapContentOnClick(string type)
    {
    }

    private void SetTab(string tab)
    {
        _currentTab = tab;
        UpdateTabHighlight();
        Refresh();
    }

    private void UpdateTabHighlight()
    {
        foreach (var pair in _tabButtons)
        {
            if (pair.Value == null)
                continue;

            var selected = pair.Key == _currentTab;
            pair.Value.style.backgroundColor = selected
                ? new StyleColor(new Color(0.63f, 0.49f, 0.40f))
                : new StyleColor(new Color(0.29f, 0.29f, 0.29f));
            pair.Value.style.color = new StyleColor(Color.white);
        }
    }

    public void BuildSelect(Definition def)
    {
        var world = World.DefaultGameObjectInjectionWorld;
        var em = world.EntityManager;

        if (Entity != Entity.Null)
        {
            PlaceOnBoardSysetem.StartPlacingBuilding(em, def, -1, Entity);
        }
        else
        {
            PlaceOnBoardSysetem.StartPlacingBuilding(em, def);
        }
    }

    private void DrawZoneTab()
    {
        var workZones = QuerryDB.QueryDefinitions<WorkZoneDefinition>(DataType.WorkZone);
        foreach (var w in workZones)
        {
            var buildingCase = new CaseTemplate();
            buildingCase.Setup(w, () => BuildSelect(w));
            ContentBase.Add(buildingCase.Template);
        }
    }

    private void DrawDecoTab(EntityManager em)
    {
        var buildins = QuerryDB.QueryDefinitions<BuildingDefinition>(DataType.Building);
        foreach (var b in buildins)
        {
            if (!b.Deco)
                continue;

            var buildingCase = new CaseTemplate();
            buildingCase.Setup(b, () => BuildSelect(b));
            ContentBase.Add(buildingCase.Template);
        }
    }

    private void DrawZoneStockTab(EntityManager em)
    {
        var zones = QuerryDB.QueryInstances(DataType.WorkZone);
        foreach (var zone in zones)
        {
            if (zone == Entity.Null || !em.Exists(zone) || !em.HasComponent<ZoneRubble>(zone))
                continue;

            var id = em.GetComponentData<ID>(zone);
            var def = QuerryDB.QueryDefinitions<WorkZoneDefinition>(DataType.WorkZone, id.Id.ToString()).FirstOrDefault();

            var buildingCase = new CaseTemplate();
            buildingCase.Setup(zone, () => PlaceOnBoardSysetem.StartPlacingZone(em, zone));
            buildingCase.TilteLabel.text = def != null ? def.Id : id.Id.ToString();
            ContentBase.Add(buildingCase.Template);
        }
    }

    public override void Refresh()
    {
        var em = World.DefaultGameObjectInjectionWorld.EntityManager;
        ContentBase.Clear();

        if (Entity == Entity.Null)
        {
            if (TabBar != null)
                TabBar.style.display = DisplayStyle.Flex;

            if (_currentTab == "ZONE")
                DrawZoneTab();
            else if (_currentTab == "DECO")
                DrawDecoTab(em);
            else
                DrawZoneStockTab(em);

            return;
        }

        if (TabBar != null)
            TabBar.style.display = DisplayStyle.None;

        if (!em.Exists(Entity))
            return;

        var id = em.GetComponentData<ID>(Entity);
        var buildins = QuerryDB.QueryDefinitions<BuildingDefinition>(DataType.Building);

        foreach (var b in buildins)
        {
            if (b.WorkZoneIds.Contains(id.Id.ToString()) || b.WorkZoneIds.Contains("all") || b.Deco)
            {
                var buildingCase = new CaseTemplate();
                buildingCase.Setup(b, () => BuildSelect(b));
                ContentBase.Add(buildingCase.Template);
            }
        }
    }
}