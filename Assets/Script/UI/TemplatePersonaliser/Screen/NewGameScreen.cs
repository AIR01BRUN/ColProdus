using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.UIElements;


public class NewGameScreen : TemplateUI
{
    
    public VisualElement Menu { get; private set; }
    public VisualElement Screen { get; private set; }
    public ContainerTemplate MenuContainer { get; private set; }
    public ButtonTemplate BtnValidate { get; private set; }
    public ButtonTemplate BtnCancel { get; private set; }
    public FieldTemplate NameSave { get; private set; }
    public DropdownTemplate SizeMapDropdown { get; private set; }
   

    public  NewGameScreen()
    {
        base.Template = UiTemplateLoader.Get("MenuScreen");
      
        Screen = Template.Q<VisualElement>("Screen");
        Menu =  Template.Q<VisualElement>("Menu");
        MenuContainer = new ContainerTemplate();
        MenuContainer.Setup(new ContainerOption
        {
             Alignment = ContainerAlignment.Bottom,
        }, "NEW GAME");
        Menu.Add(MenuContainer.Template);

        Template.style.width = Length.Percent(100);
        Template.style.height = Length.Percent(100);

        var form =  UiTemplateLoader.Get("MenuForm");
        MenuContainer.AddContent(form);
        form .style.width = Length.Percent(100);
        form .style.maxHeight = Length.Percent(100);
        
        var valideBtnPlacement = form.Q<VisualElement>("ValideBtnPlacement");
        var cancelBtnPlacement = form.Q<VisualElement>("CancelBtnPlacement");

        BtnValidate = new ButtonTemplate(1);
        SetSize(BtnValidate);
        BtnValidate.Setup("Start", ValideOnClick);
        valideBtnPlacement.Add(BtnValidate.Template);

        BtnCancel = new ButtonTemplate(1);
        SetSize(BtnCancel);
        BtnCancel.Setup("Cancel", CancelOnClick);
        cancelBtnPlacement.Add(BtnCancel.Template);

        var content = form.Q<VisualElement>("Content");

        NameSave = new FieldTemplate(0);
        SetSize(NameSave);
        NameSave.Setup("NAME");
        content.Add( NameSave.Template );

        SizeMapDropdown = new DropdownTemplate(0);
        SetSize(SizeMapDropdown);
        SizeMapDropdown.AddValue("16x16");
        SizeMapDropdown.AddValue("32x32");
        SizeMapDropdown.AddValue("64x64");
        SizeMapDropdown.Setup("Size Map :");
        content.Add( SizeMapDropdown .Template );

        UiEntityRegistry.RegisterSingleton(this);

    }
    public void SetSize(TemplateUI templateUI)
    {
        templateUI.Template.style.width = Length.Percent(100);
        templateUI.Template.style.marginBottom = 10;
    }

    public void ValideOnClick()
    {
                var entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
                //SaveManagementSystem.CreateSave(entityManager, NameSave.GetValue());
                NewGameManagementSystem.CreateNewGame(entityManager, GetSize());


        var root = UiEntityRegistry.GetSingleton<RootTemplate>();
        var gameScreen  = UiEntityRegistry.GetSingleton<GameScreen>();
        if(gameScreen == null)
        {
            gameScreen= new GameScreen();
        }
        root.SwapScreen(gameScreen);
    }
     public void CancelOnClick()
    {
        var root = UiEntityRegistry.GetSingleton<RootTemplate>();
        var tilteScreen = UiEntityRegistry.GetSingleton<TilteScreen>();
        root.SwapScreen(tilteScreen);
    }


    private int2 GetSize()
    {
        string clean = SizeMapDropdown.GetValue().Trim().ToLowerInvariant();
        string[] parts = clean.Split('x');

        int.TryParse(parts[0], out int width);
        int.TryParse(parts[1], out int height);

        return new int2( width , height);
    }
}

