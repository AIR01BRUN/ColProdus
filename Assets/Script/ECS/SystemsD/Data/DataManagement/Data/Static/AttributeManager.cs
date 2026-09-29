using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

/// <summary>Nature d'un effet appliqué à un attribut.</summary>
public enum AttributeEffectType : byte
{
    Addition = 0,
    Multiplication = 1
}

/// <summary>
/// Un attribut centralisé (stat) d'une entité : une valeur de base, la valeur actuelle
/// calculée à partir des effets, et les bornes entre lesquelles elle est maintenue.
/// Toutes les stats d'une entité vivent dans le même buffer (AttributeStat), les stats
/// étant retrouvées par leur id.
/// </summary>
public struct AttributeStat : IBufferElementData
{
    public FixedString32Bytes Id;
    public float Actual;
    public float Base;
    public float Min;
    public float Max;

    public static AttributeStat Create(FixedString32Bytes id, float baseValue)
    {
        return new AttributeStat
        {
            Id = id,
            Base = baseValue,
            Actual = baseValue,
            Min = float.MinValue,
            Max = float.MaxValue
        };
    }
}

/// <summary>
/// Effet qui modifie un attribut : il cible un attribut par son id, et son calcul est
/// appliqué dans l'ordre de la liste (Actual = Base, puis chaque effet est appliqué).
/// </summary>
public struct AttributeEffect : IBufferElementData
{
    public FixedString32Bytes Attribute;
    public FixedString32Bytes Id;
    public float Value;
    public AttributeEffectType Type;

    public static AttributeEffect Create(FixedString32Bytes attribute, FixedString32Bytes id, float value, AttributeEffectType type)
    {
        return new AttributeEffect { Attribute = attribute, Id = id, Value = value, Type = type };
    }
}

/// <summary>
/// Gestionnaire central des attributs (stats) d'une entité : Sleep Rate Recovery,
/// Sleep Rate Consume, Hunger Rate Consume, Move Speed, Work Performance...
/// Un attribut = (id, actual, base) dans le buffer AttributeStat. Chaque attribut
/// possède sa propre liste d'effets dans le buffer AttributeEffect (id, valeur, type
/// addition ou multiplication) : Actual est recalculé à partir de la base et de la
/// liste d'effets à chaque modification.
/// </summary>
public static class AttributeManager
{
    public const string SleepRateRecovery = "sleep_rate_recovery";
    public const string SleepRateConsume = "sleep_rate_consume";
    public const string HungerRateConsume = "hunger_rate_consume";
    public const string MoveSpeed = "move_speed";
    public const string WorkPerformance = "work_performance";
    public const string Value = "value";

    private static readonly Dictionary<string, float> DefaultBaseValues = new Dictionary<string, float>
    {
        { SleepRateRecovery, 1f },
        { SleepRateConsume, 0.083f },
        { HungerRateConsume, 0.056f },
        { MoveSpeed, 2f },
        { WorkPerformance, 1f },
        { Value, 100f }
    };

    /// <summary>Effet posé par le système de besoins sur la performance de travail.</summary>
    public const string NeedsEffect = "needs";

    /// <summary>Base par défaut d'un attribut (0 si l'attribut n'est pas connu).</summary>
    public static float GetDefaultBase(string attributeId)
    {
        if (string.IsNullOrEmpty(attributeId))
            return 0f;

        return DefaultBaseValues.TryGetValue(attributeId, out var value) ? value : 0f;
    }

    /// <summary>Tous les attributs par défaut d'une entité.</summary>
    public static void Create(EntityManager em, Entity entity)
    {
        foreach (var attributeId in DefaultBaseValues.Keys)
            Add(em, entity, attributeId);
    }

    /// <summary>Crée les attributs demandés qui n'existent pas encore sur l'entité.</summary>
    public static void Create(EntityManager em, Entity entity, params string[] attributeIds)
    {
        if (attributeIds == null)
            return;

        foreach (var attributeId in attributeIds)
            Add(em, entity, attributeId);
    }

    /// <summary>Ajoute un attribut (base = valeur par défaut) s'il n'existe pas encore.</summary>
    public static bool Add(EntityManager em, Entity entity, string attributeId, float baseValue = -1f)
    {
        if (!CanWrite(em, entity) || string.IsNullOrEmpty(attributeId))
            return false;

        var stats = em.GetBuffer<AttributeStat>(entity);
        if (GetIndex(stats, attributeId) != -1)
            return false;

        var id = new FixedString32Bytes(attributeId);
        var baseStat = baseValue >= 0f ? baseValue : GetDefaultBase(attributeId);
        stats.Add(AttributeStat.Create(id, baseStat));
        return true;
    }

    /// <summary>Retrouve un attribut par son id.</summary>
    public static bool TryGet(EntityManager em, Entity entity, string attributeId, out AttributeStat stat)    {
        stat = default;
        if (!CanRead(em, entity) || string.IsNullOrEmpty(attributeId))
            return false;

        var stats = em.GetBuffer<AttributeStat>(entity);
        var index = GetIndex(stats, attributeId);
        if (index == -1)
            return false;

        stat = stats[index];
        return true;
    }

    public static bool Has(EntityManager em, Entity entity, string attributeId)
    {
        return TryGet(em, entity, attributeId, out _);
    }

    /// <summary>Valeur actuelle (base + effets). Retourne la base par défaut si l'attribut n'existe pas.</summary>
    public static float GetActual(EntityManager em, Entity entity, string attributeId)
    {
        if (TryGet(em, entity, attributeId, out var stat))
            return stat.Actual;

        return GetDefaultBase(attributeId);
    }

    public static float GetBase(EntityManager em, Entity entity, string attributeId)
    {
        if (TryGet(em, entity, attributeId, out var stat))
            return stat.Base;

        return GetDefaultBase(attributeId);
    }

    /// <summary>Valeur actuelle exprimée en pourcentage de la base (100 = valeur de base).</summary>
    public static float GetPercentage(EntityManager em, Entity entity, string attributeId)
    {
        var baseValue = GetBase(em, entity, attributeId);
        if (baseValue == 0f)
            return 0f;

        return GetActual(em, entity, attributeId) / baseValue * 100f;
    }

    /// <summary>Écrit directement la valeur actuelle (sans passer par les effets).</summary>
    public static bool SetActual(EntityManager em, Entity entity, string attributeId, float value)
    {
        if (!CanWrite(em, entity) || string.IsNullOrEmpty(attributeId))
            return false;

        var stats = em.GetBuffer<AttributeStat>(entity);
        var index = GetIndex(stats, attributeId);
        if (index == -1)
            return false;

        var stat = stats[index];
        stat.Actual = math.clamp(value, stat.Min, stat.Max);
        stats[index] = stat;
        return true;
    }

    /// <summary>Fixe les bornes entre lesquelles la valeur actuelle est maintenue.</summary>
    public static bool SetBounds(EntityManager em, Entity entity, string attributeId, float min, float max)
    {
        if (!CanWrite(em, entity) || string.IsNullOrEmpty(attributeId) || max < min)
            return false;

        var stats = em.GetBuffer<AttributeStat>(entity);
        var index = GetIndex(stats, attributeId);
        if (index == -1)
            return false;

        var stat = stats[index];
        stat.Min = min;
        stat.Max = max;
        stats[index] = stat;
        Recalculate(em, entity, attributeId);
        return true;
    }

    /// <summary>Change la base de l'attribut et recalcule sa valeur actuelle.</summary>
    public static bool SetBase(EntityManager em, Entity entity, string attributeId, float value)
    {
        if (!CanWrite(em, entity) || string.IsNullOrEmpty(attributeId))
            return false;

        var stats = em.GetBuffer<AttributeStat>(entity);
        var index = GetIndex(stats, attributeId);
        if (index == -1)
            return false;

        var stat = stats[index];
        stat.Base = value;
        stats[index] = stat;
        Recalculate(em, entity, attributeId);
        return true;
    }

    /// <summary>Ajoute un effet sur l'attribut puis recalcule sa valeur actuelle.</summary>
    public static bool AddEffect(EntityManager em, Entity entity, string attributeId, string effectId, float value, AttributeEffectType type)
    {
        if (!CanWrite(em, entity) || string.IsNullOrEmpty(attributeId) || string.IsNullOrEmpty(effectId))
            return false;

        Add(em, entity, attributeId);

        var effects = em.GetBuffer<AttributeEffect>(entity);
        var attribute = new FixedString32Bytes(attributeId);
        var id = new FixedString32Bytes(effectId);
        for (var i = 0; i < effects.Length; i++)
        {
            if (effects[i].Attribute != attribute || effects[i].Id != id)
                continue;

            effects[i] = AttributeEffect.Create(attribute, id, value, type);
            Recalculate(em, entity, attributeId);
            return true;
        }

        effects.Add(AttributeEffect.Create(attribute, id, value, type));
        Recalculate(em, entity, attributeId);
        return true;
    }

    /// <summary>Met à jour un effet existant (ou l'ajoute s'il n'existe pas) puis recalcule.</summary>
    public static bool SetEffect(EntityManager em, Entity entity, string attributeId, string effectId, float value, AttributeEffectType type)
    {
        return AddEffect(em, entity, attributeId, effectId, value, type);
    }

    public static bool RemoveEffect(EntityManager em, Entity entity, string attributeId, string effectId)
    {
        if (!CanWrite(em, entity) || string.IsNullOrEmpty(attributeId) || string.IsNullOrEmpty(effectId))
            return false;

        var effects = em.GetBuffer<AttributeEffect>(entity);
        var attribute = new FixedString32Bytes(attributeId);
        var id = new FixedString32Bytes(effectId);
        for (var i = 0; i < effects.Length; i++)
        {
            if (effects[i].Attribute != attribute || effects[i].Id != id)
                continue;

            effects.RemoveAt(i);
            Recalculate(em, entity, attributeId);
            return true;
        }

        return false;
    }

    public static bool ClearEffects(EntityManager em, Entity entity, string attributeId)
    {
        if (!CanWrite(em, entity) || string.IsNullOrEmpty(attributeId))
            return false;

        var effects = em.GetBuffer<AttributeEffect>(entity);
        var attribute = new FixedString32Bytes(attributeId);
        for (var i = effects.Length - 1; i >= 0; i--)
        {
            if (effects[i].Attribute == attribute)
                effects.RemoveAt(i);
        }

        Recalculate(em, entity, attributeId);
        return true;
    }

    /// <summary>Effets d'un attribut, dans l'ordre d'application.</summary>
    public static List<AttributeEffect> GetEffects(EntityManager em, Entity entity, string attributeId)
    {
        var result = new List<AttributeEffect>();
        if (!CanRead(em, entity) || string.IsNullOrEmpty(attributeId))
            return result;

        var effects = em.GetBuffer<AttributeEffect>(entity);
        var attribute = new FixedString32Bytes(attributeId);
        for (var i = 0; i < effects.Length; i++)
        {
            if (effects[i].Attribute == attribute)
                result.Add(effects[i]);
        }

        return result;
    }

    public static List<AttributeStat> GetAll(EntityManager em, Entity entity)
    {
        var result = new List<AttributeStat>();
        if (!CanRead(em, entity))
            return result;

        var stats = em.GetBuffer<AttributeStat>(entity);
        for (var i = 0; i < stats.Length; i++)
            result.Add(stats[i]);

        return result;
    }

    /// <summary>
    /// Recalcule la valeur actuelle d'un attribut : on part de la base puis on parcourt
    /// sa liste d'effets dans l'ordre (additions et multiplications).
    /// </summary>
    public static float Recalculate(EntityManager em, Entity entity, string attributeId)
    {
        if (!CanWrite(em, entity) || string.IsNullOrEmpty(attributeId))
            return 0f;

        var stats = em.GetBuffer<AttributeStat>(entity);
        var statIndex = GetIndex(stats, attributeId);
        if (statIndex == -1)
            return 0f;

        var attribute = new FixedString32Bytes(attributeId);
        var value = stats[statIndex].Base;
        var effects = em.GetBuffer<AttributeEffect>(entity);
        for (var i = 0; i < effects.Length; i++)
        {
            if (effects[i].Attribute != attribute)
                continue;

            value = effects[i].Type == AttributeEffectType.Multiplication
                ? value * effects[i].Value
                : value + effects[i].Value;
        }

        var stat = stats[statIndex];
        stat.Actual = math.clamp(value, stat.Min, stat.Max);
        stats[statIndex] = stat;
        return stat.Actual;
    }

    private static int GetIndex(DynamicBuffer<AttributeStat> stats, string attributeId)
    {
        var id = new FixedString32Bytes(attributeId);
        for (var i = 0; i < stats.Length; i++)
        {
            if (stats[i].Id == id)
                return i;
        }

        return -1;
    }

    private static bool CanRead(EntityManager em, Entity entity)
    {
        return em != null && entity != Entity.Null && em.Exists(entity) && em.HasBuffer<AttributeStat>(entity) &&
               em.HasBuffer<AttributeEffect>(entity);
    }

    private static bool CanWrite(EntityManager em, Entity entity)
    {
        if (em == null || entity == Entity.Null || !em.Exists(entity))
            return false;

        if (!em.HasBuffer<AttributeStat>(entity))
            em.AddBuffer<AttributeStat>(entity);

        if (!em.HasBuffer<AttributeEffect>(entity))
            em.AddBuffer<AttributeEffect>(entity);

        return true;
    }
}
