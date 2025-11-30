-- Check what seasons exist
SELECT * FROM seasons ORDER BY created_at DESC;

-- Check what gameweeks exist
SELECT season_id, week_number, deadline, is_locked, elimination_count, created_at
FROM gameweeks
ORDER BY season_id, week_number;

-- Count gameweeks per season
SELECT season_id, COUNT(*) as gameweek_count
FROM gameweeks
GROUP BY season_id;
