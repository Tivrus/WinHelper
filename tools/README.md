# Утилиты для gitHelper

## GitHelper.Tool

Консольная программа с теми же командами git, что раньше были «встроены» в основное окно. Путь к папке и URL репозитория читаются из `%LocalAppData%\gitHelper\settings.json` (как у основного приложения).

### Сборка

Из корня репозитория:

```powershell
dotnet publish tools/GitHelper.Tool/GitHelper.Tool.csproj -c Release -r win-x64 --self-contained true
```

Готовый `GitHelper.Tool.exe` будет в `tools/GitHelper.Tool/bin/Release/net8.0/win-x64/publish/`.

### Команды

- `GitHelper.Tool save <сообщение> [ветка]` — add, remote, commit, push
- `GitHelper.Tool branch <имя> [-M]` — создание или переименование ветки (`-M` — force)
- `GitHelper.Tool add [путь]` — `git add` (по умолчанию `.`)
- `GitHelper.Tool init` — `git init`, remote, `branch -M main`
- `GitHelper.Tool status` — открывает `cmd` с `git status` в выбранной папке

Справка: `GitHelper.Tool --help`

### Связка с фигурой в gitHelper

В настройках кружка укажите **путь к .exe** (`executablePath`) на этот файл (или скопируйте несколько копий с разными именами и задайте у каждого кружка свой файл и при необходимости **Modal** с полями — аргументы пойдут в командную строку после подкоманд).

Пример для «save» с сообщением из формы: тип **Modal**, поле с текстом коммита, в `figure.json` порядок полей должен соответствовать ожиданию `save <сообщение> [ветка]`.

Основное приложение по умолчанию поставляется с **пустой** `figure.json`; фигуру вы собираете сами и подключаете внешние `.exe` из удобной вам папки.
