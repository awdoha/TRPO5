namespace Lab5.Model; // пространство имён папки Model (слой Model в MVVM: чистая логика без WPF)

/// <summary>
/// Модель игрового поля "Жизнь". Хранит состояние клеток и реализует
/// классические правила Конвея. Не зависит от UI — вся логика чистая.
/// Соседи считаются по 8-связности, клетки за пределами поля считаются
/// мёртвыми (края поля неактивны, без "тора").
/// </summary>
/// <remarks>
/// [MVVM] Model: ничего не знает про окна, кнопки и привязки, поэтому её можно проверить в консоли или тестами.
/// Используется: единственный экземпляр хранится в MainViewModel._field (MainViewModel.cs:40), создаётся в конструкторе MainViewModel (MainViewModel.cs:155),
/// а при импорте создаётся заново в FieldJsonService.Import (FieldJsonService.cs:61). Пользователь видит модель косвенно — через CellViewModel на экране.
/// </remarks>
public sealed class GameField // sealed — наследовать нельзя
{
    // Двумерный массив bool[,] — «таблица» из Rows строк и Cols столбцов; true = клетка жива. Доступ: _cells[строка, столбец] (запятая внутри одних скобок — это НЕ массив массивов).
    // Используется: во всех методах этого класса; снаружи скрыт — доступ только через индексатор this[,] и методы.
    private bool[,] _cells;

    // Число строк. private set — менять может только сам класс (в конструкторе и Resize); снаружи только читать (авто-свойство).
    // Используется: MainViewModel.RebuildCells (MainViewModel.cs:189), Import (MainViewModel.cs:322), FieldJsonService.Export (FieldJsonService.cs:38).
    public int Rows { get; private set; }
    // Число столбцов, устроено так же. Используется: RebuildCells, Import, FieldJsonService.Export.
    public int Cols { get; private set; }

    // Конструктор: создаёт пустое поле rows x cols. Используется: MainViewModel (MainViewModel.cs:155) и FieldJsonService.Import (FieldJsonService.cs:61).
    public GameField(int rows, int cols)
    {
        // Защита от некорректных размеров: ArgumentOutOfRangeException — «аргумент вне допустимого диапазона»; nameof(rows) подставляет имя параметра строкой
        if (rows <= 0) throw new ArgumentOutOfRangeException(nameof(rows));
        if (cols <= 0) throw new ArgumentOutOfRangeException(nameof(cols));

        Rows = rows; // запоминаем число строк
        Cols = cols; // запоминаем число столбцов
        _cells = new bool[rows, cols]; // выделяем массив; все элементы по умолчанию false (мёртвые)
    } // конец конструктора

    // Индексатор: позволяет писать field[r, c] вместо вызова метода. get читает клетку, set записывает.
    // Используется: MainViewModel.RebuildCells (MainViewModel.cs:194 — чтение), SyncCellsFromField (MainViewModel.cs:208 — чтение), ToggleCell (MainViewModel.cs:221 — запись).
    public bool this[int row, int col]
    {
        get => _cells[row, col]; // прочитать клетку
        set => _cells[row, col] = value; // записать клетку (ручной клик пользователя попадает сюда)
    }

    // Очистка: создаём новый пустой массив тех же размеров (все false). Используется: MainViewModel.ClearField (MainViewModel.cs:238) по кнопке «Очистить».
    public void Clear() => _cells = new bool[Rows, Cols];

    /// <summary>Пересоздаёт поле с новыми размерами (все клетки становятся мёртвыми).</summary>
    // Используется: MainViewModel.ResizeField (MainViewModel.cs:247) по кнопке «Пересоздать поле».
    public void Resize(int rows, int cols)
    {
        if (rows <= 0) throw new ArgumentOutOfRangeException(nameof(rows)); // проверка размера
        if (cols <= 0) throw new ArgumentOutOfRangeException(nameof(cols)); // проверка размера

        Rows = rows; // новые размеры
        Cols = cols;
        _cells = new bool[rows, cols]; // новый пустой массив — старое состояние теряется
    } // конец Resize

    /// <summary>Случайно заполняет поле с заданной долей живых клеток (0..1).</summary>
    // Используется: MainViewModel.Randomize (MainViewModel.cs:229) по кнопке «Случайное заполнение»; density берётся из ползунка «Плотность».
    // Random? random = null — необязательный параметр: если не передан, создадим свой генератор.
    public void Randomize(double density, Random? random = null)
    {
        density = Math.Clamp(density, 0d, 1d); // на всякий случай зажимаем плотность в [0;1]
        random ??= new Random(); // ??= — «если null, то присвоить новое значение»

        for (int r = 0; r < Rows; r++) // перебираем строки
        {
            for (int c = 0; c < Cols; c++) // перебираем столбцы
            {
                // NextDouble() — случайное число от 0 до 1; если оно меньше density (например 0.3) — клетка оживает; вероятность = density, т.е. ~30% живых
                _cells[r, c] = random.NextDouble() < density;
            }
        }
    } // конец Randomize

    /// <summary>Разворачивает состояние поля в одномерный массив построчно (для сериализации).</summary>
    // [JSON] Двумерный массив в JSON записывать неудобно, поэтому «разворачиваем» в одномерный. Используется: FieldJsonService.Export (FieldJsonService.cs:40) по кнопке «Экспорт в JSON».
    public bool[] ToFlatArray()
    {
        var flat = new bool[Rows * Cols]; // одномерный массив нужной длины
        for (int r = 0; r < Rows; r++) // строки
        {
            for (int c = 0; c < Cols; c++) // столбцы
            {
                flat[(r * Cols) + c] = _cells[r, c]; // индекс = номер строки * ширина + номер столбца («построчная» раскладка)
            }
        }

        return flat; // отдаём массив вызывающему коду (в DTO для JSON)
    } // конец ToFlatArray

    /// <summary>Загружает состояние поля из одномерного массива построчно (размер массива должен совпадать с Rows*Cols).</summary>
    // [JSON] Обратная операция к ToFlatArray. Используется: FieldJsonService.Import (FieldJsonService.cs:62) по кнопке «Импорт из JSON».
    public void LoadFrom(bool[] flat)
    {
        // Защита: длина массива из файла должна равняться Rows*Cols, иначе данные не подходят полю
        if (flat.Length != Rows * Cols)
        {
            throw new ArgumentException("Размер массива не совпадает с размером поля.", nameof(flat)); // ArgumentException — «неверный аргумент»
        }

        for (int r = 0; r < Rows; r++) // строки
        {
            for (int c = 0; c < Cols; c++) // столбцы
            {
                _cells[r, c] = flat[(r * Cols) + c]; // обратное преобразование: из одномерного индекса в [строка, столбец]
            }
        }
    } // конец LoadFrom

    /// <summary>Число живых соседей клетки (row, col) среди 8 соседних клеток.</summary>
    // Используется: только в NextGeneration (GameField.cs:152) для каждой клетки на каждом шаге.
    public int CountAliveNeighbors(int row, int col)
    {
        int count = 0; // счётчик живых соседей
        for (int dr = -1; dr <= 1; dr++) // dr — сдвиг по строке: -1 (выше), 0 (та же), 1 (ниже)
        {
            for (int dc = -1; dc <= 1; dc++) // dc — сдвиг по столбцу: -1 (левее), 0, 1 (правее) — вместе 9 комбинаций
            {
                if (dr == 0 && dc == 0) continue; // сдвиг (0,0) — это сама клетка, её не считаем; continue — переходим к следующей комбинации

                int nr = row + dr; // строка соседа
                int nc = col + dc; // столбец соседа
                // Сосед учитывается, только если он внутри поля (иначе индекс вышел бы за массив) и жив. За краем поля клетки считаются мёртвыми — поэтому это НЕ тор.
                if (nr >= 0 && nr < Rows && nc >= 0 && nc < Cols && _cells[nr, nc])
                {
                    count++; // нашли живого соседа
                }
            }
        }

        return count; // от 0 до 8
    } // конец CountAliveNeighbors

    /// <summary>
    /// Вычисляет следующее поколение по правилам Конвея:
    /// живая клетка с 2 или 3 живыми соседями выживает, мёртвая клетка ровно с 3 соседями оживает,
    /// во всех остальных случаях клетка мертва.
    /// </summary>
    // Используется: MainViewModel.Advance (MainViewModel.cs:256) — по кнопке «Шаг» и на каждый Tick таймера при «Старт».
    public void NextGeneration()
    {
        var next = new bool[Rows, Cols]; // временный массив для нового поколения: нельзя менять _cells на лету, иначе соседи считались бы по уже обновлённым клеткам

        for (int r = 0; r < Rows; r++) // строки
        {
            for (int c = 0; c < Cols; c++) // столбцы
            {
                int neighbors = CountAliveNeighbors(r, c); // сколько живых соседей у клетки в ТЕКУЩЕМ поколении
                bool alive = _cells[r, c]; // жива ли клетка сейчас
                // Тернарный оператор «условие ? если_да : если_нет». Живая: выживает при 2 или 3 соседях (`is 2 or 3` — «равно 2 ИЛИ 3», шаблон C#).
                // Мёртвая: оживает только при ровно 3 соседях. Остальные случаи дают false (смерть от одиночества/перенаселения).
                next[r, c] = alive ? neighbors is 2 or 3 : neighbors == 3;
            }
        }

        _cells = next; // подменяем поле новым поколением
    } // конец NextGeneration
} // конец класса GameField
