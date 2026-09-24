using System.IO; // File (чтение/запись файла) и InvalidDataException
using System.Text; // Encoding.UTF8 — кодировка файла (чтобы не было проблем с кириллицей и т.п.)
using System.Text.Json; // JsonSerializer, JsonSerializerOptions — встроенная в .NET сериализация JSON
using Lab5.Model; // GameField — то, что сохраняем и создаём при загрузке

namespace Lab5.Services; // пространство имён папки Services (работа с файлами, вне MVVM-слоёв View/ViewModel)

/// <summary>Импорт/экспорт состояния поля "Жизнь" в JSON-файл.</summary>
/// <remarks>
/// [JSON] Сервис ничего не знает про UI: диалоги выбора файла и MessageBox — в MainViewModel, а здесь только «поле и файл».
/// Используется: поле MainViewModel._jsonService (MainViewModel.cs:35), вызовы в Export (MainViewModel.cs:294) и Import (MainViewModel.cs:318).
/// </remarks>
public sealed class FieldJsonService
{
    // [JSON] Настройки сериализатора: WriteIndented = true — JSON с отступами и переносами строк (читаемый человеком).
    // static readonly — один общий экземпляр на всё приложение (создавать настройки на каждый вызов дорого). Используется: JsonSerializer.Serialize в Export (FieldJsonService.cs:43).
    private static readonly JsonSerializerOptions SerializerOptions = new() { WriteIndented = true };

    /// <summary>DTO для сериализации: размеры поля + состояние клеток построчно.</summary>
    // [JSON] DTO (Data Transfer Object) — простой «контейнер данных» ровно той формы, какой мы хотим видеть JSON. Вложенный private-класс: снаружи не виден.
    // Используется: создаётся в Export (FieldJsonService.cs:36), читается в Import (FieldJsonService.cs:52).
    private sealed class FieldDto
    {
        // Число строк — станет полем "Rows" в JSON. Авто-свойство с get/set (set нужен десериализатору).
        public int Rows { get; set; }
        // Число столбцов — поле "Cols" в JSON.
        public int Cols { get; set; }
        // Клетки построчно — массив "Cells": [true,false,...]. Начальное значение — пустой массив, чтобы свойство не было null (Nullable).
        public bool[] Cells { get; set; } = Array.Empty<bool>();
    } // конец FieldDto

    // [JSON] Сохранение поля в файл. Используется: MainViewModel.Export (MainViewModel.cs:294) по кнопке «Экспорт в JSON» (filePath берётся из SaveFileDialog).
    public void Export(GameField field, string filePath)
    {
        // Заполняем DTO данными модели (инициализатор объекта)
        var dto = new FieldDto
        {
            Rows = field.Rows, // размеры берём из модели
            Cols = field.Cols,
            Cells = field.ToFlatArray() // клетки — развёрнутый в одномерный массив список (GameField.ToFlatArray)
        };

        string json = JsonSerializer.Serialize(dto, SerializerOptions); // превращаем объект в текст JSON (с отступами)
        File.WriteAllText(filePath, json, Encoding.UTF8); // записываем текст в файл в кодировке UTF-8 (перезаписывает существующий)
    } // конец Export

    // [JSON] Загрузка поля из файла. Используется: MainViewModel.Import (MainViewModel.cs:318) по кнопке «Импорт из JSON» (filePath — из OpenFileDialog).
    public GameField Import(string filePath)
    {
        string json = File.ReadAllText(filePath, Encoding.UTF8); // читаем весь файл в строку
        // Deserialize<FieldDto> превращает текст JSON обратно в объект. Если получился null — бросаем свою ошибку (?? throw)
        FieldDto dto = JsonSerializer.Deserialize<FieldDto>(json)
            ?? throw new InvalidDataException("Файл не содержит корректных данных поля.");

        // Проверка целостности: размеры положительные и длина массива клеток = Rows*Cols. Иначе файл повреждён/чужой
        if (dto.Rows <= 0 || dto.Cols <= 0 || dto.Cells.Length != dto.Rows * dto.Cols)
        {
            throw new InvalidDataException("Размеры поля в файле некорректны."); // ловится в MainViewModel.Import (MainViewModel.cs:329) и показывается через MessageBox
        }

        var field = new GameField(dto.Rows, dto.Cols); // создаём пустую модель нужного размера
        field.LoadFrom(dto.Cells); // раскладываем плоский массив в двумерный
        return field; // возвращаем готовое поле во ViewModel
    } // конец Import
} // конец класса FieldJsonService
