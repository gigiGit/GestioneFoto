using GestioneFoto.App.Models;

namespace GestioneFoto.App.UI;

internal enum ProfileSaveMode
{
    Save,
    SaveAs
}

internal sealed class ProfileEditorForm : Form
{
    private readonly TextBox _txtProfileName;
    private readonly TextBox _txtUserName;
    private readonly TextBox _txtIncludePath;
    private readonly ListBox _lstExcludePaths;
    private readonly ListBox _lstExtensions;
    private readonly TextBox _txtDestinationRoot;
    private readonly TextBox _txtDatabasePath;
    private readonly ComboBox _cmbDestinationTemplate;
    private readonly TextBox _txtAlbumDestination;
    private readonly NumericUpDown _numMinSize;

    public ProfileConfig EditedProfile { get; private set; }
    public ProfileSaveMode SaveMode { get; private set; } = ProfileSaveMode.Save;

    public ProfileEditorForm(ProfileConfig profile)
    {
        EditedProfile = profile;

        Text = "Modifica Profilo";
        Width = 940;
        Height = 700;
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.Sizable;
        MaximizeBox = false;
        MinimizeBox = false;

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(12),
            ColumnCount = 2,
            RowCount = 10
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 170));
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        for (var i = 0; i < 8; i++)
        {
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
        }

        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));

        _txtProfileName = AddTextRow(root, "ProfileName", profile.ProfileName, 0);
        _txtUserName = AddTextRow(root, "UserName", profile.UserName ?? string.Empty, 1);

        _txtIncludePath = AddTextRow(root, "IncludePath", profile.IncludePath, 2, includeBrowseButton: true, onBrowse: () => BrowseFolder(_txtIncludePath!));
        _txtDestinationRoot = AddTextRow(root, "DestinationRoot", profile.DestinationRoot, 3, includeBrowseButton: true, onBrowse: () => BrowseFolder(_txtDestinationRoot!));
        _txtDatabasePath = AddTextRow(root, "DatabasePath", profile.DatabasePath, 4, includeBrowseButton: true, onBrowse: () => BrowseDatabase(_txtDatabasePath!));

        _cmbDestinationTemplate = AddTemplateRow(root, "DestinationTemplate", profile.DestinationTemplate, 5);

        _txtAlbumDestination = AddTextRow(root, "AlbumDestination", profile.AlbumDestination ?? string.Empty, 6, includeBrowseButton: true, onBrowse: () => BrowseFolder(_txtAlbumDestination!));

        _numMinSize = AddNumericRow(root, "MinSize", profile.MinSize, 7);

        var listsPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            Margin = new Padding(3, 8, 3, 8)
        };
        listsPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        listsPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));

        _lstExcludePaths = new ListBox { Dock = DockStyle.Fill };
        foreach (var path in profile.ExcludePaths)
        {
            _lstExcludePaths.Items.Add(path);
        }
        var excludeBox = CreateListEditorGroup(
            "ExcludePaths",
            _lstExcludePaths,
            onAdd: () => AddExcludePath(),
            onRemove: () => RemoveSelectedItem(_lstExcludePaths));
        listsPanel.Controls.Add(excludeBox, 0, 0);

        _lstExtensions = new ListBox { Dock = DockStyle.Fill };
        foreach (var ext in profile.Extensions)
        {
            _lstExtensions.Items.Add(ext);
        }
        var extensionBox = CreateListEditorGroup(
            "Extensions",
            _lstExtensions,
            onAdd: () => AddExtension(),
            onRemove: () => RemoveSelectedItem(_lstExtensions));
        listsPanel.Controls.Add(extensionBox, 1, 0);

        root.Controls.Add(new Label
        {
            Text = "Liste",
            TextAlign = ContentAlignment.MiddleLeft,
            Dock = DockStyle.Fill,
            Margin = new Padding(3, 8, 3, 0)
        }, 0, 8);
        root.Controls.Add(listsPanel, 1, 8);

        var buttonPanel = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.RightToLeft,
            Dock = DockStyle.Fill,
            AutoSize = true,
            Margin = new Padding(0)
        };

        var btnCancel = new Button { Text = "Annulla", Width = 110, Height = 32, DialogResult = DialogResult.Cancel };
        var btnSaveAs = new Button { Text = "Salva come", Width = 110, Height = 32 };
        var btnSave = new Button { Text = "Salva", Width = 110, Height = 32 };

        btnSave.Click += (_, _) => SaveAndClose(ProfileSaveMode.Save);
        btnSaveAs.Click += (_, _) => SaveAndClose(ProfileSaveMode.SaveAs);

        buttonPanel.Controls.Add(btnCancel);
        buttonPanel.Controls.Add(btnSaveAs);
        buttonPanel.Controls.Add(btnSave);

        root.Controls.Add(buttonPanel, 0, 9);
        root.SetColumnSpan(buttonPanel, 2);

        Controls.Add(root);

        AcceptButton = btnSave;
        CancelButton = btnCancel;
    }

    private static TextBox AddTextRow(
        TableLayoutPanel grid,
        string label,
        string value,
        int row,
        bool includeBrowseButton = false,
        Action? onBrowse = null)
    {
        grid.Controls.Add(CreateLabel(label), 0, row);

        if (!includeBrowseButton)
        {
            var txt = new TextBox
            {
                Text = value,
                Dock = DockStyle.Fill,
                Margin = new Padding(3, 4, 3, 4)
            };

            grid.Controls.Add(txt, 1, row);
            return txt;
        }

        var panel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Margin = new Padding(0),
            ColumnCount = 2,
            RowCount = 1
        };
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 36));

        var textBox = new TextBox
        {
            Text = value,
            Dock = DockStyle.Fill,
            Margin = new Padding(3, 4, 3, 4)
        };
        var browseButton = new Button
        {
            Text = "...",
            Width = 30,
            Height = 26,
            Margin = new Padding(0, 4, 3, 4)
        };
        browseButton.Click += (_, _) => onBrowse?.Invoke();

        panel.Controls.Add(textBox, 0, 0);
        panel.Controls.Add(browseButton, 1, 0);

        grid.Controls.Add(panel, 1, row);
        return textBox;
    }

    private static NumericUpDown AddNumericRow(TableLayoutPanel grid, string label, long value, int row)
    {
        grid.Controls.Add(CreateLabel(label), 0, row);

        var num = new NumericUpDown
        {
            Dock = DockStyle.Left,
            Width = 180,
            Margin = new Padding(3, 4, 3, 4),
            Minimum = 0,
            Maximum = int.MaxValue,
            ThousandsSeparator = true,
            Value = Math.Clamp(value, 0, int.MaxValue)
        };

        grid.Controls.Add(num, 1, row);
        return num;
    }

    private static Label CreateLabel(string text)
    {
        return new Label
        {
            Text = text,
            TextAlign = ContentAlignment.MiddleLeft,
            Dock = DockStyle.Fill,
            Margin = new Padding(3, 8, 3, 0)
        };
    }

    private static ComboBox AddTemplateRow(TableLayoutPanel grid, string label, string value, int row)
    {
        var lbl = CreateLabel(label);

        var combo = new ComboBox
        {
            Dock = DockStyle.Fill,
            Margin = new Padding(3, 4, 3, 4),
            DropDownStyle = ComboBoxStyle.DropDownList
        };
        combo.Items.AddRange(new object[] { "YYYY/MM", "YYYY/MM/DD" });
        var normalized = string.IsNullOrWhiteSpace(value) ? "YYYY/MM/DD" : value;
        combo.SelectedItem = combo.Items.Cast<object>().FirstOrDefault(x => string.Equals(x?.ToString(), normalized, StringComparison.OrdinalIgnoreCase));
        if (combo.SelectedIndex < 0)
        {
            combo.Items.Add(normalized);
            combo.SelectedItem = normalized;
        }

        grid.Controls.Add(lbl, 0, row);
        grid.Controls.Add(combo, 1, row);
        return combo;
    }

    private static GroupBox CreateListEditorGroup(string title, ListBox listBox, Action onAdd, Action onRemove)
    {
        var group = new GroupBox
        {
            Text = title,
            Dock = DockStyle.Fill,
            Padding = new Padding(10)
        };

        var panel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1
        };
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120));

        var actions = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false
        };

        var btnAdd = new Button { Text = "Aggiungi", Width = 105, Height = 30 };
        var btnRemove = new Button { Text = "Rimuovi", Width = 105, Height = 30 };
        btnAdd.Click += (_, _) => onAdd();
        btnRemove.Click += (_, _) => onRemove();

        actions.Controls.Add(btnAdd);
        actions.Controls.Add(btnRemove);

        panel.Controls.Add(listBox, 0, 0);
        panel.Controls.Add(actions, 1, 0);
        group.Controls.Add(panel);
        return group;
    }

    private void BrowseFolder(TextBox target)
    {
        using var dialog = new FolderBrowserDialog();
        if (Directory.Exists(target.Text))
        {
            dialog.SelectedPath = target.Text;
        }

        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            target.Text = dialog.SelectedPath;
        }
    }

    private void BrowseDatabase(TextBox target)
    {
        using var dialog = new OpenFileDialog
        {
            Filter = "SQLite DB (*.db;*.sqlite)|*.db;*.sqlite|Tutti i file (*.*)|*.*",
            CheckFileExists = false,
            FileName = string.IsNullOrWhiteSpace(target.Text) ? string.Empty : target.Text
        };

        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            target.Text = dialog.FileName;
        }
    }

    private void AddExcludePath()
    {
        using var dialog = new FolderBrowserDialog();
        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        AddDistinctItem(_lstExcludePaths, dialog.SelectedPath, StringComparer.OrdinalIgnoreCase);
    }

    private void AddExtension()
    {
        var value = PromptForText("Nuova estensione", "Inserisci estensione (es. jpg, png, mp4):");
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        var normalized = value.Trim().TrimStart('.').ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return;
        }

        AddDistinctItem(_lstExtensions, normalized, StringComparer.OrdinalIgnoreCase);
    }

    private static void AddDistinctItem(ListBox listBox, string value, StringComparer comparer)
    {
        var exists = listBox.Items
            .Cast<object>()
            .Select(x => x?.ToString() ?? string.Empty)
            .Any(x => comparer.Equals(x, value));

        if (!exists)
        {
            listBox.Items.Add(value);
        }
    }

    private static void RemoveSelectedItem(ListBox listBox)
    {
        var idx = listBox.SelectedIndex;
        if (idx >= 0)
        {
            listBox.Items.RemoveAt(idx);
        }
    }

    private string? PromptForText(string title, string label)
    {
        using var prompt = new Form
        {
            Width = 460,
            Height = 155,
            Text = title,
            StartPosition = FormStartPosition.CenterParent,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            MaximizeBox = false,
            MinimizeBox = false
        };

        var lbl = new Label
        {
            Left = 12,
            Top = 15,
            Width = 420,
            Text = label
        };
        var txt = new TextBox
        {
            Left = 12,
            Top = 40,
            Width = 420
        };
        var btnOk = new Button
        {
            Text = "OK",
            Left = 260,
            Width = 80,
            Top = 74,
            DialogResult = DialogResult.OK
        };
        var btnCancel = new Button
        {
            Text = "Annulla",
            Left = 352,
            Width = 80,
            Top = 74,
            DialogResult = DialogResult.Cancel
        };

        prompt.Controls.Add(lbl);
        prompt.Controls.Add(txt);
        prompt.Controls.Add(btnOk);
        prompt.Controls.Add(btnCancel);
        prompt.AcceptButton = btnOk;
        prompt.CancelButton = btnCancel;

        return prompt.ShowDialog(this) == DialogResult.OK ? txt.Text : null;
    }

    private void SaveAndClose(ProfileSaveMode mode)
    {
        if (!TryBuildProfile(out var profile, out var error))
        {
            MessageBox.Show(this, error, "Profilo", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        EditedProfile = profile;
        SaveMode = mode;
        DialogResult = DialogResult.OK;
        Close();
    }

    private bool TryBuildProfile(out ProfileConfig profile, out string error)
    {
        var profileName = _txtProfileName.Text.Trim();
        var includePath = _txtIncludePath.Text.Trim();
        var databasePath = _txtDatabasePath.Text.Trim();
        var destinationRoot = _txtDestinationRoot.Text.Trim();
        var destinationTemplate = _cmbDestinationTemplate.Text.Trim();

        if (string.IsNullOrWhiteSpace(profileName))
        {
            profile = EditedProfile;
            error = "ProfileName e' obbligatorio.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(includePath))
        {
            profile = EditedProfile;
            error = "IncludePath e' obbligatorio.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(databasePath))
        {
            profile = EditedProfile;
            error = "DatabasePath e' obbligatorio.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(destinationRoot))
        {
            profile = EditedProfile;
            error = "DestinationRoot e' obbligatorio.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(destinationTemplate))
        {
            profile = EditedProfile;
            error = "DestinationTemplate e' obbligatorio.";
            return false;
        }

        var minSize = (long)_numMinSize.Value;

        profile = new ProfileConfig
        {
            ProfileName = profileName,
            UserName = NullIfWhiteSpace(_txtUserName.Text),
            IncludePath = includePath,
            DatabasePath = databasePath,
            DestinationRoot = destinationRoot,
            DestinationTemplate = destinationTemplate,
            AlbumDestination = NullIfWhiteSpace(_txtAlbumDestination.Text),
            Extensions = _lstExtensions.Items.Cast<object>().Select(x => x.ToString() ?? string.Empty).Where(x => !string.IsNullOrWhiteSpace(x)).ToArray(),
            ExcludePaths = _lstExcludePaths.Items.Cast<object>().Select(x => x.ToString() ?? string.Empty).Where(x => !string.IsNullOrWhiteSpace(x)).ToArray(),
            MinSize = minSize
        };

        error = string.Empty;
        return true;
    }

    private static string? NullIfWhiteSpace(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
