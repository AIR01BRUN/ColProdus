using System.Collections.Generic;
using System.ComponentModel;
using Unity.Entities;
using UnityEngine;

public class ContainerPreviewScreen : ContainerTemplate
{
    public Entity Entity { get; private set; }
    public PreviewTilteTemplate PreviewTilte { get; private set; }
    public ButtonTemplate BtnControl { get; private set; }
    public ButtonTemplate BtnOpenNewScreen { get; private set; }
    public List<WorkerTemplate> WorkerTemplates { get; private set; }
    public ContainerPreviewScreen(int num = 1)  : base(num)
    {
        PreviewTilte = new PreviewTilteTemplate();
        PreviewTilte.Setup( "aaaaaa");
        Content.Add(PreviewTilte.Template);

        BtnControl = new ButtonTemplate(1);
        WorkerTemplates = new List<WorkerTemplate>();
        
        BtnOpenNewScreen = new ButtonTemplate(2);
        UiEntityRegistry.RegisterSingleton(this);
    }
    public void Setup(Entity entity)
    {
        if(Entity != Entity.Null) UiEntityRegistry.Unregister(Entity,this);
        Entity = entity;
         UiEntityRegistry.Register(entity, this);
        this.Setup(new ContainerOption{},"Preview");
        Refresh();
    }
    public override void Refresh()
    {
        SetVisible(true);
        Content.Clear();
        var em  = World.DefaultGameObjectInjectionWorld.EntityManager;
        var name = em.GetComponentData<ID>(Entity).Name.ToString();
        PreviewTilte.Setup( name);
        Content.Add(PreviewTilte.Template);
   

        if (em.GetComponentData<ID>(Entity).DataType == DataType.Pawn)
        {
             BtnControl.Setup(GetControlButtonText(em), ToggleControl);
            Content.Add(BtnControl.Template);
        }
        if (em.HasBuffer<WorkerAssignment>(Entity))
        {
            var workerAssignments = em.GetBuffer<WorkerAssignment>(Entity);
            foreach (var workerAsign in workerAssignments )
            {
                var workerTemplate = new WorkerTemplate();
                workerTemplate.SetupPawn(workerAsign.Pawn);
                Content.Add(workerTemplate.Template );
            }
           
        }
       
        BtnOpenNewScreen.Setup("OPEN", GoToNewScreen);
        Content.Add(BtnOpenNewScreen.Template);
    }
    public void Active(bool active)
    {
        SetVisible(active);
    }

    private string GetControlButtonText(EntityManager em)
    {
        return em.HasComponent<CrontolThis>(Entity) ? "Remove Control" : "Add Control";
    }

    private void ToggleControl()
    {
        var em = World.DefaultGameObjectInjectionWorld.EntityManager;

        if (em.HasComponent<CrontolThis>(Entity))
        {
            em.RemoveComponent<CrontolThis>(Entity);
        }
        else
        {
            em.AddComponent<CrontolThis>(Entity);
        }

        Refresh();
    }
       private void GoToNewScreen()
    {
        var root = UiEntityRegistry.GetSingleton<RootTemplate>();
        var screen = UiEntityRegistry.GetSingleton<ObjectDetailScreen>();
        
        if (screen == null)
        {
            screen = new ObjectDetailScreen();
        }
        screen.Setup(Entity);

        root.SwapScreen(screen);
    }

}
