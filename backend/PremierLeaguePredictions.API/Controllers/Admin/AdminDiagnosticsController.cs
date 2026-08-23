using System.Diagnostics;
using System.Net.Sockets;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Asp.Versioning;
using PremierLeaguePredictions.API.Authorization;
using PremierLeaguePredictions.Application.DTOs;

namespace PremierLeaguePredictions.API.Controllers.Admin;

/// <summary>
/// TEMPORARY. Answers one question: can this container open outbound TCP connections on the
/// SMTP ports? Render's shell is a paid feature, so the test has to run from inside the app.
/// Delete this controller once the question is settled.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/admin/diagnostics")]
[Authorize(Policy = AdminPolicies.ExternalSync)]
public class AdminDiagnosticsController : ControllerBase
{
    private static readonly TimeSpan ProbeTimeout = TimeSpan.FromSeconds(5);

    private static readonly (string Host, int Port, string Purpose)[] Probes =
    [
        ("smtp.gmail.com", 587, "SMTP submission (what the app uses)"),
        ("smtp.gmail.com", 465, "SMTP over implicit TLS"),
        ("smtp.gmail.com", 25,  "SMTP relay"),
        ("www.google.com", 443, "control — proves DNS and general egress work"),
    ];

    private readonly ILogger<AdminDiagnosticsController> _logger;

    public AdminDiagnosticsController(ILogger<AdminDiagnosticsController> logger)
    {
        _logger = logger;
    }

    [HttpGet("egress")]
    public async Task<ActionResult<ApiResponse<object>>> CheckEgress(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Running outbound connectivity probes");

        var results = new List<object>();

        foreach (var (host, port, purpose) in Probes)
        {
            var stopwatch = Stopwatch.StartNew();
            string outcome;
            string verdict;

            try
            {
                using var client = new TcpClient();
                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                timeout.CancelAfter(ProbeTimeout);

                await client.ConnectAsync(host, port, timeout.Token);

                outcome = "connected";
                verdict = "open";
            }
            catch (OperationCanceledException)
            {
                // No response at all. A firewall dropping packets looks exactly like this,
                // whereas a closed-but-reachable port answers immediately with a refusal.
                outcome = $"no response within {ProbeTimeout.TotalSeconds}s";
                verdict = "filtered — outbound traffic on this port is being dropped";
            }
            catch (SocketException ex) when (ex.SocketErrorCode == SocketError.ConnectionRefused)
            {
                outcome = "connection refused";
                verdict = "reachable, but nothing is listening";
            }
            catch (SocketException ex) when (ex.SocketErrorCode is SocketError.HostNotFound or SocketError.TryAgain)
            {
                outcome = $"DNS failure ({ex.SocketErrorCode})";
                verdict = "name resolution is broken — not a port problem";
            }
            catch (Exception ex)
            {
                outcome = $"{ex.GetType().Name}: {ex.Message}";
                verdict = "unexpected";
            }

            stopwatch.Stop();

            _logger.LogInformation("Probe {Host}:{Port} — {Outcome} after {Elapsed}ms",
                host, port, outcome, stopwatch.ElapsedMilliseconds);

            results.Add(new
            {
                target = $"{host}:{port}",
                purpose,
                outcome,
                verdict,
                elapsedMs = stopwatch.ElapsedMilliseconds
            });
        }

        return Ok(ApiResponse<object>.SuccessResult(
            new { probes = results, timeoutSeconds = ProbeTimeout.TotalSeconds },
            "Outbound connectivity probe complete"));
    }
}
