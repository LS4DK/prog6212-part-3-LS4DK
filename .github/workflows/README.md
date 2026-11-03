# RaceDay API — Part 2 (PROG6212/w POE)

## Overview
RaceDay is an event management platform for South African road running, walking and cycling events. This repository contains **Part 2**: the RESTful API, built with ASP.NET Core Web API (C#, .NET 10), Entity Framework Core (code-first) and SQL Server. It implements the endpoint plan from Part 1, enforces role-based access, is documented with Swagger, and is covered by automated unit tests that run in GitHub Actions on every push.

## The two roles
- **Organiser** — creates, edits and deletes their own events; defines categories for their events; views everyone enrolled in their events; captures and corrects finish times and positions.
- **Participant** — browses events and categories; enrols in an event by choosing a category; views their own enrolments; views their own results (including "47th out of 312 finishers").

Both roles can register, log in, log out, and view and update their own profile. Anyone (even when not logged in) can browse events and categories.

## Video presentation
Unlisted YouTube link: <!-- paste your link here -->

## CI/CD
GitHub Actions builds the solution and runs every unit test on each push. Latest green run:

![Green build](ci-green-build.png)

The workflow is in `.github/workflows/part2-build-and-test.yml`. It validates the repository structure (solution, API project, test project, EF migrations), restores, builds in Release mode, runs the tests and uploads the test report.

## Tech stack
- ASP.NET Core Web API (.NET 10, controllers)
- Entity Framework Core (code-first migrations) with SQL Server
- Server-side session authentication (user ID and role stored in the session)
- Passwords hashed with PBKDF2 (random salt per password, 600,000 iterations)
- Swagger UI for live documentation and testing
- xUnit with `WebApplicationFactory` and the EF Core in-memory provider for tests

## Repository structure
```
RaceDay/
├── RaceDay.sln(x)
├── RaceDay.Api/
│   ├── Auth/            Session role filter ([SessionAuthorize]) and helpers
│   ├── Controllers/     Auth, Users, Events, Categories, Enrolments, Results
│   ├── Data/            RaceDayDbContext (fluent configuration matching the Part 1 schema)
│   ├── Dtos/            Request and response classes (the password hash is never returned)
│   ├── Migrations/      EF Core migrations
│   ├── Models/          Entities: Role, User, Event, Category, Enrolment, Result
│   ├── Services/        PasswordService, SessionKeys
│   ├── appsettings.json Connection string
│   └── Program.cs
└── RaceDay.Api.Tests/   Unit tests
.github/workflows/       CI workflow
```

## How to run
**Prerequisites:** Visual Studio with the ASP.NET workload, the .NET 10 SDK, and SQL Server (LocalDB or Express).

1. Clone the repository and open the solution in `RaceDay/` in Visual Studio.
2. Open `RaceDay/RaceDay.Api/appsettings.json` and make sure `Server=` in `DefaultConnection` matches your SQL Server instance (the default uses `(localdb)\MSSQLLocalDB`). The database is created as `RaceDayApi`.
3. Open **Tools → NuGet Package Manager → Package Manager Console**, set the default project to `RaceDay.Api`, and run:
   ```
   Update-Database
   ```
   This creates the six tables and seeds the two roles (Organiser, Participant).
4. Press **F5**. The console shows the HTTPS address the API is listening on (for example `https://localhost:7241`).
5. Open `https://localhost:7241/swagger` in a browser (use the port shown in your console).

## Trying it in Swagger
1. `POST /api/auth/register` — create an Organiser (`"role": "Organiser"`).
2. `POST /api/auth/login` — log in. The session cookie is then sent automatically with every request from the Swagger page.
3. `POST /api/events`, then `POST /api/events/{eventId}/categories` — create an event and a category.
4. `POST /api/auth/logout`, then register and log in as a Participant (`"role": "Participant"`).
5. `POST /api/events/{eventId}/enrolments` with the category ID — enrol.
6. Log back in as the Organiser and use `GET /api/events/{eventId}/enrolments` and `POST /api/enrolments/{enrolmentId}/result`; then log in as the Participant and call `GET /api/users/me/results`.

Status codes used: 200/201/204 success, 400 invalid input, **401 not logged in**, **403 logged in but wrong role or not the owner**, 404 not found, 409 conflict.

## Endpoints (19)
| Method | Route | Who can call it |
|---|---|---|
| POST | `/api/auth/register` | Anyone |
| POST | `/api/auth/login` | Anyone |
| POST | `/api/auth/logout` | Anyone |
| GET | `/api/users/me` | Any logged-in user |
| PUT | `/api/users/me` | Any logged-in user |
| GET | `/api/events` | Anyone |
| GET | `/api/events/{id}` | Anyone |
| POST | `/api/events` | Organiser |
| PUT | `/api/events/{id}` | Organiser (owner of the event) |
| DELETE | `/api/events/{id}` | Organiser (owner of the event) |
| GET | `/api/events/{eventId}/categories` | Anyone |
| POST | `/api/events/{eventId}/categories` | Organiser (owner of the event) |
| DELETE | `/api/categories/{id}` | Organiser (owner of the event) |
| POST | `/api/events/{eventId}/enrolments` | Participant |
| GET | `/api/users/me/enrolments` | Participant |
| GET | `/api/events/{eventId}/enrolments` | Organiser (owner of the event) |
| POST | `/api/enrolments/{enrolmentId}/result` | Organiser (owner of the event) |
| PUT | `/api/enrolments/{enrolmentId}/result` | Organiser (owner of the event) |
| GET | `/api/users/me/results` | Participant |

## Authentication and role-based access
- On login the server stores the user's ID and role in a server-side session; the browser only holds a session cookie.
- The `[SessionAuthorize]` filter runs before each protected action: no session returns **401**, the wrong role returns **403**.
- Ownership is checked inside the controllers: an Organiser can only change, delete or view enrolments for events they created.
- Passwords are never stored in plain text and are never returned by the API.

## Unit tests
The tests start the real API in memory with an in-memory database, log in through the real login endpoint, and call the endpoints over HTTP. They cover:
- registration and login (success and failure cases, hashed passwords, sessions, logout, profile update)
- event management by Organisers, including ownership rules
- categories
- role-based access: every protected endpoint returns 401 when not logged in, and 403 for the wrong role
- event enrolments (enrolling, duplicates, wrong category, who can see what)
- results (capturing, correcting, viewing own results)

To run them: open **Test → Test Explorer → Run All Tests** in Visual Studio, or from the `RaceDay` folder run `dotnet test`.

## Deviations from the Part 1 plan
| Change | Reason |
|---|---|
| Added `POST /api/auth/logout` | Part 3 requires users to be able to end their session. |
| `GET /api/events` returns upcoming events only, with an optional `includePast=true` query parameter | Organisers need to see past events to capture results. |
| `DELETE /api/events/{id}` returns 409 if the event has enrolments | Protects enrolment and result history (matches the no-cascade-delete decision in the SQL script). |
| `POST /api/events/{eventId}/categories` returns 409 for a duplicate category name | Backed by the unique index on (EventId, Name). |
| `PUT /api/users/me` returns 409 if the email belongs to another account | Emails must stay unique. |
| Database is named `RaceDayApi` (Part 1 script created `RaceDay`) | Avoids clashing with the Part 1 database; the schema follows the Part 1 script. |
| Column names follow the SQL script (`RoleId`, `RoleName`) rather than the ERD image (`RoleID`, `Name`) | The SQL script is the executable version of the design. |
| `Enrolment.Status` defaults to `Confirmed` in C# instead of a database default; EF Core names the indexes itself | EF Core code-first conventions. |

## AI tool disclosure
<!-- Edit this so it is accurate for what YOU did. -->
I used Claude (Anthropic) throughout Part 2 as a step-by-step guide. It provided starting code and explanations for the entities, DbContext, authentication and session handling, role filter, controllers, Swagger configuration, unit tests and the CI workflow. I added the code to the project, built it, ran the tests and checked the behaviour in Swagger. <!-- Add anything you changed, chose or debugged yourself. -->
