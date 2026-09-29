using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

/// <summary>Besoin générique d'un pawn (faim, sommeil, ...). Buffer pour en ajouter à l'avenir.</summary>
public struct PawnNeed : IBufferElementData
{
    public FixedString32Bytes Id;
    public float Current;
    public float Max;

    public float GetPercentage()
    {
        return Max > 0f ? math.clamp(Current / Max, 0f, 1f) : 0f;
    }

    public static PawnNeed Create(FixedString32Bytes id, float max, float current)
    {
        return new PawnNeed { Id = id, Current = current, Max = max };
    }
}

/// <summary>Référence du besoin porté par une tâche de besoin (PawnNeedTask).</summary>
public struct NeedTaskRef : IComponentData
{
    public Entity Pawn;
    public FixedString32Bytes NeedId;
}

public static class PawnNeedManager
{
    public const string Faim = "faim";
    public const string Sommeil = "sommeil";

    public static void Initialize(EntityManager em, Entity entity)
    {
        if (entity == Entity.Null || !em.Exists(entity))
            return;

        if (!em.HasBuffer<PawnNeed>(entity))
        {
            var needs = em.AddBuffer<PawnNeed>(entity);
            needs.Add(PawnNeed.Create(new FixedString32Bytes(Faim), 100f, 100f));
            needs.Add(PawnNeed.Create(new FixedString32Bytes(Sommeil), 100f, 100f));
        }
    }

    public static float GetPercentage(EntityManager em, Entity pawn, FixedString32Bytes needId)
    {
        if (pawn == Entity.Null || !em.Exists(pawn) || !em.HasBuffer<PawnNeed>(pawn))
            return 1f;

        var needs = em.GetBuffer<PawnNeed>(pawn);
        for (var i = 0; i < needs.Length; i++)
            if (needs[i].Id == needId)
                return needs[i].GetPercentage();
        return 1f;
    }

    /// <summary>Restaure un besoin d'un pourcentage de sa valeur max (percent de 0 à 100).</summary>
    public static void RestoreNeed(EntityManager em, Entity pawn, string needId, float percent)
    {
        if (pawn == Entity.Null || !em.Exists(pawn) || !em.HasBuffer<PawnNeed>(pawn))
            return;

        var needs = em.GetBuffer<PawnNeed>(pawn);
        for (var i = 0; i < needs.Length; i++)
        {
            if (needs[i].Id != new FixedString32Bytes(needId))
                continue;

            var need = needs[i];
            need.Current = math.min(need.Max, need.Current + need.Max * percent / 100f);
            needs[i] = need;
            return;
        }
    }
}