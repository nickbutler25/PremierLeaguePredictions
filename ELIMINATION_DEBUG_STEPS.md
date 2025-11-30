# Steps to Debug Elimination Page

I've added logging to help diagnose the issue. Follow these steps:

## Step 1: Rebuild and Restart
```bash
cd backend
dotnet build
# Then restart your API
```

## Step 2: Check the Logs

When you navigate to the Elimination Management page, check your API console logs. You should see:
- `GetEliminationConfigs called for seasonId: [season name]`
- `Getting elimination configs for season: [season name]`
- `Found X gameweeks for season [season name]`
- `Returned X elimination configs for season [season name]`

**Look for these values in the logs!**

## Step 3: Verify in Browser

Open browser DevTools (F12) -> Network tab:
1. Refresh the Elimination Management page
2. Look for a request to `/api/admin/eliminations/configs/[something]`
3. Click on it and check:
   - **Request URL**: Does the season name look correct? (e.g., `2024%2F2025` is `2024/2025` URL-encoded)
   - **Response**: What does it return? Empty array `[]` or actual data?

## Step 4: Direct Database Check

Connect to your PostgreSQL database and run:

```sql
-- What's the active season?
SELECT name, is_active FROM seasons WHERE is_active = true;

-- What gameweeks exist?
SELECT season_id, week_number, deadline
FROM gameweeks
ORDER BY season_id, week_number;
```

## Common Scenarios:

### Scenario A: Logs show "Found 0 gameweeks"
**Problem**: No gameweeks exist for that season in the database
**Solution**:
1. Go to Season Management page
2. Click "Sync Current Season Fixtures"
3. Wait for it to complete
4. Refresh Elimination Management page

### Scenario B: Database has gameweeks but different season name
**Problem**: Gameweeks exist for "2024/2025" but active season is "2024-2025" (format mismatch)
**Solution**: The season names must match exactly. Check the database and fix season name if needed.

### Scenario C: Logs show correct count but page is still empty
**Problem**: Frontend not receiving or processing the data
**Solution**: Check browser console for JavaScript errors

### Scenario D: Request URL has wrong season name
**Problem**: Frontend is requesting configs for wrong/non-existent season
**Solution**: Check what `getActiveSeason()` returns in the Network tab

## Quick Test

Run this in browser console while on the Elimination Management page:
```javascript
// Check what season the frontend thinks is active
const activeSeason = document.querySelector('[class*="container"]')?.textContent;
console.log('Looking for text containing season name:', activeSeason);

// Make a direct API call
fetch('/api/admin/seasons/active')
  .then(r => r.json())
  .then(season => {
    console.log('Active season from API:', season);
    return fetch(`/api/admin/eliminations/configs/${encodeURIComponent(season.name)}`);
  })
  .then(r => r.json())
  .then(configs => console.log('Elimination configs:', configs));
```

Share the output of these logs and I can help further!
