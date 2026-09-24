using System.ComponentModel; // нужен для INotifyPropertyChanged, PropertyChangedEventHandler, PropertyChangedEventArgs
using System.Runtime.CompilerServices; // нужен для атрибута [CallerMemberName] (компилятор сам подставляет имя вызывающего свойства)

namespace Lab5.ViewModel; // пространство имён папки ViewModel; в MainWindow.xaml подключено как xmlns:vm

/// <summary>Базовый класс для всех ViewModel — реализует INotifyPropertyChanged.</summary>
/// <remarks>
/// [MVVM] Это «фундамент» MVVM. INotifyPropertyChanged — интерфейс-«звонок»: когда свойство ViewModel
/// изменилось, объект кричит событием PropertyChanged, а WPF-привязка ({Binding ...}) его слышит и
/// перерисовывает элемент. Без него экран показал бы значение один раз и больше не обновлялся бы.
/// Используется: наследуют MainViewModel (MainViewModel.cs:20) и CellViewModel (CellViewModel.cs:13).
/// </remarks>
public abstract class ViewModelBase : INotifyPropertyChanged // abstract — сам по себе не создаётся, только как родитель; INotifyPropertyChanged — реализуемый интерфейс (неявная реализация: событие объявлено public)
{
    // [MVVM] Событие PropertyChanged: WPF при привязке САМ подписывается на него (+=) у объекта из DataContext.
    // Знак ? — событие может быть null, если никто не подписан (Nullable, включён в Lab5.csproj).
    // Используется: вызывается в OnPropertyChanged (строка ниже); подписчик — движок привязок WPF (Binding из MainWindow.xaml).
    public event PropertyChangedEventHandler? PropertyChanged;

    // [MVVM] Поднимает событие PropertyChanged для свойства с именем propertyName.
    // [CallerMemberName] — если вызвать OnPropertyChanged() без аргумента, компилятор подставит имя того свойства/метода, откуда вызвали.
    // Используется: из SetField (ниже); напрямую — MainViewModel.cs:198, 211, 222 (AliveCount), 322–323 (Rows/Cols при импорте).
    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        // ?.Invoke — «вызови, только если есть подписчики»; this — отправитель (сама ViewModel); PropertyChangedEventArgs несёт имя изменившегося свойства
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    /// <summary>Устанавливает поле и уведомляет UI, если значение действительно изменилось.</summary>
    // [MVVM] SetField — универсальный «сеттер»: T — любой тип (int, bool, double, string). ref T field — ссылка на само поле (_rows и т.п.), чтобы метод мог его перезаписать.
    // Возвращает true, если значение изменилось (это использует IntervalMs, MainViewModel.cs:95).
    // Используется: во всех сеттерах свойств MainViewModel (строки 67, 75, 83, 95, 108, 116, 129) и CellViewModel.IsAlive (CellViewModel.cs:31).
    protected bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        // Сравниваем старое и новое значение стандартным сравнивателем для типа T (для int — ==, для string — Equals и т.д.)
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            // Значение не изменилось — ничего не делаем, UI не дёргаем (защита от лишних перерисовок и зацикливания)
            return false;
        }

        field = value; // записываем новое значение прямо в приватное поле (благодаря ref)
        OnPropertyChanged(propertyName); // сообщаем WPF: «свойство propertyName изменилось» — привязки перечитают его через get
        return true; // сообщаем вызывающему коду, что изменение произошло
    } // конец SetField
} // конец класса ViewModelBase
