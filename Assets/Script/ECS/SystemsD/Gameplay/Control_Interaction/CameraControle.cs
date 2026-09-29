using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

public partial struct CameraControle : ISystem
{
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<Camera_Input>();
    }

    public void OnUpdate(ref SystemState state)
    {
        if (!SystemAPI.TryGetSingletonRW<Camera_Input>(out var cameraInput))
        {
            return;
        }

        var camera = Camera.main;
        if (camera == null)
        {
            return;
        }

        float2 move = cameraInput.ValueRO.MoveCamera;
        if (math.lengthsq(move) > 0f)
        {
            Vector3 direction = new Vector3(move.x, 0f, move.y);
            camera.transform.position += direction * (10f * Time.unscaledDeltaTime);
        }

        if (math.abs(cameraInput.ValueRO.ZoomCamera) > 0.001f)
        {
            Vector3 position = camera.transform.position;
            float zoomSpeed = cameraInput.ValueRO.FastCamera ? 20f : 10f;
            position.y = math.clamp(position.y - cameraInput.ValueRO.ZoomCamera * zoomSpeed * Time.unscaledDeltaTime, 2f, 50f);
            camera.transform.position = position;
        }
    }
}
