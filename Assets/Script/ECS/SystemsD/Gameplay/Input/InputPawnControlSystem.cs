using Unity.Entities;
using UnityEngine.InputSystem;

public partial class InputPawnControlSystem : SystemBase
{
    private InputActionMap _pawnControlInputMap;
    private InputAction _leftClickAction;
    private InputAction _rightClickAction;

    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<PawnControl_Input>();
    }

    protected override void OnUpdate()
    {
        if (_pawnControlInputMap == null)
        {
            if (!SystemAPI.TryGetSingleton<InputActionsReference>(out var inputRef)) return;
            _pawnControlInputMap = inputRef.InputActionAsset.Value.FindActionMap("Selection", throwIfNotFound: true);
            _leftClickAction = _pawnControlInputMap.FindAction("Select", throwIfNotFound: true);
            _rightClickAction = _pawnControlInputMap.FindAction("CancelSelect", throwIfNotFound: true);
            _pawnControlInputMap.Enable();
        }

        if (!SystemAPI.TryGetSingletonRW<PawnControl_Input>(out var input)) return;
        input.ValueRW.LeftClick = _leftClickAction.WasPressedThisFrame();
        input.ValueRW.RightClick = _rightClickAction.WasPressedThisFrame();
    }
}

public struct PawnControl_Input : IComponentData
{
    public bool LeftClick;
    public bool RightClick;
}
