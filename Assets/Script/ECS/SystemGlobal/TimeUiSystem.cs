using Unity.Entities;
using UnityEngine;

/// <summary>
/// Met à jour la barre de temps (heure du monde + boutons de vitesse) affichée
/// en haut de l'écran de jeu.
/// </summary>
public partial struct TimeUiSystem : ISystem
{
    private float _timer;

    public void OnCreate(ref SystemState state)
    {
        _timer = 0f;
    }

    public void OnUpdate(ref SystemState state)
    {
        _timer += Time.deltaTime;
        if (_timer < 0.25f)
            return;
        _timer = 0f;

        var world = World.DefaultGameObjectInjectionWorld;
        if (world == null || !world.IsCreated)
            return;

        var bar = UiEntityRegistry.GetSingleton<TimeTemplate>();
        bar?.Refresh();
    }
}
