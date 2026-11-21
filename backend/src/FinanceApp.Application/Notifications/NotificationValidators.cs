using FluentValidation;

namespace FinanceApp.Application.Notifications;

public sealed class NotificationListQueryValidator : AbstractValidator<NotificationListQuery>
{
    public NotificationListQueryValidator()
    {
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1).WithMessage("A página deve ser maior ou igual a 1.");
        RuleFor(x => x.PageSize)
            .InclusiveBetween(1, NotificationListQuery.MaxPageSize)
            .WithMessage($"O tamanho da página deve estar entre 1 e {NotificationListQuery.MaxPageSize}.");
    }
}
