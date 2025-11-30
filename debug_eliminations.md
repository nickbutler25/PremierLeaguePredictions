# Debug Steps for Elimination Page

## 1. Check if you have an active season
Open your browser DevTools (F12) -> Console tab and run:
```javascript
fetch('/api/admin/seasons')
  .then(r => r.json())
  .then(data => console.log('Seasons:', data));
```

## 2. Check what the active season is
```javascript
fetch('/api/admin/seasons/active')
  .then(r => r.json())
  .then(data => console.log('Active Season:', data));
```

## 3. Check if gameweeks exist for the active season
Replace `SEASON_NAME` with your active season name (e.g., "2024/2025"):
```javascript
const seasonName = 'YOUR_SEASON_NAME_HERE'; // e.g., '2024/2025'
fetch(`/api/admin/eliminations/configs/${encodeURIComponent(seasonName)}`)
  .then(r => r.json())
  .then(data => console.log('Elimination Configs:', data));
```

## 4. If configs are empty, check the database directly

Run this SQL query on your PostgreSQL database:
```sql
-- Check what seasons exist
SELECT * FROM seasons ORDER BY created_at DESC;

-- Check what gameweeks exist
SELECT season_id, week_number, deadline, elimination_count
FROM gameweeks
ORDER BY season_id, week_number;
```

## 5. If no gameweeks exist, sync fixtures again

Make sure to:
1. Stop the API if running
2. Rebuild: `cd backend && dotnet build`
3. Restart the API
4. In the admin panel, click "Sync Current Season Fixtures"

## Common Issues:

### Issue 1: No active season
- Create a season in the Season Management page

### Issue 2: Gameweeks exist but not for the active season
- The season name in the gameweeks table doesn't match the active season
- Re-sync fixtures for the correct season

### Issue 3: API returns empty array even though gameweeks exist
- Check the browser console for any errors
- Check if the seasonId in the API call URL is correct (it should be URL-encoded if it contains slashes like "2024/2025")
