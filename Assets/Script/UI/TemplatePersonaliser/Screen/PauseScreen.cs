using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;


public class PauseScreen : TemplateUI
{
    
    public VisualElement Menu { get; private set; }
    public VisualElement Screen { get; private set; }
    public ContainerTemplate MenuContainer { get; private set; }
    public ButtonTemplate BtnContinue { get; private set; }
    public ButtonTemplate BtnOption { get; private set; }
    public ButtonTemplate BtnLoad { get; private set; }
    public ButtonTemplate BtnSave { get; private set; }
    public ButtonTemplate BtnSaveQuit { get; private set; }
     public ButtonTemplate BtnQuit { get; private set; }

    public PauseScreen ()
    {
        Template = UiTemplateLoader.Get("MenuScreen");
      
        Screen = Template.Q<VisualElement>("Screen");
        Menu =  Template.Q<VisualElement>("Menu");
        MenuContainer = new ContainerTemplate();
        MenuContainer.Setup(new ContainerOption
        {
            Alignment = ContainerAlignment.Bottom,
        }, "PAUSE");
        MenuContainer.Template.style.width = Length.Percent(50);
        Menu.Add(MenuContainer.Template);

        BtnContinue = new ButtonTemplate(1);
        SetSize(BtnContinue);
        BtnContinue.Setup("Continue",ContinueOnClick);

          BtnOption = new ButtonTemplate(1);
        SetSize(BtnOption);
        BtnOption.Setup("OPTION", OptionOnClick);

        BtnLoad = new ButtonTemplate(1);
        SetSize(BtnLoad);
        BtnLoad.Setup("LOAD", LoadOnClick);

        BtnSave = new ButtonTemplate(1);
        SetSize(BtnSave);
        BtnSave.Setup("SAVE", SaveOnClick);

        BtnQuit = new ButtonTemplate(1);
        SetSize( BtnQuit);
        BtnQuit.Setup("QUIT GAME", QuitOnClick);



        var FieldTemplate = new FieldTemplate(0);
         SetSize(FieldTemplate);
         FieldTemplate.Setup("NAME");

        MenuContainer.AddContent(BtnContinue);
        MenuContainer.AddContent(BtnOption);
        MenuContainer.AddContent(BtnLoad);
        MenuContainer.AddContent(BtnSave);
      
        MenuContainer.AddContent(BtnQuit);

        UiEntityRegistry.RegisterSingleton(this);

    }
    public void SetSize(TemplateUI templateUI)
    {
        templateUI.Template.style.width = Length.Percent(100);
        templateUI.Template.style.marginBottom = 10;
    }

    public void OptionOnClick()
    {   
    }

    public void ContinueOnClick()
    {
        GameSpeedController.Resume();
        var root = UiEntityRegistry.GetSingleton<RootTemplate>();
        var gameScreen  = UiEntityRegistry.GetSingleton<GameScreen>();
        root.SwapScreen(gameScreen);
    }
     public void LoadOnClick()
    { 
        GameSpeedController.Resume();
    }

      public void SaveOnClick()
    {
        
        var em  = World.DefaultGameObjectInjectionWorld.EntityManager;
        Database.SaveInstance(em, "Save1");
    
    }
  

     public void QuitOnClick()
    {
        GameSpeedController.Reset();
        
        var tilteScreen  = UiEntityRegistry.GetSingleton<TilteScreen>();
        var root = UiEntityRegistry.GetSingleton<RootTemplate>();

        var em  = World.DefaultGameObjectInjectionWorld.EntityManager;
        Database.ClearAllInstances(em);

        root.SwapScreen(tilteScreen);
    }


    


   
}
