using System.Collections.ObjectModel; // ObservableCollection<T> — коллекция, которая сама сообщает UI о добавлении/удалении элементов
using System.IO; // Path, IOException, InvalidDataException (работа с именами файлов и ошибками ввода-вывода)
using System.Linq; // метод Count(предикат) для AliveCount (LINQ)
using System.Text.Json; // JsonException — исключение при битом JSON (ловится в Import)
using System.Windows; // MessageBox, MessageBoxButton, MessageBoxImage — окна с сообщением об ошибке
using System.Windows.Input; // ICommand — тип команд для привязки Command="{Binding ...}"
using System.Windows.Threading; // DispatcherTimer — таймер, тикающий в UI-потоке
using Lab5.Model; // GameField — модель поля (Model/GameField.cs)
using Lab5.Services; // FieldJsonService — импорт/экспорт JSON (Services/FieldJsonService.cs)
using Microsoft.Win32; // SaveFileDialog / OpenFileDialog — стандартные диалоги Windows выбора файла

namespace Lab5.ViewModel; // пространство имён папки ViewModel (в XAML подключено как xmlns:vm)

/// <summary>Главная ViewModel приложения "Игра Жизнь". Связывает модель GameField с View через биндинги и команды.</summary>
/// <remarks>
/// [MVVM] Это ViewModel — «посредник» между View (MainWindow.xaml) и Model (GameField). View про модель ничего не знает и
/// видит только публичные свойства (Rows, Cols, Density, ...) и команды (StartCommand, ...) этого класса через {Binding}.
/// Используется: создаётся один раз в MainWindow.xaml.cs (MainWindow.xaml.cs:22) и кладётся в DataContext окна.
/// </remarks>
public sealed class MainViewModel : ViewModelBase // наследует SetField/OnPropertyChanged (уведомление UI об изменениях)
{
    // Минимальный размер поля (строк/столбцов). Используется: Clamp в Rows (MainViewModel.cs:67) и Cols (MainViewModel.cs:75) — ниже 5 значение не опустится.
    private const int MinFieldSize = 5;
    // Максимальный размер поля: больше 60 — сотни Button в ItemsControl тормозят. Используется: те же Clamp в Rows/Cols.
    private const int MaxFieldSize = 60;
    // Минимальный интервал таймера, мс. Используется: Clamp в IntervalMs (MainViewModel.cs:93).
    private const int MinIntervalMs = 50;
    // Максимальный интервал таймера, мс. Используется: тот же Clamp (совпадает с Maximum="2000" у Slider, MainWindow.xaml:67).
    private const int MaxIntervalMs = 2000;

    // [Таймер] DispatcherTimer — таймер WPF: событие Tick приходит в UI-потоке, поэтому из него можно менять свойства, к которым привязан экран.
    // Используется: создаётся в конструкторе (MainViewModel.cs:157), запускается в Start (MainViewModel.cs:267), останавливается в Stop (MainViewModel.cs:274), интервал — в IntervalMs и Start.
    private readonly DispatcherTimer _timer;
    // Сервис экспорта/импорта JSON. new() — короткая форма создания объекта того же типа. Используется: Export (MainViewModel.cs:294) и Import (MainViewModel.cs:318).
    private readonly FieldJsonService _jsonService = new();
    // Генератор случайных чисел (один на всё приложение). Используется: Randomize (MainViewModel.cs:229) передаёт его в GameField.Randomize.
    private readonly Random _random = new();

    // Модель поля (Model). Не readonly — при импорте подменяется целиком. Используется: конструктор, RebuildCells, SyncCellsFromField, ToggleCell, Randomize, ClearField, ResizeField, Advance, Export, Import.
    private GameField _field;

    // Поле-хранилище для Rows (число строк, по умолчанию 20). Используется: свойство Rows, создание модели (MainViewModel.cs:155), Import.
    private int _rows = 20;
    // Поле-хранилище для Cols (число столбцов, по умолчанию 20). Используется: свойство Cols, конструктор, Import.
    private int _cols = 20;
    // Поле-хранилище для Density (доля живых клеток при случайном заполнении, 0.3 = 30%). Используется: свойство Density.
    private double _density = 0.3;
    // Поле-хранилище для IntervalMs (пауза между поколениями, мс). Используется: свойство IntervalMs.
    private int _intervalMs = 300;
    // Поле-хранилище для Generation (номер поколения). Используется: свойство Generation.
    private int _generation;
    // Поле-хранилище для IsRunning (идёт ли симуляция). Используется: свойство IsRunning.
    private bool _isRunning;
    // Поле-хранилище для StatusMessage (сообщение в строке состояния). Стартовый текст виден пользователю сразу после запуска. Используется: свойство StatusMessage.
    private string _statusMessage = "Готово. Задайте начальное состояние и запустите симуляцию.";

    // [Binding] [MVVM] ObservableCollection — коллекция, которая при Add/Clear сама уведомляет (INotifyCollectionChanged), и ItemsControl перерисовывает клетки.
    // Обычный List такого не умеет. Только get — саму коллекцию не подменяем, а очищаем и наполняем (RebuildCells).
    // Используется: ItemsSource="{Binding Cells}" (MainWindow.xaml:100); наполняется в RebuildCells; читается в SyncCellsFromField и AliveCount.
    public ObservableCollection<CellViewModel> Cells { get; } = new();

    // [Binding] Число строк поля. Полная форма свойства: get отдаёт поле, set пишет через SetField с зажатием в диапазон Clamp(5..60).
    // Используется: TextBox Text="{Binding Rows, ...}" (MainWindow.xaml:42), UniformGrid Rows="{Binding Rows}" (MainWindow.xaml:107), ResizeField (MainViewModel.cs:247), Import.
    public int Rows
    {
        get => _rows; // привязка читает значение
        set => SetField(ref _rows, Math.Clamp(value, MinFieldSize, MaxFieldSize)); // Math.Clamp обрезает значение до [5;60], SetField записывает и шлёт PropertyChanged
    }

    // [Binding] Число столбцов поля, устроено так же, как Rows.
    // Используется: TextBox Text="{Binding Cols, ...}" (MainWindow.xaml:47), UniformGrid Columns="{Binding Cols}" (MainWindow.xaml:107), ResizeField, Import.
    public int Cols
    {
        get => _cols; // привязка читает значение
        set => SetField(ref _cols, Math.Clamp(value, MinFieldSize, MaxFieldSize)); // обрезаем до [5;60] и уведомляем UI
    }

    // [Binding] Плотность случайного заполнения 0..1.
    // Используется: Slider Value="{Binding Density}" (MainWindow.xaml:58), подпись с процентами (MainWindow.xaml:61), Randomize (MainViewModel.cs:229).
    public double Density
    {
        get => _density; // ползунок читает текущее значение
        set => SetField(ref _density, Math.Clamp(value, 0d, 1d)); // ползунок пишет сюда при перетаскивании; 0d/1d — числа типа double
    }

    // [Binding] [Таймер] Интервал между поколениями в мс (скорость симуляции).
    // Используется: Slider Value="{Binding IntervalMs}" (MainWindow.xaml:67) и подпись справа (MainWindow.xaml:69); Start (MainViewModel.cs:266).
    public int IntervalMs
    {
        get => _intervalMs; // ползунок читает значение
        set
        {
            int clamped = Math.Clamp(value, MinIntervalMs, MaxIntervalMs); // ограничиваем диапазоном 50..2000
            // SetField вернёт true, только если значение реально изменилось; && IsRunning — и симуляция сейчас идёт
            if (SetField(ref _intervalMs, clamped) && IsRunning)
            {
                // Если симуляция уже запущена — новая скорость применяется немедленно.
                _timer.Interval = TimeSpan.FromMilliseconds(_intervalMs); // TimeSpan — промежуток времени; таймер продолжает идти уже с новым интервалом
            }
        }
    }

    // [Binding] Номер текущего поколения. private set — менять может только сама ViewModel, View лишь читает.
    // Используется: в строке состояния Run Text="{Binding Generation, Mode=OneWay}" (MainWindow.xaml:141); меняется в Randomize, ClearField, ResizeField, Advance (Generation++), Import.
    public int Generation
    {
        get => _generation; // отдаём значение привязке
        private set => SetField(ref _generation, value); // пишем и уведомляем UI
    }

    // [Binding] Флаг «симуляция идёт». private set — включается только в Start, выключается в Stop.
    // Используется: во всех CanExecute команд (MainViewModel.cs:163–179): !IsRunning блокирует правку при работе, IsRunning включает «Стоп»; в сеттере IntervalMs.
    public bool IsRunning
    {
        get => _isRunning; // читают лямбды CanExecute
        private set => SetField(ref _isRunning, value); // пишем и уведомляем (кнопки перепроверятся через RequerySuggested)
    }

    // [Binding] Число живых клеток — вычисляемое свойство (нет поля и set): каждый раз считаем LINQ-запросом Count по коллекции клеток.
    // Уведомление о его изменении шлём вручную: OnPropertyChanged(nameof(AliveCount)) в RebuildCells, SyncCellsFromField, ToggleCell.
    // Используется: в строке состояния Run Text="{Binding AliveCount, Mode=OneWay}" (MainWindow.xaml:136).
    public int AliveCount => Cells.Count(c => c.IsAlive); // лямбда c => c.IsAlive — «считай только те клетки, у которых IsAlive == true»

    // [Binding] Текст сообщения внизу окна. private set — пишется только изнутри при каждой операции.
    // Используется: TextBlock Text="{Binding StatusMessage}" (MainWindow.xaml:144); присваивается в ToggleCell, Randomize, ClearField, ResizeField, Advance, Start, Stop, Export, Import.
    public string StatusMessage
    {
        get => _statusMessage; // читает привязка
        private set => SetField(ref _statusMessage, value); // пишем и уведомляем UI — текст внизу окна обновляется сам
    }

    // [Команда] Ниже — 9 команд. Тип ICommand (интерфейс), объекты внутри — RelayCommand. Свойства только get: присваиваются один раз в конструкторе.
    // Клик по клетке. Используется: передаётся в каждый CellViewModel (MainViewModel.cs:194), кнопка клетки Command="{Binding ToggleCommand}" (MainWindow.xaml:120).
    public ICommand ToggleCellCommand { get; }
    // «Случайное заполнение». Используется: Command="{Binding RandomizeCommand}" (MainWindow.xaml:76); метод Randomize.
    public ICommand RandomizeCommand { get; }
    // «Очистить». Используется: Command="{Binding ClearCommand}" (MainWindow.xaml:78); метод ClearField.
    public ICommand ClearCommand { get; }
    // «Шаг» — одно поколение. Используется: Command="{Binding StepCommand}" (MainWindow.xaml:80); метод Advance.
    public ICommand StepCommand { get; }
    // «Старт» — непрерывная симуляция. Используется: Command="{Binding StartCommand}" (MainWindow.xaml:82); метод Start.
    public ICommand StartCommand { get; }
    // «Стоп». Используется: Command="{Binding StopCommand}" (MainWindow.xaml:84); метод Stop.
    public ICommand StopCommand { get; }
    // «Экспорт в JSON». Используется: Command="{Binding ExportCommand}" (MainWindow.xaml:86); метод Export.
    public ICommand ExportCommand { get; }
    // «Импорт из JSON». Используется: Command="{Binding ImportCommand}" (MainWindow.xaml:88); метод Import.
    public ICommand ImportCommand { get; }
    // «Пересоздать поле» под новые Rows/Cols. Используется: Command="{Binding ResizeFieldCommand}" (MainWindow.xaml:52); метод ResizeField.
    public ICommand ResizeFieldCommand { get; }

    // Конструктор: создаёт модель, таймер и команды, строит клетки. Используется: MainWindow.xaml.cs (MainWindow.xaml.cs:22) — new MainViewModel().
    public MainViewModel()
    {
        _field = new GameField(_rows, _cols); // создаём модель 20x20 (Model/GameField.cs), все клетки мёртвые

        _timer = new DispatcherTimer(); // создаём таймер (пока не запущен; интервал по умолчанию 0)
        // [Таймер] += подписывает обработчик на событие Tick (делегат). Лямбда (_, _) => Advance() игнорирует sender и e и на каждый тик делает шаг поколения.
        _timer.Tick += (_, _) => Advance();

        // [Команда] param => ToggleCell(param as CellViewModel): param — CommandParameter из XAML (сама клетка), «as» приводит тип (null, если не клетка);
        // второй аргумент _ => !IsRunning — CanExecute: клик по клетке разрешён, только пока симуляция НЕ идёт.
        ToggleCellCommand = new RelayCommand(param => ToggleCell(param as CellViewModel), _ => !IsRunning);
        // [Команда] Randomize — метод (method group) без параметров; () => !IsRunning — CanExecute: кнопка активна, когда симуляция остановлена.
        RandomizeCommand = new RelayCommand(Randomize, () => !IsRunning);
        // [Команда] «Очистить» доступна только при остановленной симуляции.
        ClearCommand = new RelayCommand(ClearField, () => !IsRunning);
        // [Команда] «Шаг» вызывает Advance; доступен только при остановленной симуляции (иначе шаги конфликтовали бы с таймером).
        StepCommand = new RelayCommand(Advance, () => !IsRunning);
        // [Команда] «Старт» активен, только пока симуляция не идёт (нельзя запустить дважды).
        StartCommand = new RelayCommand(Start, () => !IsRunning);
        // [Команда] «Стоп» — наоборот: активен, только пока IsRunning == true.
        StopCommand = new RelayCommand(Stop, () => IsRunning);
        // [Команда] «Экспорт» — второй аргумент не задан, значит CanExecute всегда true: сохранять можно и во время работы.
        ExportCommand = new RelayCommand(Export);
        // [Команда] «Импорт» меняет поле, поэтому только при остановленной симуляции.
        ImportCommand = new RelayCommand(Import, () => !IsRunning);
        // [Команда] «Пересоздать поле» — только при остановленной симуляции.
        ResizeFieldCommand = new RelayCommand(ResizeField, () => !IsRunning);

        RebuildCells(); // наполняем Cells клетками 20x20 — их сразу увидит ItemsControl
    } // конец конструктора

    /// <summary>Полностью пересоздаёт коллекцию клеток из текущего состояния модели (при изменении размеров/импорте).</summary>
    // Используется: конструктор (MainViewModel.cs:181), ResizeField (MainViewModel.cs:248), Import (MainViewModel.cs:324).
    private void RebuildCells()
    {
        Cells.Clear(); // очищаем коллекцию — ItemsControl убирает все кнопки
        for (int r = 0; r < _field.Rows; r++) // цикл по строкам модели
        {
            for (int c = 0; c < _field.Cols; c++) // вложенный цикл по столбцам
            {
                // создаём ViewModel клетки: координаты, текущее состояние из модели (индексатор GameField[r, c]) и ОБЩУЮ команду переключения; Add -> ItemsControl добавляет кнопку
                Cells.Add(new CellViewModel(r, c, _field[r, c], ToggleCellCommand));
            }
        }

        OnPropertyChanged(nameof(AliveCount)); // сообщаем UI: «AliveCount поменялся, перечитай» (вычисляемое свойство само не уведомляет)
    } // конец RebuildCells

    /// <summary>Обновляет состояние существующих клеток без пересоздания коллекции (шаг, randomize, clear).</summary>
    // Используется: Randomize, ClearField, Advance — после того как модель GameField изменилась, переносим её состояние в CellViewModel.
    private void SyncCellsFromField()
    {
        foreach (CellViewModel cell in Cells) // перебираем все клетки-ViewModel
        {
            // берём значение из модели по координатам клетки; set IsAlive шлёт PropertyChanged -> DataTrigger (App.xaml) перекрашивает кнопку
            cell.IsAlive = _field[cell.Row, cell.Col];
        }

        OnPropertyChanged(nameof(AliveCount)); // обновляем счётчик «Живых клеток» внизу
    } // конец SyncCellsFromField

    // [Команда] Обработчик клика по клетке (Execute для ToggleCellCommand). Параметр приходит из CommandParameter="{Binding}" (MainWindow.xaml:121).
    // Используется: лямбда в конструкторе (MainViewModel.cs:163).
    private void ToggleCell(CellViewModel? cell)
    {
        if (cell is null) return; // защита: если параметр не клетка — ничего не делаем

        cell.IsAlive = !cell.IsAlive; // инвертируем состояние клетки; UI обновится сам (PropertyChanged)
        _field[cell.Row, cell.Col] = cell.IsAlive; // дублируем изменение в модель GameField (индексатор set), иначе шаг не увидит правку
        OnPropertyChanged(nameof(AliveCount)); // пересчёт счётчика живых
        StatusMessage = $"Клетка ({cell.Row}; {cell.Col}) переключена вручную"; // $"..." — интерполяция строк; текст появляется в строке состояния
    } // конец ToggleCell

    // Execute «Случайное заполнение». Используется: RandomizeCommand (MainViewModel.cs:165).
    private void Randomize()
    {
        _field.Randomize(Density, _random); // модель заполняется случайно с долей Density (значение ползунка «Плотность»)
        SyncCellsFromField(); // переносим состояние модели на экран
        Generation = 0; // новый старт — счётчик поколений обнуляем
        StatusMessage = $"Случайное заполнение с плотностью {Density:P0}"; // :P0 — формат процентов без дробной части (0.3 -> 30 %)
    } // конец Randomize

    // Execute «Очистить». Используется: ClearCommand (MainViewModel.cs:167).
    private void ClearField()
    {
        _field.Clear(); // модель: все клетки мёртвые
        SyncCellsFromField(); // отражаем на экране
        Generation = 0; // сбрасываем поколения
        StatusMessage = "Поле очищено"; // сообщение в строке состояния
    } // конец ClearField

    // Execute «Пересоздать поле». Используется: ResizeFieldCommand (MainViewModel.cs:179).
    private void ResizeField()
    {
        _field.Resize(Rows, Cols); // модель пересоздаёт массив под размеры из TextBox (уже зажатые в 5..60)
        RebuildCells(); // число клеток изменилось — строим коллекцию заново (UniformGrid подстроится под Rows/Cols)
        Generation = 0; // сбрасываем поколения
        StatusMessage = $"Поле пересоздано: {Rows} x {Cols}"; // сообщение с новыми размерами
    } // конец ResizeField

    // Execute «Шаг» и обработчик каждого Tick таймера в режиме «Старт». Используется: StepCommand (MainViewModel.cs:169) и _timer.Tick (MainViewModel.cs:159).
    private void Advance()
    {
        _field.NextGeneration(); // модель считает следующее поколение по правилам Конвея (GameField.NextGeneration)
        SyncCellsFromField(); // переносим результат на экран
        Generation++; // увеличиваем номер поколения; сеттер шлёт PropertyChanged -> «Поколение: N» внизу окна
        StatusMessage = $"Поколение {Generation}"; // сообщение в строке состояния
    } // конец Advance

    // Execute «Старт». Используется: StartCommand (MainViewModel.cs:171).
    private void Start()
    {
        IsRunning = true; // включаем режим работы: CanExecute у всех команд, кроме «Стоп» и «Экспорт», станет false -> кнопки серые
        _timer.Interval = TimeSpan.FromMilliseconds(IntervalMs); // задаём интервал из ползунка «Интервал (мс)»
        _timer.Start(); // запускаем таймер — начнут приходить Tick -> Advance
        StatusMessage = "Симуляция запущена"; // сообщение пользователю
    } // конец Start

    // Execute «Стоп». Используется: StopCommand (MainViewModel.cs:173).
    private void Stop()
    {
        _timer.Stop(); // останавливаем таймер — Tick больше не приходят
        IsRunning = false; // выключаем режим работы: кнопки редактирования снова доступны
        StatusMessage = "Симуляция остановлена"; // сообщение пользователю
    } // конец Stop

    // [JSON] Execute «Экспорт в JSON»: спросить у пользователя файл и записать поле. Используется: ExportCommand (MainViewModel.cs:175).
    private void Export()
    {
        // [JSON] SaveFileDialog — стандартное окно Windows «Сохранить как». Инициализатор объекта { ... } задаёт свойства при создании.
        var dialog = new SaveFileDialog
        {
            Filter = "Файлы JSON (*.json)|*.json|Все файлы (*.*)|*.*", // фильтр списка типов: «подпись|маска|подпись|маска»
            FileName = "life-field.json" // предлагаемое имя файла по умолчанию
        };

        // ShowDialog() показывает окно и возвращает true, если нажали «Сохранить»; иначе (отмена) выходим без действий
        if (dialog.ShowDialog() != true) return;

        try // пробуем записать; ошибки диска ловим ниже, чтобы приложение не падало
        {
            _jsonService.Export(_field, dialog.FileName); // сервис сериализует модель в JSON и пишет в выбранный файл (Services/FieldJsonService.cs)
            StatusMessage = $"Поле сохранено в файл {Path.GetFileName(dialog.FileName)}"; // Path.GetFileName берёт из полного пути только имя файла
        }
        // when (...) — фильтр исключений: ловим только ошибки ввода-вывода и «нет доступа»; is X or Y — проверка типа исключения
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // показываем пользователю окно с ошибкой (кнопка OK, значок ошибки)
            MessageBox.Show($"Не удалось сохранить файл: {ex.Message}", "Ошибка экспорта", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    } // конец Export

    // [JSON] Execute «Импорт из JSON»: выбрать файл, прочитать поле, перестроить экран. Используется: ImportCommand (MainViewModel.cs:177).
    private void Import()
    {
        // [JSON] OpenFileDialog — стандартное окно Windows «Открыть файл»
        var dialog = new OpenFileDialog
        {
            Filter = "Файлы JSON (*.json)|*.json|Все файлы (*.*)|*.*" // показываем только *.json (или все файлы)
        };

        if (dialog.ShowDialog() != true) return; // пользователь нажал «Отмена» — выходим

        try // читаем и разбираем файл; ошибки ловим ниже
        {
            GameField imported = _jsonService.Import(dialog.FileName); // сервис читает JSON, проверяет данные и возвращает готовую модель
            _field = imported; // подменяем текущую модель загруженной
            _rows = imported.Rows; // размеры пишем прямо в поля (минуя сеттер), чтобы не срабатывал Clamp
            _cols = imported.Cols; // то же для столбцов
            OnPropertyChanged(nameof(Rows)); // сообщаем UI: TextBox «Строк» и UniformGrid должны перечитать Rows
            OnPropertyChanged(nameof(Cols)); // то же для «Столбцов»
            RebuildCells(); // строим клетки под новое поле
            Generation = 0; // сбрасываем поколения
            StatusMessage = $"Поле загружено из файла {Path.GetFileName(dialog.FileName)}"; // сообщение с именем файла
        }
        // Ловим ошибки файла, неверные данные (InvalidDataException из сервиса) и битый JSON (JsonException) — покажем окно вместо падения программы
        catch (Exception ex) when (ex is IOException or InvalidDataException or JsonException or UnauthorizedAccessException)
        {
            // окно с текстом ошибки
            MessageBox.Show($"Не удалось загрузить файл: {ex.Message}", "Ошибка импорта", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    } // конец Import
} // конец класса MainViewModel
