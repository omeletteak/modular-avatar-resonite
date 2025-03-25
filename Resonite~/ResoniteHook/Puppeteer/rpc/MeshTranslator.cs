using Elements.Assets;
using ResoPuppetSchema.Mesh;

namespace nadena.dev.resonity.remote.puppeteer.rpc;

public static class MeshTranslator
{
    public static MeshX ToMeshX(this Mesh rpcMesh)
    {
        var meshx = new MeshX();
        var vertexCount = rpcMesh.Positions.Count;
        meshx.SetVertexCount(vertexCount);

        for (int i = 0; i < rpcMesh.Positions.Count; i++)
        {
            meshx.SetVertex(i, rpcMesh.Positions[i].Vec3());
            if (i < rpcMesh.Normals.Count) meshx.SetNormal(i, rpcMesh.Normals[i].Vec3());
            if (i < rpcMesh.Tangents.Count) meshx.SetTangent(i, rpcMesh.Tangents[i].Vec4());
            if (i < rpcMesh.Colors.Count) meshx.SetColor(i, rpcMesh.Colors[i].Color());
        }

        for (int uv = 0; uv < rpcMesh.Uvs.Count; uv++)
        {
            var uvData = rpcMesh.Uvs[uv]!;
            for (int v = 0; v < uvData.Uvs.Count; v++)
            {
                meshx.SetUV(v, uv, uvData.Uvs[v].Vec2());
            }
        }
        
        // TODO: support for point submeshes
        foreach (var submesh in rpcMesh.Submeshes)
        {
            var submeshx = meshx.AddSubmesh<TriangleSubmesh>();
            foreach (var tri in submesh.Triangles)
            {
                submeshx.AddTriangle(tri.V0, tri.V1, tri.V2);
            }
        }

        foreach (var bone in rpcMesh.Bones)
        {
            meshx.AddBone(bone.Name).BindPose = bone.Bindpose.To4x4();
        }

        return meshx;
    }  
}