using System.Collections.ObjectModel;
using System.Globalization;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Windows.Input;
using Axora.Studio.Models;
using Axora.Studio.Services;
using Axora.Studio.Services.Contracts;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Axora.Studio.ViewModels;

public sealed class ResumeViewModel : ObservableObject
{
    private readonly ResumeStore _store;
    private readonly IResumeFilePicker _picker;
    private Func<Task<ResumeDeparture>> _decision = () => Task.FromResult(ResumeDeparture.Cancel);
    private Func<ResumeDocument, Task<bool>> _consent = _ => Task.FromResult(false);
    private Func<StudioRoute, Task<bool>> _navigate = _ => Task.FromResult(false);
    private Func<nint> _owner = () => 0;
    private Action<Action> _dispatch = action => action();
    private readonly HashSet<string> _invalidFields = new(StringComparer.Ordinal);
    private int _page;
    private long _editorGeneration, _editorActivationVersion = -1;
    private string _status = "Open or create a Resume";
    public ResumeSession Session { get; }
    public string Status { get => _status; private set => SetProperty(ref _status, value); }
    public string ActiveTitle => Session.Current?.Document.ResumeTitle ?? "No active Resume";
    public bool HasDocument => Session.Current is not null;
    public bool IsSaving => Session.IsActive;
    public bool CanEdit => Session.CanEdit && _editorActivationVersion == Session.ActivationVersion;
    public ObservableCollection<ResumeEntry> Documents { get; } = [];
    public ObservableCollection<ResumeEditorGroup> Groups { get; } = [];
    public ObservableCollection<ResumeRecoveryEntry> RecoveryEntries { get; } = [];
    public event EventHandler? EditorStructureChanged;
    public IAsyncRelayCommand NewCommand { get; }
    public IAsyncRelayCommand<ResumeEntry> OpenCommand { get; }
    public IAsyncRelayCommand ImportCommand { get; }
    public IAsyncRelayCommand SaveCommand { get; }
    public IAsyncRelayCommand SaveCopyCommand { get; }
    public IAsyncRelayCommand BackCommand { get; }
    public IAsyncRelayCommand RefreshCommand { get; }
    public IAsyncRelayCommand NextPageCommand { get; }
    public IAsyncRelayCommand PreviousPageCommand { get; }
    public IAsyncRelayCommand<ResumeRecoveryEntry> RecoverCommand { get; }
    public IAsyncRelayCommand<ResumeEntry> ReviewRecoveryCommand { get; }
    public ICommand UndoCommand { get; }
    public ResumeViewModel(ResumeSession session, ResumeStore store, IResumeFilePicker picker)
    {
        (Session, _store, _picker) = (session, store, picker);
        Session.Changed += OnSessionChanged;
        NewCommand = new AsyncRelayCommand(async () => { var result = await Session.NewAsync(_decision); await EnterEditorAsync(result); });
        OpenCommand = new AsyncRelayCommand<ResumeEntry>(async entry => { if (entry?.CanOpen == true) await EnterEditorAsync(await Session.OpenAsync(entry.DocumentId, _decision)); });
        ImportCommand = new AsyncRelayCommand(ImportAsync);
        SaveCommand = new AsyncRelayCommand(async () => { var result = await Session.SaveAsync(); Status = Session.IsDirty ? Session.Status : result.Message; });
        SaveCopyCommand = new AsyncRelayCommand(async () => { var result = await Session.SaveAsync(true); Status = Session.IsDirty ? Session.Status : result.Message; });
        BackCommand = new AsyncRelayCommand(async () => { await _navigate(StudioRoute.ResumeDashboard); });
        RefreshCommand = new AsyncRelayCommand(RefreshAsync);
        NextPageCommand = new AsyncRelayCommand(async () => { if (_page < 9) { _page++; await RefreshAsync(); } });
        PreviousPageCommand = new AsyncRelayCommand(async () => { if (_page > 0) { _page--; await RefreshAsync(); } });
        RecoverCommand = new AsyncRelayCommand<ResumeRecoveryEntry>(async entry =>
        {
            if (entry is not null) await EnterEditorAsync(await Session.RecoverAsync(entry.DocumentId, entry.Backup, _decision));
        });
        ReviewRecoveryCommand = new AsyncRelayCommand<ResumeEntry>(ReviewRecoveryAsync);
        UndoCommand = new RelayCommand(() =>
        {
            if (!CanEdit) { Status = ResumeEditorField.EditingUnavailable; return; }
            if (_invalidFields.Count != 0) { _invalidFields.Clear(); Session.SetInvalidDraft(false); RebuildEditor(); }
            else if (Session.Undo()) RebuildEditor();
        });
    }
    public void Configure(Func<nint> owner, Func<Task<ResumeDeparture>> decision, Func<ResumeDocument, Task<bool>> consent,
        Func<StudioRoute, Task<bool>> navigate, Action<Action> dispatch)
    { (_owner, _decision, _consent, _navigate, _dispatch) = (owner, decision, consent, navigate, dispatch); }
    public async Task RefreshAsync()
    {
        try
        {
            var entries = await _store.ListAsync(_page);
            RecoveryEntries.Clear();
            Documents.Clear(); foreach (var entry in entries) Documents.Add(entry);
            Status = $"Page {_page + 1} · {entries.Count} documents";
        }
        catch (Exception) { Status = "Resume library unavailable or exceeds its safety limit"; }
    }
    private async Task ImportAsync()
    {
        if (Session.IsActive) { Status = "Busy · wait for the admitted operation"; return; }
        try
        {
            string? path = await _picker.SelectImportAsync(_owner());
            if (path is null) { Status = "Import cancelled"; return; }
            byte[] snapshot = await ResumeStore.CaptureAsync(path);
            await EnterEditorAsync(await Session.ImportAsync(snapshot, _decision, _consent));
        }
        catch (Exception) { Status = "Import failed · source and current content preserved"; }
    }
    private async Task EnterEditorAsync(ResumeResult result)
    {
        Status = result.Message;
        if (!result.Success) return;
        _invalidFields.Clear(); RebuildEditor(); await _navigate(StudioRoute.ResumeEditor);
    }
    public Task<bool> GuardDepartureAsync() => Session.GuardDepartureAsync(_decision);
    public Task<bool> PrepareCloseAsync() => Session.PrepareCloseAsync(_decision);
    private void OnSessionChanged(object? sender, EventArgs args) => _dispatch(() =>
    {
        Status = Session.Status;
        OnPropertyChanged(nameof(ActiveTitle)); OnPropertyChanged(nameof(HasDocument)); OnPropertyChanged(nameof(IsSaving));
        if (!Session.IsActive && _editorActivationVersion != Session.ActivationVersion)
        { _invalidFields.Clear(); RebuildEditor(); }
        else if (!Session.HasInvalidDraft && _invalidFields.Count != 0) { _invalidFields.Clear(); RebuildEditor(); }
        OnPropertyChanged(nameof(CanEdit));
    });
    public void RebuildEditor()
    {
        _editorGeneration++;
        _editorActivationVersion = Session.ActivationVersion;
        Groups.Clear(); RecoveryEntries.Clear();
        var file = Session.Current;
        if (file is null) { EditorStructureChanged?.Invoke(this, EventArgs.Empty); return; }
        var profile = new ResumeEditorGroup("Profile", "Contact information and document title", null, null, null);
        profile.Fields.Add(Field(typeof(ResumeDocument).GetProperty(nameof(ResumeDocument.ResumeTitle))!, "ResumeTitle", file.Document.ResumeTitle));
        AddFields(profile.Fields, file.Document.Header, "Header"); Groups.Add(profile);
        foreach (var section in file.Document.SectionOrder)
        {
            string? collection = section switch { ResumeSection.Education => "Education", ResumeSection.Experience => "Experiences",
                ResumeSection.Skills => "SkillCategories", ResumeSection.Projects => "Projects", ResumeSection.Certifications => "Certifications",
                ResumeSection.Achievements => "Achievements", ResumeSection.Responsibilities => "Responsibilities", _ => null };
            string visibility = "Show" + section;
            var group = new ResumeEditorGroup(section.ToString(), "Edit content, visibility and order", section,
                EditorAction(() => MoveSection(section, -1)), EditorAction(() => MoveSection(section, 1)));
            var visibilityProperty = typeof(ResumeDocument).GetProperty(visibility)!;
            group.Fields.Add(Field(visibilityProperty, visibility, visibilityProperty.GetValue(file.Document)!));
            if (collection is null)
                group.Fields.Add(Field(typeof(ResumeDocument).GetProperty("Summary")!, "Summary", file.Document.Summary));
            else
            {
                var property = typeof(ResumeDocument).GetProperty(collection)!;
                var items = ((System.Collections.IEnumerable)property.GetValue(file.Document)!).Cast<object>().ToArray();
                Type itemType = property.PropertyType.GetGenericArguments()[0];
                group.AddCommand = new RelayCommand(EditorAction(() => AddEntry(collection, itemType)));
                for (int index = 0; index < items.Length; index++)
                {
                    int entryIndex = index;
                    var entry = new ResumeEditorItem(index + 1,
                        EditorAction(() => RemoveEntry(collection, entryIndex)), EditorAction(() => MoveEntry(collection, entryIndex, -1)), EditorAction(() => MoveEntry(collection, entryIndex, 1)));
                    AddFields(entry.Fields, items[index], collection + "/" + index.ToString(CultureInfo.InvariantCulture));
                    group.Items.Add(entry);
                }
            }
            Groups.Add(group);
        }
        var preferences = new ResumeEditorGroup("Preferences", "Saved preferences for later rendering", null, null, null);
        AddFields(preferences.Fields, file.Document.Formatting, "Formatting"); Groups.Add(preferences);
        try
        {
            foreach (string backup in _store.Backups(file.DocumentId))
            {
                string revision = Path.GetFileName(backup).Split('_')[0].TrimStart('0');
                RecoveryEntries.Add(new(file.DocumentId, backup, "Recover revision " + revision));
            }
        }
        catch (Exception) { Status = "Saved revisions unavailable · current editor content retained"; }
        OnPropertyChanged(nameof(CanEdit));
        EditorStructureChanged?.Invoke(this, EventArgs.Empty);
    }
    private async Task ReviewRecoveryAsync(ResumeEntry? entry)
    {
        RecoveryEntries.Clear();
        if (entry is null) return;
        try
        {
            foreach (string backup in _store.Backups(entry.DocumentId))
            {
                try
                {
                    var file = await _store.ReadBackupAsync(entry.DocumentId, backup);
                    RecoveryEntries.Add(new(entry.DocumentId, backup, $"Recover revision {file.Revision} · {file.Document.ResumeTitle}"));
                }
                catch (Exception) { Status = "An invalid saved revision was excluded from recovery"; }
            }
            Status = RecoveryEntries.Count == 0 ? "No verified saved revisions available" : "Choose a verified revision to publish as a new saved revision";
        }
        catch (Exception) { Status = "Saved revisions unavailable"; }
    }
    private void AddFields(ObservableCollection<ResumeEditorField> fields, object item, string prefix)
    {
        foreach (var property in item.GetType().GetProperties().Where(property => property.Name != "Id"))
            fields.Add(Field(property, prefix + "/" + property.Name, property.GetValue(item)!));
    }
    private ResumeEditorField Field(PropertyInfo property, string path, object value)
    {
        bool narrative = property.Name is "Summary" or "BulletsRaw" or "Description";
        int limit = property.Name == "ResumeTitle" ? ResumeLimits.Title : narrative ? ResumeLimits.Narrative
            : path.StartsWith("Header/", StringComparison.Ordinal) || property.Name.EndsWith("Url", StringComparison.Ordinal) || property.Name is "ProjectLink" or "Link" ? ResumeLimits.Contact : ResumeLimits.Scalar;
        long generation = _editorGeneration, activation = _editorActivationVersion;
        return new(path, Regex.Replace(property.Name, "(?<=[a-z])([A-Z])", " $1"), property.PropertyType, value, limit, narrative,
            newValue => generation == _editorGeneration && activation == Session.ActivationVersion
                ? CommitField(path, property.PropertyType, newValue) : ResumeEditorField.EditingUnavailable,
            () => CanEdit && generation == _editorGeneration && activation == Session.ActivationVersion);
    }
    private Action EditorAction(Action action)
    {
        long generation = _editorGeneration, activation = _editorActivationVersion;
        return () =>
        {
            if (!CanEdit || generation != _editorGeneration || activation != Session.ActivationVersion)
            { Status = ResumeEditorField.EditingUnavailable; return; }
            action();
        };
    }
    private string? CommitField(string path, Type type, object value)
    {
        if (!CanEdit) return ResumeEditorField.EditingUnavailable;
        try
        {
            JsonNode? node = type == typeof(string) ? JsonValue.Create((string)value) : type == typeof(bool) ? JsonValue.Create((bool)value)
                : type.IsEnum ? JsonValue.Create((int)value) : JsonValue.Create(double.Parse((string)value, NumberStyles.Float, CultureInfo.InvariantCulture));
            MutateDocument(root =>
            {
                string[] parts = path.Split('/'); JsonNode target = root;
                foreach (string part in parts[..^1]) target = target is JsonArray array ? array[int.Parse(part, CultureInfo.InvariantCulture)]! : target[part]!;
                target[parts[^1]] = node;
            });
            _invalidFields.Remove(path); Session.SetInvalidDraft(_invalidFields.Count != 0);
            return null;
        }
        catch (Exception ex) when (ex is InvalidDataException or JsonException or FormatException or OverflowException or InvalidOperationException)
        {
            // Replacement admission may win after the first availability check. Never turn
            // that rejection into an accepted invalid editor draft.
            try { Session.SetInvalidDraft(true); }
            catch (InvalidOperationException) { return ResumeEditorField.EditingUnavailable; }
            _invalidFields.Add(path);
            return "Invalid value or document limit exceeded. Correct this field before saving.";
        }
    }
    private void MutateDocument(Action<JsonObject> mutation)
    {
        var root = JsonSerializer.SerializeToNode(Session.Current?.Document ?? throw new InvalidOperationException("No active Resume"))!.AsObject();
        mutation(root);
        Session.Edit(root.Deserialize<ResumeDocument>() ?? throw new InvalidDataException("Invalid semantic edit"));
    }
    private void AddEntry(string collection, Type itemType) => StructureEdit(root =>
    {
        var array = root[collection]!.AsArray();
        int total = new[] { "Education", "Experiences", "SkillCategories", "Projects", "Certifications", "Achievements", "Responsibilities" }.Sum(name => root[name]!.AsArray().Count);
        if (array.Count >= ResumeLimits.EntriesPerCollection || total >= ResumeLimits.EntriesTotal) throw new InvalidDataException("Entry limit reached");
        array.Add(JsonSerializer.SerializeToNode(Activator.CreateInstance(itemType), itemType));
    });
    private void RemoveEntry(string collection, int index) => StructureEdit(root => root[collection]!.AsArray().RemoveAt(index));
    private void MoveEntry(string collection, int index, int direction) => StructureEdit(root =>
    {
        var array = root[collection]!.AsArray(); int next = index + direction;
        if (next < 0 || next >= array.Count) return;
        var node = array[index]; array.RemoveAt(index); array.Insert(next, node);
    });
    private void MoveSection(ResumeSection section, int direction) => StructureEdit(root =>
    {
        var array = root["SectionOrder"]!.AsArray(); int index = array.Select(node => node!.GetValue<int>()).ToList().IndexOf((int)section), next = index + direction;
        if (next < 0 || next >= array.Count) return;
        var node = array[index]; array.RemoveAt(index); array.Insert(next, node);
    });
    private void StructureEdit(Action<JsonObject> mutation)
    {
        if (!CanEdit) { Status = ResumeEditorField.EditingUnavailable; return; }
        if (_invalidFields.Count != 0) { Status = "Correct invalid fields before changing entries or order"; return; }
        try { MutateDocument(mutation); RebuildEditor(); }
        catch (InvalidDataException) { Status = "Entry or document safety limit reached"; }
        catch (InvalidOperationException) { Status = ResumeEditorField.EditingUnavailable; }
    }
}
public sealed record ResumeRecoveryEntry(string DocumentId, string Backup, string Label);
public sealed class ResumeEditorGroup(string title, string description, ResumeSection? section, Action? up, Action? down)
{
    public string Title { get; } = title;
    public string Description { get; } = description;
    public ResumeSection? Section { get; } = section;
    public ObservableCollection<ResumeEditorField> Fields { get; } = [];
    public ObservableCollection<ResumeEditorItem> Items { get; } = [];
    public ICommand? AddCommand { get; set; }
    public ICommand? UpCommand { get; } = up is null ? null : new RelayCommand(up);
    public ICommand? DownCommand { get; } = down is null ? null : new RelayCommand(down);
}
public sealed class ResumeEditorItem(int number, Action remove, Action up, Action down)
{
    public int Number { get; } = number;
    public ObservableCollection<ResumeEditorField> Fields { get; } = [];
    public ICommand RemoveCommand { get; } = new RelayCommand(remove);
    public ICommand UpCommand { get; } = new RelayCommand(up);
    public ICommand DownCommand { get; } = new RelayCommand(down);
}
public sealed class ResumeEditorField : ObservableObject
{
    internal const string EditingUnavailable = "Busy · Resume editing unavailable";
    private readonly Func<object, string?> _commit;
    private readonly Func<bool> _canEdit;
    private string _text, _error = "";
    private bool _boolean;
    private int _selected;
    public string Path { get; }
    public string Label { get; }
    public Type ValueType { get; }
    public int MaxLength { get; }
    public bool IsNarrative { get; }
    public IReadOnlyList<string> Choices { get; }
    public string Error { get => _error; private set => SetProperty(ref _error, value); }
    public string Text { get => _text; set { if (_text != value && Accept(value)) SetProperty(ref _text, value); } }
    public bool Boolean { get => _boolean; set { if (_boolean != value && Accept(value)) SetProperty(ref _boolean, value); } }
    public int Selected { get => _selected; set { if (value >= 0 && _selected != value && Accept(value)) SetProperty(ref _selected, value); } }
    private bool Accept(object value)
    {
        if (!_canEdit()) return false;
        string? error = _commit(value);
        if (error == EditingUnavailable) return false;
        Error = error ?? "";
        return true;
    }
    public ResumeEditorField(string path, string label, Type type, object value, int maxLength, bool narrative, Func<object, string?> commit, Func<bool> canEdit)
    {
        (Path, Label, ValueType, MaxLength, IsNarrative, _commit) = (path, label, type, maxLength, narrative, commit);
        _canEdit = canEdit;
        _text = Convert.ToString(value, CultureInfo.InvariantCulture) ?? "";
        _boolean = value is bool flag && flag;
        _selected = type.IsEnum ? Convert.ToInt32(value, CultureInfo.InvariantCulture) : -1;
        Choices = type.IsEnum ? Enum.GetNames(type) : [];
    }
}
