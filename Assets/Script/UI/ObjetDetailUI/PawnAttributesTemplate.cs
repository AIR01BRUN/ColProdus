using Unity.Collections;
using Unity.Entities;
using UnityEngine.UIElements;

/// <summary>
/// Attributs d'un pawn (niveau, expérience, points d'attributs) sélectionné.
/// </summary>
public class PawnAttributesTemplate : TemplateEntityUi
{
    private readonly Label _levelLabel;
    private readonly Label _experienceLabel;
    private readonly VisualElement _attributes;

    public PawnAttributesTemplate()
    {
        Template = UITheme.Root();

        var header = UITheme.Card();
        _levelLabel = UITheme.Title(string.Empty);
        _experienceLabel = UITheme.MutedText(string.Empty);
        header.Add(_levelLabel);
        header.Add(_experienceLabel);

        var listCard = UITheme.Card();
        listCard.style.flexGrow = 1;
        listCard.Add(UITheme.Section("ATTRIBUTES"));

        _attributes = new VisualElement();
        _attributes.style.flexDirection = FlexDirection.Column;
        _attributes.style.marginTop = 4;
        listCard.Add(_attributes);

        Template.Add(header);
        Template.Add(listCard);
    }

    public override void Refresh()
    {
        _attributes.Clear();
        var em = World.DefaultGameObjectInjectionWorld.EntityManager;
        if (Entity == Entity.Null || !em.Exists(Entity) || !em.HasComponent<PawnAttributes>(Entity) || !em.HasBuffer<PawnAttributeEntry>(Entity))
            return;

        var attributes = em.GetComponentData<PawnAttributes>(Entity);
        var attributeEntries = em.GetBuffer<PawnAttributeEntry>(Entity);
        _levelLabel.text = DetailUiUtility.GetEntityName(em, Entity);
        _experienceLabel.text = $"Niveau : {attributes.Level}   Expérience : {attributes.Experience} / {attributes.ExperienceMax}";
        foreach (var entry in attributeEntries)
            AddAttribute(em, attributeEntries, entry);
    }

    private void AddAttribute(EntityManager em, DynamicBuffer<PawnAttributeEntry> attributes, PawnAttributeEntry entry)
    {
        var row = UITheme.Row();
        row.style.marginBottom = 4;

        var value = UITheme.Body($"{entry.Attribute} : {entry.Level}   Flammes : {entry.Flames}");
        value.style.flexGrow = 1;

        var addButton = new ButtonTemplate(1);
        var capturedAttribute = entry.Attribute;
        addButton.Setup("+", () => AddPoint(em, capturedAttribute));
        addButton.Template.style.width = 34;
        addButton.Template.style.minWidth = 34;

        row.Add(value);
        row.Add(addButton.Template);
        _attributes.Add(row);
    }

    private void AddPoint(EntityManager em, FixedString32Bytes attribute)
    {
        var attributes = em.GetBuffer<PawnAttributeEntry>(Entity);
        PawnAttributeGenerator.AddPoint(attributes, attribute);
        Refresh();
    }
}
