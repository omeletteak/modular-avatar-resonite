#nullable enable

using System.Collections.Generic;
using System.IO;
using System.Linq;
using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using nadena.dev.ndmf.proto.mesh;
using nadena.dev.ndmf.proto.rpc;
using ResoPuppetSchema;
using UnityEditor;
using UnityEngine;
using BoneWeight = nadena.dev.ndmf.proto.mesh.BoneWeight;
using Mesh = UnityEngine.Mesh;
using p = nadena.dev.ndmf.proto;

internal static class Converters
{
    public static p::Vector ToRPC(this UnityEngine.Vector3 v) => new() { X = v.x, Y = v.y, Z = v.z };
    public static p::Quaternion ToRPC(this UnityEngine.Quaternion q) => new() { X = q.x, Y = q.y, Z = q.z, W = q.w };
    
    public static p::Color ToRPC(this Color c) => new() { R = c.r, G = c.g, B = c.b, A = c.a };
} 

public class TestClient
{
    private ResoPuppeteer.ResoPuppeteerClient client;

    private ulong nextAssetID = 1;
    private ulong nextObjectID = 1;

    private Dictionary<UnityEngine.Object, p.AssetID> _unityToAsset = new();
    private Dictionary<UnityEngine.Object, p.ObjectID> _unityToObject = new();
    private Dictionary<Mesh, SkinnedMeshRenderer> _referenceRenderer = new();

    private Queue<UnityEngine.Object> _unprocessedAssets = new();

    private p.ExportRoot _exportRoot = new();
    
    private p.AssetID MintAssetID()
    {
        return new p.AssetID() { Id = nextAssetID++ };
    }

    private p.ObjectID MintObjectID()
    {
        return new p.ObjectID() { Id = nextObjectID++ };
    }

    private p.AssetID MapAsset(UnityEngine.Object? asset)
    {
        if (asset == null) return new p.AssetID() { Id = 0 };
        if (_unityToAsset.TryGetValue(asset, out var id)) return id;
        
        _unityToAsset[asset] = id = MintAssetID();
        _unprocessedAssets.Enqueue(asset);

        return id;
    }

    private p.ObjectID MapObject(UnityEngine.Object? obj)
    {
        if (obj == null) return new p.ObjectID() { Id = 0 };
        if (_unityToObject.TryGetValue(obj, out var id)) return id;
        
        _unityToObject[obj] = id = MintObjectID();
        
        return id;
    }
    
    [MenuItem("Test/Run client")]
    static void RunClient()
    {
        new TestClient().Export(Selection.activeGameObject);
    }

    private void Export(GameObject go)
    {
        var conn = new Connector();
        client = conn.Client;

        _exportRoot.Root = CreateTransforms(go.transform);

        ProcessAssets();

        client.ConvertObject(new()
        {
            Root = _exportRoot,
            Path = "d:\\test.resonitepackage"
        });
    }


    private p.GameObject CreateTransforms(Transform t)
    {
        var protoObject = new p.GameObject();
        protoObject.Name = t.gameObject.name;
        protoObject.Id = MapObject(t.gameObject);
        protoObject.Enabled = t.gameObject.activeSelf;
        protoObject.LocalTransform = new p.Transform()
        {
            Position = t.localPosition.ToRPC(),
            Rotation = t.localRotation.ToRPC(),
            Scale = t.localScale.ToRPC()
        };

        foreach (Component c in t.gameObject.GetComponents<Component>())
        {
            IMessage protoComponent;
            switch (c)
            {
                case MeshRenderer mr:
                    protoComponent = TranslateMeshRenderer(mr);
                    break;
                case SkinnedMeshRenderer smr:
                    protoComponent = TranslateSkinnedMeshRenderer(smr);
                    break;
                default: continue;
            }

            p.Component wrapper = new p.Component()
            {
                Enabled = (c as Behaviour)?.enabled ?? true,
                Id = MapObject(c),
                Component_ = Any.Pack(protoComponent)
            };
            
            protoObject.Components.Add(wrapper);
        }

        foreach (Transform child in t)
        {
            protoObject.Children.Add(CreateTransforms(child));
        }

        return protoObject;
    }
    
    private IMessage TranslateMeshRenderer(MeshRenderer r)
    {
        var meshFilter = r.GetComponent<MeshFilter>();
        
        var protoMeshRenderer = new p.MeshRenderer();

        var sharedMesh = meshFilter.sharedMesh;
        
        TranslateRendererCommon(r, sharedMesh, protoMeshRenderer);

        return protoMeshRenderer;
    }

    private IMessage TranslateSkinnedMeshRenderer(SkinnedMeshRenderer r)
    {
        var protoMeshRenderer = new p.MeshRenderer();

        var sharedMesh = r.sharedMesh;

        if (sharedMesh != null) _referenceRenderer[sharedMesh] = r;
        
        TranslateRendererCommon(r, sharedMesh, protoMeshRenderer);

        foreach (Transform bone in r.bones)
        {
            protoMeshRenderer.Bones.Add(MapObject(bone.gameObject));
        }

        var blendshapes = sharedMesh.blendShapeCount;
        for (int i = 0; i < blendshapes; i++)
        {
            protoMeshRenderer.BlendshapeWeights.Add(r.GetBlendShapeWeight(i));
        }

        return protoMeshRenderer;
    }

    private void TranslateRendererCommon(Renderer r, Mesh? sharedMesh, p.MeshRenderer proto)
    {
        proto.Mesh = MapAsset(sharedMesh);
        foreach (var mat in r.sharedMaterials)
        {
            proto.Materials.Add(MapAsset(mat));
        }
    }


    private void ProcessAssets()
    {
        while (_unprocessedAssets.Count > 0)
        {
            var asset = _unprocessedAssets.Dequeue()!;
            var assetID = _unityToAsset[asset];

            IMessage protoAsset;
            switch (asset)
            {
                case Texture2D tex2d: protoAsset = TranslateTexture2D(tex2d); break;
                case Material mat: protoAsset = TranslateMaterial(mat); break;
                case Mesh mesh: protoAsset = TranslateMesh(mesh); break;
                default: continue;
            }

            p.Asset wrapper = new()
            {
                Name = asset.name,
                Id = assetID,
                Asset_ = Any.Pack(protoAsset)
            };
            
            _exportRoot.Assets.Add(wrapper);
        }
    }

    private IMessage TranslateTexture2D(Texture2D tex2d)
    {
        var protoTex = new p.Texture();
        var filePath = AssetDatabase.GetAssetPath(tex2d);

        protoTex.Bytes = new() { Inline = ByteString.CopyFrom(File.ReadAllBytes(filePath)) };

        if (filePath.ToLowerInvariant().EndsWith(".png")) protoTex.Format = p.TextureFormat.Png;
        else if (filePath.ToLowerInvariant().EndsWith(".jpg") || filePath.ToLowerInvariant().EndsWith(".jpeg")) protoTex.Format = p.TextureFormat.Jpeg;
        else throw new System.Exception("Unsupported texture format: " + filePath);

        return protoTex;
    }

    private IMessage TranslateMaterial(Material material)
    {
        var protoMat = new p.Material();
        
        protoMat.MainTexture = MapAsset(material.mainTexture);
        protoMat.MainColor = material.color.ToRPC();

        return protoMat;
    }
    
    
    private p.mesh.Mesh TranslateMesh(UnityEngine.Mesh mesh)
    {
        SkinnedMeshRenderer? referenceSMR = _referenceRenderer.GetValueOrDefault(mesh);
        
        var msgMesh = new p.mesh.Mesh();
        msgMesh.Positions.AddRange(mesh.vertices.Select(v => new p.Vector() { X = v.x, Y = v.y, Z = v.z }));
        msgMesh.Normals.AddRange(mesh.normals.Select(v => new p.Vector() { X = v.x, Y = v.y, Z = v.z }));
        msgMesh.Tangents.AddRange(mesh.tangents.Select(v => new p.Vector() { X = v.x, Y = v.y, Z = v.z, W = v.w }));
        msgMesh.Colors.AddRange(mesh.colors.Select(c => new p.Color() { R = c.r, G = c.g, B = c.b, A = c.a }));

        // only copy UV0 for now
        var uv0 = new p.mesh.UVChannel();
        uv0.Uvs.AddRange(mesh.uv.Select(v => new p.Vector() { X = v.x, Y = v.y }));
        msgMesh.Uvs.Add(uv0);

        var smc = mesh.subMeshCount;
        var indexBuf = mesh.triangles;
        for (int i = 0; i < smc; i++)
        {
            var submesh = new p.mesh.Submesh();
            var desc = mesh.GetSubMesh(i);

            submesh.Triangles = new();
            
            for (int v = 0; v < desc.indexCount; v += 3)
            {
                var tri = new p.mesh.Triangle()
                {
                    V0 = indexBuf[desc.indexStart + v] + desc.baseVertex,
                    V1 = indexBuf[desc.indexStart + v + 1] + desc.baseVertex,
                    V2 = indexBuf[desc.indexStart + v + 2] + desc.baseVertex
                };
                submesh.Triangles.Triangles.Add(tri);
                
                //Debug.Log("Triangle coordinates: " + msgMesh.Positions[tri.V0] + " " + msgMesh.Positions[tri.V1] + " " + msgMesh.Positions[tri.V2]);
            }
            
            msgMesh.Submeshes.Add(submesh);
        }

        var refBones = referenceSMR?.bones;
        var bindposes = mesh.bindposes;
        for (int i = 0; i < bindposes.Length; i++)
        {
            var boneName = refBones?[i].gameObject.name ?? "Bone" + i;
            var mat = new p.Matrix();
            var pose = bindposes[i];

            mat.Values.Capacity = 16;
            mat.Values.Add(pose.m00);
            mat.Values.Add(pose.m01);
            mat.Values.Add(pose.m02);
            mat.Values.Add(pose.m03);
            mat.Values.Add(pose.m10);
            mat.Values.Add(pose.m11);
            mat.Values.Add(pose.m12);
            mat.Values.Add(pose.m13);
            mat.Values.Add(pose.m20);
            mat.Values.Add(pose.m21);
            mat.Values.Add(pose.m22);
            mat.Values.Add(pose.m23);
            mat.Values.Add(pose.m30);
            mat.Values.Add(pose.m31);
            mat.Values.Add(pose.m32);
            mat.Values.Add(pose.m33);

            msgMesh.Bones.Add(new p.mesh.Bone() { Name = boneName, Bindpose = mat });
        }

        var boneWeights = mesh.boneWeights;

        if (boneWeights.Length > 0)
        {
            for (int v = 0; v < mesh.vertexCount; v++)
            {
                var vbw = new VertexBoneWeights();
                var weights = boneWeights[v];
                if (weights.weight0 > 0)
                    vbw.BoneWeights.Add(new BoneWeight()
                        { Weight = weights.weight0, BoneIndex = (uint)weights.boneIndex0 });
                if (weights.weight1 > 0)
                    vbw.BoneWeights.Add(new BoneWeight()
                        { Weight = weights.weight1, BoneIndex = (uint)weights.boneIndex1 });
                if (weights.weight2 > 0)
                    vbw.BoneWeights.Add(new BoneWeight()
                        { Weight = weights.weight2, BoneIndex = (uint)weights.boneIndex2 });
                if (weights.weight3 > 0)
                    vbw.BoneWeights.Add(new BoneWeight()
                        { Weight = weights.weight3, BoneIndex = (uint)weights.boneIndex3 });

                msgMesh.VertexBoneWeights.Add(vbw);
            }
        }

        return msgMesh;
    }
}
