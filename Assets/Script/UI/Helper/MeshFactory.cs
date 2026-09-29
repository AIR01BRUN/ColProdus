using Unity.Mathematics;
using UnityEngine;

public static class MeshFactory
{
    public static Mesh CreateLine(float width = 0.04f)
    {
        float halfWidth = width * 0.5f;
        Mesh mesh = new Mesh
        {
            name = "MovementLine"
        };

        mesh.vertices = new Vector3[]
        {
            new Vector3(-0.5f, 0f, -halfWidth),
            new Vector3(0.5f, 0f, -halfWidth),
            new Vector3(0.5f, 0f, halfWidth),
            new Vector3(-0.5f, 0f, halfWidth)
        };
        mesh.triangles = new[] { 0, 2, 1, 0, 3, 2 };
        mesh.uv = new Vector2[]
        {
            new Vector2(0f, 0f),
            new Vector2(1f, 0f),
            new Vector2(1f, 1f),
            new Vector2(0f, 1f)
        };

        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }
    
     public static Mesh CreateTile(float size = 1f, bool centered = false)
    {
        Mesh mesh = new Mesh();
        mesh.name = "Tile_1x1";

        float half = size * 0.5f;

        Vector3[] vertices = centered ? new Vector3[4]
        {
            new Vector3(-half, 0, -half),
            new Vector3( half, 0, -half),
            new Vector3( half, 0,  half),
            new Vector3(-half, 0,  half)
        }
        : new Vector3[4]
        {
            new Vector3(0, 0, 0),
            new Vector3(size, 0, 0),
            new Vector3(size, 0, size),
            new Vector3(0, 0, size)
        };

        int[] triangles = new int[] { 0, 2, 1, 0, 3, 2 };

        Vector2[] uv = new Vector2[] 
        { 
            new Vector2(0, 0), 
            new Vector2(1, 0), 
            new Vector2(1, 1), 
            new Vector2(0, 1) 
        };

        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.uv = uv;

        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        mesh.Optimize();

        return mesh;
    }

    public static Mesh CreateBoard(int width, int depth, float tileSize = 1f)
    {
        if (width <= 0 || depth <= 0)
        {
            return new Mesh();
        }

        int vertWidth = width + 1;
        int vertDepth = depth + 1;

        Vector3[] vertices = new Vector3[vertWidth * vertDepth];
        Vector2[] uv = new Vector2[vertices.Length];
        int[] triangles = new int[width * depth * 6];

        int vertexIndex = 0;
        for (int z = 0; z < vertDepth; z++)
        {
            for (int x = 0; x < vertWidth; x++)
            {
                vertices[vertexIndex] = new Vector3(x * tileSize, 0f, z * tileSize);
                uv[vertexIndex] = new Vector2((float)x / width, (float)z / depth);
                vertexIndex++;
            }
        }

        int triangleIndex = 0;
        for (int z = 0; z < depth; z++)
        {
            for (int x = 0; x < width; x++)
            {
                int i = z * vertWidth + x;

                triangles[triangleIndex + 0] = i;
                triangles[triangleIndex + 1] = i + vertWidth;
                triangles[triangleIndex + 2] = i + 1;

                triangles[triangleIndex + 3] = i + 1;
                triangles[triangleIndex + 4] = i + vertWidth;
                triangles[triangleIndex + 5] = i + vertWidth + 1;

                triangleIndex += 6;
            }
        }

        Mesh plateauMesh = new Mesh();
        plateauMesh.name = $"Board_{width}x{depth}";
        plateauMesh.vertices = vertices;
        plateauMesh.uv = uv;
        plateauMesh.triangles = triangles;
        plateauMesh.RecalculateNormals();
        plateauMesh.RecalculateBounds();
        plateauMesh.Optimize();

        return plateauMesh;
    }

    public static Mesh CreateSquareMesh(int2 size, float scale = 1f)
    {
        if (size.x <= 0 || size.y <= 0)
        {
            return new Mesh();
        }

        int width = size.x;
        int height = size.y;

        // Vertices
        Vector3[] vertices = new Vector3[(width + 1) * (height + 1)];
        Vector2[] uv = new Vector2[vertices.Length];
        int[] triangles = new int[width * height * 6];

        int vertexIndex = 0;
        int triangleIndex = 0;

        float halfWidth = width * 0.5f * scale;
        float halfHeight = height * 0.5f * scale;

        for (int y = 0; y <= height; y++)
        {
            for (int x = 0; x <= width; x++)
            {
                // Position centrée
                float posX = (x * scale) - halfWidth;
                float posY = (y * scale) - halfHeight;

                vertices[vertexIndex] = new Vector3(posX, 0, posY);
                uv[vertexIndex] = new Vector2((float)x / width, (float)y / height);

                vertexIndex++;
            }
        }

        // Triangles
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int i = y * (width + 1) + x;

                triangles[triangleIndex + 0] = i;
                triangles[triangleIndex + 1] = i + (width + 1);
                triangles[triangleIndex + 2] = i + 1;

                triangles[triangleIndex + 3] = i + 1;
                triangles[triangleIndex + 4] = i + (width + 1);
                triangles[triangleIndex + 5] = i + (width + 1) + 1;

                triangleIndex += 6;
            }
        }

        Mesh mesh = new Mesh
        {
            name = $"SquareMesh_{width}x{height}"
        };

        mesh.vertices = vertices;
        mesh.uv = uv;
        mesh.triangles = triangles;

        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        mesh.Optimize();

        return mesh;
    }


}
