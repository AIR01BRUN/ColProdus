using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.InputSystem;

public class InputAuthoring : MonoBehaviour
{
    public InputActionAsset InputActions;

    
}

public class InputBaker : Baker<InputAuthoring>
{
    public override void Bake(InputAuthoring authoring)
    {
        var inputEntity = GetEntity(TransformUsageFlags.None);
        AddComponent(inputEntity , new InputActionsReference
        {
            InputActionAsset = authoring.InputActions
        });
           

        
        
        
      
        
    }
}
