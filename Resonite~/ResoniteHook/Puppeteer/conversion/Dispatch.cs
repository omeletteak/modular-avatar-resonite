#nullable enable

using Google.Protobuf;

using e = Elements.Core;
using f = FrooxEngine;
using p = nadena.dev.ndmf.proto;

namespace nadena.dev.resonity.remote.puppeteer.rpc;

public partial class RootConverter
{
    private const int PHASE_RESOLVE_REFERENCES = 0;
    private const int PHASE_RIG_SETUP = 100;
    private const int PHASE_AVATAR_SETUP = 200;
    private const int PHASE_ENABLE_RIG = 500;
    
    private Dictionary<string, ComponentBuilder> ComponentTypes = new();

    private delegate Task<f::IComponent> ComponentBuilder(f::Slot parent, p::Component component);
    private delegate Task<f::IComponent> TypedComponentBuilder<M>(f::Slot parent, M component) where M : IMessage, new();

    private Dictionary<string, AssetBuilder> AssetTypes = new();
    
    private delegate Task<f.IWorldElement?> AssetBuilder(f::Slot parent, p::Asset asset);
    private delegate Task<f.IWorldElement?> TypedAssetBuilder<M>(f::Slot parent, M asset) where M : IMessage, new();

    private void RegisterComponentType<M>(TypedComponentBuilder<M> builder)
        where M : IMessage, new()
    {
        ComponentTypes[new M().Descriptor.FullName] =
            (obj, untyped) => builder(obj, untyped.Component_.Unpack<M>());
    }
    
    private void RegisterAssetType<M>(TypedAssetBuilder<M> builder)
        where M : IMessage, new()
    {
        AssetTypes[new M().Descriptor.FullName] =
            (obj, untyped) => builder(obj, untyped.Asset_.Unpack<M>());
    }

    private SortedDictionary<int, List<Func<Task>>> _deferredActions = new();

    private void Defer(int phase, Func<Task> action)
    {
        if (!_deferredActions.TryGetValue(phase, out var actions))
        {
            actions = new List<Func<Task>>();
            _deferredActions[phase] = actions;
        }
        
        actions.Add(action);
    }
    
    private void Defer(int phase, Action action)
    {
        Defer(phase, () =>
        {
            action();
            return Task.CompletedTask;
        });
    }

    private async Task RunDeferred()
    {
        foreach (var kv in _deferredActions)
        {
            foreach (var action in kv.Value)
            {
                await action();
                await new f.ToWorld();
            }
        }
    }

    private void InitComponentTypes()
    {
        // Components
        RegisterComponentType<p::MeshRenderer>(CreateMeshRenderer);
        RegisterComponentType<p::RigRoot>(SetupRig);
        RegisterComponentType<p::AvatarDescriptor>(SetupAvatar);
        
        
        // Assets
        RegisterAssetType<p::Texture>(CreateTexture);
        RegisterAssetType<p::Material>(CreateMaterial);
        RegisterAssetType<p::mesh.Mesh>(CreateMesh);
    }
    
    
    private async Task ConvertComponent(f::Slot parent, p.Component component)
    {
        var typeName = Google.Protobuf.WellKnownTypes.Any.GetTypeName(component.Component_.TypeUrl);

        if (!ComponentTypes.TryGetValue(typeName, out var componentBuilder))
        {
            System.Console.WriteLine("Unknown component type: " + typeName);
            return;
        }
        
        var fComponent = await componentBuilder(parent, component);
        _objects[component.Id] = fComponent;
    }

    private async Task ConvertAsset(p.Asset asset)
    {
        var typeName = Google.Protobuf.WellKnownTypes.Any.GetTypeName(asset.Asset_.TypeUrl);
        
        if (!AssetTypes.TryGetValue(typeName, out var assetBuilder))
        {
            System.Console.WriteLine("Unknown asset type: " + typeName);
            return;
        }

        var slot = _assetRoot.AddSlot(asset.Name);
        var fAsset = await assetBuilder(slot, asset);
        await new f::ToWorld();
        if (fAsset != null) _assets[asset.Id] = fAsset;
    }
}