using System.Windows; // нужен для ThemeInfo и ResourceDictionaryLocation

// Атрибут сборки (шаблонный, создаётся мастером WPF). Говорит WPF, где искать словари ресурсов тем оформления.
// Используется: WPF читает его при запуске; на работу Игры «Жизнь» не влияет (своих тем нет).
[assembly:ThemeInfo(
    ResourceDictionaryLocation.None,            //where theme specific resource dictionaries are located
                                                //(used if a resource is not found in the page,
                                                // or application resource dictionaries)
    ResourceDictionaryLocation.SourceAssembly   //where the generic resource dictionary is located
                                                //(used if a resource is not found in the page,
                                                // app, or any theme specific resource dictionaries)
)]
