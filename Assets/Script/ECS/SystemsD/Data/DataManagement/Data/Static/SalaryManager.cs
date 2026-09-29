using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

/// <summary>Salaire d'un pawn : montant défini manuellement (0 = 10% de la valeur du pawn).</summary>
public struct PawnSalary : IComponentData
{
    public float Amount;
}

/// <summary>Effet de salaire cumulé d'un pawn sur sa satisfaction (borné entre -50 et +50).</summary>
public struct SalaryState : IComponentData
{
    public float Effect;
}

/// <summary>Réglages de la paie de la colonie (salaire minimum commun à tous les pawns).</summary>
public struct SalarySettings : IComponentData
{
    public float Minimum;
}

/// <summary>Marqueur posé sur les bâtiments SalaryAdministrator (panneau de détail de l'UI).</summary>
public struct SalaryAdministrator : IComponentData
{
}

/// <summary>Data d'une tâche de salaire portée par la tâche GetSalary.</summary>
public struct SalaryTask : IComponentData
{
    public Entity Pawn;
    public Entity Building;
    public int Expected;
}

/// <summary>
/// Salaires de la colonie. Le bâtiment SalaryAdministrator est un inventaire qui
/// demande (ItemNeed) des items de type currency (coin) à hauteur de la masse salariale :
/// cette demande est rafraîchie à chaque fois qu'un salaire de pawn change
/// (SalaryManager.SetSalary) ou qu'une valeur de pawn change (PawnValueManager.Refresh),
/// et c'est InventorySystem qui fait ensuite apporter la monnaie par une tâche TaskNeedItem.
/// À la fin de chaque semaine (WorldManagerSystem), une tâche GetSalary est posée sur
/// chaque pawn : il va chercher sa monnaie dans le bâtiment, puis l'action "GetSalary"
/// règle sa paie : elle retire les pièces de l'inventaire du bâtiment, les donne au pawn
/// et publie l'impact sur la satisfaction :
///   - rien reçu          : -50
///   - salaire incomplet  : -30
///   - salaire complet    : ce qu'il a touché est comparé à ce qu'il mérite
///                           (10% de sa valeur) : plus de 10% en dessous => -10 par
///                           tranche de 10%, plus de 10% au dessus => +10 par tranche.
/// L'effet est cumulé de semaine en semaine (de -50 à +50).
/// </summary>
public static class SalaryManager
{
    public const string BuildingId = "salaryadministrator";
    public const string CurrencyId = "coin";
    public const string TaskId = "GetSalary";
    public const string ActionId = "GetSalary";
    public const string EffectId = "salary";
    public const int DaysPerWeek = 7;
    public const float BaseSalary = 10f;

    public const float MinEffect = -50f;
    public const float MaxEffect = 50f;
    public const float NoPayPenalty = -50f;
    public const float PartialPenalty = -30f;
    public const float TrancheSize = 0.10f;
    public const float TranchePenalty = 10f;

    public static void Initialize(EntityManager em, Entity pawn)
    {
        if (em == null || pawn == Entity.Null || !em.Exists(pawn))
            return;

        if (!em.HasComponent<PawnSalary>(pawn))
            em.AddComponentData(pawn, new PawnSalary { Amount = 0f });

        if (!em.HasComponent<SalaryState>(pawn))
            em.AddComponentData(pawn, new SalaryState { Effect = 0f });
    }

    /// <summary>Salaire dû à un pawn : son salaire défini, sinon le maximum entre le salaire minimum et 10% de sa valeur.</summary>
    public static float GetExpectedSalary(EntityManager em, Entity pawn)
    {
        if (em == null || pawn == Entity.Null || !em.Exists(pawn))
            return 0f;

        if (em.HasComponent<PawnSalary>(pawn))
        {
            var salary = em.GetComponentData<PawnSalary>(pawn);
            if (salary.Amount > 0f)
                return salary.Amount;
        }

        return math.max(GetMinimumSalary(em), PawnValueManager.GetDeservedSalary(em, pawn));
    }

    /// <summary>Salaire minimum commun à tous les pawns (10 par défaut).</summary>
    public static float GetMinimumSalary(EntityManager em)
    {
        if (em == null)
            return BaseSalary;

        using (var query = em.CreateEntityQuery(ComponentType.ReadOnly<SalarySettings>()))
        {
            if (query.IsEmptyIgnoreFilter)
                return BaseSalary;

            return math.max(0f, query.GetSingleton<SalarySettings>().Minimum);
        }
    }

    /// <summary>Définit le salaire minimum commun puis rafraîchit la demande du bâtiment.</summary>
    public static bool SetMinimumSalary(EntityManager em, float amount)
    {
        if (em == null)
            return false;

        Entity settings;
        using (var query = em.CreateEntityQuery(ComponentType.ReadWrite<SalarySettings>()))
        {
            if (query.IsEmptyIgnoreFilter)
            {
                settings = em.CreateEntity();
                em.AddComponentData(settings, new SalarySettings { Minimum = math.max(0f, amount) });
                em.SetName(settings, "SALARY SETTINGS");
            }
            else
            {
                settings = query.GetSingletonEntity();
                var current = query.GetSingleton<SalarySettings>();
                current.Minimum = math.max(0f, amount);
                em.SetComponentData(settings, current);
            }
        }

        RefreshDemand(em);
        return true;
    }

    /// <summary>Définit le salaire d'un pawn puis rafraîchit la demande du bâtiment.</summary>
    public static bool SetSalary(EntityManager em, Entity pawn, float amount)
    {
        if (em == null || pawn == Entity.Null || !em.Exists(pawn))
            return false;

        Initialize(em, pawn);
        var salary = em.GetComponentData<PawnSalary>(pawn);
        salary.Amount = math.max(0f, amount);
        em.SetComponentData(pawn, salary);

        RefreshDemand(em);
        return true;
    }

    /// <summary>Masse salariale de la colonie (somme des salaires dus à tous les pawns).</summary>
    public static int GetTotalSalary(EntityManager em)
    {
        var total = 0f;
        foreach (var pawn in QuerryDB.QueryInstances<CurrentTask>(em, DataType.Pawn))
        {
            if (pawn == Entity.Null || !em.Exists(pawn))
                continue;

            total += GetExpectedSalary(em, pawn);
        }

        return (int)math.floor(total);
    }

    /// <summary>
    /// Rafraîchit la demande (ItemNeed) de currency de tous les bâtiments de salaire :
    /// la quantité demandée vaut la masse salariale de la colonie. Comme
    /// InventorySystem compare le stock à cette demande, un changement de salaire
    /// regénère automatiquement la tâche qui apporte les pièces.
    /// </summary>
    public static bool RefreshDemand(EntityManager em)
    {
        var totalSalary = GetTotalSalary(em);
        var updated = false;
        foreach (var building in GetBuildings(em))
        {
            var inventory = em.GetComponentData<InventoryLink>(building).Inventory;
            updated |= SetCurrencyNeed(em, inventory, totalSalary);
        }

        return updated;
    }

    /// <summary>
    /// Pièces de currency demandées au bâtiment (ItemNeed) : c'est la masse salariale,
    /// donc le nombre de pièces qu'il faut apporter dans la caisse pour payer tout le monde.
    /// </summary>
    public static int GetNeededCurrency(EntityManager em)
    {
        return GetTotalSalary(em);
    }

    /// <summary>Monnaie actuellement en caisse dans le(s) bâtiment(s) de salaire.</summary>
    public static int GetCash(EntityManager em)
    {
        var total = 0;
        foreach (var building in GetBuildings(em))
        {
            var inventory = em.GetComponentData<InventoryLink>(building).Inventory;
            total += GetCurrencyQuantity(em, inventory);
        }

        return total;
    }

    /// <summary>Impact de la semaine sur la satisfaction, à partir de ce qui a été reçu.</summary>
    public static float GetWeeklyDelta(int received, float expected, float deserved)
    {
        if (received <= 0)
            return NoPayPenalty;

        if (expected <= 0f || received < expected)
            return PartialPenalty;

        if (deserved <= 0f)
            return 0f;

        var ratio = received / deserved;
        if (ratio < 1f - TrancheSize)
            return -TranchePenalty * math.ceil((1f - ratio) / TrancheSize);

        if (ratio > 1f + TrancheSize)
            return TranchePenalty * math.ceil((ratio - 1f) / TrancheSize);

        return 0f;
    }

    /// <summary>
    /// Action "GetSalary" : règle la paie d'un pawn sur la tâche GetSalary. La valeur
    /// reçue est retirée de l'inventaire du bâtiment, donnée au pawn, puis comparée à son
    /// salaire attendu pour publier l'effet sur sa satisfaction.
    /// </summary>
    public static void GetSalary(EntityManager em, Entity targetEntity)
    {
        if (em == null || targetEntity == Entity.Null || !em.Exists(targetEntity) ||
            !em.HasComponent<SalaryTask>(targetEntity))
            return;

        var salary = em.GetComponentData<SalaryTask>(targetEntity);
        GetSalary(em, salary.Pawn, salary.Expected, salary.Building);
    }

    /// <summary>
    /// Retire la monnaie Due à un pawn de l'inventaire du bâtiment, la lui donne et
    /// publie l'impact sur sa satisfaction. Retourne la quantité réellement reçue.
    /// </summary>
    public static int GetSalary(EntityManager em, Entity pawn, int expected, Entity building)
    {
        if (em == null || pawn == Entity.Null || !em.Exists(pawn))
            return 0;

        var received = TakeCurrency(em, pawn, expected, building);
        Apply(em, pawn, received);
        return received;
    }

    /// <summary>Prend la monnaie dans le stock du bâtiment et la transfère au pawn.</summary>
    public static int TakeCurrency(EntityManager em, Entity pawn, int expected, Entity building)
    {
        if (!TryGetInventory(em, building, out var buildingInventory))
            return 0;

        var received = math.min(math.max(0, expected), GetCurrencyQuantity(em, buildingInventory));
        if (received <= 0 || !em.HasComponent<InventoryLink>(pawn))
            return 0;

        var pawnInventory = em.GetComponentData<InventoryLink>(pawn).Inventory;
        if (pawnInventory == Entity.Null || !em.Exists(pawnInventory))
            return 0;

        // On ne retire la monnaie que si le pawn peut réellement la stocker,
        // sinon elle serait perdue (AddItem ignore silencieusement un inventaire plein).
        if (!InventoryManager.CanAddItem(em, pawnInventory, CurrencyId))
            return 0;

        var coins = new Items(new FixedString64Bytes(CurrencyId), received);
        InventoryManager.RemoveItem(em, buildingInventory, coins);
        InventoryManager.AddItem(em, pawnInventory, coins);

        // La caisse du bâtiment a été vidée : la demande de salary est republiée.
        RefreshDemand(em);
        return received;
    }

    /// <summary>
    /// Cumul de l'impact hebdomadaire sur la satisfaction du pawn et publication de
    /// l'effet "salary" sur l'attribut satisfaction.
    /// </summary>
    public static float Apply(EntityManager em, Entity pawn, int received)
    {
        if (em == null || pawn == Entity.Null || !em.Exists(pawn))
            return 0f;

        Initialize(em, pawn);
        SatisfactionManager.Initialize(em, pawn);

        var expected = GetExpectedSalary(em, pawn);
        var deserved = PawnValueManager.GetDeservedSalary(em, pawn);
        var delta = GetWeeklyDelta(received, expected, deserved);

        var state = em.GetComponentData<SalaryState>(pawn);
        state.Effect = math.clamp(state.Effect + delta, MinEffect, MaxEffect);
        em.SetComponentData(pawn, state);

        AttributeManager.SetEffect(em, pawn, SatisfactionManager.AttributeId, SatisfactionManager.SalaryEffectId,
            state.Effect, AttributeEffectType.Addition);
        return state.Effect;
    }

    /// <summary>Paie de la semaine : pose une tâche GetSalary sur chaque pawn.</summary>
    public static void CreatePaydayTasks(EntityManager em)
    {
        if (em == null)
            return;

        var buildings = GetBuildings(em);
        if (buildings.Count == 0)
            return;

        foreach (var pawn in QuerryDB.QueryInstances<CurrentTask>(em, DataType.Pawn))
        {
            if (pawn == Entity.Null || !em.Exists(pawn))
                continue;

            CreatePaydayTask(em, pawn, buildings[0]);
        }
    }

    private static void CreatePaydayTask(EntityManager em, Entity pawn, Entity building)
    {
        if (!em.HasComponent<TaskSend>(pawn))
            em.AddComponentData(pawn, new TaskSend());

        var taskSend = em.GetComponentData<TaskSend>(pawn);
        if (taskSend.Task != Entity.Null && em.Exists(taskSend.Task))
            TaskManager.Finish(em, taskSend.Task);

        var task = TaskManager.Create(em, TaskId, building, requiresPawn: true);
        var taskData = em.GetComponentData<Task>(task);
        taskData.Priority = 1;
        taskData.CanBeDone = true;
        em.SetComponentData(task, taskData);

        em.AddComponentData(task, new SalaryTask
        {
            Pawn = pawn,
            Building = building,
            Expected = (int)math.floor(GetExpectedSalary(em, pawn))
        });

        taskSend = em.GetComponentData<TaskSend>(pawn);
        taskSend.Task = task;
        em.SetComponentData(pawn, taskSend);
    }

    /// <summary>Bâtiments SalaryAdministrator construits, dont l'inventaire est exploitable.</summary>
    public static List<Entity> GetBuildings(EntityManager em)
    {
        var result = new List<Entity>();
        foreach (var building in QuerryDB.QueryInstances<SalaryAdministrator>(em, DataType.Building))
        {
            if (!TryGetInventory(em, building, out _))
                continue;

            result.Add(building);
        }

        return result;
    }

    /// <summary>Inventaire de stock d'un bâtiment de salaire (construit et non en ruine).</summary>
    public static bool TryGetInventory(EntityManager em, Entity building, out Entity inventory)
    {
        inventory = Entity.Null;
        if (em == null || building == Entity.Null || !em.Exists(building) || em.HasComponent<BuildingRubble>(building))
            return false;

        if (!em.HasComponent<StateBuilding>(building) ||
            em.GetComponentData<StateBuilding>(building).State != StateBuild.Available)
            return false;

        if (!em.HasComponent<InventoryLink>(building))
            return false;

        inventory = em.GetComponentData<InventoryLink>(building).Inventory;
        return inventory != Entity.Null && em.Exists(inventory);
    }

    public static int GetCurrencyQuantity(EntityManager em, Entity inventory)
    {
        if (em == null || inventory == Entity.Null || !em.Exists(inventory) || !em.HasBuffer<Items>(inventory))
            return 0;

        var index = InventoryManager.GetItemIndex(em, inventory, CurrencyId);
        if (index == -1)
            return 0;

        return math.max(0, em.GetBuffer<Items>(inventory)[index].Quantity);
    }

    /// <summary>
    /// Demande (ItemNeed) de l'inventaire de salaire : la quantité de currency
    /// correspondant à la masse salariale. Les autres demandes de l'inventaire
    /// ne sont pas touchées.
    /// </summary>
    public static bool SetCurrencyNeed(EntityManager em, Entity inventory, int quantity)
    {
        if (em == null || inventory == Entity.Null || !em.Exists(inventory))
            return false;

        quantity = math.max(0, quantity);
        if (!em.HasBuffer<ItemNeed>(inventory))
            em.AddBuffer<ItemNeed>(inventory);

        var needs = em.GetBuffer<ItemNeed>(inventory);
        var currency = new FixedString64Bytes(CurrencyId);
        for (var i = 0; i < needs.Length; i++)
        {
            if (needs[i].Item.ItemId != currency)
                continue;

            if (needs[i].Item.Quantity == quantity)
                return false;

            needs[i] = new ItemNeed { Item = new Items(currency, quantity) };
            return true;
        }

        needs.Add(new ItemNeed { Item = new Items(currency, quantity) });
        return true;
    }
}
