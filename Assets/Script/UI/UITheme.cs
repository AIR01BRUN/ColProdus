using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Charte graphique commune des écrans et templates d'UI.
/// Mêmes couleurs que SelectionTemplate : fond bleu nuit, sous-panneaux bleu ardoise,
/// accents bleu clair et titres bleu sable.
/// </summary>
public static class UITheme
{
    public static readonly Color Bg = new Color32(28, 38, 51, 255);
    public static readonly Color Panel = new Color32(43, 60, 79, 255);
    public static readonly Color Accent = new Color32(67, 96, 123, 255);
    public static readonly Color Border = new Color32(109, 149, 187, 255);
    public static readonly Color TextAccent = new Color32(203, 174, 156, 255);
    public static readonly Color Muted = new Color32(159, 178, 196, 255);
    public static readonly Color Text = Color.white;

    /// <summary>Sélection et état actif : fond des boutons actifs, cadre des cases sélectionnées.</summary>
    public static readonly Color Highlight = new Color32(255, 242, 76, 255);

    /// <summary>Racine d'un template : colonne qui occupe tout l'espace disponible.</summary>
    public static VisualElement Root()
    {
        var root = new VisualElement();
        root.style.flexGrow = 1;
        root.style.flexDirection = FlexDirection.Column;
        root.style.paddingLeft = 4;
        root.style.paddingRight = 4;
        root.style.paddingTop = 2;
        root.style.paddingBottom = 4;
        return root;
    }

    /// <summary>Carte : sous-panneau arrondi utilisé pour les en-têtes et les blocs.</summary>
    public static VisualElement Card()
    {
        var card = new VisualElement();
        card.style.flexGrow = 0;
        card.style.flexShrink = 0;
        card.style.flexDirection = FlexDirection.Column;
        card.style.backgroundColor = Panel;
        card.style.borderTopWidth = 1;
        card.style.borderRightWidth = 1;
        card.style.borderBottomWidth = 1;
        card.style.borderLeftWidth = 1;
        SetBorders(card, Border);
        card.style.borderTopLeftRadius = 6;
        card.style.borderTopRightRadius = 6;
        card.style.borderBottomRightRadius = 6;
        card.style.borderBottomLeftRadius = 6;
        card.style.paddingTop = 5;
        card.style.paddingRight = 8;
        card.style.paddingBottom = 5;
        card.style.paddingLeft = 8;
        card.style.marginBottom = 6;
        return card;
    }

    public static Label Title(string text)
    {
        var label = new Label(text);
        label.style.color = Text;
        label.style.fontSize = 15;
        label.style.unityFontStyleAndWeight = FontStyle.Bold;
        label.style.unityTextAlign = TextAnchor.MiddleLeft;
        label.style.whiteSpace = WhiteSpace.Normal;
        label.style.marginTop = 0;
        label.style.marginRight = 0;
        label.style.marginBottom = 0;
        label.style.marginLeft = 0;
        return label;
    }

    /// <summary>Petit libellé d'accent (section, catégorie).</summary>
    public static Label Section(string text)
    {
        var label = new Label(text);
        label.style.color = TextAccent;
        label.style.fontSize = 11;
        label.style.unityFontStyleAndWeight = FontStyle.Bold;
        label.style.whiteSpace = WhiteSpace.Normal;
        label.style.marginTop = 4;
        label.style.marginBottom = 2;
        return label;
    }

    public static Label Body(string text)
    {
        var label = new Label(text);
        label.style.color = Text;
        label.style.fontSize = 12;
        label.style.whiteSpace = WhiteSpace.Normal;
        label.style.marginTop = 1;
        label.style.marginBottom = 1;
        return label;
    }

    public static Label MutedText(string text)
    {
        var label = new Label(text);
        label.style.color = Muted;
        label.style.fontSize = 11;
        label.style.whiteSpace = WhiteSpace.Normal;
        label.style.marginTop = 1;
        label.style.marginBottom = 1;
        return label;
    }

    /// <summary>Ligne "clé : valeur" avec la clé en accent.</summary>
    public static VisualElement Stat(string key, string value)
    {
        var row = Row();

        var keyLabel = new Label(key);
        keyLabel.style.color = TextAccent;
        keyLabel.style.fontSize = 11;
        keyLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
        keyLabel.style.width = 96;
        keyLabel.style.flexShrink = 0;

        var valueLabel = Body(value);
        valueLabel.style.flexGrow = 1;

        row.Add(keyLabel);
        row.Add(valueLabel);
        return row;
    }

    /// <summary>Ligne de liste avec une pastille accent à gauche.</summary>
    public static VisualElement Bullet(string text)
    {
        var row = Row();
        row.style.alignItems = Align.FlexStart;

        var dot = new VisualElement();
        dot.style.width = 5;
        dot.style.height = 5;
        dot.style.flexShrink = 0;
        dot.style.backgroundColor = Border;
        dot.style.borderTopLeftRadius = 3;
        dot.style.borderTopRightRadius = 3;
        dot.style.borderBottomRightRadius = 3;
        dot.style.borderBottomLeftRadius = 3;
        dot.style.marginTop = 5;
        dot.style.marginRight = 6;

        var label = Body(text);
        label.style.flexGrow = 1;

        row.Add(dot);
        row.Add(label);
        return row;
    }

    public static VisualElement Row()
    {
        var row = new VisualElement();
        row.style.flexGrow = 0;
        row.style.flexShrink = 0;
        row.style.flexDirection = FlexDirection.Row;
        row.style.alignItems = Align.Center;
        row.style.marginTop = 1;
        row.style.marginBottom = 1;
        return row;
    }

    public static VisualElement Separator()
    {
        var line = new VisualElement();
        line.style.flexGrow = 0;
        line.style.flexShrink = 0;
        line.style.height = 1;
        line.style.backgroundColor = Border;
        return line;
    }

    /// <summary>Bloc de contenu qui prend la place restante et peut défiler.</summary>
    public static ScrollView ScrollList()
    {
        var scroll = new ScrollView(ScrollViewMode.Vertical);
        scroll.style.flexGrow = 1;
        scroll.style.backgroundColor = new Color(0, 0, 0, 0);
        return scroll;
    }

    private static void SetBorders(VisualElement element, Color color)
    {
        element.style.borderTopColor = color;
        element.style.borderRightColor = color;
        element.style.borderBottomColor = color;
        element.style.borderLeftColor = color;
    }
}
