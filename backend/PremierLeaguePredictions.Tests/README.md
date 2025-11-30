# Integration Tests Setup

## Prerequisites

You need a PostgreSQL test database running. The tests expect the following connection:

- **Host**: localhost
- **Port**: 5433
- **Database**: plp_test
- **Username**: testuser
- **Password**: testpass

## Option 1: Docker (Recommended)

If you have Docker installed, run:

```bash
cd backend
docker compose -f docker-compose.test.yml up -d
```

To stop the database:

```bash
docker compose -f docker-compose.test.yml down
```

To stop and remove all data:

```bash
docker compose -f docker-compose.test.yml down -v
```

## Option 2: Manual PostgreSQL Setup

If you don't have Docker, you can set up a test database manually:

1. Install PostgreSQL if not already installed
2. Create a test database and user:

```sql
CREATE USER testuser WITH PASSWORD 'testpass';
CREATE DATABASE plp_test OWNER testuser;
GRANT ALL PRIVILEGES ON DATABASE plp_test TO testuser;
```

3. Make sure PostgreSQL is listening on port 5433, or update the connection string in `TestWebApplicationFactory.cs`

## Running the Tests

Once the test database is running:

```bash
cd backend/PremierLeaguePredictions.Tests
dotnet test
```

Or run specific test classes:

```bash
dotnet test --filter "FullyQualifiedName~EliminationManagementTests"
dotnet test --filter "FullyQualifiedName~CreateSeasonTests"
dotnet test --filter "FullyQualifiedName~DashboardNoActiveSeasonTests"
```

## How Integration Tests Work

The `TestWebApplicationFactory` class:

1. Replaces the application's database configuration with the test database
2. Runs all EF Core migrations on startup to ensure schema is up-to-date
3. Deletes and recreates the database before each test run for isolation

Each test:

1. Gets a fresh database (migrations already applied)
2. Seeds its own test data
3. Executes the test
4. Database is cleaned up before the next test

This approach ensures:
- Tests use the real database provider (PostgreSQL)
- Migrations are tested
- Tests are isolated from each other
- Tests run against a realistic environment
