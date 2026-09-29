using Unity.Entities;
using Unity.Mathematics;

public static class BoardManager
{
    public static Entity Create(EntityManager em, string id, int2 size)
    {
        if (em == null || string.IsNullOrEmpty(id))
            return Entity.Null;

        var entity = em.CreateEntity();
        var number = Database.GetNextNumber(DataType.Board, id);


        em.AddComponentData(entity, new BoardSize
        {
            GridSize = size,
            CellSize = 1f,
            Origin = new float3(0f, 0f, 0f)
        });

        var cells = em.AddBuffer<Cell>(entity);
        for (int x = 0; x < size.x; x++)
        {
            for (int y = 0; y < size.y; y++)
            {
                cells.Add(new Cell());
            }
        }

        em.AddComponent<BoardGraphicUpdate>(entity);
        
        em.AddBuffer<BoardWorkZone>(entity);

        Database.AddInstance(DataType.Board, id, number, entity,em);
        return entity;
    }

    public static Entity Create(EntityManager em, string id)
    {
        return Create(em, id, new int2(1, 1));
    }

    
    public static void AddOn(EntityManager em, Entity entity, Entity board, int2 position, int rotation, bool updateGraphic)
    {
       var id = em.GetComponentData<ID>(entity);

        if (id.DataType == DataType.WorkZone)
        {
            if (em.HasComponent<ZoneRubble>(entity))
                em.RemoveComponent<ZoneRubble>(entity);
        }
        else if (id.DataType == DataType.Building)
        {
            if (em.HasComponent<BuildingRubble>(entity))
                em.RemoveComponent<BuildingRubble>(entity);
        }

        var onBoard = new OnBoard
        {
            Board = board,
            Position = position,
            Rotation = rotation
        };

        if (em.HasComponent<OnBoard>(entity))
            em.SetComponentData(entity, onBoard);
        else
            em.AddComponentData(entity, onBoard);

        var size = em.HasComponent<Size>(entity) ? em.GetComponentData<Size>(entity).Value : new int2(1, 1);
        var boardSize = em.GetComponentData<BoardSize>(board);
        var cells = em.GetBuffer<Cell>(board);

        if (em.HasBuffer<CellsOccupy>(entity))
        {
            var occupying = em.GetBuffer<CellsOccupy>(entity);
            foreach (var cellOccupy in occupying)
            {
                var cell = cells[cellOccupy.IndexCell];
                if(id.DataType == DataType.WorkZone)
                {
                    cell.WorkZone = Entity.Null;
                }
                else
                {
                     cell.ObjectOn = Entity.Null;
                }
               
                cells[cellOccupy.IndexCell] = cell;
            }
            occupying.Clear();
        }
        else
        {
            em.AddBuffer<CellsOccupy>(entity);
        }
        cells = em.GetBuffer<Cell>(board);
        var occupyBuffer = em.GetBuffer<CellsOccupy>(entity);
        for (int x = 0; x < size.x; x++)
        {
            for (int y = 0; y < size.y; y++)
            {
                var index = BoardUtility.PosToIndex(position + new int2(x, y), boardSize.GridSize);
                var cell = cells[index];
                 if(id.DataType == DataType.WorkZone)
                {
                    cell.WorkZone = entity;
                }
                else
                {
                     cell.ObjectOn = entity;
                }
                cells[index] = cell;
                occupyBuffer.Add(new CellsOccupy { IndexCell = index });
            }
        }

        if (updateGraphic)
            em.AddComponent<GraphicUpdate>(entity);
    }

    /// <summary>
    /// Retire une entité (bâtiment ou zone) du plateau : cellule(s) libérée(s),
    /// composants OnBoard/CellsOccupy retirés, graphique détruite + GraphicLink retiré.
    /// </summary>
    public static void RemoveOn(EntityManager em, Entity entity)
    {
        if (em == null || entity == Entity.Null || !em.Exists(entity))
            return;

        if (em.HasComponent<OnBoard>(entity))
        {
            var onBoard = em.GetComponentData<OnBoard>(entity);
            var isZone = false;
            if (em.HasComponent<ID>(entity))
                isZone = em.GetComponentData<ID>(entity).DataType == DataType.WorkZone;

            if (onBoard.Board != Entity.Null && em.Exists(onBoard.Board) && em.HasBuffer<Cell>(onBoard.Board))
            {
                var cells = em.GetBuffer<Cell>(onBoard.Board);
                if (em.HasBuffer<CellsOccupy>(entity))
                {
                    foreach (var occupy in em.GetBuffer<CellsOccupy>(entity))
                    {
                        if (occupy.IndexCell < 0 || occupy.IndexCell >= cells.Length)
                            continue;
                        var cell = cells[occupy.IndexCell];
                        if (isZone)
                            cell.WorkZone = Entity.Null;
                        else
                            cell.ObjectOn = Entity.Null;
                        cells[occupy.IndexCell] = cell;
                    }
                }
            }

            em.RemoveComponent<OnBoard>(entity);
            if (em.HasBuffer<CellsOccupy>(entity))
                em.RemoveComponent<CellsOccupy>(entity);
        }

        TaskManager.FinishAllLinked(em, entity);

        if (em.HasComponent<ID>(entity) && em.GetComponentData<ID>(entity).DataType == DataType.Building)
        {
            if (!em.HasComponent<BuildingRubble>(entity))
                em.AddComponentData(entity, new BuildingRubble());
        }

        EntityGraphicsUtility.DestroyGraphicFromParent(em, entity);
        if (em.HasComponent<GraphicLink>(entity))
            em.RemoveComponent<GraphicLink>(entity);
    }
}


public struct OnBoard : IComponentData //ENTITY placer sur le board
{
    public Entity Board;
    public int2 Position;            // Coordonnées grille (x,z)
    public int Rotation;            // 0,90,180,270 ou enum
}
public struct CellsOccupy : IBufferElementData //SUr enity qui est sur le board LIEN entity => gridBoard
{
    public int IndexCell;
}
public struct BoardSize :  IComponentData
{
    public int2 GridSize;      // ex: (128, 128)
    public float CellSize;
    public float3 Origin;
}

public struct Cell : IBufferElementData
{
    public Entity ObjectOn;     // Entity.Null si la cellule est vide
    public Entity WorkZone;
    public Entity PawnEntity;
}

public struct BoardWorkZone : IBufferElementData
{
    public Entity WorkZone;
}