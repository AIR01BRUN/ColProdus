using Unity.Entities;
using UnityEngine.UIElements;

/// <summary>
/// Vue d'ensemble d'une zone de travail : niveau + liste des bâtiments.
/// </summary>
public class WorkZoneTemplate : TemplateEntityUi
{
    private readonly Label _title;
    private readonly Label _level;
    private readonly ScrollView _content;

    public WorkZoneTemplate()
    {
        Template = UITheme.Root();

        var header = UITheme.Card();
        _title = UITheme.Title(string.Empty);
        _level = UITheme.MutedText(string.Empty);
        header.Add(_title);
        header.Add(_level);

        _content = UITheme.ScrollList();

        Template.Add(header);
        Template.Add(_content);
    }

    public override void Refresh()
    {
        _content.Clear();
        var em = World.DefaultGameObjectInjectionWorld.EntityManager;
        if (Entity == Entity.Null || !em.Exists(Entity))
            return;

        _title.text = DetailUiUtility.GetEntityName(em, Entity);
        _level.text = em.HasComponent<WorkZone>(Entity)
            ? $"LEVEL : {em.GetComponentData<WorkZone>(Entity).Level}"
            : "";

        if (!em.HasBuffer<WorkZoneEntity>(Entity))
            return;

        var buildings = em.GetBuffer<WorkZoneEntity>(Entity);
        _content.Add(UITheme.Section($"BUILDINGS ({buildings.Length})"));
        foreach (var entry in buildings)
        {
            if (entry.Entity == Entity.Null || !em.Exists(entry.Entity))
                continue;
            _content.Add(UITheme.Bullet(DetailUiUtility.GetEntityName(em, entry.Entity)));
        }
    }
}
