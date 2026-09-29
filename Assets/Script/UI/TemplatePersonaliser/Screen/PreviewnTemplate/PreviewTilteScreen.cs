using Unity.Entities;
using Unity.Mathematics;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;


public class PreviewTilteTemplate : TemplateUI
{
    
    public CaseTemplate Case { get; private set; }
    public FieldTemplate Tilte { get; private set; }

    private string _tilte ;

   

    public  PreviewTilteTemplate()
    {
        base.Template = UiTemplateLoader.Get("Preview_Tilte");
        Case = new CaseTemplate();
        Template.Q< VisualElement>("CaseContent").Add(Case.Template);
        Tilte = new FieldTemplate();
        Tilte.Setup("Name");
        Template.Q< VisualElement>("InfoContent").Add(Tilte.Template);

        UiEntityRegistry.RegisterSingleton(this);

    }
    public void Setup(string tilte)
    {
       _tilte = tilte;
       Refresh();
    }

    public override void Refresh()
    {
        Tilte.SetValue(_tilte);
    }
}

