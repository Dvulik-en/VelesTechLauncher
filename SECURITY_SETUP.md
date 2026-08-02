# 🔐 Настройка приватных данных лаунчера

## ⚠️ КРИТИЧЕСКИ ВАЖНО — перед публикацией на GitHub

Твой старый API-токен Pterodactyl **`ptlc_DxhEEs404YOrl5YhQyJCpXQAEHkcw9ojCb0xHdcQ3ec`** засветился в переписке. **Отзови его немедленно:**

1. Открой панель: `https://mgr.hosting-minecraft.pro`
2. Профиль (правый верхний угол) → **API Credentials**
3. Удали старый токен (Delete)
4. Создай новый — **Create API Key**
5. Скопируй новый `ptlc_...` и вставь в `launcher.secrets.json` (см. ниже)

---

## 📂 Как теперь устроены конфиги

| Файл | Куда | Коммитить в git? | Что содержит |
|------|------|------------------|--------------|
| `launcher.config.json` | Рядом с `.exe` | ✅ ДА | Версии MC / NeoForge, имена папок, публичный URL GitHub Pages |
| `launcher.secrets.example.json` | В репозиторий | ✅ ДА | Шаблон с заглушками — образец для админа |
| `launcher.secrets.json` | `%APPDATA%\VelesTech\` | ❌ **НЕТ** (в `.gitignore`) | API-токен Pterodactyl, IP игрового сервера, ID сервера панели |

## 🚀 Как настроить у себя (один раз)

### Шаг 1. Клонируй репозиторий

```bash
git clone https://github.com/твой-логин/VelesTechLauncher.git
cd VelesTechLauncher
```

### Шаг 2. Создай приватный конфиг

Скопируй пример и заполни:

```bash
copy launcher.secrets.example.json "%APPDATA%\VelesTech\launcher.secrets.json"
```

Открой `%APPDATA%\VelesTech\launcher.secrets.json` в блокноте и подставь **новые** реальные значения:

```json
{
  "panelUrl": "https://mgr.hosting-minecraft.pro",
  "panelServerId": "00b1d7f9",
  "panelApiToken": "ptlc_ТВОЙ_НОВЫЙ_ТОКЕН",
  "archiveFileName": "velestech_client.zip",

  "serverIp": "213.152.43.48",
  "serverPort": 25572
}
```

### Шаг 3. Проверь `.gitignore`

Убедись что в корне репозитория есть строка (уже добавлена в этой поставке):
```
launcher.secrets.json
```

### Шаг 4. Собери и запусти

```bash
dotnet build -c Release
```

При старте лаунчер прочитает публичный `launcher.config.json` рядом с `.exe`, наложит на него значения из `%APPDATA%\VelesTech\launcher.secrets.json` — и ты получишь готовый манифест.

## 📤 Как раздавать сборку игрокам

Игрокам нужен только `.exe` + твой `launcher.secrets.json` (если ты не хочешь их заставлять получать свой API-токен). Есть три варианта:

**Вариант A — Portable, ты сам подкладываешь секреты в билд:**
Положи `launcher.secrets.json` рядом с `VelesTech.exe` в архиве раздачи. Лаунчер ищет секреты и в `AppContext.BaseDirectory`, и в `%APPDATA%`.

Плюсы: игрок распаковал zip и играет.  
Минусы: игроки увидят твой токен если распакуют exe (лаунчер читает JSON в открытом виде).

**Вариант B — на GitHub Pages вместо Pterodactyl:**
У тебя уже есть `modpackJsonUrl` и `filesBaseUrl` в публичном конфиге — они ведут на `https://dvulik-en.github.io/my-modpack-storage/`. Переключи `ModpackInstaller.cs` на скачивание с GitHub Pages (по `ModpackJsonUrl` + список файлов), и Pterodactyl-токен вообще не понадобится игрокам.

Плюсы: никакой приватной информации на клиенте, всё берётся с публичного CDN.  
Минусы: нужно поддерживать `modpack.json` актуальным (используй генератор из шага 3 предыдущего разговора).

**Вариант C — свой мини-API, отдающий одноразовые ссылки:**
Игрок стучится в твой сервер → тот проверяет что игрок легитимный → отдаёт signed URL от Pterodactyl. Токен остаётся только на твоём сервере.

Плюсы: максимальная защита.  
Минусы: нужен ещё один сервис (Node/Python/…).

## 🧪 Как проверить что секреты правильно подгружены

В коде запуска (`MinecraftLauncher.LaunchAsync` или где угодно перед скачиванием сборки) добавь:

```csharp
if (!ManifestLoader.ValidateSecrets(_manifest, out var missing))
{
    throw new InvalidOperationException(
        $"Не заполнены секретные поля: {missing}. " +
        $"Создайте файл: {ManifestLoader.GetRecommendedSecretsPath()} " +
        $"(шаблон — в launcher.secrets.example.json).");
}
```

Тогда при отсутствии токена лаунчер покажет игроку понятную ошибку вместо `HTTP 401 Unauthorized`.

## 🔎 Что смотрит лаунчер и в каком порядке

**Публичный конфиг** (`launcher.config.json`) — по приоритету:

1. `{папка_exe}\launcher.config.json`
2. `{папка_exe}\Assets\launcher.config.json`
3. `%APPDATA%\VelesTech\launcher.config.json`

**Приватный конфиг** (`launcher.secrets.json`) — по приоритету:

1. `%APPDATA%\VelesTech\launcher.secrets.json` ← рекомендуется для игроков
2. `{папка_exe}\launcher.secrets.json` ← portable / только для админа

Приватный файл **накладывается поверх** публичного — так что если в `launcher.secrets.json` есть, например, поле `versionFolder`, оно перезапишет публичное. Обычно так делать не надо: всё что не секретно — держи в публичном конфиге.


## ✅ Финальный чек-лист

- [ ] Старый токен `ptlc_DxhEEs404YO...` **отозван** в панели Pterodactyl
- [ ] Новый токен сгенерирован и сохранён **только** в `%APPDATA%\VelesTech\launcher.secrets.json`
- [ ] `launcher.secrets.json` **не** попал в `git add`
- [ ] `.gitignore` содержит `launcher.secrets.json`
- [ ] `launcher.config.json` **не содержит** ничего секретного
- [ ] `launcher.secrets.example.json` содержит только заглушки типа `panel.example.com`
- [ ] `grep`/`Select-String` по репозиторию не находит реальный IP/токен/URL панели
