using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;
using UnityEngine.UIElements;

public class WorkerManageTemplate: TemplateEntityUi
{
    public VisualElement TopPanel { get; private set; }
    public VisualElement BottomPanel { get; private set; }
    public SelectionTemplate SelectionTemplate { get; private set; }


    public WorkerManageTemplate()
    {
        Template = UiTemplateLoader.Get("WorkerManageTemplate");
   

        TopPanel = Template.Q<VisualElement>("TopPanel");
        BottomPanel = Template.Q<VisualElement>("BottomPanel");


        SelectionTemplate = new SelectionTemplate();
       
        UiEntityRegistry.RegisterSingleton(this);
    }

    public override void Refresh()
    {
      

        var em = World.DefaultGameObjectInjectionWorld.EntityManager;
        TopPanel.Clear();

        if (Entity != Entity.Null && em.Exists(Entity) && em.HasBuffer<WorkerAssignment>(Entity))
        {
            var workerAssignments = em.GetBuffer<WorkerAssignment>(Entity);
            var workerSelect = new List<Entity>();
            for (var i = 0; i < workerAssignments.Length; i++)
            {
                var assignment = workerAssignments[i];
                var workerTemplate = new WorkerTemplate();
                workerTemplate.Setup(Entity, i);
                TopPanel.Add(workerTemplate.Template);
                if(assignment.Pawn != Entity.Null) workerSelect.Add(assignment.Pawn);
            }

             SelectionTemplate.Setup("PAWN", DataType.Pawn, true, workerAssignments.Length, SelectionTemplate.ValidationMode.SelectionTwoStep,AsignPawn,RemovePawn,  workerSelect);
             SelectionTemplate.AddHasComponentFilter<WorkIn>("WorkIn", false);
             SelectionTemplate.AddVariableDropdownFilter<WorkIn>(
                 "WorkIn",
                 "IndexWorker",
                 Enumerable.Range(0, workerAssignments.Length).Select(i => i.ToString()));
        }

   
     

        BottomPanel.Clear();
        BottomPanel.Add(SelectionTemplate.Template);
    }
    public void AsignPawn(Entity entity)
    {
        var em = World.DefaultGameObjectInjectionWorld.EntityManager;
        WorkerManager.AssignWorker(em, Entity, entity);
       
    }
    public void RemovePawn(Entity entity)
    {
        var em = World.DefaultGameObjectInjectionWorld.EntityManager;
        WorkerManager.RemoveWorker(em, Entity, entity);
       
    }

    private void ReturnToGameScreen()
    {
        var root = UiEntityRegistry.GetSingleton<RootTemplate>();
        var gameScreen = UiEntityRegistry.GetSingleton<GameScreen>();
        if (gameScreen == null)
        {
            gameScreen = new GameScreen();
        }

        root.SwapScreen(gameScreen);
    }
}
