# iPhone 17 Update Guide

iPhone 17 specifications are not yet available. This guide will help you add support once Apple releases the official specs.

## 📅 When to Update

- **Typical Release**: September each year
- **Specs Available**: At Apple's announcement event
- **Update Timeline**: Within 1-2 days of announcement

## 🔍 Where to Find Official Specs

1. **Apple's Official Site**: https://www.apple.com/iphone/compare/
2. **Apple Developer Portal**: https://developer.apple.com/
3. **Tech Specs Page**: Look for "Display" section with pixel dimensions

## 📐 How to Calculate Splash Screen Size

Apple provides:
- **Logical Resolution**: e.g., "393 × 852 points"
- **Pixel Ratio**: Usually 3x (@3x)

**Formula**:
```
Splash Width = Logical Width × Pixel Ratio
Splash Height = Logical Height × Pixel Ratio
```

**Example** (iPhone 16 Pro):
- Logical: 402 × 874 points
- Ratio: 3x
- Splash: 1206 × 2622 px

## ✏️ How to Update (Step-by-Step)

### Step 1: Get Official Specs

Once iPhone 17 is announced, find these values:
- [ ] Device width (points)
- [ ] Device height (points)
- [ ] Pixel ratio (usually 3x)
- [ ] Model names (e.g., 17, 17 Pro, 17 Pro Max, 17 Plus)

### Step 2: Calculate Splash Dimensions

For each model:
```
Width in pixels = Device width × Pixel ratio
Height in pixels = Device height × Pixel ratio
```

### Step 3: Update index.html

Open `frontend/index.html` and find the iPhone 17 placeholder section (line ~37).

**Before:**
```html
<!-- iPhone 17 Series (2025) - PLACEHOLDER: Update when specs are released -->
<!-- <link rel="apple-touch-startup-image" media="screen and (device-width: TBD) and (device-height: TBD) and (-webkit-device-pixel-ratio: 3) and (orientation: portrait)" href="/splash-iphone-17-pro-max.png" /> -->
```

**After** (example with hypothetical specs):
```html
<!-- iPhone 17 Series (2025) -->
<link rel="apple-touch-startup-image" media="screen and (device-width: 450px) and (device-height: 980px) and (-webkit-device-pixel-ratio: 3) and (orientation: portrait)" href="/splash-iphone-17-pro-max.png" />
<link rel="apple-touch-startup-image" media="screen and (device-width: 410px) and (device-height: 890px) and (-webkit-device-pixel-ratio: 3) and (orientation: portrait)" href="/splash-iphone-17-pro.png" />
<link rel="apple-touch-startup-image" media="screen and (device-width: 440px) and (device-height: 956px) and (-webkit-device-pixel-ratio: 3) and (orientation: portrait)" href="/splash-iphone-17-plus.png" />
<link rel="apple-touch-startup-image" media="screen and (device-width: 400px) and (device-height: 870px) and (-webkit-device-pixel-ratio: 3) and (orientation: portrait)" href="/splash-iphone-17.png" />
```

### Step 4: Update Splash Generator

Open `frontend/public/splash-template.html` and add to the device dropdown (around line 175):

```html
<optgroup label="iPhone 17 Series (2025)">
  <option value="1350,2940">iPhone 17 Pro Max (1350 x 2940)</option>
  <option value="1230,2670">iPhone 17 Pro (1230 x 2670)</option>
  <option value="1320,2868">iPhone 17 Plus (1320 x 2868)</option>
  <option value="1200,2610">iPhone 17 (1200 x 2610)</option>
</optgroup>
```

### Step 5: Update Documentation

Update `COMPLETE_SPLASH_SIZES.md`:

Add at the top of the file:
```markdown
### iPhone 17 Series (2025)
| Device | Dimensions | Filename |
|--------|------------|----------|
| iPhone 17 Pro Max | 1350 x 2940 px | `splash-iphone-17-pro-max.png` |
| iPhone 17 Pro | 1230 x 2670 px | `splash-iphone-17-pro.png` |
| iPhone 17 Plus | 1320 x 2868 px | `splash-iphone-17-plus.png` |
| iPhone 17 | 1200 x 2610 px | `splash-iphone-17.png` |
```

### Step 6: Generate Splash Screens

1. Run dev server: `npm run dev`
2. Open: `http://localhost:5173/splash-template.html`
3. Select each iPhone 17 model from dropdown
4. Generate and download splash screens
5. Place files in `frontend/public/`

### Step 7: Deploy

```bash
git add .
git commit -m "Add iPhone 17 splash screen support"
git push
```

## 🎯 Quick Update Checklist

When iPhone 17 specs are released:

- [ ] Find official device dimensions from Apple
- [ ] Calculate splash screen sizes (width × 3, height × 3)
- [ ] Update `index.html` (uncomment and fill in dimensions)
- [ ] Update `splash-template.html` (add to dropdown)
- [ ] Update `COMPLETE_SPLASH_SIZES.md` (add to table)
- [ ] Generate splash screen images using template
- [ ] Test on real iPhone 17 device
- [ ] Deploy to production

## 📊 Expected Changes (Based on History)

Historically, Apple increases screen sizes slightly each year:

| Generation | Pro Max Width | Pro Max Height |
|------------|---------------|----------------|
| iPhone 14 Pro Max | 1290 | 2796 |
| iPhone 15 Pro Max | 1290 | 2796 |
| iPhone 16 Pro Max | 1320 | 2868 |
| iPhone 17 Pro Max | TBD (~1350?) | TBD (~2940?) |

**Note**: These are estimates. Always use official specs when available.

## 🔔 Reminder

Set a reminder to update this when:
- **Apple Event** (usually September)
- **Developer Beta Release** (specs available in Xcode)
- **Public Launch** (full specs on Apple.com)

## 📞 Support Resources

- Apple Developer Forums: https://developer.apple.com/forums/
- iOS Human Interface Guidelines: https://developer.apple.com/design/human-interface-guidelines/
- Web.dev PWA Guidance: https://web.dev/learn/pwa/

---

**Current Status**: ⏳ Waiting for iPhone 17 announcement
**Last Checked**: December 2025
**Next Check**: September 2025 (Apple Event)
