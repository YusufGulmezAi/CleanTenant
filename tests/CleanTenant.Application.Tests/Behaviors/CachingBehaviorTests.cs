using CleanTenant.Application.Common.Behaviors;
using CleanTenant.Application.Common.Interfaces;
using CleanTenant.Application.Common.Models;
using FluentAssertions;
using MediatR;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace CleanTenant.Application.Tests.Behaviors;

public class CachingBehaviorTests
{
    // Cacheable test query
    private record TestCacheQuery : IRequest<Result<string>>, ICacheableQuery
    {
        public string CacheKey => "test:cache:key";
        public TimeSpan? CacheDuration => TimeSpan.FromMinutes(5);
    }

    // Non-cacheable test command
    private record TestCommand : IRequest<Result<string>>;

    [Fact]
    public async Task Handle_NonCacheableRequest_ShouldCallNextDirectly()
    {
        // Arrange
        var cache = Substitute.For<ICacheService>();
        var logger = Substitute.For<ILogger<CachingBehavior<TestCommand, Result<string>>>>();
        var behavior = new CachingBehavior<TestCommand, Result<string>>(cache, logger);
        var next = Substitute.For<RequestHandlerDelegate<Result<string>>>();
        next.Invoke().Returns(Result<string>.Success("direct"));

        // Act
        var result = await behavior.Handle(new TestCommand(), next, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be("direct");
        await next.Received(1).Invoke();
        await cache.DidNotReceive().GetAsync<Result<string>>(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_CacheHit_ShouldReturnCachedValue()
    {
        // Arrange
        var cache = Substitute.For<ICacheService>();
        var logger = Substitute.For<ILogger<CachingBehavior<TestCacheQuery, Result<string>>>>();
        var cachedResult = Result<string>.Success("cached-data");
        cache.GetAsync<Result<string>>("test:cache:key", Arg.Any<CancellationToken>())
            .Returns(cachedResult);

        var behavior = new CachingBehavior<TestCacheQuery, Result<string>>(cache, logger);
        var next = Substitute.For<RequestHandlerDelegate<Result<string>>>();

        // Act
        var result = await behavior.Handle(new TestCacheQuery(), next, CancellationToken.None);

        // Assert
        result.Value.Should().Be("cached-data");
        await next.DidNotReceive().Invoke(); // Handler çağrılMADI — cache'ten döndü
    }

    [Fact]
    public async Task Handle_CacheMiss_ShouldCallNextAndCacheResult()
    {
        // Arrange
        var cache = Substitute.For<ICacheService>();
        var logger = Substitute.For<ILogger<CachingBehavior<TestCacheQuery, Result<string>>>>();
        cache.GetAsync<Result<string>>("test:cache:key", Arg.Any<CancellationToken>())
            .Returns((Result<string>?)null); // Cache miss

        var behavior = new CachingBehavior<TestCacheQuery, Result<string>>(cache, logger);
        var next = Substitute.For<RequestHandlerDelegate<Result<string>>>();
        next.Invoke().Returns(Result<string>.Success("fresh-data"));

        // Act
        var result = await behavior.Handle(new TestCacheQuery(), next, CancellationToken.None);

        // Assert
        result.Value.Should().Be("fresh-data");
        await next.Received(1).Invoke(); // Handler çağrıldı
        await cache.Received(1).SetAsync(
            "test:cache:key",
            Arg.Any<Result<string>>(),
            TimeSpan.FromMinutes(5),
            Arg.Any<CancellationToken>()); // Cache'e yazıldı
    }
}

public class CacheInvalidationBehaviorTests
{
    private record TestInvalidateCommand : IRequest<Result<string>>, ICacheInvalidator
    {
        public string[] CacheKeysToInvalidate => ["key1", "key2"];
    }

    [Fact]
    public async Task Handle_SuccessfulCommand_ShouldInvalidateKeys()
    {
        // Arrange
        var cache = Substitute.For<ICacheService>();
        var logger = Substitute.For<ILogger<CacheInvalidationBehavior<TestInvalidateCommand, Result<string>>>>();
        var behavior = new CacheInvalidationBehavior<TestInvalidateCommand, Result<string>>(cache, logger);
        var next = Substitute.For<RequestHandlerDelegate<Result<string>>>();
        next.Invoke().Returns(Result<string>.Success("ok"));

        // Act
        await behavior.Handle(new TestInvalidateCommand(), next, CancellationToken.None);

        // Assert
        await cache.Received(1).RemoveAsync("key1", Arg.Any<CancellationToken>());
        await cache.Received(1).RemoveAsync("key2", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_FailedCommand_ShouldNotInvalidateKeys()
    {
        // Arrange
        var cache = Substitute.For<ICacheService>();
        var logger = Substitute.For<ILogger<CacheInvalidationBehavior<TestInvalidateCommand, Result<string>>>>();
        var behavior = new CacheInvalidationBehavior<TestInvalidateCommand, Result<string>>(cache, logger);
        var next = Substitute.For<RequestHandlerDelegate<Result<string>>>();
        next.Invoke().Returns(Result<string>.Failure("error"));

        // Act
        await behavior.Handle(new TestInvalidateCommand(), next, CancellationToken.None);

        // Assert — cache silinMEDİ
        await cache.DidNotReceive().RemoveAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }
}
