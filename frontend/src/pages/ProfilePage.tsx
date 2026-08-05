import { useRef, useState } from 'react';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { useAuth } from '@/contexts/AuthContext';
import { usersService } from '@/services/users';
import { resizeImageForUpload } from '@/lib/image';
import { UserAvatar } from '@/components/UserAvatar';

const MAX_UPLOAD_BYTES = 5 * 1024 * 1024;
const ALLOWED_TYPES = ['image/jpeg', 'image/png', 'image/webp'];

type Status = { type: 'success' | 'error'; message: string } | null;

export function ProfilePage() {
  const { user, updateUser } = useAuth();
  const fileInputRef = useRef<HTMLInputElement>(null);

  const [firstName, setFirstName] = useState(user?.firstName ?? '');
  const [lastName, setLastName] = useState(user?.lastName ?? '');
  const [status, setStatus] = useState<Status>(null);
  const [isUploading, setIsUploading] = useState(false);
  const [isSaving, setIsSaving] = useState(false);

  if (!user) return null;

  const handleFileChange = async (e: React.ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0];
    e.target.value = ''; // allow re-selecting the same file
    if (!file) return;

    if (!ALLOWED_TYPES.includes(file.type)) {
      setStatus({ type: 'error', message: 'Please choose a JPEG, PNG, or WebP image.' });
      return;
    }
    if (file.size > MAX_UPLOAD_BYTES) {
      setStatus({ type: 'error', message: 'Image must be 5 MB or smaller.' });
      return;
    }

    setIsUploading(true);
    setStatus(null);
    try {
      const resized = await resizeImageForUpload(file);
      const updated = await usersService.uploadPhoto(resized);
      updateUser(updated);
      setStatus({ type: 'success', message: 'Profile picture updated.' });
    } catch {
      setStatus({ type: 'error', message: 'Failed to upload picture. Please try again.' });
    } finally {
      setIsUploading(false);
    }
  };

  const handleRemove = async () => {
    setIsUploading(true);
    setStatus(null);
    try {
      const updated = await usersService.deletePhoto();
      updateUser(updated);
      setStatus({ type: 'success', message: 'Profile picture removed.' });
    } catch {
      setStatus({ type: 'error', message: 'Failed to remove picture. Please try again.' });
    } finally {
      setIsUploading(false);
    }
  };

  const handleSaveName = async (e: React.FormEvent) => {
    e.preventDefault();
    setIsSaving(true);
    setStatus(null);
    try {
      const updated = await usersService.updateUser(user.id, { firstName, lastName });
      updateUser(updated);
      setStatus({ type: 'success', message: 'Profile updated.' });
    } catch {
      setStatus({ type: 'error', message: 'Failed to save changes. Please try again.' });
    } finally {
      setIsSaving(false);
    }
  };

  return (
    <div className="container mx-auto max-w-2xl p-4 sm:p-6 space-y-6" data-testid="profile-page">
      <h1 className="text-2xl font-bold">Profile</h1>

      <Card>
        <CardHeader>
          <CardTitle>Profile picture</CardTitle>
          <CardDescription>Add or update the picture shown next to your name.</CardDescription>
        </CardHeader>
        <CardContent className="flex flex-col sm:flex-row items-center gap-6">
          <UserAvatar
            firstName={user.firstName}
            lastName={user.lastName}
            photoUrl={user.photoUrl}
            className="w-64 h-64 sm:w-80 sm:h-80 max-w-full aspect-square rounded-2xl object-contain bg-muted text-6xl"
          />
          <div className="flex flex-col gap-2">
            <input
              ref={fileInputRef}
              type="file"
              accept="image/jpeg,image/png,image/webp"
              className="hidden"
              onChange={handleFileChange}
              data-testid="avatar-file-input"
            />
            <Button
              type="button"
              onClick={() => fileInputRef.current?.click()}
              disabled={isUploading}
              data-testid="upload-avatar-button"
            >
              {isUploading ? 'Uploading...' : user.photoUrl ? 'Change picture' : 'Upload picture'}
            </Button>
            {user.photoUrl && (
              <Button
                type="button"
                variant="outline"
                onClick={handleRemove}
                disabled={isUploading}
                data-testid="remove-avatar-button"
              >
                Remove picture
              </Button>
            )}
            <p className="text-xs text-muted-foreground">JPEG, PNG, or WebP. Max 5 MB.</p>
          </div>
        </CardContent>
      </Card>

      <Card>
        <CardHeader>
          <CardTitle>Your details</CardTitle>
        </CardHeader>
        <CardContent>
          <form onSubmit={handleSaveName} className="space-y-4">
            <div className="grid grid-cols-1 sm:grid-cols-2 gap-3">
              <div className="space-y-1">
                <Label htmlFor="profile-firstName">First name</Label>
                <Input
                  id="profile-firstName"
                  value={firstName}
                  onChange={(e) => setFirstName(e.target.value)}
                  required
                  autoComplete="given-name"
                />
              </div>
              <div className="space-y-1">
                <Label htmlFor="profile-lastName">Last name</Label>
                <Input
                  id="profile-lastName"
                  value={lastName}
                  onChange={(e) => setLastName(e.target.value)}
                  required
                  autoComplete="family-name"
                />
              </div>
            </div>
            <div className="space-y-1">
              <Label htmlFor="profile-email">Email</Label>
              <Input id="profile-email" value={user.email} disabled />
            </div>
            <Button type="submit" disabled={isSaving} data-testid="save-profile-button">
              {isSaving ? 'Saving...' : 'Save changes'}
            </Button>
          </form>
        </CardContent>
      </Card>

      {status && (
        <div
          className={`p-3 text-sm rounded-md border ${
            status.type === 'success'
              ? 'text-green-700 dark:text-green-400 bg-green-50 dark:bg-green-950/30 border-green-200 dark:border-green-800'
              : 'text-red-600 dark:text-red-400 bg-red-50 dark:bg-red-950/30 border-red-200 dark:border-red-800'
          }`}
          data-testid="profile-status"
        >
          {status.message}
        </div>
      )}
    </div>
  );
}
