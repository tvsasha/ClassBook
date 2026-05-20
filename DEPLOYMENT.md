# Развертывание ClassBook на VPS

## Минимальный вариант через Docker Compose

1. Установить Docker и Docker Compose plugin на VPS.
2. Скопировать проект на сервер или сделать `git clone`.
3. В папке проекта создать `.env`:

```bash
cp .env.example .env
nano .env
```

Обязательно заменить `MSSQL_SA_PASSWORD` на сложный пароль.

4. Запустить:

```bash
docker compose up -d --build
docker compose logs -f app
```

При первом старте приложение само применит миграции EF Core и создаст администратора `1` / `1`.

## HTTPS

Для теста на людях лучше не открывать приложение просто по IP. Поставьте Caddy или Nginx и проксируйте домен на `127.0.0.1:8080`.

Пример для Caddy:

```caddyfile
classbook.example.ru {
    reverse_proxy 127.0.0.1:8080
}
```

Если временно запускаете без HTTPS по `http://IP:8080`, в `.env` нужно поставить:

```env
CLASSBOOK_COOKIE_SAMESITE=Lax
CLASSBOOK_COOKIE_SECURE_POLICY=SameAsRequest
```

Для HTTPS оставьте значения по умолчанию:

```env
CLASSBOOK_COOKIE_SAMESITE=None
CLASSBOOK_COOKIE_SECURE_POLICY=Always
```

## Порты

- Приложение публикуется на `CLASSBOOK_HTTP_PORT`, по умолчанию `8080`.
- SQL Server опубликован только на `127.0.0.1:14333`, наружу VPS он не торчит.
- Если на сервере уже работает Amnezia, проверьте, что порт `8080` свободен. При конфликте поменяйте `CLASSBOOK_HTTP_PORT`.

## Обновление

```bash
git pull
docker compose up -d --build
docker compose logs -f app
```

## Резервная копия базы

Пример ручного бэкапа:

```bash
docker exec classbook-sqlserver /opt/mssql-tools18/bin/sqlcmd \
  -S localhost -U sa -P "$MSSQL_SA_PASSWORD" -C \
  -Q "BACKUP DATABASE [ClassBookDb] TO DISK = N'/var/opt/mssql/backup/ClassBookDb.bak' WITH INIT"
```

Перед этим создайте папку для backup внутри контейнера или добавьте отдельный volume под `/var/opt/mssql/backup`.

## Важные замечания

- Не коммитьте `.env`, CSV с доступами и резервные копии базы.
- После первых тестов лучше сменить пароль администратора `1`.
- Для SQL Server в Docker желательно минимум 2 ГБ RAM, комфортнее 4 ГБ.
