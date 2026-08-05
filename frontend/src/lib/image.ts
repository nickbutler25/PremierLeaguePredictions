/**
 * Client-side image processing for avatars: downscale to fit within a bounding box
 * while preserving the original aspect ratio, so we only ever upload a small file to
 * storage and never crop the picture. The whole image is kept; the display frame
 * (object-contain) handles any letterboxing.
 */

const DEFAULT_MAX_SIZE = 512;
const OUTPUT_TYPE = 'image/jpeg';
const OUTPUT_QUALITY = 0.85;

export async function resizeImageForUpload(file: File, maxSize = DEFAULT_MAX_SIZE): Promise<File> {
  const bitmap = await createImageBitmap(file);
  try {
    // Scale so the longest edge is at most `maxSize`, preserving aspect ratio.
    const scale = Math.min(1, maxSize / Math.max(bitmap.width, bitmap.height));
    const width = Math.max(1, Math.round(bitmap.width * scale));
    const height = Math.max(1, Math.round(bitmap.height * scale));

    const canvas = document.createElement('canvas');
    canvas.width = width;
    canvas.height = height;
    const ctx = canvas.getContext('2d');
    if (!ctx) throw new Error('Canvas is not supported in this browser');
    // JPEG has no alpha; paint white behind the image so transparent PNGs don't go black.
    ctx.fillStyle = '#ffffff';
    ctx.fillRect(0, 0, width, height);
    ctx.drawImage(bitmap, 0, 0, width, height);

    const blob = await new Promise<Blob | null>((resolve) =>
      canvas.toBlob(resolve, OUTPUT_TYPE, OUTPUT_QUALITY)
    );
    if (!blob) throw new Error('Failed to process the image');

    return new File([blob], 'avatar.jpg', { type: OUTPUT_TYPE });
  } finally {
    bitmap.close?.();
  }
}
