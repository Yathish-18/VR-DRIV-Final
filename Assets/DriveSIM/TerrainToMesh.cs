using UnityEngine;
using System.Collections.Generic;

public class TerrainToMesh : MonoBehaviour
{
    public Terrain terrain;

    public void ConvertTerrain()
    {
        TerrainData terrainData = terrain.terrainData;
        int width = terrainData.heightmapResolution;
        int height = terrainData.heightmapResolution;

        float[,] heights = terrainData.GetHeights(0, 0, width, height);
        Vector3 meshScale = terrainData.size;
        meshScale = new Vector3(meshScale.x / (width - 1), meshScale.y, meshScale.z / (height - 1));

        List<Vector3> vertices = new List<Vector3>();
        List<int> triangles = new List<int>();

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                float heightValue = heights[y, x];
                vertices.Add(new Vector3(x * meshScale.x, heightValue * meshScale.y, y * meshScale.z));
            }
        }

        for (int y = 0; y < height - 1; y++)
        {
            for (int x = 0; x < width - 1; x++)
            {
                int start = y * width + x;
                triangles.Add(start);
                triangles.Add(start + width);
                triangles.Add(start + 1);

                triangles.Add(start + 1);
                triangles.Add(start + width);
                triangles.Add(start + width + 1);
            }
        }

        Mesh mesh = new Mesh();
        mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        mesh.vertices = vertices.ToArray();
        mesh.triangles = triangles.ToArray();
        mesh.RecalculateNormals();

        GameObject meshObj = new GameObject("TerrainMesh");
        MeshFilter mf = meshObj.AddComponent<MeshFilter>();
        mf.mesh = mesh;
        meshObj.AddComponent<MeshRenderer>();
    }
}
