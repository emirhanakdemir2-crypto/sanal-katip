using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using Microsoft.Win32;

namespace FDBTakip;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        var app = new Application();
        app.Run(new MainWindow());
    }
}

internal sealed class MainWindow : Window
{
    private readonly TextBlock _statusText;
    private readonly TextBlock _totalText;
    private readonly TextBlock _changeText;
    private readonly TextBlock _lastScanText;
    private readonly DataGrid _changesGrid;
    private readonly TextBox _questionBox;
    private readonly TextBox _analysisBox;
    private readonly Button _scanButton;
    private readonly Button _exportAnalysisButton;

    private List<Dictionary<string, string>> _records = new();
    private List<Dictionary<string, string>> _lastAnalysisRecords = new();
    private string _lastChangesPath = "";

    public MainWindow()
    {
        Title = "FDB Takip 1.2";
        Width = 1080;
        Height = 760;
        MinWidth = 900;
        MinHeight = 620;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;

        var root = new DockPanel { Margin = new Thickness(18) };
        Content = root;

        var header = new StackPanel { Orientation = Orientation.Vertical, Margin = new Thickness(0, 0, 0, 14) };
        DockPanel.SetDock(header, Dock.Top);
        root.Children.Add(header);
        header.Children.Add(new TextBlock
        {
            Text = "FDB Takip",
            FontSize = 28,
            FontWeight = FontWeights.SemiBold
        });
        header.Children.Add(new TextBlock
        {
            Text = "DOSYATKP.FDB → değişiklik takibi, Excel ve yerel analiz",
            Margin = new Thickness(0, 4, 0, 0),
            Opacity = 0.72
        });

        var tabs = new TabControl();
        root.Children.Add(tabs);

        var scanTab = new TabItem { Header = "Tarama" };
        tabs.Items.Add(scanTab);
        var scanRoot = new Grid { Margin = new Thickness(12) };
        scanRoot.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        scanRoot.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        scanRoot.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        scanTab.Content = scanRoot;

        var summary = new Grid { Margin = new Thickness(0, 0, 0, 12) };
        summary.ColumnDefinitions.Add(new ColumnDefinition());
        summary.ColumnDefinitions.Add(new ColumnDefinition());
        summary.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(2, GridUnitType.Star) });
        Grid.SetRow(summary, 0);
        scanRoot.Children.Add(summary);

        _totalText = AddSummaryCard(summary, 0, "Toplam kayıt", "-");
        _changeText = AddSummaryCard(summary, 1, "Yeni / Güncellenen", "-");
        _lastScanText = AddSummaryCard(summary, 2, "Son durum", "Henüz tarama yapılmadı");

        var buttonBar = new WrapPanel { Margin = new Thickness(0, 0, 0, 12) };
        Grid.SetRow(buttonBar, 1);
        scanRoot.Children.Add(buttonBar);

        _scanButton = MakeButton("FDB Seç ve Tara", 145);
        _scanButton.Click += ScanButton_Click;
        buttonBar.Children.Add(_scanButton);

        var allButton = MakeButton("Tüm Kayıtları Aç", 145);
        allButton.Click += (_, _) => OpenFileIfExists(FdbEngine.AllOutputPath, "Önce bir FDB tarayın.");
        buttonBar.Children.Add(allButton);

        var changesButton = MakeButton("Yeni/Güncellenen Aç", 165);
        changesButton.Click += (_, _) =>
        {
            string path = !string.IsNullOrWhiteSpace(_lastChangesPath) ? _lastChangesPath : FindLatestChangesFile();
            OpenFileIfExists(path, "Henüz Yeni/Güncellenen Excel'i yok.");
        };
        buttonBar.Children.Add(changesButton);

        var folderButton = MakeButton("Çıktı Klasörü", 130);
        folderButton.Click += (_, _) => OpenFolder(FdbEngine.OutputDirectory);
        buttonBar.Children.Add(folderButton);

        _statusText = new TextBlock
        {
            Text = "Hazır.",
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(10, 9, 0, 0),
            TextWrapping = TextWrapping.Wrap
        };
        buttonBar.Children.Add(_statusText);

        _changesGrid = BuildChangesGrid();
        Grid.SetRow(_changesGrid, 2);
        scanRoot.Children.Add(_changesGrid);

        var analysisTab = new TabItem { Header = "Yerel Analiz" };
        tabs.Items.Add(analysisTab);
        var analysisRoot = new Grid { Margin = new Thickness(14) };
        analysisRoot.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        analysisRoot.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        analysisRoot.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        analysisRoot.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        analysisTab.Content = analysisRoot;

        var info = new TextBlock
        {
            Text = "Sorular bilgisayarınızda, yerel kayıtlar üzerinde hesaplanır. FDB verisi internete gönderilmez.\n" +
                   "Örnek: “Mehmet Ali Yeniay 1 Temmuz 2026'dan bugüne kaç dosyaya bakmış, kaç dosyayı karara çıkarmış?”",
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 10)
        };
        Grid.SetRow(info, 0);
        analysisRoot.Children.Add(info);

        var questionPanel = new Grid { Margin = new Thickness(0, 0, 0, 10) };
        questionPanel.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        questionPanel.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        Grid.SetRow(questionPanel, 1);
        analysisRoot.Children.Add(questionPanel);

        _questionBox = new TextBox
        {
            MinHeight = 70,
            AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            FontSize = 14,
            Padding = new Thickness(8),
            Text = "Mehmet Ali Yeniay geldiği tarihten itibaren kaç dosyaya bakmış, kaç dosyayı karara çıkarmış?"
        };
        Grid.SetColumn(_questionBox, 0);
        questionPanel.Children.Add(_questionBox);

        var analyzeButton = MakeButton("Analiz Et", 110);
        analyzeButton.Height = 42;
        analyzeButton.Margin = new Thickness(10, 0, 0, 0);
        analyzeButton.Click += AnalyzeButton_Click;
        Grid.SetColumn(analyzeButton, 1);
        questionPanel.Children.Add(analyzeButton);

        _analysisBox = new TextBox
        {
            IsReadOnly = true,
            TextWrapping = TextWrapping.Wrap,
            AcceptsReturn = true,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Padding = new Thickness(12),
            FontFamily = new System.Windows.Media.FontFamily("Consolas"),
            FontSize = 14,
            Text = "Bir soru yazıp Analiz Et'e basın."
        };
        Grid.SetRow(_analysisBox, 2);
        analysisRoot.Children.Add(_analysisBox);

        var analysisButtons = new WrapPanel { Margin = new Thickness(0, 10, 0, 0) };
        Grid.SetRow(analysisButtons, 3);
        analysisRoot.Children.Add(analysisButtons);

        _exportAnalysisButton = MakeButton("Sonucu Excel'e Aktar", 165);
        _exportAnalysisButton.IsEnabled = false;
        _exportAnalysisButton.Click += ExportAnalysisButton_Click;
        analysisButtons.Children.Add(_exportAnalysisButton);

        var analysisFolderButton = MakeButton("Çıktı Klasörü", 125);
        analysisFolderButton.Click += (_, _) => OpenFolder(FdbEngine.OutputDirectory);
        analysisButtons.Children.Add(analysisFolderButton);

        LoadExistingState();
    }

    private static TextBlock AddSummaryCard(Grid parent, int column, string caption, string value)
    {
        var border = new Border
        {
            BorderBrush = System.Windows.Media.Brushes.LightGray,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(7),
            Padding = new Thickness(14),
            Margin = new Thickness(column == 0 ? 0 : 6, 0, column == 2 ? 0 : 6, 0)
        };
        Grid.SetColumn(border, column);
        parent.Children.Add(border);
        var stack = new StackPanel();
        border.Child = stack;
        stack.Children.Add(new TextBlock { Text = caption, Opacity = 0.67, FontSize = 12 });
        var text = new TextBlock { Text = value, FontSize = column == 2 ? 15 : 22, FontWeight = FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap };
        stack.Children.Add(text);
        return text;
    }

    private static Button MakeButton(string text, double width)
    {
        return new Button
        {
            Content = text,
            Width = width,
            Height = 36,
            Margin = new Thickness(0, 0, 8, 0),
            Padding = new Thickness(8, 4, 8, 4)
        };
    }

    private static DataGrid BuildChangesGrid()
    {
        var grid = new DataGrid
        {
            AutoGenerateColumns = false,
            IsReadOnly = true,
            CanUserAddRows = false,
            CanUserDeleteRows = false,
            SelectionMode = DataGridSelectionMode.Single,
            HeadersVisibility = DataGridHeadersVisibility.Column,
            GridLinesVisibility = DataGridGridLinesVisibility.Horizontal
        };
        grid.Columns.Add(new DataGridTextColumn { Header = "Dosya", Binding = new Binding(nameof(ChangeView.Dosya)), Width = 100 });
        grid.Columns.Add(new DataGridTextColumn { Header = "İşlem", Binding = new Binding(nameof(ChangeView.Islem)), Width = 110 });
        grid.Columns.Add(new DataGridTextColumn { Header = "Değişen Alanlar", Binding = new Binding(nameof(ChangeView.DegisenAlanlar)), Width = new DataGridLength(2, DataGridLengthUnitType.Star) });
        grid.Columns.Add(new DataGridTextColumn { Header = "Hakim", Binding = new Binding(nameof(ChangeView.Hakim)), Width = new DataGridLength(1.2, DataGridLengthUnitType.Star) });
        grid.Columns.Add(new DataGridTextColumn { Header = "Dava Türü", Binding = new Binding(nameof(ChangeView.DavaTuru)), Width = new DataGridLength(1.2, DataGridLengthUnitType.Star) });
        grid.Columns.Add(new DataGridTextColumn { Header = "Karar No", Binding = new Binding(nameof(ChangeView.KararNo)), Width = 100 });
        return grid;
    }

    private void LoadExistingState()
    {
        try
        {
            _records = FdbEngine.LoadBaselineRecords();
            if (_records.Count > 0)
            {
                _totalText.Text = _records.Count.ToString("N0", new CultureInfo("tr-TR"));
                _changeText.Text = "-";
                _lastScanText.Text = "Mevcut yerel hafıza yüklendi";
                _statusText.Text = "Analiz kullanıma hazır. Yeni FDB geldiğinde Tara'ya basın.";
            }
            _lastChangesPath = FindLatestChangesFile();
        }
        catch
        {
            _records = new();
        }
    }

    private async void ScanButton_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog
        {
            Title = "Güncel DOSYATKP.FDB dosyasını seçin",
            Filter = "Firebird veritabanı (*.FDB)|*.FDB|Tüm dosyalar (*.*)|*.*",
            Multiselect = false
        };
        if (dlg.ShowDialog(this) != true) return;

        string fdbPath = dlg.FileName;
        string firebirdFolder = FdbEngine.FindFirebirdFolder(fdbPath);
        if (string.IsNullOrWhiteSpace(firebirdFolder))
        {
            MessageBox.Show(this, "Firebird okuma araçları otomatik bulunamadı. Bir kez gbak.exe dosyasını seçin. Aynı klasörde isql.exe de bulunmalı.", "FDB Takip");
            var fb = new OpenFileDialog { Title = "Firebird gbak.exe seçin", Filter = "gbak.exe|gbak.exe|EXE (*.exe)|*.exe", FileName = "gbak.exe" };
            if (fb.ShowDialog(this) != true) return;
            firebirdFolder = Path.GetDirectoryName(fb.FileName) ?? "";
            if (!FdbEngine.IsFirebirdFolder(firebirdFolder))
            {
                MessageBox.Show(this, "Seçilen klasörde hem gbak.exe hem isql.exe bulunamadı.", "FDB Takip");
                return;
            }
            FdbEngine.SaveFirebirdFolder(firebirdFolder);
        }

        try
        {
            _scanButton.IsEnabled = false;
            _statusText.Text = "FDB okunuyor ve karşılaştırılıyor...";
            System.Windows.Input.Mouse.OverrideCursor = System.Windows.Input.Cursors.Wait;
            var result = await Task.Run(() => FdbEngine.Scan(fdbPath, firebirdFolder));
            _records = result.Records;
            _lastChangesPath = result.ChangesOutputPath;
            _totalText.Text = result.TotalCount.ToString("N0", new CultureInfo("tr-TR"));
            _changeText.Text = result.ChangeCount.ToString("N0", new CultureInfo("tr-TR"));
            _lastScanText.Text = DateTime.Now.ToString("dd.MM.yyyy HH:mm") + " • " + Path.GetFileName(fdbPath);
            _statusText.Text = result.FirstRun
                ? "İlk hafıza oluşturuldu. Tum_Kayitlar.xlsx kaydedildi."
                : "Tarama tamamlandı. Excel dosyaları Belgeler > FDB Takip Ciktilari klasöründe.";
            _changesGrid.ItemsSource = result.Changes.Select(c => new ChangeView(c)).ToList();

            string message = result.FirstRun
                ? $"İlk tarama tamamlandı.\nToplam kayıt: {result.TotalCount:N0}\n\nTüm kayıtlar:\n{result.AllOutputPath}"
                : $"Tarama tamamlandı.\nToplam kayıt: {result.TotalCount:N0}\nYeni/Güncellenen: {result.ChangeCount:N0}\n\nTüm kayıtlar:\n{result.AllOutputPath}" +
                  (result.ChangeCount > 0 ? $"\n\nYeni/Güncellenen:\n{result.ChangesOutputPath}" : "");
            MessageBox.Show(this, message, "FDB Takip");
        }
        catch (Exception ex)
        {
            _statusText.Text = "Hata oluştu.";
            MessageBox.Show(this, ex.Message, "FDB Takip - Hata", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            System.Windows.Input.Mouse.OverrideCursor = null;
            _scanButton.IsEnabled = true;
        }
    }

    private void AnalyzeButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (_records.Count == 0) _records = FdbEngine.LoadBaselineRecords();
            if (_records.Count == 0)
            {
                MessageBox.Show(this, "Önce en az bir FDB tarayın.", "FDB Takip");
                return;
            }
            var result = LocalAnalyzer.Analyze(_questionBox.Text, _records);
            _analysisBox.Text = result.Summary;
            _lastAnalysisRecords = result.MatchingRecords;
            _exportAnalysisButton.IsEnabled = _lastAnalysisRecords.Count > 0;
        }
        catch (Exception ex)
        {
            _analysisBox.Text = "Analiz hatası: " + ex.Message;
            _lastAnalysisRecords = new();
            _exportAnalysisButton.IsEnabled = false;
        }
    }

    private void ExportAnalysisButton_Click(object sender, RoutedEventArgs e)
    {
        if (_lastAnalysisRecords.Count == 0) return;
        try
        {
            Directory.CreateDirectory(FdbEngine.OutputDirectory);
            string path = Path.Combine(FdbEngine.OutputDirectory, "Analiz_Sonucu_" + DateTime.Now.ToString("yyyy-MM-dd_HHmmss") + ".xlsx");
            ExcelWriter.WriteAllXlsx(path, _lastAnalysisRecords, "Analiz Sonucu");
            MessageBox.Show(this, "Analiz sonucu Excel'e aktarıldı:\n" + path, "FDB Takip");
            OpenFileIfExists(path, "");
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Excel aktarım hatası");
        }
    }

    private string FindLatestChangesFile()
    {
        try
        {
            if (!Directory.Exists(FdbEngine.OutputDirectory)) return "";
            return Directory.GetFiles(FdbEngine.OutputDirectory, "Yeni_Guncellenenler_*.xlsx")
                .OrderByDescending(File.GetLastWriteTime)
                .FirstOrDefault() ?? "";
        }
        catch { return ""; }
    }

    private void OpenFileIfExists(string path, string missingMessage)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            if (!string.IsNullOrWhiteSpace(missingMessage)) MessageBox.Show(this, missingMessage, "FDB Takip");
            return;
        }
        try
        {
            Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
        }
        catch (Exception ex) { MessageBox.Show(this, ex.Message, "Dosya açılamadı"); }
    }

    private void OpenFolder(string folder)
    {
        try
        {
            Directory.CreateDirectory(folder);
            Process.Start(new ProcessStartInfo("explorer.exe", "\"" + folder + "\"") { UseShellExecute = true });
        }
        catch (Exception ex) { MessageBox.Show(this, ex.Message, "Klasör açılamadı"); }
    }
}

internal sealed class ChangeView
{
    public string Dosya { get; }
    public string Islem { get; }
    public string DegisenAlanlar { get; }
    public string Hakim { get; }
    public string DavaTuru { get; }
    public string KararNo { get; }

    public ChangeView(ChangeItem item)
    {
        Dosya = FdbEngine.Value(item.Record, "ESASYILI") + "/" + FdbEngine.Value(item.Record, "ESASNO");
        Islem = item.Operation;
        DegisenAlanlar = item.ChangedFields;
        Hakim = FdbEngine.Value(item.Record, "TEKHAKIM");
        if (string.IsNullOrWhiteSpace(Hakim)) Hakim = FdbEngine.Value(item.Record, "BASKAN");
        DavaTuru = FdbEngine.Value(item.Record, "UYAPDAVATURU");
        if (string.IsNullOrWhiteSpace(DavaTuru)) DavaTuru = FdbEngine.Value(item.Record, "DAVATURU");
        string ky = FdbEngine.Value(item.Record, "KARARYILI");
        string kn = FdbEngine.Value(item.Record, "KARARNO");
        KararNo = string.IsNullOrWhiteSpace(kn) ? "" : (string.IsNullOrWhiteSpace(ky) ? kn : ky + "/" + kn);
    }
}

internal sealed class ScanResult
{
    public int TotalCount { get; init; }
    public int ChangeCount { get; init; }
    public bool FirstRun { get; init; }
    public string AllOutputPath { get; init; } = "";
    public string ChangesOutputPath { get; init; } = "";
    public List<Dictionary<string, string>> Records { get; init; } = new();
    public List<ChangeItem> Changes { get; init; } = new();
}

internal sealed class ChangeItem
{
    public string Operation { get; init; } = "";
    public string ChangedFields { get; init; } = "";
    public Dictionary<string, string> Record { get; init; } = new(StringComparer.OrdinalIgnoreCase);
}

internal static class FdbEngine
{
    public static readonly string[] Fields =
    {
        "ESASYILI","ESASNO","BASKAN","TEKHAKIM","DAVATARIHI","HEDEFSURE","HEDEFTARIH",
        "DAVATURU","UYAPDAVATURU","DOSYADURUMU","ELEALINMATARIHI","DAVACI","DAVALI",
        "DURUSMATARIHI","KESIFTARIHI","KESIFSAATI","KESIFYERI","BILIRKISITARIHI",
        "KARARYILI","KARARNO","KARARTARIHI","KARAR","ACIKLAMA","SONISLEMTARIHI"
    };

    public static readonly string[] Headers =
    {
        "E.Yılı","E.No","Başkan","Tek Hakim","Dava Tarihi","Hdf","Hedef Tarih",
        "Dava Türü","UYAP Dava Türü","Dosya Durumu","Ele Al. Tar.","Davacı","Davalı",
        "Duruşma T.","Keşif Tarihi","Keşif Saati","Keşif Yeri","Bilirkişi Ver. T.",
        "K.Yıl","K.No","Karar Tarihi","Verilen Karar","UYAP Açıklaması","Son İşl. Tar."
    };

    private const string Query = @"SET LIST ON;
SET COUNT OFF;
SELECT
  D.ESASYILI AS ESASYILI,
  D.ESASNO AS ESASNO,
  REPLACE(REPLACE(HB.ADISOYADI, ASCII_CHAR(13), ' '), ASCII_CHAR(10), ' ') AS BASKAN,
  REPLACE(REPLACE(HT.ADISOYADI, ASCII_CHAR(13), ' '), ASCII_CHAR(10), ' ') AS TEKHAKIM,
  D.DAVATARIHI AS DAVATARIHI,
  D.HEDEFSURE AS HEDEFSURE,
  D.HEDEFTARIH AS HEDEFTARIH,
  REPLACE(REPLACE(DT.DAVATURU, ASCII_CHAR(13), ' '), ASCII_CHAR(10), ' ') AS DAVATURU,
  REPLACE(REPLACE(D.UYAPDAVATURU, ASCII_CHAR(13), ' '), ASCII_CHAR(10), ' ') AS UYAPDAVATURU,
  REPLACE(REPLACE(DD.DOSYADURUMU, ASCII_CHAR(13), ' '), ASCII_CHAR(10), ' ') AS DOSYADURUMU,
  D.ELEALINMATARIHI AS ELEALINMATARIHI,
  REPLACE(REPLACE(D.DAVACI, ASCII_CHAR(13), ' '), ASCII_CHAR(10), ' ') AS DAVACI,
  REPLACE(REPLACE(D.DAVALI, ASCII_CHAR(13), ' '), ASCII_CHAR(10), ' ') AS DAVALI,
  D.DURUSMATARIHI AS DURUSMATARIHI,
  D.KESIFTARIHI AS KESIFTARIHI,
  D.KESIFSAATI AS KESIFSAATI,
  REPLACE(REPLACE(D.KESIFYERI, ASCII_CHAR(13), ' '), ASCII_CHAR(10), ' ') AS KESIFYERI,
  D.BILIRKISITARIHI AS BILIRKISITARIHI,
  D.KARARYILI AS KARARYILI,
  D.KARARNO AS KARARNO,
  D.KARARTARIHI AS KARARTARIHI,
  REPLACE(REPLACE(D.KARAR, ASCII_CHAR(13), ' '), ASCII_CHAR(10), ' ') AS KARAR,
  REPLACE(REPLACE(D.ACIKLAMA, ASCII_CHAR(13), ' '), ASCII_CHAR(10), ' ') AS ACIKLAMA,
  D.SONISLEMTARIHI AS SONISLEMTARIHI
FROM DOSYALAR D
LEFT JOIN HAKIMLER HB ON HB.SICILNO = D.BASKANSICNO
LEFT JOIN HAKIMLER HT ON HT.SICILNO = D.TEKHAKIMSICNO
LEFT JOIN DAVATURU DT ON DT.DAVATURKODU = D.DAVATURKODU
LEFT JOIN DOSYADURUMU DD ON DD.DOSYADURUMKODU = D.DOSYADURUMKODU
ORDER BY D.ESASYILI, D.ESASNO;
QUIT;";

    public static string DataDirectory => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "FDBTakipUiPath");
    public static string HistoryDirectory => Path.Combine(DataDirectory, "history");
    public static string OutputDirectory => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "FDB Takip Ciktilari");
    public static string AllOutputPath => Path.Combine(OutputDirectory, "Tum_Kayitlar.xlsx");
    private static string BaselinePath => Path.Combine(DataDirectory, "baseline.tsv");
    private static string StatePath => Path.Combine(DataDirectory, "state.txt");
    private static string FirebirdConfig => Path.Combine(DataDirectory, "firebird_dir.txt");

    public static string Value(Dictionary<string, string> record, string key)
        => record.TryGetValue(key, out string value) ? NormalizeValue(value) : "";

    private static string NormalizeValue(string value)
        => value == null || value == "<null>" ? "" : value.TrimEnd();

    public static bool IsFirebirdFolder(string folder)
    {
        if (string.IsNullOrWhiteSpace(folder)) return false;
        return File.Exists(Path.Combine(folder, "gbak.exe")) && File.Exists(Path.Combine(folder, "isql.exe"));
    }

    public static void SaveFirebirdFolder(string folder)
    {
        Directory.CreateDirectory(DataDirectory);
        File.WriteAllText(FirebirdConfig, folder, new UTF8Encoding(false));
    }

    public static string FindFirebirdFolder(string fdbPath)
    {
        try
        {
            if (File.Exists(FirebirdConfig))
            {
                string saved = File.ReadAllText(FirebirdConfig, Encoding.UTF8).Trim();
                if (IsFirebirdFolder(saved)) return saved;
            }
        }
        catch { }

        var candidates = new List<string>();
        string pf = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        string pfx86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
        foreach (string baseDir in new[] { pf, pfx86 })
        {
            if (string.IsNullOrWhiteSpace(baseDir)) continue;
            candidates.Add(Path.Combine(baseDir, "Firebird", "Firebird_5_0"));
            candidates.Add(Path.Combine(baseDir, "Firebird", "Firebird_4_0"));
            candidates.Add(Path.Combine(baseDir, "Firebird", "Firebird_3_0"));
            candidates.Add(Path.Combine(baseDir, "Firebird"));
        }

        string dir = Path.GetDirectoryName(fdbPath) ?? "";
        for (int i = 0; i < 5 && !string.IsNullOrWhiteSpace(dir); i++)
        {
            candidates.Add(dir);
            candidates.Add(Path.Combine(dir, "bin"));
            candidates.Add(Path.Combine(dir, "Firebird"));
            DirectoryInfo parent = Directory.GetParent(dir);
            if (parent == null || parent.FullName == dir) break;
            dir = parent.FullName;
        }

        foreach (string candidate in candidates)
            if (IsFirebirdFolder(candidate)) return candidate;

        foreach (string baseDir in new[] { pf, pfx86 })
        {
            if (string.IsNullOrWhiteSpace(baseDir) || !Directory.Exists(baseDir)) continue;
            try
            {
                foreach (string d in Directory.GetDirectories(baseDir, "*Firebird*", SearchOption.TopDirectoryOnly))
                {
                    if (IsFirebirdFolder(d)) return d;
                    try
                    {
                        foreach (string sub in Directory.GetDirectories(d, "*", SearchOption.AllDirectories))
                            if (IsFirebirdFolder(sub)) return sub;
                    }
                    catch { }
                }
            }
            catch { }
        }
        return "";
    }

    public static ScanResult Scan(string fdbPath, string firebirdFolder)
    {
        Directory.CreateDirectory(DataDirectory);
        Directory.CreateDirectory(HistoryDirectory);
        Directory.CreateDirectory(OutputDirectory);

        var records = ReadFdb(fdbPath, firebirdFolder);
        ExcelWriter.WriteAllXlsx(AllOutputPath, records, "Tüm Kayıtlar");

        string currentMax = MaxSonIslem(records);
        string previousMax = File.Exists(StatePath) ? File.ReadAllText(StatePath, Encoding.UTF8).Trim() : "";
        if (!string.IsNullOrWhiteSpace(previousMax) && !string.IsNullOrWhiteSpace(currentMax) && string.CompareOrdinal(currentMax, previousMax) < 0)
        {
            throw new Exception("Seçilen FDB, daha önce işlenen FDB'den eski görünüyor. Hafıza korunarak işlem durduruldu.\nÖnceki son işlem: " + previousMax + "\nSeçilen FDB son işlem: " + currentMax);
        }

        var baseline = LoadBaselineCanonical();
        bool firstRun = baseline.Count == 0;
        var changes = new List<ChangeItem>();

        if (!firstRun)
        {
            foreach (var record in records)
            {
                string key = KeyOf(record);
                string currentCanonical = Canonical(record);
                if (!baseline.ContainsKey(key))
                {
                    changes.Add(new ChangeItem { Operation = "YENİ", ChangedFields = "Yeni dosya", Record = record });
                }
                else if (!string.Equals(baseline[key], currentCanonical, StringComparison.Ordinal))
                {
                    changes.Add(new ChangeItem { Operation = "GÜNCELLENDİ", ChangedFields = ChangedFields(baseline[key], record), Record = record });
                }
            }
        }

        if (File.Exists(BaselinePath))
        {
            string hist = Path.Combine(HistoryDirectory, "baseline_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".tsv");
            File.Copy(BaselinePath, hist, true);
        }

        string changesPath = "";
        if (changes.Count > 0)
        {
            changesPath = Path.Combine(OutputDirectory, "Yeni_Guncellenenler_" + DateTime.Now.ToString("yyyy-MM-dd_HHmmss") + ".xlsx");
            ExcelWriter.WriteChangesXlsx(changesPath, changes);
        }

        SaveBaseline(records);
        File.WriteAllText(StatePath, currentMax, new UTF8Encoding(false));

        return new ScanResult
        {
            TotalCount = records.Count,
            ChangeCount = changes.Count,
            FirstRun = firstRun,
            AllOutputPath = AllOutputPath,
            ChangesOutputPath = changesPath,
            Records = records,
            Changes = changes
        };
    }

    public static List<Dictionary<string, string>> LoadBaselineRecords()
    {
        var list = new List<Dictionary<string, string>>();
        if (!File.Exists(BaselinePath)) return list;
        foreach (string line in File.ReadAllLines(BaselinePath, Encoding.UTF8))
        {
            if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#")) continue;
            int tab = line.IndexOf('\t');
            if (tab <= 0) continue;
            try
            {
                string canonical = Encoding.UTF8.GetString(Convert.FromBase64String(line[(tab + 1)..]));
                string[] values = canonical.Split('\u001F');
                var rec = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                for (int i = 0; i < Fields.Length; i++) rec[Fields[i]] = i < values.Length ? NormalizeValue(values[i]) : "";
                list.Add(rec);
            }
            catch { }
        }
        return list;
    }

    private static List<Dictionary<string, string>> ReadFdb(string fdbPath, string firebirdFolder)
    {
        string gbak = Path.Combine(firebirdFolder, "gbak.exe");
        string isql = Path.Combine(firebirdFolder, "isql.exe");
        string tempDir = Path.Combine(Path.GetTempPath(), "FDBTakip_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        try
        {
            string backup = Path.Combine(tempDir, "readcopy.fbk");
            string restored = Path.Combine(tempDir, "readcopy.fdb");
            string queryPath = Path.Combine(tempDir, "query.sql");
            File.WriteAllText(queryPath, Query, new UTF8Encoding(false));
            RunProcess(gbak, new[] { "-b", "-g", "-user", "SYSDBA", "-password", "masterkey", fdbPath, backup }, firebirdFolder);
            RunProcess(gbak, new[] { "-c", "-user", "SYSDBA", "-password", "masterkey", backup, restored }, firebirdFolder);
            string output = RunProcess(isql, new[] { "-q", "-ch", "UTF8", "-user", "SYSDBA", "-password", "masterkey", restored, "-i", queryPath }, firebirdFolder);
            return ParseIsql(output);
        }
        finally
        {
            try { if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true); } catch { }
        }
    }

    private static string RunProcess(string exe, IEnumerable<string> args, string workingDir)
    {
        var psi = new ProcessStartInfo
        {
            FileName = exe,
            WorkingDirectory = workingDir,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = new UTF8Encoding(false),
            StandardErrorEncoding = new UTF8Encoding(false)
        };
        foreach (string arg in args) psi.ArgumentList.Add(arg);
        using var p = new Process { StartInfo = psi };
        p.Start();
        string stdout = p.StandardOutput.ReadToEnd();
        string stderr = p.StandardError.ReadToEnd();
        p.WaitForExit();
        if (p.ExitCode != 0)
        {
            string combined = (stdout + "\n" + stderr).Trim();
            if (combined.Length > 3000) combined = combined[^3000..];
            throw new Exception(Path.GetFileName(exe) + " başarısız oldu.\n" + combined);
        }
        return stdout;
    }

    private static List<Dictionary<string, string>> ParseIsql(string text)
    {
        var known = new HashSet<string>(Fields, StringComparer.OrdinalIgnoreCase);
        var records = new List<Dictionary<string, string>>();
        var current = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        string currentField = "";
        string[] lines = text.Replace("\r\n", "\n").Replace("\r", "\n").Split('\n');
        foreach (string raw in lines)
        {
            string line = raw ?? "";
            if (string.IsNullOrWhiteSpace(line))
            {
                AddParsedRecord(records, current);
                current = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                currentField = "";
                continue;
            }
            int prefixLen = Math.Min(32, line.Length);
            string candidate = line[..prefixLen].Trim();
            if (known.Contains(candidate))
            {
                string value = line.Length > 32 ? line[32..].TrimStart() : "";
                current[candidate] = NormalizeValue(value);
                currentField = candidate;
            }
            else if (!string.IsNullOrEmpty(currentField) && current.ContainsKey(currentField))
            {
                current[currentField] = (current[currentField] + " " + line.Trim()).Trim();
            }
        }
        AddParsedRecord(records, current);
        if (records.Count == 0) throw new Exception("FDB içinden DOSYALAR kayıtları okunamadı.");
        return records;
    }

    private static void AddParsedRecord(List<Dictionary<string, string>> records, Dictionary<string, string> current)
    {
        if (!current.ContainsKey("ESASYILI") || !current.ContainsKey("ESASNO")) return;
        var copy = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (string f in Fields) copy[f] = current.ContainsKey(f) ? NormalizeValue(current[f]) : "";
        records.Add(copy);
    }

    private static string Canonical(Dictionary<string, string> record)
        => string.Join("\u001F", Fields.Select(f => Value(record, f)));

    private static string KeyOf(Dictionary<string, string> record)
        => Value(record, "ESASYILI") + "/" + Value(record, "ESASNO");

    private static Dictionary<string, string> LoadBaselineCanonical()
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (!File.Exists(BaselinePath)) return result;
        foreach (string line in File.ReadAllLines(BaselinePath, Encoding.UTF8))
        {
            if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#")) continue;
            int tab = line.IndexOf('\t');
            if (tab <= 0) continue;
            try { result[line[..tab]] = Encoding.UTF8.GetString(Convert.FromBase64String(line[(tab + 1)..])); }
            catch { }
        }
        return result;
    }

    private static void SaveBaseline(List<Dictionary<string, string>> records)
    {
        string tmp = BaselinePath + ".tmp";
        using (var sw = new StreamWriter(tmp, false, new UTF8Encoding(false)))
        {
            sw.WriteLine("# FDB Takip baseline v1.2");
            foreach (var record in records)
            {
                string b64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(Canonical(record)));
                sw.Write(KeyOf(record));
                sw.Write('\t');
                sw.WriteLine(b64);
            }
        }
        if (File.Exists(BaselinePath)) File.Delete(BaselinePath);
        File.Move(tmp, BaselinePath);
    }

    private static string ChangedFields(string oldCanonical, Dictionary<string, string> current)
    {
        string[] oldValues = oldCanonical.Split('\u001F');
        var changed = new List<string>();
        for (int i = 0; i < Fields.Length; i++)
        {
            string oldValue = i < oldValues.Length ? NormalizeValue(oldValues[i]) : "";
            string newValue = Value(current, Fields[i]);
            if (!string.Equals(oldValue, newValue, StringComparison.Ordinal)) changed.Add(Headers[i]);
        }
        return string.Join(", ", changed);
    }

    private static string MaxSonIslem(List<Dictionary<string, string>> records)
    {
        string max = "";
        foreach (var record in records)
        {
            string v = Value(record, "SONISLEMTARIHI");
            if (!string.IsNullOrEmpty(v) && string.CompareOrdinal(v, max) > 0) max = v;
        }
        return max;
    }
}

internal static class ExcelWriter
{
    public static void WriteChangesXlsx(string path, List<ChangeItem> changes)
    {
        string[] headers = new string[FdbEngine.Headers.Length + 2];
        headers[0] = "İşlem Türü";
        headers[1] = "Değişen Alanlar";
        Array.Copy(FdbEngine.Headers, 0, headers, 2, FdbEngine.Headers.Length);

        var rows = new List<string[]>();
        foreach (var change in changes)
        {
            var vals = new string[headers.Length];
            vals[0] = change.Operation;
            vals[1] = change.ChangedFields;
            for (int i = 0; i < FdbEngine.Fields.Length; i++) vals[i + 2] = FdbEngine.Value(change.Record, FdbEngine.Fields[i]);
            rows.Add(vals);
        }
        WriteWorkbook(path, "Yeni-Güncellenen", headers, rows, true);
    }

    public static void WriteAllXlsx(string path, List<Dictionary<string, string>> records, string sheetName)
    {
        var rows = new List<string[]>(records.Count);
        foreach (var record in records)
        {
            var vals = new string[FdbEngine.Fields.Length];
            for (int i = 0; i < FdbEngine.Fields.Length; i++) vals[i] = FdbEngine.Value(record, FdbEngine.Fields[i]);
            rows.Add(vals);
        }
        WriteWorkbook(path, sheetName, FdbEngine.Headers, rows, false);
    }

    private static void WriteWorkbook(string path, string sheetName, string[] headers, List<string[]> rows, bool changeWorkbook)
    {
        var sb = new StringBuilder();
        sb.Append("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>");
        sb.Append("<worksheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\">");
        sb.Append("<sheetViews><sheetView workbookViewId=\"0\"><pane ySplit=\"1\" topLeftCell=\"A2\" activePane=\"bottomLeft\" state=\"frozen\"/></sheetView></sheetViews>");
        sb.Append("<cols>");
        for (int i = 1; i <= headers.Length; i++)
        {
            double width = changeWorkbook && i == 2 ? 42 : 18;
            if ((!changeWorkbook && (i == 9 || i == 12 || i == 13 || i == 22 || i == 23)) ||
                (changeWorkbook && (i == 11 || i == 14 || i == 15 || i == 24 || i == 25))) width = 42;
            sb.Append("<col min=\"").Append(i).Append("\" max=\"").Append(i).Append("\" width=\"")
              .Append(width.ToString(CultureInfo.InvariantCulture)).Append("\" customWidth=\"1\"/>");
        }
        sb.Append("</cols><sheetData>");
        sb.Append("<row r=\"1\">");
        for (int c = 0; c < headers.Length; c++)
        {
            string cell = ExcelColumn(c + 1) + "1";
            sb.Append("<c r=\"").Append(cell).Append("\" t=\"inlineStr\"><is><t xml:space=\"preserve\">")
              .Append(XmlEscape(headers[c])).Append("</t></is></c>");
        }
        sb.Append("</row>");

        int rowNo = 2;
        foreach (string[] row in rows)
        {
            sb.Append("<row r=\"").Append(rowNo).Append("\">");
            for (int c = 0; c < headers.Length; c++)
            {
                string cell = ExcelColumn(c + 1) + rowNo;
                string value = c < row.Length ? row[c] ?? "" : "";
                sb.Append("<c r=\"").Append(cell).Append("\" t=\"inlineStr\"><is><t xml:space=\"preserve\">")
                  .Append(XmlEscape(value)).Append("</t></is></c>");
            }
            sb.Append("</row>");
            rowNo++;
        }
        sb.Append("</sheetData><autoFilter ref=\"A1:").Append(ExcelColumn(headers.Length)).Append(Math.Max(1, rowNo - 1)).Append("\"/></worksheet>");

        string contentTypes = "<?xml version=\"1.0\" encoding=\"UTF-8\"?><Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\"><Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/><Default Extension=\"xml\" ContentType=\"application/xml\"/><Override PartName=\"/xl/workbook.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml\"/><Override PartName=\"/xl/worksheets/sheet1.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml\"/></Types>";
        string rootRels = "<?xml version=\"1.0\" encoding=\"UTF-8\"?><Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\"><Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument\" Target=\"xl/workbook.xml\"/></Relationships>";
        string workbook = "<?xml version=\"1.0\" encoding=\"UTF-8\"?><workbook xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\" xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\"><sheets><sheet name=\"" + XmlEscape(sheetName) + "\" sheetId=\"1\" r:id=\"rId1\"/></sheets></workbook>";
        string wbRels = "<?xml version=\"1.0\" encoding=\"UTF-8\"?><Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\"><Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet\" Target=\"worksheets/sheet1.xml\"/></Relationships>";

        Directory.CreateDirectory(Path.GetDirectoryName(path) ?? ".");
        if (File.Exists(path)) File.Delete(path);
        using var fs = new FileStream(path, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None);
        using var zip = new ZipArchive(fs, ZipArchiveMode.Create);
        WriteZipText(zip, "[Content_Types].xml", contentTypes);
        WriteZipText(zip, "_rels/.rels", rootRels);
        WriteZipText(zip, "xl/workbook.xml", workbook);
        WriteZipText(zip, "xl/_rels/workbook.xml.rels", wbRels);
        WriteZipText(zip, "xl/worksheets/sheet1.xml", sb.ToString());
    }

    private static void WriteZipText(ZipArchive zip, string entryName, string content)
    {
        var entry = zip.CreateEntry(entryName, CompressionLevel.Optimal);
        using var stream = entry.Open();
        using var writer = new StreamWriter(stream, new UTF8Encoding(false));
        writer.Write(content);
    }

    private static string XmlEscape(string value)
        => (value ?? "").Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;").Replace("'", "&apos;");

    private static string ExcelColumn(int oneBased)
    {
        string s = "";
        int n = oneBased;
        while (n > 0)
        {
            n--;
            s = ((char)('A' + n % 26)) + s;
            n /= 26;
        }
        return s;
    }
}

internal sealed class AnalysisResult
{
    public string Summary { get; init; } = "";
    public List<Dictionary<string, string>> MatchingRecords { get; init; } = new();
}

internal sealed class DateRange
{
    public DateTime Start { get; init; }
    public DateTime End { get; init; }
    public string Description { get; init; } = "";
    public bool Contains(DateTime date) => date.Date >= Start.Date && date.Date <= End.Date;
}

internal static class LocalAnalyzer
{
    private static readonly CultureInfo Tr = new("tr-TR");
    private static readonly string[] MonthNames = { "ocak", "subat", "mart", "nisan", "mayis", "haziran", "temmuz", "agustos", "eylul", "ekim", "kasim", "aralik" };

    public static AnalysisResult Analyze(string question, List<Dictionary<string, string>> records)
    {
        if (records == null || records.Count == 0) return new AnalysisResult { Summary = "Analiz edilecek kayıt yok." };
        if (string.IsNullOrWhiteSpace(question)) return new AnalysisResult { Summary = "Bir soru yazın." };

        string q = NormalizeText(question);
        string judge = FindJudge(question, records);
        DateRange range = ResolveDateRange(question, judge, records);

        if ((q.Contains("en cok") || q.Contains("en fazla")) && q.Contains("hakim") && q.Contains("karar"))
            return RankJudgesByDecision(records, range);

        if ((q.Contains("en cok") || q.Contains("en fazla")) && q.Contains("dava tur"))
            return RankCaseTypes(records, range);

        if (!string.IsNullOrWhiteSpace(judge))
            return JudgeAnalysis(judge, records, range);

        string caseType = FindCaseType(question, records);
        if (!string.IsNullOrWhiteSpace(caseType))
            return CaseTypeAnalysis(caseType, records, range);

        return GeneralAnalysis(records, range);
    }

    private static AnalysisResult JudgeAnalysis(string judge, List<Dictionary<string, string>> records, DateRange range)
    {
        var judgeRecords = records.Where(r => JudgeMatches(r, judge)).ToList();
        if (judgeRecords.Count == 0) return new AnalysisResult { Summary = judge + " adına kayıt bulunamadı." };

        if (range == null)
        {
            var allLookDates = judgeRecords.Select(LookDate).Where(d => d.HasValue).Select(d => d.Value).ToList();
            if (allLookDates.Count > 0)
            {
                DateTime min = allLookDates.Min();
                DateTime max = allLookDates.Max();
                range = new DateRange { Start = min, End = max, Description = min.ToString("dd.MM.yyyy") + " - " + max.ToString("dd.MM.yyyy") };
            }
        }

        var looked = judgeRecords.Where(r =>
        {
            DateTime? d = LookDate(r);
            return d.HasValue && (range == null || range.Contains(d.Value));
        }).ToList();

        var decidedInPeriod = judgeRecords.Where(r =>
        {
            DateTime? d = DecisionDate(r);
            return HasDecision(r) && d.HasValue && (range == null || range.Contains(d.Value));
        }).ToList();

        var lookedAndEventuallyDecided = looked.Where(HasDecision).ToList();
        var pendingFromLooked = looked.Where(r => !HasDecision(r)).ToList();
        double completion = looked.Count == 0 ? 0 : 100.0 * lookedAndEventuallyDecided.Count / looked.Count;

        string period = range == null ? "Tüm kayıtlar" : range.Description;
        string note = "“Baktığı dosya” hesabında Ele Alınma Tarihi varsa o, yoksa Dava Tarihi kullanılır. “Karara çıkardığı” hesabında Karar Tarihi kullanılır.";
        if (NormalizeText(period).Contains("veride gorunen ilk")) note += " Bu başlangıç tarihi resmi atama tarihi değildir; FDB'de hakim adına görünen en erken dosya tarihidir.";

        var matching = looked.Concat(decidedInPeriod)
            .GroupBy(r => FdbEngine.Value(r, "ESASYILI") + "/" + FdbEngine.Value(r, "ESASNO"), StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .OrderByDescending(r => ParseDate(FdbEngine.Value(r, "SONISLEMTARIHI")) ?? DateTime.MinValue)
            .ToList();

        var sb = new StringBuilder();
        sb.AppendLine(judge);
        sb.AppendLine("Dönem: " + period);
        sb.AppendLine(new string('-', 55));
        sb.AppendLine("Baktığı / ele aldığı dosya: " + looked.Count.ToString("N0", Tr));
        sb.AppendLine("Dönemde karara çıkardığı dosya: " + decidedInPeriod.Count.ToString("N0", Tr));
        sb.AppendLine("Baktığı dosyalardan karar bilgisi oluşan: " + lookedAndEventuallyDecided.Count.ToString("N0", Tr));
        sb.AppendLine("Baktığı dosyalardan halen karar bilgisi olmayan: " + pendingFromLooked.Count.ToString("N0", Tr));
        sb.AppendLine("Baktığı dosya grubunda karar oranı: %" + completion.ToString("0.0", Tr));
        sb.AppendLine();
        sb.AppendLine(note);
        sb.AppendLine();
        sb.AppendLine("Alttaki “Sonucu Excel'e Aktar” ile bu hesabı oluşturan dosyaları görebilirsiniz.");

        return new AnalysisResult { Summary = sb.ToString(), MatchingRecords = matching };
    }

    private static AnalysisResult RankJudgesByDecision(List<Dictionary<string, string>> records, DateRange range)
    {
        var decisions = records.Where(r => HasDecision(r) && DecisionDate(r).HasValue && (range == null || range.Contains(DecisionDate(r).Value))).ToList();
        var ranked = decisions
            .Select(r => new { Record = r, Judge = PrimaryJudge(r) })
            .Where(x => !string.IsNullOrWhiteSpace(x.Judge))
            .GroupBy(x => x.Judge, StringComparer.CurrentCultureIgnoreCase)
            .Select(g => new { Judge = g.Key, Count = g.Count() })
            .OrderByDescending(x => x.Count)
            .ThenBy(x => x.Judge)
            .Take(15)
            .ToList();

        var sb = new StringBuilder();
        sb.AppendLine("Karara çıkan dosya sayısına göre hakimler");
        sb.AppendLine("Dönem: " + (range?.Description ?? "Tüm kayıtlar"));
        sb.AppendLine(new string('-', 55));
        int i = 1;
        foreach (var x in ranked) sb.AppendLine((i++).ToString().PadLeft(2) + ". " + x.Judge + " — " + x.Count.ToString("N0", Tr));
        if (ranked.Count == 0) sb.AppendLine("Bu ölçüte uyan kayıt bulunamadı.");
        return new AnalysisResult { Summary = sb.ToString(), MatchingRecords = decisions };
    }

    private static AnalysisResult RankCaseTypes(List<Dictionary<string, string>> records, DateRange range)
    {
        var filtered = records.Where(r =>
        {
            DateTime? d = LookDate(r);
            return range == null || (d.HasValue && range.Contains(d.Value));
        }).ToList();
        var ranked = filtered
            .Select(r => CaseTypeOf(r))
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .GroupBy(s => s, StringComparer.CurrentCultureIgnoreCase)
            .Select(g => new { Type = g.Key, Count = g.Count() })
            .OrderByDescending(x => x.Count)
            .Take(15)
            .ToList();

        var sb = new StringBuilder();
        sb.AppendLine("Dava türlerine göre dosya sayısı");
        sb.AppendLine("Dönem: " + (range?.Description ?? "Tüm kayıtlar"));
        sb.AppendLine(new string('-', 55));
        int i = 1;
        foreach (var x in ranked) sb.AppendLine((i++).ToString().PadLeft(2) + ". " + x.Type + " — " + x.Count.ToString("N0", Tr));
        return new AnalysisResult { Summary = sb.ToString(), MatchingRecords = filtered };
    }

    private static AnalysisResult CaseTypeAnalysis(string caseType, List<Dictionary<string, string>> records, DateRange range)
    {
        var matching = records.Where(r => NormalizeText(CaseTypeOf(r)) == NormalizeText(caseType)).Where(r =>
        {
            DateTime? d = LookDate(r);
            return range == null || (d.HasValue && range.Contains(d.Value));
        }).ToList();
        int decided = matching.Count(HasDecision);
        var sb = new StringBuilder();
        sb.AppendLine(caseType);
        sb.AppendLine("Dönem: " + (range?.Description ?? "Tüm kayıtlar"));
        sb.AppendLine(new string('-', 55));
        sb.AppendLine("Toplam dosya: " + matching.Count.ToString("N0", Tr));
        sb.AppendLine("Karar bilgisi olan: " + decided.ToString("N0", Tr));
        sb.AppendLine("Karar bilgisi olmayan: " + (matching.Count - decided).ToString("N0", Tr));
        return new AnalysisResult { Summary = sb.ToString(), MatchingRecords = matching };
    }

    private static AnalysisResult GeneralAnalysis(List<Dictionary<string, string>> records, DateRange range)
    {
        var filtered = records.Where(r =>
        {
            DateTime? d = LookDate(r);
            return range == null || (d.HasValue && range.Contains(d.Value));
        }).ToList();
        int decided = filtered.Count(HasDecision);
        var sb = new StringBuilder();
        sb.AppendLine("Genel dosya özeti");
        sb.AppendLine("Dönem: " + (range?.Description ?? "Tüm kayıtlar"));
        sb.AppendLine(new string('-', 55));
        sb.AppendLine("Toplam dosya: " + filtered.Count.ToString("N0", Tr));
        sb.AppendLine("Karar bilgisi olan: " + decided.ToString("N0", Tr));
        sb.AppendLine("Karar bilgisi olmayan: " + (filtered.Count - decided).ToString("N0", Tr));
        sb.AppendLine();
        sb.AppendLine("Hakim adı veya dava türü içeren daha ayrıntılı bir soru yazabilirsiniz.");
        return new AnalysisResult { Summary = sb.ToString(), MatchingRecords = filtered };
    }

    private static string FindJudge(string question, List<Dictionary<string, string>> records)
    {
        string q = NormalizeText(question);
        var judges = records.SelectMany(r => new[] { FdbEngine.Value(r, "TEKHAKIM"), FdbEngine.Value(r, "BASKAN") })
            .Where(s => !string.IsNullOrWhiteSpace(s)).Distinct(StringComparer.CurrentCultureIgnoreCase).ToList();

        foreach (string judge in judges.OrderByDescending(s => s.Length))
            if (q.Contains(NormalizeText(judge))) return judge;

        var qTokens = q.Split(' ', StringSplitOptions.RemoveEmptyEntries).Where(t => t.Length >= 3).ToHashSet();
        string best = "";
        int bestScore = 0;
        foreach (string judge in judges)
        {
            string[] tokens = NormalizeText(judge).Split(' ', StringSplitOptions.RemoveEmptyEntries);
            int score = tokens.Count(qTokens.Contains);
            if (score > bestScore && score >= Math.Min(2, tokens.Length)) { bestScore = score; best = judge; }
        }
        return best;
    }

    private static string FindCaseType(string question, List<Dictionary<string, string>> records)
    {
        string q = NormalizeText(question);
        var types = records.Select(CaseTypeOf).Where(s => !string.IsNullOrWhiteSpace(s)).Distinct(StringComparer.CurrentCultureIgnoreCase).OrderByDescending(s => s.Length);
        foreach (string type in types)
            if (q.Contains(NormalizeText(type))) return type;
        return "";
    }

    private static DateRange ResolveDateRange(string question, string judge, List<Dictionary<string, string>> records)
    {
        string q = NormalizeText(question);
        DateTime today = DateTime.Today;

        if (!string.IsNullOrWhiteSpace(judge) && (q.Contains("geldigi tarihten") || q.Contains("geldiginden beri") || q.Contains("gelisinden beri")))
        {
            var dates = records.Where(r => JudgeMatches(r, judge)).Select(LookDate).Where(d => d.HasValue).Select(d => d.Value).ToList();
            if (dates.Count > 0)
            {
                DateTime start = dates.Min().Date;
                return new DateRange { Start = start, End = today, Description = "Veride görünen ilk dosya tarihi " + start.ToString("dd.MM.yyyy") + " - " + today.ToString("dd.MM.yyyy") };
            }
        }

        if (q.Contains("bu ay"))
        {
            DateTime start = new(today.Year, today.Month, 1);
            return new DateRange { Start = start, End = today, Description = start.ToString("dd.MM.yyyy") + " - " + today.ToString("dd.MM.yyyy") };
        }
        if (q.Contains("gecen ay"))
        {
            DateTime firstThis = new(today.Year, today.Month, 1);
            DateTime start = firstThis.AddMonths(-1);
            DateTime end = firstThis.AddDays(-1);
            return new DateRange { Start = start, End = end, Description = start.ToString("dd.MM.yyyy") + " - " + end.ToString("dd.MM.yyyy") };
        }

        var explicitDates = ExtractDates(question);
        if (explicitDates.Count >= 2)
        {
            DateTime start = explicitDates.Min().Date;
            DateTime end = explicitDates.Max().Date;
            return new DateRange { Start = start, End = end, Description = start.ToString("dd.MM.yyyy") + " - " + end.ToString("dd.MM.yyyy") };
        }
        if (explicitDates.Count == 1)
        {
            DateTime d = explicitDates[0].Date;
            if (q.Contains("bugune") || q.Contains("itibaren") || q.Contains("sonra") || q.Contains("beri"))
                return new DateRange { Start = d, End = today, Description = d.ToString("dd.MM.yyyy") + " - " + today.ToString("dd.MM.yyyy") };
            return new DateRange { Start = d, End = d, Description = d.ToString("dd.MM.yyyy") };
        }

        Match monthYear = Regex.Match(q, @"\b(ocak|subat|mart|nisan|mayis|haziran|temmuz|agustos|eylul|ekim|kasim|aralik)\s+(20\d{2})\b");
        if (!monthYear.Success) monthYear = Regex.Match(q, @"\b(20\d{2})\s+(ocak|subat|mart|nisan|mayis|haziran|temmuz|agustos|eylul|ekim|kasim|aralik)\b");
        if (monthYear.Success)
        {
            string monthText = monthYear.Groups[1].Value.StartsWith("20") ? monthYear.Groups[2].Value : monthYear.Groups[1].Value;
            int year = int.Parse(monthYear.Groups[1].Value.StartsWith("20") ? monthYear.Groups[1].Value : monthYear.Groups[2].Value);
            int month = Array.IndexOf(MonthNames, monthText) + 1;
            DateTime start = new(year, month, 1);
            DateTime end = start.AddMonths(1).AddDays(-1);
            return new DateRange { Start = start, End = end, Description = start.ToString("MMMM yyyy", Tr) };
        }

        Match yearMatch = Regex.Match(q, @"\b(20\d{2})\b");
        if (yearMatch.Success && (q.Contains("yil") || q.Contains("senesinde") || q.Contains("boyunca")))
        {
            int year = int.Parse(yearMatch.Groups[1].Value);
            return new DateRange { Start = new DateTime(year, 1, 1), End = new DateTime(year, 12, 31), Description = year + " yılı" };
        }
        return null;
    }

    private static List<DateTime> ExtractDates(string question)
    {
        var result = new List<DateTime>();
        foreach (Match m in Regex.Matches(question, @"\b(\d{1,2})[./-](\d{1,2})[./-](20\d{2})\b"))
            if (DateTime.TryParseExact(m.Value.Replace('-', '.'), new[] { "d.M.yyyy", "dd.MM.yyyy" }, Tr, DateTimeStyles.None, out DateTime d)) result.Add(d);
        foreach (Match m in Regex.Matches(question, @"\b(20\d{2})-(\d{1,2})-(\d{1,2})\b"))
            if (DateTime.TryParseExact(m.Value, new[] { "yyyy-M-d", "yyyy-MM-dd" }, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime d)) result.Add(d);

        string normalized = NormalizeText(question);
        foreach (Match m in Regex.Matches(normalized, @"\b(\d{1,2})\s+(ocak|subat|mart|nisan|mayis|haziran|temmuz|agustos|eylul|ekim|kasim|aralik)\s+(20\d{2})\b"))
        {
            int day = int.Parse(m.Groups[1].Value);
            int month = Array.IndexOf(MonthNames, m.Groups[2].Value) + 1;
            int year = int.Parse(m.Groups[3].Value);
            try { result.Add(new DateTime(year, month, day)); } catch { }
        }
        return result.Distinct().ToList();
    }

    private static bool JudgeMatches(Dictionary<string, string> r, string judge)
    {
        string n = NormalizeText(judge);
        return NormalizeText(FdbEngine.Value(r, "TEKHAKIM")) == n || NormalizeText(FdbEngine.Value(r, "BASKAN")) == n;
    }

    private static string PrimaryJudge(Dictionary<string, string> r)
    {
        string t = FdbEngine.Value(r, "TEKHAKIM");
        return string.IsNullOrWhiteSpace(t) ? FdbEngine.Value(r, "BASKAN") : t;
    }

    private static string CaseTypeOf(Dictionary<string, string> r)
    {
        string t = FdbEngine.Value(r, "UYAPDAVATURU");
        return string.IsNullOrWhiteSpace(t) ? FdbEngine.Value(r, "DAVATURU") : t;
    }

    private static bool HasDecision(Dictionary<string, string> r)
        => !string.IsNullOrWhiteSpace(FdbEngine.Value(r, "KARARNO")) || !string.IsNullOrWhiteSpace(FdbEngine.Value(r, "KARARTARIHI")) || !string.IsNullOrWhiteSpace(FdbEngine.Value(r, "KARAR"));

    private static DateTime? LookDate(Dictionary<string, string> r)
        => ParseDate(FdbEngine.Value(r, "ELEALINMATARIHI")) ?? ParseDate(FdbEngine.Value(r, "DAVATARIHI"));

    private static DateTime? DecisionDate(Dictionary<string, string> r)
        => ParseDate(FdbEngine.Value(r, "KARARTARIHI"));

    private static DateTime? ParseDate(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        if (DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces, out DateTime d)) return d;
        if (DateTime.TryParse(value, Tr, DateTimeStyles.AllowWhiteSpaces, out d)) return d;
        return null;
    }

    private static string NormalizeText(string value)
    {
        string s = (value ?? "").ToLower(Tr);
        s = s.Replace('ı', 'i').Replace('ğ', 'g').Replace('ü', 'u').Replace('ş', 's').Replace('ö', 'o').Replace('ç', 'c');
        s = Regex.Replace(s, @"[^a-z0-9./-]+", " ");
        return Regex.Replace(s, @"\s+", " ").Trim();
    }
}
