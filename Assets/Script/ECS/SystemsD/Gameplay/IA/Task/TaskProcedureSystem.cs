using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

public partial struct TaskProcedureSystem : ISystem
{
    public void OnCreate(ref SystemState state)
    {
      
    }

    public void OnUpdate(ref SystemState state)
    {
     
        var em = state.EntityManager;

        var Tasks = QuerryDB.QueryInstances<Task>(em, DataType.Task);
        foreach(var task in Tasks)
        {
            var id = em.GetComponentData<ID>(task);
            if(id.Id != "Procedure") continue;
            var taskProcedure = em.GetComponentData<Task>(task);
            var objet = taskProcedure.Object;
            var pawn = taskProcedure.Pawn;

     
            var procedureState = em.GetComponentData<ProcedureState>(objet);
            var prodcedureStep = em.GetBuffer<ProcedureStep>(objet);
            if(prodcedureStep.Length == 0 )
            {
                TaskManager.Finish(em, task);
                continue;
            }

            var inventoryObject = em.GetComponentData<ProcedureInventoryLink>(objet).Inventory;
            var currentStepEntity = prodcedureStep[procedureState.CurrentStep].Value;

              var currentStepData = em.GetComponentData<Step>(currentStepEntity);
            /* var conditionId = currentStepData.ConditionId.ToString();
            var conditionEntity = currentStepData.ConditionEntity;
            if (!string.IsNullOrEmpty(conditionId) && !ConditionRegistry.CheckCondition(em, conditionEntity, conditionId))
            {
                taskProcedure.CanBeDone = false;
                continue;
            }*/

            // Avant traitement : si aucun pawn n'est assigné, la tâche n'est prenable
            // que si l'inventaire de la zone a assez d'items (canBeDone = le pawn peut la prendre).
            var itemNeed = InventoryManager.GetMissingItem(em,inventoryObject);
            if (itemNeed.Count > 0)
            {
            
                var gateWorkZone = em.GetComponentData<WorkZoneLink>(objet).WorkZone;
                var inventoryZones = WorkZoneManager.GetBuildingWithInventory(em, gateWorkZone);
                var itemOwned = InventoryManager.GetAllStockAvailableItems(em,itemNeed.Keys.ToList(),inventoryZones);
                foreach(var inventoryZoneB in inventoryZones)
                {
                    var inventoryZone = em.GetComponentData<InventoryLink>(inventoryZoneB).Inventory;
                    var zoneInventory =  InventoryManager.GetItems(em,inventoryZone,itemNeed.Keys.ToList());
                    itemOwned =  InventoryManager.Merge(itemOwned,zoneInventory);
                }
                
                if(pawn != Entity.Null)
                {
                    var pawnInventory = InventoryManager.GetItems(em,em.GetComponentData<InventoryLink>(pawn).Inventory,itemNeed.Keys.ToList());
                    itemOwned =  InventoryManager.Merge(itemOwned,pawnInventory);
                }

                if (InventoryManager.HasEnougthItem(itemNeed, itemOwned))
                {
                       taskProcedure.RequiresPawn = true;
                       taskProcedure.CanBeDone = true;
                } 
                else
                {
                    taskProcedure.CanBeDone = false;
                }
                
               
            }
            else
            {
                taskProcedure.CanBeDone = true;
            }
            em.SetComponentData(task, taskProcedure);

            if (!taskProcedure.CanBeDone)
            {
                if(pawn != Entity.Null) TaskManager.Finish(em,task);
                continue;
            }

            if(taskProcedure.ActualAction == Entity.Null)
            {
                
                if (itemNeed.Count > 0)
                {
                   
                    if(pawn == Entity.Null)
                    {
                        continue;
                    }
                    if (TaskManager.DoGetItemNeed(em, task, itemNeed)) continue;
                    TaskManager.DoTransfereItem(em, task, pawn, inventoryObject, true, itemNeed);
                    continue;
                }
                else
                {
                    taskProcedure.RequiresPawn = false;
                    em.SetComponentData(task,taskProcedure);
                }
               
                    

if (currentStepData.RequiresWorker)
                {
                    taskProcedure.RequiresPawn  = true;
                    em.SetComponentData(task,taskProcedure);
                    if(pawn == Entity.Null)
                    {
                        continue;
                    }
                    if(TaskManager.DoMovePawn(em, task, objet)) continue;
                }
                else
                {
                        // L'étape n'a pas besoin d'un travailleur pour l'action :
                        // une fois les items apportés, on libère le pawn (il redevient disponible),
                        // l'action se lance sans lui.
                        taskProcedure.Pawn = Entity.Null;
                        taskProcedure.RequiresPawn = false;
                        //taskProcedure.CanBeDone = true;
                        em.SetComponentData(task, taskProcedure);
                        if(pawn != Entity.Null)
                        {
                            var currentTask = em.GetComponentData<CurrentTask>(pawn);
                            currentTask.Task = Entity.Null;
                            em.SetComponentData(pawn, currentTask);
                        }
              
                       
                }
                

                if(procedureState.PtsWorkActual > 0)
                {
                    var workAction = ActionManager.Create(em, "WorkAtProcedure", task, taskProcedure.Pawn);
                    em.AddComponentData(workAction,  new WorkAtProcedure { Procedure = objet });
                    taskProcedure.ActualAction = workAction;

                    if (currentStepData.RequiresWorker)
                    {
                        // L'étape a besoin d'un travailleur : on laisse le pawn sur la tâche.
                        if (taskProcedure.Pawn != Entity.Null)
                        {
                            var currentAction = em.GetComponentData<CurrentAction>(taskProcedure.Pawn);
                            currentAction.Action = workAction;
                            em.SetComponentData(taskProcedure.Pawn, currentAction);
                        }
                    }
                    else
                    {
                        // L'étape n'a pas besoin d'un travailleur : elle se déroule sans lui.
                        taskProcedure.RequiresPawn = false;
                    }
                    em.SetComponentData(task, taskProcedure);
                }
                else
                {

                    // Étape en attente (PtsWorkNeed <= -1) : elle ne travaille pas, ne
                    // lance aucune action et ne passe à la suivante qu'une fois sa
                    // FinishCondition (id ConditionRegistry) vérifiée (ex: IsNextDay).
                    if (currentStepData.PtsWorkNeed <= -1f)
                    {
                        var finishId = currentStepData.FinishCondition.ToString();
                        if (!ConditionRegistry.CheckCondition(em, objet, finishId))
                        {
                            em.SetComponentData(task, taskProcedure);
                            continue;
                        }
                    }

                    ProcedureManager.NextStep(em, objet);
                    var nextStepIndex = em.GetComponentData<ProcedureState>(objet).CurrentStep;
                    var nextStepEntity = prodcedureStep[nextStepIndex].Value;

                    if (taskProcedure.Pawn != Entity.Null && em.HasComponent<InventoryLink>(taskProcedure.Pawn))
                    {
                        var pawnInventory = em.GetComponentData<InventoryLink>(taskProcedure.Pawn).Inventory;
                        if (pawnInventory != Entity.Null && em.Exists(pawnInventory) && em.HasBuffer<StepItemOut>(currentStepEntity))
                        {
                            var itemOutputs = em.GetBuffer<StepItemOut>(currentStepEntity);
                            for (int itemIndex = 0; itemIndex < itemOutputs.Length; itemIndex++)
                            {
                                var itemOutput = itemOutputs[itemIndex];
                                InventoryManager.AddItem(em, pawnInventory, itemOutput.Item);
                            }
                        }
                        PawnAttributeGenerator.AddExp(em, taskProcedure.Pawn, 10);
                    }


                    // L'action tourne avant la destruction de la tâche : elle peut encore
                    // lire le TaskSend du bâtiment pour récupérer le pawn qui a terminé.
                    ActionRegistry.TryExecute(em, currentStepData.ActionId.ToString(), objet);
                    if (currentStepData.NeedEnergy && em.HasComponent<Energy>(objet))
                        EnergyManager.RemoveEnergy(em, objet, em.GetComponentData<Energy>(objet).EnergyConsume);
                    TaskManager.Finish(em, task);
                }
            }
            else
            {
                continue;
            }
        }
    }

    private static string PawnName(EntityManager em, Entity pawn)
    {
        if (pawn == Entity.Null || !em.Exists(pawn))
            return "none";

        var id = em.GetComponentData<ID>(pawn);
        return id.Id != null ? $"{id.Id}_{id.NumId}" : pawn.Index.ToString();
    }

    private static string FormatItems(List<Items> items)
    {
        if (items == null || items.Count == 0)
            return "none";

        var values = new List<string>();
        foreach (var item in items)
            values.Add($"{item.ItemId}={item.Quantity}");
        return string.Join(", ", values);
    }
}
