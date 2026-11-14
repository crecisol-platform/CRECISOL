using Crecisol.Application.Identity;
using Microsoft.AspNetCore.Http;

namespace Crecisol.Api.Contracts;

/// <summary>
/// Internal composition boundary for the C05 neutral registration intent.
/// Deliberately NOT mapped into Program.cs and NOT an authentication route.
/// </summary>
public interface INeutralRegistrationIntentHandler
{
    Task SubmitAsync(string canonicalMailbox, CancellationToken cancellationToken);
}

/// <summary>
/// Adapter to the existing C05-02 application use case. Its acknowledgement
/// MUST NOT be interpreted as verified mailbox, membership or session.
/// </summary>
public sealed class ApplicationNeutralRegistrationIntentHandler : INeutralRegistrationIntentHandler
{
    private readonly RequestNeutralRegistrationIntent useCase;

    public ApplicationNeutralRegistrationIntentHandler(RequestNeutralRegistrationIntent useCase)
    {
        this.useCase = useCase ?? throw new ArgumentNullException(nameof(useCase));
    }

    public async Task SubmitAsync(string canonicalMailbox, CancellationToken cancellationToken)
    {
        _ = await useCase.ExecuteAsync(canonicalMailbox, cancellationToken);
    }
}

public enum NeutralRegistrationIntakeOutcome
{
    Denied = 0,
    Acknowledged = 1
}

/// <summary>
/// Joins transport, exact JSON schema and an injectable application port.
/// No ASP.NET endpoint, cookie/JWT, identity, role or tenant selection is created.
/// </summary>
public sealed class NeutralRegistrationIntake
{
    private readonly INeutralRegistrationIntentHandler handler;

    public NeutralRegistrationIntake(INeutralRegistrationIntentHandler handler)
    {
        this.handler = handler ?? throw new ArgumentNullException(nameof(handler));
    }

    public async Task<NeutralRegistrationIntakeOutcome> SubmitAsync(
        HttpRequest request, ReadOnlyMemory<byte> body, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        if (!NeutralRegistrationTransportPreflight.IsEligible(request) ||
            request.ContentLength != body.Length ||
            !NeutralRegistrationPayload.TryReadCanonicalMailbox(body, out var mailbox) ||
            mailbox is null)
            return NeutralRegistrationIntakeOutcome.Denied;

        // Only a neutral registration intent; all tenant resolution and durable
        // writes remain the responsibility of the trusted Application adapters.
        await handler.SubmitAsync(mailbox, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        return NeutralRegistrationIntakeOutcome.Acknowledged;
    }
}
