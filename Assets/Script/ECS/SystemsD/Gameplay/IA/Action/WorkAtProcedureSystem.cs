using Unity.Entities;
using UnityEngine;

public partial struct WorkAtProcedureSystem : ISystem
{
    public void OnCreate(ref SystemState state)
    {

    }

    public void OnUpdate(ref SystemState state)
    {
        var em = state.EntityManager;
        var actions = QuerryDB.QueryInstances<WorkAtProcedure>(em, DataType.Action);

        foreach (var actionEntity in actions)
        {
            var action = em.GetComponentData<WorkAtProcedure>(actionEntity);
            if (action.Procedure == Entity.Null || !em.Exists(action.Procedure) ||
                !em.HasComponent<ProcedureState>(action.Procedure) || !em.HasBuffer<ProcedureStep>(action.Procedure))
                continue;

            var procedureState = em.GetComponentData<ProcedureState>(action.Procedure);
            var procedureSteps = em.GetBuffer<ProcedureStep>(action.Procedure);
            if (procedureState.CurrentStep < 0 || procedureState.CurrentStep >= procedureSteps.Length)
                continue;

            var stepEntity = procedureSteps[procedureState.CurrentStep].Value;
            if (stepEntity == Entity.Null || !em.Exists(stepEntity) || !em.HasComponent<Step>(stepEntity))
                continue;

            var step = em.GetComponentData<Step>(stepEntity);
            var workPerSecond = 1f;
            var pawn = em.GetComponentData<ActionLink>(actionEntity).Pawn;
            if (em.Exists(pawn) && em.HasComponent<PawnAttributes>(pawn) && em.HasBuffer<PawnAttributeEntry>(pawn))
            {
                var attributes = em.GetBuffer<PawnAttributeEntry>(pawn);
                if (em.HasBuffer<StepAttribute>(stepEntity) && em.GetBuffer<StepAttribute>(stepEntity).Length > 0)
                {
                    workPerSecond = 0f;
                    foreach (var requirement in em.GetBuffer<StepAttribute>(stepEntity))
                    {
                        var definition = AttributeDatabase.Get(requirement.Attribute);
                        if (definition != null)
                            workPerSecond += definition.Base + PawnAttributeGenerator.GetValue(attributes, requirement.Attribute) * definition.PerLevel;
                    }
                    if (workPerSecond <= 0f)
                        workPerSecond = 1f;
                }
            }
            if (em.Exists(pawn))
                workPerSecond *= AttributeManager.GetActual(em, pawn, AttributeManager.WorkPerformance);
            procedureState.PtsWorkActual -= workPerSecond * Time.deltaTime;
            if (procedureState.PtsWorkActual > 0f)
            {
                em.SetComponentData(action.Procedure, procedureState);
                continue;
            }

            procedureState.PtsWorkActual = 0f;
            
            em.SetComponentData(action.Procedure, procedureState);
            ActionManager.Finish(em, actionEntity);
        }
    }

}

public struct WorkAtProcedure : IComponentData
{
    public Entity Procedure;
}