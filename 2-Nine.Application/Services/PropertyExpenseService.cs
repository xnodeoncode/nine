using Nine.Core.Constants;
using Nine.Core.Entities;
using Nine.Core.Interfaces.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Nine.Application.Services;

/// <summary>
/// Service for managing fixed/recurring property expenses (mortgage, insurance,
/// property tax, HOA, maintenance, utilities, and other custom costs).
/// </summary>
public class PropertyExpenseService : BaseService<PropertyExpense>
{
    public PropertyExpenseService(
        ApplicationDbContext context,
        ILogger<PropertyExpenseService> logger,
        IUserContextService userContext,
        IOptions<ApplicationSettings> settings)
        : base(context, logger, userContext, settings)
    {
    }

    /// <summary>
    /// Validates property expense business rules before create/update.
    /// </summary>
    protected override async Task ValidateEntityAsync(PropertyExpense entity)
    {
        var orgId = await _userContext.GetActiveOrganizationIdAsync();

        // Validate PropertyId exists and belongs to active organization
        var propertyExists = await _context.Properties
            .AnyAsync(p => p.Id == entity.PropertyId && p.OrganizationId == orgId && !p.IsDeleted);

        if (!propertyExists)
        {
            throw new InvalidOperationException("Property not found or does not belong to your organization.");
        }

        // Validate ExpenseType is a known value
        if (!ApplicationConstants.PropertyExpenseTypes.AllPropertyExpenseTypes.Contains(entity.ExpenseType))
        {
            throw new InvalidOperationException($"'{entity.ExpenseType}' is not a valid expense type.");
        }

        // Validate Frequency is a known value
        if (!ApplicationConstants.ExpenseFrequencies.AllExpenseFrequencies.Contains(entity.Frequency))
        {
            throw new InvalidOperationException($"'{entity.Frequency}' is not a valid expense frequency.");
        }

        // Validate Amount is non-negative
        if (entity.Amount < 0)
        {
            throw new InvalidOperationException("Amount cannot be negative.");
        }

        // Validate EndDate is after EffectiveDate
        if (entity.EndDate.HasValue && entity.EndDate.Value < entity.EffectiveDate)
        {
            throw new InvalidOperationException("End date must be on or after the effective date.");
        }

        await base.ValidateEntityAsync(entity);
    }

    /// <summary>
    /// Gets all property expenses for the active organization with Property navigation included.
    /// Overrides base method to include navigation properties.
    /// </summary>
    public override async Task<List<PropertyExpense>> GetAllAsync()
    {
        var orgId = await _userContext.GetActiveOrganizationIdAsync();

        return await _dbSet
            .Where(e => !e.IsDeleted && !e.IsArchived && e.OrganizationId == orgId)
            .Include(e => e.Property)
            .OrderBy(e => e.Property!.Address)
            .ThenBy(e => e.ExpenseType)
            .ToListAsync();
    }

    /// <summary>
    /// Gets all archived property expenses for the active organization with Property navigation included.
    /// </summary>
    public override async Task<List<PropertyExpense>> GetArchivedAsync()
    {
        var orgId = await _userContext.GetActiveOrganizationIdAsync();

        return await _dbSet
            .Where(e => !e.IsDeleted && e.IsArchived && e.OrganizationId == orgId)
            .Include(e => e.Property)
            .OrderBy(e => e.Property!.Address)
            .ThenBy(e => e.ExpenseType)
            .ToListAsync();
    }

    /// <summary>
    /// Gets currently active expenses for a specific property (no EndDate set).
    /// </summary>
    public async Task<List<PropertyExpense>> GetExpensesByPropertyAsync(Guid propertyId)
    {
        var orgId = await _userContext.GetActiveOrganizationIdAsync();

        return await _dbSet
            .Where(e => e.PropertyId == propertyId && !e.IsDeleted && !e.IsArchived && !e.EndDate.HasValue)
            .Include(e => e.Property)
            .Where(e => e.Property!.OrganizationId == orgId)
            .OrderBy(e => e.ExpenseType)
            .ToListAsync();
    }

    /// <summary>
    /// Gets all expenses (including cancelled) for a property within a date range.
    /// Used for lease-period reporting: returns any expense whose effective span
    /// overlaps the given period.
    /// </summary>
    public async Task<List<PropertyExpense>> GetExpensesForLeasePeriodAsync(Guid propertyId, DateTime leaseStart, DateTime leaseEnd)
    {
        var orgId = await _userContext.GetActiveOrganizationIdAsync();

        return await _dbSet
            .Where(e => e.PropertyId == propertyId && !e.IsDeleted)
            .Include(e => e.Property)
            .Where(e => e.Property!.OrganizationId == orgId)
            // Expense was active at some point during the lease period
            .Where(e => e.EffectiveDate <= leaseEnd &&
                        (!e.EndDate.HasValue || e.EndDate.Value >= leaseStart))
            .OrderBy(e => e.EffectiveDate)
            .ThenBy(e => e.ExpenseType)
            .ToListAsync();
    }

    /// <summary>
    /// Gets the total normalized monthly expense amount for a specific property,
    /// summed across all active (non-ended) expenses.
    /// </summary>
    public async Task<decimal> GetTotalMonthlyExpensesByPropertyAsync(Guid propertyId)
    {
        var expenses = await GetExpensesByPropertyAsync(propertyId);
        var today = DateTime.Today;

        return expenses
            .Where(e => !e.EndDate.HasValue || e.EndDate.Value >= today)
            .Sum(e => e.MonthlyAmount);
    }

    /// <summary>
    /// Gets total normalized monthly expenses grouped by property, for all properties
    /// in the active organization.
    /// </summary>
    public async Task<Dictionary<Guid, decimal>> GetTotalMonthlyExpensesByAllPropertiesAsync()
    {
        var orgId = await _userContext.GetActiveOrganizationIdAsync();
        var today = DateTime.Today;

        var expenses = await _dbSet
            .Where(e => !e.IsDeleted && !e.IsArchived && e.OrganizationId == orgId)
            .Where(e => !e.EndDate.HasValue || e.EndDate.Value >= today)
            .ToListAsync();

        return expenses
            .GroupBy(e => e.PropertyId)
            .ToDictionary(g => g.Key, g => g.Sum(e => e.MonthlyAmount));
    }

    /// <summary>
    /// Cancels an expense by setting EndDate to today and optionally recording a reason.
    /// The record is permanently retained for audit and lease-period reporting.
    /// </summary>
    public async Task CancelExpenseAsync(Guid id, string? reason = null)
    {
        var orgId = await _userContext.GetActiveOrganizationIdAsync();
        var userId = await _userContext.GetUserIdAsync();

        var expense = await _dbSet
            .FirstOrDefaultAsync(e => e.Id == id && !e.IsDeleted && e.OrganizationId == orgId);

        if (expense == null)
            throw new InvalidOperationException("Expense not found or does not belong to your organization.");

        expense.EndDate = DateTime.Today;
        expense.EndReason = string.IsNullOrWhiteSpace(reason)
            ? $"Cancelled by {userId}"
            : reason.Trim();
        expense.LastModifiedOn = DateTime.UtcNow;
        expense.LastModifiedBy = userId;

        _context.Update(expense);
        await _context.SaveChangesAsync();
    }

    /// <summary>
    /// Voids an expense entered in error. Deletes the record according to
    /// ApplicationSettings.SoftDeleteEnabled (hard delete when disabled, soft delete when enabled).
    /// </summary>
    public async Task VoidExpenseAsync(Guid id)
    {
        await DeleteAsync(id);
    }
}
