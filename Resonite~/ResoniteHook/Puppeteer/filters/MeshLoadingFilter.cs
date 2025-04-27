using Elements.Core;
using FrooxEngine;
using FrooxEngine.FrooxEngine.ProtoFlux.CoreNodes;
using FrooxEngine.ProtoFlux;
using FrooxEngine.ProtoFlux.Runtimes.Execution;
using FrooxEngine.ProtoFlux.Runtimes.Execution.Nodes;
using FrooxEngine.ProtoFlux.Runtimes.Execution.Nodes.Casts;
using FrooxEngine.ProtoFlux.Runtimes.Execution.Nodes.FrooxEngine.Assets;
using FrooxEngine.ProtoFlux.Runtimes.Execution.Nodes.FrooxEngine.Async;
using FrooxEngine.ProtoFlux.Runtimes.Execution.Nodes.FrooxEngine.Slots;
using FrooxEngine.ProtoFlux.Runtimes.Execution.Nodes.FrooxEngine.Variables;
using FrooxEngine.ProtoFlux.Runtimes.Execution.Nodes.Math.Random;
using FrooxEngine.ProtoFlux.Runtimes.Execution.Nodes.Operators;
using nadena.dev.resonity.remote.puppeteer.dynamic_flux;

namespace nadena.dev.resonity.remote.puppeteer.filters;

public class MeshLoadingFilter
{
    private Slot _avatarRoot;
    private string _loadingUri = "resrec:///U-1Nj73SRfaDY/R-c507ae1e-fb6c-4da6-bdbc-8048b06e7ff7"; 

    public MeshLoadingFilter(Slot avatarRoot)
    {
        this._avatarRoot = avatarRoot;
    }

    public async Task Apply()
    {
        var centeredRoot = _avatarRoot.FindChild("CenteredRoot");
        var assets = centeredRoot?.FindChild("__Assets");
        if (assets == null) return;

        var meshAssets = assets.GetComponentsInChildren<StaticMesh>();
        if (meshAssets.Count == 0) return;

        var loadingSpinner = await SpawnLoadingSpinner();
        var headProxy = _avatarRoot.FindChild("Head Proxy");
        loadingSpinner.LocalPosition = headProxy.LocalPosition;

        var checkRoot = _avatarRoot.AddSlot("Mesh loading gate");
        var checkRootValue = checkRoot.AttachComponent<ValueField<bool>>();
        centeredRoot.ActiveSelf_Field.DriveFrom(checkRootValue.Value);
        
        using var rootFlux = new FluxBuilder(checkRoot, "ProtoFlux");

        List<INodeValueOutput<bool>> assetLoadingStatus = new();
        using (var loadingCheckGroup = rootFlux.Vertical("Asset Checks"))
        {
            foreach (var mesh in meshAssets)
            {
                assetLoadingStatus.Add(CreateAssetLoadingStatusChecker(loadingCheckGroup, mesh));
            }
            assetLoadingStatus.Reverse();
        }

        INodeValueOutput<bool> notLoaded;
        var boolNAND = rootFlux.Spawn<NAND_Multi_Bool>();
        foreach (var status in assetLoadingStatus)
        {
            boolNAND.Operands.Add().Target = status;
        }

        notLoaded = boolNAND;

        DataModelBooleanToggle updateTrigger;
        INodeValueOutput<bool> updateTriggerOutput;
        SyncRef<ISyncNodeOperation> flowNode;
        
        using (var vertical = rootFlux.Vertical())
        {
            flowNode = vertical.Spawn<OnStart>().Trigger;
            updateTrigger = vertical.Spawn<DataModelBooleanToggle>();
            
            updateTriggerOutput = updateTrigger;
        }

        INodeValueOutput<bool> isLoadedWithTrigger;
        INodeValueOutput<bool> notLoadedWithTrigger;
        using (var vertical = rootFlux.Vertical())
        {
            var and1 = vertical.Spawn<AND_Bool>();
            and1.A.Target = notLoaded;
            notLoadedWithTrigger = and1;
            
            var nand2 = vertical.Spawn<NAND_Bool>();
            nand2.A.Target = notLoaded;
            isLoadedWithTrigger = nand2;
            
            var eq = vertical.Spawn<ValueEquals<bool>>();
            eq.A.Target = updateTriggerOutput;
            eq.B.Target = updateTriggerOutput;
            
            and1.B.Target = eq;
            nand2.B.Target = eq;
        }

        using (var vertical = rootFlux.Vertical())
        {
            var driver = vertical.Spawn<ValueFieldDrive<bool>>();
            driver.TrySetRootTarget(checkRootValue.Value);
            driver.Value.Target = isLoadedWithTrigger;

            using (var horizontal = vertical.Horizontal())
            {
                var spinnerDriver = horizontal.Spawn<ValueFieldDrive<bool>>();
                spinnerDriver.TrySetRootTarget(loadingSpinner.ActiveSelf_Field);
                spinnerDriver.Value.Target = notLoadedWithTrigger;
            }
        }

        SyncRef<INodeOperation> asyncFlow;
        
        var startAsyncTask = rootFlux.Spawn<StartAsyncTask>();
        flowNode.Target = startAsyncTask;
        asyncFlow = startAsyncTask.TaskStart;

        var loop = rootFlux.Spawn<AsyncWhile>();
        loop.Condition.Target = boolNAND;
        asyncFlow.Target = loop;

        using (var vertical = rootFlux.Vertical())
        {
            var loopTime = vertical.Spawn<ValueInput<float>>();
            loopTime.Value.Value = 0.25f;

            var loopDelay = vertical.Spawn<DelayUpdatesOrSecondsFloat>();
            loopDelay.Duration.Target = loopTime;
            loop.LoopIteration.Target = loopDelay;
        }

        loop.LoopEnd.Target = updateTrigger.Toggle;
    }

    private async Task<Slot> SpawnLoadingSpinner()
    {
        var slot = _avatarRoot.AddSlot("LoadingSpinner");

        Console.WriteLine("==== Spawn loading spinner ==="); 
        bool result = await slot.LoadObjectAsync(new Uri(_loadingUri));
        Console.WriteLine("Result: " + result);
        
        slot.LocalRotation = floatQ.Identity;
        slot.LocalScale = float3.One;

        return slot;
    }

    private INodeValueOutput<bool> CreateAssetLoadingStatusChecker(FluxBuilder loadingCheckGroup, StaticMesh mesh)
    {
        using var flux = loadingCheckGroup.Horizontal("Status Check: " + mesh.Slot.Name);

        var reference = flux.Spawn<ElementSource<StaticMesh>>();
        reference.TrySetRootSource(mesh);

        var cast = flux.Spawn<ObjectCast<StaticMesh, IAssetProvider<Mesh>>>();
        cast.Input.Target = reference;
        
        var getAsset = flux.Spawn<GetAsset<Mesh>>();
        getAsset.Provider.Target = cast;

        var isNull = flux.Spawn<IsNull<Mesh>>();
        isNull.Instance.Target = getAsset;

        var not = flux.Spawn<NOT_Bool>();
        not.A.Target = isNull;

        return not;
    }
}