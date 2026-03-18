using Microsoft.Playwright;

var htmlPath = args.Length > 0 ? args[0] : null;
if (string.IsNullOrWhiteSpace(htmlPath) || !File.Exists(htmlPath))
{
    Console.Error.WriteLine("Usage: dotnet run -- <path-to-html-quote>");
    return 1;
}

var html = await File.ReadAllTextAsync(htmlPath);
var outPath = Path.ChangeExtension(htmlPath, ".test-output.pdf");

Console.WriteLine($"Input:  {htmlPath} ({html.Length} chars)");
Console.WriteLine($"Output: {outPath}");

using var pw = await Playwright.CreateAsync();
await using var browser = await pw.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Headless = true });
var page = await browser.NewPageAsync();

await page.SetContentAsync(html);
await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

await page.PdfAsync(new PagePdfOptions
{
    Path = outPath,
    Format = "A4",
    PrintBackground = true,
    Margin = new() { Top = "10mm", Bottom = "12mm", Left = "10mm", Right = "10mm" }
});

var fileInfo = new FileInfo(outPath);
Console.WriteLine($"PDF generated: {fileInfo.Length:N0} bytes");
Console.WriteLine("Done.");
return 0;
