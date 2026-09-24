using System.Windows; // Window — базовый класс окна WPF
using Lab5.ViewModel; // MainViewModel — ViewModel, которую кладём в DataContext

namespace Lab5; // корневое пространство имён проекта (совпадает с x:Class="Lab5.MainWindow" в XAML)

/// <summary>
/// Главное окно приложения. Вся логика — во ViewModel, code-behind содержит
/// только инициализацию компонентов и установку DataContext (MVVM).
/// </summary>
/// <remarks>
/// [MVVM] Это «частичный» (partial) класс: вторая половина генерируется из MainWindow.xaml. Окно запускается из App.xaml (StartupUri, App.xaml:9).
/// Code-behind пуст сознательно: обработчиков Click нет — кнопки работают через Command, данные — через Binding.
/// </remarks>
public partial class MainWindow : Window
{
    // Конструктор окна; вызывается WPF автоматически при старте (StartupUri="MainWindow.xaml" в App.xaml).
    public MainWindow()
    {
        InitializeComponent(); // создаёт элементы из MainWindow.xaml (Grid, кнопки, ItemsControl...) — сгенерированный метод
        // [MVVM] DataContext — «источник данных по умолчанию» для всех {Binding ...} окна и вложенных элементов.
        // Кладём сюда новую MainViewModel — после этого Binding Rows/Cols/Density/Cells/StartCommand... ищут свойства именно в ней.
        DataContext = new MainViewModel();
    } // конец конструктора
} // конец класса MainWindow
