using Microsoft.Extensions.Hosting;

namespace AIHub.DemoSeed;

// Açık sınıf: üst düzey ifadeler public bir Program üretir ve testlerde Api'nin Program'ıyla çakışırdı.
internal static class Program
{
    /// <summary>
    /// `dotnet run --project tools/AIHub.DemoSeed` launchSettings ile Development'ta çalışır. Ortam belirtilmezse
    /// .NET varsayılanı Production'dır ve araç hiçbir kayıt yazmadan reddeder (Spec 0006, R-10, AC-11).
    /// </summary>
    private static async Task<int> Main(string[] args)
    {
        var builder = Host.CreateApplicationBuilder(args);
        return await DemoSeeder.RunAsync(builder.Environment.EnvironmentName, builder.Configuration, Console.Out, CancellationToken.None);
    }
}
