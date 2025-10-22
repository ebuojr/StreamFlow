using FluentValidation;
using OrderEntity = Entities.Model.Order;

namespace ERPApi.Services.Validation
{
    /// <summary>
    /// FluentValidation validator for Order entity.
    /// Validates order data before processing to catch errors early.
    /// </summary>
    public class OrderValidator : AbstractValidator<OrderEntity>
    {
        public OrderValidator()
        {
            // Customer validation
            RuleFor(order => order.Customer)
                .NotNull()
                .WithMessage("Customer is required");

            When(order => order.Customer != null, () =>
            {
                RuleFor(order => order.Customer)
                    .Must(customer => !string.IsNullOrWhiteSpace(customer.FirstName) || 
                                     !string.IsNullOrWhiteSpace(customer.LastName))
                    .WithMessage("Customer name (FirstName or LastName) is required");

                RuleFor(order => order.Customer.Email)
                    .NotEmpty()
                    .WithMessage("Customer email is required")
                    .EmailAddress()
                    .WithMessage("Customer email must be a valid email address");
            });

            // Shipping Address validation
            RuleFor(order => order.ShippingAddress)
                .NotNull()
                .WithMessage("Shipping address is required");

            When(order => order.ShippingAddress != null, () =>
            {
                RuleFor(order => order.ShippingAddress.Street)
                    .NotEmpty()
                    .WithMessage("Shipping street is required");

                RuleFor(order => order.ShippingAddress.City)
                    .NotEmpty()
                    .WithMessage("Shipping city is required");

                RuleFor(order => order.ShippingAddress.PostalCode)
                    .NotEmpty()
                    .WithMessage("Shipping postal code is required");

                RuleFor(order => order.ShippingAddress.Country)
                    .NotEmpty()
                    .WithMessage("Shipping country is required");
            });

            // Order Items validation
            RuleFor(order => order.OrderItems)
                .NotNull()
                .WithMessage("Order must contain at least one item")
                .NotEmpty()
                .WithMessage("Order must contain at least one item");

            RuleForEach(order => order.OrderItems)
                .ChildRules(item =>
                {
                    item.RuleFor(x => x.Sku)
                        .NotEmpty()
                        .WithMessage("SKU is required");

                    item.RuleFor(x => x.UnitPrice)
                        .GreaterThan(0)
                        .WithMessage("UnitPrice must be greater than 0");

                    item.RuleFor(x => x.Quantity)
                        .GreaterThan(0)
                        .WithMessage("Quantity must be greater than 0");

                    item.RuleFor(x => x.Name)
                        .NotEmpty()
                        .WithMessage("Product name is required");
                });

            // Payment validation
            RuleFor(order => order.Payment)
                .NotNull()
                .WithMessage("Payment information is required");

            When(order => order.Payment != null, () =>
            {
                RuleFor(order => order.Payment.PaymentMethod)
                    .NotEmpty()
                    .WithMessage("Payment method is required");

                RuleFor(order => order.Payment.Amount)
                    .GreaterThan(0)
                    .WithMessage("Payment amount must be greater than 0");
            });

            // Total amount validation
            RuleFor(order => order.TotalAmount)
                .GreaterThan(0)
                .WithMessage("Total amount must be greater than 0");

            // Total amount must match sum of items
            When(order => order.OrderItems != null && order.OrderItems.Any(), () =>
            {
                RuleFor(order => order.TotalAmount)
                    .Must((order, totalAmount) =>
                    {
                        var calculatedTotal = order.OrderItems.Sum(i => i.UnitPrice * i.Quantity);
                        return Math.Abs(totalAmount - calculatedTotal) <= 0.01m; // Allow 1 cent rounding difference
                    })
                    .WithMessage(order =>
                    {
                        var calculatedTotal = order.OrderItems.Sum(i => i.UnitPrice * i.Quantity);
                        return $"Total amount mismatch: Order total ({order.TotalAmount:F2}) does not match sum of items ({calculatedTotal:F2})";
                    });
            });
        }
    }
}
