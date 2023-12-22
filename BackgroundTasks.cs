using System.Collections.Immutable;
using Microsoft.Azure.Cosmos;
using zinfandel_movie_club.Config;
using zinfandel_movie_club.Data;
using zinfandel_movie_club.Data.Models;

namespace zinfandel_movie_club;

public class BackgroundTasks : BackgroundService
{
    private readonly IServiceProvider _sp;
    public BackgroundTasks(IServiceProvider sp)
    {
        _sp = sp;
    }

    private static async Task InitializeDocumentManagers(IServiceProvider sp, CancellationToken stoppingToken)
    {
        var assembly = System.Reflection.Assembly.GetExecutingAssembly();
        var types = assembly.DefinedTypes.ToImmutableList();
        
        var cosmosDocumentType = typeof(CosmosDocument);
        var cosmosDocumentTypes = types.Where(t => t.IsSubclassOf(cosmosDocumentType)).ToImmutableList();

        var cosmosDocumentManagerInterface = typeof(ICosmosDocumentManager<>);

        foreach (var documentType in cosmosDocumentTypes)
        {
            if (stoppingToken.IsCancellationRequested) break;

            var documentManagerType = cosmosDocumentManagerInterface.MakeGenericType(new Type[] { documentType });
            
            var manager = sp.GetService(documentManagerType);
            if (manager == null) continue;

            var initializeMethod = documentManagerType.GetMethod("InitializeContainer");
            if (initializeMethod == null) continue;

            Task<Container>? initTask = null;
            try
            {
                initTask = initializeMethod.Invoke(manager, new object[] { stoppingToken }) as Task<Container>;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Could not start initialization of {documentManagerType.Name}");
                Console.WriteLine(ex.GetType().Name);
                Console.WriteLine(ex.Message);
                Console.WriteLine(ex.StackTrace);
                continue;
            }
            
            if (initTask == null) continue;
            try
            {
                var container = await initTask;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Could not await initialization of {documentManagerType.Name}");
                Console.WriteLine(ex.GetType().Name);
                Console.WriteLine(ex.Message);
                Console.WriteLine(ex.StackTrace);
                continue;
            }
        }
    }
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var scope = _sp.CreateScope();
        var sp = scope.ServiceProvider;

        var config = sp.GetRequiredService<CosmosConfig>();
        
        await InitializeDocumentManagers(sp, stoppingToken);
    }
}