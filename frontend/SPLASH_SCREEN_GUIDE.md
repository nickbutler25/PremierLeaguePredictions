# iOS Splash Screen Creation Guide

## 📱 Required Splash Screen Sizes

### iPhone 15 Series
- **iPhone 15 Pro Max**: 1290 x 2796 px (`splash-iphone-15-pro-max.png`)
- **iPhone 15 Pro**: 1179 x 2556 px (`splash-iphone-15-pro.png`)
- **iPhone 15/14**: 1170 x 2532 px (`splash-iphone-14.png`)

### iPhone 13/12 Series (Optional)
- **iPhone 13 Pro Max/12 Pro Max**: 1284 x 2778 px
- **iPhone 13/12**: 1170 x 2532 px (same as iPhone 14)
- **iPhone 13 mini/12 mini**: 1080 x 2340 px

### iPhone 11 Series (Optional)
- **iPhone 11 Pro Max/XS Max**: 1242 x 2688 px
- **iPhone 11 Pro/XS/X**: 1125 x 2436 px
- **iPhone 11/XR**: 828 x 1792 px

### iPhone SE (Optional)
- **iPhone SE (3rd gen)**: 750 x 1334 px

## 🎨 Design Guidelines

### 1. Safe Area
- **Top safe area**: 47px (status bar)
- **Bottom safe area**: 34px (home indicator on newer iPhones)
- Keep important content within safe areas

### 2. Design Elements
- **Background**: Use your brand color (#38003c - Premier League purple)
- **Logo**: Centered, should be visible and recognizable
- **Text**: Optional tagline or app name
- **Keep it simple**: Splash screens appear briefly

### 3. Design Principles
- Use a solid background color
- Center your logo vertically and horizontally
- Ensure logo is readable at small sizes
- Match your app's theme (light/dark)

## 🚀 Quick Method: Use Automated Tools

### Option 1: PWA Asset Generator (Recommended - Easy)
1. Visit: https://www.pwabuilder.com/imageGenerator
2. Upload your logo (high-resolution PNG, at least 512x512)
3. Select "iOS" platform
4. Download the generated splash screens
5. Place them in `frontend/public/`

### Option 2: Figma Template (Best Quality)
1. Use this template: https://www.figma.com/community/file/972119730427711114
2. Replace logo with your own
3. Export each frame as PNG
4. Name according to specifications

### Option 3: Online Generator
1. Visit: https://appsco.pe/developer/splash-screens
2. Upload a 2048x2048 logo
3. Choose background color (#38003c)
4. Download all sizes
5. Rename to match required filenames

## 🛠️ Manual Method: Using Design Software

### Using Figma (Free)

1. **Create New Design**
   ```
   File → New → Design File
   ```

2. **Set Frame Size** (for iPhone 15 Pro Max)
   ```
   Frame Tool (F) → 1290 x 2796
   ```

3. **Add Background**
   ```
   Rectangle Tool (R)
   Fill with #38003c (or your brand color)
   ```

4. **Add Logo**
   ```
   Drag your logo to center
   Recommended size: 256 x 256 px
   Position: Center (X: 645, Y: 1398)
   ```

5. **Add App Name (Optional)**
   ```
   Text below logo
   Font: System font, Bold, 32px
   Color: White
   ```

6. **Export**
   ```
   Select Frame → Export → PNG → 3x → Export
   ```

7. **Repeat for Other Sizes**
   - Create frames for each device size
   - Keep logo proportional
   - Export each as PNG

### Using Photoshop

1. **Create Document**: 1290 x 2796 px
2. **Add Background Layer**: Fill with #38003c
3. **Place Logo**: Centered, ~256 x 256 px
4. **Optional Text**: Add app name
5. **Save As**: PNG-24
6. **Repeat for other sizes**

### Using Canva (Free Online)

1. **Create Custom Design**
   - Click "Custom size"
   - Enter 1290 x 2796 px

2. **Design Splash Screen**
   - Background: Solid color #38003c
   - Upload and center your logo
   - Add text if desired

3. **Download as PNG**

4. **Resize and Export Other Sizes**
   - Use Canva's resize feature
   - Create versions for each device

## 📝 Simple HTML/CSS Generator

You can create splash screens programmatically with HTML/CSS and take screenshots:

```html
<!DOCTYPE html>
<html>
<head>
  <style>
    body {
      margin: 0;
      display: flex;
      align-items: center;
      justify-content: center;
      height: 2796px;
      width: 1290px;
      background: #38003c;
    }
    .container {
      text-align: center;
      margin-top: 47px; /* Safe area */
    }
    img {
      width: 256px;
      height: 256px;
    }
    h1 {
      color: white;
      font-family: -apple-system, BlinkMacSystemFont, "Segoe UI", Roboto;
      font-size: 32px;
      margin-top: 24px;
    }
  </style>
</head>
<body>
  <div class="container">
    <img src="pl-logo.png" alt="Logo">
    <h1>EPL Predictions</h1>
  </div>
</body>
</html>
```

Then screenshot at each device size.

## 🎯 Recommended Simple Design

For a clean, professional look:

```
┌─────────────────────┐
│                     │
│                     │  ← Safe area (status bar)
│                     │
│                     │
│                     │
│         🏆          │  ← Your logo (centered)
│    EPL Predict      │  ← App name
│                     │
│                     │
│                     │
│                     │  ← Safe area (home indicator)
└─────────────────────┘
```

**Colors**:
- Background: `#38003c` (Premier League purple)
- Logo: Full color
- Text: White `#ffffff`

## 📦 File Naming Convention

After creating, save files as:
- `splash-iphone-15-pro-max.png` (1290 x 2796)
- `splash-iphone-15-pro.png` (1179 x 2556)
- `splash-iphone-14.png` (1170 x 2532)

Place all files in: `frontend/public/`

## ✅ Testing

1. Build your app: `npm run build`
2. Deploy to your domain
3. On iPhone Safari:
   - Delete existing PWA if installed
   - Visit site and add to home screen
   - Open app - you should see the splash screen briefly

## 🎨 Example Using Your Existing Logo

If you already have `pl-banner-logo-light.png` or `pl-banner-logo-dark.png`:

1. **Extract the logo** from banner
2. **Create canvas** at splash screen size
3. **Fill background** with brand color
4. **Center logo** (make it 20-30% of screen height)
5. **Export as PNG**

## 💡 Pro Tips

1. **Test on real device** - Simulators may not show splash screens correctly
2. **Keep file sizes small** - Optimize PNGs (use TinyPNG or similar)
3. **Use dark mode variant** - Consider a dark background for dark mode
4. **Logo should be visible** - Ensure good contrast with background
5. **Don't overcomplicate** - Users only see it for 1-2 seconds

## 🔄 Updating Splash Screens

To update:
1. Replace PNG files in `frontend/public/`
2. Clear Safari cache on iPhone
3. Delete PWA from home screen
4. Reinstall from Safari

---

**Need help?** Use the PWA Asset Generator (Option 1) - it's the fastest and easiest method!
