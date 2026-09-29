using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using UnityEngine;


public partial struct SelectionSystem : ISystem
{
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<Mouse_Input>();
        state.RequireForUpdate<Selection_Input>();
    }

    public void OnUpdate(ref SystemState state)
    {
        var em = state.EntityManager;
        var physicsWorldSingleton = SystemAPI.GetSingleton<PhysicsWorldSingleton>();
       
        if (!SystemAPI.TryGetSingleton<Mouse_Input>(out var mouse )) return;
        var inputE =SystemAPI.GetSingletonEntity<Mouse_Input>();
 
        if (!SystemAPI.TryGetSingleton<Selection_Input>(out var select)) return;

    
  

        UnityEngine.Ray ray =  Camera.main.ScreenPointToRay(new Vector3(mouse.ScreenPosition.x, mouse.ScreenPosition.y, 0));
        float3 rayOrigin = ray.origin;
        float3 rayDirection = ray.direction * 1000f; // distance max

       
        var collisionWorld = physicsWorldSingleton.CollisionWorld;

        
       
        RaycastInput raycastInput = new RaycastInput
        {
            Start = rayOrigin,
            End = rayOrigin + rayDirection,
            Filter = new CollisionFilter 
            { 
                BelongsTo = ~0u, 
                CollidesWith = ~0u, 
                GroupIndex = 0 
            }
        };

        if (collisionWorld.CastRay(raycastInput, out Unity.Physics.RaycastHit hit))
        {
            

            Entity hitEntityG = hit.Entity;

            if(em.HasComponent<GraphicOf>(hitEntityG))
            {
                var boardEntity = em.GetComponentData<GraphicOf>(hitEntityG).Entity;
                var highlight = em.GetComponentData<HighlightRef>(boardEntity).GraphicEntity;
                var position = BoardUtility.WorldToPos(hit.Position);
                var worldPosition = BoardUtility.PosToWorld(position,0.05f);
                
                 mouse.PositionSelect = position;
                 mouse.BoardSelect = boardEntity;
                SystemAPI.SetSingleton(mouse);
            
            
              

                EntityGraphicsUtility.Move(highlight,worldPosition,em);
                    

                    // === CLIC DROIT ===
                    if (select.IsSelectPressed && !select.BlockByUi)
                    {
                        ShowCellInfo(em ,boardEntity,position);
                     
                    }
                
            }
           
         

            
            
        }
    

        
    }

   

    private void ShowCellInfo(EntityManager em,Entity board ,int2 position)
    {
            BoardUtility.TryGetCell(em, board, position,out var cell);
             var screen = UiEntityRegistry.GetSingleton<ObjectDetailScreen>();
             if(screen  == null) screen = new();

            Entity toShow = Entity.Null;
            if (cell.ObjectOn != Entity.Null && em.Exists(cell.ObjectOn))
            {
                var building = cell.ObjectOn;
                var zone = Entity.Null;
                if (em.HasComponent<WorkZoneLink>(building))
                    zone = em.GetComponentData<WorkZoneLink>(building).WorkZone;

                // 1er clic sur un bâtiment -> on montre sa zone ;
                // 2e clic sur un bâtiment de la même zone (ou sur ce bâtiment) -> on montre le bâtiment.
                if (zone != Entity.Null && screen.Entity != zone && screen.Entity != building)
                    toShow = zone;
                else
                    toShow = building;
            }
            else if (cell.WorkZone != Entity.Null)
            {
                toShow = cell.WorkZone;
            }

            if (toShow == Entity.Null)
                return;

            if (em.HasComponent<BuildingRubble>(toShow))
                return;

            screen.Setup(toShow);
            var gameScreen = UiEntityRegistry.GetSingleton<GameScreen>();
             gameScreen.SwapContent(screen );

            return;

    }
}