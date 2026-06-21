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

    public int    HotkeyModifiers  { get; set; } = 3; // MOD_CONTROL (2) | MOD_ALT (1)
    public int    HotkeyVirtualKey { get; set; } = 0xBA; // VK_OEM_1 (;)
    public string HotkeyText       { get; set; } = "Ctrl + Alt + OemSemicolon";
}
