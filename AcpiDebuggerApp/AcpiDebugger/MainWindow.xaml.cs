using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Folding;
using ICSharpCode.AvalonEdit.Highlighting.Xshd;
using ICSharpCode.AvalonEdit.Highlighting;
using AcpiDebugger.Models;
using AcpiDebugger.Services;
using System.ComponentModel;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using System.Xml;

namespace AcpiDebugger;

public partial class MainWindow : Window
{
    private readonly string _workspaceDir;
    private string _toolsDir = string.Empty;
    private AcpiToolService? _toolService;
    private AcpiOverrideService? _overrideService;
    private CancellationTokenSource? _batchCancellation;
    private bool _batchRunning;
    private string? _currentFile;
    private bool _isDirty;
    private bool _loadingDocument;
    private bool _changingSelection;
    private FoldingManager? _foldingManager;
    private readonly BraceFoldingStrategy _foldingStrategy = new();
    private readonly List<CompilerDiagnostic> _diagnostics = new();
    private readonly List<AcpiFileItem> _amlItems = new();
    private readonly List<string> _aslFiles = new();
    private DiagnosticLineRenderer? _diagnosticRenderer;
    private readonly DispatcherTimer _foldingTimer = new()
    {
        Interval = TimeSpan.FromMilliseconds(350)
    };

    public MainWindow()
    {
        InitializeComponent();

        _workspaceDir = Path.Combine(AppContext.BaseDirectory, "AcpiWorkspace");
        Directory.CreateDirectory(_workspaceDir);

        _foldingTimer.Tick += (_, _) =>
        {
            _foldingTimer.Stop();
            UpdateFoldings();
        };

        ConfigureEditor();
        LocateTools();
        RefreshFileLists();
        UpdateToolStatus();

        MachineText.Text = Environment.MachineName;
        bool testMode = _overrideService?.IsTestSigningEnabled() == true;
        TestModeText.Text = testMode ? "Test Mode" : "Test Mode Off";
        TestModeDot.Fill = testMode ? Brushes.ForestGreen : Brushes.IndianRed;
        BottomTestModeText.Text = testMode ? "Test Mode" : "Test Mode Off";
        BottomTestModeDot.Fill = testMode ? Brushes.ForestGreen : Brushes.IndianRed;

        Editor.TextArea.Caret.PositionChanged += (_, _) => UpdateCaretStatus();
        Log($"Ready. Tools: {(string.IsNullOrEmpty(_toolsDir) ? "not found" : _toolsDir)}");
        Log("AvalonEdit initialized with native WPF integration.");
    }

    private void ConfigureEditor()
    {
        Editor.Options.ConvertTabsToSpaces = true;
        Editor.Options.IndentationSize = 4;
        Editor.Options.HighlightCurrentLine = true;
        Editor.TextArea.TextView.CurrentLineBackground =
            new SolidColorBrush(Color.FromRgb(235, 246, 250));
        Editor.TextArea.TextView.CurrentLineBorder = null;
        Editor.TextArea.SelectionCornerRadius = 0;

        try
        {
            var uri = new Uri("pack://application:,,,/Resources/ASL.xshd");
            using Stream stream = Application.GetResourceStream(uri).Stream;
            using XmlReader reader = XmlReader.Create(stream);
            Editor.SyntaxHighlighting = HighlightingLoader.Load(reader, HighlightingManager.Instance);
        }
        catch (Exception ex)
        {
            Log($"ASL highlighting could not be loaded: {ex.Message}");
        }

        _foldingManager = FoldingManager.Install(Editor.TextArea);
        _diagnosticRenderer = new DiagnosticLineRenderer(Editor.TextArea.TextView);
        Editor.TextArea.TextView.BackgroundRenderers.Add(_diagnosticRenderer);
    }

    private void LocateTools()
    {
        _toolsDir = ToolLocatorService.LocateToolsDirectory();
        _toolService = new AcpiToolService(_toolsDir);
        _overrideService = new AcpiOverrideService(_toolsDir);

        if (!_toolService.IsAvailable)
        {
            MessageBox.Show(
                "iasl.exe and acpidump.exe were not found in the tools directory.",
                "Tools missing", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void UpdateToolStatus()
    {
        ToolStatus status = ToolLocatorService.GetStatus(_toolsDir);
        ToolStatusText.Text = $"iasl: {(status.IaslAvailable ? "OK" : "Missing")}  "
                            + $"acpidump: {(status.AcpiDumpAvailable ? "OK" : "Missing")}  "
                            + $"asl: {(status.MicrosoftAslAvailable ? "OK" : "Missing")}";
        ToolStatusText.ToolTip = string.IsNullOrEmpty(_toolsDir) ? "Tools directory not found" : _toolsDir;
    }

    private void RefreshFileLists()
    {
        AmlList.ItemsSource = null;
        AslList.ItemsSource = null;

        _amlItems.Clear();
        _amlItems.AddRange(Directory.EnumerateFiles(_workspaceDir)
            .Where(file => file.EndsWith(".dat", StringComparison.OrdinalIgnoreCase)
                        || file.EndsWith(".aml", StringComparison.OrdinalIgnoreCase))
            .OrderBy(Path.GetFileName)
            .Select(file => new AcpiFileItem(Path.GetFileName(file), GetAcpiDescription(file))));

        _aslFiles.Clear();
        _aslFiles.AddRange(Directory.EnumerateFiles(_workspaceDir, "*.dsl")
            .Select(Path.GetFileName)
            .OfType<string>()
            .OrderBy(name => name));

        AmlList.ItemsSource = _amlItems;
        AslList.ItemsSource = _aslFiles;

        TableCountText.Text = $"{_amlItems.Count} tables";
        BuildTableTree(TableSearchBox?.Text);
    }

    private static string GetAcpiDescription(string filePath) =>
        AcpiTableService.GetSummary(filePath);

    private async void DumpAml_Click(object sender, RoutedEventArgs e)
    {
        if (!EnsureTools() || !ConfirmDiscardChanges()) return;

        try
        {
            SetBusy(true, "Dumping ACPI tables...");
            await Task.Run(() =>
            {
                foreach (string file in Directory.EnumerateFiles(_workspaceDir))
                    File.Delete(file);
            });

            ClearEditor();
            ProcessResult result = await _toolService!.DumpAsync(_workspaceDir);
            Log(result.Output);

            RefreshFileLists();
            int count = Directory.GetFiles(_workspaceDir, "*.dat").Length;
            Log(count > 0
                ? $"Dump completed: {count} tables."
                : "No tables were dumped. Run the application as administrator.");
        }
        catch (Exception ex)
        {
            ShowError("Dump failed", ex);
        }
        finally
        {
            SetBusy(false);
        }
    }

    private async void DecompileSelected_Click(object sender, RoutedEventArgs e)
    {
        if (!EnsureTools() || AmlList.SelectedItem is not AcpiFileItem item)
        {
            MessageBox.Show("Select an AML table first.", "No selection");
            return;
        }

        await DecompileFileAsync(item.Filename);
    }

    private async Task<bool> DecompileFileAsync(string filename, bool refresh = true)
    {
        try
        {
            SetBusy(true, $"Decompiling {filename}...");
            string target = Path.Combine(_workspaceDir, filename);
            bool includeDsdt = !filename.Equals("dsdt.dat", StringComparison.OrdinalIgnoreCase);
            ProcessResult result = await _toolService!.DecompileAsync(
                _workspaceDir,
                filename,
                includeDsdt,
                _batchCancellation?.Token ?? CancellationToken.None);
            if (refresh || result.ExitCode != 0)
                Log(result.Output);

            // Some SSDTs duplicate namespace objects already present in DSDT.
            // If external-reference loading conflicts, retry the table alone.
            if (result.ExitCode != 0 && includeDsdt)
            {
                Log($"{filename}: external reference conflict; retrying standalone decompile.");
                result = await _toolService.DecompileAsync(
                    _workspaceDir,
                    filename,
                    includeDsdt: false,
                    _batchCancellation?.Token ?? CancellationToken.None);
                if (refresh || result.ExitCode != 0)
                    Log(result.Output);
            }

            string dsl = Path.ChangeExtension(target, ".dsl");
            bool success = result.ExitCode == 0 && File.Exists(dsl);
            if (success)
            {
                if (refresh)
                {
                    RefreshFileLists();
                    SelectAslFile(Path.GetFileName(dsl));
                }
                Log($"Decompiled: {Path.GetFileName(dsl)}");
            }
            else
            {
                Log($"Decompile failed: {filename} (exit code {result.ExitCode}).");
            }
            return success;
        }
        catch (Exception ex)
        {
            ShowError("Decompile failed", ex);
            return false;
        }
        finally
        {
            SetBusy(false);
        }
    }

    private async void DecompileAll_Click(object sender, RoutedEventArgs e)
    {
        if (!EnsureTools()) return;
        var files = Directory.EnumerateFiles(_workspaceDir)
            .Where(file => file.EndsWith(".dat", StringComparison.OrdinalIgnoreCase)
                        || file.EndsWith(".aml", StringComparison.OrdinalIgnoreCase))
            .Select(Path.GetFileName)
            .OfType<string>()
            .OrderBy(name => name.Equals("dsdt.dat", StringComparison.OrdinalIgnoreCase) ? 0 : 1)
            .ToList();

        if (files.Count == 0)
        {
            MessageBox.Show("No AML tables are available.", "Nothing to decompile");
            return;
        }

        BeginBatch(files.Count, "Decompiling tables");
        int success = 0;
        try
        {
            for (int index = 0; index < files.Count; index++)
            {
                _batchCancellation!.Token.ThrowIfCancellationRequested();
                UpdateBatchProgress(index, files.Count, files[index]);
                if (await DecompileFileAsync(files[index], false)) success++;
            }
            Log($"Decompile all completed: {success}/{files.Count}.");
        }
        catch (OperationCanceledException)
        {
            Log($"Decompile all cancelled: {success}/{files.Count} completed.");
        }
        finally
        {
            RefreshFileLists();
            EndBatch();
        }
    }

    private async void CompileSelected_Click(object sender, RoutedEventArgs e)
    {
        if (!EnsureTools() || string.IsNullOrEmpty(_currentFile))
        {
            MessageBox.Show("Select an ASL source first.", "No selection");
            return;
        }

        await SaveCurrentFileAsync();
        await CompileFileAsync(_currentFile);
    }

    private async Task<bool> CompileFileAsync(string filePath)
    {
        try
        {
            SetBusy(true, $"Compiling {Path.GetFileName(filePath)}...");
            _diagnostics.Clear();

            ProcessResult result = await _toolService!.CompileAsync(
                _workspaceDir,
                Path.GetFileName(filePath),
                _batchCancellation?.Token ?? CancellationToken.None);

            Log(result.Output);

            IReadOnlyList<ExternalRepair> repairs =
                IaslDiagnosticService.CreateExternalRepairs(filePath, result.Output);
            if (result.ExitCode != 0 && repairs.Count > 0 && !_batchRunning)
            {
                string preview = string.Join(Environment.NewLine,
                    repairs.Take(8).Select(item =>
                        $"Line {item.Line}:\n- {item.Original.Trim()}\n+ {item.Updated.Trim()}"));
                MessageBoxResult decision = MessageBox.Show(
                    $"iASL reported Error 6163. Apply {repairs.Count} suggested External path repair(s)?\n\n{preview}",
                    "Review Error 6163 repairs",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);
                if (decision == MessageBoxResult.Yes)
                {
                    IaslDiagnosticService.ApplyExternalRepairs(filePath, repairs);
                    result = await _toolService.CompileAsync(
                        _workspaceDir,
                        Path.GetFileName(filePath),
                        _batchCancellation?.Token ?? CancellationToken.None);
                    Log(result.Output);
                    if (string.Equals(filePath, _currentFile, StringComparison.OrdinalIgnoreCase))
                        LoadDocument(filePath);
                }
            }

            ParseDiagnostics(result.Output);

            bool success = result.ExitCode == 0;
            if (success)
            {
                Log($"Compiled: {Path.GetFileName(filePath)}");
                RefreshFileLists();
            }
            else
            {
                Log($"Compile failed with {_diagnostics.Count} diagnostic(s).");
                JumpToFirstError();
            }
            return success;
        }
        catch (Exception ex)
        {
            ShowError("Compile failed", ex);
            return false;
        }
        finally
        {
            SetBusy(false);
        }
    }

    private async void CompileAll_Click(object sender, RoutedEventArgs e)
    {
        if (!EnsureTools()) return;
        if (_isDirty) await SaveCurrentFileAsync();

        string[] files = Directory.GetFiles(_workspaceDir, "*.dsl");
        if (files.Length == 0)
        {
            MessageBox.Show("No ASL source files are available.", "Nothing to compile");
            return;
        }

        BeginBatch(files.Length, "Compiling sources");
        int success = 0;
        try
        {
            for (int index = 0; index < files.Length; index++)
            {
                _batchCancellation!.Token.ThrowIfCancellationRequested();
                UpdateBatchProgress(index, files.Length, Path.GetFileName(files[index]));
                if (await CompileFileAsync(files[index])) success++;
            }
            Log($"Compile all completed: {success}/{files.Length}.");
        }
        catch (OperationCanceledException)
        {
            Log($"Compile all cancelled: {success}/{files.Length} completed.");
        }
        finally
        {
            EndBatch();
        }
    }

    private async void LoadToSystem_Click(object sender, RoutedEventArgs e)
    {
        if (AmlList.SelectedItem is not AcpiFileItem item)
        {
            MessageBox.Show("Select an AML table first.", "No selection");
            return;
        }

        try
        {
            SetBusy(true, $"Loading {item.Filename}...");
            string result = await _overrideService!.StageAsync(
                Path.Combine(_workspaceDir, item.Filename));
            Log(result);

            if (result.StartsWith("Success:", StringComparison.Ordinal))
            {
                MessageBox.Show(
                    "The ACPI override has been staged in the registry.\n\n"
                  + "Restart Windows before checking Device Manager or running Dump again. "
                  + "The live ACPI namespace is not replaced immediately.",
                    "Restart required",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
        }
        catch (Exception ex)
        {
            ShowError("Load failed", ex);
        }
        finally
        {
            SetBusy(false);
        }
    }

    private void AmlList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (AmlList.SelectedItem is AcpiFileItem item)
            UpdateTableDetails(Path.Combine(_workspaceDir, item.Filename));
    }

    private void TableSearchBox_TextChanged(object sender, TextChangedEventArgs e) =>
        BuildTableTree(TableSearchBox.Text);

    private void BuildTableTree(string? searchText)
    {
        if (TableTree == null) return;
        string query = searchText?.Trim() ?? string.Empty;
        TableTree.Items.Clear();

        var nodes = _amlItems
            .Select(item => new TableNode(item.Filename, false, item))
            .Concat(_aslFiles.Select(file => new TableNode(file, true, null)))
            .Where(node => string.IsNullOrEmpty(query) ||
                node.Filename.Contains(query, StringComparison.OrdinalIgnoreCase))
            .ToList();

        AddTableGroup("DSDT", nodes.Where(node =>
            node.Filename.StartsWith("dsdt", StringComparison.OrdinalIgnoreCase)));
        AddTableGroup("SSDT", nodes.Where(node =>
            node.Filename.StartsWith("ssdt", StringComparison.OrdinalIgnoreCase)));
        var otherGroups = nodes
            .Where(node => !node.Filename.StartsWith("dsdt", StringComparison.OrdinalIgnoreCase) &&
                           !node.Filename.StartsWith("ssdt", StringComparison.OrdinalIgnoreCase))
            .GroupBy(node => GetTableGroupName(node.Filename))
            .OrderBy(group => group.Key, StringComparer.OrdinalIgnoreCase);

        foreach (var group in otherGroups)
            AddTableGroup(group.Key, group);
    }

    private static string GetTableGroupName(string filename)
    {
        string basename = Path.GetFileNameWithoutExtension(filename);
        Match match = Regex.Match(basename, "^[A-Za-z]+");
        return match.Success ? match.Value.ToUpperInvariant() : "OTHER";
    }

    private void AddTableGroup(string name, IEnumerable<TableNode> source)
    {
        List<TableNode> nodes = source
            .OrderBy(node => node.Filename, StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (nodes.Count == 0) return;

        var group = new TreeViewItem
        {
            Header = $"{name}  ({nodes.Count})",
            IsExpanded = name is "DSDT" or "SSDT",
            FontWeight = FontWeights.SemiBold
        };

        foreach (TableNode node in nodes)
        {
            group.Items.Add(new TreeViewItem
            {
                Header = node.Filename,
                Tag = node,
                FontWeight = FontWeights.Normal,
                ToolTip = node.IsSource ? "ASL source" : node.AmlItem?.Description
            });
        }

        TableTree.Items.Add(group);
    }

    private void TableTreeItem_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not TreeViewItem item || item.Tag is not TableNode) return;

        if (!item.IsSelected)
            item.IsSelected = true;
        else
            ActivateTableNode((TableNode)item.Tag);
        e.Handled = true;
    }

    private void TableTree_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        if (e.NewValue is TreeViewItem item && item.Tag is TableNode node)
            ActivateTableNode(node);
    }

    private void ActivateTableNode(TableNode node)
    {
        if (node.IsSource)
        {
            if (!ConfirmDiscardChanges()) return;
            LoadDocument(Path.Combine(_workspaceDir, node.Filename));
        }
        else if (node.AmlItem != null)
        {
            AmlList.SelectedItem = node.AmlItem;
        }
    }

    private void Refresh_Click(object sender, RoutedEventArgs e) => RefreshFileLists();

    private void UpdateTableDetails(string filePath)
    {
        try
        {
            AcpiTableInfo info = AcpiTableService.Read(filePath);
            DetailSignature.Text = info.Signature;
            DetailDescription.Text = info.Description;
            DetailLength.Text = $"{info.Length:N0} bytes";
            DetailRevision.Text = info.Revision.ToString();
            DetailChecksum.Text = $"0x{info.Checksum:X2}";
            DetailOemId.Text = info.OemId;
            DetailTableId.Text = info.TableId;
            DetailOemRevision.Text = $"0x{info.OemRevision:X8}";
            DetailCreatorId.Text = info.CreatorId;
            DetailCreatorRevision.Text = $"0x{info.CreatorRevision:X8}";
            DetailSource.Text = info.Source;
            DetailValidated.Text = info.ChecksumValid
                ? "Yes — checksum valid"
                : "No — checksum mismatch";
        }
        catch (Exception ex)
        {
            DetailDescription.Text = ex.Message;
            DetailValidated.Text = "Unable to validate";
        }
    }

    private void AslList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_changingSelection || AslList.SelectedItem is not string filename) return;

        if (!ConfirmDiscardChanges())
        {
            _changingSelection = true;
            AslList.SelectedItem = _currentFile == null ? null : Path.GetFileName(_currentFile);
            _changingSelection = false;
            return;
        }

        LoadDocument(Path.Combine(_workspaceDir, filename));
    }

    private void SelectAslFile(string filename)
    {
        _changingSelection = true;
        AslList.SelectedItem = filename;
        _changingSelection = false;
        LoadDocument(Path.Combine(_workspaceDir, filename));
    }

    private void LoadDocument(string filePath)
    {
        _loadingDocument = true;
        try
        {
            Editor.Load(filePath);
            _currentFile = filePath;
            _isDirty = false;
            EditorTabText.Text = Path.GetFileName(filePath);
            DirtyDot.Visibility = Visibility.Collapsed;
            Title = $"ACPI Debugger — {Path.GetFileName(filePath)}";
            ScheduleFoldingUpdate();
            Editor.Focus();
        }
        finally
        {
            _loadingDocument = false;
        }
    }

    private void ClearEditor()
    {
        _loadingDocument = true;
        Editor.Clear();
        _loadingDocument = false;
        _currentFile = null;
        _isDirty = false;
        EditorTabText.Text = "No file open";
        DirtyDot.Visibility = Visibility.Collapsed;
        Title = "ACPI Debugger";
    }

    private async void Save_Click(object sender, RoutedEventArgs e) => await SaveCurrentFileAsync();

    private async Task<bool> SaveCurrentFileAsync()
    {
        if (string.IsNullOrEmpty(_currentFile)) return false;
        await File.WriteAllTextAsync(_currentFile, Editor.Text, new UTF8Encoding(false));
        _isDirty = false;
        DirtyDot.Visibility = Visibility.Collapsed;
        Title = $"ACPI Debugger — {Path.GetFileName(_currentFile)}";
        StatusText.Text = $"Saved {Path.GetFileName(_currentFile)}";
        return true;
    }

    private void Editor_TextChanged(object? sender, EventArgs e)
    {
        if (!_loadingDocument && _currentFile != null)
        {
            _isDirty = true;
            DirtyDot.Visibility = Visibility.Visible;
            Title = $"ACPI Debugger — {Path.GetFileName(_currentFile)} *";
        }
        ScheduleFoldingUpdate();
        UpdateCaretStatus();
    }

    private void ScheduleFoldingUpdate()
    {
        _foldingTimer.Stop();
        _foldingTimer.Start();
    }

    private void UpdateFoldings()
    {
        if (_foldingManager != null && Editor.Document != null)
            _foldingStrategy.UpdateFoldings(_foldingManager, Editor.Document);
    }

    private void ShowFind_Click(object sender, RoutedEventArgs e)
    {
        FindBar.Visibility = Visibility.Visible;
        FindTextBox.Focus();
        FindTextBox.SelectAll();
    }

    private void CloseFind_Click(object sender, RoutedEventArgs e)
    {
        FindBar.Visibility = Visibility.Collapsed;
        Editor.Focus();
    }

    private void FindNext_Click(object sender, RoutedEventArgs e) => FindText(true);
    private void FindPrevious_Click(object sender, RoutedEventArgs e) => FindText(false);

    private void FindAllFiles_Click(object sender, RoutedEventArgs e)
    {
        string query = FindTextBox.Text;
        if (string.IsNullOrWhiteSpace(query))
        {
            FindResultText.Text = "Enter text to search";
            return;
        }

        var results = new List<SearchResult>();
        foreach (string file in Directory.EnumerateFiles(_workspaceDir, "*.dsl"))
        {
            int lineNumber = 0;
            foreach (string line in File.ReadLines(file))
            {
                lineNumber++;
                if (line.Contains(query, StringComparison.OrdinalIgnoreCase))
                {
                    results.Add(new SearchResult(
                        Path.GetFileName(file),
                        lineNumber,
                        line.Trim()));
                }
            }
        }

        SearchResultsGrid.ItemsSource = results;
        SearchResultsTab.IsSelected = true;
        FindResultText.Text = $"{results.Count} matches in {_aslFiles.Count} files";
        StatusText.Text = $"Find All: {results.Count} matches for '{query}'";
    }

    private void SearchResultsGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (SearchResultsGrid.SelectedItem is not SearchResult result) return;

        string path = Path.Combine(_workspaceDir, result.File);
        if (!File.Exists(path) || !ConfirmDiscardChanges()) return;

        LoadDocument(path);
        GoToLine(result.Line);

        DocumentLine line = Editor.Document.GetLineByNumber(result.Line);
        string lineText = Editor.Document.GetText(line);
        int index = lineText.IndexOf(FindTextBox.Text, StringComparison.OrdinalIgnoreCase);
        if (index >= 0)
            Editor.Select(line.Offset + index, FindTextBox.Text.Length);
    }

    private void Replace_Click(object sender, RoutedEventArgs e)
    {
        if (Editor.SelectionLength > 0 &&
            Editor.SelectedText.Equals(FindTextBox.Text, StringComparison.OrdinalIgnoreCase))
        {
            int start = Editor.SelectionStart;
            Editor.Document.Replace(start, Editor.SelectionLength, ReplaceTextBox.Text);
            Editor.Select(start, ReplaceTextBox.Text.Length);
        }
        FindText(true);
    }

    private void ReplaceAll_Click(object sender, RoutedEventArgs e)
    {
        string find = FindTextBox.Text;
        if (string.IsNullOrEmpty(find)) return;

        int count = 0;
        int offset = 0;
        Editor.Document.BeginUpdate();
        try
        {
            while (offset <= Editor.Document.TextLength)
            {
                int index = Editor.Text.IndexOf(find, offset, StringComparison.OrdinalIgnoreCase);
                if (index < 0) break;
                Editor.Document.Replace(index, find.Length, ReplaceTextBox.Text);
                offset = index + ReplaceTextBox.Text.Length;
                count++;
            }
        }
        finally
        {
            Editor.Document.EndUpdate();
        }
        FindResultText.Text = $"Replaced {count}";
    }

    private void FindText(bool forward)
    {
        string query = FindTextBox.Text;
        if (string.IsNullOrEmpty(query)) return;

        string text = Editor.Text;
        int start = forward ? Editor.SelectionStart + Editor.SelectionLength : Editor.SelectionStart - 1;
        int index = forward
            ? text.IndexOf(query, Math.Clamp(start, 0, text.Length), StringComparison.OrdinalIgnoreCase)
            : text.LastIndexOf(query, Math.Clamp(start, 0, Math.Max(0, text.Length - 1)), StringComparison.OrdinalIgnoreCase);

        if (index < 0)
        {
            index = forward
                ? text.IndexOf(query, StringComparison.OrdinalIgnoreCase)
                : text.LastIndexOf(query, StringComparison.OrdinalIgnoreCase);
        }

        if (index >= 0)
        {
            Editor.Select(index, query.Length);
            Editor.ScrollToLine(Editor.Document.GetLineByOffset(index).LineNumber);
            FindResultText.Text = $"Line {Editor.Document.GetLineByOffset(index).LineNumber}";
        }
        else
        {
            FindResultText.Text = "No matches";
        }
    }

    private void FindTextBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            FindText(!Keyboard.Modifiers.HasFlag(ModifierKeys.Shift));
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            CloseFind_Click(sender, e);
            e.Handled = true;
        }
    }

    private void GoToLine_Click(object sender, RoutedEventArgs e)
    {
        string? value = PromptForText("Go to line", "Line number:", Editor.TextArea.Caret.Line.ToString());
        if (int.TryParse(value, out int line))
            GoToLine(line);
    }

    private void GoToLine(int line)
    {
        if (Editor.Document.LineCount == 0) return;
        line = Math.Clamp(line, 1, Editor.Document.LineCount);
        DocumentLine documentLine = Editor.Document.GetLineByNumber(line);
        Editor.CaretOffset = documentLine.Offset;
        Editor.ScrollToLine(line);
        Editor.Focus();
    }

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
        {
            if (e.Key == Key.S)
            {
                Save_Click(sender, e);
                e.Handled = true;
            }
            else if (e.Key == Key.F)
            {
                ShowFind_Click(sender, e);
                if (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))
                    FindAllFiles_Click(sender, e);
                e.Handled = true;
            }
            else if (e.Key == Key.G)
            {
                GoToLine_Click(sender, e);
                e.Handled = true;
            }
            else if (e.Key == Key.Oem2)
            {
                ToggleLineComments();
                e.Handled = true;
            }
        }
        else if (e.Key == Key.F3)
        {
            FindText(!Keyboard.Modifiers.HasFlag(ModifierKeys.Shift));
            e.Handled = true;
        }
    }

    private void ToggleLineComments()
    {
        int startLine = Editor.Document.GetLineByOffset(Editor.SelectionStart).LineNumber;
        int endOffset = Math.Max(Editor.SelectionStart, Editor.SelectionStart + Editor.SelectionLength - 1);
        int endLine = Editor.Document.GetLineByOffset(endOffset).LineNumber;
        bool uncomment = true;

        for (int lineNumber = startLine; lineNumber <= endLine; lineNumber++)
        {
            DocumentLine line = Editor.Document.GetLineByNumber(lineNumber);
            if (!Editor.Document.GetText(line).TrimStart().StartsWith("//"))
            {
                uncomment = false;
                break;
            }
        }

        Editor.Document.BeginUpdate();
        try
        {
            for (int lineNumber = endLine; lineNumber >= startLine; lineNumber--)
            {
                DocumentLine line = Editor.Document.GetLineByNumber(lineNumber);
                string text = Editor.Document.GetText(line);
                int whitespace = text.Length - text.TrimStart().Length;
                if (uncomment)
                {
                    if (text.AsSpan(whitespace).StartsWith("//"))
                        Editor.Document.Remove(line.Offset + whitespace, 2);
                }
                else
                {
                    Editor.Document.Insert(line.Offset + whitespace, "// ");
                }
            }
        }
        finally
        {
            Editor.Document.EndUpdate();
        }
    }

    private void ParseDiagnostics(string output)
    {
        _diagnostics.Clear();
        _diagnostics.AddRange(IaslDiagnosticService.Parse(output));
        _diagnosticRenderer?.SetDiagnostics(_diagnostics.Select(d =>
            (d.Line, d.Severity.Equals("Error", StringComparison.OrdinalIgnoreCase))));
        DiagnosticsGrid.ItemsSource = null;
        DiagnosticsGrid.ItemsSource = _diagnostics;
    }

    private void JumpToFirstError()
    {
        CompilerDiagnostic? diagnostic = _diagnostics.FirstOrDefault(d =>
            d.Severity.Equals("Error", StringComparison.OrdinalIgnoreCase)) ?? _diagnostics.FirstOrDefault();
        if (diagnostic == null) return;

        string path = Path.Combine(_workspaceDir, Path.GetFileName(diagnostic.File));
        if (File.Exists(path) && !string.Equals(path, _currentFile, StringComparison.OrdinalIgnoreCase))
            LoadDocument(path);

        GoToLine(diagnostic.Line);
        StatusText.Text = $"{diagnostic.Severity} {diagnostic.Code}: {diagnostic.Message}";
    }

    private void OutputBox_MouseDoubleClick(object sender, MouseButtonEventArgs e) => JumpToFirstError();

    private void DiagnosticsGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (DiagnosticsGrid.SelectedItem is CompilerDiagnostic diagnostic)
            JumpToDiagnostic(diagnostic);
    }

    private void JumpToDiagnostic(CompilerDiagnostic diagnostic)
    {
        string path = Path.Combine(_workspaceDir, Path.GetFileName(diagnostic.File));
        if (File.Exists(path) && !string.Equals(path, _currentFile, StringComparison.OrdinalIgnoreCase))
            LoadDocument(path);
        GoToLine(diagnostic.Line);
        StatusText.Text = $"{diagnostic.Severity} {diagnostic.Code}: {diagnostic.Message}";
    }

    private bool ConfirmDiscardChanges()
    {
        if (!_isDirty) return true;

        MessageBoxResult result = MessageBox.Show(
            "The current ASL file has unsaved changes. Save them now?",
            "Unsaved changes", MessageBoxButton.YesNoCancel, MessageBoxImage.Question);

        if (result == MessageBoxResult.Cancel) return false;
        if (result == MessageBoxResult.Yes)
            SaveCurrentFileAsync().GetAwaiter().GetResult();
        return true;
    }

    private void Window_Closing(object? sender, CancelEventArgs e)
    {
        if (!ConfirmDiscardChanges()) e.Cancel = true;
    }

    private bool EnsureTools()
    {
        if (!string.IsNullOrEmpty(_toolsDir)) return true;
        MessageBox.Show("ACPICA tools were not found.", "Tools missing");
        return false;
    }

    private void BeginBatch(int total, string status)
    {
        _batchCancellation?.Dispose();
        _batchCancellation = new CancellationTokenSource();
        _batchRunning = true;
        ToolbarPanel.IsEnabled = false;
        BatchProgress.Visibility = Visibility.Visible;
        BatchProgress.Minimum = 0;
        BatchProgress.Maximum = total;
        BatchProgress.Value = 0;
        CancelBatchButton.IsEnabled = true;
        CancelBatchButton.Visibility = Visibility.Visible;
        StatusText.Text = status;
    }

    private void UpdateBatchProgress(int completed, int total, string currentFile)
    {
        BatchProgress.Value = completed;
        StatusText.Text = $"{completed + 1}/{total}  {currentFile}";
    }

    private void EndBatch()
    {
        if (BatchProgress.Maximum > 0)
            BatchProgress.Value = BatchProgress.Maximum;
        _batchRunning = false;
        _batchCancellation?.Dispose();
        _batchCancellation = null;
        ToolbarPanel.IsEnabled = true;
        BatchProgress.Visibility = Visibility.Collapsed;
        CancelBatchButton.Visibility = Visibility.Collapsed;
        StatusText.Text = "Ready";
    }

    private void CancelBatch_Click(object sender, RoutedEventArgs e)
    {
        CancelBatchButton.IsEnabled = false;
        StatusText.Text = "Cancelling...";
        _batchCancellation?.Cancel();
    }

    private void SetBusy(bool busy, string? status = null)
    {
        ToolbarPanel.IsEnabled = !busy && !_batchRunning;
        Mouse.OverrideCursor = null;
        if (status != null) StatusText.Text = status;
        else if (!busy && !_batchRunning) StatusText.Text = "Ready";
    }

    private void UpdateCaretStatus()
    {
        PositionText.Text = $"Ln {Editor.TextArea.Caret.Line}, Col {Editor.TextArea.Caret.Column}";
    }

    private void Log(string message)
    {
        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.Invoke(() => Log(message));
            return;
        }

        string line = $"[{DateTime.Now:HH:mm:ss}] {message.TrimEnd()}";
        OutputBox.Document.Blocks.Add(new Paragraph(new Run(line))
        {
            Margin = new Thickness(0),
            Foreground = new SolidColorBrush(Color.FromRgb(31, 41, 55))
        });
        OutputBox.ScrollToEnd();
    }

    private void ShowError(string title, Exception exception)
    {
        Log($"{title}: {exception.Message}");
        MessageBox.Show(exception.Message, title, MessageBoxButton.OK, MessageBoxImage.Error);
    }

    private static string? PromptForText(string title, string prompt, string initialValue)
    {
        var input = new TextBox { Text = initialValue, Margin = new Thickness(0, 8, 0, 12) };
        var ok = new Button { Content = "OK", IsDefault = true, Width = 80 };
        var cancel = new Button { Content = "Cancel", IsCancel = true, Width = 80 };
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        buttons.Children.Add(ok);
        buttons.Children.Add(cancel);

        var panel = new StackPanel { Margin = new Thickness(16) };
        panel.Children.Add(new TextBlock { Text = prompt });
        panel.Children.Add(input);
        panel.Children.Add(buttons);

        var dialog = new Window
        {
            Title = title,
            Content = panel,
            SizeToContent = SizeToContent.WidthAndHeight,
            MinWidth = 320,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            ResizeMode = ResizeMode.NoResize
        };
        ok.Click += (_, _) => dialog.DialogResult = true;
        return dialog.ShowDialog() == true ? input.Text : null;
    }

    private void About_Click(object sender, RoutedEventArgs e)
    {
        var about = new AboutWindow
        {
            Owner = this
        };
        about.ShowDialog();
    }

    private sealed record TableNode(string Filename, bool IsSource, AcpiFileItem? AmlItem);

    private sealed record AcpiFileItem(string Filename, string Description)
    {
        public override string ToString() => Filename;
    }
}
