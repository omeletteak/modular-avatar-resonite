using System.Diagnostics;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using nadena.dev.ndmf.proto.rpc;

namespace nadena.dev.resonity.remote.puppeteer.rpc;

public class PendingEntryPoint : nadena.dev.ndmf.proto.rpc.ResoPuppeteer.ResoPuppeteerBase
{
    private TaskCompletionSource<EntryPoint> _backend = new();

    public void SetBackend(EntryPoint backend)
    {
        _backend.SetResult(backend);
    }

    private async Task<EntryPoint> GetBackend(ServerCallContext context)
    {
        var ep = await _backend.Task;
        
        if (context.Deadline < DateTime.Now) throw new RpcException(new Status(StatusCode.DeadlineExceeded, "Deadline exceeded"));

        return ep;
    }
    
    public override async Task<Empty> Ping(Empty request, ServerCallContext context)
    {
        return await (await GetBackend(context)).Ping(request, context);
    }

    public override async Task<Empty> ConvertObject(ConvertObjectRequest request, ServerCallContext context)
    {
        return await (await GetBackend(context)).ConvertObject(request, context);
    }

    public override Task<Empty> Shutdown(Empty request, ServerCallContext context)
    {
        Process.GetCurrentProcess().Kill();
        
        return Task.FromResult(new Empty());
    }
}