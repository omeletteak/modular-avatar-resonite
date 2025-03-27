using System;
using System.Security.Principal;
using GrpcDotNetNamedPipes;
using nadena.dev.ndmf.proto.rpc;

namespace ResoPuppetSchema
{
    public class Connector
    {
        private NamedPipeChannel _channel;
        private ResoPuppeteer.ResoPuppeteerClient _client;

        public ResoPuppeteer.ResoPuppeteerClient Client => _client;
        
        public Connector()
        {
            _channel = new NamedPipeChannel(".", "TEST_PIPE_PUPPETEER", new NamedPipeChannelOptions()
            {
                ImpersonationLevel = TokenImpersonationLevel.None
            });
            _client = new ResoPuppeteer.ResoPuppeteerClient(_channel);
        }
    }
}