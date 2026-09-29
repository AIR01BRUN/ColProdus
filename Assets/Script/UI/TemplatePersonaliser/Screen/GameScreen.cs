using System.Linq;
using Unity.Entities;
using Unity.Mathematics;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;


public class GameScreen : TemplateUI
{
    
    public VisualElement Menu { get; private set; }
    public VisualElement SelecedtContent { get; private set; }
    public VisualElement TimeContent { get; private set; }
    public VisualElement AllItemContent { get; private set; }

    public ButtonTemplate BtnOption { get; private set; }
    public ButtonTemplate BtnBuild { get; private set; }
    public ButtonTemplate BtnCodex { get; private set; }
    public ButtonTemplate BtnSchedule { get; private set; }
    public ButtonTemplate BtnSalary { get; private set; }
    public ButtonTemplate BtnPawn { get; private set; }

    public TimeTemplate Time { get; private set; }
    public AllItemTemplate AllItems { get; private set; }

   


    public  GameScreen()
    {
        Template = UiTemplateLoader.Get("GameScreen");
        Template.style.width = Length.Percent(100);
        Template.style.height = Length.Percent(100);

        SelecedtContent = Template.Q<VisualElement>("SelecedtContent");
        TimeContent      = Template.Q<VisualElement>("TimeContent");
        AllItemContent   = Template.Q<VisualElement>("AllItemContent");

        Menu =  Template.Q<VisualElement>("Menu");
    
        BtnBuild  = new ButtonTemplate(2);
        SetSize(BtnBuild );
        BtnBuild .Setup("BUILD", BuildOnClick);
        Menu.Add(BtnBuild.Template);

        BtnCodex  = new ButtonTemplate(2);
        SetSize(BtnCodex);
        BtnCodex  .Setup("CODEX", CodexOnClick);
        Menu.Add(BtnCodex .Template);

        BtnSchedule = new ButtonTemplate(2);
        SetSize(BtnSchedule);
        BtnSchedule.Setup("SCHEDULE", ScheduleOnClick);
        Menu.Add(BtnSchedule.Template);

        BtnSalary = new ButtonTemplate(2);
        SetSize(BtnSalary);
        BtnSalary.Setup("SALARY", SalaryOnClick);
        Menu.Add(BtnSalary.Template);

        
        BtnOption  = new ButtonTemplate(2);
        SetSize(BtnOption );
        BtnOption .Setup("OPTION", OptionOnClick);
        Menu.Add(BtnOption.Template);

        BtnPawn = new ButtonTemplate(2);
        SetSize(BtnPawn);
        BtnPawn.Setup("PAWN", SpawnPawnOnClick);
        Menu.Add(BtnPawn.Template);

        Time = new TimeTemplate();
        TimeContent.Add(Time.Template);

        AllItems = new AllItemTemplate();
        AllItemContent.Add(AllItems.Template);
        AllItems.Template.style.width = 320;

        UiEntityRegistry.RegisterSingleton(this);

    }
    public void SetSize(TemplateUI templateUI)
    {
       
        templateUI.Template.style.flexGrow = 1; 
        templateUI.Template.style.height = 50;
    }

public void OptionOnClick()
    {
        GameSpeedController.Pause();
       
        var pauseScreen   = UiEntityRegistry.GetSingleton<PauseScreen>();
        if(pauseScreen  == null)
        {
            pauseScreen = new PauseScreen();
        }

        var root = UiEntityRegistry.GetSingleton<RootTemplate>();
        root.SwapScreen(pauseScreen);
    }
    public void BuildOnClick()
    {
        var selectBuildScreen = UiEntityRegistry.GetSingleton<SelectionBuildScreen>();
        if(selectBuildScreen == null)
        {
            selectBuildScreen = new SelectionBuildScreen();
        }
        SwapContent(selectBuildScreen);
      
    }
    public void CodexOnClick()
    {
    }
    public void ScheduleOnClick()
    {
        var schedule = new ScheduleTemplate();
        schedule.Setup();
        SwapContent(schedule);
    }

    public void SalaryOnClick()
    {
        var salary = UiEntityRegistry.GetSingleton<SalaryTemplate>();
        if (salary == null)
        {
            salary = new SalaryTemplate();
        }

        salary.Setup();
        SwapContent(salary);
    }
    public void SpawnPawnOnClick()
    {
        var em = World.DefaultGameObjectInjectionWorld.EntityManager;
        var board = QuerryDB.QueryInstances(DataType.Board).FirstOrDefault();
        if (board == Entity.Null || !em.Exists(board))
            return;

        var basePosition = new int2(0, 10);
        const int radius = 3;
        var position = BoardUtility.GetRandomFreePosInCircle(em, board, basePosition, radius);
        if (position.x == -1 && position.y == -1)
            return;

        var pawn = PawnManager.Create(em);
        BoardManager.AddOn(em, pawn, board, position, 0, true);
    }
    public void  SwapContent(TemplateUI template)
    {
        SelecedtContent.Clear();
        SelecedtContent.Add(template.Template);
    }

  


    


   
}
