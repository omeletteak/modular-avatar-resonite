using System;
using System.Security.Principal;
using GrpcDotNetNamedPipes;

namespace ResoPuppetSchema
{
    public class Connector
    {
        private NamedPipeChannel _channel;
        private ResoPuppet.ResoPuppetClient _client;

        public ResoPuppet.ResoPuppetClient Client => _client;
        
        public Connector()
        {
            _channel = new NamedPipeChannel(".", "TEST_PIPE_PUPPETEER", new NamedPipeChannelOptions()
            {
                ImpersonationLevel = TokenImpersonationLevel.None
            });
            _client = new ResoPuppet.ResoPuppetClient(_channel);
        }
    }
}