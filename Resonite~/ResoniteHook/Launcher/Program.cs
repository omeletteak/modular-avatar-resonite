// See https://aka.ms/new-console-template for more information

namespace nadena.dev.resonity.remote.bootstrap;

internal class Program
{

    [STAThread]
    private static async Task Main(string[] args)
    {
        await new Launcher().Launch();
    }
}