# Green & Tasty: деплой та груповий pre-order

Навчальний проєкт IT Marathon: ресторанний додаток на AWS (Lambda, API Gateway, DynamoDB, S3, CloudFront) з Angular-фронтендом. Тут описано, що було розгорнуто, що реалізовано зверх готового архіву, як це перевірялося та як повторити.

**Сайт:** https://d2i35mrxvqur44.cloudfront.net  
**Регіон AWS:** `us-west-2`

---

## Що зроблено

| Блок | Результат |
| --- | --- |
| **Деплой інфраструктури** | Terraform: S3 для фронтенда, CloudFront, політики доступу, сповіщення (SNS, Budget) |
| **Деплой бекенду** | AWS Syndicate: 3 Lambda, API Gateway `restaurant-backend-api` (stage `develop`), 13 таблиць DynamoDB |
| **Деплой фронтенда** | Angular-збірка в S3, роздача через CloudFront |
| **Бекенд, груповий pre-order** | 3 рівні складності (див. нижче) та виправлення знайденої помилки |
| **Фронтенд, екран Owner** | Практичне, рівні 1-3 та бонус Remove Guest |
| **Автотести (AQA)** | BDD + Screenplay: Reqnroll, TUnit, Playwright для .NET |
| **Тестові дані** | Локації, столи та страви в DynamoDB (скрипт завантаження з фіксованими id) |

---

## Архітектура

```text
Браузер ──► CloudFront ──► S3 (Angular, приватний бакет)
   │
   └──────► API Gateway (REST, /develop) ──► Lambda api-handler (.NET)
                                           ├─► Lambda reports-handler
                                           └─► Lambda report-sender
                                                    │
                                                    └─► DynamoDB (Dishes, Locations, Tables, Reservations,
                                                         GroupReservations, GuestSessions, Orders, ...)
