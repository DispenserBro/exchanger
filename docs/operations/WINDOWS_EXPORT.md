# Debug и Release export для Windows x64

## Требования

- официальный Godot 4.7.1 Mono;
- export templates той же версии;
- .NET SDK 8;
- PowerShell 5.1 или новее.

## Native Video

`addons/native_video` — runtime GDExtension, а не editor-addon. Скрипт экспорта намеренно
исключает все addons из staging-копии, затем явно копирует только этот runtime-каталог. После
экспорта он проверяет наличие непустых `native_video.gdextension` и соответствующей конфигурации
`native_video.windows.<debug|release>.x86_64.dll` в staging, а после export — DLL выбранного
режима рядом с EXE. Манифест Godot встраивает в PCK, поэтому он не появляется внешним файлом.
При отсутствии любого из проверяемых файлов сборка удаляется как некорректная. Для MP4/MOV/M4V проект работает с рендерером Mobile (Vulkan); Compatibility для
этого расширения не подходит. Windows-библиотека использует Media Foundation: D3D12 работает в
zero-copy-режиме, а Vulkan использует совместимый CPU-copy fallback. После каждого export всё
равно нужно проверить воспроизведение реального ролика со звуком на целевом GPU, потому что
headless smoke не создаёт RenderingDevice и не подтверждает декодирование/аудиовывод.

## Пресеты

В проекте есть два самостоятельных Windows-пресета:

- `Windows x64 Release` — production-фича, без debug symbols и консольного wrapper;
- `Windows x64 Debug` — development-фича, с debug symbols, исходниками C# и консольным wrapper.

В Godot режим шаблона всё равно определяется действием экспорта. Для гарантированного результата
используйте скрипт ниже: он вызывает `--export-release` или `--export-debug` в соответствии с
выбранной конфигурацией.

## Сборка

Из корня проекта:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\tools\export-windows.ps1
```

Эта команда равнозначна явной Release-сборке:

```powershell
.\tools\export-windows.ps1 -Configuration Release
```

Отладочная сборка:

```powershell
.\tools\export-windows.ps1 -Configuration Debug
```

Другой путь к Godot передаётся явно:

```powershell
.\tools\export-windows.ps1 -Configuration Release -GodotPath "D:\Godot\Godot_v4.7.1-stable_mono_win64.exe"
```

Результат создаётся в новом каталоге
`artifacts/windows/Exchanger-0.6.0-<Release|Debug>-YYYYMMDD-HHMMSS/`; существующие сборки не
перезаписываются. Release-файл называется `Exchanger.exe`. Debug создаёт основной
`Exchanger.Debug.exe` и консольный wrapper `Exchanger.Debug.console.exe`.

## Изоляция production-проекта

Скрипт создаёт временную staging-копию внутри `artifacts/export-staging`, проверяет абсолютный
путь перед каждым рекурсивным удалением и очищает staging в `finally`.

В staging и PCK не попадают:

- `.git`, `.godot`, `.agents`, `.vscode`;
- `references/`, `tests/`, `tools/`, `docs/`, `artifacts/`;
- все editor/development addons; единственное исключение — явно скопированный runtime-каталог
  `addons/native_video`.

Из staging-версии `project.godot` удаляются editor plugins и два development-autoload:
`GodotxToast` и `_mcp_game_helper`. Исходный проект при этом не меняется, поэтому редакторская
автоматизация продолжает работать после экспорта.

## Состав и проверка

Оба пресета встраивают PCK и .NET outputs в EXE. Release не включает debug symbols и C#-исходники;
Debug включает их и использует консольный wrapper. Тестовые сборки и editor-addon файлы не входят
ни в один вариант. В каждом варианте рядом с EXE должна лежать DLL Native Video того же режима
(`debug` или `release`); `native_video.gdextension` хранится внутри embedded PCK. Это
дополнительно проверяется скриптом.

Контрольный экспорт 2026-09-02:

- Debug: `Exchanger.Debug.exe` — 185175952 байта, console wrapper — 91136 байт;
- Release: один `Exchanger.exe` — 190151408 байт;
- оба основных EXE завершили headless smoke с mock-оборудованием и exit code 0;
- эти контрольные файлы не подписаны Authenticode; подпись остаётся отдельным release-шагом.

Короткий автоматический smoke-run без открытия окна:

```powershell
$exe = ".\artifacts\windows\<build>\Exchanger.exe"
Start-Process $exe -ArgumentList "--headless", "--quit-after", "180", "--no-theme-dlc" -Wait
```

Первый проверенный export 0.6.0 содержал 189 файлов и занимал 180,8 МиБ; headless smoke завершился
с exit code 0. Эти числа являются диагностическими и могут меняться вместе с Godot/.NET.

## Перед релизом

- выполнить `dotnet test Exchanger.sln`;
- установить актуальный `active_theme.pck` либо отдельно проверить fallback;
- проверить реальный COM-порт и аппаратную приёмку;
- выполнить чистую установку на отдельном Windows-устройстве;
- при необходимости подписать EXE и DLL;
- сохранить checksum каталога релиза и совместимую версию конфигурации контроллера.
