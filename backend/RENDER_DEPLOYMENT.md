# Deploying to Render

This guide explains how to deploy the Premier League Predictions API to Render.

## Prerequisites

1. A [Render](https://render.com) account
2. Your repository pushed to GitHub
3. A Football Data API key from [football-data.org](https://www.football-data.org/)
4. A Google OAuth Client ID from [Google Cloud Console](https://console.cloud.google.com/)

## Deployment Steps

### Option 1: Using render.yaml (Recommended)

1. **Connect Your Repository**
   - Go to [Render Dashboard](https://dashboard.render.com/)
   - Click "New" → "Blueprint"
   - Connect your GitHub repository
   - Render will automatically detect the `render.yaml` file

2. **Configure Environment Variables**

   After the blueprint is created, set these environment variables in the Render dashboard:

   **For the API service (premierleague-api):**
   - `Google__ClientId`: Your Google OAuth Client ID
   - `FootballData__ApiKey`: Your Football Data API key
   - `AllowedOrigins__0`: Your frontend URL (e.g., `https://premierleague-frontend.onrender.com`)

   **For the Frontend service (premierleague-frontend):**
   - `VITE_GOOGLE_CLIENT_ID`: Same Google OAuth Client ID
   - `VITE_API_URL`: Your API URL (e.g., `https://premierleague-api.onrender.com`)

3. **Deploy**
   - Click "Apply" to deploy all services
   - Render will:
     - Create a PostgreSQL database
     - Build and deploy the .NET API
     - Build and deploy the React frontend

### Option 2: Manual Setup

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
   AllowedOrigins__0=[Your frontend URL when deployed]
   ```

5. Click "Create Web Service"

#### 3. Deploy the Frontend

1. Go to Render Dashboard → New → Static Site
2. Connect your repository
3. Configure:
   - **Name**: `premierleague-frontend`
   - **Root Directory**: `frontend`
   - **Build Command**: `npm install && npm run build`
   - **Publish Directory**: `dist`

4. Add Environment Variables:
   ```
   VITE_API_URL=https://premierleague-api.onrender.com
   VITE_GOOGLE_CLIENT_ID=[Your Google OAuth Client ID]
   ```

5. Add Rewrite Rule for SPA:
   - Source: `/*`
   - Destination: `/index.html`
   - Action: Rewrite

6. Click "Create Static Site"

## Post-Deployment

### 1. Update Google OAuth Authorized Origins

In the [Google Cloud Console](https://console.cloud.google.com/):
- Go to APIs & Services → Credentials
- Edit your OAuth 2.0 Client ID
- Add to "Authorized JavaScript origins":
  - `https://premierleague-frontend.onrender.com`
- Add to "Authorized redirect URIs":
  - `https://premierleague-frontend.onrender.com`

### 2. Update CORS Origins

Update the API's `AllowedOrigins__0` environment variable with your actual frontend URL.

### 3. Run Database Migrations

The API should automatically run migrations on startup. Check the API logs to verify.

### 4. Initialize Data

1. Log in to the app as an admin
2. Go to Admin panel
3. Click "Sync Teams" to import Premier League teams
4. Click "Sync Current Season Fixtures" to import fixtures and create gameweeks

## Monitoring

- **API Logs**: Dashboard → premierleague-api → Logs
- **Database**: Dashboard → premierleague-db → Connect
- **Frontend**: Dashboard → premierleague-frontend → Logs

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

### Frontend Required Variables
| Variable | Description | Example |
|----------|-------------|---------|
| `VITE_API_URL` | Backend API URL | `https://premierleague-api.onrender.com` |
| `VITE_GOOGLE_CLIENT_ID` | Google OAuth Client ID | From Google Cloud |

## Additional Notes

- The first deployment may take 5-10 minutes
- Free tier services automatically sleep after 15 minutes of inactivity
- Database backups are available on paid plans only
- Consider using Render's Infrastructure as Code (render.yaml) for version-controlled deployments
