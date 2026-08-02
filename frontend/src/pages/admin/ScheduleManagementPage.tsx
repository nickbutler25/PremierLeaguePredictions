import { useState } from 'react';
import { useMutation } from '@tanstack/react-query';
import { adminService } from '@/services/admin';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { toast } from 'sonner';

export function ScheduleManagementPage() {
  const [lastResult, setLastResult] = useState<{
    message: string;
    jobCount: number;
  } | null>(null);

  const generateMutation = useMutation({
    mutationFn: () => adminService.generateWeeklySchedule(),
    onSuccess: (data) => {
      setLastResult({ message: data.message, jobCount: data.jobCount });
      toast.success(`Schedule generated: ${data.jobCount} jobs created on cron-job.org`);
    },
    onError: () => {
      toast.error('Failed to generate schedule — check server logs');
    },
  });

  return (
    <div className="space-y-6">
      <div>
        <h1 className="text-2xl font-bold">Schedule Management</h1>
        <p className="text-muted-foreground mt-1">
          Generates this week's cron jobs on cron-job.org — score syncs, pick reminders, and
          auto-picks based on fixture kickoff times in the database.
        </p>
      </div>

      <Card>
        <CardHeader>
          <CardTitle>Weekly Schedule</CardTitle>
        </CardHeader>
        <CardContent className="space-y-4">
          <p className="text-sm text-muted-foreground">
            Reads all upcoming fixtures and deadlines, then creates or replaces jobs on
            cron-job.org. Run this now to cover the current gameweek, or set up a master cron job to
            call{' '}
            <code className="bg-muted px-1 py-0.5 rounded text-xs">
              POST /api/v1/admin/schedule/generate
            </code>{' '}
            every Monday at 09:00 UTC for hands-free scheduling.
          </p>

          <Button onClick={() => generateMutation.mutate()} disabled={generateMutation.isPending}>
            {generateMutation.isPending ? 'Generating…' : 'Generate Weekly Schedule'}
          </Button>

          {lastResult && (
            <div className="rounded-md border p-4 text-sm space-y-1">
              <p className="font-medium">{lastResult.message}</p>
              <p className="text-muted-foreground">{lastResult.jobCount} jobs created</p>
            </div>
          )}
        </CardContent>
      </Card>
    </div>
  );
}
