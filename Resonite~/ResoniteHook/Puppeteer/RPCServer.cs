using Grpc.Core;
using GrpcDotNetNamedPipes;
using ResoPuppetSchema;

namespace nadena.dev.resonity.remote.puppeteer;

public class RPCServer
{
    public void Start(ResoPuppet.ResoPuppetBase entryPoint)
    {
        var server = new NamedPipeServer("TEST_PIPE_PUPPETEER", new() {
            CurrentUserOnly = true,
        });

        ResoPuppet.BindService(server.ServiceBinder, entryPoint);
        
        server.Start();
    }
}