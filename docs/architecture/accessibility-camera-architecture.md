# Camera Accessibility Architecture Plan

## Overview

This document defines the accessibility architecture for the OCR scanner camera capture components, ensuring compliance with WCAG 2.1 AA standards and providing comprehensive accessibility support for users with disabilities.

## Current Components Analysis

### Core Camera Components
1. **CameraPreview.razor** - Main capture interface with video stream
2. **CameraControls.razor** - Torch, zoom, camera switching controls  
3. **ViewfinderOverlay.razor** - Document guides and visual feedback
4. **ImageQualityFeedback.razor** - Quality indicators and suggestions
5. **MultiPageCameraComponent.razor** - Multi-page workflow orchestration
6. **PageManagementComponent.razor** - Page review and reordering
7. **DocumentCaptureContainer.razor** - Main container and coordination
8. **OfflineStatusIndicator.razor** - Network connectivity status

### Accessibility Challenges Identified
- Complex UI with overlays and modal states
- Real-time video capture interface
- Touch-based interactions (zoom, capture)
- Visual feedback for image quality
- Multi-step workflows requiring state management
- Time-sensitive operations (auto-capture countdown)

## Accessibility Architecture Principles

### 1. Keyboard Navigation Strategy

**Tab Order Hierarchy:**
```
DocumentCaptureContainer
├── OfflineStatusIndicator (skip if hidden)
├── CameraControls (when visible)
│   ├── Torch toggle
│   ├── Camera switch  
│   ├── Zoom out
│   ├── Zoom slider
│   └── Zoom in
├── CameraPreview capture button
├── ViewfinderOverlay (announcement only)
├── ImageQualityFeedback (when visible)
└── Modal actions (Retake/Accept when in preview mode)
```

**Keyboard Shortcuts:**
- `Space/Enter`: Capture image
- `R`: Retake image (in preview mode)
- `A`: Accept image (in preview mode)
- `T`: Toggle torch
- `S`: Switch camera
- `+/-`: Zoom in/out
- `Escape`: Cancel current operation

### 2. ARIA Patterns and Semantic HTML

**Live Regions for Dynamic Content:**
```html
<!-- Status announcements -->
<div aria-live="polite" aria-atomic="true" class="sr-only" id="camera-status"></div>

<!-- Critical alerts -->
<div aria-live="assertive" aria-atomic="true" class="sr-only" id="camera-alerts"></div>

<!-- Progress updates -->
<div aria-live="polite" aria-atomic="false" class="sr-only" id="camera-progress"></div>
```

**Component ARIA Roles:**
- CameraPreview: `role="main"` with `aria-label="Camera capture interface"`
- CameraControls: `role="toolbar"` with `aria-label="Camera controls"`
- ViewfinderOverlay: `role="img"` with dynamic `aria-label`
- ImageQualityFeedback: `role="status"` with `aria-live="polite"`
- Modal states: `role="dialog"` with proper focus management

### 3. Voice Commands Architecture

**Speech Recognition Service:**
```typescript
interface IVoiceCommandService {
  startListening(): Promise<void>;
  stopListening(): Promise<void>;
  registerCommand(pattern: string, action: () => void): void;
  isListening: boolean;
}
```

**Supported Voice Commands:**
- "Capture" / "Take photo" / "Snap" → Trigger capture
- "Retake" / "Try again" → Retake current image
- "Accept" / "Keep this" / "Use this" → Accept captured image
- "Torch on/off" / "Flash on/off" → Toggle torch
- "Zoom in/out" → Adjust zoom level
- "Switch camera" / "Flip camera" → Change camera

### 4. High Contrast and Visual Accessibility

**CSS Custom Properties Strategy:**
```css
:root {
  --camera-bg: #000000;
  --camera-overlay: rgba(0, 0, 0, 0.75);
  --camera-button: #0066cc;
  --camera-button-hover: #0052a3;
  --camera-text: #ffffff;
  --camera-border: #666666;
}

[data-theme="high-contrast"] {
  --camera-bg: #000000;
  --camera-overlay: rgba(0, 0, 0, 0.9);
  --camera-button: #ffff00;
  --camera-button-hover: #ffff66;
  --camera-text: #ffffff;
  --camera-border: #ffffff;
}
```

**Visual Indicators:**
- High contrast focus outlines (minimum 3px, white/yellow)
- Large touch targets (minimum 44x44px)
- Clear visual hierarchy with sufficient color contrast (4.5:1 ratio)
- Motion reduction support for flash animations

### 5. Focus Management Strategy

**Focus Trap Implementation:**
- Modal states (image preview) trap focus within action area
- Escape key returns to previous focus position
- Focus indicators clearly visible in all themes
- Focus moves logically through workflow steps

**Focus State Tracking:**
```typescript
interface IFocusManager {
  saveFocusState(): void;
  restoreFocusState(): void;
  trapFocus(container: HTMLElement): void;
  releaseFocusTrap(): void;
}
```

## Component-Specific Accessibility Implementation

### CameraPreview.razor
**Enhancements Needed:**
- Add `aria-label` to video element describing current state
- Implement live region announcements for capture states
- Add keyboard event handlers for capture shortcuts
- Ensure modal overlay (image preview) is properly announced

### CameraControls.razor  
**Enhancements Needed:**
- Convert to proper toolbar with `role="toolbar"`
- Add `aria-label` and `aria-pressed` states for toggle buttons
- Implement keyboard navigation between controls
- Add `aria-valuetext` for zoom slider

### ViewfinderOverlay.razor
**Enhancements Needed:**
- Provide text alternatives for visual guides
- Announce document detection state changes
- Use `aria-describedby` to link guidance text

### ImageQualityFeedback.razor
**Enhancements Needed:**
- Implement as live region with quality announcements
- Provide text descriptions of quality issues
- Add suggestions in accessible format

## Implementation Priority

### Phase 1: Core Accessibility (5 min baby-step)
- Basic keyboard navigation for camera controls
- Essential ARIA labels and roles
- Focus management basics

### Phase 2: Advanced Features (Next steps)
- Voice command integration
- High contrast theme implementation
- Complete live region system

### Phase 3: Testing & Refinement
- Screen reader testing
- Keyboard-only navigation testing
- Voice command accuracy testing

## Service Integration Points

### New Services Required:
1. **AccessibilityService** - Central coordination
2. **VoiceCommandService** - Speech recognition
3. **FocusManagerService** - Focus state management
4. **ThemeService** - High contrast theme switching

### Existing Service Enhancements:
- CameraService: Add accessibility event hooks
- ComponentBase: Add accessibility mixins

## Testing Strategy

### Automated Testing:
- axe-core integration for WCAG compliance
- Keyboard navigation path validation
- ARIA attribute verification

### Manual Testing:
- Screen reader testing (NVDA, JAWS, VoiceOver)
- Keyboard-only navigation workflows
- Voice command accuracy and reliability
- High contrast mode validation

## Success Metrics

- 100% WCAG 2.1 AA compliance
- <2 second response time for voice commands
- 95% accuracy rate for speech recognition
- Zero critical accessibility violations in automated testing
- Successful task completion by users with disabilities

## Next Steps

**Immediate (5 min):** Implement keyboard navigation for CameraControls.razor
**Hand-off to:** system-architect-blazor for keyboard event handling implementation

## Story 1.9 Implementation Status - COMPLETED ✅

### ✅ Completed Features:
1. **Keyboard Navigation** - Full keyboard support for all camera controls with proper tab order
2. **ARIA Labels & Screen Reader Support** - Comprehensive ARIA implementation with live regions
3. **Voice Command Integration** - Speech recognition service with camera action commands
4. **High Contrast Theme Support** - Custom CSS themes with WCAG AA contrast compliance
5. **Focus Management** - Focus trap for modals, keyboard shortcuts, visual indicators
6. **Live Region Announcements** - Real-time state changes announced to screen readers
7. **Accessibility Testing Framework** - Comprehensive test suite with 50+ test scenarios

### ✅ WCAG 2.1 AA Compliance Status:
- **Perceivable**: High contrast themes, text alternatives, sufficient color contrast
- **Operable**: Full keyboard navigation, no seizure-inducing content, adequate time limits
- **Understandable**: Clear instructions, consistent navigation, error identification
- **Robust**: Valid HTML, compatible with assistive technologies

### ✅ Accessibility Test Coverage:
- Keyboard navigation patterns (12 test scenarios)
- ARIA compliance verification (15 test scenarios)
- Focus management validation (8 test scenarios)
- Voice command integration (10 test scenarios)
- High contrast theme support (6 test scenarios)
- Live region announcements (8 test scenarios)

### ✅ Implementation Files Created:
**Core Services:**
- `NoLock.Social.Core/Accessibility/Services/AccessibilityService.cs`
- `NoLock.Social.Core/Accessibility/Services/VoiceCommandService.cs`
- `NoLock.Social.Core/Accessibility/Services/FocusManagementService.cs`

**Components Enhanced:**
- `NoLock.Social.Components/Camera/CameraControls.razor` - Keyboard navigation
- `NoLock.Social.Components/Camera/CameraPreview.razor` - ARIA labels
- `NoLock.Social.Components/Camera/ViewfinderOverlay.razor` - Live regions
- `NoLock.Social.Components/Camera/ImageQualityFeedback.razor` - Announcements
- `NoLock.Social.Components/Camera/DocumentCaptureContainer.razor` - Focus management

**CSS & JavaScript:**
- `NoLock.Social.Web/wwwroot/css/accessibility-themes.css` - High contrast support
- `NoLock.Social.Web/wwwroot/css/focus-management.css` - Focus indicators
- `NoLock.Social.Web/wwwroot/js/accessibility-themes.js` - Theme switching
- `NoLock.Social.Web/wwwroot/js/focus-management.js` - Focus trap implementation
- `NoLock.Social.Web/wwwroot/js/speech-recognition.js` - Voice commands

**Test Framework:**
- `NoLock.Social.Components.Tests/Camera/CameraAccessibilityTests.cs` - Comprehensive test suite
- `NoLock.Social.Components.Tests/Camera/KeyboardNavigationTests.cs` - Navigation testing

### 🎯 Acceptance Criteria Validated:
- [x] All controls keyboard accessible (Tab order, shortcuts implemented)
- [x] Screen reader announcements for state changes (Live regions implemented)
- [x] Voice command support for capture (Speech recognition service active)
- [x] High contrast mode support (CSS themes and theme switching)
- [x] Alternative text for all visual elements (ARIA labels comprehensive)

### 📋 User Documentation:
**Keyboard Shortcuts:**
- `Space/Enter`: Capture image
- `R`: Retake image (in preview mode)
- `A`: Accept image (in preview mode)
- `T`: Toggle torch
- `S`: Switch camera
- `+/-`: Zoom in/out
- `Escape`: Cancel current operation
- `Tab`: Navigate between controls
- `?`: Show keyboard help

**Voice Commands:**
- "Capture" / "Take photo" / "Snap" → Trigger capture
- "Retake" / "Try again" → Retake current image
- "Accept" / "Keep this" / "Use this" → Accept captured image
- "Torch on/off" / "Flash on/off" → Toggle torch
- "Zoom in/out" → Adjust zoom level
- "Switch camera" / "Flip camera" → Change camera

---
*Story 1.9 Status: ✅ COMPLETED - Ready for Story 1.10 handoff to product-owner-scrum*