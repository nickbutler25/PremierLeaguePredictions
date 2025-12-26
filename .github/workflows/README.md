# GitHub Actions CI/CD Pipeline

Automated testing, building, and deployment pipeline for the Premier League Predictions app.

## 🎯 Pipeline Overview

The CI/CD pipeline runs on every push and pull request, ensuring code quality and automatically deploying to production.

### Pipeline Stages

```
┌─────────────┐  ┌─────────────┐  ┌─────────────┐
│   Lint      │  │  TypeCheck  │  │    Test     │
│  & Format   │  │             │  │  (Vitest)   │
└──────┬──────┘  └──────┬──────┘  └──────┬──────┘
       │                │                │
       └────────────────┴────────────────┘
                        │
                   ┌────▼────┐
                   │  Build  │
                   └────┬────┘
                        │
       ┌────────────────┴────────────────┐
       │                                 │
  ┌────▼────┐                      ┌────▼────┐
  │  E2E    │                      │ Deploy  │
  │  Tests  │                      │ (Prod)  │
  └─────────┘                      └─────────┘
```

## 📋 Jobs

### **1. Lint & Format Check**
- Runs ESLint to catch code quality issues
- Checks code formatting with Prettier
- **Duration:** ~30 seconds
- **Runs:** Parallel with TypeCheck and Test

### **2. TypeScript Type Check**
- Compiles TypeScript to check for type errors
- Uses `tsc --noEmit` (no build output)
- **Duration:** ~20 seconds
- **Runs:** Parallel with Lint and Test

### **3. Unit Tests (Vitest)**
- Runs all unit tests with coverage
- Uploads coverage to Codecov
- **Duration:** ~45 seconds
- **Runs:** Parallel with Lint and TypeCheck

### **4. E2E Tests (Playwright)**
- Runs end-to-end tests in Chromium
- Tests critical user flows (login, picks, dashboard, league)
- Uploads Playwright report if tests fail
- **Duration:** ~2 minutes
- **Runs:** Parallel with Build

### **5. Production Build**
- Builds optimized production bundle
- Uploads source maps to Sentry
- Uploads build artifacts for deployment
- **Duration:** ~1 minute
- **Runs:** After Lint, TypeCheck, and Test pass

### **6. Deploy to Production**
- Deploys to Vercel production environment
- **Triggers:** Only on `push` to `main` branch
- **Requires:** Build and E2E tests to pass
- **Duration:** ~30 seconds

### **7. Deploy Preview**
- Deploys to Vercel preview environment
- **Triggers:** Only on `pull_request`
- **Requires:** Build and E2E tests to pass
- **Adds:** Comment to PR with preview URL

### **8. CI/CD Summary**
- Prints summary of all job results
- Fails if any critical job failed
- **Duration:** ~5 seconds

---

## 🔧 Setup Instructions

### **1. Configure GitHub Secrets**

Go to your repository → Settings → Secrets and variables → Actions → New repository secret

Add the following secrets:

#### **Required for Deployment:**
```
VERCEL_TOKEN           # From: https://vercel.com/account/tokens
VERCEL_ORG_ID          # From: Vercel project settings
VERCEL_PROJECT_ID      # From: Vercel project settings
```

#### **Optional (for Sentry):**
```
VITE_SENTRY_DSN        # Your Sentry DSN
SENTRY_AUTH_TOKEN      # From: https://sentry.io/settings/account/api/auth-tokens/
SENTRY_ORG             # Your Sentry organization slug
SENTRY_PROJECT         # Your Sentry project slug
```

#### **Optional (for Codecov):**
```
CODECOV_TOKEN          # From: https://codecov.io/
```

#### **Optional (Custom API URL):**
```
VITE_API_URL           # Defaults to: https://api.eplpredict.com
```

### **2. Get Vercel Credentials**

**Get Vercel Token:**
1. Go to https://vercel.com/account/tokens
2. Create new token → Name it "GitHub Actions"
3. Copy token → Add to GitHub secrets as `VERCEL_TOKEN`

**Get Vercel Org and Project IDs:**
1. Go to your Vercel project settings
2. Copy "Project ID" → Add to GitHub secrets as `VERCEL_PROJECT_ID`
3. Copy "Team ID" (or use personal account) → Add to GitHub secrets as `VERCEL_ORG_ID`

**Alternative method:**
```bash
# Install Vercel CLI
npm i -g vercel

# Login and link project
cd frontend
vercel link

# Get project details
cat .vercel/project.json
```

### **3. Configure Sentry (Optional)**

If you want source maps uploaded to Sentry:

1. Get Sentry DSN from your project settings
2. Create Sentry auth token with `project:releases` scope
3. Add all Sentry secrets to GitHub

### **4. Configure Codecov (Optional)**

For test coverage tracking:

1. Sign up at https://codecov.io/
2. Add your repository
3. Copy upload token
4. Add to GitHub secrets as `CODECOV_TOKEN`

---

## 🚀 How It Works

### **On Push to `main`:**
1. ✅ Runs all quality checks (lint, typecheck, tests)
2. ✅ Builds production bundle
3. ✅ Runs E2E tests
4. ✅ Deploys to Vercel production
5. ✅ Users see new version immediately

### **On Pull Request:**
1. ✅ Runs all quality checks
2. ✅ Builds production bundle
3. ✅ Runs E2E tests
4. ✅ Deploys to preview environment
5. ✅ Adds comment with preview URL
6. ✅ Reviewers can test changes before merging

### **On Push to Other Branches:**
1. ✅ Runs all quality checks
2. ✅ Builds production bundle
3. ✅ Runs E2E tests
4. ❌ Does NOT deploy

---

## 📊 Viewing Results

### **GitHub Actions Tab**

View all pipeline runs:
```
https://github.com/[your-username]/[repo-name]/actions
```

### **Pull Request Checks**

All checks appear in PR → Checks tab:
- ✅ Green checkmark = passed
- ❌ Red X = failed
- ⏳ Yellow circle = running

### **Status Badge**

Add to README.md:
```markdown
![CI/CD](https://github.com/[username]/[repo]/workflows/CI/CD%20Pipeline/badge.svg)
```

---

## 🐛 Troubleshooting

### **Build Fails on Type Errors**

```bash
# Run locally to debug
cd frontend
npm run build
```

Fix type errors in your editor before committing.

### **E2E Tests Fail**

```bash
# Run E2E tests locally
cd frontend
npm run test:e2e

# Debug with UI
npm run test:e2e:ui
```

### **Deployment Fails**

**Check:**
1. Are Vercel secrets configured correctly?
2. Is `VERCEL_TOKEN` valid?
3. Does the build pass locally?

**Debug:**
```bash
# Test Vercel deployment locally
cd frontend
vercel --prod
```

### **Codecov Upload Fails**

This is non-critical. The pipeline will continue even if Codecov fails.

To fix:
1. Verify `CODECOV_TOKEN` is correct
2. Check Codecov status: https://status.codecov.io/

### **Playwright Installation Fails**

The pipeline uses `--with-deps` to install browser dependencies.

If it fails, the E2E job may need:
```yaml
- name: Install system dependencies
  run: |
    sudo apt-get update
    sudo apt-get install -y libnss3 libnspr4 libatk1.0-0
```

---

## ⚡ Performance Optimization

### **Current Timing:**
- Lint & Format: ~30s
- TypeCheck: ~20s
- Unit Tests: ~45s
- E2E Tests: ~2m
- Build: ~1m
- Deploy: ~30s
- **Total:** ~4-5 minutes

### **Optimization Tips:**

**1. Enable npm caching:**
Already enabled with `cache: 'npm'`

**2. Run jobs in parallel:**
Already optimized - Lint, TypeCheck, and Test run in parallel

**3. Skip E2E on draft PRs:**
```yaml
if: github.event.pull_request.draft == false
```

**4. Use build matrix for multi-browser testing:**
```yaml
strategy:
  matrix:
    browser: [chromium, firefox, webkit]
```

---

## 🔐 Security

### **Secrets Protection:**
- ✅ Never logged in console
- ✅ Not accessible in forks
- ✅ Only available to workflow

### **Deployment Protection:**
- ✅ Only deploys from `main` branch
- ✅ Requires all tests to pass
- ✅ Uses GitHub environments for approval (optional)

### **Enable Protected Branches:**
1. Go to Settings → Branches
2. Add rule for `main`
3. Require status checks to pass:
   - Lint & Format Check
   - TypeScript Type Check
   - Unit Tests (Vitest)
   - E2E Tests (Playwright)
   - Production Build
4. Require pull request reviews

---

## 🎨 Customization

### **Change Node Version:**
```yaml
env:
  NODE_VERSION: '20.x'  # Change to 18.x, 22.x, etc.
```

### **Run on Specific Branches:**
```yaml
on:
  push:
    branches: [main, develop, staging]  # Add more branches
```

### **Skip CI for Specific Commits:**
Add `[skip ci]` to commit message:
```bash
git commit -m "Update README [skip ci]"
```

### **Add Manual Approval for Production:**
```yaml
deploy-production:
  environment:
    name: production
    # Requires manual approval in GitHub
```

Then configure environment in Settings → Environments → production → Required reviewers

### **Run Tests Only on /test Files:**
```yaml
on:
  push:
    paths:
      - 'frontend/**/*.test.ts'
      - 'frontend/**/*.spec.ts'
```

---

## 📚 Resources

- **GitHub Actions Docs:** https://docs.github.com/en/actions
- **Vercel Deployment:** https://vercel.com/docs/deployments/overview
- **Playwright CI:** https://playwright.dev/docs/ci
- **Vitest CI:** https://vitest.dev/guide/ci.html

---

## ✅ Checklist

Before enabling this pipeline:

- [ ] Add all required GitHub secrets
- [ ] Test Vercel deployment locally
- [ ] Verify all npm scripts work locally
- [ ] Enable branch protection rules
- [ ] Add status badge to README
- [ ] Configure Codecov (optional)
- [ ] Configure Sentry (optional)
- [ ] Test with a dummy PR
- [ ] Document any custom modifications

---

**Questions or issues?** Check the GitHub Actions tab for detailed logs!
