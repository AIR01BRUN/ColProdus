using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

public static class BoardUtility
{
    public static int PosToIndex(EntityManager em , Entity board , int2 position)
    {
        var size = em.GetComponentData<BoardSize>(board);
        return PosToIndex( position,size.GridSize);
    }
     public static int PosToIndex(int2 position,int2 sizeBoard)
    {
        return position.y * sizeBoard.x + position.x;
    }
    public static bool TryGetCell(EntityManager em , Entity board , int2 position, out Cell cell)
    {
        var index =  PosToIndex(em ,board ,position);
        
        var cells = em.GetBuffer<Cell>(board);
      
        if(index < cells.Length &&  index  >=  0)
        {
            cell = cells[index];
            return true;
        }
        cell = default;
        return false;
        

    }
    public static bool CellIsFree(EntityManager em , Entity board , int2 position)
    {
        if(!TryGetCell(em ,board ,position, out var cell)) return false;

        var size = em.GetComponentData<BoardSize>(board);
        if(cell.ObjectOn != Entity.Null || size.GridSize.x < position.x  ||  size.GridSize.y < position.y  || 0 > position.x || 0 > position.y  ) return false;
        return true;
    }
    public static bool CellsIsFree(EntityManager em , Entity board , NativeList<int2> positions)
    {
        foreach(var pos in positions)
        {
            if(!CellIsFree(em,board,pos)) return false;
        }
        return true;
    }
    public static bool CellsIsFree(EntityManager em , Entity board ,int2 position, int2 size)
    {
        for(int x = 0 ; x < size.x ; x++)
        for(int y = 0 ; y < size.y; y++)
        {
            if(!CellIsFree(em,board,position+new int2(x,y))) return false;
        }
        return true;
    }

    /// <summary>
    /// Position aléatoire libre dans un cercle de rayon <paramref name="radius"/> centré
    /// sur <paramref name="center"/>. Retourne (-1,-1) si aucune position libre n'est
    /// trouvée après plusieurs essais.
    /// </summary>
    public static int2 GetRandomFreePosInCircle(EntityManager em, Entity board, int2 center, int radius)
    {
        const int maxAttempts = 64;
        for (var attempt = 0; attempt < maxAttempts; attempt++)
        {
            var distance = radius * math.sqrt(UnityEngine.Random.value);
            var angle = UnityEngine.Random.value * math.PI * 2;
            var position = center + new int2(
                (int)math.round(distance * math.cos(angle)),
                (int)math.round(distance * math.sin(angle))
            );

            if (CellIsFree(em, board, position))
                return position;
        }
        return new int2(-1, -1);
    }
    public static int2 PosCenter(int2 pos , int2 size)
    {
        return pos + size / 2;
    }
   
    public static Vector3 PosToWorld(int2 pos, float heightOffset = 0.01f , float offSet = 0f)
    {
        float3 localPos = new float3(
            pos.x + offSet ,
            heightOffset,
            pos.y + offSet
        );

        return  localPos;
    }
    public static int2 WorldToPos(Vector3 worldPosition)
    {
        return new int2((int)worldPosition.x,  (int)worldPosition.z);
    }
    public static Quaternion GetRotationQuaternion(RotationState rotation)
    {
        return rotation switch
        {
            RotationState.North => Quaternion.Euler(90, 0, 0),
            RotationState.East => Quaternion.Euler(90, 0, 90),
            RotationState.South => Quaternion.Euler(90, 0, 180),
            RotationState.West => Quaternion.Euler(90, 0, 270),
            _ => Quaternion.Euler(90, 0, 0)
        };
    }
     public static NativeList<int2> PositionTarget(int2 pos, int2 size, Allocator allocator = Allocator.Temp)
    {
        var targets = new NativeList<int2>(size.x * size.y, allocator);

        // On récupère le coin bas-gauche grâce à la nouvelle fonction
      

        for (int y = 0; y < size.y; y++)
        {
            for (int x = 0; x < size.x; x++)
            {
                targets.Add(pos + new int2(x, y));
            }
        }

        return targets;
    }
    public static NativeList<int2> GetNeigthbour(int2 position, int2 size, Allocator allocator = Allocator.Temp)
    {
        // Estimation de la capacité : 2*(largeur + hauteur) + 4 coins
        int capacity = 2 * (size.x + size.y) + 4;
        var targets = new NativeList<int2>(capacity, allocator);

        int minX = position.x;
        int maxX = position.x + size.x - 1;
        int minY = position.y;
        int maxY = position.y + size.y - 1;

        // === Colonnes gauche et droite (hauteur + 2 pour inclure les coins) ===
        for (int y = minY - 1; y <= maxY + 1; y++)
        {
            targets.Add(new int2(minX - 1, y)); // gauche
            targets.Add(new int2(maxX + 1, y)); // droite
        }

        // === Lignes bas et haut (sans les coins, déjà ajoutés) ===
        for (int x = minX; x <= maxX; x++)
        {
            targets.Add(new int2(x, minY - 1)); // bas
            targets.Add(new int2(x, maxY + 1)); // haut
        }

        return targets;
    }
    public static int2 GetFirstNeigthbour(EntityManager em , Entity board ,int2 position, int2 size, Allocator allocator = Allocator.Temp)
    {
        foreach(var neigthbour in GetNeigthbour(position, size,allocator))
        {
            if (CellIsFree(em, board, neigthbour))
            {
                return neigthbour;
            }
        }
        return new int2(-1,-1);
    
    }

    /// <summary>
    /// Voisin libre du bâtiment le plus proche de fromPosition (distance manhattan),
    /// pour minimiser le déplacement du pawn.
    /// </summary>
    public static int2 GetNearestFreeNeigthbour(EntityManager em , Entity board ,int2 position, int2 size, int2 fromPosition, Allocator allocator = Allocator.Temp)
    {
        var neigthbours = GetNeigthbour(position, size, allocator);
        var result = new int2(-1, -1);
        var bestDistance = int.MaxValue;

        for (var i = 0; i < neigthbours.Length; i++)
        {
            var neigthbour = neigthbours[i];
            if (!CellIsFree(em, board, neigthbour))
                continue;

            var distance = math.abs(neigthbour.x - fromPosition.x) + math.abs(neigthbour.y - fromPosition.y);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                result = neigthbour;
            }
        }

        neigthbours.Dispose();
        return result;
    }

    /// <summary>
    /// Toutes les positions voisines autour du bâtiment qui sont valides
    /// (libres : ni pawn ni autre bâtiment dessus).
    /// </summary>
    public static NativeList<int2> GetValidNeigthbours(EntityManager em , Entity board ,int2 position, int2 size, Allocator allocator = Allocator.Temp)
    {
        var neigthbours = GetNeigthbour(position, size, allocator);
        var valid = new NativeList<int2>(neigthbours.Length, allocator);

        for (var i = 0; i < neigthbours.Length; i++)
        {
            if (CellIsFree(em, board, neigthbours[i]))
                valid.Add(neigthbours[i]);
        }

        neigthbours.Dispose();
        return valid;
    }

    /// <summary>
    /// Position valide autour du bâtiment la plus proche de fromPosition ET
    /// réellement atteignable par le pawn (un chemin existe).
    /// Retourne (-1,-1) si aucune position n'est atteignable.
    /// </summary>
    public static int2 GetNearestReachableNeigthbour(EntityManager em , Entity board ,int2 position, int2 size, int2 fromPosition, Allocator allocator = Allocator.Temp)
    {
        var valid = GetValidNeigthbours(em, board, position, size, allocator);
        var result = new int2(-1, -1);
        var bestDistance = int.MaxValue;

        for (var i = 0; i < valid.Length; i++)
        {
            var neigthbour = valid[i];
            var distance = math.abs(neigthbour.x - fromPosition.x) + math.abs(neigthbour.y - fromPosition.y);
            if (distance >= bestDistance)
                continue;

            if (!FindPath(em, board, fromPosition, neigthbour, out _))
                continue;

            bestDistance = distance;
            result = neigthbour;
        }

        valid.Dispose();
        return result;
    }

    public static bool FindPath(EntityManager em ,Entity board ,int2 start, int2 target, out List<int2> path)
    {
        path = new List<int2>();

        if (start.Equals(target))
        {
            path.Add(start);
            return true;
        }

        int2[] directions =
        {
            new int2( 1,  0), new int2(-1,  0), new int2( 0,  1), new int2( 0, -1),
            new int2( 1,  1), new int2( 1, -1), new int2(-1,  1), new int2(-1, -1)
        };

        var cameFrom = new Dictionary<int2, int2>();
        var frontier = new Queue<int2>();
        var visited = new HashSet<int2>();

        frontier.Enqueue(start);
        visited.Add(start);
        cameFrom[start] = start;

        bool found = false;

        while (frontier.Count > 0)
        {
            int2 current = frontier.Dequeue();

            if (current.Equals(target))
            {
                found = true;
                break;
            }

            foreach (var dir in directions)
            {
                int2 neighbour = current + dir;

                if (!CellIsFree(em,board,neighbour) || visited.Contains(neighbour)  )
                    continue;

                frontier.Enqueue(neighbour);
                visited.Add(neighbour);
                cameFrom[neighbour] = current;
            }
        }
         if (!found)
        {
            return false;
        }

        List<int2> tempPath = new List<int2>();
        int2 currentPos = target;

        while (true)
        {
            tempPath.Add(currentPos);

            if (currentPos.Equals(start))
                break;

            currentPos = cameFrom[currentPos];
        }

        path = new List<int2>(tempPath.Count);
        for (int i = tempPath.Count - 1; i >= 0; i--)
            path.Add(tempPath[i]);

        return true;
    }



    public enum RotationState
    {
        North,  // 0°
        East, // 90°
        South, // 180°
        West, // 270°
        
    }

}