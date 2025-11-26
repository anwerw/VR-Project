using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(SkinnedMeshRenderer))]
public class BarycentricInjector : MonoBehaviour
{
    [Tooltip("Set true to log basic info during injection.")]
    public bool verbose = false;

    // Choose which UV channel to store barycentrics (uv2 recommended)
    public enum UVChannel { UV2, UV3, UV4 }
    public UVChannel targetChannel = UVChannel.UV2;

    // Optional: thickness control via material property
    public float lineThickness = 0.8f; // Adjust in material later

    private SkinnedMeshRenderer smr;
    private Mesh runtimeMesh;

    void Awake()
    {
        smr = GetComponent<SkinnedMeshRenderer>();
        if (!smr || !smr.sharedMesh)
        {
            Debug.LogError("BarycentricInjector: SkinnedMeshRenderer or mesh missing.");
            return;
        }

        // Clone original mesh to avoid modifying the asset
        runtimeMesh = Instantiate(smr.sharedMesh);
        runtimeMesh.name = smr.sharedMesh.name + "_Barycentric";

        InjectBarycentrics(runtimeMesh);

        // Assign the processed mesh back
        smr.sharedMesh = runtimeMesh;

        if (verbose)
            Debug.Log($"BarycentricInjector: Injected into {runtimeMesh.name}");
    }

    void InjectBarycentrics(Mesh mesh)
    {
        // Get base data
        var vertices = mesh.vertices;
        var normals = mesh.normals;
        var tangents = mesh.tangents;
        var colors = mesh.colors;
        var uv0 = new List<Vector2>();
        mesh.GetUVs(0, uv0);

        // Prepare triangle indexing across all submeshes
        int subMeshCount = mesh.subMeshCount;

        // We will flatten triangles so each triangle has unique vertices
        var newVertices = new List<Vector3>();
        var newNormals = new List<Vector3>();
        var newTangents = new List<Vector4>();
        var newColors = new List<Color>();
        var newUV0 = new List<Vector2>();
        var newBary = new List<Vector3>();
        var newBones = new List<BoneWeight>();

        var originalBones = mesh.boneWeights;

        var newIndicesPerSubmesh = new List<int[]>(subMeshCount);

        for (int sm = 0; sm < subMeshCount; sm++)
        {
            var tris = mesh.GetTriangles(sm);
            var newIndices = new int[tris.Length];

            for (int t = 0; t < tris.Length; t += 3)
            {
                int i0 = tris[t];
                int i1 = tris[t + 1];
                int i2 = tris[t + 2];

                // Duplicate original verts so each triangle has its own set
                int baseIndex = newVertices.Count;

                newVertices.Add(vertices[i0]);
                newVertices.Add(vertices[i1]);
                newVertices.Add(vertices[i2]);

                if (normals != null && normals.Length == vertices.Length)
                {
                    newNormals.Add(normals[i0]);
                    newNormals.Add(normals[i1]);
                    newNormals.Add(normals[i2]);
                }
                if (tangents != null && tangents.Length == vertices.Length)
                {
                    newTangents.Add(tangents[i0]);
                    newTangents.Add(tangents[i1]);
                    newTangents.Add(tangents[i2]);
                }
                if (colors != null && colors.Length == vertices.Length)
                {
                    newColors.Add(colors[i0]);
                    newColors.Add(colors[i1]);
                    newColors.Add(colors[i2]);
                }
                if (uv0 != null && uv0.Count == vertices.Length)
                {
                    newUV0.Add(uv0[i0]);
                    newUV0.Add(uv0[i1]);
                    newUV0.Add(uv0[i2]);
                }

                // Preserve skinning weights
                if (originalBones != null && originalBones.Length == vertices.Length)
                {
                    newBones.Add(originalBones[i0]);
                    newBones.Add(originalBones[i1]);
                    newBones.Add(originalBones[i2]);
                }

                // Barycentrics for wireframe: (1,0,0), (0,1,0), (0,0,1)
                newBary.Add(new Vector3(1, 0, 0));
                newBary.Add(new Vector3(0, 1, 0));
                newBary.Add(new Vector3(0, 0, 1));

                newIndices[t] = baseIndex;
                newIndices[t + 1] = baseIndex + 1;
                newIndices[t + 2] = baseIndex + 2;
            }

            newIndicesPerSubmesh[sm] = newIndices;
        }

        // Assign flattened geometry back to mesh
        mesh.Clear();

        mesh.SetVertices(newVertices);

        if (newNormals.Count == newVertices.Count) mesh.SetNormals(newNormals);
        if (newTangents.Count == newVertices.Count) mesh.SetTangents(newTangents);
        if (newColors.Count == newVertices.Count) mesh.SetColors(newColors);
        if (newUV0.Count == newVertices.Count) mesh.SetUVs(0, newUV0);

        // Store barycentrics in UV2 (or UV3/UV4 by choice)
        switch (targetChannel)
        {
            case UVChannel.UV2:
                var uv2 = new List<Vector2>(newVertices.Count);
                for (int i = 0; i < newBary.Count; i++) uv2.Add(new Vector2(newBary[i].x, newBary[i].y));
                mesh.SetUVs(1, uv2); // UV2 index = 1
                // Put the remaining component (z) into UV3's x if needed
                var uv3 = new List<Vector2>(newVertices.Count);
                for (int i = 0; i < newBary.Count; i++) uv3.Add(new Vector2(newBary[i].z, 0f));
                mesh.SetUVs(2, uv3); // UV3 index = 2
                break;
            case UVChannel.UV3:
                var uv3B = new List<Vector2>(newVertices.Count);
                for (int i = 0; i < newBary.Count; i++) uv3B.Add(new Vector2(newBary[i].x, newBary[i].y));
                mesh.SetUVs(2, uv3B); // UV3
                var uv4B = new List<Vector2>(newVertices.Count);
                for (int i = 0; i < newBary.Count; i++) uv4B.Add(new Vector2(newBary[i].z, 0f));
                mesh.SetUVs(3, uv4B); // UV4
                break;
            case UVChannel.UV4:
                var uv4C = new List<Vector2>(newVertices.Count);
                for (int i = 0; i < newBary.Count; i++) uv4C.Add(new Vector2(newBary[i].x, newBary[i].y));
                mesh.SetUVs(3, uv4C); // UV4
                // Drop z if not needed
                break;
        }

        // Recreate submeshes
        mesh.subMeshCount = subMeshCount;
        for (int sm = 0; sm < subMeshCount; sm++)
        {
            mesh.SetTriangles(newIndicesPerSubmesh[sm], sm);
        }

        // Preserve bindposes and bones for skinning
        mesh.boneWeights = newBones.ToArray();
        mesh.RecalculateBounds();
        mesh.RecalculateNormals(); // Optional, if normals lost
        mesh.Optimize(); // Unity may ignore; okay to call
    }
}