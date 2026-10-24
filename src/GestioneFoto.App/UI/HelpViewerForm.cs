using Markdig;

namespace GestioneFoto.App.UI;

internal sealed class HelpViewerForm : Form
{
    private readonly WebBrowser _browser;

    public HelpViewerForm(string markdownPath)
    {
        Text = $"Guida - {Path.GetFileName(markdownPath)}";
        Width = 960;
        Height = 700;
        StartPosition = FormStartPosition.CenterParent;

        _browser = new WebBrowser
        {
            Dock = DockStyle.Fill,
            ScriptErrorsSuppressed = true,
            IsWebBrowserContextMenuEnabled = true,
            WebBrowserShortcutsEnabled = true
        };

        Controls.Add(_browser);

        LoadMarkdown(markdownPath);
    }

    private void LoadMarkdown(string markdownPath)
    {
        var markdown = File.ReadAllText(markdownPath);

        var pipeline = new MarkdownPipelineBuilder()
            .UseAdvancedExtensions()
            .Build();

        var contentHtml = Markdown.ToHtml(markdown, pipeline);
        var baseDir = Path.GetDirectoryName(markdownPath) ?? Environment.CurrentDirectory;
        var baseHref = new Uri(baseDir + Path.DirectorySeparatorChar).AbsoluteUri;

        var fullHtml = $$"""
<!DOCTYPE html>
<html lang="it">
<head>
  <meta charset="utf-8" />
  <base href="{{baseHref}}" />
  <style>
    body { font-family: Segoe UI, Arial, sans-serif; margin: 24px; line-height: 1.55; color: #1f2328; background: #ffffff; }
    h1, h2, h3 { color: #0f4c81; }
    code { background: #f6f8fa; padding: 2px 4px; border-radius: 4px; }
    pre { background: #f6f8fa; padding: 12px; border-radius: 6px; overflow-x: auto; }
    table { border-collapse: collapse; }
    th, td { border: 1px solid #d0d7de; padding: 6px 8px; }
    a { color: #0969da; }
  </style>
</head>
<body>
{{contentHtml}}
</body>
</html>
""";

        _browser.DocumentText = fullHtml;
    }
}
