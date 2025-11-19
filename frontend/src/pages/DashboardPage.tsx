import { Summary } from "@/components/dashboard/Summary";
import { Picks } from "@/components/dashboard/Picks";
import { Fixtures } from "@/components/dashboard/Fixtures";
import { LeagueStandings } from "@/components/league/LeagueStandings";

export function DashboardPage() {
  return (
    <div className="container mx-auto p-4 space-y-4">
      {/* Summary Header */}
      <Summary />

      {/* Main Content Area: 3 Column Layout */}
      <div className="grid gap-4 grid-cols-1 md:grid-cols-2 lg:grid-cols-3">
        {/* Left Column - Picks */}
        <div>
          <Picks />
        </div>

        {/* Middle Column - Fixtures */}
        <div>
          <Fixtures />
        </div>

        {/* Right Column - League Standings */}
        <div>
          <LeagueStandings />
        </div>
      </div>
    </div>
  );
}
