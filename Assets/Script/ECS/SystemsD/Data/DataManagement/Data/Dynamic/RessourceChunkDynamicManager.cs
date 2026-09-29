using System.Linq;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

public static class RessourceChunkManager
{
    public static void AddOn(EntityManager em,Entity board,string idRessource,RessourceChunkCreationData data, bool updateGraphic)
    {
    
        var chunkSize = data.ChunkSize;
        var chunkCount = data.ChunkCount;
        var density = data.Density;
        var seed = data.Seed;

        var definition = QuerryDB.QueryDefinitions< RessourceDefinition>(DataType.Ressource, idRessource).FirstOrDefault() ;

        var boardSize = em.GetComponentData<BoardSize>(board).GridSize;
        var resourceSize = definition.Size;

        var maxOrigin = boardSize - resourceSize;
        var random = Unity.Mathematics.Random.CreateFromIndex(math.max(seed, 1u));
        using var reservedCells = new NativeHashSet<int>(boardSize.x * boardSize.y, Allocator.Temp);

        
        for (int chunkIndex = 0; chunkIndex < chunkCount; chunkIndex++)
        {
            var chunkOrigin = new int2(random.NextInt(0, math.max(boardSize.x - chunkSize.x + 1, 1)),random.NextInt(0, math.max(boardSize.y - chunkSize.y + 1, 1)));
            var canReserve = true;
            for (int y = 0; y < chunkSize.y; y++)
            for (int x = 0; x < chunkSize.x; x++)
            {
                if (random.NextFloat() > density) continue;
                var position = chunkOrigin + new int2(x, y);
                if (!BoardUtility.CellsIsFree(em, board, position, resourceSize)) continue;
                 for (int resourceY = 0; resourceY < resourceSize.y; resourceY++)
                        {
                            for (int resourceX = 0; resourceX < resourceSize.x; resourceX++)
                            {
                                var cellPosition = position + new int2(resourceX, resourceY);
                                if (reservedCells.Contains(BoardUtility.PosToIndex(cellPosition, boardSize)))
                                {
                                    canReserve = false;
                                    break;
                                }
                            }

                            if (!canReserve)
                                break;
                        }

                        if (!canReserve)
                            continue;

                        for (int resourceY = 0; resourceY < resourceSize.y; resourceY++)
                        {
                            for (int resourceX = 0; resourceX < resourceSize.x; resourceX++)
                            {
                                var cellPosition = position + new int2(resourceX, resourceY);
                                reservedCells.Add(BoardUtility.PosToIndex(cellPosition, boardSize));
                            }
                        }
                        var ressource = RessourceManager.Create(em, idRessource);
                        BoardManager.AddOn(em,ressource, board, position, 0, updateGraphic);
                        
                    
            }   

               
        }

    }

}
public class RessourceChunkCreationData
{
    public int ChunkCount;
    public int2 ChunkSize;
    public float Density;
    public uint Seed;
  
}
