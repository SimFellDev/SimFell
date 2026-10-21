using FellowshipSim.Core;
using FellowshipSim.Core.APL;
using FellowshipSim.Server.Services;
using Microsoft.AspNetCore.Mvc;
using Superpower;

namespace FellowshipSim.Server.Controllers;

/// <summary>The visual action-list editor: each hero's vocabulary, checking expressions, converting text and actions, saved lists.</summary>
[ApiController]
[Route("api/apl")]
public sealed class AplController(ActionListStore store) : ControllerBase
{
    /// <summary>The heroes an action list can be written for.</summary>
    [HttpGet("heroes")]
    public IActionResult Heroes() => Ok(Catalog.Heroes.Keys.Order());

    /// <summary>The abilities, resources, auras, values and properties an action list for <paramref name="hero"/> can use.</summary>
    [HttpGet("heroes/{hero}/vocabulary")]
    public IActionResult Vocabulary(string hero) =>
        Catalog.HasHero(hero) ? Ok(store.Vocabulary(hero)) : NotFound(new { error = $"No hero named {hero}." });

    public sealed record CheckRequest(string Text);

    /// <summary>Tokens of an expression with their kinds for <paramref name="hero"/>, and its parse error.</summary>
    [HttpPost("heroes/{hero}/check")]
    public IActionResult Check(string hero, [FromBody] CheckRequest request) =>
        Catalog.HasHero(hero) ? Ok(store.Vocabulary(hero).Check(request.Text ?? "")) : NotFound(new { error = $"No hero named {hero}." });

    public sealed record TextRequest(string Text);

    /// <summary>The actions of an action list's text.</summary>
    [HttpPost("parse")]
    public IActionResult ParseText([FromBody] TextRequest request)
    {
        try
        {
            return Ok(new { actions = AplDocument.Parse(request.Text ?? "") });
        }
        catch (ParseException e)
        {
            return BadRequest(new { error = e.Message });
        }
    }

    public sealed record ActionsRequest(List<AplDocumentAction> Actions);

    /// <summary>The text of an action list's actions, or the problem with each field that stops it being written.</summary>
    [HttpPost("write")]
    public IActionResult Write([FromBody] ActionsRequest request)
    {
        var (text, errors) = AplDocument.Write(request.Actions);
        return Ok(new { text, errors });
    }

    /// <summary>Every saved action list.</summary>
    [HttpGet("lists")]
    public IActionResult Lists() => Ok(store.List());

    /// <summary>A saved action list's text and actions.</summary>
    [HttpGet("lists/{hero}/{name}")]
    public IActionResult Read(string hero, string name)
    {
        if (!Catalog.HasHero(hero) || !ActionListStore.IsValidName(name)) return NotFound();
        string? text = store.Read(store.Vocabulary(hero).Hero, name);
        if (text == null) return NotFound(new { error = $"No action list named {name}." });
        try
        {
            return Ok(new { text, actions = AplDocument.Parse(text) });
        }
        catch (ParseException e)
        {
            return Ok(new { text, error = e.Message });
        }
    }

    /// <summary>Saves an action list's actions as text, unless a field does not parse.</summary>
    [HttpPut("lists/{hero}/{name}")]
    public IActionResult Save(string hero, string name, [FromBody] ActionsRequest request)
    {
        if (!Catalog.HasHero(hero)) return NotFound(new { error = $"No hero named {hero}." });
        if (!ActionListStore.IsValidName(name))
        {
            return BadRequest(new { error = "Name the list with letters, digits, spaces, '_' or '-' (up to 64 characters)." });
        }
        var (text, errors) = AplDocument.Write(request.Actions);
        if (text == null) return BadRequest(new { error = "Fix the marked fields first.", errors });
        store.Save(store.Vocabulary(hero).Hero, name, text);
        return Ok(new { text, path = Path.Combine(store.Directory, store.Vocabulary(hero).Hero, $"{name}.simc") });
    }
}
