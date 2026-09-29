using Unity.Entities;
using Unity.Collections;
using UnityEngine;
using System.Collections.Generic;
using System;

public static class ProcedureManager
{
   

    public static void ChangeStep(EntityManager em, Entity procedure, int numStep)
    {

        var steps = em.GetBuffer<ProcedureStep>(procedure);

        if (numStep < 0 || numStep >= steps.Length)
            numStep = 0;

        var state = em.GetComponentData<ProcedureState>(procedure);
        state.CurrentStep = numStep;

        // Chaque étape doit être (ré)activée avant de pouvoir s'exécuter si elle
        // requiert une activation (ex: ordres de marché quotidiens).
      

        var stepEntity = steps[numStep].Value;
        state.PtsWorkActual = em.GetComponentData<Step>(stepEntity).PtsWorkNeed;

        var inventory = em.GetComponentData<ProcedureInventoryLink>(procedure).Inventory;
        InventoryManager.Clear(em,inventory);
        var itemNeed = em.GetBuffer<StepItemIn>(stepEntity);
        foreach(var item in itemNeed)
        {
            InventoryManager.AddItem(em,inventory,new Items(item.Item.ItemId, 0));
        }

        // À chaque changement d'étape : on vide puis on remplit le buffer ItemNeed
        // avec les items requis par l'étape courante (quantité modifiable à la demande).
        InventoryManager.ClearItemNeed(em, inventory);
        foreach(var item in itemNeed)
        {
            InventoryManager.AddItemNeed(em, inventory, new Items(item.Item.ItemId, item.Item.Quantity));
        }

        em.SetComponentData(procedure, state);
    }

    public static void NextStep(EntityManager em, Entity procedure)
    {
        if (procedure == Entity.Null || !em.Exists(procedure) || !em.HasBuffer<ProcedureStep>(procedure))
            return;

        var steps = em.GetBuffer<ProcedureStep>(procedure);
        if (steps.Length == 0)
            return;

        var currentStep = em.GetComponentData<ProcedureState>(procedure).CurrentStep + 1;
        if (currentStep >= steps.Length)
            currentStep = 0;

        ChangeStep(em, procedure, currentStep);
    }

    /// <summary>Supprime uniquement la première étape d'une procédure.</summary>
    public static void RemoveFirstStep(EntityManager em, Entity procedure)
    {
        RemoveStep(em, procedure, 0);
    }

    /// <summary>
    /// Supprime l'étape située à l'index demandé : l'entrée est retirée du buffer
    /// ProcedureStep et l'entité Step est détruite. Si c'était la dernière étape, la
    /// procédure est nettoyée (état, envoi de tâche et inventaire de procedure).
    /// </summary>
    public static void RemoveStep(EntityManager em, Entity procedure, int stepIndex)
    {
        if (em == null || procedure == Entity.Null || !em.Exists(procedure) || !em.HasBuffer<ProcedureStep>(procedure))
            return;

        var steps = em.GetBuffer<ProcedureStep>(procedure);
        if (steps.Length == 0 || stepIndex < 0 || stepIndex >= steps.Length)
            return;

        var stepEntity = steps[stepIndex].Value;
        steps.RemoveAt(stepIndex);

        if (stepEntity != Entity.Null && em.Exists(stepEntity))
            em.DestroyEntity(stepEntity);
        steps = em.GetBuffer<ProcedureStep>(procedure);
        if (steps.Length == 0)
        {
            em.RemoveComponent<ProcedureStep>(procedure);

            if (em.HasComponent<ProcedureState>(procedure))
                em.RemoveComponent<ProcedureState>(procedure);

            if (em.HasComponent<ProcedureInventoryLink>(procedure))
            {
                var inventory = em.GetComponentData<ProcedureInventoryLink>(procedure).Inventory;
                if (inventory != Entity.Null && em.Exists(inventory))
                    InventoryManager.Destroy(em, inventory);
                em.RemoveComponent<ProcedureInventoryLink>(procedure);
            }

            if (em.HasComponent<TaskSend>(procedure))
                em.RemoveComponent<TaskSend>(procedure);

            return;
        }

        if (em.HasComponent<ProcedureState>(procedure))
            ChangeStep(em, procedure, stepIndex >= steps.Length ? steps.Length - 1 : stepIndex);
    }

    public static void AddStep(EntityManager em, Entity entity,ProcuredCreationInfo info , bool changeStep = false)
    {
        if (!em.HasBuffer<ProcedureStep>(entity))
            em.AddBuffer<ProcedureStep>(entity);
    
        var stepEntity = em.CreateEntity();

        em.AddComponentData(stepEntity, new Step
        {
            PtsWorkNeed = info.PtsWorkNeed,
            RequiresWorker = info.RequiresWorker,
            NeedEnergy = info.NeedEnergy,
            RequireActivation = info.RequireActivation,
            ActionId = info.ActionId,
            ConditionId = info.ConditionId,
            ConditionEntity = info.ConditionEntity,
            FinishCondition = info.FinishCondition,
            TargetEntity = entity
        });
        

        var stepItemOut = em.AddBuffer<StepItemOut>(stepEntity);
        var stepAttributes = em.AddBuffer<StepAttribute>(stepEntity);
        foreach (var attribute in info.AttributesId)
            stepAttributes.Add(new StepAttribute
            {
                Attribute = attribute,
            });
        var stepItemIn = em.AddBuffer<StepItemIn>(stepEntity);
      
      
        foreach (var item in info.ItemOut)
        {
            stepItemOut = em.GetBuffer<StepItemOut>(stepEntity);
            stepItemOut.Add(new StepItemOut { Item = item, Luck = 100f });
        }
        foreach (var item in info.ItemIn)
        {
            stepItemIn = em.GetBuffer<StepItemIn>(stepEntity);
            stepItemIn.Add(new StepItemIn { Item = item, Luck = 100f });
        }
        var procedureStep = em.GetBuffer<ProcedureStep>(entity);
        procedureStep.Add(new ProcedureStep { Value = stepEntity });

        if (!em.HasComponent<ProcedureState>(entity))
        {
            em.AddComponentData(entity, new ProcedureState
            {
                CurrentStep = 0,
                Activation = false,
                PtsWorkActual = info.PtsWorkNeed,
            });
        }
        if(!em.HasComponent<TaskSend>(entity))
        {
            em.AddComponentData(entity, new TaskSend { Task = Entity.Null });
        }
        procedureStep = em.GetBuffer<ProcedureStep>(entity);
        if(procedureStep.Length <= 1)
        {
            var inventory = InventoryManager.Create(em, InventoryType.RequestInventory,8);
            em.AddComponentData(entity, new ProcedureInventoryLink{ Inventory = inventory});
            em.AddComponentData(inventory, new InventoryOwnerLink{ Owner = entity});

            ChangeStep(em,entity,0);
        }
        else
        {
            if(changeStep == true)
            {
                ChangeStep(em,entity,procedureStep.Length-1);
            }
        }

        // Informe l'inventaire de la zone des besoins de cette étape (recette ou construction).
        if (em.HasComponent<WorkZoneLink>(entity))
        {
            var workZone = em.GetComponentData<WorkZoneLink>(entity).WorkZone;
            WorkZoneManager.RefreshStockItemNeed(em, workZone);
        }
    }

    public static void Destroy(EntityManager em,Entity objet)
    {
        if (em == null || objet == Entity.Null || !em.Exists(objet))
            return;

        if (em.HasBuffer<ProcedureStep>(objet))
        {
            var bufferStep = em.GetBuffer<ProcedureStep>(objet);
            var steps = bufferStep.ToNativeArray(Allocator.Temp);
            em.RemoveComponent<ProcedureStep>(objet);

            if (em.HasComponent<ProcedureState>(objet))
                em.RemoveComponent<ProcedureState>(objet);

            foreach (var step in steps)
            {
                var value = step.Value;
                if (value != Entity.Null && em.Exists(value))
                    em.DestroyEntity(value);
            }

            steps.Dispose();
        }
        else if (em.HasComponent<ProcedureState>(objet))
        {
            em.RemoveComponent<ProcedureState>(objet);
        }

        if (em.HasComponent<ProcedureInventoryLink>(objet))
        {
            var inventory = em.GetComponentData<ProcedureInventoryLink>(objet).Inventory;
            InventoryManager.Destroy(em, inventory);
            em.RemoveComponent<ProcedureInventoryLink>(objet);
        }
    }
}
public class ProcuredCreationInfo
{
   public float PtsWorkNeed;
   public bool RequiresWorker;
   public bool NeedEnergy;
   public bool RequireActivation;
    public List<FixedString32Bytes> AttributesId = new List<FixedString32Bytes>();
   public FixedString64Bytes ActionId;
    public FixedString64Bytes ConditionId;
    public Entity ConditionEntity;
   public FixedString64Bytes FinishCondition;
    public List<Items> ItemOut = new List<Items>();
   public List<Items> ItemIn = new List<Items>();
}

public struct ProcedureState : IComponentData
{
    public int CurrentStep;          // index de l'étape en cours
    public float PtsWorkActual;          
    public bool Activation; 
}

public struct ProcedureStep : IBufferElementData
{
    public Entity Value;
}

public struct Step : IComponentData
{
    public float PtsWorkNeed;           // temps nécessaire si c'est automatique
    public bool RequiresWorker;      // true = besoin d'un travailleur
    public bool NeedEnergy;         // true = consomme de l'energie du batiment a la fin de l'etape
    public bool RequireActivation; // true = besoin de l'activer avec un button
    public FixedString64Bytes ActionId; // Action quand Step FINIE
    public FixedString64Bytes ConditionId; // Condition requise avant l'execution
    public Entity ConditionEntity; // Entite testee par la condition
    public FixedString64Bytes FinishCondition; // Condition requise pour terminer l'étape (id ConditionRegistry)
    public Entity TargetEntity;
}

public struct StepAttribute : IBufferElementData
{
    public FixedString32Bytes Attribute;

}

public struct StepItemOut : IBufferElementData
{

    public Items Item;
    public float Luck;

}

public struct StepItemIn : IBufferElementData
{
    public Items Item;
    public float Luck;
}

public struct ProcedureInventoryLink : IComponentData
{
    public Entity Inventory;
}


