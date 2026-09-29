using System.Linq;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;


public partial struct NewGameManagementSystem : ISystem
{
    public void OnCreate(ref SystemState state)
    {
        
        state.RequireForUpdate<NewGameRequest>();
    }

    public void OnUpdate(ref SystemState state)
    {
        var entityManager = state.EntityManager;
        var em =  entityManager ;
        var request = SystemAPI.GetSingleton<NewGameRequest>();
        var requestE = SystemAPI.GetSingletonEntity<NewGameRequest>();

        GameSpeedController.Reset();
        WorldManager.Create(em);
            ScheduleManager.Create(em);
            var board = BoardManager.Create(em,"board",request.BoardSize);
            var allRessources = QuerryDB.QueryDefinitions<RessourceDefinition>(DataType.Ressource);
            var chunkCount = Mathf.Max(2, request.BoardSize.x / 12);

            for (var resourceIndex = 0; resourceIndex < allRessources.Count; resourceIndex++)
            {
                var ressource = allRessources[resourceIndex];
                if (ressource.QuarryOnly)
                    continue;

                RessourceChunkManager.AddOn(em,board,ressource.Id,new RessourceChunkCreationData
                    {
                        ChunkSize = new int2(2, 2),
                        ChunkCount = chunkCount,
                        Density = 0.5f,
                        Seed = request.Seed + (uint)resourceIndex
                    },true);
            }

            CreateTestMapContent(em, board);

            entityManager.DestroyEntity( requestE);
        }
    public static void CreateNewGame(EntityManager entityManager, int2 boardSize)
    {
        var newGame = entityManager.CreateEntity();
        entityManager.AddComponentData(newGame,new NewGameRequest {
            BoardSize = new int2(192, 192),
            Seed = 5,
            IsPerformanceTest = false
        });
    }

    private static void CreateTestMapContent(EntityManager em, Entity board)
    {
        var zoneTypes = new[] { "camp", "farm", "quarry", "sawmill", "workshop", "marketplace" };
        const int columns = 3;
        const int separation = 3;
        var start = new int2(2, 2);

        var rows = (zoneTypes.Length + columns - 1) / columns;
        var columnWidths = new int[columns];
        var rowHeights = new int[rows];

        for (var typeIndex = 0; typeIndex < zoneTypes.Length; typeIndex++)
        {
            var definition = QuerryDB.QueryDefinitions<WorkZoneDefinition>(DataType.WorkZone, zoneTypes[typeIndex]).FirstOrDefault();
            if (definition == null)
                continue;

            var column = typeIndex % columns;
            var row = typeIndex / columns;
            columnWidths[column] = Mathf.Max(columnWidths[column], definition.BaseSize.x);
            rowHeights[row] = Mathf.Max(rowHeights[row], definition.BaseSize.y);
        }

        for (var typeIndex = 0; typeIndex < zoneTypes.Length; typeIndex++)
        {
            var definition = QuerryDB.QueryDefinitions<WorkZoneDefinition>(DataType.WorkZone, zoneTypes[typeIndex]).FirstOrDefault();
            if (definition == null)
                continue;

            var column = typeIndex % columns;
            var row = typeIndex / columns;

            var position = new int2(start.x, start.y);
            for (var index = 0; index < column; index++)
                position.x += columnWidths[index] + separation;

            for (var index = 0; index < row; index++)
                position.y += rowHeights[index] + separation;

            CreateTestZone(em, board, zoneTypes[typeIndex], position);
        }
    }

    private static void CreateTestZone(EntityManager em, Entity board, string zoneId, int2 position)
    {
        var zone = WorkZoneManager.Create(em, zoneId);
        if (zone == Entity.Null)
            return;

        BoardManager.AddOn(em, zone, board, position, 0, true);

        var worker = PawnManager.Create(em);
        BoardManager.AddOn(em, worker, board, position + new int2(0, 1), 0, true);

        var checkroom = BuildingManager.Create(em, "checkroom");
        BoardManager.AddOn(em, checkroom, board, position + new int2(1, 0), 0, true);
        WorkZoneManager.AddOn(em, zone, checkroom);
        WorkerManager.AssignWorker(em, checkroom, worker);
        BuildingManager.FinishConstruction(em, checkroom);

        var inventory = BuildingManager.Create(em, "inventory");
        BoardManager.AddOn(em, inventory, board, position + new int2(2, 0), 0, true);
        WorkZoneManager.AddOn(em, zone, inventory);
        BuildingManager.FinishConstruction(em, inventory);

        if (zoneId != "quarry")
            return;

        var mine = BuildingManager.Create(em, "mine");
        BoardManager.AddOn(em, mine, board, position + new int2(3, 0), 0, true);
        WorkZoneManager.AddOn(em, zone, mine);
        BuildingManager.FinishConstruction(em, mine);
    }

    public static void CreatePerformanceTestGame(EntityManager entityManager)
    {
        var newGame = entityManager.CreateEntity();
        entityManager.AddComponentData(newGame, new NewGameRequest
        {
            BoardSize = new int2(192, 192),
            Seed = 5,
            IsPerformanceTest = true
        });
    }

    private static void CreateDefaultContent(EntityManager em, Entity board)
    {
        // Camp : zone + checkroom + inventory, 1 pawn affecté.
        var campPawn = PawnManager.Create(em);
        BoardManager.AddOn(em, campPawn, board, new int2(2, 1), 0, true);
        CreateCamp(em, board, new int2(2, 2), campPawn);

        // Workshop : zone + checkroom + inventory + grindstone + kitchencounter + furnace, 1 pawn affecté.

        CreateWorkshop(em, board, new int2(8, 2));
     
    
        // Farm : zone + checkroom + inventory + 4 bâtiments farm, 1 pawn affecté.
        var farmPawn = PawnManager.Create(em);
        BoardManager.AddOn(em, farmPawn, board, new int2(2, 6), 0, true);
        CreateFarm(em, board, new int2(2, 7), farmPawn);

        // Un pawn libre, sans affectation.
        var freePawn = PawnManager.Create(em);
        BoardManager.AddOn(em, freePawn, board, new int2(12, 8), 0, true);

        // MarketPlace : zone + checkroom + inventory + market, 1 pawn affecté.
        CreateMarketplace(em, board, new int2(14, 2));
    }

    private static void CreateMarketplace(EntityManager em, Entity board, int2 position)
    {
        var zone = WorkZoneManager.Create(em, "marketplace");
        em.SetComponentData(zone, new Size { Value = new int2(8, 8) });
        BoardManager.AddOn(em, zone, board, position, 0, true);

        var worker = PawnManager.Create(em);
        BoardManager.AddOn(em, worker, board, position + new int2(0, 2), 0, true);

        var checkroom = BuildingManager.Create(em, "checkroom");
        BoardManager.AddOn(em, checkroom, board, position, 0, true);
        WorkZoneManager.AddOn(em, zone, checkroom);
        WorkerManager.AssignWorker(em, checkroom, worker);
        BuildingManager.FinishConstruction(em, checkroom);

        var inventory = BuildingManager.Create(em, "inventory");
        BoardManager.AddOn(em, inventory, board, position + new int2(1, 0), 0, true);
        WorkZoneManager.AddOn(em, zone, inventory);
        BuildingManager.FinishConstruction(em, inventory);

        var market = BuildingManager.Create(em, "market");
        BoardManager.AddOn(em, market, board, position + new int2(3, 0), 0, true);
        WorkZoneManager.AddOn(em, zone, market);
        BuildingManager.FinishConstruction(em, market);

        // Ordre de vente par défaut : 5 blés (SELL).
        MarketManager.SetMarket(em, market, MarketType.SELL, new Items("wheat", 5));

        // Second marché pour acheter du pain (BUY).
        var buyMarket = BuildingManager.Create(em, "market");
        BoardManager.AddOn(em, buyMarket, board, position + new int2(3, 2), 0, true);
        WorkZoneManager.AddOn(em, zone, buyMarket);
        BuildingManager.FinishConstruction(em, buyMarket);

        MarketManager.SetMarket(em, buyMarket, MarketType.BUY, new Items("bread", 1));
    }

    private static void CreatePerformanceTestContent(EntityManager em, Entity board)
    {
        // 16 x 16 fermes : 256 zones, 256 pions et 1 536 bâtiments sur une carte 192 x 192.
        const int farmCountPerAxis = 16;
        const int farmSpacing = 10;
        const int farmStart = 12;

        var campPawn = PawnManager.Create(em);
        BoardManager.AddOn(em, campPawn, board, new int2(0, 0), 0, true);
        CreateCamp(em, board, new int2(4, 4), campPawn);

        for (var x = 0; x < farmCountPerAxis; x++)
        {
            for (var y = 0; y < farmCountPerAxis; y++)
            {
                var position = new int2(farmStart + x * farmSpacing, farmStart + y * farmSpacing);
                var pawn = PawnManager.Create(em);
                BoardManager.AddOn(em, pawn, board, position + new int2(2, 7), 0, true);
                CreateFarm(em, board, position, pawn);
            }
        }

        // 8 x 2 ateliers (zone workshop 5x5 chacun, 1 pion par atelier).
        const int workshopCountX = 8;
        const int workshopCountY = 2;
        const int workshopStartX = 12;
        const int workshopStartY = 176;
        const int workshopSpacing = 10;

        for (var x = 0; x < workshopCountX; x++)
        {
            for (var y = 0; y < workshopCountY; y++)
            {
                var position = new int2(workshopStartX + x * workshopSpacing, workshopStartY + y * workshopSpacing);
      
                CreateWorkshop(em, board, position);
            }
        }
    }

    private static void CreateFarm(EntityManager em, Entity board, int2 position, Entity worker)
    {
        var zone = WorkZoneManager.Create(em, "farm");
        em.SetComponentData(zone, new Size { Value = new int2(8, 8) });
        BoardManager.AddOn(em, zone, board, position, 0, true);

        var checkroom = BuildingManager.Create(em, "checkroom");
        BoardManager.AddOn(em, checkroom, board, position + new int2(2, 0), 0, true);
        WorkZoneManager.AddOn(em, zone, checkroom);
        WorkerManager.AssignWorker(em, checkroom, worker);
        BuildingManager.FinishConstruction(em, checkroom);

        var inventory = BuildingManager.Create(em, "inventory");
        BoardManager.AddOn(em, inventory, board, position, 0, true);
        WorkZoneManager.AddOn(em, zone, inventory);
        BuildingManager.FinishConstruction(em, inventory);

        CreateFarmBuilding(em, board, zone, position + new int2(4, 0));
      /*  CreateFarmBuilding(em, board, zone, position + new int2(0, 3));
        CreateFarmBuilding(em, board, zone, position + new int2(3, 3));
        CreateFarmBuilding(em, board, zone, position + new int2(6, 3));*/
    }

    private static void CreateFarmBuilding(EntityManager em, Entity board, Entity zone, int2 position)
    {
        var farmBuilding = BuildingManager.Create(em, "farm");
        BoardManager.AddOn(em, farmBuilding, board, position, 0, true);
        WorkZoneManager.AddOn(em, zone, farmBuilding);
        BuildingManager.FinishConstruction(em, farmBuilding);
    }

    private static void CreateCamp(EntityManager em, Entity board, int2 position, Entity worker)
    {
        var zone = WorkZoneManager.Create(em, "camp");
        BoardManager.AddOn(em, zone, board, position, 0, true);

        var checkroom = BuildingManager.Create(em, "checkroom");
        BoardManager.AddOn(em, checkroom, board, position, 0, true);
        WorkZoneManager.AddOn(em, zone, checkroom);
        WorkerManager.AssignWorker(em, checkroom, worker);
        BuildingManager.FinishConstruction(em, checkroom);

        var inventory = BuildingManager.Create(em, "inventory");
        BoardManager.AddOn(em, inventory, board, position + new int2(1, 0), 0, true);
        WorkZoneManager.AddOn(em, zone, inventory);
        BuildingManager.FinishConstruction(em, inventory);
        var inventoryE = em.GetComponentData<InventoryLink>(inventory).Inventory;
        //InventoryManager.AddItem(em,inventoryE,"bread",20);
        //InventoryManager.AddItem(em,inventoryE,"wheat",1);
    }

    private static void CreateWorkshop(EntityManager em, Entity board, int2 position)
    {
        var zone = WorkZoneManager.Create(em, "workshop");
        BoardManager.AddOn(em, zone, board, position, 0, true);

        var worker1 = PawnManager.Create(em);
        BoardManager.AddOn(em, worker1, board, position + new int2(0, 2), 0, true);
          var worker2 = PawnManager.Create(em);
        BoardManager.AddOn(em, worker2, board, position + new int2(0, 3), 0, true);

        var checkroom = BuildingManager.Create(em, "checkroom");
        BoardManager.AddOn(em, checkroom, board, position + new int2(0, 0), 0, true);
        WorkZoneManager.AddOn(em, zone, checkroom);
        WorkerManager.AssignWorker(em, checkroom, worker1);
        WorkerManager.AssignWorker(em, checkroom, worker2);
        BuildingManager.FinishConstruction(em, checkroom);

        var inventory = BuildingManager.Create(em, "inventory");
        BoardManager.AddOn(em, inventory, board, position + new int2(0, 4), 0, true);
        WorkZoneManager.AddOn(em, zone, inventory);
        BuildingManager.FinishConstruction(em, inventory);
        var inventoryE = em.GetComponentData<InventoryLink>(inventory).Inventory;
        InventoryManager.AddItem(em,inventoryE,new Items("dough",20));

        var grindstone = BuildingManager.Create(em, "grindstone");
        BoardManager.AddOn(em, grindstone, board, position + new int2(2, 0), 0, true);
        WorkZoneManager.AddOn(em, zone, grindstone);
        BuildingManager.FinishConstruction(em, grindstone);

        var kitchencounter = BuildingManager.Create(em, "kitchencounter");
        BoardManager.AddOn(em, kitchencounter, board, position + new int2(3, 0), 0, true);
        WorkZoneManager.AddOn(em, zone, kitchencounter);
        BuildingManager.FinishConstruction(em, kitchencounter);

        var furnace = BuildingManager.Create(em, "furnace");
        BoardManager.AddOn(em, furnace, board, position + new int2(4, 0), 0, true);
        WorkZoneManager.AddOn(em, zone, furnace);
        BuildingManager.FinishConstruction(em, furnace);
    }
}

public struct NewGameRequest :  IComponentData
{
    public int2 BoardSize;
    public uint Seed;
    public bool IsPerformanceTest;
}
