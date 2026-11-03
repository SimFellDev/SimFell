using FellowshipSim.Core.APL;
using FellowshipSim.Engine;
using Microsoft.AspNetCore.Mvc;

namespace FellowshipSim.EngineServer.Controllers;

/// <summary>The action-list grammar: each hero's vocabulary, checking expressions, converting text and actions.</summary>
[ApiController]
[Route("apl")]
public sealed class AplController(ISimEngine engine) : ControllerBase
{
    /// <summary>The abilities, resources, auras, values and properties an action list for <paramref name="hero"/> can use.</summary>
    [HttpGet("heroes/{hero}/vocabulary")]
    public async Task<IActionResult> Vocabulary(string hero, CancellationToken ct) =>
        await engine.GetVocabularyAsync(hero, ct) is { } vocabulary ? Ok(vocabulary) : NotFound(new { error = $"No hero named {hero}." });

    /// <summary>Tokens of an expression with their kinds for <paramref name="hero"/>, and its parse error.</summary>
    [HttpPost("heroes/{hero}/check")]
    public async Task<IActionResult> Check(string hero, [FromBody] AplText request, CancellationToken ct) =>
        await engine.CheckExpressionAsync(hero, request.Text ?? "", ct) is { } check ? Ok(check) : NotFound(new { error = $"No hero named {hero}." });

    /// <summary>Tokens of an action list's text with their kinds for <paramref name="hero"/>, and its first line outside the grammar.</summary>
    [HttpPost("heroes/{hero}/check-list")]
    public async Task<IActionResult> CheckList(string hero, [FromBody] AplText request, CancellationToken ct) =>
        await engine.CheckActionListAsync(hero, request.Text ?? "", ct) is { } check ? Ok(check) : NotFound(new { error = $"No hero named {hero}." });

    /// <summary>The actions of an action list's text.</summary>
    [HttpPost("parse")]
    public async Task<IActionResult> ParseText([FromBody] AplText request, CancellationToken ct)
    {
        try
        {
            return Ok(await engine.ParseActionListAsync(request.Text ?? "", ct));
        }
        catch (SimEngineException e)
        {
            return BadRequest(new { error = e.Message });
        }
    }

    /// <summary>The text of an action list's actions, or the problem with each field that stops it being written.</summary>
    [HttpPost("write")]
    public async Task<IActionResult> Write([FromBody] AplDocumentActions request, CancellationToken ct) =>
        Ok(await engine.WriteActionListAsync(request.Actions, ct));
}
