# CalendarBar для Windows 11

Тот же Exchange-клиент, что и macOS CalendarBar: календарь и почта через **ActiveSync**, без окна на панели задач. Две иконки в системном трее (календарь и почта), по клику — всплывающая панель.

## Возможности

Совпадают с Mac-версией:

- сводка в трее: число оставшихся встреч сегодня и время следующей
- таймлайн дня 00:00–24:00, навигация по дням, «Сегодня»
- детали встречи, принятие / отклонение / удаление
- повторяющиеся события, пересекающиеся встречи колонками
- красная линия текущего времени
- почта: входящие / отправленные / черновики / корзина, треды, ответ, пересылка, вложения
- уведомления Windows о встречах и новой почте (старые непрочитанные при старте не пушатся)
- запуск при входе в Windows

## Требования

- Windows 11 (соберётся и на Windows 10 1809+)
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- Exchange с ActiveSync (`/Microsoft-Server-ActiveSync`)
- WebView2 Runtime (обычно уже стоит на Windows 11)

## Сборка

На машине с Windows:

```bat
cd calendar-bar-win
dotnet publish CalendarBar\CalendarBar.csproj -c Release -r win-x64 --self-contained true -o publish
```

Запуск:

```bat
publish\CalendarBar.exe
```

Или откройте `CalendarBar.sln` в Visual Studio 2022 и нажмите F5.

## Авторизация

При первом запуске кликните иконку календаря в трее и заполните форму:

| Поле | Пример |
|------|--------|
| Email | `user@organization.com` |
| Сервер | `mail.organization.com` |
| Домен | `ORGANIZATION` |
| Имя пользователя | `u_username` |
| Пароль | `••••••••` |

Email, сервер, домен и имя пользователя хранятся в `%AppData%\CalendarBar\settings.json`. Пароль — только в Windows Credential Manager.

Логин ActiveSync: `DOMAIN\username`, если домен указан, иначе email.

## Как это работает

Тот же протокол, что на Mac:

- `OPTIONS` / discovery endpoint
- `Provision` для policy key
- `FolderSync` для Calendar, Inbox, Sent, Drafts, Trash
- `Sync`, `ItemOperations Fetch`, `SendMail` / `SmartReply` / `SmartForward`
- `MeetingResponse`
- свой WBXML encode/decode
- `DeviceId` и `User-Agent` в стиле iPhone

Синхронизация по умолчанию раз в 5 минут.

## Настройки

Меню ⚙ в панели календаря:

- уведомление за 5 / 10 / 15 / 30 / 60 минут
- настройки уведомлений Windows
- запускать при входе
- интервал синхронизации
- почта / HTML / картинки
- о приложении, выход из аккаунта, закрыть CalendarBar

## Локальные данные

- настройки: `%AppData%\CalendarBar\settings.json`
- пароль: Credential Manager, цель `CalendarBar/exchange-password`
- кэш писем: `%LocalAppData%\CalendarBar\MailCache`

## Отличия от macOS

Windows не рисует текст рядом с иконкой трея, как menu bar на Mac. Сводка `3 · 16:00` и счётчик непрочитанных показываются в подсказке иконки и бейджем на ней. Панели — обычные окна с закруглением Windows 11, а не NSPopover.
