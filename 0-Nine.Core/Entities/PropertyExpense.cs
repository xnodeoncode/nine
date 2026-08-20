using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Nine.Core.Constants;
using Nine.Core.Validation;

namespace Nine.Core.Entities;

/// <summary>
/// Represents a fixed/recurring expense associated with a property (e.g. mortgage,
/// insurance, property tax, HOA, maintenance, utilities, or other custom costs).
/// Amounts are captured with a Frequency and normalized to a monthly equivalent via
/// <see cref="MonthlyAmount"/> so expenses of differing cadences can be rolled up
/// together for reporting.
/// </summary>
public class PropertyExpense : BaseModel
{
    // Core Identity
    [RequiredGuid]
    public Guid PropertyId { get; set; }

    [Required]
    [StringLength(50)]
    [Display(Name = "Expense Type")]
    public string ExpenseType { get; set; } = string.Empty; // From ApplicationConstants.PropertyExpenseTypes

    [StringLength(200)]
    [Display(Name = "Description")]
    public string Description { get; set; } = string.Empty; // e.g., "Primary mortgage - Chase", "Annual hazard insurance"

    [Required]
    [Column(TypeName = "decimal(18,2)")]
    [Display(Name = "Amount")]
    public decimal Amount { get; set; }

    [Required]
    [StringLength(20)]
    [Display(Name = "Frequency")]
    public string Frequency { get; set; } = ApplicationConstants.ExpenseFrequencies.Monthly; // From ApplicationConstants.ExpenseFrequencies

    /// <summary>
    /// Date the expense becomes effective. Defaults to today.
    /// </summary>
    [Required]
    [Display(Name = "Effective Date")]
    public DateTime EffectiveDate { get; set; } = DateTime.Today;

    /// <summary>
    /// Optional date the expense ends (e.g. mortgage payoff, policy cancellation).
    /// Null means the expense is ongoing/indefinite.
    /// </summary>
    [Display(Name = "End Date")]
    public DateTime? EndDate { get; set; }

    [StringLength(2000)]
    [Display(Name = "Notes")]
    public string Notes { get; set; } = string.Empty;

    /// <summary>
    /// Recorded when an expense is cancelled via the Edit → Cancel Expense flow.
    /// Null for active expenses and voided records.
    /// </summary>
    [StringLength(50)]
    [Display(Name = "Cancellation Reason")]
    public string? EndReason { get; set; }

    // Navigation Properties
    public virtual Property? Property { get; set; }

    // Computed Properties
    [NotMapped]
    [Display(Name = "Monthly Amount")]
    public decimal MonthlyAmount => Frequency switch
    {
        ApplicationConstants.ExpenseFrequencies.Monthly => Amount,
        ApplicationConstants.ExpenseFrequencies.Quarterly => Amount / 3m,
        ApplicationConstants.ExpenseFrequencies.Annual => Amount / 12m,
        ApplicationConstants.ExpenseFrequencies.OneTime => 0m, // One-time costs don't contribute to recurring monthly totals
        _ => Amount
    };

    [NotMapped]
    [Display(Name = "Is Active")]
    public bool IsActive => !EndDate.HasValue || EndDate.Value >= DateTime.Today;
}
