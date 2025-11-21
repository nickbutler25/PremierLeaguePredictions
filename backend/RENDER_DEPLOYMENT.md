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
6. Scroll down to **Connection String** → **URI**
7. Copy the connection string (it looks like: `postgresql://postgres:[YOUR-PASSWORD]@db.xxx.supabase.co:5432/postgres`)
8. Replace `[YOUR-PASSWORD]` with the password you created in step 3

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
   ConnectionStrings__DefaultConnection=[Paste your Supabase connection string]
   JWT__Secret=[Generate a random 32+ character string - use a password generator]
   JWT__Issuer=PremierLeaguePredictions
   JWT__Audience=PremierLeaguePredictions
   JWT__ExpirationInMinutes=43200
   Google__ClientId=[Your Google OAuth Client ID]
   FootballData__ApiKey=[Your Football Data API Key]
   AllowedOrigins__0=http://localhost:5173
   ```

   **Note**: Set `AllowedOrigins__0` to `http://localhost:5173` for now. You'll update this with your Vercel URL after deploying the frontend.

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

The API should automatically run migrations on startup. Check the API logs to verify.

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
- If you get connection errors, try using Supabase's connection pooling URL
- In Supabase Dashboard → Settings → Database, use the "Connection pooling" URI instead
- This uses port 6543 instead of 5432 and handles connections better

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
| `ConnectionStrings__DefaultConnection` | PostgreSQL connection string | From Render database |
| `JWT__Secret` | JWT signing secret | Random 32+ char string |
| `JWT__Issuer` | JWT issuer claim | `PremierLeaguePredictions` |
| `JWT__Audience` | JWT audience claim | `PremierLeaguePredictions` |
| `JWT__ExpirationInMinutes` | Token expiration | `43200` (30 days) |
| `Google__ClientId` | Google OAuth Client ID | From Google Cloud |
| `FootballData__ApiKey` | Football Data API key | From football-data.org |
| `AllowedOrigins__0` | CORS allowed origin | Frontend URL |

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
