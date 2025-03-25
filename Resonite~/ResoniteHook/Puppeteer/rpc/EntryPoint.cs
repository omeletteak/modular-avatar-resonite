using System.Numerics;
using FrooxEngine;
using FrooxEngine.Store;
using Grpc.Core;
using JetBrains.Annotations;
using ResoPuppetSchema;
using Vector = ResoPuppetSchema.Vector;

namespace nadena.dev.resonity.remote.puppeteer.rpc;

public class EntryPoint : ResoPuppet.ResoPuppetBase
{
    private static readonly Empty Empty = new Empty();
    private readonly Engine _engine;
    private readonly World _world;
    private readonly AutoResetEvent _requestFrame;
    
    private HashSet<Slot> _slots = new();
    
    public EntryPoint(Engine engine, World world, AutoResetEvent requestFrame)
    {
        _engine = engine;
        _world = world;
        _requestFrame = requestFrame;
    }

    Task<T> RunAsync<T>(Func<Task<T>> func)
    {
        TaskCompletionSource<T> result = new();
        
        _world.Coroutines.StartTask(async f =>
        {
            Func<Task<T>> func = f!;
            try
            {
                result.SetResult(await func());
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                result.SetException(e);
            }
        }, func);

        _requestFrame.Set();
        
        return result.Task;
    }
    
    [MustUseReturnValue]
    Task<T> Run<T>(Func<T> func)
    {
        TaskCompletionSource<T> result = new();
        
        _world.Coroutines.Post(f =>
        {
            Func<T> func = (Func<T>)f!;
            try
            {
                result.SetResult(func());
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                result.SetException(e);
            }
        }, func);

        _requestFrame.Set();

        return result.Task;
    }
    
    public override Task<RefID> CreateSlot(CreateSlotRequest request, ServerCallContext context)
    {
        return Run(() =>
        {
            var slot = _world.RootSlot.AddSlot(request.Name ?? "Slot");
            _slots.Add(slot);

            if (request.LocalTransform != null) SetTransform(slot, request.LocalTransform);

            if (request.Parent != null)
            {
                try
                {
                    var elem = _world.ReferenceController.GetObjectOrThrow(request.Parent.Id);
                    if (elem is Slot parent)
                    {
                        slot.SetParent(parent, false);
                    }
                    else
                    {
                        throw new RpcException(new Status(StatusCode.Internal, "Referenced ID was not a slot"));
                    }
                }
                catch (Exception)
                {
                    slot.Destroy();
                    _slots.Remove(slot);
                    throw;
                }
            }

            if (request.IsRoot)
            {
                slot.AttachComponent<ObjectRoot>();
            }
            
            Console.WriteLine($"Slot {slot.Name} added with ID {slot.ReferenceID}");
            
            return new RefID() { Id = (ulong) slot.ReferenceID };
        });
    }

    private void SetTransform(Slot slot, Transform localTransform)
    {
        slot.LocalPosition = localTransform.Position.Vec3();
        slot.LocalRotation = localTransform.Rotation.Quat();
        slot.LocalScale = localTransform.Scale.Vec3();
    }

    public override Task<Empty> DestroyAll(Empty request, ServerCallContext context)
    {
        return Run(() =>
        {
            foreach (var slot in _slots)
            {
                if (!slot.IsDestroyed)
                {
                    slot.Destroy();
                }
            }

            _slots.Clear();

            return Empty;
        });
    }

    public override Task<Empty> DestroySlot(RefID request, ServerCallContext context)
    {
        return Run(() =>
        {
            var element = _world.ReferenceController.GetObjectOrNull(request.Id);

            if (element is Slot s)
            {
                s.Destroy();
                _slots.Remove(s);
            }

            return Empty;
        });
    }

    public override Task<Empty> Export(ExportRequest request, ServerCallContext context)
    {
        return RunAsync(async () =>
        {
            var element = _world.ReferenceController.GetObjectOrThrow(request.Root.Id);
            if (!(element is Slot slot))
            {
                throw new RpcException(new Status(StatusCode.InvalidArgument, "Root element is not a slot"));
            }

            Console.WriteLine("Pre-toSync");
            await new ToWorld();
            Console.WriteLine("Post-toSync");

            var tmpSlot = _world.AddSlot("Exporter");
            try
            {
                var exporter = tmpSlot.AttachComponent<PackageExportable>();

                if (!exporter.Root.TrySet(element))
                {
                    throw new RpcException(new Status(StatusCode.InvalidArgument, "Failed to set root element"));
                }

                if (!await exporter.Export(request.Folder, request.Name, request.IncludeVariants ? 1 : 0))
                {
                    throw new RpcException(new Status(StatusCode.Internal, "Export failed"));
                }

                return Empty;
            }
            finally
            {
                tmpSlot.Destroy();
            }
        });
    }

    public override Task<RefID> CreateMesh(MeshData request, ServerCallContext context)
    {
        return RunAsync(async () =>
        {
            Console.WriteLine("Creating mesh");
            var meshx = request.Mesh.ToMeshX();

            string tempFilePath = _engine.LocalDB.GetTempFilePath("meshx");
            meshx.SaveToFile(tempFilePath);

            Uri uri = await _engine.LocalDB.ImportLocalAssetAsync(tempFilePath, LocalDB.ImportLocation.Move);
            
            await new ToWorld();

            var elem = (Slot)_world.ReferenceController.GetObjectOrThrow(request.TargetSlot.Id);
            var meshComponent = elem.AttachComponent<StaticMesh>();
            meshComponent.URL.Value = uri;
            
            /*
            // For testing, add what we need to render
            var pbsMetallic = elem.AttachComponent<PBS_Metallic>();
            var meshRenderer = elem.AttachComponent<MeshRenderer>();
            meshRenderer.Mesh.TrySet(meshComponent);
            for (int i = 0; i < meshx.SubmeshCount; i++) meshRenderer.Materials.Add(pbsMetallic);

            elem.AttachComponent<MeshCollider>().Mesh.Value = meshComponent.ReferenceID;
            */

            return new RefID() { Id = (ulong)meshComponent.ReferenceID };
        });
    }

    public override Task<RefID> CreateTexture(CreateTextureRequest request, ServerCallContext context)
    {
        return RunAsync(async () =>
        {
            string extension;
            switch (request.Format)
            {
                case TextureFormat.Png: extension = "png"; break;
                case TextureFormat.Jpeg: extension = "jpeg"; break;
                default: throw new RpcException(new Status(StatusCode.InvalidArgument, "Unsupported texture format"));
            }
            
            string tempFilePath = _engine.LocalDB.GetTempFilePath(extension);
            System.IO.File.WriteAllBytes(tempFilePath, request.Data.ToByteArray());

            Uri uri = await _engine.LocalDB.ImportLocalAssetAsync(tempFilePath, LocalDB.ImportLocation.Move);
            
            await new ToWorld();
            
            var elem = (Slot)_world.ReferenceController.GetObjectOrThrow(request.TargetSlot.Id);
            var textureComponent = elem.AttachComponent<StaticTexture2D>();
            textureComponent.URL.Value = uri;

            return new RefID() { Id = (ulong) textureComponent.ReferenceID };
        });
    }

    public override Task<RefID> CreateTestMaterial(CreateTestMaterialRequest request, ServerCallContext context)
    {
        return RunAsync(async () =>
        {
            await new ToWorld();

            var slot = (Slot)_world.ReferenceController.GetObjectOrThrow(request.TargetSlot.Id);
            var pbsMetallic = slot.AttachComponent<PBS_Metallic>();
            pbsMetallic.AlbedoTexture.Value = request.Texture.Id;

            return new RefID() { Id = (ulong)pbsMetallic.ReferenceID };
        });
    }

    public override Task<RefID> CreateMeshRenderer(CreateMeshRendererRequest request, ServerCallContext context)
    {
        return RunAsync(async () =>
        {
            await new ToWorld();

            var slot = (Slot)_world.ReferenceController.GetObjectOrThrow(request.TargetSlot.Id);
            var meshRenderer = slot.AttachComponent<MeshRenderer>();

            meshRenderer.Mesh.Value = request.Mesh.Id;

            foreach (var mat in request.Material)
            {
                meshRenderer.Materials.Add((IAssetProvider<Material>)_world.ReferenceController.GetObjectOrThrow(mat.Id));
            }

            return new RefID() { Id = (ulong)meshRenderer.ReferenceID };
        });
    }
    
    
    public override Task<RefID> CreateSkinnedMeshRenderer(CreateSkinnedMeshRendererRequest request, ServerCallContext context)
    {
        return RunAsync(async () =>
        {
            await new ToWorld();

            var slot = (Slot)_world.ReferenceController.GetObjectOrThrow(request.TargetSlot.Id);
            var meshRenderer = slot.AttachComponent<SkinnedMeshRenderer>();

            meshRenderer.Mesh.Value = request.Mesh.Id;

            foreach (var mat in request.Material)
            {
                meshRenderer.Materials.Add((IAssetProvider<Material>)_world.ReferenceController.GetObjectOrThrow(mat.Id));
            }

            foreach (var bone in request.Bones)
            {
                var boneSlot = (Slot)_world.ReferenceController.GetObjectOrNull(bone.Id);
                
                meshRenderer.Bones.Add(boneSlot);
            }

            return new RefID() { Id = (ulong)meshRenderer.ReferenceID };
        });
    }

    public override Task<RefID> CreateMeshCollider(CreateMeshColliderRequest request, ServerCallContext context)
    {
        return RunAsync(async () =>
        {
            await new ToWorld();

            var slot = (Slot)_world.ReferenceController.GetObjectOrThrow(request.TargetSlot.Id);
            var meshCollider = slot.AttachComponent<MeshCollider>();

            if (request.Mesh != null)
            {
                meshCollider.Mesh.Value = request.Mesh.Id;
            }

            return new RefID() { Id = (ulong)meshCollider.ReferenceID };
        });
    }

    public override Task<Empty> MakeGrabbable(RefID request, ServerCallContext context)
    {
        return Run(() =>
        {
            var slot = (Slot)_world.ReferenceController.GetObjectOrThrow(request.Id);

            slot.AttachComponent<Grabbable>();

            return Empty;
        });
    }
}