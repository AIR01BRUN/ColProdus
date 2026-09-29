using Unity.Burst;
using Unity.Entities;
using UnityEngine;
using UnityEngine.InputSystem;
public partial class InputSelectionSystem: SystemBase
{
    private InputActionMap _selectionInputMap;
    private InputAction _selectAction;
    private InputAction _cancelSelectAction;

    /// <summary>Nombre de frames pendant lesquelles l'UI garde la priorite sur le raycast de selection.</summary>
    public const int BlockByUiFrames = 2;

    public void OnCreate(ref SystemState state)
    {
      
        state.RequireForUpdate<Selection_Input>();
       
    }

    /// <summary>
    /// Ecrit directement dans le composant singleton Selection_Input.
    /// Appele par l'UI (ButtonTemplate) pour passer en premier sur le clic.
    /// </summary>
    public static void BlockByUi(bool on)
    {
        var world = World.DefaultGameObjectInjectionWorld;
        if (world == null || !world.IsCreated)
            return;

        var em = world.EntityManager;
        using var query = em.CreateEntityQuery(ComponentType.ReadWrite<Selection_Input>());
        if (query.CalculateEntityCount() != 1)
            return;

        var entity = query.GetSingletonEntity();
        var data = em.GetComponentData<Selection_Input>(entity);
        data.BlockByUi = on;
        em.SetComponentData(entity, data);
    }

    protected override void OnUpdate()
    {
        if(_selectionInputMap  == null){ 
        if (!SystemAPI.TryGetSingleton<InputActionsReference>(out var inputRef)) return;
        _selectionInputMap = inputRef.InputActionAsset.Value.FindActionMap("Selection", throwIfNotFound: true);
        _selectAction = _selectionInputMap.FindAction("Select", throwIfNotFound: true);
        _cancelSelectAction = _selectionInputMap.FindAction("CancelSelect", throwIfNotFound: true);
        _selectionInputMap.Enable();
        }

        if (!SystemAPI.TryGetSingletonRW< Selection_Input >(out var input)) return;
        input.ValueRW.IsSelectPressed = _selectAction.WasPressedThisFrame();
        input.ValueRW.IsCancelSelectPressed = _cancelSelectAction.WasPressedThisFrame();

    }
}
public struct Selection_Input : IComponentData
{
    public bool IsSelectPressed;
    public bool IsCancelSelectPressed;
    /// <summary>Mis a On quand un ButtonTemplate est presse : l'UI passe en priorite sur le raycast de selection.</summary>
    public bool BlockByUi;
    /// <summary>Frame jusqu'a laquelle BlockByUi reste a On, puis il repasse a Off tout seul.</summary>
    public int BlockByUiUntilFrame;
    
}
