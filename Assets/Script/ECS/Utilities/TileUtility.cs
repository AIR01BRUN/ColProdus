using Unity.Entities;
using Unity.Mathematics;

public static class TileUtility
{

    public static int2[] GetNeighbourTile(int2 currentTile)
    {
        // Retourne les 8 voisins autour d'une tuile : 4 orthogonales + 4 diagonales.
        // En BFS, on explorera l'ordre défini ici (droite, gauche, haut, bas, diagonales) ;
        // l'ordre peut affecter les chemins de même coût, mais pas l'optimalité en nombre d'étapes.
        int2[] neighbors = new int2[8];
        neighbors[0] = new int2(currentTile.x + 1, currentTile.y); // droite
        neighbors[1] = new int2(currentTile.x - 1, currentTile.y); // gauche
        neighbors[2] = new int2(currentTile.x, currentTile.y + 1); // haut    
        neighbors[3] = new int2(currentTile.x, currentTile.y - 1); // bas
        neighbors[4] = new int2(currentTile.x + 1, currentTile.y + 1); // diagonale haut droite
        neighbors[5] = new int2(currentTile.x - 1, currentTile.y + 1); // diagonale haut gauche
        neighbors[6] = new int2(currentTile.x + 1, currentTile.y - 1); // diagonale bas droite
        neighbors[7] = new int2(currentTile.x - 1, currentTile.y - 1); // diagonale bas gauche

        return neighbors; 
    }

    public static int2[] GetTilesInRange(int2 currentTile, int2 range)
{
        // Retourne toutes les tuiles dans une zone rectangulaire centrée sur "currentTile"
        // currentTile est le centre de la zone, pas un coin
        // Par exemple, pour un bâtiment de taille (2,3) centré sur (5,5), cela retournera les 6 tuiles autour du centre
        int totalTiles = range.x * range.y;
        int2[] tiles = new int2[totalTiles];
        int index = 0;

        // Calculer l'offset pour centrer la zone sur currentTile
        int offsetX = -range.x / 2;
        int offsetY = -range.y / 2;

        for (int x = 0; x < range.x; x++)
        {
            for (int y = 0; y < range.y; y++)
            {
                tiles[index] = new int2(currentTile.x + offsetX + x, currentTile.y + offsetY + y);
                index++;
            }
        }
    return tiles;
}

 
    

    public static bool IsInsideBoard(int2 tile, int2 size) => tile.x >= 0 && tile.y >= 0 && tile.x < size.x && tile.y < size.y;
    public static int2 PositionToTile(float3 position) => new int2((int)math.round(position.x), (int)math.round(position.z)); 
    public static float3 TileToPosition(int2 tile) => new float3(tile.x, 0f, tile.y);   

    public static bool IsOnTile(float3 position, int2 tile) => math.distance(position, TileToPosition(tile)) < 0.1f;
    
    public static  float GetDistanceByTile(int2 a, int2 b) => math.distance(a, b);

}
