using System.Collections.Generic;
using Unity.Entities;
using UnityEngine;
using UnityEngine.UIElements;

public class ScheduleTemplate : TemplateUI
{
    private readonly Dictionary<ScheduleTag, Button> _tagButtons = new();
    private readonly List<Button> _hourButtons = new();
    private VisualElement _hours;
    private Label _pawnLabel;
    private ScheduleTag _selectedTag = ScheduleTag.FreeTime;
    public ScheduleTemplate()
    {
        Template = UiTemplateLoader.Get("ScheduleTemplate");
        _hours = Template.Q<VisualElement>("Hours");
        _pawnLabel = Template.Q<Label>("PawnLabel");

        CreateTagButton(ScheduleTag.Work, "WORK");
        CreateTagButton(ScheduleTag.FreeTime, "FREE TIME");
        CreateTagButton(ScheduleTag.Lunch, "LUNCH");
        CreateTagButton(ScheduleTag.Sleep, "SLEEP");
        CreateHourButtons();
        SelectTag(ScheduleTag.FreeTime);
    }

    public void Setup()
    {
        Refresh();
    }

    private void CreateTagButton(ScheduleTag tag, string text)
    {
        var button = new Button { text = text };
        button.style.height = 42;
        button.style.minWidth = 120;
        button.style.marginBottom = 8;
        button.clicked += () => SelectTag(tag);
        Template.Q<VisualElement>("Tags").Add(button);
        _tagButtons.Add(tag, button);
    }

    private void CreateHourButtons()
    {
        for (var hour = 0; hour < 24; hour++)
        {
            var selectedHour = hour;
            var button = new Button { text = FormatHour(selectedHour, ScheduleTag.FreeTime) };
            button.style.width = 78;
            button.style.height = 58;
            button.style.marginRight = 6;
            button.style.marginBottom = 6;
            button.clicked += () => AssignTag(selectedHour);
            _hours.Add(button);
            _hourButtons.Add(button);
        }
    }

    private void SelectTag(ScheduleTag tag)
    {
        _selectedTag = tag;
        foreach (var pair in _tagButtons)
        {
            pair.Value.style.backgroundColor = pair.Key == tag
                ? new StyleColor(new Color(0.22f, 0.48f, 0.68f))
                : new StyleColor(new Color(0.16f, 0.16f, 0.16f));
        }
    }

    private void AssignTag(int hour)
    {
        var world = World.DefaultGameObjectInjectionWorld;
        if (world == null) return;

        ScheduleManager.SetTag(world.EntityManager, hour, _selectedTag);
        Refresh();
    }

    public override void Refresh()
    {
        var world = World.DefaultGameObjectInjectionWorld;
        if (world == null)
        {
            _pawnLabel.text = "WORLD SCHEDULE";
            return;
        }

        var entityManager = world.EntityManager;
        _pawnLabel.text = "WORLD SCHEDULE";
        var scheduleEntity = ScheduleManager.Create(entityManager);

        var schedule = entityManager.GetBuffer<ScheduleHour>(scheduleEntity);
        for (var hour = 0; hour < _hourButtons.Count && hour < schedule.Length; hour++)
        {
            _hourButtons[hour].text = FormatHour(hour, schedule[hour].Tag);
            _hourButtons[hour].style.backgroundColor = GetTagColor(schedule[hour].Tag);
        }
    }

    private static string FormatHour(int hour, ScheduleTag tag)
    {
        return $"{hour:00}\n{tag.ToString().ToUpperInvariant()}";
    }

    private static Color GetTagColor(ScheduleTag tag)
    {
        switch (tag)
        {
            case ScheduleTag.Work: return new Color(0.22f, 0.48f, 0.68f);
            case ScheduleTag.Lunch: return new Color(0.68f, 0.45f, 0.18f);
            case ScheduleTag.Sleep: return new Color(0.32f, 0.26f, 0.52f);
            default: return new Color(0.22f, 0.42f, 0.28f);
        }
    }
}