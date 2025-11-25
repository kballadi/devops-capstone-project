using FluentValidation;
using LogiTrack.Models;

namespace LogiTrack.Validators
{
    /// <summary>
    /// Validator for Order model to ensure data integrity
    /// </summary>
    public class OrderValidator : AbstractValidator<Order>
    {
        public OrderValidator()
        {
            RuleFor(x => x.CustomerName)
                .NotEmpty().WithMessage("Customer name is required")
                .MaximumLength(100).WithMessage("Customer name cannot exceed 100 characters");

            RuleFor(x => x.DatePlaced)
                .LessThanOrEqualTo(DateTime.UtcNow).WithMessage("Order date cannot be in the future");

            RuleFor(x => x.Items)
                .NotNull().WithMessage("Order must have items");
        }
    }

    /// <summary>
    /// Validator for InventoryItem model
    /// </summary>
    public class InventoryItemValidator : AbstractValidator<InventoryItem>
    {
        public InventoryItemValidator()
        {
            RuleFor(x => x.Name)
                .NotEmpty().WithMessage("Item name is required")
                .MaximumLength(200).WithMessage("Item name cannot exceed 200 characters");

            RuleFor(x => x.Quantity)
                .GreaterThanOrEqualTo(0).WithMessage("Quantity cannot be negative");

            RuleFor(x => x.Location)
                .NotEmpty().WithMessage("Location is required")
                .MaximumLength(100).WithMessage("Location cannot exceed 100 characters");
        }
    }
}
