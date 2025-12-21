# Asset Store Image Requirements

## Quick Reference

| Asset Type | Size | Format | Notes |
|------------|------|--------|-------|
| Package Icon | 128x128 | PNG-24 | Transparent background |
| Card Image | 420x280 | PNG/JPG | Promotional card |
| Screenshots | 1920x1080+ | PNG/JPG | Min 5 required |
| Social Card | 1200x630 | PNG/JPG | For social media |

---

## 1. Package Icon (128x128)

**Current**: `Assets/ColouringBook/Felina.png`

### Requirements
- **Dimensions**: Exactly 128x128 pixels
- **Format**: PNG-24 with alpha channel
- **Background**: Transparent
- **Content**: 
  - Clear, recognizable logo
  - High contrast
  - No text (logo/icon only)
  - Looks good at small sizes

### Design Tips
```
? DO:
- Use bold, simple shapes
- High contrast colors
- Center the icon
- Test at 32x32 to ensure readability

? DON'T:
- Use gradients (keeps it simple)
- Include tiny details
- Use low contrast colors
- Add text/labels
```

---

## 2. Card Image (420x280)

**Create new**: Promotional banner for Asset Store

### Requirements
- **Dimensions**: Exactly 420x280 pixels
- **Format**: PNG or high-quality JPG
- **DPI**: 72 (web standard)
- **Content**:
  - Package name/logo
  - 1-2 key features
  - Sample screenshot
  - Price badge (optional)

### Template Layout
```
???????????????????????????????????????
? [LOGO]    Felina AR Coloring Book   ? ? Header (60px)
?                                      ?
?  ??????????????????   Key Features: ? ? Main area
?  ?  Screenshot    ?   • AR Tracking ?   (200px)
?  ?  Here          ?   • Auto Scan   ?
?  ?                ?   • Mobile      ?
?  ??????????????????                 ?
?                                      ?
?  [?????] Unity 2022.3+    [$49.99] ? ? Footer (20px)
???????????????????????????????????????
```

### Photoshop/GIMP Template
```
1. Create new image: 420x280px, 72 DPI, RGB
2. Add gradient background (brand colors)
3. Place package icon (64x64) at top-left
4. Add package name (24pt font, bold)
5. Add screenshot (200x150px) centered-left
6. Add 2-3 key features (14pt, right side)
7. Add platform badges (bottom)
8. Export as PNG-24 or JPG (quality 90+)
```

---

## 3. Screenshots (Minimum 5)

**Resolution**: 1920x1080 or device native (higher is better)
**Format**: PNG or high-quality JPG
**Quantity**: 5 required, 10 maximum

### Recommended Screenshots

#### Screenshot 1: "Live AR Tracking"
**Content**: Show app running on device with AR tracking active
```
Scene elements:
- Physical coloring page visible
- AR overlay showing tracked image
- Quality indicator (85% or higher)
- Device stability indicator
- Timestamp/frame counter
```

**Annotations** (add with image editor):
- "Real-time tracking"
- "Quality detection: 92%"
- "Auto-capture ready"

#### Screenshot 2: "Editor Inspector - Setup"
**Content**: Unity editor with ARScannerManager inspector open
```
Show:
- Scene hierarchy (left)
- Inspector panel (right) showing:
  - Reference Library assigned
  - Active Targets list
  - Configuration settings
  - "Refresh Active Targets" button
- Game view preview
```

**Annotations**:
- "Custom editor tools"
- "Drag & drop setup"
- "One-click populate"

#### Screenshot 3: "Texture Capture Result"
**Content**: Before/after comparison
```
Layout:
???????????????????????????????
?   BEFORE     ?    AFTER     ?
?  (Physical)  ?  (In AR)     ?
?   [Image]    ?   [Image]    ?
???????????????????????????????
```

**Annotations**:
- "Original coloring"
- "Applied to 3D model"
- "GPU-accelerated unwarp"

#### Screenshot 4: "Performance Settings"
**Content**: Editor showing configuration options
```
Show:
- Resolution settings (512/1024/2048)
- Capture threshold slider
- Platform presets (Mobile/Desktop)
- Memory usage indicators
- FPS counter
```

**Annotations**:
- "Optimized for mobile"
- "Configurable quality"
- "60 FPS on iPhone 12"

#### Screenshot 5: "Sample Scene"
**Content**: Complete sample scene hierarchy
```
Show:
- Full scene hierarchy
- ARScannerManager setup
- ARContentSpawner setup
- ARPaintableObject on 3D models
- Reference images assigned
- Sample running in Game view
```

**Annotations**:
- "Complete workflow"
- "Sample scene included"
- "Production-ready"

### Screenshot Capture Methods

#### Unity Editor
```csharp
// Game View capture:
1. Window > General > Game
2. Set resolution: 1920x1080
3. Play scene
4. Use Windows Snipping Tool or:
   - Windows: Win + Shift + S
   - Mac: Cmd + Shift + 4

// Scene View capture:
Same as above, but capture Scene view instead
```

#### iOS Device
```bash
# Using Xcode
1. Connect device
2. Window > Devices and Simulators
3. Select device
4. Click "Take Screenshot"
5. Save as PNG

# On device
1. Press Volume Up + Power Button
2. Photo saved to Photos app
3. AirDrop to Mac
```

#### Android Device
```bash
# Via ADB
adb shell screencap -p /sdcard/screenshot.png
adb pull /sdcard/screenshot.png

# On device
1. Hold Power + Volume Down
2. Transfer via USB
```

### Post-Processing
```
For all screenshots:
1. Resize to 1920x1080 if needed (maintain aspect ratio)
2. Add annotations using:
   - Photoshop
   - GIMP
   - Figma
   - Canva
3. Use consistent font/style:
   - Font: Roboto or Open Sans
   - Size: 24-36pt
   - Color: White with 50% black stroke
   - Position: Top-right or bottom-left
4. Export as PNG (lossless) or JPG 95% quality
```

---

## 4. Demo Video

**Platform**: YouTube (unlisted or public)
**Length**: 2-5 minutes recommended
**Resolution**: 1920x1080 minimum (4K preferred)
**Format**: MP4, H.264 codec

### Video Structure

```
0:00 - 0:10  Intro
             - Logo animation
             - Package name
             - "Available on Unity Asset Store"

0:10 - 0:30  Problem/Hook
             - "Want to create interactive AR coloring books?"
             - Show physical coloring page
             - "Traditional methods are complex..."

0:30 - 1:00  Solution
             - "Felina AR Coloring Book makes it easy"
             - Show quick setup in editor
             - Show live tracking on device

1:00 - 2:30  Key Features (30s each)
             - Auto-tracking with quality detection
             - One-button texture capture
             - Instant texture application
             - Performance optimization

2:30 - 3:00  Editor Workflow
             - Quick setup demonstration
             - Drag & drop simplicity
             - Reference library integration

3:00 - 3:30  Live Demo
             - Physical coloring page
             - AR tracking
             - Texture capture
             - Result on 3D model

3:30 - 4:00  Technical Highlights
             - Multi-platform support
             - Native plugin performance
             - Complete source code
             - Documentation

4:00 - 4:30  Getting Started
             - "Import package"
             - "Add ARScannerManager"
             - "Configure and build"
             - "That's it!"

4:30 - 5:00  Call to Action
             - Asset Store link
             - "Start your free trial"
             - Support email
             - "Get started today!"
```

### Recording Setup

**Screen Recording** (Unity Editor):
```bash
# Windows
- OBS Studio (free): https://obsproject.com/
- Set source: Display Capture
- Resolution: 1920x1080
- FPS: 60
- Bitrate: 6000 kbps

# Mac
- QuickTime Player (built-in)
- File > New Screen Recording
- Options: Show Mouse Clicks
```

**Device Recording** (AR Demo):
```bash
# iOS
- Use Xcode: Window > Devices > Record
- Or use screen recording on device
- Transfer via AirDrop

# Android
- adb shell screenrecord /sdcard/demo.mp4
- adb pull /sdcard/demo.mp4
- Or use built-in screen recorder
```

**Video Editing**:
```
Free options:
- DaVinci Resolve (powerful, free)
- Shotcut (simple, free)
- iMovie (Mac only)

Paid options:
- Adobe Premiere Pro
- Final Cut Pro
- Camtasia
```

### Video Checklist
- [ ] Clear audio (use microphone, not laptop mic)
- [ ] Background music (royalty-free)
- [ ] Text overlays for key points
- [ ] Smooth transitions
- [ ] No long pauses/dead air
- [ ] Call to action at end
- [ ] Captions/subtitles (YouTube auto-generate)
- [ ] Thumbnail image (1280x720)

---

## 5. Social Media Assets

### Twitter/X Card (1200x630)
```
Content:
- Package logo (left)
- Key visual/screenshot (center-right)
- Package name (top)
- One-liner: "Professional AR Coloring Book for Unity"
- CTA: "Available on Asset Store"
```

### Instagram Post (1080x1080)
```
Content:
- Square format
- Centered screenshot or collage
- Logo watermark (bottom-right)
- Hashtags: #unity3d #gamedev #AR #madewithunity
```

### LinkedIn Banner (1200x627)
```
Content:
- Professional layout
- Multiple screenshots showcasing features
- Company branding
- Contact information
```

---

## Tools & Resources

### Design Tools
- **Photoshop/GIMP**: Full image editing
- **Figma**: UI mockups and cards (free)
- **Canva**: Quick social media graphics (free)
- **Inkscape**: Vector graphics (free)

### Screenshot Annotation
- **Skitch**: Simple annotations (free, Mac/Win)
- **Snagit**: Professional annotations (paid)
- **LICEcap**: Animated GIFs (free)

### Video Tools
- **OBS Studio**: Screen recording (free)
- **DaVinci Resolve**: Video editing (free)
- **HandBrake**: Video compression (free)

### Asset Resources
- **Unsplash**: Stock photos (free)
- **Pexels**: Stock videos (free)
- **Freesound**: Sound effects (free)
- **Icons8**: Icons and graphics (free tier)

---

## File Organization

```
Assets/ColouringBook/.publisher/
??? metadata.json
??? SUBMISSION_GUIDE.md
??? IMAGE_REQUIREMENTS.md (this file)
??? icon/
?   ??? package_icon_128x128.png
??? card/
?   ??? card_image_420x280.png
??? screenshots/
?   ??? 01_live_tracking.png
?   ??? 02_editor_inspector.png
?   ??? 03_texture_capture.png
?   ??? 04_performance_settings.png
?   ??? 05_sample_scene.png
??? social/
?   ??? twitter_card_1200x630.png
?   ??? instagram_post_1080x1080.png
?   ??? linkedin_banner_1200x627.png
??? video/
    ??? demo_video.mp4
    ??? thumbnail_1280x720.png
```

---

## Validation Checklist

Before uploading to Asset Store:

### Icon
- [ ] Exactly 128x128 pixels
- [ ] PNG-24 with transparency
- [ ] Looks good at 32x32
- [ ] No text included
- [ ] Consistent branding

### Card
- [ ] Exactly 420x280 pixels
- [ ] PNG or high-quality JPG
- [ ] Package name clearly visible
- [ ] Key features highlighted
- [ ] Professional appearance

### Screenshots
- [ ] Minimum 5 screenshots
- [ ] 1920x1080 or higher
- [ ] PNG or high-quality JPG
- [ ] Clear annotations
- [ ] Consistent style
- [ ] Show real functionality
- [ ] No placeholder content

### Video
- [ ] 2-5 minutes length
- [ ] 1080p minimum quality
- [ ] Uploaded to YouTube
- [ ] Clear audio
- [ ] Demonstrates all features
- [ ] Call to action included

---

**Need help creating these assets?**

Contact: support@felina.dev

Last Updated: 2024-01-15
