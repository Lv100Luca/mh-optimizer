using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Unicode;
using MHWildsOptimizer.Api;
using MHWildsOptimizer.Core.Data;
using MHWildsOptimizer.StaticSite;
using MHWildsOptimizer.StaticSite.Pages;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

// Writes <wwwroot>/wiki/ (rewritten whole), sitemap.xml and 404.html. A page at /wiki/skills/agitator/ is
// wiki/skills/agitator/index.html, which Caddy's file_server and any static server serve as it is.
if (args.Length != 1)
{
    Console.Error.WriteLine("usage: MHWildsOptimizer.StaticSite <wwwroot>");
    return 2;
}
var output = Path.GetFullPath(args[0]);
var repo = FindRepo(AppContext.BaseDirectory) ?? FindRepo(Environment.CurrentDirectory)
           ?? throw new DirectoryNotFoundException("data/wiki.json not found above the program or the current directory.");

WikiData data;
await using (var stream = File.OpenRead(Path.Combine(repo, "data", "wiki.json")))
    data = await JsonSerializer.DeserializeAsync<WikiData>(stream, GameDataLoader.JsonOptions)
           ?? throw new InvalidDataException("data/wiki.json is empty.");
var site = new SiteIndex(data);

var clashes = site.Skills.Select(s => Urls.Skill(s.Name)).Concat(site.Families.Keys.Select(Urls.Family)).Concat(site.Weapons.Select(Urls.WeaponPage))
    .GroupBy(u => u).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
if (clashes.Count > 0)
{
    Console.Error.WriteLine($"Two pages would share a URL: {string.Join(", ", clashes)}");
    return 1;
}

// every page: its path, the sitemap section it counts for, the component and its parameters
var pages = new List<(string Path, string Section, Type Component, Dictionary<string, object?> Parameters)>
{
    ("/wiki/", "wiki", typeof(WikiHome), []),
    ("/wiki/skills/", "skills", typeof(SkillsIndex), []),
};
pages.AddRange(site.Skills.Select(s => (Urls.Skill(s.Name), "skills", typeof(SkillPage), new Dictionary<string, object?> { ["Skill"] = s })));
pages.Add(("/wiki/armor/", "wiki", typeof(ArmorIndex), []));
pages.AddRange(site.Families.Select(f => (Urls.Family(f.Key), "armor", typeof(ArmorFamilyPage),
    new Dictionary<string, object?> { ["Family"] = f.Key, ["Sets"] = f.Value })));
pages.Add(("/wiki/items/", "items", typeof(ItemsIndex), []));
pages.AddRange(site.Categories.Select(c => (Urls.Category(c.Key), "items", typeof(ItemCategoryPage), new Dictionary<string, object?> { ["Category"] = c })));
pages.AddRange(site.OwnPageItems.Select(i => (Urls.Item(i.Name), "items", typeof(ItemPage), new Dictionary<string, object?> { ["Item"] = i })));
pages.Add(("/wiki/meals/", "meals", typeof(MealsPage), []));
pages.Add(("/wiki/weapons/", "weapons", typeof(WeaponsIndex), []));
pages.AddRange(site.WeaponKinds.Select(k => (Urls.WeaponType(k), "weapons", typeof(WeaponTypePage), new Dictionary<string, object?> { ["Kind"] = k })));
// every weapon has a page, linked from its type's page; only the type pages are in the sitemap
pages.AddRange(site.Weapons.Select(w => (Urls.WeaponPage(w), "weapon-pages", typeof(WeaponPage), new Dictionary<string, object?> { ["Weapon"] = w })));
pages.Add(("/404.html", "404", typeof(NotFound), []));

var wikiDir = Path.Combine(output, "wiki");
if (Directory.Exists(wikiDir)) Directory.Delete(wikiDir, recursive: true);

// readable output: '·', 'γ' and '+' as they are rather than &#xB7; and the like (markup characters are still escaped)
var services = new ServiceCollection().AddLogging().AddSingleton(site)
    .AddSingleton(HtmlEncoder.Create(UnicodeRanges.All)).BuildServiceProvider();
await using var renderer = new HtmlRenderer(services, services.GetRequiredService<ILoggerFactory>());
foreach (var (path, _, component, parameters) in pages)
{
    var html = await renderer.Dispatcher.InvokeAsync(async () =>
        (await renderer.RenderComponentAsync(component, ParameterView.FromDictionary(parameters))).ToHtmlString());
    var file = path.EndsWith('/') ? Path.Combine(output, path.Trim('/'), "index.html") : Path.Combine(output, path.TrimStart('/'));
    Directory.CreateDirectory(Path.GetDirectoryName(file)!);
    await File.WriteAllTextAsync(file, "<!DOCTYPE html>\n" + html + "\n", new UTF8Encoding(false));
}

var listed = pages.Where(p => SiteIndex.SitemapSections.Contains(p.Section)).Select(p => Urls.Site + p.Path).Prepend(Urls.Site + "/").ToList();
var sitemap = new StringBuilder("<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n<urlset xmlns=\"http://www.sitemaps.org/schemas/sitemap/0.9\">\n");
foreach (var url in listed) sitemap.Append("  <url><loc>").Append(System.Security.SecurityElement.Escape(url)).Append("</loc></url>\n");
sitemap.Append("</urlset>\n");
await File.WriteAllTextAsync(Path.Combine(output, "sitemap.xml"), sitemap.ToString(), new UTF8Encoding(false));

Console.WriteLine($"{pages.Count(p => p.Section != "404")} pages in {output}, {listed.Count} in sitemap.xml");
return 0;

static string? FindRepo(string start)
{
    for (var dir = new DirectoryInfo(start); dir is not null; dir = dir.Parent)
        if (File.Exists(Path.Combine(dir.FullName, "data", "wiki.json"))) return dir.FullName;
    return null;
}
