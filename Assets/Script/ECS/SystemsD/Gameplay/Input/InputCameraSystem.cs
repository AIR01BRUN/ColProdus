using Unity.Entities;
using Unity.Mathematics;
using UnityEngine.InputSystem;

public partial class InputCameraSystem : SystemBase
{
    private InputActionMap _cameraInputMap;
    private InputAction _moveCameraAction;
    private InputAction _zoomCameraAction;
    private InputAction _fastCameraAction;

    protected override void OnCreate()
    {
        RequireForUpdate<Camera_Input>();
    }

    protected override void OnUpdate()
    {
        if (_cameraInputMap == null)
        {
            if (!SystemAPI.TryGetSingleton<InputActionsReference>(out var inputRef))
            {
                return;
            }

            _cameraInputMap = inputRef.InputActionAsset.Value.FindActionMap("Camera", throwIfNotFound: true);
            _moveCameraAction = _cameraInputMap.FindAction("Move", throwIfNotFound: true);
            _zoomCameraAction = _cameraInputMap.FindAction("Zoom", throwIfNotFound: true);
            _fastCameraAction = _cameraInputMap.FindAction("Fast", throwIfNotFound: true);

            _cameraInputMap.Enable();
        }

        if (!SystemAPI.TryGetSingletonRW<Camera_Input>(out var input))
        {
            return;
        }

        input.ValueRW.MoveCamera = _moveCameraAction.ReadValue<UnityEngine.Vector2>();
        input.ValueRW.ZoomCamera = _zoomCameraAction.ReadValue<float>();
        input.ValueRW.FastCamera = _fastCameraAction.IsPressed();
    }
}
