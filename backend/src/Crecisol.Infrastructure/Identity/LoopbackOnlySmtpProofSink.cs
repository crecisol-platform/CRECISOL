using System.Net;
using System.Net.Mail;
using System.Text;
using Crecisol.Application.Identity;

namespace Crecisol.Infrastructure.Identity;

/// <summary>
/// C05-02 disposable transport fixture ONLY. Proves an SMTP conversation
/// across a real loopback TCP socket to a separate synthetic receiving process.
/// Not a production SMTP client, not a provider integration and not a grant.
/// Never register this fixture in the product API's DI container.
/// </summary>
public sealed class LoopbackOnlySmtpProofSink : ITrustedLocalProofMailboxSink
{
    private readonly int port;

    public LoopbackOnlySmtpProofSink(int receiverPort)
    {
        // A privileged SMTP port or remote name must never be configured.
        if (receiverPort is < 1024 or > 65535)
            throw new InvalidOperationException(
                "Only an unprivileged disposable loopback receiver is allowed.");
        port = receiverPort;
    }

    public async Task DeliverInTestOnlyAsync(
        string storedCanonicalMailbox, ReadOnlyMemory<byte> capability,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        // The mailbox must have come from the authenticated encrypted intent,
        // not from an HTTP request. Only reserved synthetic recipients are sent.
        if (capability.Length != 32 ||
            string.IsNullOrWhiteSpace(storedCanonicalMailbox) ||
            !string.Equals(
                RequestNeutralRegistrationIntent.CanonicalMailboxOrNull(
                    storedCanonicalMailbox),
                storedCanonicalMailbox, StringComparison.Ordinal) ||
            !storedCanonicalMailbox.EndsWith(
                "@example.test", StringComparison.Ordinal))
            throw new InvalidOperationException(
                "Only an exact synthetic local recipient is allowed.");

        using var message = new MailMessage(
            new MailAddress("no-reply@crecisol.example.test"),
            new MailAddress(storedCanonicalMailbox))
        {
            Subject = "CRECISOL disposable mailbox proof",
            Body = Convert.ToHexString(capability.Span),
            IsBodyHtml = false,
            SubjectEncoding = Encoding.ASCII,
            BodyEncoding = Encoding.ASCII
        };
        // The destination is hardcoded to an IP literal. Do not use a
        // user-controlled hostname, HTTP proxy, MX lookup, or external service.
        using var sender = new SmtpClient(IPAddress.Loopback.ToString(), port)
        {
            DeliveryMethod = SmtpDeliveryMethod.Network,
            EnableSsl = false, // ONLY for this isolated disposable loopback test.
            UseDefaultCredentials = false,
            Credentials = null,
            Timeout = 5000
        };
        await sender.SendMailAsync(message, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        // SMTP 250 is acceptance by a synthetic receiver, NOT evidence of
        // inbox ownership or verified identity. The receiver must present
        // this challenge independently to the protected backend ledger.
    }

    public override string ToString() => "LoopbackOnlySmtpProofSink[redacted]";
}
