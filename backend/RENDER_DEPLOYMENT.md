# Deploying to Render + Supabase

This guide explains how to deploy the Premier League Predictions API using Render (API) and Supabase (Database).

## Prerequisites

1. A [Render](https://render.com) account (free tier)
2. A [Supabase](https://supabase.com) account (free tier)
3. A [Vercel](https://vercel.com) account (free tier)
4. Your repository pushed to GitHub
5. A Football Data API key from [football-data.org](https://www.football-data.org/)
6. A Google OAuth Client ID from [Google Cloud Console](https://console.cloud.google.com/)

## Why This Stack?

- **Supabase**: Free PostgreSQL database (500MB) with no expiration
- **Render**: Free .NET API hosting (spins down after 15min inactivity)
- **Vercel**: Free React frontend hosting with instant deployments

## Deployment Steps

#### 1. Create PostgreSQL Database on Supabase

1. Go to [Supabase Dashboard](https://supabase.com/dashboard)
2. Click "New Project"
3. Configure:
   - **Name**: `premierleague-predictions`
   - **Database Password**: Generate a strong password (save this!)
   - **Region**: Choose closest to you
   - **Plan**: Free
4. Click "Create new project" (takes ~2 minutes to provision)
5. Once ready, go to **Settings** → **Database**
6. Find your connection details:
   - **Host**: `db.xxx.supabase.co`
   - **Database name**: `postgres`
   - **Port**: `5432` (or `6543` for connection pooling)
   - **User**: `postgres`
   - **Password**: The password you created in step 3
7. Format the connection string for .NET (Npgsql format):

   **For Render deployment (recommended - uses connection pooling):**
   - Go to **Settings** → **Database** → **Connection Pooling**
   - Copy the connection string and convert to Npgsql format:
   ```
   Host=aws-1-us-east-1.pooler.supabase.com;Database=postgres;Username=postgres.PROJECT_REF;Port=5432;Pool_Mode=session;Password=YOUR_PASSWORD
   ```

   **For local development (direct connection):**
   ```
   Host=db.xxx.supabase.co;Database=postgres;Username=postgres;Password=YOUR_PASSWORD
   ```

   **Note**: Use the Npgsql format (not the URI `postgresql://` format). The pooler connection is required for Render due to IPv6 connectivity issues.

#### 2. Deploy the API to Render

1. Go to [Render Dashboard](https://dashboard.render.com/) → New → Web Service
2. Connect your GitHub repository
3. Configure:
   - **Name**: `premierleague-api`
   - **Region**: Choose closest to you (preferably same region as Supabase)
   - **Root Directory**: `backend`
   - **Runtime**: Docker
   - **Dockerfile Path**: `./Dockerfile`

4. Add Environment Variables (click "Advanced" to add them):
   ```
   ASPNETCORE_ENVIRONMENT=Production
   ConnectionStrings__DefaultConnection=Host=aws-1-us-east-1.pooler.supabase.com;Database=postgres;Username=postgres.PROJECT_REF;Port=5432;Pool_Mode=session;Password=YOUR_PASSWORD
   JWT__Secret=[Generate a random 32+ character string - use a password generator]
   JWT__Issuer=PremierLeaguePredictions
   JWT__Audience=PremierLeaguePredictions
   JWT__ExpirationInMinutes=43200
   Google__ClientId=[Your Google OAuth Client ID]
   FootballData__ApiKey=[Your Football Data API Key]
   AllowedOrigins__0=http://localhost:5173
   ApiBaseUrl=https://premierleague-api.onrender.com
   GitHub__Owner=[Your GitHub username]
   GitHub__Repository=PremierLeaguePredictions
   GitHub__PersonalAccessToken=[Will be added in step 5]
   ```

   **Important Notes**:
   - Use the Supabase connection pooling string (from Settings → Database → Connection Pooling)
   - Replace `PROJECT_REF` with your actual Supabase project reference (e.g., `postgres.ktnpraboebotzcxvnzlt`)
   - Use the Npgsql connection string format (Host=;Database=;Username=;Password=)
   - Do NOT use the URI format (postgresql://)
   - The pooler connection is required for Render due to IPv6 connectivity issues
   - Set `AllowedOrigins__0` to `http://localhost:5173` for now. You'll update this with your Vercel URL after deploying the frontend.

5. Click "Create Web Service"
6. Wait for the build to complete (~5-10 minutes for first deploy)
7. Copy your API URL (e.g., `https://premierleague-api.onrender.com`)

#### 3. Deploy the Frontend to Vercel

1. Go to [Vercel Dashboard](https://vercel.com/dashboard)
2. Click "Add New" → "Project"
3. Import your GitHub repository
4. Configure:
   - **Framework Preset**: Vite
   - **Root Directory**: `frontend`
   - **Build Command**: `npm run build`
   - **Output Directory**: `dist`

5. Add Environment Variables:
   ```
   VITE_API_URL=https://premierleague-api.onrender.com
   VITE_GOOGLE_CLIENT_ID=[Your Google OAuth Client ID]
   ```

6. Click "Deploy"
7. Copy your Vercel URL (e.g., `https://your-app.vercel.app`)

#### 4. Update CORS Settings

After deploying the frontend to Vercel, go back to your Render API service:
1. Go to Environment tab
2. Update `AllowedOrigins__0` with your Vercel URL
3. Save changes (service will redeploy)

#### 5. Configure GitHub Actions Scheduler

The application uses GitHub Actions for automated tasks (reminders, auto-picks, score syncing).

**5.1. Create GitHub Personal Access Token**

1. Go to [GitHub Settings > Personal Access Tokens > Tokens (classic)](https://github.com/settings/tokens)
2. Click "Generate new token (classic)"
3. Set description: `PremierLeague Scheduler`
4. Select scopes:
   - ✅ `repo` (Full control of private repositories)
   - ✅ `workflow` (Update GitHub Action workflows)
5. Click "Generate token"
6. **Copy the token immediately** (you won't see it again)

**5.2. Add Token to Render Environment**

1. Go to your Render API service
2. Navigate to "Environment" tab
3. Update `GitHub__PersonalAccessToken` with the token you just created
4. Save changes (service will redeploy)

**5.3. Add API Key to GitHub Secrets**

1. Go to your GitHub repository
2. Navigate to Settings > Secrets and variables > Actions
3. Click "New repository secret"
4. Add:
   ```
   Name:  EXTERNAL_SYNC_API_KEY
   Value: [Copy from Render Environment: ExternalSyncApiKey]
   ```

**5.4. Verify Master Scheduler**

1. Check that `.github/workflows/master-scheduler.yml` exists in your repository
2. Go to GitHub repository > Actions tab
3. You should see "Master Scheduler" workflow
4. It will run automatically every Monday at 9 AM UTC
5. Or manually trigger it: Actions > Master Scheduler > Run workflow

**What the Scheduler Does:**
- **Every Monday 9 AM UTC**: Generates weekly schedule based on upcoming gameweeks
- **Throughout the week**: Runs scheduled jobs:
  - Send reminders (24h, 12h, 3h before deadlines)
  - Auto-pick for users who didn't submit picks
  - Sync live scores every 2 minutes during matches

See [DEPLOYMENT.md](../../DEPLOYMENT.md#github-actions-scheduler-setup) for detailed scheduler documentation.

## Post-Deployment

### 1. Update Google OAuth Authorized Origins

In the [Google Cloud Console](https://console.cloud.google.com/):
- Go to APIs & Services → Credentials
- Edit your OAuth 2.0 Client ID
- Add to "Authorized JavaScript origins":
  - Your Vercel URL (e.g., `https://your-app.vercel.app`)
- Add to "Authorized redirect URIs":
  - Your Vercel URL (e.g., `https://your-app.vercel.app`)

### 2. Run Database Migrations

The API automatically runs migrations on startup (configured in Program.cs). Check the Render logs to verify:
- Look for "Applying database migrations..."
- Followed by "Database migrations applied successfully"

If migrations fail, check the connection string and database credentials.

### 3. Initialize Data

1. Log in to the app as an admin
2. Go to Admin panel
3. Click "Sync Teams" to import Premier League teams
4. Click "Sync Current Season Fixtures" to import fixtures and create gameweeks

## Monitoring

- **API Logs**: Render Dashboard → premierleague-api → Logs
- **Database**: Supabase Dashboard → Your project → Database → Query Editor
- **Frontend**: Vercel Dashboard → Your project → Deployments

## Troubleshooting

### API won't start
- Check that all environment variables are set correctly
- Verify the database connection string
- Check API logs for specific errors

### Frontend shows API errors
- Verify `VITE_API_URL` matches your deployed API URL
- Check that CORS is configured correctly in the API
- Ensure API is running and healthy

### Google OAuth fails
- Verify Google OAuth authorized origins include your frontend URL
- Check that `VITE_GOOGLE_CLIENT_ID` matches the backend `Google__ClientId`

### Database connection fails
- Verify the Supabase connection string is correct
- Make sure you replaced `[YOUR-PASSWORD]` with your actual password
- Check that the password doesn't contain special characters that need URL encoding
- Try connecting to your Supabase database using a PostgreSQL client to verify credentials

### Supabase connection pooling issues
- If you get connection errors, make sure you're using Supabase's connection pooling connection string
- In Supabase Dashboard → Settings → Database → Connection Pooling
- Use the Session mode pooler connection string in Npgsql format:
  ```
  Host=aws-1-us-east-1.pooler.supabase.com;Database=postgres;Username=postgres.PROJECT_REF;Port=5432;Pool_Mode=session;Password=YOUR_PASSWORD
  ```
- The direct connection (`db.xxx.supabase.co`) won't work on Render's free tier due to IPv6 connectivity issues

## Free Tier Limitations

**Render (API):**
- Web services sleep after 15 minutes of inactivity (first request takes ~50 seconds to wake up)
- 750 hours/month of runtime
- Build time limit: 15 minutes

**Supabase (Database):**
- 500MB database storage
- No time limit - free forever!
- Automatic backups (7 days retention)
- Connection pooling included

**Vercel (Frontend):**
- 100GB bandwidth/month
- Unlimited deployments
- Automatic HTTPS

## Upgrading

To upgrade from free tier:
- Go to service settings
- Change plan to "Starter" or higher
- Benefit from always-on service and more resources

## Environment Variables Reference

### API Required Variables
| Variable | Description | Example |
|----------|-------------|---------|
| `ASPNETCORE_ENVIRONMENT` | ASP.NET environment | `Production` |
| `ConnectionStrings__DefaultConnection` | PostgreSQL connection string | From Supabase pooler |
| `JWT__Secret` | JWT signing secret | Random 32+ char string |
| `JWT__Issuer` | JWT issuer claim | `PremierLeaguePredictions` |
| `JWT__Audience` | JWT audience claim | `PremierLeaguePredictions` |
| `JWT__ExpirationInMinutes` | Token expiration | `43200` (30 days) |
| `Google__ClientId` | Google OAuth Client ID | From Google Cloud |
| `FootballData__ApiKey` | Football Data API key | From football-data.org |
| `AllowedOrigins__0` | CORS allowed origin | Frontend URL |
| `ApiBaseUrl` | Public API URL | `https://premierleague-api.onrender.com` |
| `GitHub__Owner` | GitHub username | Your GitHub username |
| `GitHub__Repository` | Repository name | `PremierLeaguePredictions` |
| `GitHub__PersonalAccessToken` | GitHub PAT for workflow management | From GitHub settings |

### Frontend Required Variables (Vercel)
| Variable | Description | Example |
|----------|-------------|---------|
| `VITE_API_URL` | Backend API URL | `https://premierleague-api.onrender.com` |
| `VITE_GOOGLE_CLIENT_ID` | Google OAuth Client ID | From Google Cloud |

## Additional Notes

- The first deployment may take 5-10 minutes
- **Free tier API services automatically sleep after 15 minutes of inactivity** - first request takes ~50 seconds to wake up
- Supabase provides automatic backups (7 days) even on free tier
- Vercel provides instant deploys and automatic preview deployments for pull requests
- All three services (Render, Supabase, Vercel) are completely free with no credit card required
- No database expiration with Supabase - free forever!
