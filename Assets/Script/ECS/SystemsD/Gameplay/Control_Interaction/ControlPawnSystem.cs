using Unity.Entities;
using Unity.Mathematics;

public partial struct ControlPawnSystem : ISystem
{
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<PawnControl_Input>();
      
    }

    public void OnUpdate(ref SystemState state)
    {
        if (!SystemAPI.TryGetSingleton<PawnControl_Input>(out var pawnInput)) return;
        if (!SystemAPI.TryGetSingleton<Mouse_Input>(out var mouse)) return;
        

        var em = state.EntityManager;
      

        var controlledPawns = QuerryDB.QueryInstances<CrontolThis>(em, DataType.Pawn);

        if (pawnInput.RightClick)
        {
            foreach (var pawn in controlledPawns)
            {
                
                if (!em.Exists(pawn)) continue;

                var target = new MoveTo
                {
                    Position = mouse.PositionSelect,
                    //Finish = false
                };

                if (em.HasComponent<MoveTo>(pawn))
                {
                    em.SetComponentData(pawn, target);
                }
                else
                {
                    em.AddComponentData(pawn, target);
                }
            }
        }

        
    }
}
public struct CrontolThis : IComponentData
{
}
