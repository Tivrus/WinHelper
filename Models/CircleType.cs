namespace TransparentHotkeyUtility.Models;

/// <summary>Определяет режим поведения кружка при клике.</summary>
public enum CircleType
{
    /// <summary>Клик выполняет команду напрямую (как Status, Init).</summary>
    FireAndForget = 0,

    /// <summary>Клик перемещает кружок в центр и показывает форму с полями ввода.</summary>
    Modal = 1,

    /// <summary>Клик раскрывает вложенную группу дочерних кружков.</summary>
    Group = 2,
}
