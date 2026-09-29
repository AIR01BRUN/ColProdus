using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

/// <summary>
/// Drain des besoins des pawns au fil du temps. Les taux de consommation sont lus dans
/// les attributs centralisés (AttributeManager) : chaque besoin possède son attribut de
/// consommation (hunger_rate_consume, sleep_rate_consume), ce qui permet de les modifier
/// avec des effets (items, bâtiments, besoins) sans toucher au code.
/// La performance de travail (AttributeManager.WorkPerformance) est alimentée par un
/// effet "needs" (multiplication) : elle reste à 100% tant que le niveau moyen des
/// besoins est >= 30%, puis descend proportionnellement en dessous de 30% (jusqu'à 0).
/// </summary>
public partial struct PawnNeedSystem : ISystem
{
    private const float PerformanceDropThreshold = 0.30f;

    public void OnUpdate(ref SystemState state)
    {
        var em = state.EntityManager;
        var pawns = QuerryDB.QueryInstances<CurrentTask>(em, DataType.Pawn);

        foreach (var pawn in pawns)
        {
            if (pawn == Entity.Null || !em.Exists(pawn) || !em.HasBuffer<PawnNeed>(pawn))
                continue;

            var needs = em.GetBuffer<PawnNeed>(pawn);
            for (var i = 0; i < needs.Length; i++)
            {
                var need = needs[i];
                need.Current = math.max(0f, need.Current -
                    AttributeManager.GetActual(em, pawn, GetConsumeAttribute(need.Id)) * Time.deltaTime);
                needs[i] = need;
            }

            var total = 0f;
            foreach (var need in needs)
                total += need.GetPercentage();

            var average = needs.Length > 0 ? total / needs.Length : 1f;
            var multiplier = math.min(1f, math.clamp(average, 0f, 1f) / PerformanceDropThreshold);
            AttributeManager.SetEffect(em, pawn, AttributeManager.WorkPerformance, AttributeManager.NeedsEffect,
                multiplier, AttributeEffectType.Multiplication);
        }
    }

    private static string GetConsumeAttribute(FixedString32Bytes needId)
    {
        if (needId == new FixedString32Bytes(PawnNeedManager.Faim))
            return AttributeManager.HungerRateConsume;
        if (needId == new FixedString32Bytes(PawnNeedManager.Sommeil))
            return AttributeManager.SleepRateConsume;
        return string.Empty;
    }
}
