
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.InputSystem;

public partial class InputMouseSystem : SystemBase
{
    public InputActionMap _mouseInputMap;
    private InputAction mousePositionAction;

    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<Mouse_Input>();
    }

    protected override void OnUpdate()
    {
        if(_mouseInputMap == null)
        {
            if (!SystemAPI.TryGetSingleton<InputActionsReference>(out var inputRef)) return;
            _mouseInputMap = inputRef.InputActionAsset.Value.FindActionMap("Mouse", throwIfNotFound: true);
            mousePositionAction = _mouseInputMap.FindAction("MousePosition", throwIfNotFound: true);
            _mouseInputMap.Enable();
        }
        Vector2 screenPos = mousePositionAction.ReadValue<Vector2>();
        Vector3 worldPos = Camera.main.ScreenToWorldPoint(new Vector3(screenPos.x, screenPos.y, 0));
      

        // Mise à jour du singleton
       var input = SystemAPI.GetSingleton<Mouse_Input>();
       input.ScreenPosition =  new float2(screenPos.x, screenPos.y);
       input.WorldPosition  = new float3(worldPos.x, worldPos.y, worldPos.z);
        SystemAPI.SetSingleton(input);
        
    }
}

public struct Mouse_Input : IComponentData
{
    public float2 ScreenPosition;
    public float3 WorldPosition;
    public int2 PositionSelect;
    public Entity BoardSelect;
}
