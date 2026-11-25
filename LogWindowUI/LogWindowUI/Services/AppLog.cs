using System.IO;
using Serilog;

namespace LogWindowUI.Services
{
    public static class AppLog
    {
        public const string OutputTemplate =
            "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}";

        public static string LogDirectory { get; } = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "GoatStore", "logs");

        public static string UnexpectedErrorMessage =>
            $"Сталася неочікувана помилка. Програма продовжить роботу, а деталі записано в журнал:{Environment.NewLine}{LogDirectory}";

        public static void Start(string apiUrl)
        {
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Information()
                .WriteTo.File(
                    Path.Combine(LogDirectory, "wpf-.log"),
                    rollingInterval: RollingInterval.Day,
                    retainedFileCountLimit: 14,
                    outputTemplate: OutputTemplate)
                .CreateLogger();

            Log.Information("GoatStore WPF started, API {ApiUrl}", apiUrl);
        }

        public static void Stop()
        {
            Log.Information("GoatStore WPF stopped");
            Log.CloseAndFlush();
        }
    }
}
