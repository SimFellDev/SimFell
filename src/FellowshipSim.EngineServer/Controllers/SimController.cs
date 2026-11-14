using System.Text.Json;
using System.Threading.Channels;
using FellowshipSim.Engine;
using Microsoft.AspNetCore.Mvc;

namespace FellowshipSim.EngineServer.Controllers;

/// <summary>Monte Carlo sims of a profile, with their progress.</summary>
[ApiController]
public sealed class SimController(ISimEngine engine, ILogger<SimController> log) : ControllerBase
{
    /// <summary>The fewest milliseconds between two progress lines.</summary>
    private const int ProgressIntervalMs = 100;

    /// <summary>
    /// Runs a sim of the request's profile and streams <see cref="SimStreamLine"/>s: progress lines, then the result or
    /// the error. A request with no fights, no duration or a target error below 0 is refused (400) before the stream starts. Closing the
    /// connection cancels the sim.
    /// </summary>
    [HttpPost("sims")]
    public async Task Simulate([FromBody] SimRequest request)
    {
        var ct = HttpContext.RequestAborted;
        if (request.Settings.Iterations <= 0 || request.Settings.DurationSeconds <= 0 || !(request.Settings.TargetError >= 0) ||
            !double.IsFinite(request.Settings.TargetError))
        {
            Response.StatusCode = StatusCodes.Status400BadRequest;
            await Response.WriteAsJsonAsync(new { error = "A sim needs at least one fight, a duration above 0 seconds and a target error of at least 0." }, ct);
            return;
        }

        Response.ContentType = SimStreamLine.ContentType;
        // Fights report on their own threads. The channel keeps the latest report, and this request's thread writes it.
        var reports = Channel.CreateBounded<SimProgress>(new BoundedChannelOptions(1) { FullMode = BoundedChannelFullMode.DropOldest });
        var sim = engine.SimulateAsync(request.Profile, request.Settings, new ChannelProgress(reports.Writer), ct);
        _ = sim.ContinueWith(_ => reports.Writer.TryComplete(), TaskScheduler.Default);

        await foreach (var progress in reports.Reader.ReadAllAsync(ct))
        {
            await WriteLine(new SimStreamLine(Progress: progress), ct);
            await Task.Delay(ProgressIntervalMs, ct);
        }

        SimStreamLine last;
        try
        {
            last = new SimStreamLine(Result: await sim);
        }
        catch (SimEngineException ex)
        {
            last = new SimStreamLine(Error: ex.Message);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            log.LogError(ex, "Sim of {Hero} failed", request.Profile.Build.Hero);
            last = new SimStreamLine(Error: $"{ex.GetType().Name}: {ex.Message}");
        }
        await WriteLine(last, ct);
    }

    /// <summary>
    /// The first fight of a sim of the request's profile with every decision of its action list, as JSON in
    /// <see cref="SimChunks.Json"/>. A request with no duration, or an action list the engine cannot run, is refused (400).
    /// </summary>
    [HttpPost("sims/trace")]
    public async Task<IActionResult> Trace([FromBody] SimRequest request, CancellationToken ct)
    {
        if (request.Settings.DurationSeconds <= 0) return BadRequest(new { error = "A fight needs a duration above 0 seconds." });
        try
        {
            var trace = await engine.TraceFightAsync(request.Profile, request.Settings, ct);
            return Content(JsonSerializer.Serialize(trace, SimChunks.Json), "application/json");
        }
        catch (SimEngineException e)
        {
            return BadRequest(new { error = e.Message });
        }
    }


    private async Task WriteLine(SimStreamLine line, CancellationToken ct)
    {
        await Response.Body.WriteAsync(JsonSerializer.SerializeToUtf8Bytes(line, SimStreamLine.Json), ct);
        await Response.Body.WriteAsync("\n"u8.ToArray(), ct);
        await Response.Body.FlushAsync(ct);
    }

    private sealed class ChannelProgress(ChannelWriter<SimProgress> writer) : IProgress<SimProgress>
    {
        public void Report(SimProgress value) => writer.TryWrite(value);
    }
}
