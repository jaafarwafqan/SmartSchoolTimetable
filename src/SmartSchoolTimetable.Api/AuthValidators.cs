using FluentValidation;
using SmartSchoolTimetable.Application;

namespace SmartSchoolTimetable.Api;

internal static class CredentialRuleExtensions
{
    public static IRuleBuilderOptions<T, string?> RequiredCode<T>(this IRuleBuilder<T, string?> rule) =>
        rule.NotEmpty().WithErrorCode(ErrorCodes.Required).WithMessage(ErrorCodes.Required);

    public static IRuleBuilderOptions<T, string?> ValidPassword<T>(this IRuleBuilder<T, string?> rule) =>
        rule.RequiredCode()
            .MinimumLength(CredentialRules.PasswordMinLength)
            .WithErrorCode(ErrorCodes.PasswordTooShort).WithMessage(ErrorCodes.PasswordTooShort)
            .MaximumLength(CredentialRules.PasswordMaxLength)
            .WithErrorCode(ErrorCodes.PasswordTooLong).WithMessage(ErrorCodes.PasswordTooLong);

    public static IRuleBuilderOptions<T, string?> ValidUsername<T>(this IRuleBuilder<T, string?> rule) =>
        rule.RequiredCode()
            .MinimumLength(CredentialRules.UsernameMinLength)
            .WithErrorCode(ErrorCodes.UsernameTooShort).WithMessage(ErrorCodes.UsernameTooShort)
            .MaximumLength(CredentialRules.UsernameMaxLength)
            .WithErrorCode(ErrorCodes.UsernameTooLong).WithMessage(ErrorCodes.UsernameTooLong);
}

public sealed class SetupRequestValidator : AbstractValidator<SetupRequest>
{
    public SetupRequestValidator()
    {
        RuleFor(request => request.Username).ValidUsername();
        RuleFor(request => request.Password).ValidPassword();
        RuleFor(request => request.ConfirmPassword)
            .Equal(request => request.Password)
            .WithErrorCode(ErrorCodes.PasswordMismatch).WithMessage(ErrorCodes.PasswordMismatch);
    }
}

public sealed class LoginRequestValidator : AbstractValidator<LoginRequest>
{
    public LoginRequestValidator()
    {
        RuleFor(request => request.Username).RequiredCode();
        RuleFor(request => request.Password).RequiredCode();
    }
}

public sealed class RecoveryRequestValidator : AbstractValidator<RecoveryRequest>
{
    public RecoveryRequestValidator()
    {
        RuleFor(request => request.RecoveryCode).RequiredCode();
        RuleFor(request => request.NewPassword).ValidPassword();
    }
}

public sealed class RecoveryCodeRequestValidator : AbstractValidator<RecoveryCodeRequest>
{
    public RecoveryCodeRequestValidator()
    {
        RuleFor(request => request.CurrentPassword).RequiredCode();
    }
}

public sealed class InactivityTimeoutRequestValidator : AbstractValidator<InactivityTimeoutRequest>
{
    public InactivityTimeoutRequestValidator()
    {
        RuleFor(request => request.InactivityTimeout)
            .RequiredCode()
            .Must(value => InactivityTimeoutChoices.TryParse(value, out _))
            .WithErrorCode(ErrorCodes.InvalidInactivityTimeout).WithMessage(ErrorCodes.InvalidInactivityTimeout);
    }
}

public sealed class ChangePasswordRequestValidator : AbstractValidator<ChangePasswordRequest>
{
    public ChangePasswordRequestValidator()
    {
        RuleFor(request => request.CurrentPassword).RequiredCode();
        RuleFor(request => request.NewPassword).ValidPassword();
    }
}
