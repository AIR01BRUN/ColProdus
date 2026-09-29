using System.Collections.Generic;
using Unity.Entities;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Écran de gestion des salaires :
///   - une ligne pour le salaire minimum commun à tous les pawns ;
///   - la liste de tous les pawns avec leur salaire, modifiable individuellement
///     (un salaire vide ou à 0 revient au salaire automatique : le maximum entre le
///     minimum commun et 10% de la valeur du pawn).
/// Chaque modification appelle SalaryManager, qui recalcule la masse salariale et
/// rafraîchit l'ItemNeed (coin) du bâtiment SalaryAdministrator : le nombre de pièces
/// nécessaires est affiché et c'est InventorySystem qui fait apporter le manque.
/// </summary>
public class SalaryTemplate : TemplateEntityUi
{
    private readonly Label _title;
    private readonly Label _summary;
    private readonly FieldTemplate _minimumField;
    private readonly VisualElement _rows;
    private readonly List<Entity> _pawns = new();

    public SalaryTemplate()
    {
        Template = UITheme.Root();

        var header = UITheme.Card();
        _title = UITheme.Title("SALARIES");
        _summary = UITheme.MutedText(string.Empty);
        header.Add(_title);
        header.Add(_summary);

        _minimumField = new FieldTemplate(0);
        _minimumField.Setup("MIN SALARY");
        _minimumField.Template.style.marginTop = 6;
        _minimumField.SetActionUpdateValie(OnMinimumChanged);
        header.Add(_minimumField.Template);

        var listCard = UITheme.Card();
        listCard.style.flexGrow = 1;
        listCard.Add(UITheme.Section("PAWNS"));

        _rows = UITheme.ScrollList();
        listCard.Add(_rows);

        Template.Add(header);
        Template.Add(listCard);

        UiEntityRegistry.RegisterSingleton(this);
    }

    public void Setup()
    {
        Refresh();
    }
    public override void Refresh()
    {
        _rows.Clear();
        _pawns.Clear();

        if (!TryGetEntityManager(out var em))
            return;

        _minimumField.SetValue(GetFormatted(SalaryManager.GetMinimumSalary(em)));

        foreach (var pawn in QuerryDB.QueryInstances<CurrentTask>(em, DataType.Pawn))
        {
            if (pawn == Entity.Null || !em.Exists(pawn))
                continue;

            _pawns.Add(pawn);
            _rows.Add(CreateRow(em, pawn));
        }

        RefreshSummary();
    }

    private VisualElement CreateRow(EntityManager em, Entity pawn)
    {
        var row = UITheme.Row();
        row.style.marginBottom = 4;

        var name = UITheme.Body(DetailUiUtility.GetEntityName(em, pawn));
        name.style.flexGrow = 1;

        var field = new FieldTemplate(0);
        field.Setup("SALARY");
        field.Template.style.width = 130;
        field.SetValue(GetFormatted(SalaryManager.GetExpectedSalary(em, pawn)));
        field.SetActionUpdateValie(() => OnSalaryChanged(em, pawn, field));

        var auto = new ButtonTemplate(1);
        auto.Template.style.marginLeft = 4;
        auto.Setup("AUTO", () => OnAutoClicked(em, pawn));

        row.Add(name);
        row.Add(field.Template);
        row.Add(auto.Template);
        return row;
    }

    private void OnMinimumChanged()
    {
        if (!TryGetEntityManager(out var em))
            return;

        if (!TryParse(_minimumField.GetValue(), out var minimum))
            return;

        SalaryManager.SetMinimumSalary(em, minimum);
        _minimumField.SetValue(GetFormatted(SalaryManager.GetMinimumSalary(em)));

        // Le minimum change tous les salaires automatiques : toute la liste est reconstruite.
        Refresh();
    }

    private void OnSalaryChanged(EntityManager em, Entity pawn, FieldTemplate field)
    {
        if (pawn == Entity.Null || !em.Exists(pawn))
            return;

        TryParse(field.GetValue(), out var amount);
        SalaryManager.SetSalary(em, pawn, amount);
        field.SetValue(GetFormatted(SalaryManager.GetExpectedSalary(em, pawn)));

        // Seul le total change : la ligne en cours d'édition n'est pas reconstruite.
        RefreshSummary();
    }

    private void OnAutoClicked(EntityManager em, Entity pawn)
    {
        if (pawn == Entity.Null || !em.Exists(pawn))
            return;

        SalaryManager.SetSalary(em, pawn, 0f);
        Refresh();
    }

    /// <summary>Met à jour les totaux (effectif, masse salariale, caisse) sans toucher aux lignes.</summary>
    private void RefreshSummary()
    {
        if (!TryGetEntityManager(out var em))
        {
            _summary.text = "no world";
            return;
        }

        var needed = SalaryManager.GetNeededCurrency(em);
        var cash = SalaryManager.GetCash(em);
        _summary.text = $"PAWNS : {_pawns.Count} | MIN : {GetFormatted(SalaryManager.GetMinimumSalary(em))} " +
                        $"| A PAYER : {needed} coin | CAISSE : {cash} coin";
    }

    private static bool TryParse(string value, out float amount)
    {
        amount = 0f;
        if (string.IsNullOrWhiteSpace(value))
            return false;

        return float.TryParse(value, System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture, out amount);
    }

    private static string GetFormatted(float value)
    {
        return ((int)value).ToString();
    }

    private static bool TryGetEntityManager(out EntityManager em)
    {
        var world = World.DefaultGameObjectInjectionWorld;
        if (world == null || !world.IsCreated)
        {
            em = default;
            return false;
        }

        em = world.EntityManager;
        return true;
    }
}
