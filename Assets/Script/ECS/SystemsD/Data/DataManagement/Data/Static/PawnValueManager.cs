using Unity.Entities;
using Unity.Mathematics;

/// <summary>
/// Valeur d'un pawn : c'est un attribut centralisé (AttributeManager) comme les autres,
/// base 100, calculé à partir de tous les points investis dans ses caractéristiques
/// (buffer PawnAttributeEntry : Level de chaque attribut).
/// La valeur sert notamment de référence au salaire : un salaire correct vaut 10% de
/// la valeur du pawn (100 de valeur => salaire de base de 10).
/// </summary>
public static class PawnValueManager
{
    public const string AttributeId = AttributeManager.Value;
    public const float BaseValue = 100f;
    public const float PointValue = 10f;
    public const string PointsEffectId = "attributes";
    public const float SalaryRatio = 0.10f;

    public static bool Initialize(EntityManager em, Entity pawn)
    {
        if (em == null || pawn == Entity.Null || !em.Exists(pawn))
            return false;

        if (!AttributeManager.Add(em, pawn, AttributeId, BaseValue))
            return false;

        AttributeManager.SetBounds(em, pawn, AttributeId, 0f, float.MaxValue);
        return true;
    }

    /// <summary>Somme des points de toutes les caractéristiques du pawn.</summary>
    public static int GetPoints(EntityManager em, Entity pawn)
    {
        if (em == null || pawn == Entity.Null || !em.Exists(pawn) || !em.HasBuffer<PawnAttributeEntry>(pawn))
            return 0;

        var entries = em.GetBuffer<PawnAttributeEntry>(pawn);
        var points = 0;
        for (var i = 0; i < entries.Length; i++)
            points += math.max(0, entries[i].Level);

        return points;
    }

    /// <summary>Valeur actuelle du pawn (base 100 + 10 par point de caractéristique).</summary>
    public static float GetValue(EntityManager em, Entity pawn)
    {
        Initialize(em, pawn);
        return AttributeManager.GetActual(em, pawn, AttributeId);
    }

    /// <summary>Recalcule la valeur à partir des points mis dans les caractéristiques.</summary>
    public static bool Refresh(EntityManager em, Entity pawn)
    {
        if (em == null || pawn == Entity.Null || !em.Exists(pawn))
            return false;

        Initialize(em, pawn);
        var previous = AttributeManager.GetActual(em, pawn, AttributeId);
        AttributeManager.SetEffect(em, pawn, AttributeId, PointsEffectId, GetPoints(em, pawn) * PointValue,
            AttributeEffectType.Addition);

        // La valeur est la référence du salaire : quand elle bouge, la demande de
        // currency du bâtiment de salaire doit suivre.
        if (math.abs(AttributeManager.GetActual(em, pawn, AttributeId) - previous) > 0.001f)
            SalaryManager.RefreshDemand(em);

        return true;
    }

    /// <summary>Salire que le pawn mériterait : 10% de sa valeur.</summary>
    public static float GetDeservedSalary(EntityManager em, Entity pawn)
    {
        return GetValue(em, pawn) * SalaryRatio;
    }
}
