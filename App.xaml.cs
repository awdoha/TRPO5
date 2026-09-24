using System.Configuration; // (шаблонный using из мастера VS; в коде не используется)
using System.Data; // (шаблонный using из мастера VS; в коде не используется)
using System.Windows; // Application — базовый класс приложения WPF

namespace Lab5; // корневое пространство имён проекта

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
/// <remarks>
/// Класс приложения (partial: вторая половина генерируется из App.xaml, x:Class="Lab5.App"). Сам код пуст: стили и запуск главного окна
/// заданы в App.xaml (StartupUri, ресурсы). Используется: точка входа программы, создаётся WPF автоматически.
/// </remarks>
public partial class App : Application // Application — объект приложения: цикл сообщений, глобальные ресурсы (Application.Resources)
{
} // тело пустое — вся настройка в App.xaml
