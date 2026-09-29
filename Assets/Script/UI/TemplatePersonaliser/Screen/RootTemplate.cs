using Unity.Entities;
using Unity.Mathematics;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;


public class RootTemplate : TemplateUI
{
    
    public VisualElement Root { get; private set; }
    public VisualElement Scene { get; private set; } // UI ON SCENE
    public VisualElement UI { get; private set; }
    public VisualElement Notification { get; private set; } //
    public TilteScreen TilteScreen { get; private set; } //
    public BuildingProgressScreen BuildingProgress { get; private set; } // ProgressBar sur les buildings de la scene

    public RootTemplate()
    {
        
        Template =  UiTemplateLoader.Get("ROOT");
        
        Root = Template.Q<VisualElement>("ROOT");
        Scene = Template.Q<VisualElement>("SCENE");
        UI = Template.Q<VisualElement>("UI");
        Notification = Template.Q<VisualElement>("NOTIF");


        Template.style.width = Length.Percent(100);
        Template.style.height = Length.Percent(100);
        Template.style.flexGrow = 1;


        TilteScreen = new TilteScreen();
        UI.Add(TilteScreen.Template);

        BuildingProgress = new BuildingProgressScreen();
        Scene.Add(BuildingProgress.Template);

        UiEntityRegistry.RegisterSingleton(this);
    }

    public void  SwapScreen(TemplateUI template)
    {
        UI.Clear();
        UI.Add(template.Template);
    }

    


   
}
