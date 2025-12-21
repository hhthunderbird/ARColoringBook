# Publisher Metadata - README

This folder contains all the metadata and assets required for Unity Asset Store submission.

## ?? Folder Contents

- **`metadata.json`** - Structured publisher information for automation
- **`SUBMISSION_GUIDE.md`** - Step-by-step submission instructions
- **`IMAGE_REQUIREMENTS.md`** - Detailed specifications for all visual assets

## ?? Required Assets

Before submitting to Asset Store, you need to create:

### 1. Package Icon (128x128)
- **Current**: Using `../Felina.png`
- **Status**: ? Ready (verify dimensions)

### 2. Card Image (420x280)
- **Location**: Create in `card/` folder
- **Status**: ?? TODO - Create promotional card

### 3. Screenshots (Minimum 5)
- **Location**: `screenshots/` folder
- **Status**: ?? TODO - Capture and annotate
- **Required**:
  1. Live AR tracking
  2. Editor inspector
  3. Texture capture result
  4. Performance settings
  5. Sample scene setup

### 4. Demo Video
- **Platform**: YouTube (unlisted/public)
- **Status**: ?? TODO - Record and upload
- **Length**: 2-5 minutes

## ?? Quick Start

1. **Review** `SUBMISSION_GUIDE.md` for complete instructions
2. **Create** missing visual assets (see `IMAGE_REQUIREMENTS.md`)
3. **Update** `metadata.json` with your specific details
4. **Test** package export and import in fresh project
5. **Submit** via Unity Publisher Portal

## ?? Pre-Submission Checklist

### Package Content
- [x] Source code included
- [x] Sample scene functional
- [x] Documentation complete
- [x] License file present
- [ ] Test in fresh Unity project

### Visual Assets
- [ ] Package icon (128x128)
- [ ] Card image (420x280)
- [ ] 5+ screenshots (1920x1080)
- [ ] Demo video uploaded

### Metadata
- [ ] Review `metadata.json` accuracy
- [ ] Update URLs and contact info
- [ ] Set appropriate price
- [ ] Verify keywords

## ?? Creating Assets

### Screenshot Workflow
```bash
1. Open sample scene in Unity
2. Configure Game view to 1920x1080
3. Play scene
4. Capture screenshots using:
   - Windows: Win + Shift + S
   - Mac: Cmd + Shift + 4
5. Annotate in Photoshop/GIMP/Figma
6. Save to screenshots/ folder
```

### Video Workflow
```bash
1. Record Unity editor with OBS Studio
2. Record device demo with Xcode/ADB
3. Edit in DaVinci Resolve/Premiere
4. Add annotations and music
5. Export as MP4 (1080p, H.264)
6. Upload to YouTube
7. Add link to metadata.json
```

## ?? Publisher Information

Update these in `metadata.json`:

```json
{
  "publisher": {
    "name": "Your Company Name",
    "displayName": "Display Name",
    "url": "Your Website",
    "supportEmail": "support@yourdomain.com"
  }
}
```

## ?? Useful Links

- **Unity Publisher Portal**: https://publisher.assetstore.unity3d.com/
- **Publisher Guidelines**: https://unity.com/how-to-publish
- **Asset Store FAQ**: https://support.unity.com/hc/en-us/sections/201590583
- **Package Repository**: https://github.com/hhthunderbird/ARColoringBook

## ?? Support

For questions about submission:
- Email: support@felina.dev
- GitHub Issues: https://github.com/hhthunderbird/ARColoringBook/issues

---

## ?? Version History

**v1.0.0** (2024-01-15)
- Initial metadata setup
- Submission guide created
- Image requirements documented

---

**Last Updated**: January 15, 2024
**Maintained By**: Felina Studios
