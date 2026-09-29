using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

/// <summary>
/// Satisfaction d'un pawn : c'est un attribut centralisé (AttributeManager) comme les
/// autres, base 100, borné entre 0 et 200. Elle possède sa propre liste d'effets :
///   - effet "needs"      : somme des paliers de tous les besoins du pawn
///                           (ex: faim entre 50 et 70 => -7 de satisfaction).
///   - tout autre effet   : items, bâtiments, événements...
///   - effet "satisfaction_tier" : porte sur la performance de travail, il dépend du
///                           palier de satisfaction atteint
///                             (ex: satisfaction entre 60 et 90 => -10% de work performance).
/// </summary>
public static class SatisfactionManager
{
    public const string AttributeId = "satisfaction";
    public const float BaseValue = 100f;
    public const float MinValue = 0f;
    public const float MaxValue = 200f;

    /// <summary>Effet de satisfaction produit par les paliers des besoins.</summary>
    public const string NeedsEffectId = "needs";

    /// <summary>Effet posé sur la performance de travail d'après le palier de satisfaction.</summary>
    public const string TierEffectId = "satisfaction_tier";

    /// <summary>Effet posé sur la satisfaction par le salaire de la semaine (SalaryManager).</summary>
    public const string SalaryEffectId = "salary";

    private struct Tier
    {
        public float Min;
        public float Max;
        public float Value;
    }

    // Paliers de satisfaction (bornes de la valeur de satisfaction) => bonus/malus
    // appliqué à la performance de travail (multiplicateur).
    private static readonly Tier[] SatisfactionTiers =
    {
        new Tier { Min = 131f, Max = MaxValue, Value = 0.10f },
        new Tier { Min = 110f, Max = 130f, Value = 0.05f },
        new Tier { Min = 91f, Max = 109f, Value = 0f },
        new Tier { Min = 60f, Max = 90f, Value = -0.10f },
        new Tier { Min = MinValue, Max = 59f, Value = 0f }
    };

    // Paliers appliqués à chaque besoin du pawn (en % du besoin) => effet sur la
    // satisfaction. Le même tableau est appliqué à tous les besoins.
    private static readonly Tier[] NeedTiers =
    {
        new Tier { Min = 85f, Max = 100f, Value = 5f },
        new Tier { Min = 71f, Max = 84f, Value = 0f },
        new Tier { Min = 50f, Max = 70f, Value = -7f },
        new Tier { Min = 25f, Max = 49f, Value = -15f },
        new Tier { Min = 0f, Max = 24f, Value = -30f }
    };

    /// <summary>Crée l'attribut de satisfaction d'un pawn (base 100, borné 0-200).</summary>
    public static bool Initialize(EntityManager em, Entity entity)
    {
        if (em == null || entity == Entity.Null || !em.Exists(entity))
            return false;

        if (!AttributeManager.Add(em, entity, AttributeId, BaseValue))
            return false;

        AttributeManager.SetBounds(em, entity, AttributeId, MinValue, MaxValue);
        return true;
    }

    /// <summary>Bonus/malus de performance de travail correspondant au palier de satisfaction.</summary>
    public static float GetTierBonus(float satisfaction)
    {
        return GetTierValue(SatisfactionTiers, satisfaction);
    }

    /// <summary>Effet sur la satisfaction correspondant au palier d'un besoin (en %).</summary>
    public static float GetNeedTierValue(float needPercentage)
    {
        return GetTierValue(NeedTiers, needPercentage);
    }

    /// <summary>
    /// Applique la satisfaction d'un pawn : effet "needs" (somme des paliers de ses
    /// besoins) sur la satisfaction, puis effet "satisfaction_tier" sur sa performance
    /// de travail d'après le palier atteint.
    /// </summary>
    public static bool Apply(EntityManager em, Entity pawn)
    {
        if (em == null || pawn == Entity.Null || !em.Exists(pawn) || !em.HasBuffer<PawnNeed>(pawn))
            return false;

        Initialize(em, pawn);

        var needs = em.GetBuffer<PawnNeed>(pawn);
        var needTotal = 0f;
        foreach (var need in needs)
            needTotal += GetNeedTierValue(need.GetPercentage() * 100f);

        AttributeManager.SetEffect(em, pawn, AttributeId, NeedsEffectId, needTotal, AttributeEffectType.Addition);

        var satisfaction = AttributeManager.GetActual(em, pawn, AttributeId);
        var bonus = GetTierBonus(satisfaction);
        AttributeManager.SetEffect(em, pawn, AttributeManager.WorkPerformance, TierEffectId, 1f + bonus,
            AttributeEffectType.Multiplication);
        return true;
    }

    private static float GetTierValue(Tier[] tiers, float value)
    {
        for (var i = 0; i < tiers.Length; i++)
        {
            if (value >= tiers[i].Min && value <= tiers[i].Max)
                return tiers[i].Value;
        }

        return 0f;
    }
}
