using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PriceWise.Collector.Adapters;
using PriceWise.Collector.Matching;
using PriceWise.Collector.Models;
using PriceWise.Collector.Storage;

namespace PriceWise.Collector;

public class Program
{
    public static async Task<int> Main(string[] args)
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;

        // Setup Dependency Injection & Logging
        var serviceCollection = new ServiceCollection();
        ConfigureServices(serviceCollection);

        using var serviceProvider = serviceCollection.BuildServiceProvider();
        var logger = serviceProvider.GetRequiredService<ILogger<Program>>();

        logger.LogInformation("=================================================");
        logger.LogInformation("PriceWise LK — Supermarket Price Collector v1.0");
        logger.LogInformation("Strict Real Data Mode: ACTIVE");
        logger.LogInformation("=================================================");

        // Parse arguments
        var targetSlug = GetArgumentValue(args, "--supermarket", Environment.GetEnvironmentVariable("TARGET_SUPERMARKET") ?? "all");
        var rawTerms = GetArgumentValue(args, "--terms", Environment.GetEnvironmentVariable("SEARCH_TERMS") ?? "milk,tea,rice,dhal,sugar,biscuit");
        var searchTerms = rawTerms.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        var storage = serviceProvider.GetRequiredService<IStorageService>();
        var collectors = serviceProvider.GetServices<ISupermarketCollector>().ToList();

        var selectedCollectors = string.Equals(targetSlug, "all", StringComparison.OrdinalIgnoreCase)
            ? collectors
            : collectors.Where(c => string.Equals(c.SupermarketSlug, targetSlug, StringComparison.OrdinalIgnoreCase)).ToList();

        if (selectedCollectors.Count == 0)
        {
            logger.LogError("No matching supermarket collector found for slug '{Slug}'. Available slugs: {Available}",
                targetSlug, string.Join(", ", collectors.Select(c => c.SupermarketSlug)));
            return 1;
        }

        var supermarkets = await storage.GetSupermarketsAsync();
        int overallExitCode = 0;

        foreach (var collector in selectedCollectors)
        {
            logger.LogInformation("-------------------------------------------------");
            logger.LogInformation("Running collector for {Name} ({Slug})...", collector.SupermarketName, collector.SupermarketSlug);

            var superEntity = supermarkets.FirstOrDefault(s => string.Equals(s.Slug, collector.SupermarketSlug, StringComparison.OrdinalIgnoreCase))
                ?? new Supermarket
                {
                    Name = collector.SupermarketName,
                    Slug = collector.SupermarketSlug,
                    WebsiteUrl = collector.WebsiteUrl,
                    IntegrationStatus = collector.IntegrationStatus,
                    StatusReason = collector.StatusReason
                };

            var run = new CollectorRun
            {
                Id = Guid.NewGuid(),
                SupermarketId = superEntity.Id,
                StartedAt = DateTime.UtcNow,
                Status = "running"
            };

            var result = await collector.CollectProductsAsync(searchTerms);

            run.FinishedAt = DateTime.UtcNow;
            run.Status = result.Status;
            run.ProductsChecked = result.ProductsChecked;
            run.ProductsUpdated = result.ProductsUpdated;
            run.ErrorMessage = result.ErrorMessage;

            await storage.SaveCollectorRunAsync(run);

            if (result.Success && result.Products.Count > 0)
            {
                await storage.SaveCollectedProductsAsync(superEntity, result.Products);
                logger.LogInformation("Successfully persisted {Count} verified observations for {Name}.", result.Products.Count, collector.SupermarketName);
            }
            else if (result.Status == "blocked")
            {
                logger.LogWarning("Collector for {Name} reported status BLOCKED: {Error}", collector.SupermarketName, result.ErrorMessage);
            }
            else
            {
                logger.LogError("Collector for {Name} failed: {Error}", collector.SupermarketName, result.ErrorMessage);
                overallExitCode = 1;
            }
        }

        logger.LogInformation("=================================================");
        logger.LogInformation("PriceWise LK Collector run completed.");
        logger.LogInformation("=================================================");

        return overallExitCode;
    }

    private static void ConfigureServices(IServiceCollection services)
    {
        services.AddLogging(builder =>
        {
            builder.AddConsole();
            builder.SetMinimumLevel(LogLevel.Information);
        });

        services.AddHttpClient();
        services.AddSingleton<ProductMatcher>();

        // Storage service configuration
        var supabaseUrl = Environment.GetEnvironmentVariable("SUPABASE_URL") 
            ?? Environment.GetEnvironmentVariable("VITE_SUPABASE_URL");
        var supabaseKey = Environment.GetEnvironmentVariable("SUPABASE_SERVICE_ROLE_KEY");

        if (!string.IsNullOrWhiteSpace(supabaseUrl))
        {
            supabaseUrl = supabaseUrl.Trim().Trim('"', '\'');

            if (supabaseUrl.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase) ||
                supabaseUrl.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase) ||
                supabaseUrl.Contains("Host=", StringComparison.OrdinalIgnoreCase))
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("[Configuration Warning] SUPABASE_URL was provided as a PostgreSQL connection string instead of an HTTPS Project URL.");
                Console.WriteLine("PriceWise LK communicates with Supabase via its HTTPS REST API (Port 443).");
                Console.WriteLine("Please use your Supabase Project URL (e.g. 'https://<project-ref>.supabase.co') found in Supabase Dashboard -> Project Settings -> API.");
                Console.ResetColor();
                supabaseUrl = null;
            }
            else if (supabaseUrl.Contains("your-project", StringComparison.OrdinalIgnoreCase))
            {
                supabaseUrl = null;
            }
            else if (!supabaseUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase) && 
                     !supabaseUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                supabaseUrl = "https://" + supabaseUrl;
            }
        }

        var storageMode = Environment.GetEnvironmentVariable("COLLECTOR_STORAGE_MODE") 
            ?? (string.IsNullOrEmpty(supabaseUrl) ? "local" : "supabase");

        if (storageMode.Equals("supabase", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(supabaseUrl) && !string.IsNullOrWhiteSpace(supabaseKey))
        {
            services.AddSingleton<IStorageService, SupabaseStorageService>(sp =>
            {
                var client = sp.GetRequiredService<IHttpClientFactory>().CreateClient();
                var matcher = sp.GetRequiredService<ProductMatcher>();
                var logger = sp.GetRequiredService<ILogger<SupabaseStorageService>>();
                return new SupabaseStorageService(client, supabaseUrl, supabaseKey, matcher, logger);
            });
        }
        else
        {
            services.AddSingleton<IStorageService, LocalStorageService>(sp =>
            {
                var matcher = sp.GetRequiredService<ProductMatcher>();
                var logger = sp.GetRequiredService<ILogger<LocalStorageService>>();

                // Locate public/data directory in frontend
                var baseDir = AppContext.BaseDirectory;
                // Try repository root or relative path
                var candidatePaths = new[]
                {
                    Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "src/frontend/public/data")),
                    Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "../../src/frontend/public/data")),
                    Path.GetFullPath(Path.Combine(baseDir, "../../../../../src/frontend/public/data")),
                    Path.GetFullPath(Path.Combine(baseDir, "data"))
                };

                var publicDataDir = candidatePaths.FirstOrDefault(p => Directory.Exists(Path.GetDirectoryName(p))) ?? candidatePaths.Last();
                Directory.CreateDirectory(publicDataDir);

                return new LocalStorageService(publicDataDir, matcher, logger);
            });
        }

        // Register Supermarket Collectors
        services.AddTransient<ISupermarketCollector, CargillsCollector>(sp =>
        {
            var client = sp.GetRequiredService<IHttpClientFactory>().CreateClient();
            client.Timeout = TimeSpan.FromSeconds(25);
            var logger = sp.GetRequiredService<ILogger<CargillsCollector>>();
            return new CargillsCollector(client, logger);
        });

        services.AddTransient<ISupermarketCollector, ArpicoCollector>(sp =>
        {
            var client = sp.GetRequiredService<IHttpClientFactory>().CreateClient();
            client.Timeout = TimeSpan.FromSeconds(25);
            var logger = sp.GetRequiredService<ILogger<ArpicoCollector>>();
            return new ArpicoCollector(client, logger);
        });

        services.AddTransient<ISupermarketCollector, KeellsCollector>(sp =>
        {
            var client = sp.GetRequiredService<IHttpClientFactory>().CreateClient();
            client.Timeout = TimeSpan.FromSeconds(20);
            var logger = sp.GetRequiredService<ILogger<KeellsCollector>>();
            return new KeellsCollector(client, logger);
        });
    }

    private static string GetArgumentValue(string[] args, string flag, string defaultValue)
    {
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (string.Equals(args[i], flag, StringComparison.OrdinalIgnoreCase))
                return args[i + 1];
        }
        return defaultValue;
    }
}
