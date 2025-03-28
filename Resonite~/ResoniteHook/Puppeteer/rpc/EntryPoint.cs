using System.Numerics;
using FrooxEngine;
using FrooxEngine.Store;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using JetBrains.Annotations;
using nadena.dev.ndmf.proto.rpc;

namespace nadena.dev.resonity.remote.puppeteer.rpc;

public class EntryPoint : nadena.dev.ndmf.proto.rpc.ResoPuppeteer.ResoPuppeteerBase
{
    private static readonly Empty Empty = new Empty();
    private readonly Engine _engine;
    private readonly World _world;
    private readonly TickController _tickController;
    
    private HashSet<Slot> _slots = new();
    
    public EntryPoint(Engine engine, World world, TickController tickController)
    {
        _engine = engine;
        _world = world;
        _tickController = tickController;
    }

    Task<T> RunAsync<T>(Func<Task<T>> func)
    {
        TaskCompletionSource<T> result = new();

        IDisposable disposable = _tickController.StartRPC();
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
            finally
            {
                disposable.Dispose();
            }
        }, func);
        _tickController.RequestFrameNow();
        
        return result.Task;
    }
    
    [MustUseReturnValue]
    Task<T> Run<T>(Func<T> func)
    {
        TaskCompletionSource<T> result = new();
        
        IDisposable disposable = _tickController.StartRPC();
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
            finally
            {
                disposable.Dispose();
            }
        }, func);
        _tickController.RequestFrameNow();

        return result.Task;
    }

    public override async Task<Empty> ConvertObject(ConvertObjectRequest request, ServerCallContext context)
    {
        using var converter = new RootConverter(_engine, _world);

        await converter.Convert(request.Root, request.Path);

        return new Empty();
    }
}