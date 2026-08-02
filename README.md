# VELES TECH Launcher — v4

## 🎯 Главный фикс v4

**Симптом:** Minecraft запускался, кнопка «Сетевая игра» становилась активной, но при попытке зайти на сервер клиент разрывал соединение с ошибкой:

```
Unidentified mapping from registry minecraft:block
Unidentified mapping from registry minecraft:entity_type
Unidentified mapping from registry minecraft:item
Missing registry data for network connection:
    minecraft:item: resourcefulbees:alexandr_bee_spawn_egg
    ... (213 недостающих регистраций)
Failed to load registry, closing connection.
```

**Причина:** `WorkingDirectory` процесса Java был установлен **не в папку сборки** (`.velestech`), а в папку `runtime/jre-legacy/bin/`, откуда стартовал `javaw.exe`. Из-за этого моды с кастом-регистри (в первую очередь `resourcefulbees` — кастомные пчёлы через JSON в `config/resourcefulbees/bees/*.json`, а также `kubejs`, `CraftTweaker`) **не находили свои config-и** — сервер регистрировал 213 предметов, а клиент 0.

TLauncher устанавливает `WorkingDirectory = gameDir` — поэтому у него всё работает. CmlLib этого **не делает по умолчанию**.

**Исправлено:** в `MinecraftLauncher.cs` после `CreateProcessAsync` теперь явно:
```csharp
process.StartInfo.WorkingDirectory = _config.GameDirectory;
```

Также поменял `VersionType` с `"VelesTech"` на `"release"` — как у TLauncher (влияет на некоторые моды-совместимости).

## 📋 Что уже было исправлено в предыдущих версиях

| Версия | Фикс |
|---|---|
| v1 | Локальная авторизация (DPAPI + SHA256), окно логина/настроек, `UserType = "mojang"` |
| v2 | Скачивание через Pterodactyl API вместо Google Drive, распаковка через `File.Copy` |
| v3 | TLauncher-совместимая раскладка `libraries/libraries/`, multi-protocol server ping |
| **v4** | **`WorkingDirectory` = gameDir + `VersionType` = "release"** |

## 📦 Правильная структура архива

```
velestech_client.zip
└── (корень)
    ├── mods/                    ← .jar моды
    ├── config/                  ← ⚠️ resourcefulbees/, kubejs/ и др. — критично!
    │   ├── resourcefulbees/
    │   │   └── bees/*.json      ← кастомные пчёлы
    │   ├── kubejs/
    │   └── ...
    ├── libraries/               ← Forge библиотеки (net/minecraftforge/...)
    ├── runtime/jre-legacy/bin/  ← Java 8 (у тебя 1.8.0_51 работает ✓)
    ├── versions/
    │   └── The Decursio Project - Expert r1.0.9/
    │       ├── *.jar
    │       └── *.json           ← Forge-манифест сборки
    ├── resourcefulbees/         ← если сборка требует отдельно
    ├── kubejs/
    ├── scripts/                 ← CraftTweaker
    ├── defaultconfigs/
    ├── shaderpacks/, resourcepacks/, saves/
    └── options.txt
```

## ⚙️ Конфигурация лаунчера (`Models/ModpackManifest.cs`)

```csharp
public string PanelUrl        = "https://mgr.hosting-minecraft.pro";
public string PanelServerId   = "00b1d7f9";
public string PanelApiToken   = "ptlc_......";
public string ArchiveFileName = "velestech_client.zip";
public string ServerIp        = "213.152.43.48";  // либо DNS-имя сервера
public ushort ServerPort      = 25572;
public string ForgeVersion    = "36.2.34";
```

## 🐛 Диагностика — если снова не заходит на сервер

1. Открой `%APPDATA%\.velestech\config\resourcefulbees\bees\` — там должны быть JSON-файлы кастомных пчёл. Если пусто — сборка распаковалась некорректно, нажми в лаунчере ⚙ → «Переустановить сборку».

2. Проверь свежий `%APPDATA%\.velestech\logs\latest.log` — если снова видишь `Missing registry data`, посмотри **какой мод недостает**:
   - `resourcefulbees:` → JSON-и в `config/resourcefulbees/bees/*.json` не подхватились
   - `kubejs:` → скрипты в `kubejs/server_scripts/` не подгружены
   - `crafttweaker:` → `.zs` скрипты в `scripts/` не найдены

3. Убедись, что в логе есть строка `WorkingDirectory = C:\Users\...\.velestech` (лаунчер её пишет в статус). Если её нет — значит запустилась старая версия лаунчера.

4. Java **обязана быть 8** (у тебя `1.8.0_51` — ✓). Если стало 11/17 — Forge 1.16.5 упадёт.

## 📁 Структура проекта

```
VelesTech/
├── Program.cs, App.cs, VelesTech.csproj
├── Models/           ← LauncherConfig, AccountData, ModpackManifest
├── Services/         ← ConfigService, ModpackInstaller, ServerMonitorService
├── Auth/             ← AuthService (DPAPI + SHA256)
├── Launcher/
│   └── MinecraftLauncher.cs   ← ⚡ WorkingDirectory + libraries/libraries + VersionType
├── Controls/         ← спрайт-анимации
├── Views/            ← LoginWindow, SettingsWindow, MainWindow
└── Assets/EmbeddedManifests/forge-1.16.5-36.2.34.json
```

## 🛠️ Сборка

```bash
dotnet build -c Release
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
```
