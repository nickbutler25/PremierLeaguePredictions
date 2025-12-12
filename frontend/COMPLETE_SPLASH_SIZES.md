# Complete iOS Splash Screen Sizes

All splash screen sizes for iPhone models from 2017 to 2024 (iPhone X through iPhone 16).

> **📱 iPhone 17 Support**: Placeholder entries have been added to `index.html`. See `IPHONE_17_UPDATE_GUIDE.md` for instructions on updating once Apple releases official specifications (typically September 2025).

## 📱 All Required Sizes

### iPhone 16 Series (2024) - Latest! 🆕
| Device | Dimensions | Filename |
|--------|------------|----------|
| iPhone 16 Pro Max | 1320 x 2868 px | `splash-iphone-16-pro-max.png` |
| iPhone 16 Pro | 1206 x 2622 px | `splash-iphone-16-pro.png` |
| iPhone 16 Plus | 1290 x 2796 px | `splash-iphone-16-plus.png` |
| iPhone 16 | 1179 x 2556 px | `splash-iphone-16.png` |

### iPhone 15 Series (2023)
| Device | Dimensions | Filename |
|--------|------------|----------|
| iPhone 15 Pro Max | 1290 x 2796 px | `splash-iphone-15-pro-max.png` |
| iPhone 15 Pro | 1179 x 2556 px | `splash-iphone-15-pro.png` |
| iPhone 15 Plus | 1290 x 2796 px | `splash-iphone-15-plus.png` |
| iPhone 15 | 1179 x 2556 px | `splash-iphone-15.png` |

### iPhone 14 Series (2022)
| Device | Dimensions | Filename |
|--------|------------|----------|
| iPhone 14 Pro Max | 1290 x 2796 px | `splash-iphone-14-pro-max.png` |
| iPhone 14 Pro | 1179 x 2556 px | `splash-iphone-14-pro.png` |
| iPhone 14 Plus | 1290 x 2796 px | `splash-iphone-14-plus.png` |
| iPhone 14 | 1170 x 2532 px | `splash-iphone-14.png` |

### iPhone 13 Series (2021)
| Device | Dimensions | Filename |
|--------|------------|----------|
| iPhone 13 Pro Max | 1284 x 2778 px | `splash-iphone-13-pro-max.png` |
| iPhone 13 Pro | 1170 x 2532 px | `splash-iphone-13-pro.png` |
| iPhone 13 | 1170 x 2532 px | `splash-iphone-13.png` |
| iPhone 13 mini | 1080 x 2340 px | `splash-iphone-13-mini.png` |

### iPhone 12 Series (2020)
| Device | Dimensions | Filename |
|--------|------------|----------|
| iPhone 12 Pro Max | 1284 x 2778 px | `splash-iphone-12-pro-max.png` |
| iPhone 12 Pro | 1170 x 2532 px | `splash-iphone-12-pro.png` |
| iPhone 12 | 1170 x 2532 px | `splash-iphone-12.png` |
| iPhone 12 mini | 1080 x 2340 px | `splash-iphone-12-mini.png` |

### iPhone 11 Series (2019)
| Device | Dimensions | Filename |
|--------|------------|----------|
| iPhone 11 Pro Max | 1242 x 2688 px | `splash-iphone-11-pro-max.png` |
| iPhone 11 Pro | 1125 x 2436 px | `splash-iphone-11-pro.png` |
| iPhone 11 | 828 x 1792 px | `splash-iphone-11.png` |

### iPhone XS/XR/X Series (2017-2018)
| Device | Dimensions | Filename |
|--------|------------|----------|
| iPhone XS Max | 1242 x 2688 px | `splash-iphone-xs-max.png` |
| iPhone XS | 1125 x 2436 px | `splash-iphone-xs.png` |
| iPhone X | 1125 x 2436 px | `splash-iphone-x.png` |
| iPhone XR | 828 x 1792 px | `splash-iphone-xr.png` |

### iPhone SE (2022/2020/2016)
| Device | Dimensions | Filename |
|--------|------------|----------|
| iPhone SE (all generations) | 750 x 1334 px | `splash-iphone-se.png` |

---

## 🎯 Priority List (Recommended Minimum)

Focus on these sizes to cover ~85% of users:

1. **iPhone 16 Pro Max** - 1320 x 2868 px ⭐
2. **iPhone 16 Pro** - 1206 x 2622 px ⭐
3. **iPhone 15 Pro Max** - 1290 x 2796 px
4. **iPhone 15 Pro / 16** - 1179 x 2556 px
5. **iPhone 14** - 1170 x 2532 px
6. **iPhone 13 Pro Max** - 1284 x 2778 px
7. **iPhone 11** - 828 x 1792 px

---

## 📊 Size Breakdown (Unique Dimensions)

To avoid duplicates, here are the **unique sizes** you need:

| Dimensions | Used By | Priority |
|------------|---------|----------|
| 1320 x 2868 | iPhone 16 Pro Max | ⭐⭐⭐ High |
| 1206 x 2622 | iPhone 16 Pro | ⭐⭐⭐ High |
| 1290 x 2796 | iPhone 15 Pro Max, 15 Plus, 16 Plus, 14 Plus, 14 Pro Max | ⭐⭐⭐ High |
| 1179 x 2556 | iPhone 16, 15, 15 Pro, 14 Pro | ⭐⭐⭐ High |
| 1170 x 2532 | iPhone 14, 13, 13 Pro, 12, 12 Pro | ⭐⭐⭐ High |
| 1284 x 2778 | iPhone 13 Pro Max, 12 Pro Max | ⭐⭐ Medium |
| 1242 x 2688 | iPhone 11 Pro Max, XS Max | ⭐⭐ Medium |
| 1125 x 2436 | iPhone 11 Pro, XS, X | ⭐ Low |
| 1080 x 2340 | iPhone 13 mini, 12 mini | ⭐ Low |
| 828 x 1792 | iPhone 11, XR | ⭐⭐ Medium |
| 750 x 1334 | iPhone SE | ⭐ Low |

---

## 🚀 Quick Generation Guide

### Using the Built-in Generator

1. Start dev server: `npm run dev`
2. Visit: `http://localhost:5173/splash-template.html`
3. Upload your logo
4. For each device size:
   - Select from dropdown
   - Click "Download Splash Screen"
   - File downloads with correct name
5. Move all files to `frontend/public/`

### Batch Creation Tip

To save time, many sizes are identical. You can create these **7 unique sizes** and reuse them:

1. **1320 x 2868** → Use for iPhone 16 Pro Max
2. **1206 x 2622** → Use for iPhone 16 Pro
3. **1290 x 2796** → Reuse for: 15 Pro Max, 15 Plus, 16 Plus, 14 Plus, 14 Pro Max
4. **1179 x 2556** → Reuse for: 16, 15, 15 Pro, 14 Pro
5. **1170 x 2532** → Reuse for: 14, 13, 13 Pro, 12, 12 Pro
6. **1284 x 2778** → Reuse for: 13 Pro Max, 12 Pro Max
7. **828 x 1792** → Reuse for: 11, XR

Just copy and rename the same file multiple times!

### Example Bash Script

```bash
cd frontend/public

# Create the unique sizes first using the generator
# Then copy/rename:

# Copy 1290 x 2796 to all devices that use it
cp splash-iphone-15-pro-max.png splash-iphone-15-plus.png
cp splash-iphone-15-pro-max.png splash-iphone-16-plus.png
cp splash-iphone-15-pro-max.png splash-iphone-14-plus.png
cp splash-iphone-15-pro-max.png splash-iphone-14-pro-max.png

# Copy 1179 x 2556
cp splash-iphone-16.png splash-iphone-15.png
cp splash-iphone-16.png splash-iphone-15-pro.png
cp splash-iphone-16.png splash-iphone-14-pro.png

# Copy 1170 x 2532
cp splash-iphone-14.png splash-iphone-13.png
cp splash-iphone-14.png splash-iphone-13-pro.png
cp splash-iphone-14.png splash-iphone-12.png
cp splash-iphone-14.png splash-iphone-12-pro.png

# Copy 1284 x 2778
cp splash-iphone-13-pro-max.png splash-iphone-12-pro-max.png

# Copy 828 x 1792
cp splash-iphone-11.png splash-iphone-xr.png

# Copy 1125 x 2436
cp splash-iphone-11-pro.png splash-iphone-xs.png

# Copy 1242 x 2688
cp splash-iphone-11-pro-max.png splash-iphone-xs-max.png
```

---

## ✅ Verification Checklist

After creating all splash screens:

- [ ] All files in `frontend/public/`
- [ ] Files named correctly (match table above)
- [ ] Files are PNG format
- [ ] Dimensions are correct
- [ ] Logo is visible and centered
- [ ] Background color matches brand
- [ ] Deployed to production
- [ ] Tested on real iPhone
- [ ] PWA reinstalled after deployment

---

## 📱 Testing

1. Deploy your changes
2. On iPhone Safari: Delete existing PWA
3. Visit your site and "Add to Home Screen"
4. Launch app from home screen
5. Watch for splash screen (appears for ~1-2 seconds)

**Note**: Splash screens only show when launching from home screen, not when using Safari directly.

---

## 💡 Pro Tips

1. **Optimize file sizes**: Use TinyPNG or similar to compress PNGs
2. **Test on real device**: Simulators don't always show splash screens correctly
3. **Match safe areas**: Keep content within the safe zone (47px top, 34px bottom)
4. **Keep it simple**: Users only see it briefly
5. **Use reusable sizes**: Save time by copying identical dimensions

---

**Total unique splash screens needed**: 7-11 (depending on coverage level)
**Total splash screen references**: 29 (all devices 2017-2024)
