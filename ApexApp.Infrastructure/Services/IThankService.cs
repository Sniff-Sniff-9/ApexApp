using ApexApp.Domain.Entities;

namespace ApexApp.Infrastructure.Services;

public interface IThankService
{
    Task<ThanksResult> CreateAsync(
        Guid fromUserId,
        CreateThankRequest request,
        CancellationToken cancellationToken = default);

    Task<List<Thank>> GetAllAsync(
        Guid userId,

        CancellationToken cancellationToken = default);
    Task<List<Thank>> GetAllFromMeAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<Thank?> GetByIdAsync(
        Guid userId,
        long id,
        CancellationToken cancellationToken = default);
}