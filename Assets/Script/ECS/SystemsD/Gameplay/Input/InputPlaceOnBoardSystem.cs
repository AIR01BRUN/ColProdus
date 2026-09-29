using Unity.Burst;
using Unity.Entities;
using UnityEngine.InputSystem;
public partial class InputPlaceOnBoardSystem: SystemBase
{
    private InputActionMap _buildingInputMap;
    private InputAction _placeBuildingAction;
    private InputAction _rotateBuildingRightAction;
    private InputAction _rotateBuildingLeftAction;
    private InputAction _cancelBuildingAction;

    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<PlaceOnBoard_Input>();
       
    }

    protected override void OnUpdate()
    {
        if(_buildingInputMap  == null){ 
        if (!SystemAPI.TryGetSingleton<InputActionsReference>(out var inputRef)) return;
        _buildingInputMap = inputRef.InputActionAsset.Value.FindActionMap("Build", throwIfNotFound: true);
        _placeBuildingAction = _buildingInputMap.FindAction("PlaceBuilding", throwIfNotFound: true);
        _rotateBuildingRightAction = _buildingInputMap.FindAction("RotateBuildingRight", throwIfNotFound: true);
        _rotateBuildingLeftAction = _buildingInputMap.FindAction("RotateBuildingLeft", throwIfNotFound: true);
        _cancelBuildingAction = _buildingInputMap.FindAction("CancelBuilding", throwIfNotFound: true);
        
        _buildingInputMap.Enable();
        }

        if (!SystemAPI.TryGetSingletonRW<PlaceOnBoard_Input>(out var input)) return;
        input.ValueRW.PlaceBuilding = _placeBuildingAction.WasPressedThisFrame();
        input.ValueRW.RotateBuildingRight = _rotateBuildingRightAction.WasPressedThisFrame();
        input.ValueRW.RotateBuildingLeft = _rotateBuildingLeftAction.WasPressedThisFrame();
        input.ValueRW.CancelBuilding = _cancelBuildingAction.WasPressedThisFrame();
    }
}
public struct PlaceOnBoard_Input : IComponentData
{
    public bool PlaceBuilding;
    public bool RotateBuildingRight;
    public bool RotateBuildingLeft;
    public bool CancelBuilding;

}