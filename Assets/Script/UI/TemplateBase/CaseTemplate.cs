using System;
using Unity.Entities;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Case cliquable ou informative basée sur Case_0.uxml.
/// Button      : zone cliquable, utile uniquement quand un choix / une action est possible.
/// LabelNameDiv: toujours affiché, porte le nom de l'item ou du building.
/// LabelNumberDiv: affiché seulement s'il y a un nombre à montrer (stock / besoin).
/// LabelResearchInfoDiv: masqué tant que l'info de research est vide.
/// </summary>
public class CaseTemplate : TemplateUI
{
    public Button Button { get; private set; }
    public VisualElement Image { get; private set; }
    public VisualElement SelectedElement { get; private set; }
    public Label StatusLabel { get; private set; }

    public VisualElement LabelNameDiv { get; private set; }
    public VisualElement LabelNumberDiv { get; private set; }
    public VisualElement LabelResearchInfoDiv { get; private set; }

    public Label NameLabel { get; private set; }
    public Label NumberLabel { get; private set; }
    public Label NeedLabel { get; private set; }
    public Label ResearchLabel { get; private set; }

    /// <summary>Ancien nom du label de titre, conservé pour les appelants existants.</summary>
    public Label TilteLabel => NameLabel;

    private Action _onClick;
    public bool Selected;

    // Une case sans action reste inerte : le bouton n'est actif que si un SetOnClick est fait.
    private bool _interactive;
    private Entity _entity = Entity.Null;
    private Definition _def = null;
    private string _name = "";
    private string _research = "";

    public CaseTemplate(int number = 0)
    {
        Template = UiTemplateLoader.Get("Case_" + number);
        Button = Template.Q<Button>("Button");
        Image = Template.Q<VisualElement>("Image");
        SelectedElement = Template.Q<VisualElement>("Selected");

        LabelNameDiv = Template.Q<VisualElement>("LabelNameDiv");
        LabelNumberDiv = Template.Q<VisualElement>("LabelNumberDiv");
        LabelResearchInfoDiv = Template.Q<VisualElement>("LabelResearchInfoDiv");

        NameLabel = Template.Q<Label>("LabelName")
                    ?? Template.Q<Label>("LavelName")
                    ?? Template.Q<Label>("TilteLabel");
        NumberLabel = Template.Q<Label>("LabelNumber");
        NeedLabel = Template.Q<Label>("LabelNeed");
        ResearchLabel = Template.Q<Label>("LabelResearch")
                        ?? Template.Q<Label>("LabelResearchInfo");

        // "10 /20" : le stock et le besoin se partagent la largeur du div.
        if (NumberLabel != null && NeedLabel != null)
        {
            NumberLabel.style.width = Length.Percent(50);
            NumberLabel.style.unityTextAlign = TextAnchor.MiddleLeft;
            NumberLabel.style.overflow = Overflow.Hidden;
            NeedLabel.style.width = Length.Percent(50);
            NeedLabel.style.unityTextAlign = TextAnchor.MiddleRight;
            NeedLabel.style.overflow = Overflow.Hidden;
        }

        StatusLabel = new Label("NONE");
        StatusLabel.name = "StatusLabel";
        StatusLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
        StatusLabel.style.fontSize = 11;
        StatusLabel.style.color = Color.white;
        StatusLabel.style.marginTop = 2;
        StatusLabel.style.marginBottom = 2;
        StatusLabel.style.whiteSpace = WhiteSpace.Normal;
        if (Image != null)
            Image.Add(StatusLabel);

        if (Button != null)
            Button.clicked += OnClicked;

        // Rien de cliquable tant qu'aucun SetOnClick n'a été fait, et aucun fond
        // de sélection tant que la case n'est pas sélectionnée.
        SetInteractive(false);
        if (SelectedElement != null)
            SelectedElement.style.backgroundColor = new Color(0, 0, 0, 0);

        Selected = false;
        SetName("");
        SetResearchInfo("");
        Refresh();
    }

    public void Setup(Entity entity, Action action)
    {
        _entity = entity;
        SetName(GetEntityName(entity));
        SetOnClick(action);
    }

    public void Setup(Definition def, Action action)
    {
        _def = def;
        SetName(def != null ? def.Id : "");
        SetOnClick(action);
    }

    /// <summary>Attribue le clic : la case devient cliquable. Sans action elle reste inerte.</summary>
    public void SetOnClick(Action action)
    {
        _onClick = action;
        SetInteractive(action != null);
    }

    private static string GetEntityName(Entity entity)
    {
        if (entity == Entity.Null)
            return "";

        var em = World.DefaultGameObjectInjectionWorld.EntityManager;
        if (em.Exists(entity) && em.HasComponent<ID>(entity))
        {
            var id = em.GetComponentData<ID>(entity);
            return $"{id.Id} {id.NumId}";
        }

        return "";
    }

    /// <summary>Nom de l'item ou du building affiché dans LabelNameDiv.</summary>
    public void SetName(string name)
    {
        _name = name ?? "";
        if (NameLabel != null)
            NameLabel.text = _name;
        if (LabelNameDiv != null)
            LabelNameDiv.visible = !string.IsNullOrEmpty(_name);
    }

    /// <summary>
    /// Nombre d'items : "stock / besoin". Le besoin est écrit dans une autre couleur
    /// si le UXML prévoit un second label, sinon il est accolé au nombre.
    /// Masque LabelNumberDiv si les deux nombres sont absents.
    /// </summary>
    public void SetNumber(int? quantity, int? need = null)
    {
        var hasQuantity = quantity.HasValue;
        var hasNeed = need.HasValue && need.Value > 0;
        var quantityText = hasQuantity ? quantity.Value.ToString() : "";

        if (NeedLabel != null)
        {
            if (NumberLabel != null)
                NumberLabel.text = quantityText;
            NeedLabel.text = hasNeed ? "/" + need.Value : "";
            NeedLabel.visible = hasNeed;
        }
        else if (NumberLabel != null)
        {
            NumberLabel.text = hasNeed ? quantityText + "/" + need.Value : quantityText;
        }

        if (LabelNumberDiv != null)
            LabelNumberDiv.visible = hasQuantity || hasNeed;
    }

    public void SetNumber(string quantity, string need = null)
    {
        var hasQuantity = !string.IsNullOrEmpty(quantity);
        var hasNeed = !string.IsNullOrEmpty(need);
        var quantityText = quantity ?? "";

        if (NeedLabel != null)
        {
            if (NumberLabel != null)
                NumberLabel.text = quantityText;
            NeedLabel.text = hasNeed ? need : "";
            NeedLabel.visible = hasNeed;
        }
        else if (NumberLabel != null)
        {
            NumberLabel.text = hasNeed ? quantityText + need : quantityText;
        }

        if (LabelNumberDiv != null)
            LabelNumberDiv.visible = hasQuantity || hasNeed;
    }

    /// <summary>
    /// Affiche ou masque le div du nombre. Les cases qui ne montrent aucun nombre
    /// (sélection par exemple) le masquent pour garder la case épurée.
    /// </summary>
    public void SetNumberVisible(bool visible)
    {
        if (LabelNumberDiv != null)
            LabelNumberDiv.visible = visible;
    }

    /// <summary>Info de research (valeur d'un item, ...). Vide => div masqué.</summary>
    public void SetResearchInfo(string text)
    {
        _research = text ?? "";
        if (ResearchLabel != null)
            ResearchLabel.text = _research;
        if (LabelResearchInfoDiv != null)
            LabelResearchInfoDiv.visible = !string.IsNullOrEmpty(_research);
    }

    public void SetStatusText(string text)
    {
        if (StatusLabel != null)
        {
            StatusLabel.text = string.IsNullOrWhiteSpace(text) ? "NONE" : text;
        }
    }

    /// <summary>
    /// Le bouton n'est utile que pour les elections : sans action il est inerte.
    /// Seul le picking est coupé, l'affichage reste identique (pas d'effet grisé).
    /// </summary>
    public void SetInteractive(bool interactive)
    {
        _interactive = interactive;
        if (Button == null)
            return;

        Button.pickingMode = interactive ? PickingMode.Position : PickingMode.Ignore;
    }

    public bool IsInteractive => _interactive;

    public void SetMatchHighlight(bool match)
    {
        if (Button != null)
            Button.style.opacity = match ? 1f : 0.55f;
    }

    public void SetVisible(bool visible)
    {
        if (Template != null)
            Template.visible = visible;
    }

    public override void Refresh()
    {
        if (Template == null)
            return;

        if (SelectedElement != null)
            SelectedElement.style.backgroundColor = Selected ? UITheme.Highlight : new Color(0, 0, 0, 0);

        if (_def != null)
            SetName(_def.Id);

        if (LabelNameDiv != null)
            LabelNameDiv.visible = !string.IsNullOrEmpty(_name);
        if (LabelResearchInfoDiv != null)
            LabelResearchInfoDiv.visible = !string.IsNullOrEmpty(_research);
    }

    private void OnClicked()
    {
        if (!_interactive)
            return;

        _onClick?.Invoke();
    }

    public void Select()
    {
        Selected = !Selected;
        Refresh();
    }

    public void Select(bool select)
    {
        Selected = select;
        Refresh();
    }
}
