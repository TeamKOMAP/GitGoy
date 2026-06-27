# GitGoy

GitGoy - учебное приложение для работы с простой системой версионирования файлов. Проект показывает основные идеи GitHub-подобных сервисов: пользователи, репозитории, ветки, коммиты, push-история, просмотр файлов и разделение доступа к проектам.

> Важно: текущая реализация является учебной моделью системы контроля версий. История репозиториев хранится в JSON-файлах на сервере, а не в настоящих `.git`-репозиториях.

## Возможности

- вход пользователя по имени;
- создание приватных и публичных репозиториев;
- просмотр своих и публичных проектов;
- поиск публичных проектов;
- управление участниками проекта с ролями `reader` и `writer`;
- создание, переименование и удаление веток;
- создание коммитов по измененным локальным файлам;
- фиксация push-событий;
- просмотр файлов репозитория по веткам;
- просмотр списка коммитов, push-истории и diff-данных;
- WPF-клиент для работы с локальной папкой проекта;
- простой web UI для просмотра репозиториев и файлов.

## Стек

- .NET 10 - API, доменная модель и инфраструктура;
- ASP.NET Core Web API;
- Entity Framework Core;
- ASP.NET Core Identity;
- SQLite по умолчанию, SQL Server поддерживается через строку подключения;
- WPF на .NET 8 для desktop-клиента;
- Swagger для документации API в режиме разработки.

## Структура проекта

```text
GitGoy/
|-- Vcs.Api/                 # ASP.NET Core API и статический web UI
|-- Vcs.Domain/              # Доменные сущности и enum-типы
|-- Vcs.Infrastructure/      # EF Core, сервисы проектов и версионирования
|-- src/Vcs.Desktop/         # WPF-клиент
|-- VersionControlSystem.sln # Основной solution-файл
`-- VersionControlSys.slnx   # Альтернативный solution-файл
```

## Архитектура

Проект разделен на несколько слоев:

- `Vcs.Domain` содержит сущности предметной области: пользователей, проекты, участников, подписки, refresh-токены и audit log.
- `Vcs.Infrastructure` содержит `AppDbContext`, DTO и сервисы. `ProjectService` отвечает за проекты, доступы и участников. `GitService` реализует учебную модель веток, файлов, коммитов и push-событий.
- `Vcs.Api` открывает HTTP API, настраивает EF Core, Identity, Swagger, CORS и раздачу статического web UI.
- `Vcs.Desktop` предоставляет WPF-интерфейс для выбора локальной папки, отслеживания изменений файлов, создания коммитов, push-событий и управления ветками.

## Хранение данных

По умолчанию API использует SQLite:

```json
"ConnectionStrings": {
  "DefaultConnection": "Data Source=storage/vcs.db"
}
```

После запуска рядом с API создается папка `storage/`:

- `storage/vcs.db` - база данных пользователей, проектов и прав доступа;
- `storage/keys/` - ключи ASP.NET Data Protection;
- `storage/repositories/<projectId>/repository.json` - состояние учебного репозитория: ветки, файлы, коммиты и push-история.

WPF-клиент дополнительно хранит связь между серверным проектом и локальной папкой пользователя в:

```text
%LOCALAPPDATA%/Vcs.Desktop/repositories.json
```

## Требования

- Windows для запуска WPF-клиента;
- .NET SDK 10 для API, `Vcs.Domain` и `Vcs.Infrastructure`;
- .NET Desktop Runtime/SDK 8 для WPF-клиента;
- Visual Studio 2022 или новее, либо CLI `dotnet`.

## Запуск API

Из корня репозитория:

```powershell
dotnet restore VersionControlSystem.sln
dotnet run --project Vcs.Api --urls http://localhost:5221
```

API будет доступен по адресу:

```text
http://localhost:5221
```

Swagger в режиме разработки:

```text
http://localhost:5221/swagger
```

Статический web UI открывается на корневом адресе:

```text
http://localhost:5221/
```

## Запуск desktop-клиента

Сначала запустите API на `http://localhost:5221`, затем в другом терминале выполните:

```powershell
dotnet run --project src/Vcs.Desktop
```

Адрес API и имя пользователя по умолчанию задаются в файле:

```text
src/Vcs.Desktop/clientsettings.json
```

```json
{
  "Api": {
    "BaseUrl": "http://localhost:5221",
    "Username": "desktop-client",
    "Password": "Password123!"
  }
}
```

Пароль в текущей учебной реализации не проверяется полноценно: пользователь создается или находится по имени.

## Основные API endpoints

### Auth

- `POST /api/auth/login` - вход или создание пользователя;
- `GET /api/auth/me` - информация о текущем пользователе.

### Projects

- `POST /api/projects` - создать проект;
- `GET /api/projects/my` - получить проекты текущего пользователя;
- `GET /api/projects/public` - получить публичные проекты;
- `GET /api/projects/search?q=...` - поиск публичных проектов;
- `GET /api/projects/{id}` - получить проект;
- `PATCH /api/projects/{id}` - обновить проект;
- `DELETE /api/projects/{id}` - удалить проект;
- `POST /api/projects/{id}/members` - добавить участника;
- `GET /api/projects/{id}/members` - получить участников;
- `DELETE /api/projects/{id}/members/{memberId}` - удалить участника.

### Repository

- `GET /api/projects/{projectId}/branches` - список веток;
- `POST /api/projects/{projectId}/branches` - создать ветку;
- `PATCH /api/projects/{projectId}/branches/{name}` - переименовать ветку;
- `DELETE /api/projects/{projectId}/branches/{name}` - удалить ветку;
- `GET /api/projects/{projectId}/commits` - список коммитов;
- `GET /api/projects/{projectId}/commits/{sha}` - детали коммита;
- `POST /api/projects/{projectId}/commits` - создать коммит;
- `GET /api/projects/{projectId}/pushes` - история push-событий;
- `POST /api/projects/{projectId}/pushes` - создать push-событие;
- `GET /api/projects/{projectId}/files` - список файлов;
- `GET /api/projects/{projectId}/diff` - изменения коммита.

Для определения пользователя API использует заголовок:

```text
X-User-Name: desktop-client
```

## Пример работы через API

Создать пользователя:

```powershell
Invoke-RestMethod `
  -Method Post `
  -Uri http://localhost:5221/api/auth/login `
  -ContentType "application/json" `
  -Body '{"username":"alice","password":"1"}'
```

Создать репозиторий:

```powershell
Invoke-RestMethod `
  -Method Post `
  -Uri http://localhost:5221/api/projects `
  -Headers @{ "X-User-Name" = "alice" } `
  -ContentType "application/json" `
  -Body '{"name":"demo","description":"Учебный репозиторий","visibility":"private"}'
```

Создать коммит:

```powershell
Invoke-RestMethod `
  -Method Post `
  -Uri http://localhost:5221/api/projects/<projectId>/commits `
  -Headers @{ "X-User-Name" = "alice" } `
  -ContentType "application/json" `
  -Body '{
    "branch": "main",
    "message": "Initial files",
    "changes": [
      {
        "path": "README.md",
        "content": "# Demo",
        "operation": "add"
      }
    ]
  }'
```

## Особенности и ограничения учебной версии

- Репозиторий не является настоящим Git-репозиторием и не содержит `.git`.
- Ветки в текущем `GitService` создаются пустыми и не копируют файлы из исходной ветки.
- `push` не отправляет данные на удаленный сервер, а записывает событие в историю проекта.
- Авторизация упрощена: имя пользователя передается через `X-User-Name`.
- Пароль принимается API, но не используется как полноценный механизм безопасности.
- Desktop-клиент отслеживает изменения локальных файлов через `FileSystemWatcher` и SHA-256-хэши.
- Из отслеживания локальных изменений исключаются `.git`, `.vs`, `bin`, `obj` и `node_modules`.

## Сборка

```powershell
dotnet build VersionControlSystem.sln
```

Если нужно собрать только API:

```powershell
dotnet build Vcs.Api/Vcs.Api.csproj
```

Если нужно собрать только WPF-клиент:

```powershell
dotnet build src/Vcs.Desktop/Vcs.Desktop.csproj
```

## Возможные направления развития

- заменить JSON-хранилище на настоящие Git-репозитории через LibGit2Sharp;
- добавить нормальную регистрацию, проверку пароля и JWT-auth;
- реализовать clone/pull/merge;
- копировать состояние исходной ветки при создании новой ветки;
- добавить просмотр содержимого файлов и полноценный diff;
- добавить тесты для `ProjectService`, `GitService` и API-контроллеров.
