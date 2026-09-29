using Unity.Entities;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Liste des tâches disponibles sur le checkroom d'une zone (ou d'un bâtiment).
/// Auto-refresh : la liste / l'attribution des tâches change pendant le jeu.
/// </summary>
public class TaskAvailableTemplate : TemplateEntityUi
{
    private readonly Label _title;
    private readonly Label _count;
    private readonly ScrollView _content;

    public TaskAvailableTemplate()
    {
        Template = UITheme.Root();

        var header = UITheme.Card();
        _title = UITheme.Title("TASKS");
        _count = UITheme.MutedText(string.Empty);
        header.Add(_title);
        header.Add(_count);

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

        _content.Clear();
        _count.text = "";
        var em = World.DefaultGameObjectInjectionWorld.EntityManager;
        if (Entity == Entity.Null || !em.Exists(Entity))
            return;

        var checkroom = Entity;
        if (em.HasComponent<WorkZone>(Entity))
        {
            WorkZoneManager.TryGetBuildingWorker(em, Entity, out checkroom);
            _title.text = $"TASKS ZONE {DetailUiUtility.GetEntityName(em, Entity)}";
            if (checkroom == Entity.Null || !em.Exists(checkroom))
            {
                AddInfo("Pas de checkroom dans la zone.");
                return;
            }
        }
        else
        {
            _title.text = $"TASKS {DetailUiUtility.GetEntityName(em, Entity)}";
        }

        if (!em.HasBuffer<TaskAvailable>(checkroom))
        {
            AddInfo("Aucune tâche disponible.");
            return;
        }

        var tasks = em.GetBuffer<TaskAvailable>(checkroom);
        if (tasks.Length == 0)
        {
            AddInfo("Aucune tâche disponible.");
            return;
        }

        _count.text = $"{tasks.Length} tâche(s)";
        for (var i = 0; i < tasks.Length; i++)
            AddTaskRow(em, tasks[i].Task);
    }

    private void AddTaskRow(EntityManager em, Entity task)
    {
        var text = task == Entity.Null || !em.Exists(task) || !em.HasComponent<Task>(task)
            ? "(tâche invalide)"
            : DetailUiUtility.DescribeTask(em, task);

        _content.Add(UITheme.Bullet(text));
    }

    private void AddInfo(string text)
    {
        _content.Add(UITheme.MutedText(text));
    }
}