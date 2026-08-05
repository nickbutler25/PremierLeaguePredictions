import { cn } from '@/lib/utils';

interface UserAvatarProps {
  firstName?: string;
  lastName?: string;
  photoUrl?: string | null;
  className?: string;
}

/**
 * Renders a user's avatar, falling back to their initials when no picture is set.
 * Size is controlled via `className` (e.g. "w-8 h-8 text-xs").
 */
export function UserAvatar({ firstName, lastName, photoUrl, className }: UserAvatarProps) {
  const name = [firstName, lastName].filter(Boolean).join(' ');
  const initials =
    `${firstName?.[0] ?? ''}${lastName?.[0] ?? ''}`.toUpperCase() || '?';

  if (photoUrl) {
    return (
      <img
        src={photoUrl}
        alt={name ? `${name}'s profile picture` : 'Profile picture'}
        className={cn('rounded-full object-cover bg-muted', className)}
        data-testid="user-avatar"
      />
    );
  }

  return (
    <div
      className={cn(
        'rounded-full bg-primary/10 text-primary flex items-center justify-center font-semibold select-none',
        className
      )}
      role="img"
      aria-label={name ? `${name}'s profile picture` : 'Profile picture'}
      data-testid="user-avatar-fallback"
    >
      {initials}
    </div>
  );
}
