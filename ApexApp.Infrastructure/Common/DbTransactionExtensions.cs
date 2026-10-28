using Microsoft.EntityFrameworkCore.Storage;

namespace ApexApp.Infrastructure.Common;

/// <summary>
/// Безопасное завершение транзакции БД: ошибка отката не должна подменять исходный
/// результат операции, например конфликт конкурентного доступа, уже полученный клиентом.
/// </summary>
public static class DbTransactionExtensions
{
    /// <summary>
    /// Откатывает транзакцию, подавляя ошибки отката: при разрыве соединения или уже
    /// прерванной сервером транзакции клиент должен получить исходный результат,
    /// а не исключение из отката.
    /// </summary>
    /// <param name="transaction">Транзакция БД, которая может быть уже завершена.</param>
    /// <returns>Задача отката транзакции.</returns>
    public static async Task RollbackQuietlyAsync(this IDbContextTransaction? transaction)
    {
        if (transaction is null)
        {
            return;
        }

        try
        {
            await transaction.RollbackAsync(CancellationToken.None);
        }
        catch (Exception)
        {
            // Откат уже прерванной транзакции не влияет на результат операции.
        }
    }
}
