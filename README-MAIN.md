Green & Tasty: деплой та груповий pre-order
Навчальний проєкт IT Marathon: ресторанний додаток на AWS (Lambda, API Gateway, DynamoDB, S3, CloudFront) з Angular-фронтендом. Тут описано, що було розгорнуто, що реалізовано зверх готового архіву, як це перевірялося та як повторити.
Сайт: https://d2i35mrxvqur44.cloudfront.net
Регіон AWS: us-west-2
Що зроблено
Блок | Результат
Деплой інфраструктури | Terraform: S3 для фронтенда, CloudFront, політики доступу, сповіщення (SNS, Budget)
Деплой бекенду | AWS Syndicate: 3 Lambda, API Gateway restaurant-backend-api (stage develop), 13 таблиць DynamoDB
Деплой фронтенда | Angular-збірка в S3, роздача через CloudFront
Бекенд, груповий pre-order | 3 рівні складності (див. нижче) та виправлення знайденої помилки
Фронтенд, екран Owner | Практичне, рівні 1-3 та бонус Remove Guest
Автотести (AQA) | BDD + Screenplay: Reqnroll, TUnit, Playwright для .NET
Тестові дані | Локації, столи та страви в DynamoDB (скрипт завантаження з фіксованими id)
Архітектура
Браузер ──► CloudFront ──► S3 (Angular, приватний бакет)
│
└──────► API Gateway (REST, /develop) ──► Lambda api-handler (.NET)
├─► Lambda reports-handler
└─► Lambda report-sender
│
└─► DynamoDB (Dishes, Locations, Tables, Reservations,
GroupReservations, GuestSessions, Orders, ...)
Три способи розгортання в одному проєкті:

Terraform (infra/terraform): загальна інфраструктура, стан у версіонованому S3-бакеті.

AWS Syndicate (backend/restaurant-backend-app): Lambda, API Gateway, DynamoDB.

aws s3 sync: фронтенд (frontend/restaurant-frontend-app).

Деплой
Порядок: Terraform, потім бекенд, потім фронтенд. Усі команди у PowerShell.

Terraform
$ACCOUNT_ID = aws sts get-caller-identity --query Account --output text

бакет для state (один раз): restaurant-terraform-state-$ACCOUNT_ID, versioning, block public access
cd infra/terraform
terraform init
terraform validate
terraform plan
terraform apply -auto-approve
У backend.tf вказано ім'я state-бакета, у variables.tf заповнені account_id та alert_emails.
2. Бекенд (Syndicate)
cd backend/restaurant-backend-app
python -m venv syndicate_venv
.\syndicate_venv\Scripts\Activate.ps1
pip install aws-syndicate
$env:SDCT_CONF = (Resolve-Path "..syndicate-config-restaurant-backend-app").Path
$env:PYTHONUTF8 = "1"
syndicate create-deploy-target-bucket
syndicate build
syndicate deploy --deploy_name dev      # далі: syndicate update
3. Фронтенд
$env:BACKEND_URL = "https://.execute-api.us-west-2.amazonaws.com/develop"
cd frontend/restaurant-frontend-app
npm ci
npm run build
aws s3 sync dist/restaurant-frontend-app/browser s3://restaurant-frontend-$ACCOUNT_ID/ --delete
aws cloudfront create-invalidation --distribution-id  --paths "/*"
З якими проблемами зіткнулися
Проблема | Вирішення
CloudFront: Your account must be verified… (403) | Звернення до AWS Support; після верифікації повторили terraform apply
Syndicate: шлях довший за 260 символів (.NET-збірка) | Робоча копія у короткому шляху C:\m
UnicodeDecodeError при збірці на Windows | $env:PYTHONUTF8 = "1"
PowerShell блокує Activate.ps1 та npm | Set-ExecutionPolicy -Scope Process -ExecutionPolicy Bypass
Після деплою фронтенда редирект на /main | У бакеті лежала стара збірка: перезібрали, завантажили, скинули кеш CloudFront
Бекенд: груповий pre-order
Owner бронювання запрошує гостей, гості збирають свої кошики, Owner бачить усе разом і надсилає в ресторан одне замовлення.
Рівень | Ендпоинт | Що робить
1 | GET /guest/groups/{groupReservationId}/sessions | Учасники, їхні страви, суми та загальний підсумок
2 | GET /reservations/{reservationId}/guest-session-id | Id owner-сесії
2 | POST /group-reservations/{groupReservationId}/submit | Консолідоване підтвердження: тільки підтверджені кошики, однакові страви об'єднуються
3 | DELETE /guest/groups/{groupReservationId}/sessions/{guestSessionId} | Видалення учасника, відповідь: оновлений список та сума
Правила видалення учасника (рівень 3):
Ситуація | Відповідь
Не Owner | 403
Немає токена | 401
Групу або учасника не знайдено | 404
Учасник з іншої групи | 400
Успіх | 200 зі знімком групи (participants, total)
Знайдена помилка
Автотест показав, що GET /reservations/{id}/guest-session-id завжди відповідав «Owner guest session was not found». Причина: owner-сесія створювалася лише в UpdateReservationAsync, до якого не доходить жоден маршрут, а POST /reservations створював групу без неї.
Виправлення (ReservationService.cs): для клієнтської броні на 2+ гостей використовується CreateGroupReservationIfDoesNotExistAsync, який створює групу та owner-сесію ідемпотентно. Для броней без клієнта поведінка колишня. Додано unit-тести (ідемпотентність, 2+ гостей, 1 гість, бронь без клієнта). Результат: dotnet test 153 із 153.
Фронтенд: екран Owner
Angular, standalone-компоненти, signals, inject().

Практичне: картка учасника та сторінка списку.

Рівень 1: верстка за макетом (шапка з адресою, датою та «4 of 5 guests», Grand Total).

Рівень 2: GroupOrdersService, стани loading / error / empty, «My Pre-Order», консолідоване замовлення (consolidatePreOrder), Grand Total лише по підтверджених кошиках, плашка про непідтверджені, діалог Submit, режим read-only після надсилання.

Рівень 3: фонова синхронізація кожні 15 секунд (exhaustMap, без лоадера на весь екран), «Updated at», кнопка Refresh, «Couldn't refresh…» при помилці, зупинка після Submit та при виході зі сторінки.

Бонус: Remove Guest з діалогом підтвердження та aria-label.
Маршрут: /reservations/:reservationId/group-order. Режим ?demo з моками доступний тільки при npm start.
Перевірки: npm run test:hw показує 67 із 67 тестів, також проходять tsc (app та spec) і production build.

Автотести (AQA)
aqa/restaurant-aqa-framework: BDD + Screenplay (Reqnroll + TUnit + Playwright для .NET). Напрямок 1: груповий pre-order, видалення учасника.
Теги | Що перевіряє
@api @group-preorder | 9 API-сценаріїв: видалення одного з кількох, видалення того, хто підтвердив, видалення останнього (participants: [], total: 0), учасник чужої групи (400), неіснуючий (404), не Owner (403 для гостя, клієнта, офіціанта), без токена (401)
@ui @group-preorder-ui | Сквозний сценарій: гості додають страви за запрошенням, Owner переглядає, видаляє одного, підтверджує, офіціант бачить замовлення без видаленого
cd aqa/restaurant-aqa-framework
Copy-Item .env.example .env       # заповнити своїми значеннями; не комітиться
dotnet build
dotnet run --no-build -- --treenode-filter "////[Category=group-preorder]"
dotnet run --no-build -- --treenode-filter "////[Category=group-preorder-ui]"
Звіти та trace-файли Playwright лежать у TestResults/.
Виправлено по ходу: очікування load на сторінці /login призводило до тайм-ауту через важкі картинки. Тепер використовується DOMContentLoaded та очікування поля Email.
Обмеження: користувачі AQA-Sample-… залишаються в Cognito, так як видалити їх без адмін-токена не можна.
Використання AI
Claude (Anthropic) використовувався як помічник та агент (Claude Code):

діагностика помилок деплою на Windows;

написання фронтенду за завданням та готовими тестами;

AQA-сценарії, виправлення очікування логіну;

пошук причини збою owner-сесії та правка бекенду;

генерація тестових даних для DynamoDB.
Як перевірявся результат: запуск тестів (npm run test:hw, dotnet test, AQA) самостійно, перегляд git diff перед кожним деплоєм, перевірка маршрутів і дат Lambda в AWS, ручна перевірка екрана та відповідей API.

Очищення ресурсів
Порядок зворотний деплою:
aws s3 rm s3://restaurant-frontend-$ACCOUNT_ID/ --recursive
cd backend/restaurant-backend-app ; syndicate clean
cd ../../infra/terraform ; terraform destroy -auto-approve

потім вручну: state-бакет (з версіями) та бакет артефактів Syndicate
Структура репозиторію
backend/restaurant-backend-app/   # .NET Lambda, Syndicate-конфігурація
frontend/restaurant-frontend-app/ # Angular, homework-tests/
infra/terraform/                  # S3, CloudFront, сповіщення
aqa/restaurant-aqa-framework/     # автотести
homework-design/                  # макети
