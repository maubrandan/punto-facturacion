using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using POS.Application.Common;
using POS.Application.Contracts;
using POS.Application.Contracts.Cash;
using POS.Application.Interfaces;

namespace POS.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public sealed class CashController : ControllerBase
{
    private readonly ICashSessionService _cash;

    public CashController(ICashSessionService cash)
    {
        _cash = cash;
    }

    /// <summary>Resumen en vivo de la sesión abierta (totales y saldo proyectado).</summary>
    [HttpGet("summary")]
    [ProducesResponseType(typeof(ApiResponse<CashSessionSummaryResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSummary(CancellationToken cancellationToken)
    {
        var summary = await _cash.GetCurrentSummaryAsync(cancellationToken);
        return Ok(
            ApiResponse<CashSessionSummaryResponse>.FromResult(
                Result<CashSessionSummaryResponse>.Ok(summary)));
    }

    [HttpPost("open")]
    [ProducesResponseType(typeof(ApiResponse<CashSessionOpenResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<CashSessionOpenResponse>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Open([FromBody] OpenSessionRequest request, CancellationToken cancellationToken)
    {
        var result = await _cash.OpenSessionAsync(request.InitialAmount, cancellationToken);
        var body = ApiResponse<CashSessionOpenResponse>.FromResult(result);
        if (!result.IsSuccess)
            return BadRequest(body);

        return Ok(body);
    }

    [HttpPost("close")]
    [ProducesResponseType(typeof(ApiResponse<CashSessionCloseResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<CashSessionCloseResponse>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Close([FromBody] CloseSessionRequest request, CancellationToken cancellationToken)
    {
        var result = await _cash.CloseSessionAsync(request.CountedAmount, cancellationToken);
        var body = ApiResponse<CashSessionCloseResponse>.FromResult(result);
        if (!result.IsSuccess)
            return BadRequest(body);

        return Ok(body);
    }

    [HttpGet("categories")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<ExpenseCategoryResponse>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetCategories(CancellationToken cancellationToken)
    {
        var list = await _cash.ListCategoriesAsync(cancellationToken);
        return Ok(
            ApiResponse<IReadOnlyList<ExpenseCategoryResponse>>.FromResult(
                Result<IReadOnlyList<ExpenseCategoryResponse>>.Ok(list)));
    }

    [HttpPost("categories")]
    [ProducesResponseType(typeof(ApiResponse<ExpenseCategoryResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<ExpenseCategoryResponse>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateCategory(
        [FromBody] CreateExpenseCategoryRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _cash.CreateCategoryAsync(request.Name, cancellationToken);
        var body = ApiResponse<ExpenseCategoryResponse>.FromResult(result);
        if (!result.IsSuccess)
            return BadRequest(body);

        return Ok(body);
    }

    [HttpPost("expenses")]
    [ProducesResponseType(typeof(ApiResponse<ExpenseResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<ExpenseResponse>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> RegisterExpense(
        [FromBody] RegisterExpenseRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _cash.RegisterExpenseAsync(request, cancellationToken);
        var body = ApiResponse<ExpenseResponse>.FromResult(result);
        if (!result.IsSuccess)
            return BadRequest(body);

        return Ok(body);
    }
}
