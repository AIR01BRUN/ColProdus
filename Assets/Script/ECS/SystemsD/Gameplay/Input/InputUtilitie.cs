using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.InputSystem;

public partial struct InputSystem : ISystem
{
      public void OnCreate(ref SystemState state)
    {
        var em = state.EntityManager;
        var input = em.CreateEntity();
        em.SetName(input , "INPUT");
        em.AddComponent<Selection_Input>(input);
        em.AddComponent<Mouse_Input>(input);
        em.AddComponent<PlaceOnBoard_Input>( input);
        em.AddComponent<Camera_Input>( input);
        em.AddComponent<PawnControl_Input>(input);
        var inputActionRef =  Resources.Load<InputActionAsset>("Control");
        em.AddComponentData(input , new InputActionsReference{ InputActionAsset = inputActionRef });
    
    }
}


public struct InputActionsReference : IComponentData
{
    public UnityObjectRef<InputActionAsset> InputActionAsset;
    
}

public struct Camera_Input : IComponentData
{
    public float2 MoveCamera;
    public float ZoomCamera;
    public bool FastCamera;
}


