#nullable enable

using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Google.Protobuf;
using JetBrains.Annotations;
using ResoPuppetSchema;
using ResoPuppetSchema.Mesh;
using UnityEditor;
using UnityEngine;
using Mesh = ResoPuppetSchema.Mesh.Mesh;
using TextureFormat = ResoPuppetSchema.TextureFormat;
using Transform = ResoPuppetSchema.Transform;

internal static class Converters
{
    public static Vector ToRPC(this UnityEngine.Vector3 v) => new() { X = v.x, Y = v.y, Z = v.z };
    public static ResoPuppetSchema.Quaternion ToRPC(this UnityEngine.Quaternion q) => new() { X = q.x, Y = q.y, Z = q.z, W = q.w };
} 

public class TestClient
{
    RefID? _assetRoot;
    Dictionary<UnityEngine.Object, RefID> _assets = new();
    Dictionary<UnityEngine.Transform, RefID> _transforms = new();
    
    private ResoPuppet.ResoPuppetClient client;
    
    [MenuItem("Test/Run client")]
    static void RunClient()
    {
        new TestClient().Export(Selection.activeGameObject);
    }

    private void Export(GameObject go)
    {
        var conn = new Connector();
        client = conn.Client;

        try
        {
            var root = CreateTransforms(null, go.transform);
            
            _assetRoot = client.CreateSlot(new() { Name = "___Assets", Parent = root});
            
            client.MakeGrabbable(root);

            foreach (var c in go.GetComponentsInChildren<Renderer>(true))
            {
                var t = c.transform;
                var obj = _transforms[t]!;
                
                if (t.TryGetComponent<MeshRenderer>(out var r) && t.TryGetComponent<MeshFilter>(out var mf))
                {
                    CreateMeshRenderer(obj, mf.sharedMesh, r);
                } else if (t.TryGetComponent<SkinnedMeshRenderer>(out var smr))
                {
                    CreateSkinnedMeshRenderer(obj, smr.sharedMesh, smr);
                }
            }
            
            client.Export(new() { Name = "test.resonitepackage", Folder = "d:\\", Root = root });
        }
        finally
        {
            client.DestroyAll(new());
        }
    }

    private RefID CreateTransforms(
        RefID? parent, 
        UnityEngine.Transform t
    )
    {
        var req = new CreateSlotRequest()
        {
            Name = t.gameObject.name, LocalTransform = new Transform()
            {
                Position = t.localPosition.ToRPC(),
                Rotation = t.localRotation.ToRPC(),
                Scale = t.localScale.ToRPC()
            },
            Parent = parent,
            IsRoot = parent == null
        };
        var obj = client.CreateSlot(req);
        _transforms[t] = obj;

        if (_assetRoot == null)
        {
            _assetRoot = client.CreateSlot(new() { Name = "___Assets", Parent = obj});
        }
        
        foreach (UnityEngine.Transform child in t)
        {
            CreateTransforms(obj, child);
        }
        return obj;
    }

    private void CreateMeshRenderer(RefID slot, UnityEngine.Mesh mesh, MeshRenderer r)
    {
        RefID meshRef = CreateMeshAsset(mesh, null);
        
        CreateMeshRendererRequest req = new()
        {
            TargetSlot = slot,
            Mesh = meshRef,
        };
        
        foreach (var mat in r.sharedMaterials)
        {
            if (mat == null)
            {
                continue;
            }
            
            var matRef = CreateMaterialAsset(client, mat);
            req.Material.Add(matRef);
        }
        
        client.CreateMeshRenderer(req);
        client.CreateMeshCollider(new() { TargetSlot = slot, Mesh = meshRef });
    }
    
    
    private void CreateSkinnedMeshRenderer(RefID slot, UnityEngine.Mesh mesh, SkinnedMeshRenderer r)
    {
        RefID meshRef = CreateMeshAsset(mesh, r);
        
        CreateSkinnedMeshRendererRequest req = new()
        {
            TargetSlot = slot,
            Mesh = meshRef,
        };
        
        foreach (var mat in r.sharedMaterials)
        {
            if (mat == null)
            {
                continue;
            }
            
            var matRef = CreateMaterialAsset(client, mat);
            req.Material.Add(matRef);
        }

        foreach (var bone in r.bones)
        {
            req.Bones.Add(_transforms.GetValueOrDefault(bone) ?? new RefID() { Id = 0 });
        }
        
        client.CreateSkinnedMeshRenderer(req);
        client.CreateMeshCollider(new() { TargetSlot = slot, Mesh = meshRef });
    }

    private RefID CreateMaterialAsset(ResoPuppet.ResoPuppetClient client, Material mat)
    {
        if (_assets.TryGetValue(mat, out var id))
        {
            return id;
        }
        
        var slot = client.CreateSlot(new() { Name = mat.name, Parent = _assetRoot });
        var tex = CreateTextureAsset(mat.mainTexture);
        
        var req = new CreateTestMaterialRequest()
        {
            TargetSlot = slot,
            Texture = tex
        };
        
        var refId = client.CreateTestMaterial(req);
        
        _assets[mat] = refId;
        
        return refId;
    }

    private RefID CreateTextureAsset(Texture matMainTexture)
    {
        if (_assets.TryGetValue(matMainTexture, out var id))
        {
            return id;
        }
        
        var slot = client.CreateSlot(new() { Name = matMainTexture.name, Parent = _assetRoot });
        
        var path = AssetDatabase.GetAssetPath(matMainTexture);
        TextureFormat format;
        if (path.EndsWith(".png"))
        {
            format = TextureFormat.Png;
        } else if (path.EndsWith(".jpeg"))
        {
            format = TextureFormat.Jpeg;
        }
        else
        {
            throw new System.Exception("Unsupported texture format: " + path);
        }
        
        var req = new CreateTextureRequest()
        {
            TargetSlot = slot,
            Format = format,
            Data = ByteString.CopyFrom(System.IO.File.ReadAllBytes(path))
        };
        
        var refId = client.CreateTexture(req);
        
        _assets[matMainTexture] = refId;
        
        return refId;
    }

    private RefID CreateMeshAsset(UnityEngine.Mesh mesh, SkinnedMeshRenderer referenceSMR)
    {
        if (_assets.TryGetValue(mesh, out var id))
        {
            return id;
        }
        
        var slot = client.CreateSlot(new() { Name = mesh.name, Parent = _assetRoot });
        
        var msgMesh = new Mesh();
        msgMesh.Positions.AddRange(mesh.vertices.Select(v => new Vector() { X = v.x, Y = v.y, Z = v.z }));
        msgMesh.Normals.AddRange(mesh.normals.Select(v => new Vector() { X = v.x, Y = v.y, Z = v.z }));
        msgMesh.Tangents.AddRange(mesh.tangents.Select(v => new Vector() { X = v.x, Y = v.y, Z = v.z, W = v.w }));
        msgMesh.Colors.AddRange(mesh.colors.Select(c => new Vector() { X = c.r, Y = c.g, Z = c.b, W = c.a }));

        // only copy UV0 for now
        var uv0 = new UVChannel();
        uv0.Uvs.AddRange(mesh.uv.Select(v => new Vector() { X = v.x, Y = v.y }));
        msgMesh.Uvs.Add(uv0);

        var smc = mesh.subMeshCount;
        var indexBuf = mesh.triangles;
        for (int i = 0; i < smc; i++)
        {
            var submesh = new Submesh();
            var desc = mesh.GetSubMesh(i);
            
            
            for (int v = 0; v < desc.indexCount; v += 3)
            {
                var tri = new Triangle()
                {
                    V0 = indexBuf[desc.indexStart + v] + desc.baseVertex,
                    V1 = indexBuf[desc.indexStart + v + 1] + desc.baseVertex,
                    V2 = indexBuf[desc.indexStart + v + 2] + desc.baseVertex
                };
                submesh.Triangles.Add(tri);
                
                //Debug.Log("Triangle coordinates: " + msgMesh.Positions[tri.V0] + " " + msgMesh.Positions[tri.V1] + " " + msgMesh.Positions[tri.V2]);
            }
            
            msgMesh.Submeshes.Add(submesh);
        }

        var refBones = referenceSMR?.bones;
        var bindposes = mesh.bindposes;
        for (int i = 0; i < bindposes.Length; i++)
        {
            var boneName = refBones?[i].gameObject.name ?? "Bone" + i;
            var mat = new Matrix();
            var pose = bindposes[i];
            for (int j = 0; j < 16; j++)
            {
                mat.Values.Add(pose[j]);
            }

            msgMesh.Bones.Add(new Bone() { Name = boneName, Bindpose = mat });
        }

        var refId = client.CreateMesh(new MeshData()
        {
            TargetSlot = slot,
            Mesh = msgMesh
        });
        
        _assets[mesh] = refId;
        
        return refId;
    }
}
