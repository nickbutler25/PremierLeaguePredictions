# Deploying to Render

This guide explains how to deploy the Premier League Predictions API to Render's free tier.

## Prerequisites

1. A [Render](https://render.com) account (free tier)
2. Your repository pushed to GitHub
3. A Football Data API key from [football-data.org](https://www.football-data.org/)
4. A Google OAuth Client ID from [Google Cloud Console](https://console.cloud.google.com/)

## Important Note

**Blueprints require paid plans on Render.** This guide uses the manual setup approach which works with the free tier.

## Deployment Steps

#### 1. Create PostgreSQL Database

1. Go to Render Dashboard → New → PostgreSQL
2. Name: `premierleague-db`
3. Database: `premierleague`
4. User: `premierleague_user` (or any name)
5. Region: Choose closest to you
6. Plan: Free
7. Click "Create Database"
8. Copy the "Internal Database URL" for later

#### 2. Deploy the API

1. Go to Render Dashboard → New → Web Service
2. Connect your repository
3. Configure:
   - **Name**: `premierleague-api`
   - **Region**: Same as database
   - **Root Directory**: `backend`
   - **Runtime**: Docker
   - **Plan**: Free
   - **Dockerfile Path**: `./Dockerfile`

4. Add Environment Variables:
   ```
   ASPNETCORE_ENVIRONMENT=Production
   ConnectionStrings__DefaultConnection=[Paste Internal Database URL]
   JWT__Secret=[Generate a random 32+ character string]
   JWT__Issuer=PremierLeaguePredictions
   JWT__Audience=PremierLeaguePredictions
   JWT__ExpirationInMinutes=43200
   Google__ClientId=[Your Google OAuth Client ID]
   FootballData__ApiKey=[Your Football Data API Key]
   AllowedOrigins__0=[Your Vercel frontend URL - you'll update this after deploying frontend]
   ```

5. Click "Create Web Service"
6. Copy your API URL (e.g., `https://premierleague-api.onrender.com`)

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
- **Database**: Render Dashboard → premierleague-db → Connect
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
- Use the "Internal Database URL" from Render (not External)
- Ensure the API and database are in the same region for best performance

## Free Tier Limitations

Render's free tier includes:
- Web services sleep after 15 minutes of inactivity (first request after waking takes ~1 minute)
- 750 hours/month of runtime
- PostgreSQL database limited to 1GB storage

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
- Database backups are available on paid plans only
- Vercel provides instant deploys and automatic preview deployments for pull requests
