using ApexApp.Api.Authorization;
using ApexApp.Api.Extensions;
using ApexApp.Domain.Entities;
using ApexApp.Infrastructure.Repositories;
using ApexApp.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ApexApp.Api.Controllers;


/// <summary>
/// Контроллер, отвечающий за управление балансами пользователей.
/// </summary>
[Route("api/balances")]
public sealed class BalancesController : ApiControllerBase
{
    private const int DefaultPageSize = 100;
    private const int MaxPageSize = 500;

    private readonly IBalanceRepository _balanceRepository;
    private readonly ITransactionRepository _transactionRepository;
    private readonly IBalanceService _balanceService;

    /// <summary>
    /// Инициализирует новый экземпляр класса <see cref="BalancesController"/>.
    /// </summary>
    /// <param name="balanceRepository">Репозиторий балансов.</param>
    /// <param name="transactionRepository">Репозиторий транзакций.</param>
    /// <param name="balanceService">Сервис балансов.</param>
    public BalancesController(
        IBalanceRepository balanceRepository,
        ITransactionRepository transactionRepository,
        IBalanceService balanceService)
    {
        _balanceRepository = balanceRepository;
        _transactionRepository = transactionRepository;
        _balanceService = balanceService;
    }

    /// <summary>
    /// Получить рабочий баланс текущего пользователя.
    /// </summary>
    /// <remarks>
    /// Возвращает информацию о рабочем балансе текущего пользователя.
    /// </remarks>
    /// <returns>Информация о балансе пользователя.</returns>
    /// <response code="200">Баланс успешно получен.</response>
    /// <response code="401">Пользователь не авторизован.</response>
    /// <response code="404">Баланс не найден.</response>
    [Authorize(Policy = AppPolicies.IssueBonus)]
    [HttpGet("~/api/admin/balances/me")]
    [ProducesResponseType(typeof(BalanceResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<BalanceResponse>> GetMyRewardBalance(CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        var balance = await _balanceRepository.GetByUserIdAsync(userId.Value, BalanceType.Reward, cancellationToken);
        if (balance is null)
        {
            return NotFound(new { message = "Balance was not found." });
        }

        return Ok(BalanceResponse.From(balance));
    }

    /// <summary>
    /// Получить личный баланс текущего пользователя.
    /// </summary>
    /// <remarks>
    /// Возвращает информацию о личном балансе текущего пользователя.
    /// </remarks>
    /// <returns>Информация о балансе пользователя.</returns>
    /// <response code="200">Баланс успешно получен.</response>
    /// <response code="401">Пользователь не авторизован.</response>
    /// <response code="404">Баланс не найден.</response>
    [Authorize(Policy = AppPolicies.ActiveUser)]
    [HttpGet("me")]
    [ProducesResponseType(typeof(BalanceResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<BalanceResponse>> GetMyBalance(CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        var balance = await _balanceRepository.GetByUserIdAsync(userId.Value, BalanceType.Personal, cancellationToken);
        if (balance is null)
        {
            return NotFound(new { message = "Balance was not found." });
        }

        return Ok(BalanceResponse.From(balance));
    }

    /// <summary>
    /// Получить список транзакций текущего пользователя.
    /// </summary>
    /// <remarks>
    /// Возвращает информацию о транзакциях текущего пользователя.
    /// </remarks>
    /// <returns>Список транзакций пользователя.</returns>
    /// <response code="200">Транзакции успешно получены.</response>
    /// <response code="401">Пользователь не авторизован.</response>
    /// <response code="400">Некорректные параметры запроса.</response>
    /// <response code="404">Транзакции не найдены.</response>
    [Authorize(Policy = AppPolicies.ActiveUser)]
    [HttpGet("me/transactions")]
    [ProducesResponseType(typeof(IReadOnlyList<TransactionResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<IReadOnlyList<TransactionResponse>>> GetMyTransactions(
        [FromQuery] int skip = 0,
        [FromQuery] int take = DefaultPageSize,
        CancellationToken cancellationToken = default)
    {
        if (skip < 0 || take < 1 || take > MaxPageSize)
        {
            return BadRequest(new { message = $"skip must be non-negative and take must be between 1 and {MaxPageSize}." });
        }

        var userId = User.GetUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        var transactions = await _transactionRepository.GetByUserIdAsync(
            userId.Value,
            skip,
            take,
            cancellationToken);

        return Ok(transactions.Select(TransactionResponse.From).ToList());
    }

    /// <summary>
    /// Изменение баланса пользователя администратором.
    /// </summary>
    /// <returns>Информация о балансе пользователя.</returns>
    /// <response code="200">Баланс успешно изменен.</response>
    /// <response code="401">Пользователь не авторизован.</response>
    [Authorize(Policy = AppPolicies.AdjustBalances)]
    [HttpPost("~/api/admin/balances/adjust")]
    [ProducesResponseType(typeof(BalanceResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<BalanceResponse>> AdjustBalance(
        [FromBody] AdjustBalanceRequest request,
        CancellationToken cancellationToken)
    {
        var createdBy = User.GetUserId();
        if (createdBy is null)
        {
            return Unauthorized();
        }

        var result = await _balanceService.AdjustBalanceAsync(
            request.UserId,
            request.Type,
            request.Amount,
            request.TransactionType,
            request.RelatedUserId,
            request.RelatedEntityType,
            request.RelatedEntityId,
            request.Description,
            createdBy.Value,
            cancellationToken);

        if (!result.IsSuccess)
        {
            return ToErrorResult(result.StatusCode, result.Error);
        }

        return Ok(BalanceResponse.From(result.Value!));
    }
}

/// <summary>
/// DTO для запроса на изменение баланса пользователя.
/// </summary>
public sealed record AdjustBalanceRequest(Guid UserId, BalanceType Type, long Amount, TransactionType TransactionType, 
    Guid? RelatedUserId = null, string? RelatedEntityType = null, long? RelatedEntityId = null, string? Description = null);

/// <summary>
/// Класс, представляющий ответ с информацией о балансе пользователя.
/// </summary>
public sealed record BalanceResponse(Guid UserId, BalanceType Type, long Amount, int Version, DateTime UpdatedAt)
{
    /// <summary>
    /// Метод, который создает экземпляр <see cref="BalanceResponse"/> из сущности <see cref="Balance"/>.
    /// </summary>
    public static BalanceResponse From(Balance balance) =>
        new(balance.UserId, balance.Type, balance.Amount, balance.Version, balance.UpdatedAt);
}

/// <summary>
/// Класс, представляющий ответ с информацией о транзакции пользователя.
/// </summary>
public sealed record TransactionResponse(
    long Id,
    long Amount,
    long BalanceAfter,
    string? Description,
    Guid? RelatedUserId,
    string? RelatedEntityType,
    long? RelatedEntityId,
    DateTime CreatedAt)
{
    /// <summary>
    /// Метод, который создает экземпляр <see cref="TransactionResponse"/> из сущности <see cref="Transaction"/>.
    /// </summary>
    public static TransactionResponse From(Transaction transaction) =>
        new(
            transaction.Id,
            transaction.Amount,
            transaction.BalanceAfter,
            transaction.Description,
            transaction.RelatedUserId,
            transaction.RelatedEntityType,
            transaction.RelatedEntityId,
            transaction.CreatedAt);
}
