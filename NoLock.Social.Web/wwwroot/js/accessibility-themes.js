// Accessibility Theme Management for Camera Components
// Provides high contrast theme switching functionality

window.AccessibilityThemes = (function() {
    'use strict';
    
    // Theme constants
    const THEMES = {
        NORMAL: 'normal',
        HIGH_CONTRAST_DARK: 'high-contrast-dark',
        HIGH_CONTRAST_LIGHT: 'high-contrast-light'
    };
    
    const STORAGE_KEY = 'accessibility-theme-preference';
    const BODY_ATTRIBUTE = 'data-theme';
    
    let currentTheme = THEMES.NORMAL;
    
    /**
     * Initialize theme system - called on page load
     */
    function initialize() {
        // Load saved theme preference
        const savedTheme = localStorage.getItem(STORAGE_KEY);
        if (savedTheme && Object.values(THEMES).includes(savedTheme)) {
            currentTheme = savedTheme;
        } else {
            // Check for OS preference
            currentTheme = detectSystemPreference();
        }
        
        // Apply the theme
        applyTheme(currentTheme);
        
        // Listen for system theme changes
        if (window.matchMedia) {
            window.matchMedia('(prefers-contrast: high)').addEventListener('change', handleSystemThemeChange);
            window.matchMedia('(prefers-color-scheme: dark)').addEventListener('change', handleSystemThemeChange);
        }
        
        console.log('Accessibility themes initialized with theme:', currentTheme);
    }
    
    /**
     * Detect system accessibility preferences
     */
    function detectSystemPreference() {
        if (window.matchMedia) {
            const prefersHighContrast = window.matchMedia('(prefers-contrast: high)').matches;
            const prefersDarkMode = window.matchMedia('(prefers-color-scheme: dark)').matches;
            
            if (prefersHighContrast) {
                return prefersDarkMode ? THEMES.HIGH_CONTRAST_DARK : THEMES.HIGH_CONTRAST_LIGHT;
            }
        }
        
        return THEMES.NORMAL;
    }
    
    /**
     * Handle system theme preference changes
     */
    function handleSystemThemeChange() {
        // Only auto-switch if user hasn't manually set a preference
        const hasManualPreference = localStorage.getItem(STORAGE_KEY);
        if (!hasManualPreference) {
            const newTheme = detectSystemPreference();
            if (newTheme !== currentTheme) {
                setTheme(newTheme);
            }
        }
    }
    
    /**
     * Apply theme to the document
     * @param {string} theme - Theme to apply
     */
    function applyTheme(theme) {
        if (!Object.values(THEMES).includes(theme)) {
            console.warn('Invalid theme specified:', theme);
            return;
        }
        
        const body = document.body;
        
        // Remove existing theme attributes
        Object.values(THEMES).forEach(t => {
            if (t === THEMES.NORMAL) return;
            body.removeAttribute(BODY_ATTRIBUTE);
        });
        
        // Apply new theme
        if (theme !== THEMES.NORMAL) {
            body.setAttribute(BODY_ATTRIBUTE, theme);
        }
        
        currentTheme = theme;
        
        // Announce theme change to screen readers
        announceThemeChange(theme);
    }
    
    /**
     * Set and save theme preference
     * @param {string} theme - Theme to set
     */
    function setTheme(theme) {
        applyTheme(theme);
        
        // Save to localStorage
        localStorage.setItem(STORAGE_KEY, theme);
        
        // Dispatch custom event for components that need to react
        const event = new CustomEvent('themeChanged', {
            detail: { theme: theme, previousTheme: currentTheme }
        });
        document.dispatchEvent(event);
        
        console.log('Theme set to:', theme);
    }
    
    /**
     * Get current theme
     * @returns {string} Current theme
     */
    function getCurrentTheme() {
        return currentTheme;
    }
    
    /**
     * Cycle through available themes
     */
    function cycleTheme() {
        const themes = Object.values(THEMES);
        const currentIndex = themes.indexOf(currentTheme);
        const nextIndex = (currentIndex + 1) % themes.length;
        setTheme(themes[nextIndex]);
    }
    
    /**
     * Announce theme change to screen readers
     * @param {string} theme - Current theme
     */
    function announceThemeChange(theme) {
        // Create temporary announcement element
        const announcement = document.createElement('div');
        announcement.setAttribute('aria-live', 'polite');
        announcement.setAttribute('aria-atomic', 'true');
        announcement.className = 'visually-hidden';
        
        let message = '';
        switch (theme) {
            case THEMES.HIGH_CONTRAST_DARK:
                message = 'High contrast dark theme activated';
                break;
            case THEMES.HIGH_CONTRAST_LIGHT:
                message = 'High contrast light theme activated';
                break;
            case THEMES.NORMAL:
                message = 'Normal theme activated';
                break;
            default:
                message = 'Theme changed';
        }
        
        announcement.textContent = message;
        document.body.appendChild(announcement);
        
        // Remove after announcement
        setTimeout(() => {
            if (announcement.parentNode) {
                announcement.parentNode.removeChild(announcement);
            }
        }, 1000);
    }
    
    /**
     * Check if high contrast mode is active
     * @returns {boolean} True if any high contrast theme is active
     */
    function isHighContrastMode() {
        return currentTheme === THEMES.HIGH_CONTRAST_DARK || currentTheme === THEMES.HIGH_CONTRAST_LIGHT;
    }
    
    /**
     * Get theme name for display
     * @param {string} theme - Theme identifier
     * @returns {string} Human-readable theme name
     */
    function getThemeDisplayName(theme) {
        switch (theme) {
            case THEMES.HIGH_CONTRAST_DARK:
                return 'High Contrast Dark';
            case THEMES.HIGH_CONTRAST_LIGHT:
                return 'High Contrast Light';
            case THEMES.NORMAL:
                return 'Normal';
            default:
                return 'Unknown';
        }
    }
    
    // Public API
    return {
        THEMES: THEMES,
        initialize: initialize,
        setTheme: setTheme,
        getCurrentTheme: getCurrentTheme,
        cycleTheme: cycleTheme,
        isHighContrastMode: isHighContrastMode,
        getThemeDisplayName: getThemeDisplayName
    };
})();

// Auto-initialize when DOM is ready
if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', window.AccessibilityThemes.initialize);
} else {
    window.AccessibilityThemes.initialize();
}