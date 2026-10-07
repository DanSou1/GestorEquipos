using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Gestor_Equipos.Services.Diagnostics;

public sealed class QueryTimingInterceptor(ILogger<QueryTimingInterceptor> logger) : DbCommandInterceptor
{
    private void Record(CommandExecutedEventData data)
    {
        if (data.Duration.TotalMilliseconds >= 1000)
            logger.LogWarning("Slow database command {CommandId}: {ElapsedMs} ms", data.CommandId, data.Duration.TotalMilliseconds);
        else
            logger.LogDebug("Database command {CommandId}: {ElapsedMs} ms", data.CommandId, data.Duration.TotalMilliseconds);
    }

    public override DbDataReader ReaderExecuted(DbCommand command, CommandExecutedEventData eventData, DbDataReader result)
    { Record(eventData); return result; }

    public override ValueTask<DbDataReader> ReaderExecutedAsync(DbCommand command, CommandExecutedEventData eventData,
        DbDataReader result, CancellationToken cancellationToken = default)
    { Record(eventData); return ValueTask.FromResult(result); }

    public override int NonQueryExecuted(DbCommand command, CommandExecutedEventData eventData, int result)
    { Record(eventData); return result; }

    public override ValueTask<int> NonQueryExecutedAsync(DbCommand command, CommandExecutedEventData eventData,
        int result, CancellationToken cancellationToken = default)
    { Record(eventData); return ValueTask.FromResult(result); }

    public override object? ScalarExecuted(DbCommand command, CommandExecutedEventData eventData, object? result)
    { Record(eventData); return result; }

    public override ValueTask<object?> ScalarExecutedAsync(DbCommand command, CommandExecutedEventData eventData,
        object? result, CancellationToken cancellationToken = default)
    { Record(eventData); return ValueTask.FromResult(result); }

    public override void CommandFailed(DbCommand command, CommandErrorEventData eventData) => RecordFailure(eventData);

    public override Task CommandFailedAsync(DbCommand command, CommandErrorEventData eventData, CancellationToken cancellationToken = default)
    { RecordFailure(eventData); return Task.CompletedTask; }

    private void RecordFailure(CommandErrorEventData data) =>
        logger.LogWarning("Database command {CommandId} failed after {ElapsedMs} ms ({ErrorType})",
            data.CommandId, data.Duration.TotalMilliseconds, data.Exception.GetType().Name);
}
