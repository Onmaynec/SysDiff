using System.Net;
using System.Text;
using System.Text.Json;
using SysDiff.Domain;

namespace SysDiff.Reporting;

public sealed class HtmlReportRenderer
{
    public string Render(
        SnapshotRecord before,
        SnapshotRecord after,
        ComparisonResult comparison)
    {
        string data = JsonSerializer.Serialize(comparison.Changes.Select(x => new
        {
            id = x.Id,
            provider = x.ProviderId,
            type = x.ChangeType.ToString(),
            severity = x.Severity.ToString(),
            name = x.DisplayName,
            explanation = x.Explanation,
            why = x.WhyThisMatters,
            identity = x.Identity,
            noise = x.IsNoise,
            properties = x.ChangedProperties.Select(p => new
            {
                name = p.Name,
                before = Format(p.Before),
                after = Format(p.After)
            })
        }));

        var builder = new StringBuilder();
        builder.Append(
            """
            <!doctype html>
            <html lang="ru">
            <head>
              <meta charset="utf-8">
              <meta name="viewport" content="width=device-width,initial-scale=1">
              <title>Отчёт SysDiff</title>
              <style>
                :root{color-scheme:dark;--bg:#071014;--panel:#0d1a20;--text:#dceef3;--muted:#89a8b1;--accent:#40d9d0;--line:#1d3942;--info:#7cb7ff;--low:#8bd49c;--medium:#f3c969;--high:#ff9567;--critical:#ff5f6d}
                [data-theme="light"]{color-scheme:light;--bg:#f3f8f9;--panel:#fff;--text:#122329;--muted:#506b73;--accent:#087f7a;--line:#cedde1}
                *{box-sizing:border-box}body{margin:0;background:var(--bg);color:var(--text);font:15px/1.55 system-ui,-apple-system,"Segoe UI",sans-serif}
                header{position:sticky;top:0;z-index:5;background:color-mix(in srgb,var(--bg) 92%,transparent);backdrop-filter:blur(12px);border-bottom:1px solid var(--line)}
                .wrap{max-width:1250px;margin:auto;padding:20px}.brand{display:flex;gap:14px;align-items:center}.logo{width:44px;height:44px;border:2px solid var(--accent);border-radius:12px;display:grid;place-items:center;font-weight:800;color:var(--accent)}
                h1{font-size:24px;margin:0}.sub{color:var(--muted)}.toolbar{display:grid;grid-template-columns:1fr repeat(3,auto);gap:10px;margin-top:18px}
                input,select,button{background:var(--panel);color:var(--text);border:1px solid var(--line);border-radius:9px;padding:10px 12px}button{cursor:pointer}
                .stats{display:grid;grid-template-columns:repeat(6,1fr);gap:10px;margin:18px 0}.stat{background:var(--panel);border:1px solid var(--line);border-radius:12px;padding:14px}.stat b{font-size:22px;display:block}
                .change{background:var(--panel);border:1px solid var(--line);border-left:4px solid var(--info);border-radius:10px;margin:10px 0;overflow:hidden}.change.medium{border-left-color:var(--medium)}.change.high{border-left-color:var(--high)}.change.critical{border-left-color:var(--critical)}.change.low{border-left-color:var(--low)}
                summary{cursor:pointer;padding:14px 16px;display:grid;grid-template-columns:90px 110px 1fr;gap:12px;align-items:center}.badge{font-size:12px;text-transform:uppercase;letter-spacing:.05em}.content{padding:0 16px 16px;border-top:1px solid var(--line)}table{width:100%;border-collapse:collapse;margin-top:12px}th,td{text-align:left;vertical-align:top;border-bottom:1px solid var(--line);padding:8px;word-break:break-word}code{font-family:"Cascadia Mono",Consolas,monospace;color:var(--accent)}
                .empty{text-align:center;color:var(--muted);padding:50px}@media(max-width:800px){.toolbar{grid-template-columns:1fr 1fr}.toolbar input{grid-column:1/-1}.stats{grid-template-columns:repeat(2,1fr)}summary{grid-template-columns:80px 1fr}.name{grid-column:1/-1}}
                @media print{header,.toolbar{position:static}.toolbar{display:none}.change{break-inside:avoid}body{background:#fff;color:#000}}
              </style>
            </head>
            <body>
            <header><div class="wrap">
              <div class="brand"><div class="logo">SD</div><div><h1>SysDiff</h1><div class="sub" id="subtitle"></div></div></div>
              <div class="toolbar">
                <input id="search" placeholder="Поиск по изменениям…">
                <select id="provider"><option value="">Все категории</option></select>
                <select id="severity"><option value="">Любая важность</option><option>Critical</option><option>High</option><option>Medium</option><option>Low</option><option>Info</option></select>
                <button id="theme">Сменить тему</button>
              </div>
            </div></header>
            <main class="wrap">
              <section class="stats" id="stats"></section>
              <section id="changes"></section>
            </main>
            <script>
            """);

        builder.Append("const meta=");
        builder.Append(JsonSerializer.Serialize(new
        {
            before = before.Name,
            after = after.Name,
            created = comparison.CreatedAtUtc.ToString("yyyy-MM-dd HH:mm:ss zzz"),
            hidden = comparison.HiddenAsNoise
        }));
        builder.Append(";const data=");
        builder.Append(data);
        builder.Append(
            """
            ;
            const esc=s=>String(s??"").replace(/[&<>"']/g,c=>({"&":"&amp;","<":"&lt;",">":"&gt;","\"":"&quot;","'":"&#39;"}[c]));
            const search=document.querySelector("#search"),provider=document.querySelector("#provider"),severity=document.querySelector("#severity"),changes=document.querySelector("#changes");
            document.querySelector("#subtitle").textContent=`${meta.before} → ${meta.after} · ${meta.created}`;
            [...new Set(data.map(x=>x.provider))].sort().forEach(x=>provider.insertAdjacentHTML("beforeend",`<option>${esc(x)}</option>`));
            document.querySelector("#theme").onclick=()=>{const root=document.documentElement;root.dataset.theme=root.dataset.theme==="light"?"dark":"light";};
            function render(){
              const q=search.value.toLowerCase(),p=provider.value,s=severity.value;
              const rows=data.filter(x=>(!q||JSON.stringify(x).toLowerCase().includes(q))&&(!p||x.provider===p)&&(!s||x.severity===s));
              const counts=k=>rows.filter(x=>x.severity===k).length;
              document.querySelector("#stats").innerHTML=[
                ["Показано",rows.length],["Скрыто",meta.hidden],["Critical",counts("Critical")],["High",counts("High")],["Medium",counts("Medium")],["Low",counts("Low")]
              ].map(x=>`<div class="stat"><span class="sub">${x[0]}</span><b>${x[1]}</b></div>`).join("");
              changes.innerHTML=rows.length?rows.map(x=>`<details class="change ${x.severity.toLowerCase()}">
                <summary><span class="badge">${esc(x.type)}</span><span class="badge">${esc(x.severity)}</span><strong class="name">${esc(x.name)}</strong></summary>
                <div class="content"><p>${esc(x.explanation)}</p><p><strong>Почему это важно:</strong> ${esc(x.why)}</p><code>${esc(x.identity)}</code>
                ${x.properties.length?`<table><thead><tr><th>Свойство</th><th>До</th><th>После</th></tr></thead><tbody>${x.properties.map(v=>`<tr><td>${esc(v.name)}</td><td>${esc(v.before)}</td><td>${esc(v.after)}</td></tr>`).join("")}</tbody></table>`:""}</div>
              </details>`).join(""):`<div class="empty">Изменения не найдены.</div>`;
            }
            [search,provider,severity].forEach(x=>x.addEventListener("input",render));render();
            </script></body></html>
            """);

        return builder.ToString();
    }

    private static string Format(ArtifactValue? value)
    {
        if (value is null)
        {
            return "∅";
        }

        return value.Redacted
            ? "<redacted>"
            : WebUtility.HtmlDecode(value.Value ?? "null");
    }
}
