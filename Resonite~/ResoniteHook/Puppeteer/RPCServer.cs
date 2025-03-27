using GrpcDotNetNamedPipes;

namespace nadena.dev.resonity.remote.puppeteer;

public class RPCServer
{
    public void Start(nadena.dev.ndmf.proto.rpc.ResoPuppeteer.ResoPuppeteerBase entryPoint)
    {
        var server = new NamedPipeServer("TEST_PIPE_PUPPETEER", new() {
            CurrentUserOnly = true,
        });

        nadena.dev.ndmf.proto.rpc.ResoPuppeteer.BindService(server.ServiceBinder, entryPoint);
        
        server.Start();
    }
}