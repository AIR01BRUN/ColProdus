using System;
using System.Collections.Generic;
using System.IO;
using Unity.Entities;
using Unity.Mathematics;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;


public class LoadSaveScreenScreen : TemplateUI
{
    
    public VisualElement Menu { get; private set; }
    public VisualElement Screen { get; private set; }
    public ContainerTemplate MenuContainer { get; private set; }
    public ButtonTemplate BtnValidate { get; private set; }
    public ButtonTemplate BtnCancel { get; private set; }
    public FieldTemplate NameSave { get; private set; }
    public DropdownTemplate SizeMapDropdown { get; private set; }
    public VisualElement  Content { get; private set; }

    public List<FoldoutTemplate> SaveFoldouts { get; private set; } = new List<FoldoutTemplate>();
    public string SelectedSave = "";

    private Action _onClickSelectSave;
   

    public  LoadSaveScreenScreen()
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

        Content = form.Q<VisualElement>("Content");

        string saveRoot = Path.Combine(Application.persistentDataPath, "Save");


        UiEntityRegistry.RegisterSingleton(this);
        Refresh();

    }
    public void SetSize(TemplateUI templateUI)
    {
        templateUI.Template.style.width = Length.Percent(100);
        templateUI.Template.style.marginBottom = 10;
    }
    public void SelectSave(string saveName)
    {
        SelectedSave = saveName;
        BtnValidate.SetActive(true);
        
    }

    public void ValideOnClick()
    {
        GameSpeedController.Reset();

        var em  = World.DefaultGameObjectInjectionWorld.EntityManager;
        //SaveManagementSystem.Load(em, SelectedSave);

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

    public override void Refresh()
    {
        string saveRoot = Path.Combine(Application.persistentDataPath, "Save");
        var allFoldouts = new List<FoldoutTemplate>();
        Content.Clear();

        foreach (string worldFolder in Directory.GetDirectories(saveRoot))
        {
             var worldName = Path.GetFileName(worldFolder);
            var foldout = new FoldoutTemplate(0);
           
            foldout.Setup(worldName,worldFolder);
            SaveFoldouts.Add(foldout);
            Content.Add(foldout.Template);

            _onClickSelectSave += () =>
            {
                foldout.DisableSelectAll();
            };
        }

        foreach (var foldout in SaveFoldouts)
        {
            
            foreach (string saveFolder in Directory.GetDirectories(foldout.GetFolderPath()))
            {
                var newOnClick = _onClickSelectSave;
                newOnClick  += () =>
                {
                    SelectSave(Path.Combine(foldout.GetFolderPath(), Path.GetFileName(saveFolder)));
                };
                foldout.AddValue(Path.GetFileName(saveFolder), newOnClick);
            }
        }
        
    }
}

