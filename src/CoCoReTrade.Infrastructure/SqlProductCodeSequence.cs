using System.Data;
using CoCoReTrade.Application;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace CoCoReTrade.Infrastructure;

public sealed class SqlProductCodeSequence(AppDbContext db) : IProductCodeSequence
{
    public async Task<long> NextAsync(CancellationToken ct)
    {
        var connection = db.Database.GetDbConnection();
        var close = connection.State != ConnectionState.Open;
        if (close) await connection.OpenAsync(ct);
        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT NEXT VALUE FOR dbo.ProductSkuSequence";
            if (db.Database.CurrentTransaction is { } transaction)
                command.Transaction = transaction.GetDbTransaction();
            return Convert.ToInt64(await command.ExecuteScalarAsync(ct));
        }
        finally { if (close) await connection.CloseAsync(); }
    }
}
