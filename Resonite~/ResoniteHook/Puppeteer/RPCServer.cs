using GrpcDotNetNamedPipes;

namespace nadena.dev.resonity.remote.puppeteer;

public class RPCServer
{
    private readonly string pipeName;
    
    public RPCServer(string pipeName)
    {
        this.pipeName = pipeName;
    }
    
    public void Start(nadena.dev.ndmf.proto.rpc.ResoPuppeteer.ResoPuppeteerBase entryPoint)
    {
        var server = new NamedPipeServer(pipeName, new() {
            CurrentUserOnly = true,
        });

        nadena.dev.ndmf.proto.rpc.ResoPuppeteer.BindService(server.ServiceBinder, entryPoint);
        
        server.Start();
    }
}