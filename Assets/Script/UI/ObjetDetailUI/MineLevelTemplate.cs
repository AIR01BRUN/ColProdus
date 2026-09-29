using System.Collections.Generic;
using System.Linq;
using Unity.Entities;
using UnityEngine;
using UnityEngine.UIElements;

public class MineLevelTemplate : TemplateEntityUi
{
    private readonly VisualElement _content;
    private readonly Dictionary<Entity, MineWidgets> _widgets = new();
    private readonly List<Entity> _mines = new();

    public MineLevelTemplate()
    {
        Template = UITheme.Root();

        var header = UITheme.Card();
        header.Add(UITheme.Title("MINE DEPTH"));

        _content = UITheme.ScrollList();
        _content.style.marginTop = 2;

        Template.Add(header);
        Template.Add(_content);
        Template.schedule.Execute(Refresh).Every(500);
    }

    public override void Refresh()
    {
        if (Template == null || Template.panel == null)
            return;

        var world = World.DefaultGameObjectInjectionWorld;
        if (world == null || Entity == Entity.Null)
            return;

        var em = world.EntityManager;
        if (!em.Exists(Entity))
            return;

        var mines = GetMines(em);
        if (mines.Count == 0)
        {
            if (_widgets.Count > 0)
            {
                _content.Clear();
                _widgets.Clear();
                _mines.Clear();
            }

            _content.Add(UITheme.MutedText("No mine in this selection."));
            return;
        }

        SyncWidgets(mines);
        foreach (var mine in mines)
            UpdateMine(em, mine);
    }

    private void SyncWidgets(List<Entity> mines)
    {
        var sameSelection = _mines.Count == mines.Count;
        if (sameSelection)
        {
            for (var index = 0; index < mines.Count; index++)
            {
                if (_mines[index] != mines[index])
                {
                    sameSelection = false;
                    break;
                }
            }
        }

        if (sameSelection)
            return;

        _content.Clear();
        _widgets.Clear();
        _mines.Clear();

        foreach (var mine in mines)
        {
            _widgets.Add(mine, CreateWidgets(mine));
            _mines.Add(mine);
        }
    }

    private MineWidgets CreateWidgets(Entity mine)
    {
        var widgets = new MineWidgets();

        var world = World.DefaultGameObjectInjectionWorld;
        if (world != null && em_HasMineState(world.EntityManager, mine))
            widgets.SelectedLevel = world.EntityManager.GetComponentData<MineState>(mine).LevelToMine;

        widgets.Root = UITheme.Card();
        widgets.Root.style.flexGrow = 1;

        widgets.Title = UITheme.Section(string.Empty);
        widgets.Status = UITheme.MutedText(string.Empty);

        widgets.Dropdown = new DropdownField("Level", new List<string> { "0" }, 0);
        widgets.Dropdown.style.color = UITheme.Text;
        widgets.Dropdown.style.backgroundColor = UITheme.Accent;
        widgets.Dropdown.style.marginTop = 4;
        widgets.Dropdown.style.marginBottom = 4;

        var capturedMine = mine;
        var capturedWidgets = widgets;
        widgets.Dropdown.RegisterValueChangedCallback(evt =>
        {
            if (!int.TryParse(evt.newValue, out var level))
                return;

            capturedWidgets.SelectedLevel = level;
            if (level <= 0)
                return;

            MineManager.SelectionLevel(World.DefaultGameObjectInjectionWorld.EntityManager, capturedMine, level);
        });

        widgets.Work = UITheme.Body(string.Empty);

        widgets.Depths = new VisualElement();
        widgets.Depths.style.flexDirection = FlexDirection.Column;

        widgets.Root.Add(widgets.Title);
        widgets.Root.Add(widgets.Status);
        widgets.Root.Add(widgets.Dropdown);
        widgets.Root.Add(widgets.Work);
        widgets.Root.Add(widgets.Depths);
        _content.Add(widgets.Root);

        return widgets;
    }

    private void UpdateMine(EntityManager em, Entity mine)
    {
        if (!_widgets.TryGetValue(mine, out var widgets))
            return;

        var mineState = em.GetComponentData<MineState>(mine);
        var busy = em.HasComponent<ProcedureState>(mine) &&
                   em.GetComponentData<ProcedureState>(mine).Activation;
        var running = em.HasComponent<TaskSend>(mine) &&
                      em.GetComponentData<TaskSend>(mine).Task != Entity.Null;
        var levels = GetLevels(em, mine);

        widgets.Title.text = $"MINE {DetailUiUtility.GetEntityName(em, mine)}";
        var last = mineState.LastResourceId.Length > 0 ? mineState.LastResourceId.ToString() : "none";
        widgets.Status.text =
            $"Unlocked depth: {mineState.DeepestLevel} | Current level: {mineState.LevelToMine} | Last resource: {last} | {(running ? "IN PROGRESS" : "IDLE")}";

        SyncDropdown(em, mine, widgets, levels, busy);

        if (widgets.SelectedLevel > 0)
            widgets.Work.text = $"LEVEL {widgets.SelectedLevel} WORK: {10f * widgets.SelectedLevel}";
        else
            widgets.Work.text = "NO LEVEL SELECTED";

        widgets.Depths.Clear();
        foreach (var level in levels)
        {
            var entries = GetLevelEntries(em, mine, level)
                .Where(entry => entry.ResourceId.Length > 0)
                .OrderByDescending(entry => entry.Percentage)
                .ToList();

            if (entries.Count == 0)
            {
                widgets.Depths.Add(UITheme.Body($"DEPTH {level} : EMPTY"));
                continue;
            }

            widgets.Depths.Add(UITheme.Body(
                $"DEPTH {level} : " + string.Join(" | ", entries.Select(entry => $"{entry.ResourceId} {entry.Percentage:0.#}%"))));
        }
    }

    private static void SyncDropdown(EntityManager em, Entity mine, MineWidgets widgets, List<int> levels, bool busy)
    {
        var choices = levels.Select(level => level.ToString()).ToList();
        var currentChoices = widgets.Dropdown.choices;
        var sameChoices = currentChoices.Count == choices.Count;
        if (sameChoices)
        {
            for (var index = 0; index < choices.Count; index++)
            {
                if (currentChoices[index] != choices[index])
                {
                    sameChoices = false;
                    break;
                }
            }
        }

        if (!sameChoices)
        {
            widgets.Dropdown.choices = choices;
            if (widgets.SelectedLevel <= 0)
                widgets.Dropdown.SetValueWithoutNotify(choices.Count > 0 ? choices[0] : "0");
        }

        if (widgets.SelectedLevel <= 0 || !levels.Contains(widgets.SelectedLevel))
            widgets.SelectedLevel = 0;

        if (widgets.Dropdown.value != widgets.SelectedLevel.ToString())
            widgets.Dropdown.SetValueWithoutNotify(widgets.SelectedLevel.ToString());

        widgets.Dropdown.SetEnabled(levels.Any(level => level > 0 && !busy));
    }

    private List<Entity> GetMines(EntityManager em)
    {
        var result = new List<Entity>();
        if (em.HasComponent<MineState>(Entity))
        {
            result.Add(Entity);
            return result;
        }

        if (!em.HasBuffer<WorkZoneEntity>(Entity))
            return result;

        foreach (var entry in em.GetBuffer<WorkZoneEntity>(Entity))
        {
            if (entry.Entity != Entity.Null && em.Exists(entry.Entity) && em.HasComponent<MineState>(entry.Entity))
                result.Add(entry.Entity);
        }

        return result;
    }

    private static bool em_HasMineState(EntityManager em, Entity mine)
    {
        return mine != Entity.Null && em.Exists(mine) && em.HasComponent<MineState>(mine);
    }

    private static List<int> GetLevels(EntityManager em, Entity mine)
    {
        var result = new List<int>();
        if (!em_HasMineState(em, mine) || !em.HasBuffer<DepthLevel>(mine))
            return result;

        foreach (var entry in em.GetBuffer<DepthLevel>(mine))
        {
            if (!result.Contains(entry.Level))
                result.Add(entry.Level);
        }

        result.Sort();
        return result;
    }

    private static List<DepthLevel> GetLevelEntries(EntityManager em, Entity mine, int level)
    {
        var result = new List<DepthLevel>();
        if (!em_HasMineState(em, mine) || !em.HasBuffer<DepthLevel>(mine))
            return result;

        foreach (var entry in em.GetBuffer<DepthLevel>(mine))
        {
            if (entry.Level == level)
                result.Add(entry);
        }

        return result;
    }

    private class MineWidgets
    {
        public VisualElement Root;
        public Label Title;
        public Label Status;
        public DropdownField Dropdown;
        public Label Work;
        public VisualElement Depths;
        public int SelectedLevel;
    }
}
