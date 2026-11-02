using System.Data;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using GestioneFoto.App.Models;
using GestioneFoto.App.Services;
using Microsoft.Data.Sqlite;

namespace GestioneFoto.App.UI;

public sealed class DatabaseViewerForm : Form
{
    private readonly ProfileConfig _profile;
    private readonly ExifDateReader _exifReader;
    private readonly DataGridView _grid;
    private readonly ComboBox _cmbTypeFilter;
    private readonly ComboBox _cmbAlbumFilter;
    private readonly DateTimePicker _dtFrom;
    private readonly DateTimePicker _dtTo;
    private readonly CheckBox _chkUseDate;
    private readonly TextBox _txtNameFilter;
    private readonly Button _btnClearFilters;
    private readonly Button _btnDuplicatesOnly;
    private readonly Button _btnAlignDuplicateDates;
    private readonly Button _btnEditDate;
    private readonly Button _btnEditAlbum;
    private readonly PictureBox _previewBox;
    private readonly Label _lblSourcePath;
    private readonly Label _lblSourceSize;
    private readonly Label _lblSourceAlbum;
    private readonly Label _lblSourceCreated;
    private readonly Label _lblSourceModified;
    private readonly Label _lblMediaType;
    private readonly Label _lblAcqDb;
    private readonly Label _lblAcqExif;
    private readonly Label _lblDstPath;
    private readonly Label _lblDstExists;
    private readonly Label _lblDstSize;
    private readonly Label _lblDstCreated;
    private readonly Label _lblDstModified;
    private readonly Label _lblDstAcqExif;
    private readonly Label _lblStatus;
    private readonly Panel _loadingOverlay;
    private readonly Label _lblLoading;
    private readonly ProgressBar _loadingProgress;

    private readonly DataTable _rowsTable = new("SourceRows");
    private readonly DataView _rowsView;
    private bool _showDuplicatesOnly;

    public DatabaseViewerForm(ProfileConfig profile)
    {
        _profile = profile;
        _exifReader = new ExifDateReader();
        _rowsView = _rowsTable.DefaultView;

        Text = $"Visualizza DB - {profile.ProfileName}";
        Width = 1380;
        Height = 820;
        StartPosition = FormStartPosition.CenterParent;

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 2
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 56));
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 44));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 94));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 6));
        Controls.Add(root);

        var leftPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            RowCount = 2,
            ColumnCount = 1,
            Padding = new Padding(8)
        };
        leftPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        leftPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.Controls.Add(leftPanel, 0, 0);

        var filterPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            WrapContents = true,
            FlowDirection = FlowDirection.LeftToRight,
            Margin = new Padding(0, 0, 0, 6),
            Padding = new Padding(0)
        };
        leftPanel.Controls.Add(filterPanel, 0, 0);

        _txtNameFilter = new TextBox
        {
            PlaceholderText = "Filtra nome/percorso",
            Width = 260
        };
        filterPanel.Controls.Add(CreateLabeledFilterHost("Nome", _txtNameFilter, 360));

        _cmbTypeFilter = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Width = 140
        };
        _cmbTypeFilter.Items.AddRange(new object[] { "Tutti", "Immagine", "Video" });
        _cmbTypeFilter.SelectedIndex = 0;
        filterPanel.Controls.Add(CreateLabeledFilterHost("Tipo", _cmbTypeFilter, 220));

        _cmbAlbumFilter = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Width = 220
        };
        _cmbAlbumFilter.Items.Add("Tutti");
        _cmbAlbumFilter.SelectedIndex = 0;
        filterPanel.Controls.Add(CreateLabeledFilterHost("Album", _cmbAlbumFilter, 310));

        _dtFrom = new DateTimePicker { Width = 120, Format = DateTimePickerFormat.Short };
        _dtTo = new DateTimePicker { Width = 120, Format = DateTimePickerFormat.Short };
        _chkUseDate = new CheckBox { Text = "Applica", AutoSize = true, Margin = new Padding(8, 6, 0, 0) };
        filterPanel.Controls.Add(CreateDateRangeFilterHost("Data", _dtFrom, _dtTo, _chkUseDate, 360));

        _btnClearFilters = new Button { Width = 90, Height = 26, Text = "Pulisci" };
        filterPanel.Controls.Add(CreateButtonHost(_btnClearFilters, 100));

        _btnDuplicatesOnly = new Button { Width = 130, Height = 26, Text = "Solo duplicati" };
        filterPanel.Controls.Add(CreateButtonHost(_btnDuplicatesOnly, 140));

        _btnAlignDuplicateDates = new Button { Width = 180, Height = 26, Text = "Allinea date duplicati", Enabled = false };
        filterPanel.Controls.Add(CreateButtonHost(_btnAlignDuplicateDates, 190));

        _btnEditDate = new Button { Width = 140, Height = 26, Text = "Modifica Data" };
        filterPanel.Controls.Add(CreateButtonHost(_btnEditDate, 150));

        _btnEditAlbum = new Button { Width = 140, Height = 26, Text = "Modifica Album" };
        filterPanel.Controls.Add(CreateButtonHost(_btnEditAlbum, 150));

        _grid = new DataGridView
        {
            Dock = DockStyle.Fill,
            ReadOnly = true,
            MultiSelect = true,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            AutoGenerateColumns = false,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            RowHeadersVisible = false
        };

        _grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            HeaderText = "File sorgente",
            DataPropertyName = "FilePathSrc",
            Width = 440,
            AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
            FillWeight = 65,
            SortMode = DataGridViewColumnSortMode.Automatic
        });
        _grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            HeaderText = "Tipo",
            DataPropertyName = "Tipo",
            Width = 90,
            AutoSizeMode = DataGridViewAutoSizeColumnMode.DisplayedCells,
            SortMode = DataGridViewColumnSortMode.Automatic
        });
        _grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            HeaderText = "Album",
            DataPropertyName = "Album",
            Width = 180,
            AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
            FillWeight = 35,
            SortMode = DataGridViewColumnSortMode.Automatic
        });
        _grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            HeaderText = "Data acquisizione",
            DataPropertyName = "AcqDateDb",
            Width = 170,
            AutoSizeMode = DataGridViewAutoSizeColumnMode.DisplayedCells,
            SortMode = DataGridViewColumnSortMode.Automatic
        });

        _grid.DataSource = _rowsView;
        _grid.SelectionChanged += (_, _) =>
        {
            UpdateDetailsForSelection();
            UpdateDuplicateDateButtonState();
        };
        leftPanel.Controls.Add(_grid, 0, 1);

        _cmbTypeFilter.SelectedIndexChanged += (_, _) => ApplyFilters();
        _cmbAlbumFilter.SelectedIndexChanged += (_, _) => ApplyFilters();
        _chkUseDate.CheckedChanged += (_, _) => ApplyFilters();
        _dtFrom.ValueChanged += (_, _) => ApplyFilters();
        _dtTo.ValueChanged += (_, _) => ApplyFilters();
        _txtNameFilter.TextChanged += (_, _) => ApplyFilters();
        _btnClearFilters.Click += (_, _) => ClearFilters();
        _btnDuplicatesOnly.Click += (_, _) =>
        {
            _showDuplicatesOnly = !_showDuplicatesOnly;
            _btnDuplicatesOnly.Text = _showDuplicatesOnly ? "Mostra tutti" : "Solo duplicati";
            ApplyFilters();
        };
        _btnAlignDuplicateDates.Click += async (_, _) => await AlignSelectedDuplicateDatesAsync();
        _btnEditDate.Click += async (_, _) => await EditSelectedAcquisitionDateAsync();
        _btnEditAlbum.Click += async (_, _) => await EditSelectedAlbumAsync();

        var rightPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            RowCount = 4,
            ColumnCount = 1,
            Padding = new Padding(8)
        };
        rightPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 320));
        rightPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 33));
        rightPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 25));
        rightPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 42));
        root.Controls.Add(rightPanel, 1, 0);

        var grpPreview = new GroupBox { Text = "Anteprima foto/video", Dock = DockStyle.Fill };
        var previewLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            RowCount = 2,
            ColumnCount = 1,
            Padding = new Padding(0)
        };
        previewLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        previewLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));

        _previewBox = new PictureBox
        {
            Dock = DockStyle.Fill,
            BackColor = Color.Black,
            SizeMode = PictureBoxSizeMode.Zoom
        };
        previewLayout.Controls.Add(_previewBox, 0, 0);

        var actionPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            Padding = new Padding(2, 2, 2, 0)
        };
        var btnOpen = new Button { Text = "Apri", Width = 78, Height = 26 };
        var btnPrint = new Button { Text = "Stampa", Width = 78, Height = 26 };
        var btnMail = new Button { Text = "Mail", Width = 78, Height = 26 };
        var btnShare = new Button { Text = "Condividi", Width = 90, Height = 26 };
        var btnOpenInExplorer = new Button { Text = "Apri Percorso in Esplora Risorse", Width = 220, Height = 26 };
        btnOpen.Click += (_, _) => OpenSelectedFile();
        btnPrint.Click += (_, _) => PrintSelectedFile();
        btnMail.Click += (_, _) => MailSelectedFile();
        btnShare.Click += (_, _) => ShareSelectedFile();
        btnOpenInExplorer.Click += (_, _) => OpenSelectedPathInExplorer();

        actionPanel.Controls.Add(btnOpen);
        actionPanel.Controls.Add(btnPrint);
        actionPanel.Controls.Add(btnMail);
        actionPanel.Controls.Add(btnShare);
        actionPanel.Controls.Add(btnOpenInExplorer);
        previewLayout.Controls.Add(actionPanel, 0, 1);

        grpPreview.Controls.Add(previewLayout);
        rightPanel.Controls.Add(grpPreview, 0, 0);

        var grpSource = new GroupBox { Text = "Dettagli file sorgente", Dock = DockStyle.Fill };
        var sourceLayout = BuildDetailsLayout();
        _lblSourcePath = AddDetail(sourceLayout, "Percorso");
        _lblSourceSize = AddDetail(sourceLayout, "Dimensione");
        _lblSourceAlbum = AddDetail(sourceLayout, "Album");
        _lblSourceCreated = AddDetail(sourceLayout, "Data creazione");
        _lblSourceModified = AddDetail(sourceLayout, "Data modifica");
        grpSource.Controls.Add(sourceLayout);
        rightPanel.Controls.Add(grpSource, 0, 1);

        var grpMedia = new GroupBox { Text = "Dettagli foto/video (acquisizione)", Dock = DockStyle.Fill };
        var mediaLayout = BuildDetailsLayout();
        _lblMediaType = AddDetail(mediaLayout, "Tipo");
        _lblAcqDb = AddDetail(mediaLayout, "Data definitiva (vista SQL)");
        _lblAcqExif = AddDetail(mediaLayout, "Data acquisizione EXIF");
        grpMedia.Controls.Add(mediaLayout);
        rightPanel.Controls.Add(grpMedia, 0, 2);

        var grpDestination = new GroupBox { Text = "Dettagli file in DestinationRoot", Dock = DockStyle.Fill };
        var dstLayout = BuildDetailsLayout();
        _lblDstPath = AddDetail(dstLayout, "Percorso destinazione");
        _lblDstExists = AddDetail(dstLayout, "Esiste");
        _lblDstSize = AddDetail(dstLayout, "Dimensione");
        _lblDstCreated = AddDetail(dstLayout, "Data creazione");
        _lblDstModified = AddDetail(dstLayout, "Data modifica");
        _lblDstAcqExif = AddDetail(dstLayout, "Data acquisizione (vista SQL)");
        grpDestination.Controls.Add(dstLayout);
        rightPanel.Controls.Add(grpDestination, 0, 3);

        _lblStatus = new Label
        {
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(8, 0, 0, 0),
            Text = "Pronto"
        };
        root.Controls.Add(_lblStatus, 0, 1);
        root.SetColumnSpan(_lblStatus, 2);

        _loadingOverlay = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.FromArgb(230, 245, 245, 245),
            Visible = false
        };
        var loadingCard = new Panel
        {
            Width = 380,
            Height = 120,
            BackColor = Color.White,
            BorderStyle = BorderStyle.FixedSingle
        };
        _lblLoading = new Label
        {
            Left = 18,
            Top = 20,
            Width = 340,
            Height = 24,
            Text = "Caricamento database in corso...",
            Font = new Font(SystemFonts.DefaultFont.FontFamily, 10, FontStyle.Bold)
        };
        _loadingProgress = new ProgressBar
        {
            Left = 18,
            Top = 56,
            Width = 340,
            Height = 18,
            Style = ProgressBarStyle.Marquee,
            MarqueeAnimationSpeed = 30
        };
        loadingCard.Controls.Add(_lblLoading);
        loadingCard.Controls.Add(_loadingProgress);
        _loadingOverlay.Controls.Add(loadingCard);
        _loadingOverlay.Resize += (_, _) =>
        {
            loadingCard.Left = Math.Max(0, (_loadingOverlay.Width - loadingCard.Width) / 2);
            loadingCard.Top = Math.Max(0, (_loadingOverlay.Height - loadingCard.Height) / 2);
        };
        root.Controls.Add(_loadingOverlay, 0, 0);
        root.SetColumnSpan(_loadingOverlay, 2);

        InitializeTableSchema();

        Load += async (_, _) => await LoadRowsAsync();
        FormClosed += (_, _) => ClearPreviewImage();
    }

    private void InitializeTableSchema()
    {
        _rowsTable.Columns.Add("Hash", typeof(string));
        _rowsTable.Columns.Add("FilePathSrc", typeof(string));
        _rowsTable.Columns.Add("FileName", typeof(string));
        _rowsTable.Columns.Add("FilePathDst", typeof(string));
        _rowsTable.Columns.Add("Album", typeof(string));
        _rowsTable.Columns.Add("Tipo", typeof(string));
        _rowsTable.Columns.Add("Size", typeof(long));
        _rowsTable.Columns.Add("DtExif", typeof(string));
        _rowsTable.Columns.Add("DtFileName", typeof(string));
        _rowsTable.Columns.Add("DtFile", typeof(string));
        _rowsTable.Columns.Add("AcqDateDb", typeof(string));
        _rowsTable.Columns.Add("AcqDateKey", typeof(string));
        _rowsTable.Columns.Add("IsDuplicate", typeof(bool));
        _rowsTable.Columns.Add("Thumbnail", typeof(byte[]));
    }

    private async Task LoadRowsAsync()
    {
        SetLoadingState(true, "Caricamento database in corso...");
        try
        {
            _rowsTable.Rows.Clear();

            if (!File.Exists(_profile.DatabasePath))
            {
                _lblStatus.Text = "Database non trovato.";
                return;
            }

            var loadedRows = await Task.Run(ReadRowsFromDb);
            var duplicateHashes = loadedRows
                .Where(row => !string.IsNullOrWhiteSpace(row.Hash))
                .GroupBy(row => row.Hash, StringComparer.OrdinalIgnoreCase)
                .Where(group => group.Count() > 1)
                .Select(group => group.Key)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            foreach (var row in loadedRows)
            {
                _rowsTable.Rows.Add(
                    row.Hash,
                    row.FilePathSrc,
                    Path.GetFileName(row.FilePathSrc),
                    row.FilePathDst,
                    row.Album,
                    string.IsNullOrWhiteSpace(row.Tipo) ? InferMediaType(row.FilePathSrc) : row.Tipo,
                    row.Size.HasValue ? row.Size.Value : DBNull.Value,
                    row.DtExif ?? string.Empty,
                    row.DtFileName ?? string.Empty,
                    row.DtFile ?? string.Empty,
                    row.AcqDateDb ?? string.Empty,
                    BuildAcqDateKey(row.AcqDateDb),
                    duplicateHashes.Contains(row.Hash),
                    row.Thumbnail is not null ? (object)row.Thumbnail : DBNull.Value);
            }

            RefreshAlbumFilterValues();
            ApplyFilters();
            UpdateDuplicateDateButtonState();

            if (_grid.Rows.Count > 0)
            {
                _grid.ClearSelection();
                _grid.Rows[0].Selected = true;
            }
        }
        catch (InvalidDataException ex)
        {
            _lblStatus.Text = ex.Message;
            MessageBox.Show(this, ex.Message, "Database non valido", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
        catch (SqliteException ex)
        {
            var message = $"Errore durante la lettura del database '{_profile.DatabasePath}': {ex.Message}";
            _lblStatus.Text = message;
            MessageBox.Show(this, message, "Errore database", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            SetLoadingState(false, string.Empty);
        }
    }

    private List<SourceFileRow> ReadRowsFromDb()
    {
        var existingTables = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        using (var schemaConnection = new SqliteConnection($"Data Source={_profile.DatabasePath}"))
        {
            schemaConnection.Open();
            using var schemaCommand = schemaConnection.CreateCommand();
            schemaCommand.CommandText = """
                    SELECT name
                    FROM sqlite_master
                    WHERE type = 'table'
                      AND name IN ('SRC', 'HASH');
                    """;

            using var schemaReader = schemaCommand.ExecuteReader();
            while (schemaReader.Read())
            {
                existingTables.Add(schemaReader.GetString(0));
            }
        }

        var missingTables = new[] { "SRC", "HASH" }
            .Where(table => !existingTables.Contains(table))
            .ToArray();
        if (missingTables.Length > 0)
        {
            throw new InvalidDataException(
                $"Il database '{_profile.DatabasePath}' non contiene le tabelle richieste: {string.Join(", ", missingTables)}. " +
                "Verifica il DatabasePath del profilo oppure esegui una scansione per inizializzare il database.");
        }

        new HashDbWriter().EnsureSchema(_profile.DatabasePath);

        using var conn = new SqliteConnection($"Data Source={_profile.DatabasePath}");
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT
                s.hash,
                s.file,
                coalesce(h.filePathDst, ''),
                coalesce(h.album, ''),
                coalesce(h.tipo, ''),
                s.size,
                s.dtExif,
                s.dtFileName,
                s.dtFile,
                v.data_acquisizione,
                s.thumbnail
            FROM SRC s
            LEFT JOIN HASH h
                ON h.hash = s.hash
            LEFT JOIN V_HASH_ACQUISITION_DATE v
                ON v.hash = s.hash
            ORDER BY s.file;
            """;

        var result = new List<SourceFileRow>();
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            var thumbnailData = reader.IsDBNull(10) ? null : (byte[])reader.GetValue(10);
            result.Add(new SourceFileRow
            {
                Hash = reader.GetString(0),
                FilePathSrc = reader.GetString(1),
                FilePathDst = reader.GetString(2),
                Album = reader.GetString(3),
                Tipo = reader.GetString(4),
                Size = reader.IsDBNull(5) ? null : reader.GetInt64(5),
                DtExif = reader.IsDBNull(6) ? null : reader.GetString(6),
                DtFileName = reader.IsDBNull(7) ? null : reader.GetString(7),
                DtFile = reader.IsDBNull(8) ? null : reader.GetString(8),
                AcqDateDb = reader.IsDBNull(9) ? null : reader.GetString(9),
                Thumbnail = thumbnailData
            });
        }

        return result;
    }

    private void SetLoadingState(bool isLoading, string message)
    {
        UseWaitCursor = isLoading;
        Cursor = isLoading ? Cursors.WaitCursor : Cursors.Default;
        _lblLoading.Text = string.IsNullOrWhiteSpace(message) ? "Caricamento database in corso..." : message;
        _loadingOverlay.Visible = isLoading;
        _loadingOverlay.BringToFront();
        _grid.Enabled = !isLoading;
        _cmbTypeFilter.Enabled = !isLoading;
        _cmbAlbumFilter.Enabled = !isLoading;
        _dtFrom.Enabled = !isLoading;
        _dtTo.Enabled = !isLoading;
        _chkUseDate.Enabled = !isLoading;
        _txtNameFilter.Enabled = !isLoading;
        _btnClearFilters.Enabled = !isLoading;
        _btnDuplicatesOnly.Enabled = !isLoading;
        _btnAlignDuplicateDates.Enabled = !isLoading && HasValidDuplicateDateSelection();
        _btnEditDate.Enabled = !isLoading;
        _btnEditAlbum.Enabled = !isLoading;
        _btnClearFilters.Refresh();
        _loadingOverlay.Refresh();
    }

    private void UpdateDetailsForSelection()
    {
        if (_grid.CurrentRow?.DataBoundItem is not DataRowView rowView)
        {
            ClearDetails();
            return;
        }

        var row = ToSourceFileRow(rowView);
        FillSourceDetails(row);
        FillMediaDetails(row);
        FillDestinationDetails(row);
        UpdatePreview(row);
    }

    private static SourceFileRow ToSourceFileRow(DataRowView rowView)
    {
        var row = rowView.Row;
        var thumbnailData = row.IsNull("Thumbnail") ? null : row.Field<byte[]>("Thumbnail");
        return new SourceFileRow
        {
            Hash = row.Field<string>("Hash") ?? string.Empty,
            FilePathSrc = row.Field<string>("FilePathSrc") ?? string.Empty,
            FilePathDst = row.Field<string>("FilePathDst") ?? string.Empty,
            Album = row.Field<string>("Album") ?? string.Empty,
            Tipo = row.Field<string>("Tipo") ?? string.Empty,
            Size = row.IsNull("Size") ? null : row.Field<long>("Size"),
            DtExif = row.Field<string>("DtExif"),
            DtFileName = row.Field<string>("DtFileName"),
            DtFile = row.Field<string>("DtFile"),
            AcqDateDb = row.Field<string>("AcqDateDb"),
            IsDuplicate = row.Field<bool>("IsDuplicate"),
            Thumbnail = thumbnailData
        };
    }

    private void RefreshAlbumFilterValues()
    {
        var current = _cmbAlbumFilter.SelectedItem as string;
        var albumValues = _rowsTable
            .AsEnumerable()
            .Select(r => (r.Field<string>("Album") ?? string.Empty).Trim())
            .Where(v => !string.IsNullOrWhiteSpace(v))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(v => v, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        _cmbAlbumFilter.BeginUpdate();
        _cmbAlbumFilter.Items.Clear();
        _cmbAlbumFilter.Items.Add("Tutti");
        foreach (var album in albumValues)
        {
            _cmbAlbumFilter.Items.Add(album);
        }

        if (!string.IsNullOrWhiteSpace(current) && _cmbAlbumFilter.Items.Contains(current))
        {
            _cmbAlbumFilter.SelectedItem = current;
        }
        else
        {
            _cmbAlbumFilter.SelectedIndex = 0;
        }

        _cmbAlbumFilter.EndUpdate();
    }

    private void ClearFilters()
    {
        _cmbTypeFilter.SelectedIndex = 0;
        _cmbAlbumFilter.SelectedIndex = 0;
        _txtNameFilter.Text = string.Empty;
        _chkUseDate.Checked = false;
        _showDuplicatesOnly = false;
        _btnDuplicatesOnly.Text = "Solo duplicati";
        _dtFrom.Value = DateTime.Today;
        _dtTo.Value = DateTime.Today;
        ApplyFilters();
    }

    private async Task EditSelectedAcquisitionDateAsync()
    {
        var selectedRows = GetSelectedRows();
        if (selectedRows.Count == 0)
        {
            MessageBox.Show(this, "Seleziona almeno una riga.", "Modifica Data", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        var initialDate = TryParseDbDate(selectedRows[0].AcqDateDb) ?? DateTime.Now;
        if (!TryPromptDateTime("Imposta data acquisizione", initialDate, out var newDate))
        {
            return;
        }

        SetLoadingState(true, "Aggiornamento data acquisizione in corso...");
        try
        {
            await Task.Run(() => UpdateAcquisitionDateInDb(selectedRows, newDate));
            await LoadRowsAsync();
            _lblStatus.Text = $"Data acquisizione aggiornata su {selectedRows.Count} record.";
        }
        finally
        {
            SetLoadingState(false, string.Empty);
        }
    }

    private async Task EditSelectedAlbumAsync()
    {
        var selectedRows = GetSelectedRows();
        if (selectedRows.Count == 0)
        {
            MessageBox.Show(this, "Seleziona almeno una riga.", "Modifica Album", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        if (!TryPromptText("Imposta album", "Album", selectedRows[0].Album ?? string.Empty, out var albumValue))
        {
            return;
        }

        SetLoadingState(true, "Aggiornamento album in corso...");
        try
        {
            await Task.Run(() => UpdateAlbumInDb(selectedRows, albumValue.Trim()));
            await LoadRowsAsync();
            _lblStatus.Text = $"Album aggiornato su {selectedRows.Count} record.";
        }
        finally
        {
            SetLoadingState(false, string.Empty);
        }
    }

    private List<SourceFileRow> GetSelectedRows()
    {
        var result = new List<SourceFileRow>();

        foreach (DataGridViewRow gridRow in _grid.SelectedRows)
        {
            if (gridRow.DataBoundItem is DataRowView rowView)
            {
                result.Add(ToSourceFileRow(rowView));
            }
        }

        if (result.Count == 0 && _grid.CurrentRow?.DataBoundItem is DataRowView currentRowView)
        {
            result.Add(ToSourceFileRow(currentRowView));
        }

        return result;
    }

    private bool HasValidDuplicateDateSelection()
    {
        var selectedRows = GetSelectedRows();
        return selectedRows.Count == 1 &&
               selectedRows[0].IsDuplicate &&
               TryParseDbDate(selectedRows[0].AcqDateDb).HasValue;
    }

    private void UpdateDuplicateDateButtonState()
    {
        _btnAlignDuplicateDates.Enabled = HasValidDuplicateDateSelection();
    }

    private async Task AlignSelectedDuplicateDatesAsync()
    {
        var selectedRows = GetSelectedRows();
        if (selectedRows.Count != 1 || !selectedRows[0].IsDuplicate)
        {
            MessageBox.Show(
                this,
                "Seleziona un solo file duplicato da usare come riferimento.",
                "Allinea date duplicati",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            return;
        }

        var referenceRow = selectedRows[0];
        var acquisitionDate = TryParseDbDate(referenceRow.AcqDateDb);
        if (!acquisitionDate.HasValue)
        {
            MessageBox.Show(
                this,
                "Il file selezionato non ha una data acquisizione nel database. Impostala prima con 'Modifica Data'.",
                "Allinea date duplicati",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            return;
        }

        var duplicateCount = _rowsTable.AsEnumerable()
            .Count(row => string.Equals(row.Field<string>("Hash"), referenceRow.Hash, StringComparison.OrdinalIgnoreCase));
        var confirmation = MessageBox.Show(
            this,
            $"Impostare la data {acquisitionDate.Value:yyyy-MM-dd HH:mm:ss} del file selezionato su tutti i {duplicateCount} file con lo stesso hash?\n\n" +
            "La data e il percorso di destinazione nel database verranno aggiornati.",
            "Allinea date duplicati",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Question);
        if (confirmation != DialogResult.Yes)
        {
            return;
        }

        SetLoadingState(true, "Allineamento date dei duplicati...");
        try
        {
            var updatedRows = await Task.Run(() => UpdateDuplicateAcquisitionDateInDb(referenceRow, acquisitionDate.Value));
            await LoadRowsAsync();
            _lblStatus.Text = $"Data allineata su {updatedRows} file duplicati.";
        }
        catch (SqliteException ex)
        {
            var message = $"Errore durante l'aggiornamento dei duplicati nel database: {ex.Message}";
            _lblStatus.Text = message;
            MessageBox.Show(this, message, "Allinea date duplicati", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        catch (InvalidOperationException ex)
        {
            _lblStatus.Text = ex.Message;
            MessageBox.Show(this, ex.Message, "Allinea date duplicati", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
        finally
        {
            SetLoadingState(false, string.Empty);
        }
    }

    private int UpdateDuplicateAcquisitionDateInDb(SourceFileRow referenceRow, DateTime acquisitionDate)
    {
        using var conn = new SqliteConnection($"Data Source={_profile.DatabasePath}");
        conn.Open();
        using var tx = conn.BeginTransaction();

        var formattedDate = acquisitionDate.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);

        using var updateSources = conn.CreateCommand();
        updateSources.Transaction = tx;
        updateSources.CommandText = """
            UPDATE SRC
            SET dtAcqOverride = $acqDate
            WHERE hash = $hash;
            """;
        updateSources.Parameters.AddWithValue("$acqDate", formattedDate);
        updateSources.Parameters.AddWithValue("$hash", referenceRow.Hash);
        var updatedRows = updateSources.ExecuteNonQuery();
        if (updatedRows < 2)
        {
            throw new InvalidOperationException("Il database non contiene piu' file con l'hash selezionato.");
        }

        using var updateHashPath = conn.CreateCommand();
        updateHashPath.Transaction = tx;
        updateHashPath.CommandText = """
            UPDATE HASH
            SET filePathDst = $dstPath
            WHERE hash = $hash;
            """;
        updateHashPath.Parameters.AddWithValue("$dstPath", BuildDestinationPath(referenceRow, acquisitionDate));
        updateHashPath.Parameters.AddWithValue("$hash", referenceRow.Hash);
        updateHashPath.ExecuteNonQuery();

        tx.Commit();
        return updatedRows;
    }

    private void UpdateAcquisitionDateInDb(IReadOnlyCollection<SourceFileRow> rows, DateTime newDate)
    {
        using var conn = new SqliteConnection($"Data Source={_profile.DatabasePath}");
        conn.Open();
        using var tx = conn.BeginTransaction();

        var formattedDate = newDate.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);

        using var updateSrc = conn.CreateCommand();
        updateSrc.Transaction = tx;
        updateSrc.CommandText = """
            UPDATE SRC
            SET dtAcqOverride = $acqDate
            WHERE hash = $hash;
            """;
        var pAcqDate = updateSrc.Parameters.Add("$acqDate", SqliteType.Text);
        var pHash = updateSrc.Parameters.Add("$hash", SqliteType.Text);

        using var selectEffectiveDate = conn.CreateCommand();
        selectEffectiveDate.Transaction = tx;
        selectEffectiveDate.CommandText = """
            SELECT data_acquisizione
            FROM V_HASH_ACQUISITION_DATE
            WHERE hash = $hash;
            """;
        var pSelectHash = selectEffectiveDate.Parameters.Add("$hash", SqliteType.Text);

        using var updateHashPath = conn.CreateCommand();
        updateHashPath.Transaction = tx;
        updateHashPath.CommandText = """
            UPDATE HASH
            SET filePathDst = $dstPath
            WHERE hash = $hash;
            """;
        var pHashDst = updateHashPath.Parameters.Add("$hash", SqliteType.Text);
        var pDstPath = updateHashPath.Parameters.Add("$dstPath", SqliteType.Text);

        foreach (var row in rows.DistinctBy(row => row.Hash, StringComparer.OrdinalIgnoreCase))
        {
            pAcqDate.Value = formattedDate;
            pHash.Value = row.Hash;
            updateSrc.ExecuteNonQuery();

            pSelectHash.Value = row.Hash;
            var effectiveDate = selectEffectiveDate.ExecuteScalar()?.ToString();
            var updatedRow = new SourceFileRow
            {
                Hash = row.Hash,
                FilePathSrc = row.FilePathSrc,
                FilePathDst = row.FilePathDst,
                Album = row.Album,
                Tipo = row.Tipo,
                Size = row.Size,
                DtExif = row.DtExif,
                DtFileName = row.DtFileName,
                DtFile = row.DtFile,
                AcqDateDb = effectiveDate
            };
            pHashDst.Value = row.Hash;
            pDstPath.Value = BuildDestinationPath(updatedRow, TryParseDbDate(effectiveDate));
            updateHashPath.ExecuteNonQuery();
        }

        tx.Commit();
    }

    private void UpdateAlbumInDb(IReadOnlyCollection<SourceFileRow> rows, string album)
    {
        using var conn = new SqliteConnection($"Data Source={_profile.DatabasePath}");
        conn.Open();
        using var tx = conn.BeginTransaction();

        using var upsertHash = conn.CreateCommand();
        upsertHash.Transaction = tx;
        upsertHash.CommandText = """
            INSERT INTO HASH(hash, filePathSrc, filePathDst, album, tipo, copied)
            VALUES($hash, $filePathSrc, $filePathDst, $album, $tipo, 0)
            ON CONFLICT(hash) DO UPDATE SET
                filePathSrc = excluded.filePathSrc,
                filePathDst = excluded.filePathDst,
                album = excluded.album,
                tipo = excluded.tipo;
            """;

        var pHash = upsertHash.Parameters.Add("$hash", SqliteType.Text);
        var pSrc = upsertHash.Parameters.Add("$filePathSrc", SqliteType.Text);
        var pDst = upsertHash.Parameters.Add("$filePathDst", SqliteType.Text);
        var pAlbum = upsertHash.Parameters.Add("$album", SqliteType.Text);
        var pTipo = upsertHash.Parameters.Add("$tipo", SqliteType.Text);

        foreach (var row in rows)
        {
            pHash.Value = row.Hash;
            pSrc.Value = row.FilePathSrc;
            pDst.Value = ResolveDestinationPath(row);
            pAlbum.Value = album;
            pTipo.Value = string.IsNullOrWhiteSpace(row.Tipo) ? InferMediaType(row.FilePathSrc) : row.Tipo;
            upsertHash.ExecuteNonQuery();
        }

        tx.Commit();
    }

    private bool TryPromptDateTime(string title, DateTime initialValue, out DateTime selected)
    {
        selected = initialValue;

        using var form = new Form
        {
            Text = title,
            Width = 320,
            Height = 160,
            StartPosition = FormStartPosition.CenterParent,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            MinimizeBox = false,
            MaximizeBox = false
        };

        var picker = new DateTimePicker
        {
            Left = 16,
            Top = 16,
            Width = 270,
            Format = DateTimePickerFormat.Custom,
            CustomFormat = "yyyy-MM-dd HH:mm:ss",
            ShowUpDown = true,
            Value = initialValue
        };

        var btnOk = new Button
        {
            Text = "OK",
            DialogResult = DialogResult.OK,
            Left = 130,
            Top = 56,
            Width = 75
        };

        var btnCancel = new Button
        {
            Text = "Annulla",
            DialogResult = DialogResult.Cancel,
            Left = 211,
            Top = 56,
            Width = 75
        };

        form.Controls.Add(picker);
        form.Controls.Add(btnOk);
        form.Controls.Add(btnCancel);
        form.AcceptButton = btnOk;
        form.CancelButton = btnCancel;

        if (form.ShowDialog(this) != DialogResult.OK)
        {
            return false;
        }

        selected = picker.Value;
        return true;
    }

    private bool TryPromptText(string title, string labelText, string initialValue, out string result)
    {
        result = initialValue;

        using var form = new Form
        {
            Text = title,
            Width = 420,
            Height = 170,
            StartPosition = FormStartPosition.CenterParent,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            MinimizeBox = false,
            MaximizeBox = false
        };

        var lbl = new Label
        {
            Left = 16,
            Top = 18,
            Width = 380,
            Text = labelText
        };

        var txt = new TextBox
        {
            Left = 16,
            Top = 40,
            Width = 380,
            Text = initialValue
        };

        var btnOk = new Button
        {
            Text = "OK",
            DialogResult = DialogResult.OK,
            Left = 240,
            Top = 78,
            Width = 75
        };

        var btnCancel = new Button
        {
            Text = "Annulla",
            DialogResult = DialogResult.Cancel,
            Left = 321,
            Top = 78,
            Width = 75
        };

        form.Controls.Add(lbl);
        form.Controls.Add(txt);
        form.Controls.Add(btnOk);
        form.Controls.Add(btnCancel);
        form.AcceptButton = btnOk;
        form.CancelButton = btnCancel;

        if (form.ShowDialog(this) != DialogResult.OK)
        {
            return false;
        }

        result = txt.Text;
        return true;
    }

    private void ApplyFilters()
    {
        var predicates = new List<string>();

        var type = _cmbTypeFilter.SelectedItem as string;
        if (!string.IsNullOrWhiteSpace(type) && !string.Equals(type, "Tutti", StringComparison.OrdinalIgnoreCase))
        {
            predicates.Add($"Tipo = '{EscapeFilterValue(type)}'");
        }

        var album = _cmbAlbumFilter.SelectedItem as string;
        if (!string.IsNullOrWhiteSpace(album) && !string.Equals(album, "Tutti", StringComparison.OrdinalIgnoreCase))
        {
            predicates.Add($"Album = '{EscapeFilterValue(album)}'");
        }

        var nameSearch = _txtNameFilter.Text.Trim();
        if (!string.IsNullOrWhiteSpace(nameSearch))
        {
            var like = EscapeLikeValue(nameSearch);
            predicates.Add($"(FileName LIKE '%{like}%' OR FilePathSrc LIKE '%{like}%')");
        }

        if (_chkUseDate.Checked)
        {
            var from = _dtFrom.Value.Date;
            var to = _dtTo.Value.Date;
            if (to < from)
            {
                (from, to) = (to, from);
            }

            var fromKey = from.ToString("yyyyMMdd", CultureInfo.InvariantCulture) + "000000";
            var toKey = to.ToString("yyyyMMdd", CultureInfo.InvariantCulture) + "235959";
            predicates.Add($"AcqDateKey <> '' AND AcqDateKey >= '{fromKey}' AND AcqDateKey <= '{toKey}'");
        }

        if (_showDuplicatesOnly)
        {
            predicates.Add("IsDuplicate = true");
        }

        _rowsView.RowFilter = predicates.Count == 0 ? string.Empty : string.Join(" AND ", predicates);
        _rowsView.Sort = "AcqDateDb DESC, FilePathSrc ASC";

        _lblStatus.Text = $"Record sorgenti: {_rowsView.Count} filtrati su {_rowsTable.Rows.Count}";

        if (_grid.Rows.Count > 0)
        {
            _grid.ClearSelection();
            _grid.Rows[0].Selected = true;
        }
        else
        {
            ClearDetails();
        }
    }

    private static string EscapeFilterValue(string value)
    {
        return value.Replace("'", "''");
    }

    private static string EscapeLikeValue(string value)
    {
        return value
            .Replace("'", "''")
            .Replace("[", "[[]")
            .Replace("%", "[%]")
            .Replace("*", "[*]");
    }

    private static string BuildAcqDateKey(string? rawDate)
    {
        var dt = TryParseDbDate(rawDate);
        return dt?.ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture) ?? string.Empty;
    }

    private static DateTime? TryParseDbDate(string? rawDate)
    {
        if (string.IsNullOrWhiteSpace(rawDate))
        {
            return null;
        }

        var formats = new[]
        {
            "yyyy-MM-dd HH:mm:ss",
            "yyyy-MM-dd HH:mm",
            "yyyy-MM-dd"
        };

        if (DateTime.TryParseExact(rawDate, formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var exact))
        {
            return exact;
        }

        if (DateTime.TryParse(rawDate, out var loose))
        {
            return loose;
        }

        return null;
    }

    private void FillSourceDetails(SourceFileRow row)
    {
        _lblSourcePath.Text = row.FilePathSrc;
        _lblSourceAlbum.Text = string.IsNullOrWhiteSpace(row.Album) ? "-" : row.Album;

        if (File.Exists(row.FilePathSrc))
        {
            var srcInfo = new FileInfo(row.FilePathSrc);
            _lblSourceSize.Text = FormatSize(srcInfo.Length, row.Size);
            _lblSourceCreated.Text = srcInfo.CreationTime.ToString("yyyy-MM-dd HH:mm:ss");
            _lblSourceModified.Text = srcInfo.LastWriteTime.ToString("yyyy-MM-dd HH:mm:ss");
        }
        else if (ArchiveSupport.IsVirtualPath(row.FilePathSrc))
        {
            _lblSourceSize.Text = row.Size.HasValue ? $"{row.Size:N0} byte (archivio)" : "-";
            _lblSourceCreated.Text = "File in archivio";
            _lblSourceModified.Text = "File in archivio";
        }
        else
        {
            _lblSourceSize.Text = row.Size.HasValue ? $"{row.Size:N0} byte (DB)" : "-";
            _lblSourceCreated.Text = "File non trovato";
            _lblSourceModified.Text = "File non trovato";
        }
    }

    private void FillMediaDetails(SourceFileRow row)
    {
        _lblMediaType.Text = string.IsNullOrWhiteSpace(row.Tipo) ? InferMediaType(row.FilePathSrc) : row.Tipo;
        _lblAcqDb.Text = string.IsNullOrWhiteSpace(row.AcqDateDb)
            ? "-"
            : row.AcqDateDb;

        var exifDate = File.Exists(row.FilePathSrc) && !ArchiveSupport.IsVirtualPath(row.FilePathSrc)
            ? _exifReader.TryRead(row.FilePathSrc)
            : null;
        _lblAcqExif.Text = exifDate?.ToString("yyyy-MM-dd HH:mm:ss") ?? "-";
    }

    private void FillDestinationDetails(SourceFileRow row)
    {
        var destinationPath = ResolveDestinationPath(row);
        _lblDstPath.Text = string.IsNullOrWhiteSpace(destinationPath) ? "-" : destinationPath;

        var dstExists = !string.IsNullOrWhiteSpace(destinationPath) && File.Exists(destinationPath);
        _lblDstExists.Text = dstExists ? "Si" : "No";
        _lblDstExists.ForeColor = dstExists ? SystemColors.ControlText : Color.Red;
        _lblDstAcqExif.Text = string.IsNullOrWhiteSpace(row.AcqDateDb) ? "-" : row.AcqDateDb;

        if (!dstExists)
        {
            _lblDstSize.Text = "-";
            _lblDstCreated.Text = "-";
            _lblDstModified.Text = "-";
            return;
        }

        var dstInfo = new FileInfo(destinationPath);
        _lblDstSize.Text = $"{dstInfo.Length:N0} byte";
        _lblDstCreated.Text = dstInfo.CreationTime.ToString("yyyy-MM-dd HH:mm:ss");
        _lblDstModified.Text = dstInfo.LastWriteTime.ToString("yyyy-MM-dd HH:mm:ss");

    }

    private void UpdatePreview(SourceFileRow row)
    {
        // Prova prima il thumbnail dal DB (per archivi e file già scansionati)
        if (row.Thumbnail is not null)
        {
            try
            {
                using var memStream = new MemoryStream(row.Thumbnail);
                ClearPreviewImage();
                _previewBox.Image = Image.FromStream(memStream);
                _lblStatus.Text = $"Record sorgenti: {_rowsView.Count} filtrati su {_rowsTable.Rows.Count}";
                return;
            }
            catch
            {
            }
        }

        // Se non c'è thumbnail, usa il metodo originale (file diretto o shell thumbnail)
        var previewPath = File.Exists(row.FilePathSrc)
            ? row.FilePathSrc
            : (File.Exists(row.FilePathDst) ? row.FilePathDst : null);

        if (string.IsNullOrWhiteSpace(previewPath))
        {
            ClearPreviewImage();
            _lblStatus.Text = "Anteprima non disponibile: file non trovato.";
            return;
        }

        try
        {
            var preview = TryCreatePreviewImage(previewPath);
            ClearPreviewImage();
            _previewBox.Image = preview;
            _lblStatus.Text = $"Record sorgenti: {_rowsView.Count} filtrati su {_rowsTable.Rows.Count}";
        }
        catch
        {
            ClearPreviewImage();
            _lblStatus.Text = "Anteprima non disponibile per il file selezionato.";
        }
    }

    private Image TryCreatePreviewImage(string filePath)
    {
        var extension = Path.GetExtension(filePath).ToLowerInvariant();
        if (IsImageExtension(extension))
        {
            using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var img = Image.FromStream(fs);
            return new Bitmap(img);
        }

        var shellThumb = ShellPreviewProvider.TryGetThumbnail(filePath, 800, 600);
        if (shellThumb is not null)
        {
            return shellThumb;
        }

        throw new InvalidOperationException("Anteprima non disponibile");
    }

    private static bool IsImageExtension(string extension)
    {
        return extension is ".jpg" or ".jpeg" or ".png" or ".bmp" or ".gif" or ".tif" or ".tiff" or ".webp";
    }

    private static Control CreateLabeledFilterHost(string labelText, Control control, int hostWidth)
    {
        var host = new Panel
        {
            Width = hostWidth,
            Height = 30,
            Margin = new Padding(0, 0, 8, 6)
        };

        var label = new Label
        {
            AutoSize = false,
            Left = 0,
            Top = 6,
            Width = 54,
            Height = 18,
            Text = labelText,
            TextAlign = ContentAlignment.MiddleLeft
        };

        control.Left = label.Right + 6;
        control.Top = 2;
        control.Height = Math.Max(control.Height, 24);

        host.Controls.Add(label);
        host.Controls.Add(control);
        return host;
    }

    private static Control CreateDateRangeFilterHost(string labelText, DateTimePicker fromPicker, DateTimePicker toPicker, CheckBox enabledCheckBox, int hostWidth)
    {
        var host = new Panel
        {
            Width = hostWidth,
            Height = 30,
            Margin = new Padding(0, 0, 8, 6)
        };

        var label = new Label
        {
            AutoSize = false,
            Left = 0,
            Top = 6,
            Width = 40,
            Height = 18,
            Text = labelText,
            TextAlign = ContentAlignment.MiddleLeft
        };

        fromPicker.Left = label.Right + 6;
        fromPicker.Top = 2;

        var separator = new Label
        {
            AutoSize = false,
            Left = fromPicker.Right + 6,
            Top = 6,
            Width = 14,
            Height = 18,
            Text = "a",
            TextAlign = ContentAlignment.MiddleCenter
        };

        toPicker.Left = separator.Right + 6;
        toPicker.Top = 2;

        enabledCheckBox.Left = toPicker.Right + 8;
        enabledCheckBox.Top = 5;

        host.Controls.Add(label);
        host.Controls.Add(fromPicker);
        host.Controls.Add(separator);
        host.Controls.Add(toPicker);
        host.Controls.Add(enabledCheckBox);
        return host;
    }

    private static Control CreateButtonHost(Button button, int hostWidth)
    {
        var host = new Panel
        {
            Width = hostWidth,
            Height = 30,
            Margin = new Padding(0, 0, 8, 6)
        };

        button.Left = 0;
        button.Top = 2;
        host.Controls.Add(button);
        return host;
    }

    private void ClearDetails()
    {
        foreach (var label in new[]
                 {
                     _lblSourcePath, _lblSourceSize, _lblSourceAlbum, _lblSourceCreated, _lblSourceModified,
                     _lblMediaType, _lblAcqDb, _lblAcqExif,
                     _lblDstPath, _lblDstExists, _lblDstSize, _lblDstCreated, _lblDstModified, _lblDstAcqExif
                 })
        {
            label.Text = "-";
        }

        _lblDstExists.ForeColor = SystemColors.ControlText;

        ClearPreviewImage();
    }

    private string ResolveDestinationPath(SourceFileRow row)
    {
        var acquisitionDate = TryParseDbDate(row.AcqDateDb);
        if (acquisitionDate.HasValue)
        {
            return BuildDestinationPath(row, acquisitionDate.Value);
        }

        if (!string.IsNullOrWhiteSpace(row.FilePathDst))
        {
            return row.FilePathDst;
        }

        if (string.IsNullOrWhiteSpace(row.FilePathSrc))
        {
            return string.Empty;
        }

        return BuildDestinationPath(row, acquisitionDate);
    }

    private string BuildDestinationPath(SourceFileRow row, DateTime? date)
    {
        if (string.IsNullOrWhiteSpace(_profile.DestinationRoot))
        {
            return string.Empty;
        }

        var targetFileName = !string.IsNullOrWhiteSpace(row.FilePathDst)
            ? Path.GetFileName(row.FilePathDst)
            : Path.GetFileName(row.FilePathSrc);
        if (string.IsNullOrWhiteSpace(targetFileName))
        {
            return string.Empty;
        }

        var relativeFolder = date.HasValue
            ? BuildDestinationRelativeFolder(_profile.DestinationTemplate, date.Value)
            : string.Empty;
        if (string.IsNullOrWhiteSpace(relativeFolder))
        {
            return Path.Combine(_profile.DestinationRoot, targetFileName);
        }

        return Path.Combine(_profile.DestinationRoot, relativeFolder, targetFileName);
    }

    private static string BuildDestinationRelativeFolder(string template, DateTime date)
    {
        var normalized = (template ?? string.Empty)
            .Replace('\\', '/')
            .Trim('/');

        if (string.IsNullOrWhiteSpace(normalized))
        {
            return string.Empty;
        }

        var replaced = normalized
            .Replace("YYYY", date.ToString("yyyy", CultureInfo.InvariantCulture), StringComparison.OrdinalIgnoreCase)
            .Replace("MM", date.ToString("MM", CultureInfo.InvariantCulture), StringComparison.OrdinalIgnoreCase)
            .Replace("DD", date.ToString("dd", CultureInfo.InvariantCulture), StringComparison.OrdinalIgnoreCase);

        var parts = replaced
            .Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        return parts.Length == 0 ? string.Empty : Path.Combine(parts);
    }

    private void ClearPreviewImage()
    {
        var previous = _previewBox.Image;
        _previewBox.Image = null;
        previous?.Dispose();
    }

    private void OpenSelectedFile()
    {
        ExecuteForSelectedFile(
            "Apri file",
            true,
            filePath => Process.Start(new ProcessStartInfo(filePath) { UseShellExecute = true }));
    }

    private void PrintSelectedFile()
    {
        ExecuteForSelectedFile(
            "Stampa file",
            true,
            filePath => Process.Start(new ProcessStartInfo(filePath) { UseShellExecute = true, Verb = "print" }));
    }

    private void MailSelectedFile()
    {
        ExecuteForSelectedFile(
            "Invio mail",
            true,
            filePath =>
            {
                var subject = Uri.EscapeDataString("Condivisione foto/video");
                var body = Uri.EscapeDataString($"Ti invio questo file:{Environment.NewLine}{filePath}");
                Process.Start(new ProcessStartInfo($"mailto:?subject={subject}&body={body}") { UseShellExecute = true });
                Clipboard.SetText(filePath);
                MessageBox.Show(
                    this,
                    "Client mail aperto. Il percorso file e' stato copiato negli appunti per facilitare l'allegato.",
                    "Mail",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            });
    }

    private void ShareSelectedFile()
    {
        ExecuteForSelectedFile(
            "Condivisione",
            true,
            filePath =>
            {
                try
                {
                    Process.Start(new ProcessStartInfo(filePath) { UseShellExecute = true, Verb = "share" });
                }
                catch
                {
                    Clipboard.SetText(filePath);
                    Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{filePath}\"") { UseShellExecute = true });
                    MessageBox.Show(
                        this,
                        "Il comando Condividi non e' disponibile su questo sistema. Ho aperto Esplora file e copiato il percorso negli appunti.",
                        "Condivisione",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information);
                }
            });
    }

    private void OpenSelectedPathInExplorer()
    {
        ExecuteForSelectedFile(
            "Apri Percorso in Esplora Risorse",
            true,
            filePath => Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{filePath}\"") { UseShellExecute = true }));
    }

    private void ExecuteForSelectedFile(string actionName, bool requireExistingFile, Action<string> action)
    {
        var filePath = GetSelectedPathForAction();
        if (string.IsNullOrWhiteSpace(filePath))
        {
            MessageBox.Show(this, "Nessun file selezionato.", actionName, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        if (requireExistingFile && !File.Exists(filePath))
        {
            MessageBox.Show(this, $"File non trovato:{Environment.NewLine}{filePath}", actionName, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        try
        {
            action(filePath);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"Operazione non riuscita: {ex.Message}", actionName, MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private string? GetSelectedPathForAction()
    {
        if (_grid.CurrentRow?.DataBoundItem is not DataRowView rowView)
        {
            return null;
        }

        var row = ToSourceFileRow(rowView);
        var sourcePath = row.FilePathSrc;
        if (!string.IsNullOrWhiteSpace(sourcePath) && File.Exists(sourcePath))
        {
            return sourcePath;
        }

        var destinationPath = ResolveDestinationPath(row);
        if (!string.IsNullOrWhiteSpace(destinationPath) && File.Exists(destinationPath))
        {
            return destinationPath;
        }

        return !string.IsNullOrWhiteSpace(sourcePath)
            ? sourcePath
            : destinationPath;
    }

    private static string FormatSize(long sourceSize, long? dbSize)
    {
        if (dbSize.HasValue && dbSize.Value != sourceSize)
        {
            return $"{sourceSize:N0} byte (FS), {dbSize.Value:N0} byte (DB)";
        }

        return $"{sourceSize:N0} byte";
    }

    private static string InferMediaType(string path)
    {
        var ext = Path.GetExtension(path).Trim().TrimStart('.').ToLowerInvariant();
        var videoExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "mp4", "avi", "mov", "mkv", "wmv", "flv", "webm", "m4v", "3gp", "mpg", "mpeg", "m2ts"
        };

        return videoExtensions.Contains(ext) ? "Video" : "Immagine";
    }

    private static TableLayoutPanel BuildDetailsLayout()
    {
        var panel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true,
            ColumnCount = 2,
            RowCount = 0,
            Padding = new Padding(8)
        };

        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 170));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        return panel;
    }

    private static Label AddDetail(TableLayoutPanel panel, string title)
    {
        var rowIndex = panel.RowCount;
        panel.RowCount++;
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 24));

        var titleLabel = new Label
        {
            AutoSize = false,
            Dock = DockStyle.Fill,
            Font = new Font(SystemFonts.DefaultFont, FontStyle.Bold),
            Margin = new Padding(0, 2, 6, 2),
            TextAlign = ContentAlignment.MiddleLeft,
            Text = title + ":"
        };

        var valueLabel = new Label
        {
            AutoSize = false,
            Dock = DockStyle.Fill,
            AutoEllipsis = true,
            Margin = new Padding(0, 2, 0, 2),
            TextAlign = ContentAlignment.MiddleLeft,
            Text = "-"
        };

        panel.Controls.Add(titleLabel, 0, rowIndex);
        panel.Controls.Add(valueLabel, 1, rowIndex);

        return valueLabel;
    }

    private sealed class SourceFileRow
    {
        public required string Hash { get; init; }
        public required string FilePathSrc { get; init; }
        public required string FilePathDst { get; init; }
        public required string Album { get; init; }
        public required string Tipo { get; init; }
        public long? Size { get; init; }
        public string? DtExif { get; init; }
        public string? DtFileName { get; init; }
        public string? DtFile { get; init; }
        public string? AcqDateDb { get; init; }
        public bool IsDuplicate { get; init; }
        public byte[]? Thumbnail { get; init; }
    }

    private static class ShellPreviewProvider
    {
        [Flags]
        private enum SIIGBF
        {
            ResizeToFit = 0x00,
            BiggerSizeOk = 0x01,
            MemoryOnly = 0x02,
            IconOnly = 0x04,
            ThumbnailOnly = 0x08,
            InCacheOnly = 0x10,
            CropToSquare = 0x20,
            WideThumbnail = 0x40,
            IconBackground = 0x80,
            ScaleUp = 0x100
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct SIZE
        {
            public int cx;
            public int cy;
        }

        [ComImport]
        [Guid("bcc18b79-ba16-442f-80c4-8a59c30c463b")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IShellItemImageFactory
        {
            void GetImage(SIZE size, SIIGBF flags, out IntPtr phbm);
        }

        [DllImport("shell32.dll", CharSet = CharSet.Unicode, PreserveSig = true)]
        private static extern int SHCreateItemFromParsingName(
            string pszPath,
            nint pbc,
            in Guid riid,
            [MarshalAs(UnmanagedType.Interface)] out IShellItemImageFactory ppv);

        [DllImport("gdi32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool DeleteObject(nint hObject);

        public static Image? TryGetThumbnail(string path, int width, int height)
        {
            var iid = typeof(IShellItemImageFactory).GUID;
            var hr = SHCreateItemFromParsingName(path, nint.Zero, in iid, out var factory);
            if (hr != 0 || factory is null)
            {
                return null;
            }

            nint hBitmap = nint.Zero;
            try
            {
                factory.GetImage(new SIZE { cx = width, cy = height }, SIIGBF.BiggerSizeOk | SIIGBF.ThumbnailOnly, out hBitmap);
                if (hBitmap == nint.Zero)
                {
                    return null;
                }

                using var img = Image.FromHbitmap(hBitmap);
                return new Bitmap(img);
            }
            catch
            {
                return null;
            }
            finally
            {
                if (hBitmap != nint.Zero)
                {
                    _ = DeleteObject(hBitmap);
                }

                if (factory is not null)
                {
                    Marshal.ReleaseComObject(factory);
                }
            }
        }
    }
}