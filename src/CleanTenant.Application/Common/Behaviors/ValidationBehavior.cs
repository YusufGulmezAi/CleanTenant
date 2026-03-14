using CleanTenant.Application.Common.Models;
using FluentValidation;
using MediatR;

namespace CleanTenant.Application.Common.Behaviors;

/// <summary>
/// FluentValidation pipeline behavior'ı — her MediatR isteğinde otomatik çalışır.
/// 
/// <para><b>PIPELINE BEHAVIOR NEDİR?</b></para>
/// MediatR'ın middleware sistemidir. Bir Command/Query handler'a ulaşmadan
/// önce sırasıyla pipeline behavior'lar çalışır:
/// 
/// <code>
/// İstek → [Validation] → [Logging] → [Authorization] → [Caching] → Handler
/// </code>
/// 
/// <para><b>NASIL ÇALIŞIR?</b></para>
/// <code>
/// // 1. Validator tanımla (ayrı sınıf):
/// public class CreateTenantValidator : AbstractValidator&lt;CreateTenantCommand&gt;
/// {
///     public CreateTenantValidator()
///     {
///         RuleFor(x => x.Name).NotEmpty().WithMessage("İsim zorunludur.");
///         RuleFor(x => x.Identifier).NotEmpty().MinimumLength(3);
///     }
/// }
/// 
/// // 2. Handler'da doğrulama kodu YAZMANA GEREK YOK!
/// // ValidationBehavior otomatik olarak validator'ı bulur ve çalıştırır.
/// // Hata varsa handler'a hiç ulaşmadan Result.ValidationFailure döner.
/// </code>
/// 
/// <para><b>NEDEN EXCEPTION FIRLATMIYORUZ?</b></para>
/// Klasik yaklaşım <c>throw new ValidationException(errors)</c> şeklindedir.
/// Biz Result pattern kullanıyoruz çünkü:
/// <list type="bullet">
///   <item>Exception fırlatmak yavaştır (stack trace maliyeti)</item>
///   <item>Flow control için exception kullanmak anti-pattern'dir</item>
///   <item>Result pattern ile hata mesajları tip güvenli taşınır</item>
///   <item>Handler'ın dönüş tipi ile tutarlı kalır</item>
/// </list>
/// </summary>
/// <typeparam name="TRequest">MediatR request tipi (Command veya Query)</typeparam>
/// <typeparam name="TResponse">Handler'ın dönüş tipi (Result&lt;T&gt; olmalı)</typeparam>
public class ValidationBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private readonly IEnumerable<IValidator<TRequest>> _validators;

    /// <summary>
    /// DI container, bu request tipi için kayıtlı TÜM validator'ları inject eder.
    /// Bir request için birden fazla validator olabilir (parçalı doğrulama).
    /// Hiç validator yoksa boş koleksiyon gelir — behavior hiçbir şey yapmadan geçer.
    /// </summary>
    public ValidationBehavior(IEnumerable<IValidator<TRequest>> validators)
    {
        _validators = validators;
    }

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        // Validator yoksa direkt handler'a geç
        if (!_validators.Any())
            return await next();

        // Tüm validator'ları paralel çalıştır
        var context = new ValidationContext<TRequest>(request);

        var validationResults = await Task.WhenAll(
            _validators.Select(v => v.ValidateAsync(context, cancellationToken)));

        // Tüm hataları topla
        var failures = validationResults
            .SelectMany(r => r.Errors)
            .Where(f => f is not null)
            .ToList();

        if (failures.Count == 0)
            return await next(); // Hata yok → handler'a geç

        // Hata var → Result.ValidationFailure dön
        var errorMessages = failures
            .Select(f => f.ErrorMessage)
            .Distinct()
            .ToList();

        // TResponse'un Result<T> olup olmadığını kontrol et
        // Result<T> ise ValidationFailure factory method'unu çağır
        var responseType = typeof(TResponse);

        if (responseType.IsGenericType &&
            responseType.GetGenericTypeDefinition() == typeof(Result<>))
        {
            // Reflection ile Result<T>.ValidationFailure(errors) çağır
            var resultType = responseType.GetGenericArguments()[0];
            var failureMethod = typeof(Result<>)
                .MakeGenericType(resultType)
                .GetMethod(nameof(Result<object>.ValidationFailure));

            if (failureMethod is not null)
            {
                var result = failureMethod.Invoke(null, [errorMessages]);
                return (TResponse)result!;
            }
        }

        // Result<T> değilse klasik exception fırlat (fallback)
        throw new ValidationException(failures);
    }
}
