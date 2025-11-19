# Premier League Predictions Competition

A web application for running an English Premier League predictions competition. Players select one team per gameweek to win, with the constraint that each team can only be picked once per half-season.

## Tech Stack

### Frontend
- **React** - UI library
- **TypeScript** - Type safety
- **Tailwind CSS** - Utility-first styling
- **shadcn/ui** - Component library
- **React Hook Form** - Form management
- **TanStack Query** - Server state management
- **Vercel** - Hosting

### Backend
- **C# / .NET 9** - Web API
- **Entity Framework Core** - ORM
- **PostgreSQL** - Database
- **Google OAuth** - Authentication
- **JWT** - Authorization
- **Resend** - Email notifications
- **football-data.org API** - Live fixture data
- **Render** - API hosting

## Competition Rules

1. **38 Gameweeks** - One pick per gameweek across the entire Premier League season
2. **Pick Once Rule** - Each team can only be selected once per half-season
   - Weeks 1-20: Pick each of the 20 teams once
   - Weeks 21-38: Teams reset, pick each team once again
3. **Scoring System**:
   - Win: 3 points
   - Draw: 1 point
   - Loss: 0 points
   - Goal difference is tracked as a tiebreaker
4. **Deadline** - Picks lock when the first fixture of a gameweek kicks off
5. **Auto-Assignment** - If no pick is made, the lowest-ranked available team is automatically assigned
6. **Winner** - Player with the most points after 38 weeks wins

## Features

### User Features
- **Dashboard** with live standings and personal stats
- **Pick Selection** with visual team availability status
- **Fixtures View** showing all gameweek matches with color-coded pick status
- **League Table** ranking all players
- **Email Reminders** sent 24 hours before deadline

### Admin Features
- User management (activate/deactivate, mark as paid)
- Override picks
- Override deadlines
- Manage fixtures
- Season management (create, archive)
- Audit log of all admin actions

## Project Structure

```
PremierLeaguePredictions/
├── backend/
│   └── PremierLeaguePredictions.API/
│       ├── Models/              # Domain entities
│       ├── Data/                # DbContext & migrations
│       ├── DTOs/                # Data transfer objects
│       ├── Controllers/         # API endpoints
│       ├── Services/            # Business logic
│       └── Configuration/       # App settings
├── frontend/                    # React application (to be created)
├── database/
│   ├── schema.sql              # PostgreSQL schema
│   └── README.md               # Database documentation
└── README.md
```

## Getting Started

### Prerequisites

- [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)
- [Node.js 18+](https://nodejs.org/)
- [PostgreSQL 15+](https://www.postgresql.org/download/)
- [Git](https://git-scm.com/downloads)

### Database Setup

1. Create a PostgreSQL database:
   ```bash
   createdb premier_league_predictions
   ```

2. Run the schema:
   ```bash
   psql -U your_username -d premier_league_predictions -f database/schema.sql
   ```

### Backend Setup

1. Navigate to the backend directory:
   ```bash
   cd backend/PremierLeaguePredictions.API
   ```

2. Create `appsettings.Development.json`:
   ```json
   {
     "ConnectionStrings": {
       "DefaultConnection": "Host=localhost;Database=premier_league_predictions;Username=your_username;Password=your_password"
     },
     "Authentication": {
       "Google": {
         "ClientId": "your-google-client-id",
         "ClientSecret": "your-google-client-secret"
       },
       "Jwt": {
         "SecretKey": "your-jwt-secret-key-min-32-chars",
         "Issuer": "PremierLeaguePredictions",
         "Audience": "PremierLeaguePredictionsUsers",
         "ExpiryMinutes": 60
       }
     },
     "FootballData": {
       "ApiKey": "your-football-data-org-api-key"
     },
     "Resend": {
       "ApiKey": "your-resend-api-key"
     }
   }
   ```

3. Run the API:
   ```bash
   dotnet run
   ```

The API will be available at `https://localhost:5001` (or the port shown in console).

### Frontend Setup

```bash
cd frontend
npm install
npm run dev
```

The frontend will be available at `http://localhost:5173`.

## API Keys Setup

### Google OAuth
1. Go to [Google Cloud Console](https://console.cloud.google.com/)
2. Create a new project
3. Enable Google+ API
4. Create OAuth 2.0 credentials
5. Add authorized redirect URIs

### football-data.org
1. Register at [football-data.org](https://www.football-data.org/)
2. Get your free API key (10 calls/minute)

### Resend
1. Sign up at [resend.com](https://resend.com/)
2. Get your API key (3,000 emails/month free)

## Environment Variables

### Backend (.NET)
See `appsettings.Development.json` template above.

### Frontend (React)
Create `.env.local`:
```
VITE_API_URL=http://localhost:5000
VITE_GOOGLE_CLIENT_ID=your-google-client-id
```

## Deployment

### Backend (Render)
1. Connect GitHub repository to Render
2. Create new Web Service
3. Set build command: `dotnet publish -c Release -o out`
4. Set start command: `cd out && dotnet PremierLeaguePredictions.API.dll`
5. Add environment variables from `appsettings.json`

### Frontend (Vercel)
1. Connect GitHub repository to Vercel
2. Set framework preset to React
3. Add environment variables
4. Deploy

### Database (Render PostgreSQL)
1. Create PostgreSQL instance on Render
2. Copy connection string
3. Run migrations/schema
4. Update backend environment variables

## Development Workflow

1. Create a feature branch: `git checkout -b feature/your-feature`
2. Make changes and commit: `git commit -m "Add feature"`
3. Push to GitHub: `git push origin feature/your-feature`
4. Create a Pull Request

## Database Schema

See [database/README.md](database/README.md) for detailed schema documentation.

## Contributing

This is a private competition application. For bugs or feature requests, please open an issue.

## License

Private - All Rights Reserved

## Support

For questions or issues, please contact the repository owner.

---

**Status**: 🚧 In Development

**Current Version**: 0.1.0 (Initial Setup)
