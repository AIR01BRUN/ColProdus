using Unity.Entities;

using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using System.Linq;
using UnityEditor.PackageManager.Requests;
using UnityEngine;

public static class TaskManager
{
    public static Entity Create(EntityManager em, string id,Entity objet, Entity buildingTodo = default, bool requiresPawn = false)
    {
       

        var entity = em.CreateEntity();
        var number = Database.GetNextNumber(DataType.Task, id);

        em.AddComponentData(entity, new ID
        {
            DataType = DataType.Task,
            Id = id,
            NumId = number,
            Name = id
        });

        em.AddComponentData(entity, new Task
        {
            Pawn = Entity.Null,
            Object = objet,
            BuildingTodo = buildingTodo,
            CanBeDone = false,
            ActualAction = Entity.Null,
            Priority = 4,
            RequiresPawn = requiresPawn
        });


        Database.AddInstance(DataType.Task, id, number, entity, em);
        return entity;
    }
    public static void Finish(EntityManager em, Entity task)
    {
        if (!em.Exists(task))
        {
            return;
        }
        var taskData = em.GetComponentData<Task>(task);

        if(taskData.Object != Entity.Null && em.HasComponent<TaskSend>(taskData.Object))
        {
            var taskSend = em.GetComponentData<TaskSend>(taskData.Object);
            taskSend.Task = Entity.Null;
            em.SetComponentData(taskData.Object, taskSend);
        }
        if(taskData.Pawn != Entity.Null && em.HasComponent<CurrentTask>(taskData.Pawn))
        {
            var currentTask = em.GetComponentData<CurrentTask>(taskData.Pawn);
            if (currentTask.Task == task)
            {
                currentTask.Task = Entity.Null;
                em.SetComponentData(taskData.Pawn, currentTask);
            }
            else
            {
            }
        }
        else if (taskData.Pawn != Entity.Null)
        {
        }

        if(taskData.ActualAction != Entity.Null)
        {
            ActionManager.Finish(em,taskData.ActualAction );
        }
        var id = em.GetComponentData<ID>(task);
        Database.DeleteInstance(DataType.Task, id.Id.ToString(), id.NumId);
        em.DestroyEntity(task);
    }

    public static void FinishAllLinked(EntityManager em, Entity entity)
    {
        if (em == null || entity == Entity.Null || !em.Exists(entity))
            return;

        if (em.HasComponent<TaskSend>(entity))
        {
            var taskSend = em.GetComponentData<TaskSend>(entity);
            if (taskSend.Task != Entity.Null && em.Exists(taskSend.Task))
                Finish(em, taskSend.Task);
        }

        foreach (var task in QuerryDB.QueryInstances(DataType.Task))
        {
            if (!em.Exists(task) || !em.HasComponent<Task>(task))
                continue;

            var taskData = em.GetComponentData<Task>(task);
            if (taskData.Object == entity || taskData.BuildingTodo == entity)
                Finish(em, task);
        }
    }

    public static bool DoGetItemNeed(EntityManager em, Entity task, Dictionary<string, int> items)
    {
        var taskData = em.GetComponentData<Task>(task);
        var pawn =  taskData.Pawn;
        var pawnInventory = em.GetComponentData<InventoryLink>(pawn).Inventory;
        var pawnInventoryDic = InventoryManager.GetItems(em,pawnInventory,items.Keys.ToList());


        var hasEnougthItem = InventoryManager.HasEnougthItem(items,pawnInventoryDic);
        if(!hasEnougthItem)
        {
            var missingItems = InventoryManager.GetMissingItem(em,pawnInventory,items);
            if (missingItems.Count == 0)
            {
                return false;
            }

            var zone = em.GetComponentData<WorkZoneLink>(em.GetComponentData<WorkIn>(pawn).Building).WorkZone;
            var inventorysZone = WorkZoneManager.GetBuildingWithInventory(em,zone)
                .Where(b => em.HasComponent<InventoryLink>(b))
                .Select(b => em.GetComponentData<InventoryLink>(b).Inventory)
                .ToList();
   
            if(InventoryManager.TryResearchInventory(em,items,inventorysZone,out var targetInventory, out var contentInventory))
            {
                return DoTransfereItem(em,task,pawn,targetInventory,false,contentInventory);
            }

            TaskManager.Finish(em, task);
            return true;
        }
        return false;
    }

    private static string FormatItems( Dictionary<string, int> items)
    {
        if (items == null || items.Count == 0)
            return "none";

        var values = new List<string>();
        foreach (var item in items)
            values.Add($"{item.Key}={item.Value}");
        return string.Join(", ", values);
    }

    private static string PawnName(EntityManager em, Entity pawn)
    {
        if (pawn == Entity.Null || !em.Exists(pawn))
            return "none";

        var id = em.GetComponentData<ID>(pawn);
        return id.Id != null ? $"{id.Id}_{id.NumId}" : pawn.Index.ToString();
    }

    public static bool DoTransfereItem(EntityManager em, Entity task, Entity pawn, Entity targetInventory, bool pawnGives, Dictionary<string, int> items)
    {
      
        var pawnInventory = em.GetComponentData<InventoryLink>(pawn).Inventory;

        // pawnGives = true  -> le pawn DONNE ses items à l'inventaire cible.
        // pawnGives = false -> le pawn PREND les items de l'inventaire cible.
        var giverInventory = pawnGives ? pawnInventory : targetInventory;
        var receiverInventory = pawnGives ? targetInventory : pawnInventory;

        // L'inventaire cible est souvent celui d'un bâtiment : on se déplace vers son propriétaire.
        if (em.HasComponent<InventoryOwnerLink>(targetInventory))
        {
            var building = em.GetComponentData<InventoryOwnerLink>(targetInventory).Owner;
            if (building != Entity.Null)
            {
                var moved = DoMovePawn(em, task, building);
                if (moved)
                {
                    return true;
                }
            }
        }
        var transferAction = ActionManager.Create(em, "TransfereItem", task, pawn);
        em.AddComponentData(transferAction, new TransferTo
        {
            Source = giverInventory,
            Destination = receiverInventory,
        });
       

         var itemToTransfer = em.AddBuffer<TransferItem>(transferAction);
        foreach(var item in items){
            itemToTransfer.Add(new TransferItem{ Item = new Items(item.Key,item.Value) });
        }
        var taskData = em.GetComponentData<Task>(task);
        taskData.ActualAction= transferAction;
        em.SetComponentData(task, taskData);
        var pawnCurrentAction = em.GetComponentData<CurrentAction>(pawn);
        pawnCurrentAction.Action = transferAction;
        em.SetComponentData(pawn,pawnCurrentAction);

        // Le pawn prend des items d'un stock de building : le building devient Busy{Pawn} tant
        // que le transfert n'est pas terminé (les autres pawns ne comptent plus ses items).
        if (!pawnGives && em.HasComponent<InventoryOwnerLink>(targetInventory))
        {
            var ownerBuilding = em.GetComponentData<InventoryOwnerLink>(targetInventory).Owner;
            if (ownerBuilding != Entity.Null && em.Exists(ownerBuilding))
            {
                if (em.HasComponent<Busy>(ownerBuilding))
                    em.SetComponentData(ownerBuilding, new Busy { Pawn = pawn });
                else
                    em.AddComponentData(ownerBuilding, new Busy { Pawn = pawn });
            }
        }

        return true;
    }

    private static Entity GetBuildingInventory(EntityManager em, Entity building)
    {
        if (building == Entity.Null || !em.Exists(building))
            return Entity.Null;
        if (em.HasComponent<InventoryLink>(building))
            return em.GetComponentData<InventoryLink>(building).Inventory;
        if (em.HasComponent<ProcedureInventoryLink>(building))
            return em.GetComponentData<ProcedureInventoryLink>(building).Inventory;
        return Entity.Null;
    }

    public static void Release(EntityManager em, Entity task)
    {
        if (task == Entity.Null || !em.Exists(task) || !em.HasComponent<Task>(task))
            return;

        var taskData = em.GetComponentData<Task>(task);
        if (taskData.Pawn != Entity.Null)
        {
            if (em.HasComponent<CurrentTask>(taskData.Pawn))
            {
                var currentTask = em.GetComponentData<CurrentTask>(taskData.Pawn);
                if (currentTask.Task == task)
                {
                    currentTask.Task = Entity.Null;
                    em.SetComponentData(taskData.Pawn, currentTask);
                }
            }

            taskData.Pawn = Entity.Null;
            em.SetComponentData(task, taskData);
        }

        // Tâche libérée => elle redevient disponible (le tag Submit est retiré).
        if (em.HasComponent<Submit>(task))
            em.RemoveComponent<Submit>(task);
    }

    private static void SetRechercheAction(EntityManager em, Entity task, Entity pawn, Entity action)
    {
        var taskData = em.GetComponentData<Task>(task);
        taskData.ActualAction = action;
        em.SetComponentData(task, taskData);
        var currentAction = em.GetComponentData<CurrentAction>(pawn);
        currentAction.Action = action;
        em.SetComponentData(pawn, currentAction);
    }

    public static bool DoMovePawn(EntityManager em,Entity task,Entity objectTarget)
    {
        var taskCompo = em.GetComponentData<Task>(task);
        var pawn = taskCompo.Pawn;
        var onBoardPawn = em.GetComponentData<OnBoard>(pawn);
        var currentAction = em.GetComponentData<CurrentAction>(pawn);
        var onBoardTarget =  em.GetComponentData<OnBoard>(objectTarget);
        var sizeTarget = em.GetComponentData<Size>(objectTarget);
     
        // Distance du pawn au rectangle occupé par le bâtiment (toutes ses cellules,
        // pas seulement le coin bas-gauche de l'entité).
        var minX = onBoardTarget.Position.x;
        var minY = onBoardTarget.Position.y;
        var maxX = minX + sizeTarget.Value.x - 1;
        var maxY = minY + sizeTarget.Value.y - 1;

        var distanceX = math.max(minX - onBoardPawn.Position.x, 0) + math.max(onBoardPawn.Position.x - maxX, 0);
        var distanceY = math.max(minY - onBoardPawn.Position.y, 0) + math.max(onBoardPawn.Position.y - maxY, 0);
                
        if(distanceX > 1 || distanceY > 1)
        {
            var targetPosition = BoardUtility.GetNearestReachableNeigthbour(em, onBoardTarget.Board, onBoardTarget.Position, sizeTarget.Value, onBoardPawn.Position);

            // Aucune position valide ET atteignable autour du building : le pawn ne
            // pourra jamais s'en approcher -> on termine la task.
            if (targetPosition.x == -1 && targetPosition.y == -1)
            {
                TaskManager.Finish(em, task);
                return true;
            }

            var actionMove = ActionManager.Create(em, "Move",task,pawn);
            em.AddComponentData(actionMove, new MoveTo { Position = targetPosition });

            currentAction.Action = actionMove;
            em.SetComponentData(pawn, currentAction);
            taskCompo.ActualAction = actionMove;
            em.SetComponentData(task, taskCompo);
            return true;
        }
        return false;
    }

  
}

public struct CurrentTask : IComponentData
{
    public Entity Task;
}

public struct Task : IComponentData
{
    public Entity Pawn;
    public Entity Object;
    public Entity BuildingTodo;
    public bool CanBeDone;
    public Entity ActualAction;
    public bool RequiresPawn;
    public int Priority; // 1 > 4  //1: URGENCE : BESOIN URGENT / JOUEUR OBLIGE TASK // 2 : WORK // 3 : BESOIN (dans periode travail) // 4: 
}
public struct TaskSend : IComponentData
{
    public Entity Task;
}

// Marqueur posé sur l'inventaire stock d'un building pour signaler qu'une
// tâche est en train d'y transférer des items (building occupé).
// Task = la task propriétaire de la réservation.
public struct TaskBusy : IComponentData
{
    public Entity Task;
}

// Marqueur posé sur un building quand un pawn récupère des items dessus.
// Pawn = le pawn qui est en train de prendre les items.
public struct Busy : IComponentData
{
    public Entity Pawn;
}

// Buffer posé sur le building checkroom : liste des tâches à faire publiées aux
// pawns de la zone, dans l'ordre d'envoi (les plus anciennes d'abord).
public struct TaskAvailable : IBufferElementData
{
    public Entity Task;
}

// Tag posé sur une tâche quand un pawn l'a prise : elle n'est plus publiée dans
// les TaskAvailable tant qu'elle n'est pas libérée/modifiée.
public struct Submit : IComponentData
{
}


