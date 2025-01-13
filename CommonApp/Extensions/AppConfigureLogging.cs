

using Elastic.CommonSchema.Serilog;
using Serilog;
using Serilog.Debugging;
using Serilog.Events;
using Serilog.Sinks.Elasticsearch;

namespace CommonApp.Extensions;

public static class AppConfigureLogging
{
    public static void ConfigureLogging(string applicationName, string tenant)
    {
        var env = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production";
        var configuration = new ConfigurationBuilder()
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .AddJsonFile($"appsettings.{env}.json", optional: true, reloadOnChange: true)
            .AddEnvironmentVariables()
            .Build();
        
        //ElasticSearch configuration
        var elasticUri = configuration.GetValue<string>("ElasticSearch:Uri");
        var elasticToken = configuration.GetValue<string>("ElasticSearch:Token")?.Replace("Authorization=", "");
        var elasticEnv = configuration.GetValue<string>("ElasticSearch:Environment");
        var logsIndexName = $"sa-logs-{elasticEnv}";
        var isElasticDisabled = configuration.GetValue<bool>("ElasticSearch:Disabled");

        //Contexts to exclude from logging 
        var excludeContexts = new HashSet<string>
        {
            "Microsoft.AspNetCore.Hosting",
            "Microsoft.AspNetCore.Mvc",
            "Microsoft.AspNetCore.Diagnostics",
            "Microsoft.AspNetCore.StaticFiles"
        };

        var loggerConfig = new LoggerConfiguration()
            .Enrich.WithProperty("ApplicationName", applicationName)
            .Enrich.WithProperty("Tenant", tenant);

        loggerConfig.WriteTo.Conditional(
            logEvent => env == "Development" ||
            logEvent.Level >= LogEventLevel.Information,
            writeTo => writeTo.Console(new EcsTextFormatter()));

        if (!isElasticDisabled && !string.IsNullOrEmpty(elasticUri))
        {
            loggerConfig.WriteTo.Conditional(
                logEvent => logEvent.Level >= LogEventLevel.Information, // Condition to filter log events
                wt => wt.Elasticsearch(new ElasticsearchSinkOptions(new Uri("http://localhost:9200"))
                {
                    AutoRegisterTemplate = true,
                    IndexFormat = $"sa-logs-{elasticEnv}-{DateTime.UtcNow:yyyy-MM}",
                    AutoRegisterTemplateVersion = AutoRegisterTemplateVersion.ESv8,
                    ConnectionTimeout = TimeSpan.FromMinutes(10),
                    CustomFormatter = new EcsTextFormatter(),
                    BatchPostingLimit = 50,
                    Period = TimeSpan.FromSeconds(10),
                    EmitEventFailure = EmitEventFailureHandling.WriteToSelfLog,
                    NumberOfShards = 2,
                    NumberOfReplicas = 1
                })
            );
        }

        Log.Logger = loggerConfig.CreateLogger();
        SelfLog.Enable(Console.Error);
    }
}