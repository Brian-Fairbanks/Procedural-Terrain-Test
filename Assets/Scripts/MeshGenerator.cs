// WTF is going on with the normalization here?  I understand that you are 
// - https://www.youtube.com/watch?v=NpeYTcS7n-M&list=PLFt_AvWsXl0eBW2EiBtl_sxmDtSgZBxB3&index=12


using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MeshGenerator
{
    public static MeshData GenerateTerrainMesh(float[,] heightMap, float heightMultiplier, AnimationCurve _heightCurve, int levelOfDetail)
    {
        //each thread using the same animation curve causes problems...
        AnimationCurve heightCurve = new AnimationCurve(_heightCurve.keys);
        int meshSimplificationIncrement = (levelOfDetail == 0) ? 1 : levelOfDetail * 2;

        int borderedSize = heightMap.GetLength(0);
        int meshSize = borderedSize - 2*meshSimplificationIncrement;
        int meshSizeUnsimplified = borderedSize - 2;

        float topLeftX = (meshSizeUnsimplified - 1) / -2f;
        float topLeftZ = (meshSizeUnsimplified - 1) / 2f;

        int verticesPerLine = (meshSize - 1) / meshSimplificationIncrement + 1;

        MeshData meshData = new MeshData(verticesPerLine);
        int[,] vertexIndicesMap = new int[borderedSize,borderedSize];
        int meshVertexIndex = 0;
        int borderVertexIndex = -1;

        // set up using vertices that account for the one next outside of the chunk.
        for (int y = 0; y < borderedSize; y += meshSimplificationIncrement) {
            for (int x = 0; x < borderedSize; x += meshSimplificationIncrement) {
                bool isBorderVertex = y == 0 || y == borderedSize - 1 || x == 0 || x == borderedSize - 1;

                if (isBorderVertex) {
                    vertexIndicesMap[x, y] = borderVertexIndex;
                    borderVertexIndex--;
                }
                else {
                    vertexIndicesMap[x, y] = meshVertexIndex;
                    meshVertexIndex++;
                }
            }
        }

        //for each square in our mesh
        for (int y=0; y<borderedSize; y+=meshSimplificationIncrement)        {
            for (int x=0; x<borderedSize; x+=meshSimplificationIncrement)            {
                int vertexIndex = vertexIndicesMap[x, y];
                Vector2 percent = new Vector2((x - meshSimplificationIncrement) / (float)meshSize, (y - meshSimplificationIncrement) / (float)meshSize);

                float height = heightCurve.Evaluate(heightMap[x, y]) * heightMultiplier;
                Vector3 vertexPos = new Vector3(topLeftX + percent.x* meshSizeUnsimplified, height, topLeftZ - percent.y* meshSizeUnsimplified); // topLeftX + x so that we are centering the mesh

                meshData.AddVertex(vertexPos, percent, vertexIndex);

                // you must add 2 triangles
                if (x < borderedSize - 1 && y < borderedSize - 1)
                {
                    int a = vertexIndicesMap[x, y];
                    int b = vertexIndicesMap[x+meshSimplificationIncrement, y];
                    int c = vertexIndicesMap[x, y+ meshSimplificationIncrement];
                    int d = vertexIndicesMap[x+ meshSimplificationIncrement, y+ meshSimplificationIncrement];
                    meshData.AddTriangle(a,d,c);
                    meshData.AddTriangle(d,a,b);
                }

                vertexIndex++;
            }
        }

        meshData.BakeNormals();

        return meshData;
    }
}


public class MeshData
{
    Vector3[] vertices;
    int[] triangles;
    Vector2[] uvs;
    Vector3[] bakedNormals;

    Vector3[] borderVertices;
    int[] borderTriangles;

    int triangleIndex;
    int borderTriangleIndex;


    public MeshData(int verticesPerLine)
    {
        vertices = new Vector3[verticesPerLine * verticesPerLine];
        uvs = new Vector2[verticesPerLine*verticesPerLine];
        triangles = new int[(verticesPerLine - 1) * (verticesPerLine - 1) * 6];

        borderVertices = new Vector3[verticesPerLine *4 +4];
        borderTriangles = new int[24*verticesPerLine];
    }

    public void AddVertex(Vector3 vertexPosition, Vector2 uv, int vertexIndex) {
        if (vertexIndex < 0) {
            borderVertices[-vertexIndex - 1] = vertexPosition;
        }
        else {
            vertices[vertexIndex] = vertexPosition;
            uvs[vertexIndex] = uv;
        }
    }

    public void AddTriangle(int a, int b, int c)
    {
        if (a < 0 || b < 0 || c < 0) { // this is a border trinagle
            borderTriangles[borderTriangleIndex] = a;
            borderTriangles[borderTriangleIndex + 1] = b;
            borderTriangles[borderTriangleIndex + 2] = c;
            borderTriangleIndex += 3;
        }
        else {  // this is a regular triangle
            triangles[triangleIndex] = a;
            triangles[triangleIndex + 1] = b;
            triangles[triangleIndex + 2] = c;
            triangleIndex += 3;
        }
    }

    // triangles normals are calculated per chunk, and thus do not meet properly.  They do not shade correctly as related to their neighbors.  The following 2 functions should help correct this.
    Vector3[] CalculateNormals() {
        Vector3[] vertexNormals = new Vector3[vertices.Length];
        int trinaglecount = triangles.Length / 3;
        for (int i = 0; i < trinaglecount; i++) {
            int normalTriangleIndex = i * 3;
            int vertexIndexA = triangles[normalTriangleIndex];
            int vertexIndexB = triangles[normalTriangleIndex + 1];
            int vertexIndexC = triangles[normalTriangleIndex + 2];

            Vector3 trinagleNormal = SurfaceNormalFromIndecies(vertexIndexA, vertexIndexB, vertexIndexC);
            vertexNormals[vertexIndexA] += trinagleNormal;
            vertexNormals[vertexIndexB] += trinagleNormal;
            vertexNormals[vertexIndexC] += trinagleNormal;
        }

        int borderTrinaglecount = borderTriangles.Length / 3;
        for (int i = 0; i < borderTrinaglecount; i++) {
            int normalTriangleIndex = i * 3;
            int vertexIndexA = borderTriangles[normalTriangleIndex];
            int vertexIndexB = borderTriangles[normalTriangleIndex + 1];
            int vertexIndexC = borderTriangles[normalTriangleIndex + 2];

            Vector3 trinagleNormal = SurfaceNormalFromIndecies(vertexIndexA, vertexIndexB, vertexIndexC);
            if (vertexIndexA >= 0) {
                vertexNormals[vertexIndexA] += trinagleNormal;
            }
            if (vertexIndexB >= 0) {
                vertexNormals[vertexIndexB] += trinagleNormal;
            }
            if (vertexIndexC >= 0) {
                vertexNormals[vertexIndexC] += trinagleNormal;
            }
        }

        // normalize these values again
        for (int i=0; i<vertexNormals.Length; i++) {
            vertexNormals[i].Normalize();
        }

        return vertexNormals;
    }


    Vector3 SurfaceNormalFromIndecies(int indexA, int indexB, int indexC) {
        Vector3 pointA = (indexA < 0)?borderVertices[-indexA-1] : vertices[indexA];
        Vector3 pointB = (indexB < 0) ? borderVertices[-indexB - 1] : vertices[indexB];
        Vector3 pointC = (indexC < 0) ? borderVertices[-indexC - 1] : vertices[indexC];

        Vector3 sideAB = pointB - pointA;
        Vector3 sideAC = pointC - pointA;

        return Vector3.Cross(sideAB, sideAC).normalized;
    }

    public void BakeNormals() {
        bakedNormals = CalculateNormals();
    }

    public Mesh CreateMesh()
    {
        Mesh mesh = new Mesh();
        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.uv = uvs;
        mesh.RecalculateNormals(); // fix lighting issues
        //mesh.normals = CalculateNormals();
        mesh.normals = bakedNormals;
        return mesh;
    }

}