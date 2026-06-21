namespace TransparentHotkeyUtility.Models;

/// <summary>Тип поля в динамической форме Modal-кружка.</summary>
public enum FormFieldType
{
    /// <summary>Текстовое поле ввода.</summary>
    TextInput = 0,

    /// <summary>Флажок (boolean).</summary>
    Checkbox = 1,

    /// <summary>Статичная текстовая метка — не передаётся в exe.</summary>
    Label = 2,

    /// <summary>Путь к папке (строка с кнопкой выбора директории).</summary>
    FolderPath = 3,
}
