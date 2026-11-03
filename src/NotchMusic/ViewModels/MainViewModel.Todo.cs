using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Input;
using System.Windows.Threading;
using NotchMusic.Helpers;
using NotchMusic.Models;
using NotchMusic.Services.Todo;

namespace NotchMusic.ViewModels;

public partial class MainViewModel
{
    private readonly ITodoService _todoService;

    private readonly ICollectionView _filteredTodos;

    private string _todoFilter = "All";

    private string _newTodoTitle = string.Empty;

    private DateTime? _newTodoDueDate;

    private bool _isSettingNewTodoDeadline;

    private DateTime? _customDeadlineDate = DateTime.Today;

    private int _customDeadlineHour = 17;

    private int _customDeadlineMinute = 0;

    private TodoNotificationEvent? _activeTodoNotification;

    private DispatcherTimer? _todoNotificationDismissTimer;

    private readonly DispatcherTimer _todoDeadlineTimer;

    // Ticker Fields for Minimized / Compact Mode
    private int _currentTickerIndex = 0;

    private string _currentTickerText = "Belum ada tugas";

    private string _currentTickerBadge = string.Empty;

    private TodoItemViewModel? _currentTickerItem;

    public string TodoNotchTooltip => HasPendingTodos
        ? $"To-Do List: {PendingTodosCount} tugas aktif\nKlik untuk buka daftar tugas"
        : "To-Do List TAMANOTCH (Klik untuk buka daftar tugas)";

    public bool IsTodoDetached
    {
        get => Settings.IsTodoDetached;
        set
        {
            if (Settings.IsTodoDetached != value)
            {
                Settings.IsTodoDetached = value;
                _settingsService.Save();
                RefreshComponentLayout();
            }
        }
    }

    public bool IsTodoWindowOpen
    {
        get => Settings.IsTodoWindowOpen;
        set
        {
            if (Settings.IsTodoWindowOpen != value)
            {
                Settings.IsTodoWindowOpen = value;
                _settingsService.Save();
                RefreshComponentLayout();
            }
        }
    }

    public bool IsTodoClosed => IsTodoDetached && !IsTodoWindowOpen;

    public bool IsTodoDetachedWindowVisible => IsTodoDetached && IsTodoWindowOpen;

    public bool IsComponentTodoOnly => ComponentDisplayMode == "TodoOnly";

    public bool ShowTodoSection => ComponentDisplayMode != "MusicOnly";

    public bool ShowTodoTicker => ShowTodoSection && (!IsTodoDetached || ComponentDisplayMode == "TodoOnly") && IsTodoWindowOpen;

    public bool ShowTodoSectionInMainWindow => ShowTodoSection && (!IsTodoDetached || ComponentDisplayMode == "TodoOnly") && IsTodoWindowOpen;

    public bool IsTodoWindowCompact
    {
        get => Settings.IsTodoWindowCompact;
        set
        {
            if (Settings.IsTodoWindowCompact != value)
            {
                Settings.IsTodoWindowCompact = value;
                _settingsService.Save();
                OnPropertyChanged(nameof(IsTodoWindowCompact));
                OnPropertyChanged(nameof(IsTodoWindowExpanded));
            }
        }
    }

    public bool IsTodoWindowExpanded => !IsTodoWindowCompact;

    public bool ShowCompactTodoOnlyView => IsCompactMode && !ShowMusicSection && ShowTodoSection;

    public System.Windows.GridLength TodoColumnWidth => ShowTodoSectionInMainWindow ? new System.Windows.GridLength(410) : new System.Windows.GridLength(0);

    // To-Do List Properties
    public ObservableCollection<TodoItemViewModel> Todos { get; } = new();

    public ICollectionView FilteredTodos => _filteredTodos;

    public string TodoFilter
    {
        get => _todoFilter;
        set
        {
            if (SetProperty(ref _todoFilter, value))
            {
                _filteredTodos.Refresh();
                OnPropertyChanged(nameof(IsAllFilterSelected));
                OnPropertyChanged(nameof(IsActiveFilterSelected));
                OnPropertyChanged(nameof(IsCompletedFilterSelected));
            }
        }
    }

    public bool IsAllFilterSelected => _todoFilter == "All";

    public bool IsActiveFilterSelected => _todoFilter == "Active";

    public bool IsCompletedFilterSelected => _todoFilter == "Completed";

    public ObservableCollection<string> AvailableCategories { get; } = new();

    public ObservableCollection<string> ExistingCategories { get; } = new();

    private string _newTodoCategory = string.Empty;

    public string NewTodoCategory
    {
        get => _newTodoCategory;
        set => SetProperty(ref _newTodoCategory, value);
    }

    private string _selectedCategoryFilter = "All";

    public string SelectedCategoryFilter
    {
        get => _selectedCategoryFilter;
        set
        {
            if (SetProperty(ref _selectedCategoryFilter, value))
            {
                _filteredTodos?.Refresh();
                OnPropertyChanged(nameof(IsAllCategorySelected));
            }
        }
    }

    public bool IsAllCategorySelected => string.Equals(_selectedCategoryFilter, "All", StringComparison.OrdinalIgnoreCase);

    public bool HasCategories => AvailableCategories.Count > 1;

    public string NewTodoTitle
    {
        get => _newTodoTitle;
        set => SetProperty(ref _newTodoTitle, value);
    }

    public DateTime? NewTodoDueDate
    {
        get => _newTodoDueDate;
        set
        {
            if (SetProperty(ref _newTodoDueDate, value))
            {
                OnPropertyChanged(nameof(HasNewTodoDueDate));
                OnPropertyChanged(nameof(NewTodoDueDateText));
            }
        }
    }

    public bool HasNewTodoDueDate => NewTodoDueDate.HasValue;

    public string NewTodoDueDateText
    {
        get
        {
            if (!NewTodoDueDate.HasValue) return string.Empty;
            var dt = NewTodoDueDate.Value;
            var today = DateTime.Today;
            if (dt.Date == today)
                return $"Hari ini {dt:HH':'mm}";
            if (dt.Date == today.AddDays(1))
                return $"Besok {dt:HH':'mm}";
            if (dt.Date == today.AddDays(-1))
                return $"Kemarin {dt:HH':'mm}";
            if (dt.Year == today.Year)
                return dt.ToString("d MMM HH':'mm");
            return dt.ToString("d MMM yyyy HH':'mm");
        }
    }

    public bool IsSettingNewTodoDeadline
    {
        get => _isSettingNewTodoDeadline;
        set => SetProperty(ref _isSettingNewTodoDeadline, value);
    }

    public DateTime? CustomDeadlineDate
    {
        get => _customDeadlineDate;
        set => SetProperty(ref _customDeadlineDate, value);
    }

    public int CustomDeadlineHour
    {
        get => _customDeadlineHour;
        set => SetProperty(ref _customDeadlineHour, value);
    }

    public int CustomDeadlineMinute
    {
        get => _customDeadlineMinute;
        set => SetProperty(ref _customDeadlineMinute, value);
    }

    public bool HasActiveTodoNotification => _activeTodoNotification != null;

    public TodoNotificationEvent? ActiveTodoNotification
    {
        get => _activeTodoNotification;
        private set
        {
            if (SetProperty(ref _activeTodoNotification, value))
            {
                OnPropertyChanged(nameof(HasActiveTodoNotification));
                if (value != null)
                {
                    _todoNotificationDismissTimer?.Stop();
                    _todoNotificationDismissTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(25) };
                    _todoNotificationDismissTimer.Tick += (s, e) =>
                    {
                        _todoNotificationDismissTimer.Stop();
                        ActiveTodoNotification = null;
                    };
                    _todoNotificationDismissTimer.Start();
                }
            }
        }
    }

    public int PendingTodosCount => Todos.Count(t => !t.IsCompleted);

    public int CompletedTodosCount => Todos.Count(t => t.IsCompleted);

    public int TotalTodosCount => Todos.Count;

    public bool HasCompletedTodos => CompletedTodosCount > 0;

    public bool HasPendingTodos => PendingTodosCount > 0;

    public string TodoSummaryText => TotalTodosCount == 0 ? "Belum ada tugas" : $"{CompletedTodosCount}/{TotalTodosCount} selesai";

    // Ticker Properties for Minimized / Standby Notch Mode
    public string CurrentTickerText => _currentTickerText;

    public string CurrentTickerBadge => _currentTickerBadge;

    public TodoItemViewModel? CurrentTickerItem => _currentTickerItem;

    public ICommand OpenTodoCommand { get; }

    public ICommand AddTodoCommand { get; }

    public ICommand ToggleSetDeadlineCommand { get; }

    public ICommand SetDeadlinePresetCommand { get; }

    public ICommand ApplyCustomDeadlineCommand { get; }

    public ICommand ClearNewTodoDueDateCommand { get; }

    public ICommand DismissTodoNotificationCommand { get; }

    public ICommand CompleteTodoNotificationCommand { get; }

    public ICommand ClearCompletedTodosCommand { get; }

    public ICommand SetTodoFilterCommand { get; }

    public ICommand SetCategoryFilterCommand { get; }

    public ICommand MoveCategoryUpCommand { get; }

    public ICommand MoveCategoryDownCommand { get; }

    public ICommand ExpandAllCategoriesCommand { get; }

    public ICommand CollapseAllCategoriesCommand { get; }

    public ICommand ToggleCategoryCollapseCommand { get; }

    public ICommand NextTickerCommand { get; }

    public ICommand PreviousTickerCommand { get; }

    public ICommand CompleteCurrentTickerCommand { get; }

    public ICommand DetachTodoCommand { get; }

    public ICommand AttachTodoCommand { get; }

    public ICommand CloseTodoCommand { get; }

    public ICommand MinimizeTodoWindowCommand { get; }

    public ICommand ExpandTodoWindowCommand { get; }

    private bool FilterTodoItem(object obj)
    {
        if (obj is not TodoItemViewModel item) return false;

        // 1. Status Filter
        bool statusMatch = _todoFilter switch
        {
            "Active" => !item.IsCompleted,
            "Completed" => item.IsCompleted,
            _ => true
        };
        if (!statusMatch) return false;

        // 2. Category Filter
        if (!string.Equals(SelectedCategoryFilter, "All", StringComparison.OrdinalIgnoreCase) &&
            !string.IsNullOrWhiteSpace(SelectedCategoryFilter))
        {
            if (!string.Equals(item.Category, SelectedCategoryFilter, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        return true;
    }

    public void UpdateAvailableCategories()
    {
        var cats = Todos
            .Where(t => t.HasCategory)
            .Select(t => t.Category)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(c => c)
            .ToList();

        var previousSelected = SelectedCategoryFilter;

        AvailableCategories.Clear();
        AvailableCategories.Add("All");
        foreach (var c in cats)
        {
            AvailableCategories.Add(c);
        }

        ExistingCategories.Clear();
        ExistingCategories.Add("(Tanpa Kategori)");
        foreach (var c in cats)
        {
            ExistingCategories.Add(c);
        }

        if (AvailableCategories.Contains(previousSelected, StringComparer.OrdinalIgnoreCase))
        {
            SelectedCategoryFilter = previousSelected;
        }
        else
        {
            SelectedCategoryFilter = "All";
        }

        OnPropertyChanged(nameof(HasCategories));
    }

    private void AddTodo()
    {
        if (string.IsNullOrWhiteSpace(NewTodoTitle)) return;

        var rawTitle = NewTodoTitle.Trim();
        string titleToSave;
        string categoryToSave = string.Empty;

        var (parsedCat, _) = TodoItem.ParseCategory(rawTitle);
        if (!string.IsNullOrEmpty(parsedCat))
        {
            categoryToSave = parsedCat;
            titleToSave = rawTitle;
        }
        else if (!string.IsNullOrWhiteSpace(NewTodoCategory) &&
                 !NewTodoCategory.Trim().Equals("(Tanpa Kategori)", StringComparison.OrdinalIgnoreCase))
        {
            categoryToSave = NewTodoCategory.Trim();
            titleToSave = $"{categoryToSave}: {rawTitle}";
        }
        else
        {
            titleToSave = rawTitle;
        }

        var item = new TodoItem
        {
            Title = titleToSave,
            Category = categoryToSave,
            CreatedAt = DateTime.Now,
            DueDate = NewTodoDueDate,
            IsDueNotified = false
        };

        var vm = new TodoItemViewModel(item);
        vm.OnChanged += OnTodoItemChanged;
        vm.OnDeleteRequested += DeleteTodo;

        Todos.Add(vm);
        NewTodoTitle = string.Empty;
        NewTodoDueDate = null;
        IsSettingNewTodoDeadline = false;

        UpdateTodoCountsAndPersist();
    }

    public void SetDeadlinePreset(string preset)
    {
        var now = DateTime.Now;
        DateTime target;
        switch (preset?.ToLowerInvariant())
        {
            case "plus_1h":
                target = now.AddHours(1);
                NewTodoDueDate = new DateTime(target.Year, target.Month, target.Day, target.Hour, (target.Minute / 5) * 5, 0);
                break;
            case "plus_3h":
                target = now.AddHours(3);
                NewTodoDueDate = new DateTime(target.Year, target.Month, target.Day, target.Hour, (target.Minute / 5) * 5, 0);
                break;
            case "today_17":
                NewTodoDueDate = new DateTime(now.Year, now.Month, now.Day, 17, 0, 0);
                break;
            case "today_21":
                NewTodoDueDate = new DateTime(now.Year, now.Month, now.Day, 21, 0, 0);
                break;
            case "tomorrow_09":
                target = now.AddDays(1);
                NewTodoDueDate = new DateTime(target.Year, target.Month, target.Day, 9, 0, 0);
                break;
            case "tomorrow_17":
                target = now.AddDays(1);
                NewTodoDueDate = new DateTime(target.Year, target.Month, target.Day, 17, 0, 0);
                break;
            default:
                break;
        }
        IsSettingNewTodoDeadline = false;
    }

    public void ApplyCustomDeadline()
    {
        var baseDate = CustomDeadlineDate ?? DateTime.Today;
        int hour = Math.Clamp(CustomDeadlineHour, 0, 23);
        int minute = Math.Clamp(CustomDeadlineMinute, 0, 59);
        NewTodoDueDate = new DateTime(baseDate.Year, baseDate.Month, baseDate.Day, hour, minute, 0);
        IsSettingNewTodoDeadline = false;
    }

    public void CheckTodoDeadlines()
    {
        var now = DateTime.Now;
        bool hasChanges = false;

        foreach (var vm in Todos)
        {
            if (vm.HasDueDate)
            {
                vm.RefreshDueStatus();
            }

            if (!vm.IsCompleted && vm.DueDate.HasValue && !vm.IsDueNotified && now >= vm.DueDate.Value)
            {
                vm.IsDueNotified = true;
                hasChanges = true;

                // 1. Play alert chime sound
                PrayerAudioHelper.PlayDeadlineAlert();

                // 2. Windows tray notification balloon
                RequestShowTrayNotification?.Invoke("⏰ Batas Waktu To-Do!", $"Tugas '{vm.TaskText}' telah mencapai batas waktu.");

                // 3. Notch Floating Island Banner
                ActiveTodoNotification = new TodoNotificationEvent
                {
                    TodoId = vm.Id,
                    Title = "⏰ Batas Waktu To-Do!",
                    Message = $"Tugas '{vm.TaskText}' telah mencapai batas waktu ({vm.DueDateText}).",
                    Timestamp = now
                };
            }
        }

        if (hasChanges)
        {
            UpdateTodoCountsAndPersist();
        }
    }

    private void DeleteTodo(TodoItemViewModel item)
    {
        item.OnChanged -= OnTodoItemChanged;
        item.OnDeleteRequested -= DeleteTodo;
        Todos.Remove(item);
        _todoService.MarkDeleted(item.Id);
        UpdateTodoCountsAndPersist();
    }

    private void ClearCompletedTodos()
    {
        var completed = Todos.Where(t => t.IsCompleted).ToList();
        if (completed.Count == 0) return;

        var ids = completed.Select(t => t.Id).ToList();
        foreach (var item in completed)
        {
            item.OnChanged -= OnTodoItemChanged;
            item.OnDeleteRequested -= DeleteTodo;
            Todos.Remove(item);
        }
        _todoService.MarkDeleted(ids);
        UpdateTodoCountsAndPersist();
    }

    private void OnTodoItemChanged()
    {
        UpdateTodoCountsAndPersist();
    }

    private void UpdateTodoCountsAndPersist()
    {
        OnPropertyChanged(nameof(PendingTodosCount));
        OnPropertyChanged(nameof(CompletedTodosCount));
        OnPropertyChanged(nameof(TotalTodosCount));
        OnPropertyChanged(nameof(HasCompletedTodos));
        OnPropertyChanged(nameof(HasPendingTodos));
        OnPropertyChanged(nameof(TodoSummaryText));
        UpdateAvailableCategories();
        _todoService.SaveTodos(Todos.Select(t => t.ToModel()));
        _filteredTodos?.Refresh();
        UpdateTicker();
        ScheduleAutoSync();
    }

    private void OnTodosSyncedFromCloud(List<TodoItem> syncedList)
    {
        Action apply = () =>
        {
            var currentIds = Todos.ToDictionary(t => t.Id);

            foreach (var item in syncedList)
            {
                if (currentIds.TryGetValue(item.Id, out var existing))
                {
                    existing.Title = item.Title;
                    existing.IsCompleted = item.IsCompleted;
                    existing.DueDate = item.DueDate;
                }
                else
                {
                    var vm = new TodoItemViewModel(item);
                    vm.OnChanged += OnTodoItemChanged;
                    vm.OnDeleteRequested += DeleteTodo;
                    Todos.Add(vm);
                }
            }

            var syncedIds = syncedList.Select(s => s.Id).ToHashSet();
            var toRemove = Todos.Where(t => !syncedIds.Contains(t.Id)).ToList();
            foreach (var r in toRemove)
            {
                r.OnChanged -= OnTodoItemChanged;
                r.OnDeleteRequested -= DeleteTodo;
                Todos.Remove(r);
            }

            _todoService.SaveTodos(Todos.Select(t => t.ToModel()));
            OnPropertyChanged(nameof(PendingTodosCount));
            OnPropertyChanged(nameof(CompletedTodosCount));
            OnPropertyChanged(nameof(TotalTodosCount));
            OnPropertyChanged(nameof(HasCompletedTodos));
            OnPropertyChanged(nameof(HasPendingTodos));
            OnPropertyChanged(nameof(TodoSummaryText));
            _filteredTodos?.Refresh();
            UpdateTicker();
        };

        if (App.Current?.Dispatcher != null && !App.Current.Dispatcher.CheckAccess())
        {
            App.Current.Dispatcher.Invoke(apply);
        }
        else
        {
            apply();
        }
    }

    public void NextTicker()
    {
        var pending = Todos.Where(t => !t.IsCompleted).ToList();
        if (pending.Count > 0)
        {
            _currentTickerIndex = (_currentTickerIndex + 1) % pending.Count;
            UpdateTicker();
        }
    }

    public void PreviousTicker()
    {
        var pending = Todos.Where(t => !t.IsCompleted).ToList();
        if (pending.Count > 0)
        {
            _currentTickerIndex = (_currentTickerIndex - 1 + pending.Count) % pending.Count;
            UpdateTicker();
        }
    }

    public void MoveTodo(TodoItemViewModel source, TodoItemViewModel target)
    {
        if (source == target) return;

        if (!string.Equals(source.CategoryGroup, target.CategoryGroup, StringComparison.OrdinalIgnoreCase))
        {
            source.Category = target.Category;
        }

        int oldIndex = Todos.IndexOf(source);
        int newIndex = Todos.IndexOf(target);

        if (oldIndex >= 0 && newIndex >= 0)
        {
            Todos.Move(oldIndex, newIndex);
            UpdateTodoCountsAndPersist();
        }
    }

    public void MoveCategoryUp(object? param)
    {
        string? categoryName = param as string ?? (param as System.Windows.Data.CollectionViewGroup)?.Name?.ToString() ?? param?.ToString();
        if (string.IsNullOrWhiteSpace(categoryName)) return;

        var order = GetCategoryOrder();
        int index = order.FindIndex(c => string.Equals(c, categoryName, StringComparison.OrdinalIgnoreCase));
        if (index <= 0) return;

        string prevCategory = order[index - 1];
        order[index - 1] = order[index];
        order[index] = prevCategory;

        ReorderTodosByCategories(order);
    }

public void ExpandAllCategories()
    {
        TodoCategoryExpansionHelper.ExpandAll();
        _filteredTodos?.Refresh();
    }

    public void CollapseAllCategories()
    {
        var cats = AvailableCategories.Where(c => !string.Equals(c, "All", StringComparison.OrdinalIgnoreCase));
        TodoCategoryExpansionHelper.CollapseAll(cats);
        _filteredTodos?.Refresh();
    }

    public void ToggleCategoryCollapse(object? param)
    {
        string? categoryName = param as string ?? (param as System.Windows.Data.CollectionViewGroup)?.Name?.ToString() ?? param?.ToString();
        if (string.IsNullOrWhiteSpace(categoryName)) return;
        TodoCategoryExpansionHelper.Toggle(categoryName);
        _filteredTodos?.Refresh();
    }

    public void MoveCategoryDown(object? param)
    {
        string? categoryName = param as string ?? (param as System.Windows.Data.CollectionViewGroup)?.Name?.ToString() ?? param?.ToString();
        if (string.IsNullOrWhiteSpace(categoryName)) return;

        var order = GetCategoryOrder();
        int index = order.FindIndex(c => string.Equals(c, categoryName, StringComparison.OrdinalIgnoreCase));
        if (index < 0 || index >= order.Count - 1) return;

        string nextCategory = order[index + 1];
        order[index + 1] = order[index];
        order[index] = nextCategory;

        ReorderTodosByCategories(order);
    }

    public void MoveCategory(string sourceCategory, string targetCategory)
    {
        if (string.IsNullOrWhiteSpace(sourceCategory) || string.IsNullOrWhiteSpace(targetCategory)) return;
        if (string.Equals(sourceCategory, targetCategory, StringComparison.OrdinalIgnoreCase)) return;

        var order = GetCategoryOrder();
        int sourceIndex = order.FindIndex(c => string.Equals(c, sourceCategory, StringComparison.OrdinalIgnoreCase));
        int targetIndex = order.FindIndex(c => string.Equals(c, targetCategory, StringComparison.OrdinalIgnoreCase));

        if (sourceIndex < 0 || targetIndex < 0) return;

        order.RemoveAt(sourceIndex);
        order.Insert(targetIndex, sourceCategory);

        ReorderTodosByCategories(order);
    }

    public List<string> GetCategoryOrder()
    {
        var order = new List<string>();
        foreach (var item in Todos)
        {
            string group = item.CategoryGroup;
            if (!order.Any(c => string.Equals(c, group, StringComparison.OrdinalIgnoreCase)))
            {
                order.Add(group);
            }
        }
        return order;
    }

    public void ReorderTodosByCategories(List<string> categoryOrder)
    {
        var reordered = new List<TodoItemViewModel>();

        foreach (var cat in categoryOrder)
        {
            var itemsInCat = Todos
                .Where(t => string.Equals(t.CategoryGroup, cat, StringComparison.OrdinalIgnoreCase))
                .ToList();
            reordered.AddRange(itemsInCat);
        }

        foreach (var item in Todos)
        {
            if (!reordered.Contains(item))
            {
                reordered.Add(item);
            }
        }

        for (int targetIdx = 0; targetIdx < reordered.Count; targetIdx++)
        {
            var targetItem = reordered[targetIdx];
            int currentIdx = Todos.IndexOf(targetItem);
            if (currentIdx >= 0 && currentIdx != targetIdx)
            {
                Todos.Move(currentIdx, targetIdx);
            }
        }

        UpdateTodoCountsAndPersist();
    }

    private void CompleteCurrentTicker()
    {
        var firstPending = Todos.FirstOrDefault(t => !t.IsCompleted);
        if (firstPending != null)
        {
            firstPending.IsCompleted = true;
        }
    }

    public void UpdateTicker()
    {
        var pending = Todos.Where(t => !t.IsCompleted).ToList();
        if (pending.Count == 0)
        {
            _currentTickerItem = null;
            _currentTickerIndex = 0;
            if (Todos.Count > 0)
            {
                _currentTickerText = "Semua tugas selesai 🎉";
                _currentTickerBadge = "✓";
            }
            else
            {
                _currentTickerText = "Belum ada tugas";
                _currentTickerBadge = "+";
            }
        }
        else
        {
            if (_currentTickerIndex >= pending.Count || _currentTickerIndex < 0)
            {
                _currentTickerIndex = 0;
            }

            _currentTickerItem = pending[_currentTickerIndex];
            _currentTickerBadge = $"{pending.Count}";

            // Aliran teks berjalan bersambung dari kanan ke kiri lintas kategori
            var parts = pending.Select(t => t.HasCategory ? $"[{t.Category}] {t.TaskText}" : t.Title);
            _currentTickerText = string.Join("     •     ", parts);
        }

        OnPropertyChanged(nameof(CurrentTickerText));
        OnPropertyChanged(nameof(CurrentTickerBadge));
        OnPropertyChanged(nameof(CurrentTickerItem));
        OnPropertyChanged(nameof(HasPendingTodos));
    }
}
