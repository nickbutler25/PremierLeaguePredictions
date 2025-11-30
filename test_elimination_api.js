// Run this in your browser console while on any page of the app
// It will test the elimination API directly

async function testEliminationAPI() {
  console.log('=== Testing Elimination API ===');

  // Step 1: Get active season
  console.log('\n1. Fetching active season...');
  const seasonResponse = await fetch('/api/admin/seasons/active');
  const activeSeason = await seasonResponse.json();
  console.log('Active season:', activeSeason);

  if (!activeSeason || !activeSeason.name) {
    console.error('❌ No active season found!');
    return;
  }

  // Step 2: Get elimination configs
  console.log('\n2. Fetching elimination configs for season:', activeSeason.name);
  const configUrl = `/api/admin/eliminations/configs/${encodeURIComponent(activeSeason.name)}`;
  console.log('Request URL:', configUrl);

  const configResponse = await fetch(configUrl);
  console.log('Response status:', configResponse.status, configResponse.statusText);

  const configs = await configResponse.json();
  console.log('Elimination configs:', configs);
  console.log('Number of configs:', configs.length);

  if (configs.length === 0) {
    console.warn('⚠️ No elimination configs returned!');
    console.log('\n3. Checking if gameweeks exist in database...');
    console.log('You need to check your backend logs or database.');
    console.log('Run this SQL query:');
    console.log(`SELECT * FROM gameweeks WHERE season_id = '${activeSeason.name}';`);
  } else {
    console.log('✅ Found', configs.length, 'gameweek configs!');
    console.log('First few:', configs.slice(0, 5));
  }
}

// Run the test
testEliminationAPI().catch(err => console.error('Error:', err));
