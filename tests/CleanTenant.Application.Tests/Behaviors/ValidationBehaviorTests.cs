using CleanTenant.Application.Common.Behaviors;
using CleanTenant.Application.Common.Models;
using FluentAssertions;
using FluentValidation;
using FluentValidation.Results;
using MediatR;
using NSubstitute;
using Xunit;

namespace CleanTenant.Application.Tests.Behaviors;

public class ValidationBehaviorTests
{
    // Test request/response tipleri
    private record TestCommand(string Name) : IRequest<Result<string>>;

    private class TestCommandValidator : AbstractValidator<TestCommand>
    {
        public TestCommandValidator()
        {
            RuleFor(x => x.Name).NotEmpty().WithMessage("İsim zorunludur.");
            RuleFor(x => x.Name).MinimumLength(3).WithMessage("İsim en az 3 karakter.");
        }
    }

    [Fact]
    public async Task Handle_WithValidRequest_ShouldCallNext()
    {
        // Arrange
        var validators = new List<IValidator<TestCommand>> { new TestCommandValidator() };
        var behavior = new ValidationBehavior<TestCommand, Result<string>>(validators);
        var next = Substitute.For<RequestHandlerDelegate<Result<string>>>();
        next.Invoke().Returns(Result<string>.Success("ok"));

        // Act
        var result = await behavior.Handle(
            new TestCommand("Valid Name"), next, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        await next.Received(1).Invoke(); // Handler çağrıldı
    }

    [Fact]
    public async Task Handle_WithInvalidRequest_ShouldReturnValidationFailure()
    {
        // Arrange
        var validators = new List<IValidator<TestCommand>> { new TestCommandValidator() };
        var behavior = new ValidationBehavior<TestCommand, Result<string>>(validators);
        var next = Substitute.For<RequestHandlerDelegate<Result<string>>>();

        // Act
        var result = await behavior.Handle(
            new TestCommand(""), next, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.StatusCode.Should().Be(422);
        result.Errors.Should().NotBeEmpty();
        await next.DidNotReceive().Invoke(); // Handler çağrılMADI
    }

    [Fact]
    public async Task Handle_WithNoValidators_ShouldCallNext()
    {
        // Arrange — validator yok
        var validators = Enumerable.Empty<IValidator<TestCommand>>();
        var behavior = new ValidationBehavior<TestCommand, Result<string>>(validators);
        var next = Substitute.For<RequestHandlerDelegate<Result<string>>>();
        next.Invoke().Returns(Result<string>.Success("ok"));

        // Act
        var result = await behavior.Handle(
            new TestCommand("anything"), next, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        await next.Received(1).Invoke();
    }
}
