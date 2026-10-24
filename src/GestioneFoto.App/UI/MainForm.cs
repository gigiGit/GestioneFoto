using GestioneFoto.App.Config;
using GestioneFoto.App.Models;
using GestioneFoto.App.Services;
using System.Reflection;
using System.Runtime.InteropServices;

namespace GestioneFoto.App.UI;

public sealed class MainForm : Form
{
    private readonly ComboBox _cmbProfiles;
    private readonly ProgressBar _progress;
    private readonly Label _lblStatus;
    private readonly Button _btnStart;
    private readonly Button _btnPause;
    private readonly Button _btnResume;
    private readonly Button _btnStop;
    private readonly Button _btnCopy;
    private readonly Button _btnRebuildAlbums;
    private readonly Button _btnEditProfile;
    private readonly Button _btnViewDb;
    private readonly Panel _pnlExecutionControls;
    private readonly TextBox _txtProfileInfo;
    private readonly TextBox _txtLog;

    private readonly ScanCoordinator _coordinator;
    private readonly CopyCoordinator _copyCoordinator;
    private readonly UtilityCoordinator _utilityCoordinator;
    private readonly PauseController _pause;
    private readonly string _configDirectory;
    private readonly string _lastProfileStateFile;
    private readonly string? _startupProfileHint;
    private readonly bool _openViewerOnStart;

    private ProfileConfig? _selectedProfile;

    private CancellationTokenSource? _cts;
    private Task? _running;
    private DateTime? _operationStartedAt;

    public MainForm(string? startupProfileHint = null, bool openViewerOnStart = false)
    {
        Text = "GestioneFoto - C#";
        Width = 760;
        Height = 560;
        MinimumSize = new Size(760, 560);
        StartPosition = FormStartPosition.CenterScreen;

        var menu = new MenuStrip();
        var fileMenu = new ToolStripMenuItem("File");
        var fileExitMenu = new ToolStripMenuItem("Exit");
        var utilityMenu = new ToolStripMenuItem("Utility");
        var utilityRebuildHashMenu = new ToolStripMenuItem("Controllo - Ricostruisci Tabella Hash");
        var utilityRecalcExifMenu = new ToolStripMenuItem("Ricalcola dtExif mancanti");
        var helpMenu = new ToolStripMenuItem("Help");
        var helpGuideMenu = new ToolStripMenuItem("Guida Utente");
        var helpReadmeMenu = new ToolStripMenuItem("README");
        var helpFullDocMenu = new ToolStripMenuItem("Documentazione Completa");
        var helpAboutMenu = new ToolStripMenuItem("Informazioni");
        fileExitMenu.Click += (_, _) => Close();
        utilityRebuildHashMenu.Click += (_, _) =>
            MessageBox.Show(this, "Comando utility disponibile in prossima iterazione .NET.", "Utility", MessageBoxButtons.OK, MessageBoxIcon.Information);
        utilityRecalcExifMenu.Click += async (_, _) => await RecalculateExifFromUtilityAsync();
        helpGuideMenu.Click += (_, _) => OpenHelpDocument("GUIDA_UTENTE.md", "Guida utente");
        helpReadmeMenu.Click += (_, _) => OpenHelpDocument("README.md", "README");
        helpFullDocMenu.Click += (_, _) => OpenHelpDocument("DOCUMENTAZIONE_COMPLETA.md", "Documentazione completa");
        helpAboutMenu.Click += (_, _) => ShowAboutDialog();
        fileMenu.DropDownItems.Add(fileExitMenu);
        menu.Items.Add(fileMenu);
        utilityMenu.DropDownItems.Add(utilityRebuildHashMenu);
        utilityMenu.DropDownItems.Add(utilityRecalcExifMenu);
        menu.Items.Add(utilityMenu);
        helpMenu.DropDownItems.Add(helpGuideMenu);
        helpMenu.DropDownItems.Add(helpReadmeMenu);
        helpMenu.DropDownItems.Add(helpFullDocMenu);
        helpMenu.DropDownItems.Add(new ToolStripSeparator());
        helpMenu.DropDownItems.Add(helpAboutMenu);
        menu.Items.Add(helpMenu);
        MainMenuStrip = menu;
        Controls.Add(menu);

        var dbWriter = new HashDbWriter();
        var exifReader = new ExifDateReader();
        _coordinator = new ScanCoordinator(exifReader, dbWriter);
        _copyCoordinator = new CopyCoordinator(dbWriter);
        _utilityCoordinator = new UtilityCoordinator(dbWriter, exifReader);
        _pause = new PauseController();
        _configDirectory = ResolveConfigDirectory();
        _lastProfileStateFile = ResolveLastProfileStateFile();
        _startupProfileHint = NormalizeProfileHint(startupProfileHint);
        _openViewerOnStart = openViewerOnStart;

        var lblProfile = new Label { Left = 16, Top = 44, Width = 120, Text = "Profilo" };
        Controls.Add(lblProfile);

        _cmbProfiles = new ComboBox
        {
            Left = 140,
            Top = 40,
            Width = 408,
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
            DropDownStyle = ComboBoxStyle.DropDownList
        };
        Controls.Add(_cmbProfiles);

        _btnEditProfile = new Button
        {
            Left = 556,
            Top = 39,
            Width = 170,
            Height = 28,
            Text = "Modifica Profilo",
            Anchor = AnchorStyles.Top | AnchorStyles.Right
        };
        Controls.Add(_btnEditProfile);

        _btnStart = new Button { Left = 16, Top = 76, Width = 90, Height = 30, Text = "Scansione" };
        _btnViewDb = new Button { Left = 116, Top = 76, Width = 110, Height = 30, Text = "Visualizza" };
        _btnCopy = new Button { Left = 236, Top = 76, Width = 90, Height = 30, Text = "Copia" };
        _btnRebuildAlbums = new Button { Left = 336, Top = 76, Width = 140, Height = 30, Text = "Rigenera Album" };

        _btnPause = new Button { Left = 0, Top = 0, Width = 90, Height = 30, Text = "Pausa", Enabled = false };
        _btnResume = new Button { Left = 100, Top = 0, Width = 90, Height = 30, Text = "Riprendi", Enabled = false };
        _btnStop = new Button { Left = 200, Top = 0, Width = 90, Height = 30, Text = "Stop", Enabled = false };

        _pnlExecutionControls = new Panel
        {
            Left = 16,
            Top = 114,
            Width = 320,
            Height = 32,
            Visible = false
        };
        _pnlExecutionControls.Controls.Add(_btnPause);
        _pnlExecutionControls.Controls.Add(_btnResume);
        _pnlExecutionControls.Controls.Add(_btnStop);

        Controls.Add(_btnStart);
        Controls.Add(_btnCopy);
        Controls.Add(_btnViewDb);
        Controls.Add(_btnRebuildAlbums);
        Controls.Add(_pnlExecutionControls);

        _progress = new ProgressBar
        {
            Left = 16,
            Top = 152,
            Width = 700,
            Height = 22,
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
        };
        Controls.Add(_progress);

        _lblStatus = new Label
        {
            Left = 16,
            Top = 180,
            Width = 700,
            Text = "Pronto",
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
        };
        Controls.Add(_lblStatus);

        var lblProfileInfo = new Label
        {
            Left = 16,
            Top = 210,
            Width = 700,
            Text = "Parametri profilo in uso",
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
        };
        Controls.Add(lblProfileInfo);

        _txtProfileInfo = new TextBox
        {
            Multiline = true,
            ScrollBars = ScrollBars.Vertical,
            ReadOnly = true,
            Dock = DockStyle.Fill
        };

        _txtLog = new TextBox
        {
            Multiline = true,
            ScrollBars = ScrollBars.Vertical,
            ReadOnly = true,
            Dock = DockStyle.Fill
        };

        var detailsSplit = new SplitContainer
        {
            Left = 16,
            Top = 232,
            Width = 700,
            Height = 292,
            Orientation = Orientation.Horizontal,
            Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
            SplitterDistance = 122
        };
        detailsSplit.Panel1.Controls.Add(_txtProfileInfo);
        detailsSplit.Panel2.Controls.Add(_txtLog);
        Controls.Add(detailsSplit);

        _btnStart.Click += async (_, _) => await StartAsync();
        _btnCopy.Click += async (_, _) => await CopyAsync();
        _btnRebuildAlbums.Click += async (_, _) => await RebuildAlbumLinksAsync();
        _btnEditProfile.Click += (_, _) => EditSelectedProfile();
        _btnViewDb.Click += (_, _) => ShowDatabaseViewer();
        _btnPause.Click += (_, _) => Pause();
        _btnResume.Click += (_, _) => Resume();
        _btnStop.Click += (_, _) => Stop();
        _cmbProfiles.SelectedIndexChanged += (_, _) => LoadSelectedProfile();

        LoadProfileList();

        if (_openViewerOnStart)
        {
            Shown += OnShownOpenViewer;
        }
    }

    private void OnShownOpenViewer(object? sender, EventArgs e)
    {
        Shown -= OnShownOpenViewer;
        BeginInvoke(new Action(ShowDatabaseViewer));
    }

    private async Task StartAsync()
    {
        if (_running is { IsCompleted: false })
        {
            return;
        }

        try
        {
            if (_selectedProfile is null)
            {
                throw new InvalidOperationException("Nessun profilo selezionato.");
            }

            var profile = _selectedProfile;
            _txtProfileInfo.Text = BuildProfileInfo(profile);
            _cts = new CancellationTokenSource();
            _pause.Resume();

            SetRunningUi(true, canPause: true);
            _operationStartedAt = DateTime.UtcNow;
            Log($"Profilo caricato: {profile.ProfileName}");
            Log("Avvio scansione.");

            var progress = new Progress<ProgressInfo>(p =>
            {
                _lblStatus.Text = FormatStatusWithEta(p);
                _progress.Value = Math.Clamp(p.Percent, 0, 100);
            });

            _running = _coordinator.RunAsync(profile, _pause, progress, _cts.Token);
            await _running;

            Log("Scansione completata.");

            Log("Avvio preparazione automatica post-scansione.");
            SetRunningUi(true, canPause: false);
            _operationStartedAt = DateTime.UtcNow;

            var prepareProgress = new Progress<ProgressInfo>(p =>
            {
                _lblStatus.Text = FormatStatusWithEta(p);
                _progress.Value = Math.Clamp(p.Percent, 0, 100);
                if (string.Equals(p.Phase, "HashDuplicate", StringComparison.OrdinalIgnoreCase))
                {
                    Log(p.Message);
                }
            });

            _running = _copyCoordinator.PrepareHashAsync(profile, prepareProgress, _cts.Token);
            var prepareResult = await ((Task<HashPreparationResult>)_running);
            Log($"Preparazione automatica completata. Nuovi record: {prepareResult.Inserted}. Gia' presenti: {prepareResult.SkippedExisting}. Duplicati scartati: {prepareResult.SkippedDuplicates}.");
        }
        catch (OperationCanceledException)
        {
            Log("Operazione annullata.");
            _lblStatus.Text = "Interrotta";
        }
        catch (Exception ex)
        {
            Log($"Errore: {ex.Message}");
            _lblStatus.Text = "Errore";
        }
        finally
        {
            SetRunningUi(false);
            _operationStartedAt = null;
            _cts?.Dispose();
            _cts = null;
        }
    }

    private void Pause()
    {
        _pause.Pause();
        _btnPause.Enabled = false;
        _btnResume.Enabled = true;
        _lblStatus.Text = "In pausa";
        Log("Pausa richiesta.");
    }

    private void Resume()
    {
        _pause.Resume();
        _btnPause.Enabled = true;
        _btnResume.Enabled = false;
        Log("Ripresa richiesta.");
    }

    private void Stop()
    {
        _cts?.Cancel();
        Log("Stop richiesto.");
    }

    private void SetRunningUi(bool running, bool canPause = false)
    {
        if (running)
        {
            _btnStart.Enabled = false;
            _btnCopy.Enabled = false;
            _btnRebuildAlbums.Enabled = false;
            _btnEditProfile.Enabled = false;
            _btnViewDb.Enabled = false;

            _pnlExecutionControls.Visible = canPause;
            _btnStop.Enabled = canPause;
            _btnPause.Enabled = canPause;
            _btnResume.Enabled = false;
            return;
        }

        _pnlExecutionControls.Visible = false;
        _btnStop.Enabled = false;
        _btnPause.Enabled = false;
        _btnResume.Enabled = false;

        RefreshWorkflowButtons();
    }

    private void RefreshWorkflowButtons()
    {
        var hasProfile = _selectedProfile is not null;

        _btnStart.Enabled = hasProfile;
        _btnCopy.Enabled = hasProfile;
        _btnViewDb.Enabled = hasProfile;
        _btnRebuildAlbums.Enabled = hasProfile;
        _btnEditProfile.Enabled = hasProfile;
    }

    private void ShowDatabaseViewer()
    {
        if (_selectedProfile is null)
        {
            MessageBox.Show(this, "Nessun profilo selezionato.", "Visualizza DB", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        using var viewer = new DatabaseViewerForm(_selectedProfile);
        viewer.ShowDialog(this);
    }

    private async Task CopyAsync()
    {
        if (_running is { IsCompleted: false })
        {
            return;
        }

        try
        {
            if (_selectedProfile is null)
            {
                throw new InvalidOperationException("Nessun profilo selezionato.");
            }

            var profile = _selectedProfile;
            _cts = new CancellationTokenSource();
            _pause.Resume();

            SetRunningUi(true, canPause: true);
            _operationStartedAt = DateTime.UtcNow;
            Log($"Avvio copia per profilo: {profile.ProfileName}");

            var progress = new Progress<ProgressInfo>(p =>
            {
                _lblStatus.Text = FormatStatusWithEta(p);
                _progress.Value = Math.Clamp(p.Percent, 0, 100);
                if (string.Equals(p.Phase, "CopySkipped", StringComparison.OrdinalIgnoreCase))
                {
                    Log(p.Message);
                }
            });

            _running = _copyCoordinator.CopyFromHashAsync(profile, _pause, progress, _cts.Token);
            var result = await ((Task<CopyExecutionResult>)_running);

            Log($"Copia completata. Copiati: {result.Copied}, sorgenti mancanti: {result.SkippedMissingSource}, errori: {result.Failed}, link album creati: {result.AlbumLinksCreated}, link album saltati: {result.AlbumLinksSkipped}, link album falliti: {result.AlbumLinksFailed}.");
        }
        catch (OperationCanceledException)
        {
            Log("Copia annullata.");
            _lblStatus.Text = "Interrotta";
        }
        catch (Exception ex)
        {
            Log($"Errore copia: {ex.Message}");
            _lblStatus.Text = "Errore";
        }
        finally
        {
            SetRunningUi(false);
            _operationStartedAt = null;
            _cts?.Dispose();
            _cts = null;
        }
    }

    private async Task RebuildAlbumLinksAsync()
    {
        if (_running is { IsCompleted: false })
        {
            return;
        }

        try
        {
            if (_selectedProfile is null)
            {
                throw new InvalidOperationException("Nessun profilo selezionato.");
            }

            var profile = _selectedProfile;
            _cts = new CancellationTokenSource();

            SetRunningUi(true, canPause: false);
            _operationStartedAt = DateTime.UtcNow;
            Log($"Rigenerazione link album per profilo: {profile.ProfileName}");

            var progress = new Progress<ProgressInfo>(p =>
            {
                _lblStatus.Text = FormatStatusWithEta(p);
                _progress.Value = Math.Clamp(p.Percent, 0, 100);
                if (string.Equals(p.Phase, "AlbumRebuildMissingDestination", StringComparison.OrdinalIgnoreCase))
                {
                    Log(p.Message);
                }
            });

            _running = _copyCoordinator.RebuildAlbumLinksAsync(profile, progress, _cts.Token);
            var result = await ((Task<AlbumRebuildResult>)_running);

            Log($"Rigenerazione album completata. Record: {result.ProcessedRows}, destinazioni mancanti: {result.MissingDestination}, link creati: {result.AlbumLinksCreated}, link saltati: {result.AlbumLinksSkipped}, link falliti: {result.AlbumLinksFailed}.");
        }
        catch (OperationCanceledException)
        {
            Log("Rigenerazione album annullata.");
            _lblStatus.Text = "Interrotta";
        }
        catch (Exception ex)
        {
            Log($"Errore rigenerazione album: {ex.Message}");
            _lblStatus.Text = "Errore";
        }
        finally
        {
            SetRunningUi(false);
            _operationStartedAt = null;
            _cts?.Dispose();
            _cts = null;
        }
    }

    private async Task RecalculateExifFromUtilityAsync()
    {
        if (_running is { IsCompleted: false })
        {
            return;
        }

        try
        {
            if (_selectedProfile is null)
            {
                throw new InvalidOperationException("Nessun profilo selezionato.");
            }

            var profile = _selectedProfile;
            _cts = new CancellationTokenSource();

            SetRunningUi(true, canPause: false);
            _operationStartedAt = DateTime.UtcNow;
            Log($"Utility: ricalcolo dtExif mancanti per profilo: {profile.ProfileName}");

            var progress = new Progress<ProgressInfo>(p =>
            {
                _lblStatus.Text = FormatStatusWithEta(p);
                _progress.Value = Math.Clamp(p.Percent, 0, 100);
            });

            _running = _utilityCoordinator.RecalculateMissingExifAsync(profile, progress, _cts.Token);
            var result = await ((Task<ExifRecalculationResult>)_running);

            Log($"Utility dtExif completata. Candidati: {result.TotalCandidates}, SRC aggiornati: {result.SrcUpdated}, HASH aggiornati: {result.HashUpdated}, file spostati: {result.MovedCopiedFiles}, sorgenti mancanti: {result.MissingSourceFiles}, destinazioni mancanti: {result.MissingCopiedDestinations}, senza EXIF: {result.SkippedNoExif}, errori: {result.Errors}.");

            MessageBox.Show(
                this,
                $"Ricalcolo completato.\n\n" +
                $"Candidati: {result.TotalCandidates}\n" +
                $"SRC aggiornati: {result.SrcUpdated}\n" +
                $"HASH aggiornati: {result.HashUpdated}\n" +
                $"File spostati: {result.MovedCopiedFiles}\n" +
                $"Errori: {result.Errors}",
                "Utility - Ricalcolo dtExif",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }
        catch (OperationCanceledException)
        {
            Log("Utility dtExif annullata.");
            _lblStatus.Text = "Interrotta";
        }
        catch (Exception ex)
        {
            Log($"Errore utility dtExif: {ex.Message}");
            _lblStatus.Text = "Errore";
        }
        finally
        {
            SetRunningUi(false);
            _operationStartedAt = null;
            _cts?.Dispose();
            _cts = null;
        }
    }

    private string FormatStatusWithEta(ProgressInfo progress)
    {
        if (_operationStartedAt is null || progress.Total <= 0 || progress.Processed <= 0)
        {
            return progress.Message;
        }

        var elapsed = DateTime.UtcNow - _operationStartedAt.Value;
        if (elapsed.TotalSeconds < 1)
        {
            return progress.Message;
        }

        var processed = Math.Min(progress.Processed, progress.Total);
        var rate = processed / elapsed.TotalSeconds;
        if (rate <= 0)
        {
            return progress.Message;
        }

        var remainingItems = progress.Total - processed;
        var remainingSeconds = remainingItems / rate;
        if (remainingSeconds < 0)
        {
            return progress.Message;
        }

        var eta = DateTime.Now.AddSeconds(remainingSeconds);
        var remaining = TimeSpan.FromSeconds(remainingSeconds);
        var remainingText = remaining.TotalHours >= 1
            ? remaining.ToString(@"hh\:mm\:ss")
            : remaining.ToString(@"mm\:ss");

        return $"{progress.Message} | ETA {eta:HH:mm:ss} (tra {remainingText})";
    }

    private void Log(string message)
    {
        _txtLog.AppendText($"[{DateTime.Now:HH:mm:ss}] {message}{Environment.NewLine}");
    }

    private void OpenHelpDocument(string markdownFileName, string documentLabel)
    {
        var helpPath = ResolveHelpMarkdownPath(markdownFileName);
        if (string.IsNullOrWhiteSpace(helpPath) || !File.Exists(helpPath))
        {
            MessageBox.Show(
                this,
                $"File non trovato (doc/{markdownFileName}).",
                "Help",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
            return;
        }

        try
        {
            using var viewer = new HelpViewerForm(helpPath);
            viewer.ShowDialog(this);
            Log($"Apertura {documentLabel}: {helpPath}");
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                this,
                $"Impossibile aprire {documentLabel}: {ex.Message}",
                "Help",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }

    private void EditSelectedProfile()
    {
        if (_selectedProfile is null)
        {
            MessageBox.Show(this, "Nessun profilo selezionato.", "Profilo", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        using var editor = new ProfileEditorForm(_selectedProfile);
        if (editor.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        var profile = editor.EditedProfile;
        var selectedName = _cmbProfiles.SelectedItem as string;
        var targetPath = Path.Combine(_configDirectory, $"{selectedName ?? profile.ProfileName}.profile");

        if (editor.SaveMode == ProfileSaveMode.SaveAs)
        {
            using var saveDialog = new SaveFileDialog
            {
                InitialDirectory = _configDirectory,
                Filter = "Profili (*.profile)|*.profile|Tutti i file (*.*)|*.*",
                FileName = $"{profile.ProfileName}.profile",
                AddExtension = true,
                DefaultExt = "profile",
                OverwritePrompt = true,
                Title = "Salva profilo come"
            };

            if (saveDialog.ShowDialog(this) != DialogResult.OK)
            {
                return;
            }

            targetPath = saveDialog.FileName;
        }

        try
        {
            SaveProfileToFile(targetPath, profile);
            Log($"Profilo salvato: {targetPath}");
            LoadProfileList();

            var savedName = Path.GetFileNameWithoutExtension(targetPath);
            _cmbProfiles.SelectedItem = savedName;
            if (_cmbProfiles.SelectedIndex < 0 && _cmbProfiles.Items.Count > 0)
            {
                _cmbProfiles.SelectedIndex = 0;
            }

            _lblStatus.Text = $"Profilo salvato: {savedName}";
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"Errore salvataggio profilo: {ex.Message}", "Profilo", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private static string? ResolveHelpMarkdownPath(string markdownFileName)
    {
        var candidates = new[] { Path.Combine(Environment.CurrentDirectory, "doc", markdownFileName) };

        foreach (var candidate in candidates)
        {
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            var documentPath = Path.Combine(current.FullName, "doc", markdownFileName);
            if (File.Exists(documentPath))
            {
                return documentPath;
            }

            current = current.Parent;
        }

        return null;
    }

    private static void SaveProfileToFile(string path, ProfileConfig profile)
    {
        var folder = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(folder))
        {
            Directory.CreateDirectory(folder);
        }

        var lines = new[]
        {
            $"ProfileName={profile.ProfileName}",
            $"MinSize={profile.MinSize}",
            $"UserName={profile.UserName ?? string.Empty}",
            $"IncludePath={profile.IncludePath}",
            $"ExcludePaths={string.Join(',', profile.ExcludePaths)}",
            $"Extensions={string.Join(',', profile.Extensions)}",
            $"DestinationRoot={profile.DestinationRoot}",
            $"DatabasePath={profile.DatabasePath}",
            $"DestinationTemplate={profile.DestinationTemplate}",
            $"AlbumDestination={profile.AlbumDestination ?? string.Empty}"
        };

        File.WriteAllLines(path, lines);
    }

    private void ShowAboutDialog()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var name = assembly.GetName();
        var infoVersion = assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion;
        var appVersion = string.IsNullOrWhiteSpace(infoVersion)
            ? (name.Version?.ToString() ?? "n/d")
            : infoVersion;
        var author = assembly
            .GetCustomAttribute<AssemblyCompanyAttribute>()?
            .Company;
        if (string.IsNullOrWhiteSpace(author))
        {
            author = "Giuliano Giusti";
        }

        var product = assembly
            .GetCustomAttribute<AssemblyProductAttribute>()?
            .Product;
        if (string.IsNullOrWhiteSpace(product))
        {
            product = "GestioneFoto";
        }

        var description = assembly
            .GetCustomAttribute<AssemblyDescriptionAttribute>()?
            .Description;
        if (string.IsNullOrWhiteSpace(description))
        {
            description = "Gestione e organizzazione foto/video con scansione, copia e album.";
        }

        var message = string.Join(
            Environment.NewLine,
            $"Prodotto: {product}",
            $"Versione: {appVersion}",
            $"Autore: {author}",
            $"Descrizione: {description}",
            $"Runtime: {RuntimeInformation.FrameworkDescription}",
            $"Sistema: {RuntimeInformation.OSDescription}",
            $"Architettura: {RuntimeInformation.ProcessArchitecture}");

        MessageBox.Show(
            this,
            message,
            "Informazioni",
            MessageBoxButtons.OK,
            MessageBoxIcon.Information);
    }

    private void LoadProfileList()
    {
        _cmbProfiles.Items.Clear();

        if (!Directory.Exists(_configDirectory))
        {
            _lblStatus.Text = "Cartella config non trovata.";
            return;
        }

        string[] profileNames;
        try
        {
            profileNames = Directory
                .EnumerateFiles(_configDirectory, "*.profile", SearchOption.TopDirectoryOnly)
                .Select(path => Path.GetFileNameWithoutExtension(path)!)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }
        catch (Exception ex)
        {
            _lblStatus.Text = $"Errore lettura profili: {ex.Message}";
            _selectedProfile = null;
            _txtProfileInfo.Text = string.Empty;
            return;
        }

        foreach (var name in profileNames)
        {
            _cmbProfiles.Items.Add(name);
        }

        if (_cmbProfiles.Items.Count > 0)
        {
            var lastUsed = TryReadLastUsedProfile();
            var preferred = profileNames.FirstOrDefault(x => string.Equals(x, "Patty", StringComparison.OrdinalIgnoreCase));
            var initial = profileNames.FirstOrDefault(x => string.Equals(x, _startupProfileHint, StringComparison.OrdinalIgnoreCase))
                ?? profileNames.FirstOrDefault(x => string.Equals(x, lastUsed, StringComparison.OrdinalIgnoreCase))
                ?? preferred
                ?? profileNames[0];

            _cmbProfiles.SelectedItem = initial;
            if (_cmbProfiles.SelectedIndex < 0)
            {
                _cmbProfiles.SelectedIndex = 0;
            }
        }
        else
        {
            _lblStatus.Text = "Nessun profilo disponibile in config.";
            _txtProfileInfo.Text = string.Empty;
            _selectedProfile = null;
        }

        RefreshWorkflowButtons();
    }

    private void LoadSelectedProfile()
    {
        var profileName = _cmbProfiles.SelectedItem as string;
        if (string.IsNullOrWhiteSpace(profileName))
        {
            _selectedProfile = null;
            _txtProfileInfo.Text = string.Empty;
            RefreshWorkflowButtons();
            return;
        }

        var profilePath = Path.Combine(_configDirectory, $"{profileName}.profile");
        try
        {
            _selectedProfile = ProfileLoader.Load(profilePath);
            _txtProfileInfo.Text = BuildProfileInfo(_selectedProfile);
            _lblStatus.Text = $"Profilo selezionato: {_selectedProfile.ProfileName}";
            SaveLastUsedProfile(_selectedProfile.ProfileName);
        }
        catch (Exception ex)
        {
            _selectedProfile = null;
            _txtProfileInfo.Text = string.Empty;
            _lblStatus.Text = $"Errore caricamento profilo: {ex.Message}";
        }

        RefreshWorkflowButtons();
    }

    private static string ResolveConfigDirectory()
    {
        var candidateFromCwd = Path.Combine(Environment.CurrentDirectory, "config");
        if (Directory.Exists(candidateFromCwd))
        {
            return candidateFromCwd;
        }

        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            var candidate = Path.Combine(current.FullName, "config");
            if (Directory.Exists(candidate))
            {
                return candidate;
            }

            current = current.Parent;
        }

        return Path.Combine(Environment.CurrentDirectory, "config");
    }

    private static string ResolveLastProfileStateFile()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var dir = Path.Combine(appData, "GestioneFoto.App");
        Directory.CreateDirectory(dir);
        return Path.Combine(dir, "last-profile.txt");
    }

    private string? TryReadLastUsedProfile()
    {
        try
        {
            if (!File.Exists(_lastProfileStateFile))
            {
                return null;
            }

            var value = File.ReadAllText(_lastProfileStateFile).Trim();
            return string.IsNullOrWhiteSpace(value) ? null : value;
        }
        catch
        {
            return null;
        }
    }

    private void SaveLastUsedProfile(string profileName)
    {
        try
        {
            File.WriteAllText(_lastProfileStateFile, profileName);
        }
        catch
        {
        }
    }

    private static string? NormalizeProfileHint(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        var value = raw.Trim().Trim('"');
        if (value.EndsWith(".profile", StringComparison.OrdinalIgnoreCase))
        {
            return Path.GetFileNameWithoutExtension(value);
        }

        return value;
    }

    private static string BuildProfileInfo(ProfileConfig profile)
    {
        var extensions = profile.Extensions.Length == 0 ? "-" : string.Join(", ", profile.Extensions);
        var excludes = profile.ExcludePaths.Length == 0
            ? "-"
            : string.Join(Environment.NewLine, profile.ExcludePaths.Select(x => $" - {x}"));

        return string.Join(
            Environment.NewLine,
            $"Profilo: {profile.ProfileName}",
            $"UserName: {profile.UserName ?? "-"}",
            $"IncludePath: {profile.IncludePath}",
            $"DatabasePath: {profile.DatabasePath}",
            $"DestinationRoot: {profile.DestinationRoot}",
            $"AlbumDestination: {profile.AlbumDestination ?? "-"}",
            $"DestinationTemplate: {profile.DestinationTemplate}",
            $"MinSize: {profile.MinSize} bytes",
            $"Extensions: {extensions}",
            "ExcludePaths:",
            excludes);
    }
}
