using System.Text.Json.Serialization;

namespace TransparentHotkeyUtility.Models;

/// <summary>
/// Описание одного поля в динамической форме Modal-кружка.
/// </summary>
public sealed class FormFieldConfig
{
    /// <summary>Тип поля (TextInput / Checkbox / Label).</summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public FormFieldType FieldType { get; set; } = FormFieldType.TextInput;

    /// <summary>Метка поля, отображаемая над ним и используемая как ключ в JSON-параметрах.</summary>
    public string Label { get; set; } = "Поле";

    /// <summary>
    /// Значение по умолчанию.
    /// Для TextInput — строка, для Checkbox — "true"/"false".
    /// Для Label не используется.
    /// </summary>
    public string DefaultValue { get; set; } = string.Empty;

    /// <summary>
    /// Сохранять ли последнее введённое значение между запусками.
    /// </summary>
    public bool Remember { get; set; } = false;

    /// <summary>
    /// Обязательное ли поле при подтверждении формы.
    /// Для Checkbox: значение должно быть true.
    /// Для TextInput/FolderPath: значение не должно быть пустым.
    /// Для Label не используется.
    /// </summary>
    public bool Required { get; set; } = false;

    /// <summary>
    /// Последнее сохранённое значение (используется только при <see cref="Remember"/> == true).
    /// Хранится в figure.json автоматически при подтверждении формы.
    /// </summary>
    public string? SavedValue { get; set; }
}
