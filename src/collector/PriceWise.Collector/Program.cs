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
        try
        {
            try
            {
                Console.OutputEncoding = System.Text.Encoding.UTF8;
            }
            catch
            {
                // Ignore console encoding setup failure on headless CI runners
            }

            return await RunAsync(args);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[FATAL CRASH] Unhandled exception in PriceWise.Collector: {ex.GetType().Name}: {ex.Message}");
            Console.Error.WriteLine(ex.StackTrace);
            return 1;
        }
    }

    private static async Task<int> RunAsync(string[] args)
    {
        // Setup Dependency Injection & Logging
        var serviceCollection = new ServiceCollection();
        var configResult = ConfigureServices(serviceCollection);
        if (configResult != 0)
        {
            return configResult;
        }

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

            var superEntity = supermarkets.FirstOrDefault(s => string.Equals(s.Slug, collector.SupermarketSlug, StringComparison.OrdinalIgnoreCase));
            if (superEntity == null)
            {
                logger.LogError("[FATAL DATABASE ERROR] Supermarket '{Slug}' not found in database catalog! Cannot associate collector run or products without a valid database foreign key.", collector.SupermarketSlug);
                overallExitCode = 1;
                continue;
            }

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

            try
            {
                await storage.SaveCollectorRunAsync(run);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to persist collector run audit record for {Name}: {Message}", collector.SupermarketName, ex.Message);
                overallExitCode = 1;
            }

            if (result.Success && result.Products.Count > 0)
            {
                try
                {
                    await storage.SaveCollectedProductsAsync(superEntity, result.Products);
                    logger.LogInformation("Successfully persisted {Count} verified observations for {Name}.", result.Products.Count, collector.SupermarketName);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "FATAL: Database persistence failed for {Name}: {Message}", collector.SupermarketName, ex.Message);
                    overallExitCode = 1;
                }
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

    private static int ConfigureServices(IServiceCollection services)
    {
        services.AddLogging(builder =>
        {
            builder.AddConsole();
            builder.SetMinimumLevel(LogLevel.Information);
        });

        services.AddHttpClient();
        services.AddSingleton<ProductMatcher>();

        // Storage service configuration
        var isGitHubActions = string.Equals(Environment.GetEnvironmentVariable("GITHUB_ACTIONS"), "true", StringComparison.OrdinalIgnoreCase);
        var rawStorageMode = Environment.GetEnvironmentVariable("COLLECTOR_STORAGE_MODE");
        
        var supabaseUrl = Environment.GetEnvironmentVariable("SUPABASE_URL") 
            ?? Environment.GetEnvironmentVariable("VITE_SUPABASE_URL");
        var supabaseKey = Environment.GetEnvironmentVariable("SUPABASE_SERVICE_ROLE_KEY");

        // Normalize URL and trim keys
        if (!string.IsNullOrWhiteSpace(supabaseUrl))
        {
            supabaseUrl = supabaseUrl.Trim().Trim('"', '\'');
        }

        if (!string.IsNullOrWhiteSpace(supabaseKey))
        {
            supabaseKey = supabaseKey.Trim().Trim('"', '\'');
        }

        // Determine intended storage mode:
        // In GitHub Actions, production mode ALWAYS targets Supabase unless explicitly set to local.
        var targetMode = (rawStorageMode ?? (isGitHubActions ? "supabase" : "local")).Trim().ToLowerInvariant();

        if (targetMode == "supabase" || isGitHubActions)
        {
            // Strict Production Validation: NEVER silently fall back to local storage in production/CI!
            if (string.IsNullOrWhiteSpace(supabaseUrl) || supabaseUrl.Contains("your-project", StringComparison.OrdinalIgnoreCase))
            {
                Console.Error.WriteLine("================================================================================");
                Console.Error.WriteLine("[FATAL CONFIGURATION ERROR] Supabase storage is active, but SUPABASE_URL is missing or unconfigured!");
                Console.Error.WriteLine("Expected: https://<project-ref>.supabase.co");
                Console.Error.WriteLine("Please configure the 'SUPABASE_URL' (or 'VITE_SUPABASE_URL') secret in GitHub Repository Settings -> Secrets and variables -> Actions.");
                Console.Error.WriteLine("================================================================================");
                return 1;
            }

            if (supabaseUrl.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase) ||
                supabaseUrl.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase) ||
                supabaseUrl.Contains("Host=", StringComparison.OrdinalIgnoreCase))
            {
                Console.Error.WriteLine("================================================================================");
                Console.Error.WriteLine("[FATAL CONFIGURATION ERROR] SUPABASE_URL was provided as a PostgreSQL connection string instead of an HTTPS Project URL!");
                Console.Error.WriteLine($"Received: {MaskConnectionString(supabaseUrl)}");
                Console.Error.WriteLine("PriceWise LK communicates with Supabase via its HTTPS REST API (Port 443).");
                Console.Error.WriteLine("Please configure SUPABASE_URL with your Project URL: https://<project-ref>.supabase.co");
                Console.Error.WriteLine("(Found in Supabase Dashboard -> Project Settings -> API)");
                Console.Error.WriteLine("================================================================================");
                return 1;
            }

            if (!supabaseUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase) && 
                !supabaseUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                supabaseUrl = "https://" + supabaseUrl;
            }

            if (string.IsNullOrWhiteSpace(supabaseKey) || supabaseKey.Contains("your-supabase", StringComparison.OrdinalIgnoreCase))
            {
                Console.Error.WriteLine("================================================================================");
                Console.Error.WriteLine("[FATAL CONFIGURATION ERROR] SUPABASE_SERVICE_ROLE_KEY is missing or unconfigured!");
                Console.Error.WriteLine("The collector requires your Supabase service_role secret key (sb_secret_... or legacy JWT) to persist verified prices.");
                Console.Error.WriteLine("Please configure the 'SUPABASE_SERVICE_ROLE_KEY' secret in GitHub Repository Settings -> Secrets and variables -> Actions.");
                Console.Error.WriteLine("================================================================================");
                return 1;
            }

            if (supabaseKey.StartsWith("sb_publishable_", StringComparison.OrdinalIgnoreCase))
            {
                Console.Error.WriteLine("================================================================================");
                Console.Error.WriteLine("[FATAL CONFIGURATION ERROR] SUPABASE_SERVICE_ROLE_KEY is set to a public anon key ('sb_publishable_...')!");
                Console.Error.WriteLine("The collector requires your privileged service_role secret key (starting with 'sb_secret_...' or legacy JWT 'eyJhbGciOi...').");
                Console.Error.WriteLine("Public publishable keys do not have permission to insert or update supermarket data under Row Level Security.");
                Console.Error.WriteLine("Please copy the secret key from Supabase Dashboard -> Project Settings -> API and update your 'SUPABASE_SERVICE_ROLE_KEY' secret.");
                Console.Error.WriteLine("================================================================================");
                return 1;
            }

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
            // Local file storage (allowed ONLY in local development)
            services.AddSingleton<IStorageService, LocalStorageService>(sp =>
            {
                var matcher = sp.GetRequiredService<ProductMatcher>();
                var logger = sp.GetRequiredService<ILogger<LocalStorageService>>();

                var baseDir = AppContext.BaseDirectory;
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

        return 0;
    }

    internal static string MaskConnectionString(string url)
    {
        if (string.IsNullOrWhiteSpace(url)) return string.Empty;
        try
        {
            if (url.Contains("://"))
            {
                var atIdx = url.IndexOf('@');
                var colonSlash = url.IndexOf("://");
                if (atIdx > 0 && colonSlash > 0 && atIdx > colonSlash)
                {
                    var scheme = url.Substring(0, colonSlash + 3);
                    var userPass = url.Substring(colonSlash + 3, atIdx - (colonSlash + 3));
                    var colonIdx = userPass.IndexOf(':');
                    var user = colonIdx > 0 ? userPass.Substring(0, colonIdx) : "user";
                    var hostAndPath = url.Substring(atIdx);
                    return $"{scheme}{user}:***{hostAndPath}";
                }
                return url;
            }
            if (url.Contains("Password=", StringComparison.OrdinalIgnoreCase))
            {
                return System.Text.RegularExpressions.Regex.Replace(
                    url,
                    @"(Password=)[^;]+",
                    "$1***",
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            }
        }
        catch { }
        return url;
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
