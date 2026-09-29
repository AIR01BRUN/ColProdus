using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using Unity.Entities;

/// <summary>
/// Sélection (item + quantité) de l'ordre affiché par le joueur sur un bâtiment Market.
/// Buffer : permet de stocker plusieurs entrées (une seule actuellement).
/// Quantity n'est plus utilisé pour le calcul de la récompense : c'est la valeur totale
/// du step 1 (GetTotalValue) qui détermine le StepItemOut du step 2.
/// </summary>
public struct MarketSelection : IBufferElementData
{
    public Items Item;
}

/// <summary>
/// Type de l'ordre en cours sur le marché :
///   - SELL        : on vend les items sélectionnés, on reçoit leur valeur en pièces.
///   - BUY         : l'inverse de SELL, on achète les items sélectionnés contre des pièces.
///   - URGENT_BUY  : comme BUY mais la conversion demande 20 pts de travail et 2x de pièces.
///   - URGENT_SELL : comme URGENT_BUY mais à l'envers : on vend et on gagne 2x moins de pièces.
/// </summary>
public enum MarketType
{
    SELL,
    BUY,
    URGENT_BUY,
    URGENT_SELL
}

/// <summary>
/// Composant du bâtiment Market : type d'ordre courant. CoinsToGive est conservé par
/// compatibilité mais n'est plus utilisé pour la récompense : celle-ci passe désormais
/// par le StepItemOut du step 2 (renseigné par l'action buy/sell du step 1 au moment
/// où la collecte se termine).
/// </summary>
public struct MarketInfo : IComponentData
{
    public MarketType Type;
    public int CoinsToGive;
}

/// <summary>
/// Gère la procédure en 2 étapes des marchés :
///   step 1 : collecte (PtsWorkNeed = -1 : étape "en attente", sans worker). Elle ne se
///            travaille pas ; elle ne se termine que quand sa FinishCondition est vérifiée.
///            Cette étape porte l'action buy/sell ("MarketSell" / "MarketBuy") : à la fin
///            de la collecte, cette action calcule la valeur totale des items insérés dans
///            la procédure (GetTotalValue sur le step 1) et renseigne le StepItemOut du
///            step 2 (conversion).
///            - normal : FinishCondition = "IsNextDay" -> la collecte attend que la nouvelle
///              journée commence (00:00:00) puis passe à la conversion (cycle quotidien).
///            - urgent : FinishCondition vide -> la collecte se termine dès que les items
///              sont apportés, la conversion est immédiate (buy/sell direct).
///   step 2 : conversion, sans action, avec un StepItemOut renseigné par l'action du step 1 :
///            - "MarketSell" : StepItemOut = pièces dont la valeur vaut celle du step 1
///              (les items vendus, lus via GetTotalValue sur le step 1).
///            - "MarketBuy"  : StepItemOut = nombre d'items sélectionnés dont la valeur
///              vaut celle du step 1 (les pièces collectées : valeurStep1 / valeur 1 item).
///            Le pawn récupère automatiquement ce StepItemOut à la fin du step 2
///            (TaskProcedureSystem le lui verse).
///            Les ordres normaux n'ont aucune action : la conversion terminée, la
///            procédure boucle sur la collecte du jour suivant.
///            Les ordres urgents portent une action simple ("MarketUrgentSell" /
///            "MarketUrgentBuy") qui conserve la sélection du marché et stoppe la
///            procédure (un ordre urgent ne se fait qu'une fois).
/// Bonus : la collecte alimente aussi le buffer ItemNeed de l'inventaire (bâtiment
/// "inventory") de la zone, comme un besoin de stock permanent.
/// </summary>
public static class MarketManager
{
    public static int GetItemValue(string itemId)
    {
        if (string.IsNullOrEmpty(itemId))
            return 0;

        var definition = QuerryDB.QueryDefinitions<ItemDefinition>(DataType.Item, itemId).FirstOrDefault();
        return definition != null ? definition.Value : 0;
    }

    public static int GetTotalValue(Items item)
    {
        return GetItemValue(item.ItemId.ToString()) * item.Quantity;
    }

    /// <summary>
    /// Retourne la valeur totale de l'entité passée en paramètre :
    ///   - un step (buffer StepItemIn) -> valeur de ce qui est "mis In" par l'étape.
    ///   - un inventaire (buffer SlotItem) -> valeur de tout son contenu.
    /// Somme de (valeur de l'item × quantité).
    /// </summary>
    public static int GetTotalValue(EntityManager em, Entity entity)
    {
        if (em == null || entity == Entity.Null || !em.Exists(entity))
            return 0;

        if (em.HasBuffer<StepItemIn>(entity))
        {
            var itemIns = em.GetBuffer<StepItemIn>(entity);
            var total = 0;
            for (int i = 0; i < itemIns.Length; i++)
                total += GetTotalValue(itemIns[i].Item);
            return total;
        }

        if (em.HasBuffer<Items>(entity))
        {
            var slots = em.GetBuffer<Items>(entity);
            var total = 0;
            for (int i = 0; i < slots.Length; i++)
                total += GetTotalValue(slots[i]);
            return total;
        }

        return 0;
    }

    /// <summary>Applique (ou met à jour) l'ordre du marché pour le type donné.</summary>
    public static bool SetMarket(EntityManager em, Entity market, MarketType type, Items selection)
    {
        if (em == null || market == Entity.Null || !em.Exists(market))
            return false;

        if (selection.ItemId.Length == 0 || selection.Quantity <= 0)
            return false;

        var definition = QuerryDB.QueryDefinitions<ItemDefinition>(DataType.Item, selection.ItemId.ToString()).FirstOrDefault();
        if (definition == null)
            return false;

        var isSell = type == MarketType.SELL || type == MarketType.URGENT_SELL;
        var urgent = type == MarketType.URGENT_BUY || type == MarketType.URGENT_SELL;

        // Sélection (buffer) : on remplace la sélection par l'entrée courante.
        var previous = new List<MarketSelection>();
        if (em.HasBuffer<MarketSelection>(market))
        {
            var oldBuffer = em.GetBuffer<MarketSelection>(market);
            for (int i = 0; i < oldBuffer.Length; i++)
                previous.Add(oldBuffer[i]);
            oldBuffer.Clear();
        }
        else
        {
            em.AddBuffer<MarketSelection>(market);
        }
        em.GetBuffer<MarketSelection>(market).Add(new MarketSelection { Item = new Items(selection.ItemId, selection.Quantity) });

        // Type + montant à donner (réinitialisé à chaque ordre).
        // Pour SELL / URGENT_SELL, CoinsToGive est un snapshot de la valeur de l'ordre
        // (les items sont livrés en intégralité avant la fin de la step de collecte).
        var coinsToGive = 0;
        if (isSell)
        {
            coinsToGive = GetTotalValue(selection);
            if (urgent)
                coinsToGive /= 2;
        }

        if (!em.HasComponent<MarketInfo>(market))
            em.AddComponentData(market, new MarketInfo { Type = type, CoinsToGive = coinsToGive });
        else
        {
            var info = em.GetComponentData<MarketInfo>(market);
            info.Type = type;
            info.CoinsToGive = coinsToGive;
            em.SetComponentData(market, info);
        }

        var requiredCoins = 0;
        if (!isSell)
        {
            requiredCoins = GetTotalValue(selection);
            if (type == MarketType.URGENT_BUY)
                requiredCoins *= 2;
        }

        // Step 1 : collecte (StepItemIn = items à vendre OU pièces à payer).
        //   - PtsWorkNeed = -1 : étape "en attente", elle ne se travaille pas et ne lance
        //     aucune action ; elle termine quand sa FinishCondition est vérifiée.
        //   - ActionId = l'action buy/sell ("MarketSell" / "MarketBuy") : elle s'exécute
        //     quand la collecte se termine et renseigne le StepItemOut du step 2 avec la
        //     valeur totale des items insérés dans la procédure.
        //   - FinishCondition = "IsNextDay" (ordres normaux) : la collecte attend que la
        //     nouvelle journée commence (00:00:00) puis passe à la conversion (quotidien).
        //   - FinishCondition vide (urgent) : la collecte se termine dès que les items sont
        //     apportés, la conversion est immédiate (buy/sell direct).
        var step1In = new List<Items>
        {
            isSell
                ? new Items(selection.ItemId, selection.Quantity)
                : new Items("coin", requiredCoins)
        };

        // La sélection est conservée même si le bâtiment n'est pas encore opérationnel ;
        // la procédure ne sera construite qu'une fois la construction finie.
        if (em.HasComponent<StateBuilding>(market))
        {
            var state = em.GetComponentData<StateBuilding>(market);
            if (state.State != StateBuild.Available)
            {
                RefreshZoneItemNeed(em, market, previous, step1In);
                return true;
            }
        }

        if (em.HasBuffer<ProcedureStep>(market) || em.HasComponent<ProcedureState>(market))
            ProcedureManager.Destroy(em, market);

        ProcedureManager.AddStep(em, market, new ProcuredCreationInfo
        {
            PtsWorkNeed = -1f,
            RequiresWorker = false,
            RequireActivation = false,
            ActionId = isSell
                ? new FixedString64Bytes("MarketSell")
                : new FixedString64Bytes("MarketBuy"),
            FinishCondition = urgent ? default : new FixedString64Bytes("IsNextDay"),
            ItemIn = step1In,
            ItemOut = new List<Items>()
        });

        // Step 2 : conversion, sans action, portant le StepItemOut renseigné par l'action
        //   du step 1 une fois la collecte terminée :
        //  - SELL / URGENT_SELL : StepItemOut = les pièces dont la valeur vaut celle du
        //    step 1 (renseigné par MarketSell).
        //  - BUY / URGENT_BUY : StepItemOut = le nombre d'items sélectionnés dont la
        //    valeur vaut celle du step 1 (renseigné par MarketBuy).
        //  - URGENT : seule exception, une action simple ("MarketUrgentSell"/"MarketUrgentBuy")
        //    qui conserve la sélection du marché et stoppe la procédure après la conversion
        //    (l'ordre urgent ne se fait qu'une fois). Les ordres normaux n'ont pas d'action.
        ProcedureManager.AddStep(em, market, new ProcuredCreationInfo
        {
            PtsWorkNeed = urgent ? 20f : 1f,
            RequiresWorker = true,
            RequireActivation = false,
            ActionId = urgent
                ? isSell
                    ? new FixedString64Bytes("MarketUrgentSell")
                    : new FixedString64Bytes("MarketUrgentBuy")
                : default,
            ItemIn = new List<Items>(),
            ItemOut = new List<Items>()
        });

        RefreshZoneItemNeed(em, market, previous, step1In);
        return true;
    }

    /// <summary>
    /// Bonus inventaire : renseigne le buffer ItemNeed du stock de l'inventaire de la
    /// zone avec la collecte de l'ordre (anciennes entrées retirées, nouvelles posées).
    /// </summary>
    private static void RefreshZoneItemNeed(EntityManager em, Entity market, List<MarketSelection> previousItems, List<Items> needs)
    {
        if (!em.HasComponent<WorkZoneLink>(market))
            return;

        var zone = em.GetComponentData<WorkZoneLink>(market).WorkZone;
        if (!WorkZoneManager.TryGetBuildingWithInventory(em, zone, out var inventoryBuilding) ||
            !em.HasComponent<InventoryLink>(inventoryBuilding))
            return;

        var stock = em.GetComponentData<InventoryLink>(inventoryBuilding).Inventory;
        if (stock == Entity.Null || !em.Exists(stock) || !em.HasBuffer<ItemNeed>(stock))
            return;

        if (previousItems != null)
        {
            foreach (var selection in previousItems)
                RemoveItemNeed(em, stock, selection.Item.ItemId.ToString());
        }

        foreach (var need in needs)
            RemoveItemNeed(em, stock, need.ItemId.ToString());
        foreach (var need in needs)
            InventoryManager.AddItemNeed(em, stock, new Items(need.ItemId, need.Quantity));
    }

    private static void RemoveItemNeed(EntityManager em, Entity stock, string itemId)
    {
        if (string.IsNullOrEmpty(itemId))
            return;

        var buffer = em.GetBuffer<ItemNeed>(stock);
        var idValue = new FixedString64Bytes(itemId);
        for (int i = buffer.Length - 1; i >= 0; i--)
        {
            if (buffer[i].Item.ItemId.Equals(idValue))
                buffer.RemoveAt(i);
        }
    }

/// <summary>
/// Récupère le step 1 et le step 2 d'une procédure (market).
/// </summary>
private static bool TryGetSteps(EntityManager em, Entity market, out Entity step1, out Entity step2)
{
    step1 = Entity.Null;
    step2 = Entity.Null;

    if (em == null || market == Entity.Null || !em.Exists(market) || !em.HasBuffer<ProcedureStep>(market))
        return false;

    var steps = em.GetBuffer<ProcedureStep>(market);
    if (steps.Length < 2)
        return false;

    step1 = steps[0].Value;
    step2 = steps[1].Value;
    return step1 != Entity.Null && em.Exists(step1) && step2 != Entity.Null && em.Exists(step2);
}

/// <summary>Réinitialise le StepItemOut du step donné.</summary>
private static void ResetStepItemOut(EntityManager em, Entity step)
{
    if (em.HasBuffer<StepItemOut>(step))
        em.GetBuffer<StepItemOut>(step).Clear();
}

/// <summary>
/// Retourne le coefficient de pénalité d'un ordre urgent :
///   - URGENT_BUY  : on paie 2x les pièces pour la même quantité -> quantité / 2.
///   - URGENT_SELL : on gagne 2x moins de pièces -> valeur / 2.
/// Renvoie 1 pour les ordres normaux.
/// </summary>
private static int UrgentPenalty(EntityManager em, Entity market)
{
    if (em.HasComponent<MarketInfo>(market))
    {
        var type = em.GetComponentData<MarketInfo>(market).Type;
        if (type == MarketType.URGENT_BUY || type == MarketType.URGENT_SELL)
            return 2;
    }
    return 1;
}

/// <summary>
/// Action buy/sell portée par le step 1 : elle s'exécute quand la collecte se termine
/// (après NextStep) et renseigne le StepItemOut du step 2 (conversion) avec les
/// pièces correspondant à la valeur totale de ce qui est "mis In" au step 1
/// (GetTotalValue sur le step 1, c'est-à-dire la valeur des items vendus).
/// Un ordre urgent ne rapporte que la moitié (pénalité).
/// Le pawn récupère ces pièces automatiquement à la fin du step 2 (TaskProcedureSystem
/// lui verse le StepItemOut).
/// </summary>
public static void MarketSell(EntityManager em, Entity targetEntity)
{
    if (!TryGetSteps(em, targetEntity, out var step1, out var step2))
        return;

    // Valeur totale du step 1 (les items vendus) -> pièces à donner au pawn.
    var totalValue = GetTotalValue(em, step1) / UrgentPenalty(em, targetEntity);

    ResetStepItemOut(em, step2);
    if (totalValue > 0)
    {
        em.GetBuffer<StepItemOut>(step2).Add(new StepItemOut
        {
            Item = new Items("coin", totalValue),
            Luck = 100f
        });
    }
}

/// <summary>
/// Action buy/sell portée par le step 1 : elle s'exécute quand la collecte se termine
/// (après NextStep) et renseigne le StepItemOut du step 2 (conversion) avec le nombre
/// d'items sélectionnés dont la valeur totale vaut la valeur du step 1 (les pièces
/// collectées) : quantity = valeurStep1 / valeur unitaire de l'item.
/// Un ordre urgent paie 2x les pièces pour la même quantité (pénalité).
/// Le pawn récupère ces items automatiquement à la fin du step 2 (TaskProcedureSystem
/// lui verse le StepItemOut).
/// </summary>
public static void MarketBuy(EntityManager em, Entity targetEntity)
{
    if (!TryGetSteps(em, targetEntity, out var step1, out var step2))
        return;

    var selections = em.GetBuffer<MarketSelection>(targetEntity);
    if (selections.Length == 0 || selections[0].Item.ItemId.Length == 0)
        return;

    var itemId = selections[0].Item.ItemId.ToString();
    var unitValue = GetItemValue(itemId);
    if (unitValue <= 0)
        return;

    // Valeur totale du step 1 (les pièces collectées) -> nombre d'items équivalents.
    var totalValue = GetTotalValue(em, step1) / UrgentPenalty(em, targetEntity);

    ResetStepItemOut(em, step2);
    var quantity = totalValue / unitValue;
    if (quantity > 0)
    {
        em.GetBuffer<StepItemOut>(step2).Add(new StepItemOut
        {
            Item = new Items(selections[0].Item.ItemId, quantity),
            Luck = 100f
        });
    }
}

    /// <summary>
    /// Action du step 2 d'un ordre urgent : la conversion (StepItemOut) a déjà été
    /// renseignée par l'action "MarketSell" du step 1. Cette action se contente de
    /// conserver la sélection du marché (buffer MarketSelection intact) et de stopper
    /// la procédure : un ordre urgent ne se fait qu'une fois.
    /// </summary>
    public static void MarketUrgentSell(EntityManager em, Entity targetEntity)
    {
        ProcedureManager.Destroy(em, targetEntity);
    }

    /// <summary>
    /// Action du step 2 d'un ordre urgent : identique à MarketUrgentSell (conserve la
    /// sélection du marché et stoppe la procédure, le StepItemOut ayant été renseigné
    /// par l'action "MarketBuy" du step 1).
    /// </summary>
    public static void MarketUrgentBuy(EntityManager em, Entity targetEntity)
    {
        ProcedureManager.Destroy(em, targetEntity);
    }
}

/// <summary>
/// Quand une nouvelle journée commence (00:00:00), les marchés dont la step courante
/// requiert une activation (ordres normaux) sont activés : leur collecte démarre alors.
/// Les ordres urgents (RequireActivation = false) démarrent immédiatement, sans attendre
/// le début de journée.
/// </summary>
public partial struct MarketDayEndSystem : ISystem
{
    private int _lastDay;

    public void OnCreate(ref SystemState state)
    {
        _lastDay = -1;
    }

    public void OnUpdate(ref SystemState state)
    {
        if (!SystemAPI.TryGetSingleton<WorldTime>(out var worldTime))
            return;

        if (_lastDay == -1)
        {
            _lastDay = worldTime.Day;
            return;
        }

        if (worldTime.Day == _lastDay)
            return;

        _lastDay = worldTime.Day;

        var em = state.EntityManager;
        var markets = QuerryDB.QueryInstances(DataType.Building, "market");
        foreach (var market in markets)
        {
            if (!em.Exists(market) || !em.HasComponent<MarketInfo>(market) ||
                !em.HasBuffer<MarketSelection>(market))
                continue;

            if (em.HasComponent<StateBuilding>(market) &&
                em.GetComponentData<StateBuilding>(market).State == StateBuild.UnderConstruct)
                continue;

            if (!em.HasComponent<ProcedureState>(market) || !em.HasBuffer<ProcedureStep>(market))
                continue;

            var procedureState = em.GetComponentData<ProcedureState>(market);
            var steps = em.GetBuffer<ProcedureStep>(market);
            if (procedureState.CurrentStep < 0 || procedureState.CurrentStep >= steps.Length)
                continue;

            var stepEntity = steps[procedureState.CurrentStep].Value;
            if (!em.Exists(stepEntity) || !em.HasComponent<Step>(stepEntity))
                continue;

            var stepData = em.GetComponentData<Step>(stepEntity);
            if (stepData.RequireActivation && !procedureState.Activation)
            {
                // SELL : snapshot du jour = valeur de l'ordre (les items sont livrés en
                // intégralité avant la fin de la collecte). URGENT_SELL n'atteint jamais
                // ce bloc (RequireActivation false) : sa valeur a été fixée dans SetMarket.
                var marketInfo = em.GetComponentData<MarketInfo>(market);
                if (marketInfo.Type == MarketType.SELL)
                {
                    var selection = em.GetBuffer<MarketSelection>(market)[0];
                    marketInfo.CoinsToGive = MarketManager.GetTotalValue(selection.Item);
                    em.SetComponentData(market, marketInfo);
                }

                procedureState.Activation = true;
                em.SetComponentData(market, procedureState);
            }
        }
    }
}