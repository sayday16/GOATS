using Serilog;
using Serilog.Events;

namespace GoatStore.API.Server.Logging
{
    public static class LoggingSetup
    {
        public const string OutputTemplate =
            "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {SourceContext}: {Message:lj}{NewLine}{Exception}";

        public static WebApplicationBuilder AddGoatLogging(this WebApplicationBuilder builder)
        {
            builder.Services.AddSingleton<AuditLog>();

            builder.Services.AddSerilog((_, configuration) =>
            {
                configuration
                    .MinimumLevel.Information()
                    .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
                    .MinimumLevel.Override("Microsoft.Hosting.Lifetime", LogEventLevel.Information)
                    .MinimumLevel.Override("System", LogEventLevel.Warning)
                    .Enrich.FromLogContext();

                if (!builder.Environment.IsEnvironment("Testing"))
                {
                    configuration.WriteTo.File(
                        Path.Combine(builder.Environment.ContentRootPath, "logs", "api-.log"),
                        rollingInterval: RollingInterval.Day,
                        retainedFileCountLimit: 14,
                        outputTemplate: OutputTemplate);
                }
            }, preserveStaticLogger: true, writeToProviders: true);

            return builder;
        }

        public static WebApplication UseGoatRequestLogging(this WebApplication app)
        {
            app.UseSerilogRequestLogging(options => options.Logger = app.Services.GetRequiredService<Serilog.ILogger>());
            return app;
        }
    }
}
