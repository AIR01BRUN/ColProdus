using Unity.Entities;
using Unity.Mathematics;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;


public class TilteScreen : TemplateUI
{
    
    public VisualElement Menu { get; private set; }
    public VisualElement Screen { get; private set; }
    public ContainerTemplate MenuContainer { get; private set; }
    public ButtonTemplate BtnStart { get; private set; }
    public ButtonTemplate BtnPerformanceTest { get; private set; }
    public ButtonTemplate BtnLoad { get; private set; }
    public ButtonTemplate BtnOption { get; private set; }
     public ButtonTemplate BtnLeave { get; private set; }

    public TilteScreen()
    {
        Template = UiTemplateLoader.Get("MenuScreen");
      
        Screen = Template.Q<VisualElement>("Screen");
        Menu =  Template.Q<VisualElement>("Menu");
        MenuContainer = new ContainerTemplate();
        MenuContainer.Setup(new ContainerOption
        {
            Alignment = ContainerAlignment.Bottom,
        }, "COLPRODUS");
        MenuContainer.Template.style.width = Length.Percent(50);
        Menu.Add(MenuContainer.Template);

        BtnStart = new ButtonTemplate(1);
        SetSize(BtnStart);
        BtnStart.Setup("START", StartOnClick);

        BtnPerformanceTest = new ButtonTemplate(1);
        SetSize(BtnPerformanceTest);
        BtnPerformanceTest.Setup("PERFORMANCE TEST", PerformanceTestOnClick);

        BtnLoad = new ButtonTemplate(1);
        SetSize(BtnLoad);
        BtnLoad.Setup("LOAD", LoadOnClick);

        BtnOption = new ButtonTemplate(1);
        SetSize(BtnOption);
        BtnOption.Setup("OPTION", StartOnClick);

        BtnLeave = new ButtonTemplate(1);
        SetSize(BtnLeave);
        BtnLeave.Setup("LEAVE", StartOnClick);

        var FieldTemplate = new FieldTemplate(0);
         SetSize(FieldTemplate);
         FieldTemplate.Setup("NAME");

        MenuContainer.AddContent(BtnStart);
        MenuContainer.AddContent(BtnPerformanceTest);
        MenuContainer.AddContent(BtnLoad);
        MenuContainer.AddContent(BtnOption);
        MenuContainer.AddContent(BtnLeave);

        UiEntityRegistry.RegisterSingleton(this);

    }
    public void SetSize(TemplateUI templateUI)
    {
        templateUI.Template.style.width = Length.Percent(100);
        templateUI.Template.style.marginBottom = 10;
    }

    public void StartOnClick()
    {
        var newGameScreen   = UiEntityRegistry.GetSingleton<NewGameScreen>();
        if(newGameScreen  == null)
        {
            newGameScreen = new NewGameScreen();
        }

        var root = UiEntityRegistry.GetSingleton<RootTemplate>();
        root.SwapScreen(newGameScreen);
    }

    /// <summary>Lance directement une partie volumineuse pour mesurer les performances.</summary>
    public void PerformanceTestOnClick()
    {
        var entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
        NewGameManagementSystem.CreatePerformanceTestGame(entityManager);

        var root = UiEntityRegistry.GetSingleton<RootTemplate>();
        var gameScreen = UiEntityRegistry.GetSingleton<GameScreen>();
        if (gameScreen == null)
            gameScreen = new GameScreen();

        root.SwapScreen(gameScreen);
    }
     public void LoadOnClick()
    {
         var loadSaveScreen   = UiEntityRegistry.GetSingleton< LoadSaveScreenScreen >();
        if(loadSaveScreen  == null)
        {
            loadSaveScreen = new LoadSaveScreenScreen();
        }
        loadSaveScreen.Refresh();
        var root = UiEntityRegistry.GetSingleton<RootTemplate>();
        root.SwapScreen(loadSaveScreen);
      
    }
    public void OptionOnClick()
    {   
    }

     public void LeaveOnClick()
    {   
    }


    


   
}
