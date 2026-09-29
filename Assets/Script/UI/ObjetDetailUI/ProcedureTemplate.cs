using System.Collections.Generic;
using Unity.Entities;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Procédure d'un bâtiment ou de tous les bâtiments d'une zone : étape courante,
/// progression du travail, pawn qui travaille, entrées / sorties de l'étape.
/// Auto-refresh (l'étape / la progression changent pendant le jeu).
/// </summary>
public class ProcedureTemplate : TemplateEntityUi
{
    private readonly Label _title;
    private readonly Label _status;
    private readonly ScrollView _content;

    public ProcedureTemplate()
    {
        Template = UITheme.Root();

        var header = UITheme.Card();
        _title = UITheme.Title(string.Empty);
        _status = UITheme.MutedText(string.Empty);
        header.Add(_title);
        header.Add(_status);

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
        var em = World.DefaultGameObjectInjectionWorld.EntityManager;
        if (Entity == Entity.Null || !em.Exists(Entity))
            return;

        _title.text = em.HasComponent<WorkZone>(Entity)
            ? $"PROCEDURES ZONE {DetailUiUtility.GetEntityName(em, Entity)}"
            : $"PROCEDURE {DetailUiUtility.GetEntityName(em, Entity)}";

        if (em.HasComponent<WorkZone>(Entity) && em.HasBuffer<WorkZoneEntity>(Entity))
        {
            var count = 0;
            foreach (var entry in em.GetBuffer<WorkZoneEntity>(Entity))
            {
                var building = entry.Entity;
                if (building == Entity.Null || !em.Exists(building))
                    continue;
                if (!em.HasComponent<ProcedureState>(building) || !em.HasBuffer<ProcedureStep>(building))
                    continue;

                AddSectionTitle(DetailUiUtility.GetEntityName(em, building));
                AddProcedureBlock(em, building);
                count++;
            }
            _status.text = count == 0 ? "Aucune procédure dans la zone." : "";
        }
        else if (em.HasComponent<ProcedureState>(Entity) && em.HasBuffer<ProcedureStep>(Entity))
        {
            _status.text = "";
            AddProcedureBlock(em, Entity);
        }
        else
        {
            _status.text = "Aucune procédure.";
        }
    }

    private void AddSectionTitle(string text)
    {
        var card = UITheme.Card();
        card.Add(UITheme.Section(text));
        card.style.marginBottom = 4;
        _content.Add(card);
    }

    private void AddInfoLabel(string text)
    {
        _content.Add(UITheme.Body(text));
    }

    private void AddProcedureBlock(EntityManager em, Entity procedure)
    {
        var state = em.GetComponentData<ProcedureState>(procedure);
        var steps = em.GetBuffer<ProcedureStep>(procedure);
        if (steps.Length == 0)
        {
            AddInfoLabel("Aucune étape.");
            return;
        }

        var index = state.CurrentStep;
        if (index < 0 || index >= steps.Length)
            index = 0;

        var card = UITheme.Card();
        card.Add(UITheme.Stat("ÉTAPE", $"{index + 1} / {steps.Length}   {(state.Activation ? "ACTIVE" : "INACTIVE")}"));

        var stepEntity = steps[index].Value;
        if (stepEntity != Entity.Null && em.Exists(stepEntity) && em.HasComponent<Step>(stepEntity))
        {
            var step = em.GetComponentData<Step>(stepEntity);

            var bar = new ProgressBarTemplate();
            bar.InvertFill = true;
            bar.Setup(step.PtsWorkNeed, state.PtsWorkActual);
            bar.Template.style.position = Position.Relative;
            bar.Template.style.width = Length.Percent(100);
            bar.Template.style.marginTop = 4;
            bar.Template.style.marginBottom = 4;
            card.Add(bar.Template);

            var pawn = DetailUiUtility.GetWorkingPawn(em, procedure);
            var workingText = pawn == Entity.Null
                ? "aucun"
                : DetailUiUtility.GetEntityName(em, pawn);
            if (pawn != Entity.Null && em.Exists(pawn) && em.HasComponent<CurrentAction>(pawn))
            {
                var action = em.GetComponentData<CurrentAction>(pawn).Action;
                workingText += action != Entity.Null ? " (travaille)" : " (attente)";
            }
            card.Add(UITheme.Stat("PAWN", workingText));
            card.Add(UITheme.Stat("IN", FormatItems(em, stepEntity, true)));
            card.Add(UITheme.Stat("OUT", FormatItems(em, stepEntity, false)));
        }

        _content.Add(card);
    }

    private static string FormatItems(EntityManager em, Entity stepEntity, bool inputs)
    {
        var result = new List<string>();
        if (inputs && em.HasBuffer<StepItemIn>(stepEntity))
        {
            var items = em.GetBuffer<StepItemIn>(stepEntity);
            for (var i = 0; i < items.Length; i++)
                result.Add(FormatItem(items[i].Item.ItemId.ToString(), items[i].Item.Quantity));
        }
        else if (!inputs && em.HasBuffer<StepItemOut>(stepEntity))
        {
            var items = em.GetBuffer<StepItemOut>(stepEntity);
            for (var i = 0; i < items.Length; i++)
                result.Add(FormatItem(items[i].Item.ItemId.ToString(), items[i].Item.Quantity));
        }
        return result.Count == 0 ? "none" : string.Join(", ", result);
    }

    private static string FormatItem(string itemId, int quantity)
    {
        return $"{itemId} x{quantity}";
    }
}