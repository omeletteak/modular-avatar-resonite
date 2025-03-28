using System.Numerics;
using System.Reflection;
using Assimp = Assimp;
using Elements.Core;
using FrooxEngine.CommonAvatar;
using FrooxEngine.FinalIK;
using FrooxEngine.Store;
using Google.Protobuf.Collections;
using SkyFrost.Base;
using Record = SkyFrost.Base.Record;

namespace nadena.dev.resonity.remote.puppeteer.rpc;

using f = FrooxEngine;
using pr = Google.Protobuf.Reflection;
using p = nadena.dev.ndmf.proto;
using pm = nadena.dev.ndmf.proto.mesh;

public partial class RootConverter : IDisposable
{
    private Dictionary<p::AssetID, f.IWorldElement> _assets = new();
    private Dictionary<p::ObjectID, f.IWorldElement> _objects = new();
    private List<Action> _deferredConfiguration = new();
    private List<Func<Task>> _deferredConfigurationAsync = new();

    private f.Slot _root;
    private f.Slot _assetRoot;

    private readonly f::Engine _engine;
    private readonly f::World _world;

    private T? Asset<T>(p::AssetID? id) where T : class
    {
        if (id == null) return null;
        
        if (!_assets.TryGetValue(id, out var elem))
        {
            return null;
        }

        if (elem is not T) throw new InvalidOperationException($"Expected {typeof(T).Name}, got {elem.GetType().Name}");

        return (T)elem;
    }
    
    private RefID AssetRefID<T>(p::AssetID? id) where T: class
    {
        return (Asset<T>(id) as f.IWorldElement)?.ReferenceID ?? RefID.Null;
    }

    private T? Object<T>(p::ObjectID? id) where T: class, f.IWorldElement
    {
        if (id == null) return null;
        
        if (!_objects.TryGetValue(id, out var elem))
        {
            return null;
        }

        if (elem is not T) throw new InvalidOperationException($"Expected {typeof(T).Name}, got {elem.GetType().Name}");

        return (T)elem;
    }
    
    private RefID ObjectRefID<T>(p::ObjectID? id) where T: class, f.IWorldElement
    {
        return Object<T>(id)?.ReferenceID ?? RefID.Null;
    }
    
    public RootConverter(f::Engine engine, f::World world)
    {
        InitComponentTypes();
        _engine = engine;
        _world = world;
    }

    public void Dispose()
    {
        _world.Coroutines.StartTask(async () =>
        {
            await new f::ToWorld();

            _root?.Destroy();
            _assetRoot?.Destroy();
        });
    }

    public Task Convert(p.ExportRoot exportRoot, string path)
    {
        if (_assetRoot != null) throw new InvalidOperationException("Already converted");
        
        return _world.Coroutines.StartTask(async () =>
        {
            await new f::ToWorld();

            await _ConvertSync(exportRoot, path);
        });
    }

    private async Task _ConvertSync(p.ExportRoot exportRoot, string path)
    {
        _assetRoot = _world.RootSlot.AddSlot("__Assets");

        await ConvertAssets(exportRoot.Assets);

        _root = await ConvertGameObject(exportRoot.Root, _world.RootSlot);

        _assetRoot.SetParent(_root);
        
        // Reset root transform
        _root.Position_Field.Value = new float3();
        _root.Rotation_Field.Value = new floatQ(0, 0, 0, 1);
        
        foreach (var action in _deferredConfiguration)
        {
            action();
        }
        
        foreach (var action in _deferredConfigurationAsync)
        {
            await action();
        }

        //_root.AttachComponent<f.DestroyRoot>();
        _root.AttachComponent<SimpleAvatarProtection>();
        _root.AttachComponent<f.ObjectRoot>();
        
        SavedGraph savedGraph = _root.SaveObject(f.DependencyHandling.CollectAssets);
        Record record = RecordHelper.CreateForObject<Record>(_root.Name, "", null);

        await new f.ToBackground();

        using (var stream = new FileStream(path, FileMode.Create))
        {
            await f.PackageCreator.BuildPackage(_engine, record, savedGraph, stream, false);
        }
    }

    private async Task<f.Slot> ConvertGameObject(p.GameObject gameObject, f::Slot parent)
    {
        var slot = parent.AddSlot(gameObject.Name);

        slot.Position_Field.Value = gameObject.LocalTransform.Position.Vec3();
        slot.Rotation_Field.Value = gameObject.LocalTransform.Rotation.Quat();
        slot.Scale_Field.Value = gameObject.LocalTransform.Scale.Vec3();
        
        _objects[gameObject.Id] = slot;

        foreach (var component in gameObject.Components)
        {
            if (component != null) await ConvertComponent(slot, component);
        }

        foreach (var child in gameObject.Children)
        {
            if (child != null) await ConvertGameObject(child, slot);
        }

        return slot;
    }

    private async Task ConvertAssets(RepeatedField<p::Asset> exportRootAssets)
    {
        List<Task> assetImportTasks = new();
        foreach (var asset in exportRootAssets)
        {
            assetImportTasks.Add(ConvertAsset(asset));
        }

        foreach (var task in assetImportTasks)
        {
            await task; // propagate exceptions
        }
    }

    private async Task<f.IWorldElement?> CreateMaterial(f::Slot holder, p::Material material)
    {
        await new f::ToWorld();
        
        // TODO: handle other material types
        var materialComponent = holder.AttachComponent<f::PBS_Metallic>();
        _deferredConfiguration.Add(() =>
        {
            materialComponent.AlbedoTexture.Value = AssetRefID<f.IAssetProvider<f.Texture2D>>(material.MainTexture);    
        });
        materialComponent.AlbedoColor.Value = material.MainColor?.ColorX() ?? colorX.White;

        return materialComponent;
    }

    private async Task<f.IWorldElement?> CreateTexture(f::Slot holder, p::Texture texture)
    {
        await new f::ToBackground();
        
        string extension;

        switch (texture.Format)
        {
            case p.TextureFormat.Png:
                extension = ".png";
                break;
            case p.TextureFormat.Jpeg:
                extension = ".jpg";
                break;
            default:
                System.Console.WriteLine("Unknown texture format");
                return null;
        }
        
        // only support blob contents for now
        if (texture.Bytes == null)
        {
            System.Console.WriteLine("Texture has no blob");
            return null;
        }

        var path = _engine.LocalDB.GetTempFilePath(extension);
        
        await File.WriteAllBytesAsync(path, texture.Bytes.Inline.ToByteArray());
        Uri uri = await _engine.LocalDB.ImportLocalAssetAsync(path, LocalDB.ImportLocation.Move);

        await new f.ToWorld();

        var textureComponent = holder.AttachComponent<f.StaticTexture2D>();
        textureComponent.URL.Value = uri;
        
        return textureComponent;
    }
    
    private async Task<f.IWorldElement?> CreateMesh(f::Slot holder, p::mesh.Mesh mesh)
    {
        await new f::ToBackground();

        var meshx = mesh.ToMeshX();
        
        string tempFilePath = _engine.LocalDB.GetTempFilePath(".mesh");
        meshx.SaveToFile(tempFilePath);
        
        Uri uri = await _engine.LocalDB.ImportLocalAssetAsync(tempFilePath, LocalDB.ImportLocation.Move);
        
        await new f::ToWorld();
        
        var meshComponent = holder.AttachComponent<f::StaticMesh>();
        meshComponent.URL.Value = uri;

        return meshComponent;
    }

    private async Task<f.IComponent> SetupRig(f.Slot parent, p.RigRoot component)
    {
        // This is a virtual component that tags a slot as being the root of a rigged model
        await new f.ToWorld();
        
        foreach (f.SkinnedMeshRenderer smr in parent.GetComponentsInChildren<f.SkinnedMeshRenderer>())
        {
            while (!smr.Mesh.IsAssetAvailable)
            {
                await new f::ToBackground();
                await new f::ToWorld();
            }
        }

        var rig = parent.AttachComponent<f.Rig>();
        var relay = parent.AttachComponent<f.MeshRendererMaterialRelay>();
        
        foreach (f.SkinnedMeshRenderer smr in parent.GetComponentsInChildren<f.SkinnedMeshRenderer>())
        {
            smr.BoundsComputeMethod.Value = f.SkinnedBounds.Static;
            rig.Bones.AddRangeUnique(smr.Bones);
            relay.Renderers.Add(smr);
        }
        
        _deferredConfigurationAsync.Add(async () =>
        {
            // Invoke ModelImporter to set up the rig
            var settings = new f.ModelImportSettings()
            {
                //ForceTpose = true,
                SetupIK = true,
                GenerateColliders = true,
                //GenerateSkeletonBoneVisuals = true,
                //GenerateSnappables = true,
            };
            
            Console.WriteLine("Root position: " + parent.Position_Field.Value);
            
            Type ty_modelImportData = typeof(f.ModelImporter).GetNestedType("ModelImportData", BindingFlags.NonPublic);
            var mid_ctor = ty_modelImportData.GetConstructors()[0];
            var modelImportData = mid_ctor.Invoke(new object[]
            {
                "",
                null,
                parent,
                _assetRoot,
                settings,
                new NullProgress()
            });

            ty_modelImportData.GetField("settings")!.SetValue(modelImportData, settings);
        
            typeof(f.ModelImporter).GetMethod(
                "GenerateRigBones", 
                BindingFlags.Static | BindingFlags.NonPublic,
                null,
                new []
                {
                    typeof(f.Rig), ty_modelImportData
                },
                null
                )!.Invoke(null, [
                    rig, modelImportData
                ]);

            await SetupAvatar(rig, component);
        });
        
        return rig;
    }

    private async Task SetupAvatar(f.Rig rig, p.RigRoot spec)
    {
        var slot = rig.Slot;

        var tmpSlot = rig.Slot.FindChild("CenteredRoot");

        var avatarBuilderSlot = tmpSlot.AddSlot("Avatar Builder");
        var avatarCreator = avatarBuilderSlot.AttachComponent<f.AvatarCreator>();
        var ref_headset = field<f.SyncRef<f.Slot>>(avatarCreator, "_headsetReference");
        var ref_left_hand = field<f.SyncRef<f.Slot>>(avatarCreator, "_leftReference");
        var ref_right_hand = field<f.SyncRef<f.Slot>>(avatarCreator, "_rightReference");

        var slot_headset = ref_headset.Target;
        var slot_left_hand = ref_left_hand.Target;
        var slot_right_hand = ref_right_hand.Target;

        var bone_head = Object<f.Slot>(spec.Head);
        var bone_left_hand = Object<f.Slot>(spec.HandLeft);
        var bone_right_hand = Object<f.Slot>(spec.HandRight);

        // Sleep one frame
        await new f.ToBackground();
        await new f.ToWorld();
        
        slot_headset.LocalPosition= spec.EyePosition.Vec3(); // relative to avatar root
        Console.WriteLine("Local position in head space: " + bone_head.GlobalPointToLocal(slot_headset.GlobalPosition));
        slot_headset.GlobalRotation = bone_head.GlobalRotation;
        
        Console.WriteLine("Head fwd vector: " + bone_head.GlobalDirectionToLocal(Vector3.UnitZ));
        Console.WriteLine("Headset fwd vector: " + slot_headset.GlobalDirectionToLocal(Vector3.UnitZ));
        
        // Align hands. The resonite (right) hand model has the Z axis facing along the fingers, and Y up.
        // First, find the corresponding axis for the model's hand.
        var rightArm = bone_right_hand.Parent;
        var handFwd = rightArm.LocalDirectionToGlobal(bone_right_hand.LocalPosition.Normalized);
        
        // Now use a look rotation to align the model's hand with the resonite hand
        var rot = Quaternion.CreateFromRotationMatrix(
            Matrix4x4.CreateLookAt(Vector3.Zero, handFwd, Vector3.UnitY)
        );

        var right = tmpSlot.AddSlot("TEMP Right hand reference");
        right.GlobalPosition = slot_right_hand.GlobalPosition;
        right.GlobalRotation = slot_right_hand.GlobalRotation;
        
        Console.WriteLine("Left hand +X vector: " + right.GlobalDirectionToLocal(Vector3.UnitX));
        Console.WriteLine("Hand directional vector: " + rightArm.LocalDirectionToGlobal(bone_right_hand.LocalPosition.Normalized));

        Console.WriteLine("Update wait...");
        await new f.NextUpdate();
        Console.WriteLine("Update wait...");
        await new f.NextUpdate();
        
        await new f.ToWorld();

        for (int i = 0; i < 10 && slot_right_hand.Position_Field.IsDriven; i++)
        {
            Console.WriteLine("Update wait... " + i);
            await new f.NextUpdate();
        
            await new f.ToWorld();
        }
        
        slot_right_hand.GlobalPosition = bone_right_hand.GlobalPosition;
        slot_right_hand.GlobalRotation = rot;
        
        await new f.NextUpdate();
        await new f.NextUpdate();
        
        await new f.ToWorld();

        var marker = bone_right_hand.AddSlot("TMP Marker");
        marker.GlobalPosition = slot_right_hand.GlobalPosition;
        marker.GlobalRotation = slot_right_hand.GlobalRotation;
        
        await new f.NextUpdate();
        await new f.NextUpdate();
        
        await new f.ToWorld();
        
        var m_alignAnchors = avatarCreator.GetType()
            .GetMethod("AlignAnchors", BindingFlags.NonPublic | BindingFlags.Instance);

        m_alignAnchors!.Invoke(avatarCreator, [slot_left_hand]);
        m_alignAnchors.Invoke(avatarCreator, [slot_right_hand]);

        // TODO - scale adjustment
        
        avatarCreator.GetType().GetMethod("RunCreate", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(avatarCreator, null);
    }

    T? field<T>(object obj, string name)
    {
        return (T?)obj.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(obj);
    }
}

class NullProgress : Elements.Core.IProgressIndicator
{
    public void UpdateProgress(float percent, LocaleString progressInfo, LocaleString detailInfo)
    {
    }

    public void ProgressDone(LocaleString message)
    {
    }

    public void ProgressFail(LocaleString message)
    {
    }
}