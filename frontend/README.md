# Premier League Predictions - Frontend

A modern React application for the Premier League Predictions competition. Built with React, TypeScript, Tailwind CSS, and shadcn/ui.

## Tech Stack

- **React 18** - UI library
- **TypeScript** - Type safety
- **Vite** - Build tool and dev server
- **Tailwind CSS 3.4** - Utility-first styling
- **shadcn/ui** - Component library
- **React Router v6** - Client-side routing
- **TanStack Query** - Server state management
- **React Hook Form** - Form handling
- **Zod** - Schema validation
- **Axios** - HTTP client
- **Lucide React** - Icon library
- **date-fns** - Date utilities

## Prerequisites

Before you begin, ensure you have the following installed:

- **Node.js** 18.x or higher ([Download](https://nodejs.org/))
- **npm** 9.x or higher (comes with Node.js)
- A modern web browser (Chrome, Firefox, Edge, Safari)

You can verify your installation by running:

```bash
node --version
npm --version
```

## Getting Started

### 1. Clone the Repository

```bash
git clone https://github.com/nickbutler25/PremierLeaguePredictions.git
cd PremierLeaguePredictions/frontend
```

### 2. Install Dependencies

Install all required npm packages:

```bash
npm install
```

This will install all dependencies listed in `package.json`, including:
- React and React DOM
- TypeScript and related types
- Tailwind CSS and plugins
- All UI libraries and utilities

### 3. Environment Configuration

Create a `.env.local` file in the `frontend` directory:

```bash
cp .env.example .env.local
```

Edit `.env.local` and add your configuration:

```env
VITE_API_URL=http://localhost:5000
VITE_GOOGLE_CLIENT_ID=your-google-client-id-here
```

**Environment Variables:**

| Variable | Description | Required | Default |
|----------|-------------|----------|---------|
| `VITE_API_URL` | Backend API base URL | Yes | `http://localhost:5000` |
| `VITE_GOOGLE_CLIENT_ID` | Google OAuth Client ID | Yes for auth | - |

**Getting Google OAuth Client ID:**

This is required for Google Sign-In functionality. Follow these detailed steps:

#### Step 1: Access Google Cloud Console

1. Go to [Google Cloud Console](https://console.cloud.google.com/)
2. Sign in with your Google account (use an organizational account if available)

#### Step 2: Create a New Project

1. Click the project dropdown at the top of the page (next to "Google Cloud")
2. Click "New Project" in the top-right of the dialog
3. Enter project details:
   - **Project name**: "Premier League Predictions" (or your preferred name)
   - **Organization**: Select if you have one, or leave as "No organization"
   - **Location**: Leave as default or choose your organization
4. Click "Create"
5. Wait for the project to be created (this may take a minute)
6. Once created, make sure your new project is selected in the project dropdown

#### Step 3: Configure OAuth Consent Screen

Before creating credentials, you must configure the OAuth consent screen:

1. In the left sidebar, navigate to: **APIs & Services** → **OAuth consent screen**
2. Choose user type:
   - **Internal**: Only for Google Workspace users (if you have an organization)
   - **External**: For any Google account user (choose this for public apps)
3. Click "Create"
4. Fill in the OAuth consent screen form:

   **App information:**
   - **App name**: "Premier League Predictions"
   - **User support email**: Your email address
   - **App logo** (optional): Upload your app logo if you have one

   **App domain** (optional for development):
   - **Application home page**: Your production URL (can add later)
   - **Application privacy policy link**: Can add later
   - **Application terms of service link**: Can add later

   **Authorized domains** (for production):
   - Add your production domain (e.g., `yourdomain.com`)
   - Do NOT include `http://` or `https://`
   - You can skip this for now and add it when deploying

   **Developer contact information:**
   - **Email addresses**: Your email address

5. Click "Save and Continue"

6. **Scopes** (Step 2 of consent screen):
   - Click "Add or Remove Scopes"
   - Select these scopes:
     - `../auth/userinfo.email` - See your email address
     - `../auth/userinfo.profile` - See your personal info
     - `openid` - Associate you with your personal info
   - Click "Update"
   - Click "Save and Continue"

7. **Test users** (Step 3 - only if you chose "External"):
   - If your app is in testing mode, add test user email addresses
   - Add your own email and any other testers
   - Click "Add Users" and enter email addresses
   - Click "Save and Continue"

8. **Summary** (Step 4):
   - Review your settings
   - Click "Back to Dashboard"

#### Step 4: Create OAuth 2.0 Client ID

1. In the left sidebar, go to: **APIs & Services** → **Credentials**
2. Click "+ Create Credentials" at the top
3. Select "OAuth client ID"
4. Configure the OAuth client:

   **Application type:**
   - Select "Web application"

   **Name:**
   - Enter a name: "Premier League Predictions Web Client"

   **Authorized JavaScript origins:**
   - Click "+ Add URI"
   - For development, add: `http://localhost:5173`
   - For production, add: `https://yourdomain.com` (when ready)

   **Authorized redirect URIs:**
   - Click "+ Add URI"
   - For development, add: `http://localhost:5173`
   - For the Google Sign-In button flow, you typically need the origin, not a specific callback path
   - For production, add: `https://yourdomain.com` (when ready)

5. Click "Create"

#### Step 5: Copy Your Credentials

1. A dialog will appear with your credentials:
   - **Client ID**: A long string like `123456789-abcdefg.apps.googleusercontent.com`
   - **Client Secret**: You'll also see a secret (you may need this for backend)

2. **Important**: Copy the **Client ID** (you'll need this for the frontend)
3. You can download the credentials as JSON if you want to save them
4. Click "OK"

5. Your OAuth 2.0 Client ID will now appear in the credentials list
   - You can always come back here to view or copy it again
   - Click the pencil icon to edit or view credentials

#### Step 6: Add to Your Environment File

1. Open or create `frontend/.env.local`
2. Add your Client ID:
   ```env
   VITE_GOOGLE_CLIENT_ID=YOUR_CLIENT_ID_HERE.apps.googleusercontent.com
   ```
3. Replace `YOUR_CLIENT_ID_HERE` with your actual Client ID
4. Save the file

#### Step 7: Testing

1. Make sure your dev server is running on the port you specified (default: 5173)
2. Visit `http://localhost:5173/login`
3. Click the "Continue with Google" button
4. You should see the Google Sign-In popup
5. If you chose "External" and your app is in testing mode, you'll see a warning - this is normal for development

#### Common Issues & Solutions

**Issue**: "Error 400: redirect_uri_mismatch"
- **Solution**: Make sure the redirect URI in your Google Cloud Console exactly matches your app's URL
- Check for `http` vs `https`
- Check the port number matches (e.g., `:5173`)
- Don't include trailing slashes

**Issue**: "Access blocked: This app's request is invalid"
- **Solution**: Make sure you've configured the OAuth consent screen
- Check that you've added the correct scopes

**Issue**: "This app isn't verified"
- **Solution**: This is normal for apps in development/testing mode
- You can proceed by clicking "Advanced" → "Go to [App Name] (unsafe)"
- For production, you'll need to submit your app for verification

**Issue**: Can't see the Google Sign-In button
- **Solution**: Make sure you've set the `VITE_GOOGLE_CLIENT_ID` in `.env.local`
- Restart your dev server after adding environment variables
- Check browser console for errors

#### Production Deployment Notes

When deploying to production:

1. Go back to Google Cloud Console → Credentials
2. Click the pencil icon next to your OAuth 2.0 Client ID
3. Add your production URLs to:
   - **Authorized JavaScript origins**: `https://yourdomain.com`
   - **Authorized redirect URIs**: `https://yourdomain.com`
4. Update the OAuth consent screen with production domain
5. Set the production Client ID in your hosting platform's environment variables
6. If your app will be used by users outside your organization, submit for verification

#### Security Best Practices

- ✅ Never commit your `.env.local` file to Git (it's in `.gitignore`)
- ✅ Never expose your Client Secret in frontend code
- ✅ Keep your Client ID in environment variables
- ✅ Use different OAuth clients for development and production
- ✅ Regularly rotate credentials if compromised
- ✅ Only add necessary scopes (don't request more permissions than needed)

### 4. Start Development Server

Run the development server:

```bash
npm run dev
```

The application will open at:
- **Local**: `http://localhost:5173`
- Or the next available port if 5173 is in use

You should see output like:
```
VITE v7.2.2  ready in 735 ms

➜  Local:   http://localhost:5173/
➜  Network: use --host to expose
```

### 5. Open in Browser

Navigate to `http://localhost:5173` in your browser. You should see the login page.

## Available Scripts

### Development

```bash
npm run dev
```
Starts the Vite development server with hot module replacement (HMR).
- Fast refresh on file changes
- TypeScript type checking
- Tailwind CSS processing
- Runs on port 5173 by default

### Build

```bash
npm run build
```
Creates an optimized production build in the `dist/` directory.
- TypeScript compilation
- Code minification
- Asset optimization
- Tree shaking for smaller bundle size

### Preview Production Build

```bash
npm run preview
```
Preview the production build locally before deploying.
- Serves the built files from `dist/`
- Useful for testing production behavior

### Lint

```bash
npm run lint
```
Run ESLint to check code quality and catch errors.

## Project Structure

```
frontend/
├── public/               # Static assets
├── src/
│   ├── components/      # React components
│   │   ├── ui/         # shadcn/ui components (Button, Card, Table)
│   │   ├── layout/     # Layout components (Header, Navigation)
│   │   ├── dashboard/  # Dashboard-specific components
│   │   ├── picks/      # Picks selection components
│   │   ├── fixtures/   # Fixtures display components
│   │   ├── league/     # League standings components
│   │   └── admin/      # Admin panel components
│   ├── pages/          # Page components
│   │   ├── LoginPage.tsx
│   │   ├── DashboardPage.tsx
│   │   └── ...
│   ├── contexts/       # React contexts
│   │   └── AuthContext.tsx
│   ├── hooks/          # Custom React hooks
│   ├── services/       # API services
│   │   └── api.ts
│   ├── types/          # TypeScript type definitions
│   │   └── index.ts
│   ├── lib/            # Utility functions
│   │   ├── utils.ts
│   │   └── queryClient.ts
│   ├── config/         # Configuration files
│   │   └── constants.ts
│   ├── App.tsx         # Main App component
│   ├── main.tsx        # Application entry point
│   └── index.css       # Global styles
├── index.html          # HTML template
├── tailwind.config.js  # Tailwind CSS configuration
├── tsconfig.json       # TypeScript configuration
├── vite.config.ts      # Vite configuration
├── .env.example        # Environment variables template
└── package.json        # Dependencies and scripts
```

## Key Features

### Authentication System
- Google OAuth integration
- JWT token management
- Protected routes
- Persistent sessions (localStorage)
- Auto-redirect on unauthorized access

### Routing
- Client-side routing with React Router
- Protected routes requiring authentication
- Admin-only routes
- Automatic redirects for logged-in/out users

### API Integration
- Axios client with interceptors
- Automatic token injection
- Error handling and retry logic
- TanStack Query for caching and refetching

### UI Components
- Fully typed shadcn/ui components
- Consistent styling with Tailwind CSS
- Responsive design
- Dark mode support (ready)

## Development Guidelines

### Path Aliases

The project uses TypeScript path aliases for cleaner imports:

```typescript
// Instead of relative paths:
import { Button } from '../../../components/ui/button';

// Use alias:
import { Button } from '@/components/ui/button';
```

Configured paths:
- `@/*` → `src/*`

### Adding New Components

When adding new shadcn/ui components, follow the pattern in `src/components/ui/`:

1. Create the component file
2. Export the component and its props
3. Use the `cn()` utility for conditional classes
4. Follow the existing styling conventions

### State Management

- **Local state**: Use React's `useState`
- **Auth state**: Use `AuthContext`
- **Server state**: Use TanStack Query
- **Form state**: Use React Hook Form

### API Calls

Use the configured API client:

```typescript
import { apiClient } from '@/services/api';
import { API_ENDPOINTS } from '@/config/constants';

// GET request
const response = await apiClient.get(API_ENDPOINTS.USERS);

// POST request
const response = await apiClient.post(API_ENDPOINTS.LOGIN, data);
```

## Styling Guidelines

### Tailwind CSS

Use Tailwind utility classes for styling:

```tsx
<div className="flex items-center justify-between p-4 rounded-lg bg-card">
  <h2 className="text-2xl font-bold">Title</h2>
</div>
```

### Component Styling

Use the `cn()` utility to merge classes conditionally:

```typescript
import { cn } from '@/lib/utils';

<Button className={cn(
  "default-classes",
  isActive && "active-classes",
  disabled && "disabled-classes"
)}>
  Click Me
</Button>
```

### CSS Variables

The project uses CSS variables for theming. See `src/index.css` for available variables:

```css
--background
--foreground
--primary
--secondary
--muted
--accent
--destructive
--border
--input
--ring
```

## Troubleshooting

### Port Already in Use

If port 5173 is already in use, Vite will automatically try the next available port. You'll see:

```
Port 5173 is in use, trying another one...
➜  Local:   http://localhost:5174/
```

To force a specific port, update `vite.config.ts`:

```typescript
export default defineConfig({
  server: {
    port: 3000
  }
})
```

### Module Resolution Errors

If you see "Cannot find module '@/...'" errors:

1. Restart your TypeScript server (VS Code: Cmd/Ctrl + Shift + P → "TypeScript: Restart TS Server")
2. Check that `tsconfig.app.json` has the correct paths configuration
3. Restart the dev server: Stop and run `npm run dev` again

### Tailwind CSS Not Working

If styles aren't applying:

1. Check that `tailwind.config.js` includes your files in `content`:
   ```js
   content: ["./index.html", "./src/**/*.{js,ts,jsx,tsx}"]
   ```
2. Verify `postcss.config.js` exists with Tailwind plugin
3. Clear Vite cache: `rm -rf node_modules/.vite`
4. Restart dev server

### Type Errors

If you get TypeScript errors:

1. Make sure all dependencies are installed: `npm install`
2. Check `tsconfig.app.json` for correct configuration
3. Restart TypeScript server in your editor
4. Run type check manually if needed

### Build Errors

If production build fails:

1. Fix any TypeScript errors first
2. Check for missing environment variables
3. Verify all imports are correct
4. Try removing `node_modules` and reinstalling:
   ```bash
   rm -rf node_modules package-lock.json
   npm install
   ```

## Browser Support

- Chrome (last 2 versions)
- Firefox (last 2 versions)
- Safari (last 2 versions)
- Edge (last 2 versions)

## Performance Tips

### Development
- Use React DevTools for component profiling
- Enable TanStack Query DevTools (already configured)
- Keep component tree shallow
- Use `React.memo` for expensive components

### Production
- Build generates optimized bundles
- Code splitting by route (lazy loading)
- Tree shaking removes unused code
- Assets are minified and hashed

## Deployment

### Vercel (Recommended)

#### Prerequisites

Before deploying to Vercel, ensure you have:

1. ✅ A GitHub/GitLab/Bitbucket account with your code pushed
2. ✅ A Vercel account (sign up at [vercel.com](https://vercel.com/))
3. ✅ Your production backend API URL
4. ✅ A production Google OAuth Client ID configured with your Vercel domain

#### Step-by-Step Deployment

##### 1. Push Your Code to GitHub

```bash
# Make sure all changes are committed
git add .
git commit -m "Prepare for deployment"
git push origin main
```

##### 2. Import Project to Vercel

1. Go to [vercel.com](https://vercel.com/) and sign in
2. Click **"Add New..."** → **"Project"**
3. Select your Git provider (GitHub, GitLab, or Bitbucket)
4. Import your repository:
   - Search for "PremierLeaguePredictions"
   - Click **"Import"**

##### 3. Configure Project Settings

Vercel will automatically detect that this is a Vite project. Configure the following:

**Framework Preset**: Vite (should auto-detect)

**Root Directory**:
- Set to `frontend` (since the React app is in a subdirectory)
- Click **"Edit"** next to Root Directory
- Enter `frontend`

**Build and Output Settings**:
- **Build Command**: `npm run build` (default)
- **Output Directory**: `dist` (default)
- **Install Command**: `npm install` (default)

##### 4. Add Environment Variables

Before deploying, add your environment variables:

1. Click **"Environment Variables"** section
2. Add the following variables:

| Name | Value | Notes |
|------|-------|-------|
| `VITE_API_URL` | `https://your-api-domain.com` | Your production backend API URL |
| `VITE_GOOGLE_CLIENT_ID` | `your-client-id.apps.googleusercontent.com` | Production Google OAuth Client ID |

**Important**:
- Don't include quotes around values
- Make sure there are no trailing slashes in `VITE_API_URL`
- All environment variables should be available for Production, Preview, and Development

##### 5. Deploy

1. Click **"Deploy"**
2. Wait for the build to complete (usually 1-2 minutes)
3. Once deployed, Vercel will show you the deployment URL

##### 6. Configure Custom Domain (Optional)

1. Go to your project settings in Vercel
2. Navigate to **"Domains"**
3. Click **"Add"**
4. Enter your custom domain (e.g., `predictions.yourdomain.com`)
5. Follow DNS configuration instructions
6. Wait for DNS propagation (can take up to 48 hours)

##### 7. Update Google OAuth Settings

After deployment, update your Google OAuth configuration:

1. Go to [Google Cloud Console](https://console.cloud.google.com/)
2. Navigate to **APIs & Services** → **Credentials**
3. Click your OAuth 2.0 Client ID
4. Add your Vercel URLs to **Authorized JavaScript origins**:
   - `https://your-app.vercel.app`
   - `https://your-custom-domain.com` (if using custom domain)
5. Add same URLs to **Authorized redirect URIs**
6. Click **"Save"**

#### Vercel Configuration

The project includes a `vercel.json` file that handles client-side routing:

```json
{
  "rewrites": [
    {
      "source": "/(.*)",
      "destination": "/index.html"
    }
  ]
}
```

This ensures that React Router works correctly on all routes.

#### Automatic Deployments

Once configured, Vercel will automatically deploy:

- **Production**: Every push to your `main` branch
- **Preview**: Every push to any other branch or pull request

You can disable auto-deployments in project settings if needed.

#### Monitoring Your Deployment

1. **Build Logs**: Check build logs in Vercel dashboard if deployment fails
2. **Runtime Logs**: View function logs for debugging
3. **Analytics**: Enable Vercel Analytics for performance monitoring
4. **Environment Variables**: Update anytime in project settings

#### Troubleshooting Vercel Deployment

**Issue**: "Build failed: Command failed with exit code 1"
- **Solution**: Check build logs for specific errors
- Run `npm run build` locally to test
- Verify all dependencies are in `package.json`
- Check that TypeScript types are correct

**Issue**: "Blank page after deployment"
- **Solution**: Check browser console for errors
- Verify environment variables are set correctly
- Make sure `VITE_API_URL` is accessible from the browser
- Check that Google OAuth is configured with production URLs

**Issue**: "Cannot GET /dashboard" or 404 on routes"
- **Solution**: This should be handled by `vercel.json`
- Verify `vercel.json` exists in the frontend directory
- Check Vercel build output for the rewrite rule

**Issue**: "Google Sign-In not working"
- **Solution**: Make sure you've added your Vercel URL to Google OAuth settings
- Check that `VITE_GOOGLE_CLIENT_ID` is set correctly
- Clear browser cache and cookies
- Test in incognito mode

**Issue**: "API calls failing with CORS errors"
- **Solution**: Your backend API needs to allow your Vercel domain
- Add your Vercel URL to backend CORS configuration
- Check that `VITE_API_URL` is correct

#### Vercel CLI Deployment (Alternative)

You can also deploy using the Vercel CLI:

1. Install Vercel CLI:
   ```bash
   npm install -g vercel
   ```

2. Login to Vercel:
   ```bash
   vercel login
   ```

3. Navigate to frontend directory:
   ```bash
   cd frontend
   ```

4. Deploy:
   ```bash
   # For preview deployment
   vercel

   # For production deployment
   vercel --prod
   ```

5. Follow the prompts to configure your project

#### Environment Variables via CLI

Set environment variables via CLI:

```bash
vercel env add VITE_API_URL production
vercel env add VITE_GOOGLE_CLIENT_ID production
```

### Manual Deployment

If you prefer other hosting platforms:

1. Build the project:
   ```bash
   npm run build
   ```

2. The `dist/` folder contains all static files
3. Upload to any static hosting service:
   - **Netlify**: Drag and drop `dist` folder
   - **AWS S3**: Upload to S3 bucket + CloudFront
   - **GitHub Pages**: Use `gh-pages` package
   - **Firebase Hosting**: Use `firebase deploy`

4. Configure environment variables on hosting platform
5. Ensure routing is configured for single-page apps

### Environment Variables for Production

Required environment variables for production:

```env
# Backend API URL (no trailing slash)
VITE_API_URL=https://your-api-domain.com

# Google OAuth Client ID for production
VITE_GOOGLE_CLIENT_ID=your-production-client-id.apps.googleusercontent.com
```

**Security Notes**:
- Never commit `.env.local` or production secrets to Git
- Use different OAuth clients for development and production
- Rotate credentials if they're ever exposed
- Use HTTPS for production API and frontend

## Contributing

1. Create a feature branch: `git checkout -b feature/your-feature`
2. Make your changes
3. Run linter: `npm run lint`
4. Build to verify: `npm run build`
5. Commit changes: `git commit -m "Add feature"`
6. Push: `git push origin feature/your-feature`
7. Create Pull Request

## Support

For issues or questions:
- Check the main [project README](../README.md)
- Review this setup guide
- Check existing GitHub issues
- Create a new issue if needed

## License

Private - All Rights Reserved

---

**Happy Coding!** ⚽
