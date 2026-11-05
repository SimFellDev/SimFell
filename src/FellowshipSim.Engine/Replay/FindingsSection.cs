using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;

namespace FellowshipSim.Engine.Replay;

/// <summary>
/// The review page's Findings section: a table per <see cref="FindingGroup"/> with one row for each finding, ranked as
/// <see cref="ReplayFindings"/> ranks them. A row opens to the finding's numbers, its timeline and its example issues, each
/// with its <see cref="EngineState"/> expandable below it. The states are one JSON block with each aura's name, log ID and
/// modifiers listed once, drawn by the page's script when a row opens.
/// </summary>
internal static class FindingsSection
{
    private const int TimelineBins = 120;

    /// <summary>Renders the section and returns the steps its examples link to.</summary>
    public static HashSet<int> Render(StringBuilder sb, IReadOnlyList<Finding> findings, ReplayResult r)
    {
        var linkedSteps = new HashSet<int>();
        var states = new List<(ReplayIssue Issue, EngineState State)>();
        int issues = findings.Sum(f => f.Issues.Count);
        sb.Append($"<h2>Findings ({findings.Count:N0})</h2><div class=\"muted\">{issues:N0} issues, one finding for each damage ID, resource, aura, ability and proc, in the order of work of docs/REPLAY_ACCURACY.md. A row opens to its numbers and examples. The review's issues.csv has every issue, and its findings.json has these findings for an agent.</div>");

        if (r.UnmodeledAbilities.Count > 0 || r.UnmodeledCustomizations.Count > 0)
        {
            sb.Append("<h3>Unmodeled content</h3><div class=\"panel\">");
            if (r.UnmodeledAbilities.Count > 0)
            {
                sb.Append($"<div>Abilities: {E(string.Join(", ", r.UnmodeledAbilities.OrderByDescending(kv => kv.Value).Select(kv => $"{kv.Key} ({kv.Value:N0} casts)")))}</div>");
            }
            if (r.UnmodeledCustomizations.Count > 0) sb.Append($"<div>Customizations: {E(string.Join(", ", r.UnmodeledCustomizations))}</div>");
            sb.Append("</div>");
        }

        foreach (var group in findings.GroupBy(f => f.Group))
        {
            bool collapsed = group.Key == FindingGroup.WithinTargets;
            if (collapsed) sb.Append($"<details class=\"panel\"><summary>{GroupTitle(group.Key)} ({group.Count():N0})</summary>");
            else sb.Append($"<h3>{GroupTitle(group.Key)} ({group.Count():N0})</h3><div class=\"muted\">{GroupNote(group.Key)}</div>");
            sb.Append($"<div class=\"{(collapsed ? "scroll" : "panel scroll")}\"><table class=\"findings\"><thead><tr><th>Subject</th><th class=num>ID</th><th class=num>Impact</th><th class=num>Issues</th><th>Numbers</th><th class=num>First</th></tr></thead><tbody>");
            foreach (var finding in group) RenderFinding(sb, finding, r, states, linkedSteps);
            sb.Append("</tbody></table></div>");
            if (collapsed) sb.Append("</details>");
        }

        sb.Append("<script type=\"application/json\" id=\"engine-states\">");
        sb.Append(StatesJson(states));
        sb.Append("</script><script>");
        sb.Append(Script);
        sb.Append("</script>");
        return linkedSteps;
    }

    private static string GroupTitle(FindingGroup group) => group switch
    {
        FindingGroup.UnmodeledContent => "Damage with no engine hit",
        FindingGroup.Calibration => "Calibration",
        FindingGroup.HitCounts => "Hit counts",
        FindingGroup.ReplayPairing => "Replay pairing",
        FindingGroup.Ticks => "Ticks",
        FindingGroup.Resources => "Resources",
        FindingGroup.Auras => "Auras",
        FindingGroup.CastsAndCooldowns => "Casts and cooldowns",
        FindingGroup.Procs => "Procs",
        _ => "Within the targets",
    };

    private static string GroupNote(FindingGroup group) => group switch
    {
        FindingGroup.UnmodeledContent => "Damage IDs in the log with no engine hit at all.",
        FindingGroup.Calibration => "Mean log/engine off 1.00, or ratios wider than the damage spread. Impact is log damage x |1 - ratio| over the log's damage.",
        FindingGroup.HitCounts => "Engine-only and missing hits. Impact is their damage over the log's damage.",
        FindingGroup.ReplayPairing => "Out-of-range hits fit another engine hit of their release, and release totals match the log: the replay paired log hits with the wrong engine hits, so single hits' ratios are not the game's. Impact is the release totals' offset and the unmatched hits. The place to read is LogOutcomeSource.",
        FindingGroup.Ticks => "Damage IDs inside their damage targets with periodic tick issues.",
        FindingGroup.Resources => "The replay takes the log's reading after every check, so each issue is one step's difference. Net is what the engine gains too little or too much over the fight.",
        FindingGroup.Auras => "The replay takes the log's stacks after every check, so each issue is one step's difference.",
        FindingGroup.CastsAndCooldowns => "Log casts the engine refuses or times differently.",
        FindingGroup.Procs => "Proc rolls the log and the engine disagree on.",
        _ => "",
    };

    private static void RenderFinding(StringBuilder sb, Finding f, ReplayResult r, List<(ReplayIssue Issue, EngineState State)> states, HashSet<int> linkedSteps)
    {
        sb.Append($"<tr class=\"finding\" tabindex=\"0\"><td>{E(f.Name)}</td><td class=num>{(f.Entity.Id > 0 ? f.Entity.Id : "—")}</td>");
        sb.Append($"<td class=num>{(f.Impact is { } impact ? impact.ToString("0.00%", CultureInfo.InvariantCulture) : "—")}</td><td class=num>{f.Issues.Count:N0}</td>");
        sb.Append($"<td class=\"wrap\">{E(f.Headline)}</td><td class=num>{(f.Issues.Count > 0 ? $"{f.Issues.Min(i => i.Time):F1}s" : "—")}</td></tr>");

        sb.Append("<tr class=\"finding-detail\" hidden><td colspan=\"6\">");
        if (f.Issues.Count > 0)
        {
            sb.Append("<strong>Issues over the fight</strong>");
            Timeline(sb, f.Issues, r.DurationSeconds);
        }
        if (f.Facts.Count > 0)
        {
            sb.Append("<ul>");
            foreach (string fact in f.Facts) sb.Append($"<li>{E(fact)}</li>");
            sb.Append("</ul>");
        }
        if (f.Splits.Count > 0)
        {
            sb.Append("<strong>Conditions dividing the ratio</strong><div class=\"scroll\"><table><thead><tr><th>Condition</th><th class=num>Ratio with</th><th class=num>Hits</th><th class=num>Ratio without</th><th class=num>Hits</th></tr></thead><tbody>");
            foreach (var s in f.Splits)
            {
                sb.Append($"<tr><td class=\"wrap\">{E(s.Condition)}</td><td class=num>{s.RatioWith:F3}</td><td class=num>{s.HitsWith:N0}</td><td class=num>{s.RatioWithout:F3}</td><td class=num>{s.HitsWithout:N0}</td></tr>");
            }
            sb.Append("</tbody></table></div>");
        }
        if (f.Examples.Count > 0)
        {
            sb.Append("<div class=\"muted\">");
            sb.Append(E(string.Join(" · ", f.Issues.GroupBy(ReplayFindings.Kind).OrderByDescending(g => g.Count()).Select(g => $"{g.Count():N0} {g.Key}"))));
            sb.Append("</div><div class=\"scroll\"><table class=\"issues\"><thead><tr><th class=num>Time</th><th class=num>Step</th><th>Example</th><th class=num>Expected</th><th class=num>Observed</th><th class=num>Deviation</th><th class=num>Relative</th><th></th></tr></thead><tbody>");
            foreach (var i in f.Examples)
            {
                linkedSteps.Add(i.Step);
                string unit = i.Expected?.Unit ?? "";
                sb.Append($"<tr><td class=num>{i.Time:F3}</td><td class=num><a href=\"#step-{i.Step}\">{i.Step}</a></td><td class=\"wrap\">{E(i.Message)}</td>");
                sb.Append($"<td class=num>{(i.Expected is { } e ? E(e.ToString()) : "—")}</td>");
                sb.Append($"<td class=num>{(i.Observed is { } o ? E(ExpectedRange.Format(o, unit)) : "—")}</td>");
                sb.Append($"<td class=num>{(i.Deviation is { } d ? E((d > 0 ? "+" : "") + ExpectedRange.Format(d, unit)) : "—")}</td>");
                sb.Append($"<td class=num>{(i.RelativeDeviation is { } rel ? rel.ToString("+0.0%;-0.0%;0.0%", CultureInfo.InvariantCulture) : "—")}</td>");
                if (i.State is { } state)
                {
                    sb.Append($"<td><button class=\"state\" data-s=\"{states.Count}\">Engine state</button></td></tr>");
                    states.Add((i, state));
                }
                else sb.Append("<td></td></tr>");
            }
            sb.Append("</tbody></table></div>");
        }
        sb.Append("</td></tr>");
    }

    /// <summary>A strip of the fight with a bar for each stretch holding issues: steady issues fill it, a pack's stand out.</summary>
    private static void Timeline(StringBuilder sb, IReadOnlyList<ReplayIssue> issues, double duration)
    {
        const double Height = 24;
        var bins = new int[TimelineBins];
        double seconds = Math.Max(1.0, Math.Max(duration, issues.Max(i => i.Time)));
        foreach (var issue in issues) bins[Math.Clamp((int)(issue.Time / seconds * TimelineBins), 0, TimelineBins - 1)]++;
        int peak = bins.Max();
        sb.Append($"<svg class=\"timeline\" viewBox=\"0 0 {TimelineBins} {Height:F0}\" preserveAspectRatio=\"none\" role=\"img\"><rect class=\"track\" width=\"{TimelineBins}\" height=\"{Height:F0}\"/>");
        for (int i = 0; i < TimelineBins; i++)
        {
            if (bins[i] == 0) continue;
            double height = Math.Max(2.0, Height * bins[i] / peak);
            sb.Append(FormattableString.Invariant($"<rect x=\"{i}\" y=\"{Height - height:F1}\" width=\"0.9\" height=\"{height:F1}\"/>"));
        }
        sb.Append($"</svg><div class=\"timeline-axis muted\"><span>0:00</span><span>{(int)seconds / 60}:{(int)seconds % 60:00}</span></div>");
    }

    /// <summary>
    /// <c>{"a": [[name, logId, [modifier, ...]], ...], "s": [state, ...]}</c>. Each state is
    /// <c>[time, message, hero, target, hit, cast]</c>, and each aura in it <c>[aura index, stacks, seconds left]</c>.
    /// </summary>
    private static string StatesJson(List<(ReplayIssue Issue, EngineState State)> states)
    {
        var auraIndex = new Dictionary<(string Name, int LogId, IReadOnlyList<StatModifier> Modifiers), int>();
        var auraList = new List<(string Name, int LogId, IReadOnlyList<StatModifier> Modifiers)>();
        using var stream = new MemoryStream();
        using (var w = new Utf8JsonWriter(stream))
        {
            void Number(double v) => w.WriteNumberValue(Math.Round(v, 4));
            void NullableNumber(double? v) { if (v is { } x) Number(x); else w.WriteNullValue(); }
            void Auras(IReadOnlyList<EngineAura> auras)
            {
                w.WriteStartArray();
                foreach (var a in auras)
                {
                    var key = (a.Name, a.LogId, a.Modifiers);
                    if (!auraIndex.TryGetValue(key, out int index))
                    {
                        auraIndex[key] = index = auraList.Count;
                        auraList.Add(key);
                    }
                    w.WriteStartArray();
                    w.WriteNumberValue(index);
                    w.WriteNumberValue(a.Stacks);
                    NullableNumber(a.SecondsLeft);
                    w.WriteEndArray();
                }
                w.WriteEndArray();
            }

            w.WriteStartObject();
            w.WritePropertyName("s");
            w.WriteStartArray();
            foreach (var (issue, s) in states)
            {
                w.WriteStartArray();
                Number(s.Time);
                w.WriteStringValue(issue.Message);

                var h = s.Hero;
                w.WriteStartArray();
                foreach (var v in new[] { h.PrimaryAttribute, h.CritChance, h.CritChanceAgainstTarget, h.CritMultiplier, h.Haste, h.Expertise, h.Spirit, h.DamageScale, h.GlobalCooldownEnd }) Number(v);
                w.WriteStringValue(string.Join(", ", h.Resources.Select(x => $"{x.Name} {x.Current.ToString("0.##", CultureInfo.InvariantCulture)}/{x.Max.ToString("0.##", CultureInfo.InvariantCulture)}")));
                w.WriteStringValue(h.CurrentCast);
                w.WriteStringValue(h.CurrentChannel);
                w.WriteNumberValue(h.ActiveEnemies);
                Auras(h.Auras);
                w.WriteEndArray();

                if (s.Target is { } t)
                {
                    w.WriteStartArray();
                    w.WriteStringValue($"{t.Name} ({t.Id}.{t.InstanceId})");
                    Number(t.HealthFraction);
                    Number(t.IncomingDamageMultiplier);
                    Auras(t.Auras);
                    w.WriteEndArray();
                }
                else w.WriteNullValue();

                if (s.Hit is { } x)
                {
                    w.WriteStartArray();
                    w.WriteStringValue($"{x.AbilityName} ({x.AbilityId})");
                    Number(x.BaseDamage);
                    NullableNumber(x.HitDamageMultiplier);
                    NullableNumber(x.AbilityDamageMultiplier);
                    Number(x.TargetDamageMultiplier);
                    Number(x.CritChance);
                    NullableNumber(x.CastCritChance);
                    NullableNumber(x.ExtraCritChance);
                    Number(x.CritMultiplier);
                    Number(x.TickFraction);
                    w.WriteBooleanValue(x.Snapshot == true);
                    Number(x.ReleaseTime);
                    NullableNumber(x.LandingTime);
                    w.WriteEndArray();
                }
                else w.WriteNullValue();

                if (s.Cast is { } c)
                {
                    w.WriteStartArray();
                    w.WriteStringValue(c.Ability);
                    Number(c.CastTime);
                    Number(c.GlobalCooldown);
                    w.WriteBooleanValue(c.GlobalCooldownScalesWithHaste);
                    Number(c.CooldownLeft);
                    w.WriteNumberValue(c.Charges);
                    Number(c.CooldownRate);
                    w.WriteStringValue(c.Refusal);
                    w.WriteEndArray();
                }
                else w.WriteNullValue();
                w.WriteEndArray();
            }
            w.WriteEndArray();

            w.WritePropertyName("a");
            w.WriteStartArray();
            foreach (var (name, logId, modifiers) in auraList)
            {
                w.WriteStartArray();
                w.WriteStringValue(name);
                w.WriteNumberValue(logId);
                w.WriteStartArray();
                foreach (var m in modifiers) w.WriteStringValue($"{m.Type} {m.Op} {m.Value.ToString("0.####", CultureInfo.InvariantCulture)}");
                w.WriteEndArray();
                w.WriteEndArray();
            }
            w.WriteEndArray();
            w.WriteEndObject();
        }
        // The default encoder escapes '<', so the block cannot close its script element early.
        return Encoding.UTF8.GetString(stream.ToArray());
    }

    /// <summary>Opens a finding's row and an example's engine state below it, closes them again, and opens a linked step.</summary>
    private const string Script = """
(()=>{const root=window.reviewRoot??document,d=JSON.parse(root.querySelector('#engine-states').textContent),A=d.a;
const e=s=>String(s??'').replace(/[&<>"]/g,c=>({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;'}[c]));
const p=v=>(v*100).toFixed(2)+'%',f=v=>v==null?'—':String(v),t=v=>v==null?'never':v.toFixed(3)+'s';
const auras=l=>l.length?l.map(([i,st,left])=>{const[n,id,m]=A[i];return e(n)+(id>0?' ('+id+')':'')+' x'+st+(left==null?'':', '+left.toFixed(3)+'s left')+(m.length?'<span class="muted"> — '+e(m.map(x=>n+' x'+st+': '+x).join('; '))+'</span>':'')}).join('<br>'):'none';
const draw=([time,msg,h,g,x,c])=>{let o='<div class="muted">'+e(msg)+'</div><div class="grid"><div><strong>Hero</strong> <span class="muted">@ '+time.toFixed(3)+'s</span><br>'
+'Primary attribute '+h[0]+' · crit '+p(h[1])+' ('+p(h[2])+' against the target) · crit multiplier '+h[3]+'<br>'
+'Haste '+p(h[4])+' · Expertise '+p(h[5])+' · Spirit '+p(h[6])+' · damage scale '+h[7]+'<br>Resources: '+e(h[9])+'<br>'
+'Global cooldown ends '+h[8].toFixed(3)+'s · casting '+e(h[10]??'nothing')+' · channeling '+e(h[11]??'nothing')+' · '+h[12]+' active enemies<br>Auras:<br>'+auras(h[13])+'</div>';
if(g)o+='<div><strong>Target</strong> '+e(g[0])+'<br>Health '+p(g[1])+' · incoming damage multiplier '+g[2]+'<br>Auras:<br>'+auras(g[3])+'</div>';
if(x)o+='<div><strong>Hit</strong> '+e(x[0])+'<br>Base damage '+x[1].toFixed(1)+' · tick fraction '+x[9]+(x[10]?' · snapshot':'')+'<br>Hit damage multiplier '+f(x[2])+' · ability damage multiplier '+f(x[3])+' · target damage multiplier '+x[4]+'<br>'
+'Crit chance '+p(x[5])+' (cast '+(x[6]==null?'—':p(x[6]))+', hit '+(x[7]==null?'—':p(x[7]))+') · crit multiplier '+x[8]+'<br>Released '+t(x[11])+' · landed '+t(x[12])+'</div>';
if(c)o+='<div><strong>Cast</strong> '+e(c[0])+'<br>Cast time '+c[1].toFixed(3)+'s · global cooldown '+c[2]+'s'+(c[3]?' (hasted)':'')+'<br>Cooldown left '+c[4].toFixed(3)+'s · '+c[5]+' charges · cooldown rate '+c[6]+(c[7]?'<br>Refusal: '+e(c[7]):'')+'</div>';
return o+'</div>'};
const toggle=row=>{const d=row.nextElementSibling;d.hidden=!d.hidden;row.classList.toggle('open',!d.hidden)};
root.addEventListener('keydown',ev=>{if(ev.key==='Enter'&&ev.target.matches('tr.finding'))toggle(ev.target)});
const openStep=t=>{for(let p=t;p;p=p.parentElement?.closest('details'))p.open=true};
if(location.hash.startsWith('#step-'))openStep(root.querySelector(location.hash));
root.addEventListener('click',ev=>{const link=ev.target.closest('a[href^="#step-"]');if(link){openStep(root.querySelector(link.getAttribute('href')));return}const finding=ev.target.closest('tr.finding');if(finding){toggle(finding);return}const b=ev.target.closest('button.state');if(!b)return;const row=b.closest('tr'),next=row.nextElementSibling;
if(next&&next.classList.contains('state-row')){next.remove();return}
const r=document.createElement('tr');r.className='state-row';r.innerHTML='<td colspan="8">'+draw(d.s[+b.dataset.s])+'</td>';row.after(r)})})();
""";

    private static string E(string s) => WebUtility.HtmlEncode(s);
}
