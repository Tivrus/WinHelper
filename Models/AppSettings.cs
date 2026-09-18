namespace TransparentHotkeyUtility.Models;

internal sealed class AppSettings
{
    public string FolderPath    { get; set; } = string.Empty;
    public string RepoUrl       { get; set; } = string.Empty;
    /// <summary>
    /// true  → фигура появляется под курсором мыши (поведение по умолчанию).
    /// false → фигура появляется по центру основного экрана.
    /// </summary>
    public bool   SpawnAtCursor { get; set; } = true;

    /// <summary>Keyboard — клавиша (с модификаторами или без); Mouse — кнопка мыши.</summary>
    public HotkeyInputKind HotkeyKind { get; set; } = HotkeyInputKind.Keyboard;

    public int    HotkeyModifiers  { get; set; } = 3; // MOD_CONTROL (2) | MOD_ALT (1)
    public int    HotkeyVirtualKey { get; set; } = 0xBA; // VK_OEM_1 (;)
    /// <summary>
    /// Код кнопки мыши: 1=Left, 2=Right, 3=Middle, 4=Mouse4 (X1), 5=Mouse5 (X2).
    /// Используется при <see cref="HotkeyKind"/> == Mouse.
    /// </summary>
    public int    HotkeyMouseButton { get; set; } = 0;
    public string HotkeyText       { get; set; } = "Ctrl + Alt + OemSemicolon";

    /// <summary>Id последней открытой страницы фигуры (переключение колёсиком).</summary>
    public string? LastPageId { get; set; }
}
