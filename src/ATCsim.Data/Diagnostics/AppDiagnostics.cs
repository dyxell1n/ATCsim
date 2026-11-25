using System.Diagnostics;
using System.Security;
using ATCsim.Data.Context;
using Microsoft.Data.Sqlite;
using Serilog;
using Serilog.Debugging;
using Serilog.Formatting.Json;

namespace ATCsim.Data.Diagnostics;

public static class AppDiagnostics
{
    public static void Initialize()
    {
        SelfLog.Enable(_ => Trace.TraceWarning("The ATCsim diagnostic log sink reported an error."));
        try
        {
            var directory = Path.Combine(DatabaseContext.LocalDataDirectory, "Logs");
            Directory.CreateDirectory(directory);
            Log.Logger = new LoggerConfiguration()
                .WriteTo.File(new JsonFormatter(renderMessage: true), Path.Combine(directory, "atcsim-.jsonl"),
                    rollingInterval: RollingInterval.Day, rollOnFileSizeLimit: true, fileSizeLimitBytes: 5 * 1024 * 1024,
                    retainedFileCountLimit: 14, shared: true)
                .CreateLogger();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or SecurityException)
        {
            Trace.TraceWarning("ATCsim could not initialize the diagnostic log ({0}).", exception.GetType().Name);
        }
    }

    public static void Close()
    {
        Log.CloseAndFlush();
        SelfLog.Disable();
    }

    public static void Information(string messageTemplate, params object?[] propertyValues) =>
        Log.Information(messageTemplate, propertyValues);

    public static void Warning(string messageTemplate, params object?[] propertyValues) =>
        Log.Warning(messageTemplate, propertyValues);

    public static void Warning(Exception exception, string messageTemplate, params object?[] propertyValues) =>
        WithExceptionDetails(exception).Warning(messageTemplate, propertyValues);

    public static void Error(Exception exception, string messageTemplate, params object?[] propertyValues) =>
        WithExceptionDetails(exception).Error(messageTemplate, propertyValues);

    private static ILogger WithExceptionDetails(Exception exception)
    {
        // Exception messages and source paths can contain user data.
        var cause = exception.GetBaseException();
        var logger = Log.ForContext("ExceptionType", exception.GetType().FullName)
            .ForContext("CauseType", cause.GetType().FullName)
            .ForContext("CauseHResult", cause.HResult)
            .ForContext("CauseStackTrace", new StackTrace(cause, fNeedFileInfo: false).ToString());

        if (cause is SqliteException sqlite)
        {
            logger = logger.ForContext("SqliteErrorCode", sqlite.SqliteErrorCode)
                .ForContext("SqliteExtendedErrorCode", sqlite.SqliteExtendedErrorCode);
        }

        return logger;
    }
}
