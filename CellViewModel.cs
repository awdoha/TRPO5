using System.Windows.Input; // нужен для типа ICommand (свойство ToggleCommand)

namespace Lab5.ViewModel; // пространство имён папки ViewModel

/// <summary>ViewModel одной клетки поля. Команда переключения общая для всех клеток (см. MainViewModel.ToggleCellCommand).</summary>
/// <remarks>
/// [MVVM] Клетка — отдельный класс, потому что ItemsControl (MainWindow.xaml:100) строит по одному Button на элемент коллекции,
/// а у каждой кнопки должно быть СВОЁ состояние IsAlive с уведомлением (INotifyPropertyChanged) — DataTrigger в App.xaml:113
/// перекрашивает именно эту кнопку. В голой модели bool[,] уведомлений нет.
/// Используется: создаётся в MainViewModel.RebuildCells (MainViewModel.cs:194), лежит в MainViewModel.Cells (MainViewModel.cs:60),
/// в XAML — DataTemplate DataType="{x:Type vm:CellViewModel}" (MainWindow.xaml:113).
/// </remarks>
public sealed class CellViewModel : ViewModelBase // наследуем SetField/PropertyChanged из ViewModelBase (ViewModelBase.cs:13)
{
    // Хранилище значения «жива ли клетка». Используется: get/set свойства IsAlive (ниже) и конструктор.
    private bool _isAlive;

    // Номер строки клетки. Только get — задаётся раз в конструкторе (свойство «только для чтения»).
    // Используется: MainViewModel.SyncCellsFromField (MainViewModel.cs:208), ToggleCell (строки 221, 223).
    public int Row { get; }
    // Номер столбца клетки, тоже неизменяемый.
    // Используется: MainViewModel.cs:208, MainViewModel.cs:221, MainViewModel.cs:223.
    public int Col { get; }

    // [Binding] Свойство «жива». Полная форма свойства: get читает поле, set пишет через SetField, который поднимает PropertyChanged.
    // Используется: DataTrigger Binding="{Binding IsAlive}" (App.xaml:113) — красит кнопку зелёным; MainViewModel.SyncCellsFromField (MainViewModel.cs:208),
    // ToggleCell (221), AliveCount (MainViewModel.cs:122).
    public bool IsAlive
    {
        get => _isAlive; // отдаём значение привязке
        set => SetField(ref _isAlive, value); // записываем и уведомляем UI, если значение изменилось (ViewModelBase.cs:31)
    }

    // [Команда] Команда клика по клетке. Это ТА ЖЕ команда, что и MainViewModel.ToggleCellCommand (общая на все клетки — экономия памяти).
    // Используется: Command="{Binding ToggleCommand}" на Button в MainWindow.xaml:120; значение передаётся в конструкторе из MainViewModel.cs:194.
    public ICommand ToggleCommand { get; }

    // Конструктор: вызывается из MainViewModel.RebuildCells (MainViewModel.cs:194) для каждой клетки поля.
    public CellViewModel(int row, int col, bool isAlive, ICommand toggleCommand)
    {
        Row = row; // запоминаем строку
        Col = col; // запоминаем столбец
        _isAlive = isAlive; // начальное состояние пишем в поле напрямую, без уведомления — UI ещё не подписан
        ToggleCommand = toggleCommand; // сохраняем общую команду переключения
    } // конец конструктора
} // конец класса CellViewModel
