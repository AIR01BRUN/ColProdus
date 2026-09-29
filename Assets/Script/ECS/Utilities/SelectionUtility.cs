using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

/// <summary>
/// Fonctions utilitaires pour récupérer une entité ECS à partir d'une position écran.
/// </summary>
public static class SelectionUtility
{
    /// <summary>
    /// Raycast sur tous les GameObjects sur le rayon et retourne le premier avec le composant T valide.
    /// Parcourt tous les objets en ordre de distance pour trouver le premier qui correspond au critère.
    /// </summary>
    public static bool TryGetGOAtScreenPosition<T>(
    Vector2 screenPos, 
    out GameObject hitGO,
    out T htiComposant, 
    float maxDistance = 1000f,
    int layerMask = -1)   // -1 = tous les layers, sinon LayerMask.GetMask("Tiles")
    where T : MonoBehaviour
    {
        hitGO = null;
        htiComposant = null;

        Camera cam = Camera.main;
        if (cam == null) 
            return false;

        Ray ray = cam.ScreenPointToRay(screenPos);

        // Si tu ne passes pas de layerMask, il utilise tous les layers
        int mask = (layerMask == -1) ? Physics.DefaultRaycastLayers : layerMask;

        // Récupère tous les hits sur le rayon
        RaycastHit[] hits = Physics.RaycastAll(ray, maxDistance, mask);

        // Parcourt les hits du plus proche au plus éloigné
        if (hits.Length > 0)
        {
            // Trier par distance (déjà fait par Physics.RaycastAll)
            for (int i = 0; i < hits.Length; i++)
            {
                RaycastHit hit = hits[i];
                GameObject candidate = hit.collider != null ? hit.collider.gameObject : hit.transform.gameObject;

                // Retourne le premier GameObject qui a le composant T
                if (candidate.TryGetComponent<T>(out var component))
                {
                    hitGO = candidate;
                    htiComposant = component;
                    return true;
                }
            }
        }

        return false;
    }



    
}

