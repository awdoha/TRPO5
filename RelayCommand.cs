using System.Windows.Input; // нужен для ICommand и CommandManager

namespace Lab5.ViewModel; // пространство имён папки ViewModel

/// <summary>
/// Стандартная реализация ICommand для MVVM. Уведомление о смене доступности
/// команды опирается на CommandManager.RequerySuggested (перепроверка при
/// действиях пользователя — клавиатура, мышь), что стандартно для WPF.
/// </summary>
/// <remarks>
/// [Команда] ICommand — интерфейс «команды»: Execute (выполнить), CanExecute (можно ли сейчас) и событие CanExecuteChanged
/// (доступность поменялась). Кнопка с Command="{Binding X}" сама вызывает CanExecute (и блокируется, если false) и Execute (при клике).
/// RelayCommand — «универсальная команда-обёртка»: ей отдают готовые методы (делегаты), а она их вызывает.
/// Используется: все 9 команд MainViewModel (MainViewModel.cs:163–179); кнопки в MainWindow.xaml (строки 52, 76–88, 120).
/// </remarks>
public sealed class RelayCommand : ICommand // sealed — от класса нельзя наследоваться; ICommand — реализуемый интерфейс (методы public — неявная реализация)
{
    // Делегат «что выполнить». Action<object?> — метод, принимающий один параметр (object?) и ничего не возвращающий.
    // Используется: заполняется в конструкторе (ниже), вызывается в Execute (RelayCommand.cs:59).
    private readonly Action<object?> _execute;
    // Делегат «можно ли выполнять». Func<object?, bool> — метод, принимающий параметр и возвращающий bool; ? — может быть null (тогда всегда можно).
    // Используется: заполняется в конструкторе, вызывается в CanExecute (RelayCommand.cs:55). Пример: () => !IsRunning из MainViewModel.cs:165.
    private readonly Func<object?, bool>? _canExecute;

    // Основной конструктор: принимает методы «выполнить» и (необязательно) «можно ли».
    // Используется: ToggleCellCommand (MainViewModel.cs:163, с параметром CellViewModel) и вторым конструктором (ниже).
    public RelayCommand(Action<object?> execute, Func<object?, bool>? canExecute = null)
    {
        // Защита: если метод не передан — бросаем исключение; ?? throw = «если null, то выбросить»
        _execute = execute ?? throw new ArgumentNullException(nameof(execute));
        _canExecute = canExecute; // запоминаем проверку доступности (может быть null)
    }

    /// <summary>Удобная перегрузка для команд без параметра.</summary>
    // Перегрузка для методов без параметров (Randomize, Start, Stop...). Используется: MainViewModel.cs:165–179 (RandomizeCommand, ClearCommand и т.д.).
    public RelayCommand(Action execute, Func<bool>? canExecute = null)
        // : this(...) — вызывает основной конструктор выше. Лямбда _ => execute() игнорирует параметр (_) и вызывает метод без аргументов;
        // для canExecute то же самое, а если он null — передаём null (команда всегда доступна, напр. ExportCommand, MainViewModel.cs:175).
        : this(_ => execute(), canExecute is null ? null : _ => canExecute())
    {
    } // тело пустое — всё уже сделал вызов this(...)

    // [Команда] Событие «доступность команды могла измениться». Кнопка на него подписывается и перезапрашивает CanExecute.
    // Здесь события «своего» нет: подписку/отписку (add/remove) перенаправляем на статический CommandManager.RequerySuggested —
    // WPF сам поднимает его при кликах, нажатиях клавиш, смене фокуса, поэтому кнопки автоматически обновляют состояние Enabled/Disabled.
    // Используется: движком WPF при установке Command="{Binding ...}" на Button (MainWindow.xaml).
    public event EventHandler? CanExecuteChanged
    {
        add => CommandManager.RequerySuggested += value; // кнопка подписалась -> подписываем её на глобальное «пересчитайте команды»
        remove => CommandManager.RequerySuggested -= value; // кнопка отписалась -> отписываем (чтобы не было утечки памяти)
    }

    // [Команда] «Можно ли выполнить сейчас?» Если проверки нет (null) — всегда true, иначе вызываем переданную функцию (например, !IsRunning).
    // Используется: WPF вызывает для каждой кнопки автоматически; если false — кнопка выключена (Opacity 0.4 через триггер App.xaml:79).
    public bool CanExecute(object? parameter) => _canExecute is null || _canExecute(parameter);

    // [Команда] Выполнить действие — WPF вызывает при клике по кнопке; просто передаёт управление делегату (Start, Stop, Advance, ...).
    // Используется: кнопки MainWindow.xaml (строки 52, 76–88) и клетки (строка 120, параметр = CellViewModel).
    public void Execute(object? parameter) => _execute(parameter);
} // конец класса RelayCommand
