using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using Nashi.Core;
namespace Nashi.Windows;

public sealed class ToolSetupWindow : Window
{
    private readonly Func<ToolSetupStatus> probe;
    private readonly Func<ToolGroup, IProgress<OperationProgress>, CancellationToken, Task> install;
    private readonly Action<bool> workingChanged;
    private readonly StackPanel groups = new();
    private readonly TextBlock status = Label("Dönüştürme, önizleme ve indirme özelliklerini etkinleştirmek için eksik araçları kurun. Bilgisayarınızdaki mevcut araçlar korunur.");
    private readonly ProgressBar progress = new() { Minimum = 0, Maximum = 1, Height = 6, Visibility = Visibility.Collapsed, Margin = new(0, 12, 0, 12) };
    private readonly Button next, cancel;
    private CancellationTokenSource? cancellation;
    internal bool Working { get; private set; }
    private bool previouslyReady;
    internal ToolSetupStatus Status => probe();
    internal Button ContinueButton => next;
    public ToolSetupWindow(Window owner, Toolchain media, OfficeRuntime office, Action<bool> workingChanged)
        : this(owner, () => new(media.Ready, office.Executable is not null),
            (group, report, token) => Task.Run(() => group == ToolGroup.Media ? media.InstallAsync(report, token) : office.InstallAsync(report, token),token), workingChanged) { }
    internal ToolSetupWindow(Window owner, Func<ToolSetupStatus> probe,
        Func<ToolGroup, IProgress<OperationProgress>, CancellationToken, Task> install, Action<bool>? workingChanged = null)
    {
        this.probe = probe; this.install = install; this.workingChanged = workingChanged ?? (_ => { });
        Owner = owner; Theme.Attach(this); Title = "Nara araçlarını kurun"; Width = 620; Height = 720; MinWidth = 590; MinHeight = 600; WindowStartupLocation = WindowStartupLocation.CenterOwner;
        L10n.Bind(this,TitleProperty,"Nara araçlarını kurun");
        var root = new DockPanel(); Content = root;
        var footer = new StackPanel { Margin = new(24) }; DockPanel.SetDock(footer, Dock.Bottom); root.Children.Add(footer);
        footer.Children.Add(status); footer.Children.Add(progress);
        var actions = new DockPanel { Margin = new(0, 14, 0, 0) }; footer.Children.Add(actions);
        next = MainWindow.Primary("Eksik araçları indir", async () => { if (Status.Ready) Close(); else await StartSetupAsync(); }); next.IsDefault = true;
        AutomationProperties.SetAutomationId(next, "SetupContinue"); DockPanel.SetDock(next, Dock.Right); actions.Children.Add(next);
        cancel = MainWindow.Button("İptal", () => cancellation?.Cancel()); AutomationProperties.SetAutomationId(cancel, "SetupCancel"); actions.Children.Add(cancel);
        var content = new StackPanel { Margin = new(28) }; root.Children.Add(new ScrollViewer { Content = content, VerticalScrollBarVisibility = ScrollBarVisibility.Auto });
        content.Children.Add(Label("İLK KURULUM", 11)); content.Children.Add(Label("Nara araçlarını kurun", 28));
        content.Children.Add(Label("Birkaç araçla daha fazla özellik. Hesap veya GitHub tokenı gerekmez.", 14)); content.Children.Add(groups);
        content.Children.Add(MainWindow.Button("Sürümler, lisanslar ve kaynak kodları", () => MainWindow.OpenUrl("https://github.com/berkberker17/nara-app/blob/nara-branding/docs/dependencies.md")));
        Closing += (_, e) => { if (Working) { e.Cancel = true; cancellation?.Cancel(); } };
        Activated += (_, _) => { if (!Working) Refresh(); };
        PreviewKeyDown += (_, e) => { if (e.Key == Key.Escape) { if (Working) cancellation?.Cancel(); else Close(); e.Handled = true; } };
        Refresh();
    }
    private static TextBlock Label(string text, double size = 13) => L10n.Label(new TextBlock { Text = text, FontSize = size, FontWeight = size >= 18 ? FontWeights.SemiBold : FontWeights.Normal, TextWrapping = TextWrapping.Wrap, Margin = new(0, 6, 0, 8) });
    private void Refresh()
    {
        var ready = Status; groups.Children.Clear();
        Group("Medya araçları", "FFmpeg, ffprobe, yt-dlp ve Deno; medya dönüştürme, önizleme ve web indirme özelliklerini etkinleştirir. Yaklaşık 180 MB. FFmpeg ve yt-dlp GPL, Deno MIT lisanslıdır.", ready.MediaReady, ToolGroup.Media);
        Group("Ofis araçları", "LibreOffice 26.8.1, ofis belgelerini dönüştürmeyi sağlar. Yaklaşık 360 MB. MPL 2.0 ve üçüncü taraf lisansları geçerlidir.", ready.OfficeReady, ToolGroup.Office);
        L10n.Bind(next, ContentControl.ContentProperty, ready.Ready ? "Nara’ya devam et" : "Eksik araçları indir"); next.IsEnabled = !Working;
        cancel.Visibility = Working ? Visibility.Visible : Visibility.Collapsed; progress.Visibility = Working ? Visibility.Visible : Visibility.Collapsed;
        if (ready.Ready && !Working) L10n.SetText(status, "Hazır. Dosyalarınıza geri dönebilirsiniz.");
        else if(previouslyReady&&!Working)L10n.SetText(status,"Dönüştürme, önizleme ve indirme özelliklerini etkinleştirmek için eksik araçları kurun. Bilgisayarınızdaki mevcut araçlar korunur.");
        previouslyReady=ready.Ready;
    }
    private void Group(string title, string detail, bool ready, ToolGroup group)
    {
        var row = new StackPanel { Margin = new(14) }; row.Children.Add(Label(title, 18)); row.Children.Add(Label(detail));
        if (ready) row.Children.Add(Label("Bilgisayarınızda zaten mevcut"));
        else
        {
            var download = MainWindow.Button(group == ToolGroup.Media ? "Medya araçlarını indir" : "Ofis araçlarını indir", async () => await StartSetupAsync(group));
            download.IsEnabled = !Working; AutomationProperties.SetAutomationId(download, group == ToolGroup.Media ? "SetupMedia" : "SetupOffice"); row.Children.Add(download);
        }
        var card = new Border { Child = row, CornerRadius = new(16), BorderThickness = new(1), Margin = new(0, 12, 0, 8) };
        card.SetResourceReference(Border.BackgroundProperty, "Surface"); card.SetResourceReference(Border.BorderBrushProperty, "Line"); groups.Children.Add(card);
    }
    internal async Task StartSetupAsync(ToolGroup? selected = null)
    {
        if (Working) return;
        var pending = Status.Missing.Where(g => selected is null || g == selected).ToArray(); if (pending.Length == 0) { Refresh(); return; }
        Working = true; cancellation = new(); workingChanged(true); Refresh(); progress.IsIndeterminate = true;
        var token = cancellation.Token;
        try
        {
            for (var i = 0; i < pending.Length; i++)
            {
                token.ThrowIfCancellationRequested(); var index = i;
                var report = new Progress<OperationProgress>(p => { if (!Working || token.IsCancellationRequested) return; progress.IsIndeterminate = p.Fraction is null; if (p.Fraction is {} value) progress.Value = (index + value) / pending.Length; L10n.SetText(status, p.Label); });
                await install(pending[i], report, token); Refresh();
            }
            if (Status.Missing.Any(pending.Contains)) throw new InvalidDataException("The downloaded tools are incomplete. Please try setup again.");
            L10n.SetText(status, Status.Ready ? "Ready. You can go back to your files." : "Bir araç grubu hazır. Kurulumu tamamlamak için diğer araçları da indirin.");
        }
        catch (OperationCanceledException) { L10n.SetText(status, "Kurulum iptal edildi. Mevcut araçlar korundu."); }
        catch (Exception error) { L10n.SetText(status, error.Message); }
        finally { Working = false; cancellation.Dispose(); cancellation = null; workingChanged(false); Refresh(); }
    }
}
