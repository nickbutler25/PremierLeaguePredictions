# Database Schema

## Setup Instructions

1. Install PostgreSQL if not already installed
2. Create a new database:
   ```sql
   CREATE DATABASE premier_league_predictions;
   ```
3. Run the schema:
   ```bash
   psql -U your_username -d premier_league_predictions -f schema.sql
   ```

## Tables Overview

### users
Stores user information including authentication details, profile info, and admin/payment status.

### seasons
Manages different competition seasons with active/archived status.

### teams
Premier League teams with external API references for syncing fixture data.

### gameweeks
38 gameweeks per season with deadlines and lock status.

### fixtures
Individual matches with scores and status tracking.

### picks
User selections for each gameweek with calculated points and goal difference.

### team_selections
Tracks which teams have been used in each half of the season (enforces pick-once rule).

### email_notifications
Logs all emails sent to users for auditing.

### admin_actions
Audit trail of all administrative actions (overrides, user management, etc).

## Key Constraints

- Users can only pick one team per gameweek
- Teams can only be picked once per half-season (weeks 1-20, then 21-38)
- Week numbers must be between 1 and 38
- Half values must be 1 or 2

## Connection String Format

```
Host=localhost;Database=premier_league_predictions;Username=your_username;Password=your_password
```
