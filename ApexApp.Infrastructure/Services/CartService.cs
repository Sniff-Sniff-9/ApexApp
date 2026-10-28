using ApexApp.Domain.Entities;
using ApexApp.Infrastructure.Common;
using ApexApp.Infrastructure.Data;
using ApexApp.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;

namespace ApexApp.Infrastructure.Services;

public sealed class CartService(
    CartRepository cartRepository,
    AppDbContext dbContext) : ICartService
{
    public Task<List<CartItem>> GetAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        return cartRepository.GetByUserIdAsync(
            userId, cancellationToken);
    }

    public async Task<(bool Success, string? Error)> AddAsync(
        Guid userId,
        int productId,
        int quantity,
        CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty || productId <= 0 || quantity <= 0)
            return (false, "Invalid user, product, or quantity.");

        // Одно чтение отдаёт и состояние товара, и текущее количество в корзине,
        // поэтому отдельный запрос позиции не нужен и при первом добавлении.
        var snapshot = await dbContext.Set<Product>()
            .AsNoTracking()
            .Where(p => p.Id == productId)
            .Select(p => new
            {
                p.IsActive,
                p.IsDigital,
                p.Stock,
                CartLine = dbContext.Set<CartItem>()
                    .Where(i => i.UserId == userId && i.ProductId == p.Id)
                    .Select(i => new { i.Id, i.Quantity })
                    .FirstOrDefault()
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (snapshot is null || !snapshot.IsActive || snapshot.IsDigital)
            return (false, "Product is unavailable.");

        var cartLine = snapshot.CartLine;

        if (cartLine is null)
        {
            if (snapshot.Stock.HasValue && quantity > snapshot.Stock.Value)
                return (false, "Requested quantity exceeds available stock.");

            dbContext.Set<CartItem>().Add(new CartItem
            {
                UserId = userId,
                ProductId = productId,
                Quantity = quantity,
                AddedAt = DateTime.UtcNow
            });

            try
            {
                await dbContext.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException exception) when (DatabaseErrors.IsUniqueViolation(exception))
            {
                // Параллельный запрос уже добавил этот товар в корзину:
                // повторяем операцию как обновление количества вместо ошибки 500.
                dbContext.ChangeTracker.Clear();

                return await AddAsync(userId, productId, quantity, cancellationToken);
            }

            return (true, null);
        }

        var newQuantity = (long)cartLine.Quantity + quantity;

        if (newQuantity > int.MaxValue)
            return (false, "Quantity exceeds the supported range.");

        if (snapshot.Stock.HasValue &&
            newQuantity > snapshot.Stock.Value)
        {
            return (false, "Requested quantity exceeds available stock.");
        }

        // Абсолютное значение пишется одним UPDATE: сущность не нужно
        // загружать в трекер, а результат не зависит от устаревшей копии строки.
        await dbContext.Set<CartItem>()
            .Where(i => i.Id == cartLine.Id && i.UserId == userId)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(i => i.Quantity, (int)newQuantity),
                cancellationToken);

        return (true, null);
    }

    public async Task<(bool Success, string? Error)> UpdateAsync(
        Guid userId,
        long itemId,
        int quantity,
        CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty || itemId <= 0 || quantity <= 0)
            return (false, "Invalid item ID or quantity.");

        // Доступность товара и остаток проверяются в том же UPDATE, что и запись
        // количества: отдельные чтения позиции и карточки больше не нужны,
        // а проверка остатка не отделена от записи окном гонки.
        var updated = await dbContext.Set<CartItem>()
            .Where(i => i.Id == itemId
                        && i.UserId == userId
                        && dbContext.Set<Product>().Any(p =>
                            p.Id == i.ProductId &&
                            p.IsActive &&
                            !p.IsDigital &&
                            (p.Stock == null || p.Stock >= quantity)))
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(i => i.Quantity, quantity),
                cancellationToken);

        if (updated > 0)
            return (true, null);

        // Ничего не обновлено: различаем отсутствие позиции, недоступный товар
        // и недостаточный остаток — те же сообщения, что и раньше.
        var item = await dbContext.Set<CartItem>()
            .AsNoTracking()
            .Where(i => i.Id == itemId && i.UserId == userId)
            .Select(i => new
            {
                i.Product.IsActive,
                i.Product.IsDigital,
                i.Product.Stock
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (item is null)
            return (false, "Cart item was not found.");

        if (!item.IsActive || item.IsDigital)
            return (false, "Product is unavailable.");

        return (false, "Requested quantity exceeds available stock.");
    }

    public async Task<(bool Success, string? Error)> RemoveAsync(
        Guid userId,
        long itemId,
        CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty || itemId <= 0)
            return (false, "Invalid item ID.");

        // Удаление выполняется одним DELETE с условием по владельцу:
        // предварительное чтение позиции не требуется.
        var removed = await dbContext.Set<CartItem>()
            .Where(i => i.Id == itemId && i.UserId == userId)
            .ExecuteDeleteAsync(cancellationToken);

        return removed > 0
            ? (true, null)
            : (false, "Cart item was not found.");
    }
}