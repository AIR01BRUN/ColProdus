using Unity.Entities;
using UnityEngine.InputSystem;

public partial class InputGameSpeedSystem : SystemBase
{
    private InputActionMap _gameSpeedInputMap;
    private InputAction _pauseAction;
    private InputAction _speedUpAction;
    private InputAction _speedDownAction;

    protected override void OnCreate()
    {
        RequireForUpdate<InputActionsReference>();
    }

    protected override void OnUpdate()
    {
        if (_gameSpeedInputMap == null)
        {
            if (!SystemAPI.TryGetSingleton<InputActionsReference>(out var inputRef))
            {
                return;
            }

            _gameSpeedInputMap = inputRef.InputActionAsset.Value.FindActionMap("GameSpeed", throwIfNotFound: true);
            _pauseAction = _gameSpeedInputMap.FindAction("Pause", throwIfNotFound: true);
            _speedUpAction = _gameSpeedInputMap.FindAction("SpeedUp", throwIfNotFound: true);
            _speedDownAction = _gameSpeedInputMap.FindAction("SpeedDown", throwIfNotFound: true);

            _gameSpeedInputMap.Enable();
        }

        if (_pauseAction.WasPressedThisFrame())
        {
            GameSpeedController.TogglePause();
        }
        if (_speedUpAction.WasPressedThisFrame())
        {
            GameSpeedController.NextSpeed();
        }
        if (_speedDownAction.WasPressedThisFrame())
        {
            GameSpeedController.PreviousSpeed();
        }
    }
}