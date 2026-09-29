using Unity.Entities;
using UnityEngine;

/// <summary>
/// Met à jour l'inventaire global du joueur (liste + dropdowns par type / sous-type)
/// affiché sous la barre de temps.
/// </summary>
public partial struct AllItemUiSystem : ISystem
{
    private float _timer;

    public void OnCreate(ref SystemState state)
    {
        _timer = 0f;
    }

    public void OnUpdate(ref SystemState state)
    {
        _timer += Time.deltaTime;
        if (_timer < 0.5f)
            return;
        _timer = 0f;

        var world = World.DefaultGameObjectInjectionWorld;
        if (world == null || !world.IsCreated)
            return;

        var panel = UiEntityRegistry.GetSingleton<AllItemTemplate>();
        panel?.Refresh(world.EntityManager);
    }
}
