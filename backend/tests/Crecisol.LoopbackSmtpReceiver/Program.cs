using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;

// Independent SMTP receiver PROCESS for a disposable C05-02 localhost test.
// This is NOT a production mail server, not a public endpoint and not proof
// of control of a real inbox. No secret is written to disk or ordinary logs.
// Protocol receipts on stdout are consumed only by a parent test's private
// redirected pipe and must never be copied to console, artifacts or CI logs.
using var listener = new TcpListener(IPAddress.Loopback, 0);
listener.Start();
var port = ((IPEndPoint)listener.LocalEndpoint).Port;
Console.Out.WriteLine($"READY {port}");
Console.Out.Flush();

while (true)
{
    using var client = await listener.AcceptTcpClientAsync();
    client.ReceiveTimeout = 5000;
    client.SendTimeout = 5000;
    using var stream = client.GetStream();
    using var reader = new StreamReader(stream, Encoding.ASCII, false, 1024, leaveOpen: true);
    using var writer = new StreamWriter(stream, Encoding.ASCII, 1024, leaveOpen: true)
    {
        NewLine = "\r\n", AutoFlush = true
    };
    await writer.WriteLineAsync("220 receiver.local CRECISOL synthetic-only");

    string? recipient = null;
    var fromAccepted = false;
    try
    {
        for (var commands = 0; commands < 25; commands++)
        {
            var line = await reader.ReadLineAsync().WaitAsync(TimeSpan.FromSeconds(8));
            if (line is null) break;
            if (line.Length > 512)
            {
                await writer.WriteLineAsync("500 command too long");
                break;
            }
            if (line.StartsWith("EHLO ", StringComparison.OrdinalIgnoreCase) ||
                line.StartsWith("HELO ", StringComparison.OrdinalIgnoreCase))
            {
                await writer.WriteLineAsync("250-receiver.local");
                await writer.WriteLineAsync("250 SIZE 4096");
            }
            else if (line.StartsWith("MAIL FROM:", StringComparison.OrdinalIgnoreCase))
            {
                if (!line.StartsWith("MAIL FROM:<no-reply@crecisol.example.test>",
                        StringComparison.OrdinalIgnoreCase))
                {
                    await writer.WriteLineAsync("550 synthetic sender only");
                    continue;
                }
                fromAccepted = true;
                recipient = null;
                await writer.WriteLineAsync("250 ok");
            }
            else if (line.StartsWith("RCPT TO:", StringComparison.OrdinalIgnoreCase))
            {
                var end = line.IndexOf('>');
                var candidate = end >= 0 && line.Length > 9
                    ? line[9..end] : string.Empty;
                if (!fromAccepted || recipient is not null ||
                    candidate.Length is < 13 or > 254 ||
                    candidate.Any(ch => ch is < '!' or > '~') ||
                    !candidate.EndsWith("@example.test", StringComparison.Ordinal) ||
                    candidate.Contains(' ') || candidate.Count(ch => ch == '@') != 1)
                {
                    await writer.WriteLineAsync("550 invalid synthetic recipient");
                    continue;
                }
                recipient = candidate;
                await writer.WriteLineAsync("250 ok");
            }
            else if (line.Equals("DATA", StringComparison.OrdinalIgnoreCase))
            {
                if (!fromAccepted || recipient is null)
                {
                    await writer.WriteLineAsync("503 need envelope");
                    continue;
                }
                await writer.WriteLineAsync("354 end with dot");
                var total = 0;
                var inBody = false;
                string? hex = null;
                var completed = false;
                while (total < 4096)
                {
                    var data = await reader.ReadLineAsync().WaitAsync(
                        TimeSpan.FromSeconds(8));
                    if (data is null) break;
                    total += data.Length + 2;
                    if (data == ".")
                    {
                        completed = true;
                        break;
                    }
                    if (data.Length == 0)
                    {
                        inBody = true;
                        continue;
                    }
                    if (inBody && hex is null &&
                        Regex.IsMatch(data, @"\A[A-F0-9]{64}\z",
                            RegexOptions.CultureInvariant))
                        hex = data;
                }
                if (!completed || hex is null || total >= 4096)
                {
                    await writer.WriteLineAsync("554 invalid synthetic message");
                    recipient = null;
                    fromAccepted = false;
                    continue;
                }
                await writer.WriteLineAsync("250 accepted in independent test receiver");
                // This capability leaves the sender process through actual
                // SMTP over TCP. Only the parent harness receives stdout.
                Console.Out.WriteLine($"MAIL {recipient} {hex}");
                Console.Out.Flush();
                recipient = null;
                fromAccepted = false;
            }
            else if (line.Equals("RSET", StringComparison.OrdinalIgnoreCase))
            {
                recipient = null;
                fromAccepted = false;
                await writer.WriteLineAsync("250 reset");
            }
            else if (line.Equals("NOOP", StringComparison.OrdinalIgnoreCase))
                await writer.WriteLineAsync("250 ok");
            else if (line.Equals("QUIT", StringComparison.OrdinalIgnoreCase))
            {
                await writer.WriteLineAsync("221 bye");
                break;
            }
            else
                await writer.WriteLineAsync("500 unsupported");
        }
    }
    catch (Exception)
    {
        // A protocol error fails only this local SMTP session. Never disclose
        // the mailbox or token in stderr or a network error response.
    }
}
