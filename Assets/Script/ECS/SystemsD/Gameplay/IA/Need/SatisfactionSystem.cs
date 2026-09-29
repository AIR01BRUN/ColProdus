using Unity.Entities;

/// <summary>
/// Applique la satisfaction de chaque pawn (SatisfactionManager.Apply) :
///   - les paliers de ses besoins (faim, sommeil...) produisent un effet "needs" sur la
///     satisfaction (ex: faim entre 50 et 70 => -7) ;
///   - le palier de satisfaction atteint produit un effet sur la performance de travail
///     (ex: satisfaction entre 60 et 90 => -10%).
/// </summary>
public partial struct SatisfactionSystem : ISystem
{
    public void OnCreate(ref SystemState state)
    {
    }

    public void OnUpdate(ref SystemState state)
    {
        var em = state.EntityManager;
        var pawns = QuerryDB.QueryInstances<CurrentTask>(em, DataType.Pawn);

        foreach (var pawn in pawns)
        {
            if (pawn == Entity.Null || !em.Exists(pawn))
                continue;

            SatisfactionManager.Apply(em, pawn);
        }
    }
}
