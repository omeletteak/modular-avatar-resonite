// See https://aka.ms/new-console-template for more information

using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Remoting;
using Assimp.Unmanaged;
using nadena.dev.resonity.remote.puppeteer.rpc;

[assembly: InternalsVisibleTo("Launcher")]

namespace nadena.dev.resonity.remote.puppeteer;

using Elements.Core;
using FrooxEngine;

internal class Program
{
    internal static async Task Main(string[] args)
    {
        throw new Exception();
    }
    
    // ReSharper disable once UnusedMember.Global
    internal static async Task Launch(string resoDirectory) {
        UniLog.OnLog += l => System.Console.WriteLine("[LOG] " + l);
        UniLog.OnError += l => System.Console.WriteLine("[ERROR] " + l);
        UniLog.OnWarning += l => System.Console.WriteLine("[WARN] " + l);

        InitAssimp(resoDirectory);

        Assembly.Load("ProtoFlux.Nodes.FrooxEngine");
        Assembly.Load("ProtoFluxBindings");

        TaskCompletionSource shutdown = new();

        AutoResetEvent requestFrame = new(false);
        StandaloneSystemInfo info = new StandaloneSystemInfo();
        LaunchOptions options = new LaunchOptions();
        options.DataDirectory = StandaloneFrooxEngineRunner.DefaultDataDirectory;
        options.CacheDirectory = StandaloneFrooxEngineRunner.DefaultCacheDirectory;
        var engine = new Engine();

        bool shutdownRequested = false;
        
        engine.OnShutdownRequest += _ => shutdownRequested = true;
        engine.OnShutdown += () => shutdown.TrySetResult();
        engine.EnvironmentCrashCallback = () => Process.GetCurrentProcess().Kill();
        engine.EnvironmentShutdownCallback = () => Process.GetCurrentProcess().Kill();
        
        await engine.Initialize(resoDirectory, options, info, null, new ConsoleEngineInitProgress()).ConfigureAwait(false);

        World world = Userspace.SetupUserspace(engine);

        var updateLoop = new Thread(() =>
        {
            while (!shutdownRequested)
            {
                engine.RunUpdateLoop();
                info.FrameFinished();
                requestFrame.WaitOne(10);
            }
        });
        
        updateLoop.Name = "Update loop";
        updateLoop.Start();
        
        Console.WriteLine("==== Startup complete ====");

        world.Coroutines.Post(_x =>
        {
            Console.WriteLine("Slot test coroutine start");
            // Try to create a slot and attach a component
            var slot = world.RootSlot.AddSlot();
            var component = slot.AttachComponent<MeshRenderer>();

            foreach (var c in slot.Components)
            {
                Console.WriteLine("Component: " + c.GetType());
            }
            
            Console.WriteLine("Slot test coroutine end");
            
            //engine.RequestShutdown();
        }, world);
        
        new RPCServer().Start(new EntryPoint(engine, world, requestFrame));
        
        await shutdown.Task;
        
        engine.Dispose();
    }

    private static void InitAssimp(string resoDirectory)
    {
        AssimpLibrary.Instance.LoadLibrary(null, resoDirectory + "\\assimp.dll");
    }
}