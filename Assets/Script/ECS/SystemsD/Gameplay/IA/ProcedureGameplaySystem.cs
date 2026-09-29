using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;


public partial struct ProcedureGameplaySystem : ISystem
{
    public void OnCreate(ref SystemState state)
    {
    }

    public void OnUpdate(ref SystemState state)
    {
        var em = state.EntityManager;
      
        var procedureEntities = QuerryDB.QueryInstances<ProcedureState>(em);
        foreach (var entity in procedureEntities)
        {
            if (em.HasComponent<BuildingRubble>(entity))
                continue;

            var steps = em.GetBuffer<ProcedureStep>(entity);
            var procedureState = em.GetComponentData<ProcedureState>(entity);

            if (procedureState.CurrentStep < 0 || procedureState.CurrentStep >= steps.Length || steps.Length == 0)
                continue;

            var currentStepEntity = steps[procedureState.CurrentStep].Value;
            if (!em.Exists(currentStepEntity))
                continue;

            var currentStep = em.GetComponentData<Step>(currentStepEntity);

            if (currentStep.RequireActivation)
            {
                if (!procedureState.Activation) continue;
            }
            var task = em.GetComponentData<TaskSend>(entity);
                if(task.Task == Entity.Null)
                {
                    var needPawnForItems = false;
                    if (em.HasComponent<ProcedureInventoryLink>(entity))
                    {
                        var inventory = em.GetComponentData<ProcedureInventoryLink>(entity).Inventory;
                        if (inventory != Entity.Null && em.Exists(inventory) && em.HasBuffer<ItemNeed>(inventory))
                            needPawnForItems = InventoryManager.GetMissingItem(em, inventory).Count > 0;
                    }
                    var requiresPawn = currentStep.RequiresWorker || needPawnForItems;

                    Entity buildingWorker = Entity.Null;
                    if (requiresPawn)
                    {
                        if(!em.HasComponent<WorkZoneLink>(entity) || !WorkZoneManager.TryGetBuildingWorker(em,em.GetComponentData<WorkZoneLink>(entity).WorkZone, out buildingWorker))
                        {
                            if(!WorkZoneManager.TryGetCamp(out  var campZone) || !WorkZoneManager.TryGetBuildingWorker(em,campZone,out buildingWorker )) continue;
                        }
                    }

                    var taskEntity = TaskManager.Create(em, "Procedure", entity, buildingWorker, requiresPawn);
                    var taskData = em.GetComponentData<Task>(taskEntity);
                    taskData.Priority = 3;
                    taskData.RequiresPawn = requiresPawn;
                    em.SetComponentData(taskEntity, taskData);
                    task.Task = taskEntity;
                    em.SetComponentData(entity,task);
                }
       
        }
    }

}