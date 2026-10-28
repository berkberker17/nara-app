using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using Nashi.Core;
namespace Nashi.Windows;

public sealed partial class MainWindow:Window
{
    private readonly ObservableCollection<string> files=[];
    private readonly ListBox list=new(){MinHeight=0,MaxHeight=110};
    private readonly Grid actions=new();
    private readonly TextBlock selection=new(){FontSize=23,FontWeight=FontWeights.Bold,TextWrapping=TextWrapping.Wrap};
    private readonly TextBlock status=Text("Dosyalarınız bilgisayarınızda kalır.",12,"Muted");
    private readonly TextBox link=new(){ToolTip="Paste a public video, image or audio page link"};
    private readonly ComboBox downloadKind=new(){ItemsSource=new[]{"video","audio","thumbnail"},SelectedIndex=0,MinWidth=110};
    private readonly Toolchain tools=new(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Nara","Tools","v1"));
    private readonly LibraryStore library;
    private readonly PreferencesStore preferences;private GlobalDragController? globalDrag;
    private readonly StackPanel home=new(),libraryPage=new(),libraryRows=new(),recentRows=new();
    private readonly TextBox recentSearch=new(){ToolTip="Son dosyalarda ara",Width=165};private readonly TextBlock recentCount=Text("0",11,"Muted");private Button? chooseFilesButton,openWheelButton;private readonly TextBlock fileDescription=Text("Görseller, videolar, belgeler ve daha fazlası.",12,"Muted");
    private readonly TextBox search=new(){ToolTip="Find a file or tool in Library",MinWidth=200};
    private readonly DockPanel manage=new();private readonly FileGarden garden=new();private readonly StackPanel webContent=new();private Window? webWindow;
    internal Window DownloadWindow=>webWindow??this;
    private readonly Button homeButton,libraryButton,updateButton,toolsSetupButton;private readonly ComboBox appearance;
    private readonly StackPanel workArea=new();
    private readonly ScrollViewer homeScroll=new(),libraryScroll=new();
    private bool busy,checking,toolSetupOffered;private WindowsUpdate? update;private Border? updateBanner;
    private ToolSetupWindow? toolSetupWindow;
    private readonly bool monitorToolSetup;
    internal bool UpdateBannerVisible=>updateBanner?.Visibility==Visibility.Visible;
    internal IReadOnlyList<string> SelectedFiles=>files;
    internal bool LibraryVisible=>libraryPage.Visibility==Visibility.Visible;
    internal TextBox SearchField=>search;
    internal TextBox LinkField=>link;
    public MainWindow(string[]? initial=null,LibraryStore? library=null,bool checkForUpdates=true,bool offerToolSetup=true)
    {
        monitorToolSetup=checkForUpdates;
        this.library=library??new LibraryStore();preferences=new(this.library.DirectoryPath);ApplyPreferences();Theme.Apply("light");Theme.Attach(this);
        Title="Nara";Width=1180;Height=800;MinWidth=940;MinHeight=640;WindowStartupLocation=WindowStartupLocation.CenterScreen;AllowDrop=true;
        Icon=NaraArtwork.Icon();
        var root=new DockPanel{Margin=new(0)};Content=root;AddHandler(UIElement.PreviewMouseWheelEvent,new MouseWheelEventHandler(RoutePageMouseWheel),true);
        var sidebar=new Border{Width=190,Padding=new(18,22,14,18),BorderThickness=new(0,0,1,0)};
        sidebar.SetResourceReference(Border.BackgroundProperty,"Surface");sidebar.SetResourceReference(Border.BorderBrushProperty,"Line");DockPanel.SetDock(sidebar,Dock.Left);root.Children.Add(sidebar);
        var side=new DockPanel{LastChildFill=false};sidebar.Child=side;
        var sideBrand=new StackPanel{Orientation=Orientation.Horizontal,Margin=new(0,0,0,26)};
        sideBrand.Children.Add(new Image{Source=NaraArtwork.Mark(),Width=42,Height=42,Margin=new(0,0,8,0)});
        sideBrand.Children.Add(Text("Nara",22));DockPanel.SetDock(sideBrand,Dock.Top);side.Children.Add(sideBrand);
        var sideNav=new StackPanel();DockPanel.SetDock(sideNav,Dock.Top);side.Children.Add(sideNav);
        var header=new DockPanel{Margin=new(28,18,28,18)};DockPanel.SetDock(header,Dock.Top);root.Children.Add(header);
        var controls=new WrapPanel{HorizontalAlignment=HorizontalAlignment.Right,VerticalAlignment=VerticalAlignment.Center};DockPanel.SetDock(controls,Dock.Right);header.Children.Add(controls);
        homeButton=Button("",()=>ShowLibrary(false));homeButton.Content=NavContent("\uE80F","Ana Sayfa");libraryButton=Button("Görüntüle",()=>ShowLibrary(true));
        homeButton.Margin=new(0,3,0,3);libraryButton.Margin=new(0,3,0,3);homeButton.HorizontalAlignment=HorizontalAlignment.Stretch;libraryButton.HorizontalAlignment=HorizontalAlignment.Stretch;homeButton.HorizontalContentAlignment=HorizontalAlignment.Left;libraryButton.HorizontalContentAlignment=HorizontalAlignment.Left;
        homeButton.Width=150;libraryButton.Width=150;
        homeButton.Padding=new(14,11,10,11);libraryButton.Padding=new(14,11,10,11);
        sideNav.Children.Add(homeButton);
        UIElement NavContent(string glyph,string label){var row=new StackPanel{Orientation=Orientation.Horizontal,VerticalAlignment=VerticalAlignment.Center};var icon=Text(glyph,16,"Ink",false);icon.FontFamily=new FontFamily("Segoe MDL2 Assets");icon.Width=20;icon.Margin=new(0,0,12,0);row.Children.Add(icon);row.Children.Add(Text(label,13));return row;}
        Button Nav(string glyph,string label,Action action){var b=Button("",action);b.Content=NavContent(glyph,label);return b;}
        var convertNav=Nav("\uE72C","Dönüştür",Choose);var downloadNav=Nav("\uE896","İndir",OpenDownload);var viewNav=Nav("\uE8B7","Görüntüle",()=>ShowLibrary(true));var toolsNav=Nav("\uE80A","Araçlar",ShowWheel);
        foreach(var nav in new[]{convertNav,downloadNav,viewNav,toolsNav}){nav.Width=150;nav.HorizontalAlignment=HorizontalAlignment.Stretch;nav.HorizontalContentAlignment=HorizontalAlignment.Left;nav.Padding=new(14,11,10,11);nav.Margin=new(0,3,0,3);}
        sideNav.Children.Add(convertNav);sideNav.Children.Add(downloadNav);sideNav.Children.Add(viewNav);sideNav.Children.Add(toolsNav);
        var sideBottom=new StackPanel();DockPanel.SetDock(sideBottom,Dock.Bottom);side.Children.Add(sideBottom);
        var settingsNav=Nav("\uE713","Ayarlar",ShowSettings);var aboutNav=Nav("\uE946","Hakkında",()=>MessageBox.Show(this,"Nara · Dosyalarınızı dönüştürün, indirin ve görüntüleyin.","Nara · Hakkında",MessageBoxButton.OK,MessageBoxImage.Information));
        foreach(var nav in new[]{settingsNav,aboutNav}){nav.Width=150;nav.HorizontalAlignment=HorizontalAlignment.Stretch;nav.HorizontalContentAlignment=HorizontalAlignment.Left;nav.Padding=new(14,11,10,11);nav.Margin=new(0,3,0,3);}
        sideBottom.Children.Add(settingsNav);sideBottom.Children.Add(aboutNav);
        toolsSetupButton=Button(tools.Ready&&officeRuntime.Executable is not null?"Kurulum tamam":"İlk kurulumu tamamla",ShowToolSetupMenu);controls.Children.Add(toolsSetupButton);
        appearance=new ComboBox{ItemsSource=new[]{"System","Light","Dark"},SelectedItem=char.ToUpper(this.library.Appearance[0])+this.library.Appearance[1..]};
        updateButton=Button("Güncellemeleri denetle",async()=>await CheckUpdatesAsync(true));
        controls.Children.Add(Text("Bilgisayarınızda",11,"Muted"));foreach(var control in controls.Children.OfType<Button>()){control.Padding=new(10,6,10,6);control.FontSize=11;}
        DockPanel.SetDock(status,Dock.Bottom);status.Margin=new(4,14,4,0);root.Children.Add(status);
        var pages=new Grid{Margin=new(28,0,28,18)};homeScroll.Content=home;homeScroll.VerticalScrollBarVisibility=ScrollBarVisibility.Auto;homeScroll.HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled;libraryScroll.Content=libraryPage;libraryScroll.VerticalScrollBarVisibility=ScrollBarVisibility.Auto;libraryScroll.HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled;pages.Children.Add(homeScroll);pages.Children.Add(libraryScroll);root.Children.Add(pages);
        var updateRow=new DockPanel();DockPanel.SetDock(updateButton,Dock.Right);updateRow.Children.Add(updateButton);var updateCaption=new StackPanel();updateCaption.Children.Add(Text("Nara her zaman güncel.",16));updateCaption.Children.Add(Text("İndirme ve kurulum zamanını siz seçin.",12,"Muted"));updateRow.Children.Add(updateCaption);updateBanner=Card(updateRow);updateBanner.Visibility=Visibility.Collapsed;home.Children.Add(updateBanner);
        var headline=new TextBlock{FontSize=30,FontWeight=FontWeights.Bold,TextWrapping=TextWrapping.Wrap,MaxWidth=560,Foreground=Theme.Brush("Ink")};
        headline.Inlines.Add(new System.Windows.Documents.Run("Dosyalarınızla\\ndaha "));
        headline.Inlines.Add(new System.Windows.Documents.Run("fazlasını yapın"){Foreground=Theme.Primary});
        home.Children.Add(headline);var intro=Text("Dönüştürün, indirin, görüntüleyin.\\nHepsi tek bir uygulamada.",15,"Muted");intro.Margin=new(0,6,0,22);home.Children.Add(intro);
        home.Children.Add(workArea);
        // Keep file-selection controls wired for the existing import workflow,
        // but keep the landing page faithful to the Nara reference: hero + four action cards.
        var drop=new Grid();drop.ColumnDefinitions.Add(new(){Width=new(1,GridUnitType.Star)});drop.ColumnDefinitions.Add(new(){Width=new(166)});
        var dropText=new StackPanel();drop.Children.Add(dropText);dropText.Children.Add(selectionPill);selection.Margin=new(0,12,0,8);dropText.Children.Add(selection);dropText.Children.Add(fileDescription);
        var picks=new WrapPanel{Margin=new(0,14,0,8)};chooseFilesButton=Primary("Dosya seç",Choose);picks.Children.Add(chooseFilesButton);openWheelButton=Primary("Araçları aç",ShowWheel);picks.Children.Add(Button("Yapıştır",PasteClipboard));picks.Children.Add(openWheelButton);dropText.Children.Add(picks);
        Grid.SetColumn(garden,1);drop.Children.Add(garden);
        var dropStack=new StackPanel();dropStack.Children.Add(drop);list.ItemsSource=files;list.SelectionMode=SelectionMode.Extended;
        BuildFileChips();var clear=Button("Temizle",()=>{files.Clear();Refresh();});DockPanel.SetDock(clear,Dock.Right);manage.Children.Add(clear);manage.Children.Add(list);dropStack.Children.Add(manage);
        shortcutHints.Orientation=Orientation.Horizontal;shortcutHints.Margin=new(0,12,0,0);dropStack.Children.Add(shortcutHints);
        var featureGrid=new Grid{Margin=new(0,18,0,0)};
        featureGrid.ColumnDefinitions.Add(new(){Width=new(1,GridUnitType.Star)});featureGrid.ColumnDefinitions.Add(new(){Width=new(1,GridUnitType.Star)});
        featureGrid.RowDefinitions.Add(new(){Height=GridLength.Auto});featureGrid.RowDefinitions.Add(new(){Height=GridLength.Auto});
        Border Feature(string glyph,string title,string description,Action action,int col,int row)
        {
            var body=new StackPanel{Orientation=Orientation.Horizontal,VerticalAlignment=VerticalAlignment.Center};
            var icon=Text(glyph,23,"Gold",false);icon.FontFamily=new FontFamily("Segoe MDL2 Assets");icon.Width=48;icon.Height=48;icon.TextAlignment=TextAlignment.Center;icon.VerticalAlignment=VerticalAlignment.Center;icon.Margin=new(0,0,14,0);
            icon.Background=new SolidColorBrush(Color.FromRgb(237,233,254));icon.Foreground=new SolidColorBrush(Color.FromRgb(124,58,237));
            body.Children.Add(icon);
            var copy=new StackPanel{VerticalAlignment=VerticalAlignment.Center};
            copy.Children.Add(Text(title,16));copy.Children.Add(Text(description,12,"Muted"));body.Children.Add(copy);
            var button=Button("",action);button.Content=body;button.HorizontalContentAlignment=HorizontalAlignment.Stretch;button.VerticalContentAlignment=VerticalAlignment.Center;button.Padding=new(16);button.BorderThickness=new(0);button.Background=Brushes.Transparent;
            var card=Card(button);card.Padding=new(14);card.MinHeight=94;card.Margin=new Thickness(0,0,col==0?12:0,12);
            Grid.SetColumn(card,col);Grid.SetRow(card,row);return card;
        }
        featureGrid.Children.Add(Feature("\uE8A5","Dönüştür","Görsel, video, ses, belge formatlarını dönüştürün.",Choose,0,0));
        featureGrid.Children.Add(Feature("\uE896","İndir","Web'den içerik indirin. Desteklenen birçok kaynak.",OpenDownload,1,0));
        featureGrid.Children.Add(Feature("\uE8A9","Görüntüle","Dosyalarınızı hızlıca önizleyin.",()=>ShowLibrary(true),0,1));
        featureGrid.Children.Add(Feature("\uE80A","Araçlar","Ek araçlar ve yardımcı özellikler.",ShowWheel,1,1));
        workArea.Children.Add(featureGrid);
        var recentHeading=new DockPanel{Margin=new(0,24,0,10)};var all=Button("Tümünü gör  ↗",()=>ShowLibrary(true));DockPanel.SetDock(all,Dock.Right);recentHeading.Children.Add(all);recentSearch.TextChanged+=(_,_)=>RenderLibrary();DockPanel.SetDock(recentSearch,Dock.Right);recentHeading.Children.Add(recentSearch);var recentTitle=new StackPanel{Orientation=Orientation.Horizontal,VerticalAlignment=VerticalAlignment.Center};recentTitle.Children.Add(Text("Son oluşturulanlar",19));recentCount.Margin=new(10,0,0,0);recentTitle.Children.Add(recentCount);recentHeading.Children.Add(recentTitle);home.Children.Add(recentHeading);var recentCard=Card(recentRows);recentCard.Padding=new(7);home.Children.Add(recentCard);
        // Setup and shortcut controls remain available from Settings; the landing page stays uncluttered.
        libraryPage.Children.Add(Text("Kitaplığınız",30));libraryPage.Children.Add(Text("Oluşturduğunuz ve içe aktardığınız dosyalar.",14,"Muted"));
        var searchLabel=Text("Dosya veya araç ara  ·  Ctrl+F",12,"Muted");searchLabel.Margin=new(4,20,4,0);libraryPage.Children.Add(searchLabel);libraryPage.Children.Add(search);libraryPage.Children.Add(libraryRows);search.TextChanged+=(_,_)=>RenderLibrary();
        AutomationProperties.SetAutomationId(search,"LibrarySearch");AutomationProperties.SetAutomationId(link,"DownloadLink");AutomationProperties.SetAutomationId(list,"SelectedFiles");
        Closing+=(_,e)=>{if(busy){e.Cancel=true;L10n.SetText(status,"Finish the current operation or choose Cancel before closing Nara.");}};
        DragOver+=(_,e)=>{e.Effects=!busy&&(e.Data.GetDataPresent(DataFormats.FileDrop)||e.Data.GetDataPresent(DataFormats.UnicodeText)||e.Data.GetDataPresent(DataFormats.Html)||e.Data.GetDataPresent("UniformResourceLocatorW"))?DragDropEffects.Copy:DragDropEffects.None;e.Handled=true;};
        Drop+=(_,e)=>{if(!busy){if(!e.Data.GetDataPresent(DataFormats.FileDrop)&&DragPayload.Parse(e.Data) is not null)OnGlobalDrop("download:auto",e.Data,null);else Import(e.Data);}e.Handled=true;};
        PreviewKeyDown+=(_,e)=>{if(HandleShortcut(e.Key,Keyboard.Modifiers,Keyboard.FocusedElement as DependencyObject))e.Handled=true;};
        if(initial is not null)Add(initial);else Refresh();ShowLibrary(false);RenderLibrary();Loaded+=(_,_)=>Theme.Entrance(LibraryVisible?libraryPage:home);
        if(this.library.RecoveryNotice is {} notice)L10n.SetText(status,notice);
        else if(preferences.RecoveryNotice is {} settingsNotice)L10n.SetText(status,settingsNotice);
        if(checkForUpdates)
        {
            Loaded+=(_,_)=>SetupDesktop();Closed+=(_,_)=>globalDrag?.Dispose();
            if(offerToolSetup)Loaded+=(_,_)=>Dispatcher.BeginInvoke(OfferFirstRunTools);
            Loaded+=async(_,_)=>{if(preferences.Value.AutomaticUpdateChecks)await CheckUpdatesAsync(false);};
            var timer=new System.Windows.Threading.DispatcherTimer{Interval=TimeSpan.FromHours(1)};timer.Tick+=async(_,_)=>{if(preferences.Value.AutomaticUpdateChecks)await CheckUpdatesAsync(false);};Closed+=(_,_)=>timer.Stop();timer.Start();
        }
    }
    private void RoutePageMouseWheel(object sender,MouseWheelEventArgs e)
    {
        var page=home.Visibility==Visibility.Visible?homeScroll:libraryScroll;
        for(var source=e.OriginalSource as DependencyObject;source is not null&&!ReferenceEquals(source,page);source=source is Visual?VisualTreeHelper.GetParent(source):LogicalTreeHelper.GetParent(source))
        {
            if(source is not ScrollViewer nested)continue;
            var canScroll=e.Delta>0?nested.VerticalOffset>.5:nested.VerticalOffset<nested.ScrollableHeight-.5;
            if(canScroll)return;
            page.ScrollToVerticalOffset(page.VerticalOffset-e.Delta);
            e.Handled=true;
            return;
        }
        page.ScrollToVerticalOffset(page.VerticalOffset-e.Delta);
        e.Handled=true;
    }
    private IEnumerable<LibraryEntry> SearchLibrary(string query)
    {
        var original=library.Search(query).Select(e=>e.Id).ToHashSet();
        return library.Entries.Where(e=>original.Contains(e.Id)||AppLocalization.Matches(e.Action,query,L10n.Language));
    }
    private static TextBlock Text(string value,double size,string? color=null,bool localize=true)
    {
        var text=new TextBlock{Text=value,FontSize=size,TextWrapping=TextWrapping.Wrap,VerticalAlignment=VerticalAlignment.Center,FontWeight=size>=17?FontWeights.SemiBold:FontWeights.Normal};if(color is not null)text.SetResourceReference(TextBlock.ForegroundProperty,color);return localize?L10n.Label(text):text;
    }
    internal bool WindowManagementEnabled=>library.WindowManagementEnabled;
    private static Border Card(UIElement content){var card=new Border{CornerRadius=new(24),Padding=new(22),Margin=new(0,0,0,6),Child=content,BorderThickness=new(1)};card.SetResourceReference(Border.BackgroundProperty,"Surface");card.SetResourceReference(Border.BorderBrushProperty,"Line");return card;}
    public static Button Button(string title,Action action){var button=L10n.Content(new Button{Content=title});button.Click+=(_,_)=>action();return button;}
    internal static Button Primary(string title,Action action){var button=Button(title,action);button.Background=Theme.Primary;button.Foreground=new SolidColorBrush(Color.FromRgb(255,255,255));return button;}
    public static void OpenUrl(string url)=>Process.Start(new ProcessStartInfo(url){UseShellExecute=true});
    public static void Reveal(string path){var info=new ProcessStartInfo("explorer.exe"){UseShellExecute=false};info.ArgumentList.Add("/select,"+Path.GetFullPath(path));Process.Start(info);}
    private void Choose(){if(busy)return;var picker=new OpenFileDialog{Multiselect=true,Title="Choose files for Nara"};if(picker.ShowDialog(this)==true){Add(picker.FileNames);ShowLibrary(false);}}
    private void Add(IEnumerable<string> paths){foreach(var path in paths.Where(p=>File.Exists(p)||Directory.Exists(p))){var full=Path.GetFullPath(path);if(!files.Contains(full))files.Add(full);}Refresh();}
    internal void ShowLibrary(bool visible)
    {
        var changed=LibraryVisible!=visible;
        if(!busy)L10n.SetText(status,"Files stay on your computer.");
        selectedLibraryId=selectedLibraryId??library.Entries.FirstOrDefault()?.Id;home.Visibility=visible?Visibility.Collapsed:Visibility.Visible;libraryPage.Visibility=visible?Visibility.Visible:Visibility.Collapsed;
        homeButton.SetResourceReference(Control.BackgroundProperty,visible?"Surface":"Sage");libraryButton.SetResourceReference(Control.BackgroundProperty,visible?"Sage":"Surface");if(visible)RenderLibrary();if(changed&&IsLoaded)Theme.Entrance(visible?libraryPage:home);
    }
    private void Record(string label,IReadOnlyList<string> paths)
    {
        try{library.Record(label,paths);RenderLibrary();}catch(Exception error)when(error is IOException or UnauthorizedAccessException){L10n.SetText(status,"Your files are ready, but Library could not be saved.");}
    }
    internal void Import(IDataObject data)
    {
        if(busy)return;
        try
        {
            var result=ClipboardImport.Read(data,library);ShowLibrary(false);
            if(result.Link is {} url){var payload=WebMediaPayload.Parse(url);link.Text=payload?.Url??url;if(payload is not null)SetDownloadKind(payload);OpenDownload();link.Focus();L10n.SetText(status,"Link ready. Choose a format, then Download.");return;}
            files.Clear();Add(result.Paths);Record(result.Label,result.Paths);L10n.SetText(status,result.Label+". Choose a tool or create a ZIP.");
        }
        catch(Exception error)when(ClipboardImport.IsClipboardError(error)){L10n.SetText(status,"Couldn’t paste: "+error.Message);}
    }
    private void PasteClipboard()
    {
        if(busy)return;try{Import(Clipboard.GetDataObject()??new DataObject());}catch(Exception error)when(ClipboardImport.IsClipboardError(error)){L10n.SetText(status,"Clipboard is busy. Try Paste again.");}
    }
    internal bool HandleShortcut(Key key,ModifierKeys modifiers,DependencyObject? focus)
    {
        if(modifiers==ModifierKeys.Control&&key==Key.OemComma){ShowSettings();return true;}
        if(modifiers==ModifierKeys.Control&&key==Key.D1){ShowLibrary(false);return true;}
        if(modifiers==ModifierKeys.Control&&key==Key.D2){ShowLibrary(true);return true;}
        if(modifiers==ModifierKeys.Control&&key==Key.F){ShowLibrary(true);search.Focus();search.SelectAll();return true;}
        if(busy)return false;
        if(modifiers==ModifierKeys.Control&&key==Key.O){Choose();return true;}
        if(modifiers==ModifierKeys.Control&&key==Key.U){_ = CheckUpdatesAsync(true);return true;}
        if(LibraryVisible&&key==Key.Down&&modifiers==ModifierKeys.None&&ReferenceEquals(focus,search)){SelectLibrary(1);return true;}
        if(ClipboardImport.IsEditing(focus))return false;
        if(LibraryVisible&&modifiers==ModifierKeys.None&&key is Key.Up or Key.Down){SelectLibrary(key==Key.Down?1:-1);return true;}
        if(LibraryVisible&&modifiers==ModifierKeys.None&&key==Key.Space&&focus is not System.Windows.Controls.Button){PreviewSelectedLibrary();return true;}
        if(LibraryVisible&&modifiers==ModifierKeys.None&&key==Key.Enter&&focus is not System.Windows.Controls.Button){ReuseSelectedLibrary();return true;}
        if(modifiers==ModifierKeys.Control&&key==Key.V){PasteClipboard();return true;}
        if(modifiers==(ModifierKeys.Control|ModifierKeys.Shift)&&key==Key.D)
        {OpenCopiedLink();return true;}
        if((modifiers==ModifierKeys.Control&&key is Key.Space or Key.Enter)||(modifiers==ModifierKeys.None&&key==Key.Enter&&focus is not System.Windows.Controls.Button)){ShowWheel();return true;}
        return false;
    }
    private void RenderLibrary()
    {
        libraryCards.Clear();libraryRows.Children.Clear();recentRows.Children.Clear();var filtered=SearchLibrary(search.Text).ToArray();
        if(filtered.Length==0)libraryRows.Children.Add(Card(Text(library.Entries.Count==0?"Nothing here yet. Paste something or make your first fresh copy.":"No matches. Try a file name or tool.",14,"Muted")));
        foreach(var entry in filtered)libraryRows.Children.Add(LibraryRow(entry));
        recentCount.Text=library.Entries.Count.ToString();foreach(var entry in SearchLibrary(recentSearch.Text).Take(3))recentRows.Children.Add(LibraryRow(entry,true));
        if(library.Entries.Count==0)recentRows.Children.Add(Card(Text("Oluşturduğunuz dosyalar burada görünecek.",13,"Muted")));
    }
    private Border LibraryRow(LibraryEntry entry,bool compact=false)
    {
        var row=new DockPanel();var available=entry.AvailablePaths;var buttons=new WrapPanel{VerticalAlignment=VerticalAlignment.Center};DockPanel.SetDock(buttons,Dock.Right);row.Children.Add(buttons);
        var reuse=Button("↖",()=>{if(busy)return;var existing=entry.AvailablePaths.ToArray();if(existing.Length==0){L10n.SetText(status,"These files have moved or were deleted.");RenderLibrary();return;}files.Clear();Add(existing);ShowLibrary(false);L10n.SetText(status,existing.Length==entry.Paths.Length?"Ready for another round.":"Available files selected. Some files have moved or were deleted.");});reuse.IsEnabled=!busy&&available.Length>0;reuse.ToolTip="Use in Nara";AutomationProperties.SetName(reuse,"Use "+entry.Name+" again");buttons.Children.Add(reuse);
        var reveal=Button("▱",()=>{var existing=entry.AvailablePaths;if(existing.Length>0)Reveal(existing[0]);else{L10n.SetText(status,"This file has moved or was deleted.");RenderLibrary();}});reveal.IsEnabled=available.Length>0;reveal.ToolTip="Show in Explorer";AutomationProperties.SetName(reveal,"Show "+entry.Name+" in Explorer");buttons.Children.Add(reveal);
        var fileIcon=new Image{Width=29,Height=29,Margin=new(0,0,12,0),VerticalAlignment=VerticalAlignment.Center};FileArtwork.Bind(fileIcon,entry.Paths[0]);DockPanel.SetDock(fileIcon,Dock.Left);row.Children.Add(fileIcon);var text=new StackPanel{Margin=new(0,0,12,0),VerticalAlignment=VerticalAlignment.Center};var name=Text(entry.Name,12,localize:false);name.TextWrapping=TextWrapping.NoWrap;name.TextTrimming=TextTrimming.CharacterEllipsis;name.ToolTip=string.Join(Environment.NewLine,entry.Paths);text.Children.Add(name);var info=new StackPanel{Orientation=Orientation.Horizontal};info.Children.Add(Text(entry.Action,10,"Muted"));info.Children.Add(Text("  ·  "+entry.Created.ToLocalTime().ToString("g"),10,"Muted",localize:false));if(available.Length<entry.Paths.Length)info.Children.Add(Text("Files missing",10,"Muted"));text.Children.Add(info);row.Children.Add(text);
        var preview=Button("◉",()=>PreviewEntry(entry));preview.IsEnabled=available.Length>0;preview.ToolTip="Preview";AutomationProperties.SetName(preview,"Preview "+entry.Name);buttons.Children.Insert(0,preview);
        var card=Card(row);if(!compact)ConfigureLibraryRow(card,entry);card.Padding=new(14,10,14,10);if(compact){card.CornerRadius=new(12);card.BorderThickness=new(0);card.Margin=new(0,0,0,2);}return card;
    }
    private void OpenDownload()
    {
        if(webWindow?.IsVisible==true){webWindow.Activate();link.Focus();return;}
        var dialog=new Window{Owner=this,Title="Download from the web · Nara",Width=800,Height=650,MinWidth=640,MinHeight=400,WindowStartupLocation=WindowStartupLocation.CenterOwner,Content=new ScrollViewer{Content=webContent,VerticalScrollBarVisibility=ScrollBarVisibility.Auto}};Theme.Attach(dialog);webWindow=dialog;
        dialog.Closing+=(_,e)=>{if(busy){e.Cancel=true;L10n.SetText(status,"Finish or cancel the current operation first.");}};
        dialog.Closed+=(_,_)=>{inspection?.Cancel();if(dialog.Content is ScrollViewer scroll)scroll.Content=null;dialog.Content=null;webWindow=null;};dialog.PreviewKeyDown+=(_,e)=>{if(e.Key==Key.Escape&&!busy){dialog.Close();e.Handled=true;}};dialog.Show();link.Focus();
    }
    private void SetBusy(bool value){busy=value;updateButton.IsEnabled=!value&&!checking;workArea.IsEnabled=!value;webContent.IsEnabled=!value;L10n.Bind(toolsSetupButton,ContentControl.ContentProperty,tools.Ready&&officeRuntime.Executable is not null?"Tools ready":"Finish setup");RenderLibrary();}
    private void ShowShortcuts()=>MessageBox.Show(this,L10n.Text("Ctrl+O  Choose files\nCtrl+V  Paste files, text, an image or link\nCtrl+Space / Enter  Open tools\nCtrl+1 / Ctrl+2  Home / Library\nCtrl+F  Find in Library\nCtrl+Shift+D  Use a copied download link\nCtrl+U  Check for updates\n\nWheel: arrows / Tab to choose, Enter to open, Esc to close\nMedia: Space play/pause, ←/→ 1 second, Shift+←/→ 1/30s, I/O set range\nCrop: drag, Shift locks ratio, Enter saves\nEditing text: normal paste and arrow keys are preserved."),L10n.Text("Nara · Features & shortcuts"));
    private sealed class FileNameConverter:System.Windows.Data.IValueConverter
    {
        public object Convert(object value,Type type,object parameter,System.Globalization.CultureInfo culture)=>Path.GetFileName((string)value);
        public object ConvertBack(object value,Type type,object parameter,System.Globalization.CultureInfo culture)=>throw new NotSupportedException();
    }
    private bool All(string[] extensions)=>files.Count>0&&files.All(p=>extensions.Contains(Path.GetExtension(p).TrimStart('.').ToLowerInvariant()));
    private bool Images=>files.Count>0&&files.All(FeatureCatalog.IsImage);
    private bool Video=>All(["mp4","mov","mkv","webm","avi","wmv","m4v","gif"]);
    private bool Audio=>All(["mp3","m4a","wav","flac","ogg","opus","aiff","aif","wma","aac"]);
    private IReadOnlyList<ToolChoice> Choices()
    {
        return FeatureCatalog.Tools(files).Select(id=>new ToolChoice(id switch{"cropImage"=>"crop","annotateImage"=>"annotate","mergePDF"=>"mergePDFs",_=>id},FeatureCatalog.ToolNames[id],ToolSymbol(id))).ToArray();
    }
    private void Refresh()
    {
        selectionPill.Text=files.Count==0?"✦  BAŞLANGIÇ":"✦  HAZIR";selection.Text=files.Count==0?"Bir dosya seçin.":files.Count==1?Path.GetFileName(files[0]):$"{files.Count} dosya hazır.";fileDescription.Text=files.Count==0?"Görseller, videolar, belgeler ve daha fazlası.":files.Count==1?Path.GetDirectoryName(files[0])!:"Dosyalarınızı birlikte veya ayrı ayrı işleyin.";if(chooseFilesButton is not null){chooseFilesButton.Content=files.Count==0?"Dosya seç":"Dosya ekle veya değiştir";if(files.Count==0){chooseFilesButton.Background=Theme.Primary;chooseFilesButton.Foreground=new SolidColorBrush(Color.FromRgb(43,54,28));}else{chooseFilesButton.SetResourceReference(Control.BackgroundProperty,"Surface");chooseFilesButton.SetResourceReference(Control.ForegroundProperty,"Ink");}}if(openWheelButton is not null)openWheelButton.Visibility=files.Count>0?Visibility.Visible:Visibility.Collapsed;list.Visibility=manage.Visibility=files.Count==0?Visibility.Collapsed:Visibility.Visible;actions.Children.Clear();garden.SetSelected(files.Count>0);
        BuildSelectionActions();
    }
    private void ShowWheel(){if(!busy&&Choices().Count>0)new WheelWindow(this,Choices(),async choice=>await EditAsync(choice)).Show();}
    private async Task EditAsync(ToolChoice choice)
    {
        if(busy||files.Count==0)return;string? temporary=null;try
        {
            var requestedFormat=choice.Id.StartsWith("convert:",StringComparison.Ordinal)?choice.Id[8..]:null;if(requestedFormat is not null)choice=new("convert","Convert to "+requestedFormat.ToUpperInvariant(),"↗");
            choice=choice with{Id=choice.Id switch{"cropImage"=>"crop","annotateImage"=>"annotate","mergePDF"=>"mergePDFs",_=>choice.Id}};
            SetBusy(true);var imageSelection=Images;var mediaSelection=Video||Audio;var selected=files.ToArray();MediaInfo? info=null;BitmapSource? preview=null;var input=selected[0];
            if(choice.Id=="removeMetadata"){SetBusy(false);await EditMetadataAsync(selected);return;}
            if(choice.Id=="readQRCode"){SetBusy(false);await ReadQrAsync(selected);return;}
            if(!imageSelection&&!mediaSelection&&choice.Id is not ("extractArchive" or "unzip")){SetBusy(false);await EditDocumentAsync(choice,selected,requestedFormat);return;}
            if(imageSelection)preview=ImageEngine.Load(input);
            else if(mediaSelection)
            {
                if(!tools.Ready){MessageBox.Show(this,L10n.Text("Medya düzenleme özelliklerini etkinleştirmek için medya araçlarını kurun."),L10n.Text("Medya kurulumu"));return;}
                var engine=new MediaEngine(tools.Executable("ffmpeg"),tools.Executable("ffprobe"));info=await engine.InspectAsync(input);
                if(choice.Id is "cropVideo" or "redactVideo")
                {
                    temporary=Path.Combine(Path.GetTempPath(),"Nara-preview-"+Guid.NewGuid().ToString("N")+".png");
                    await ProcessRunner.RunAsync(tools.Executable("ffmpeg"),["-v","error","-i",input,"-frames:v","1",temporary]);preview=ImageEngine.Load(temporary);
                }
            }
            SetBusy(false);var editor=new ToolEditor(this,choice.Id,choice.Title,input,info,preview,selected,choice.Id=="convert"?FeatureCatalog.Formats(selected):null);editor.ConfigureProfiles(preferences,requestedFormat);editor.ConfigureMediaRuntime(tools);var accepted=editor.ShowDialog()==true;if(temporary is not null)File.Delete(temporary);if(!accepted)return;
            var inputs=choice.Id is "cropVideo" or "redactVideo" or "annotate"?new[]{input}:editor.OrderedInputs;
            if(imageSelection)await RunAsync(async(destination,p,t)=>
            {
                using var tx=new ExportTransaction(destination);var exported=await OnStaAsync(()=>ImageEngine.Apply(choice.Id,inputs,tx.StagingDirectory,editor.ImageOptions with{EncoderPath=tools.Ready?tools.Executable("ffmpeg"):null},choice.Id=="convert"?editor.Format:null,t,p));
                if(choice.Id=="convert"&&editor.PreserveImageMetadata&&editor.Format is not ("pdf" or "bmp" or "gif"))
                {
                    await metadataService.InstallAsync(p,t);for(var n=0;n<exported.Count;n++)await metadataService.CopyImageTagsAsync(inputs[n],exported[n],t);
                }
                return tx.Publish(exported,t);
            },choice.Title);
            else if(mediaSelection)await RunAsync((destination,p,t)=>{var engine=new MediaEngine(tools.Executable("ffmpeg"),tools.Executable("ffprobe"));return choice.Id=="convert"?engine.ConvertAsync(inputs,editor.Format,destination,editor.MediaOptions,p,t):engine.ToolAsync(choice.Id,inputs,destination,editor.MediaOptions,p,t);},choice.Title);
            else if(choice.Id is "unzip" or "extractArchive")await RunAsync((destination,p,t)=>Task.Run<IReadOnlyList<string>>(()=>{using var tx=new ExportTransaction(destination);var outputs=inputs.SelectMany(file=>ArchiveEngine.Extract(file,tx.StagingDirectory,editor.DocumentOptions.Password,p,t)).ToList();return tx.Publish(outputs,t);},t));
            else await RunAsync((destination,p,t)=>Task.Run(()=>DocumentEngine.Pdf(choice.Id,inputs,destination,editor.Pages,t),t),choice.Title);
        }
        catch(Exception error){MessageBox.Show(this,L10n.Text(error.Message),L10n.Text("Couldn’t open this tool"),MessageBoxButton.OK,MessageBoxImage.Information);}
        finally{if(temporary is not null&&File.Exists(temporary))File.Delete(temporary);SetBusy(false);}
    }
    private async Task RunAsync(Func<string,IProgress<OperationProgress>,CancellationToken,Task<IReadOnlyList<string>>> work,string label="Fresh copy")
    {
        if(busy)return;var owner=webWindow?.IsActive==true?webWindow:this;string destination;
        if(label=="Move"||preferences.Value.OutputMode=="ask"||(files.Count==0&&owner==this)){var folder=new OpenFolderDialog{Title="Save new files to…",InitialDirectory=Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)};if(folder.ShowDialog(owner)!=true)return;destination=folder.FolderName;}
        else destination=owner==webWindow?preferences.Value.DownloadFolder:preferences.Value.OutputMode=="folder"&&!string.IsNullOrWhiteSpace(preferences.Value.OutputFolder)?preferences.Value.OutputFolder:Path.GetDirectoryName(files[0])!;
        SetBusy(true);using var cancellation=new CancellationTokenSource();var toast=new ToastWindow(owner,cancellation.Cancel);toast.Show();
        try{var outputs=await work(destination,new Progress<OperationProgress>(toast.Update),cancellation.Token);toast.Complete(outputs);Record(label,outputs);L10n.SetText(status,$"Saved {outputs.Count} new {(outputs.Count==1?"file":"files")}. Originals kept.");if(preferences.Value.RevealFinished&&outputs.Count>0)Reveal(outputs[0]);}
        catch(OperationCanceledException){toast.Close();L10n.SetText(status,"Cancelled. Originals kept.");}
        catch(Exception error){toast.Close();MessageBox.Show(this,L10n.Text(error.Message),L10n.Text("Couldn’t finish"),MessageBoxButton.OK,MessageBoxImage.Information);}
        finally{SetBusy(false);}
    }
    private static Task<IReadOnlyList<string>> OnStaAsync(Func<IReadOnlyList<string>> work)
    {
        var completion=new TaskCompletionSource<IReadOnlyList<string>>(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread=new Thread(()=>{try{completion.SetResult(work());}catch(Exception error){completion.SetException(error);}}){IsBackground=true};thread.SetApartmentState(ApartmentState.STA);thread.Start();return completion.Task;
    }
    private async Task DownloadAsync()
    {
        if(busy)return;
        var payload=WebMediaPayload.Parse(link.Text);var kind=downloadKind.SelectedItem as string??"video";
        if(!tools.Ready&&kind!="image"&&payload?.Kind!="image"&&!(kind=="thumbnail"&&payload?.Kind=="youtube")){MessageBox.Show(webWindow??this,L10n.Text("Web indirmelerini etkinleştirmek için medya araçlarını kurun."),L10n.Text("Media setup"));return;}
        if(kind=="photos"&&inspectedLink!=link.Text){await InspectWebLinkAsync();return;}
        try{var url=WebDownload.Validate(payload?.Url??link.Text).AbsoluteUri;await RunAsync((destination,p,t)=>new WebDownload(tools).DownloadAsync(url,kind,destination,p,t,webQuality.SelectedIndex==1?1080:webQuality.SelectedIndex==2?720:null,kind=="photos"?new HashSet<int>(selectedWebPhotos):null),"Download "+kind);}catch(Exception error){MessageBox.Show(webWindow??this,L10n.Text(error.Message),L10n.Text("Check your link"));}
    }
    private async Task SetupAsync()
    {
        if(busy)return;if(tools.Ready){L10n.SetText(status,"Media tools are already installed.");return;}
        if(MessageBox.Show(this,L10n.Text("Download FFmpeg (~180 MB), yt-dlp and Deno directly from their official GitHub releases? SHA-256 hashes are verified before use. FFmpeg is GPL; the other upstream licenses and source links are listed on our Features page. No administrator password is needed."),L10n.Text("Set up media tools"),MessageBoxButton.OKCancel,MessageBoxImage.Information)!=MessageBoxResult.OK)return;
        SetBusy(true);using var cancellation=new CancellationTokenSource();var toast=new ToastWindow(this,cancellation.Cancel);toast.Show();
        try{await tools.InstallAsync(new Progress<OperationProgress>(toast.Update),cancellation.Token);toast.Close();L10n.SetText(status,"Media tools ready. You can edit, convert and download media.");}
        catch(OperationCanceledException){toast.Close();L10n.SetText(status,"Setup cancelled.");}catch(Exception error){toast.Close();MessageBox.Show(this,L10n.Text(error.Message),L10n.Text("Setup couldn’t finish"));}finally{SetBusy(false);}
    }
    internal void OfferUpdate(WindowsUpdate candidate){update=candidate;updateButton.Content="Update to "+candidate.Version;if(updateBanner is not null)updateBanner.Visibility=Visibility.Visible;tray?.UpdateAvailable(candidate.Version);}
    private async Task CheckUpdatesAsync(bool user)
    {
        if(busy||checking)return;checking=true;updateButton.IsEnabled=false;if(user)L10n.SetText(status,"Checking for updates…");try
        {
            if(update is null){update=await PublicUpdates.CheckAsync(UpdateInstaller.Current);if(update is null){if(user)L10n.SetText(status,"You have the latest published Windows version.");return;}OfferUpdate(update);if(!user)return;}
            if(busy)return;
            if(MessageBox.Show(this,L10n.Text("Download, verify and install Nara "+update.Version+"? The app will restart. No account or token is needed."),L10n.Text("Nara update"),MessageBoxButton.OKCancel)!=MessageBoxResult.OK)return;
            SetBusy(true);using var cancellation=new CancellationTokenSource();var toast=new ToastWindow(this,cancellation.Cancel);toast.Show();
            try{await UpdateInstaller.StageAsync(update,new Progress<OperationProgress>(toast.Update),cancellation.Token);busy=false;Application.Current.Shutdown();}finally{toast.Close();SetBusy(false);}
        }
        catch(OperationCanceledException){L10n.SetText(status,"Update cancelled.");}catch(Exception error){if(user)MessageBox.Show(this,L10n.Text(error.Message),L10n.Text("Couldn’t check for updates"));}
        finally{checking=false;updateButton.IsEnabled=!busy;}
    }
}
