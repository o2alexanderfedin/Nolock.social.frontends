// Focus Management JavaScript Module
// Provides focus utilities for accessibility in camera workflows

let focusTrapData = null;

// Focusable element selector
const FOCUSABLE_SELECTORS = [
    'a[href]',
    'button:not([disabled])',
    'input:not([disabled])',
    'select:not([disabled])',
    'textarea:not([disabled])',
    '[tabindex]:not([tabindex="-1"])',
    '[contenteditable="true"]',
    'audio[controls]',
    'video[controls]',
    'summary'
].join(', ');

// Set focus to an element
export function setFocus(element) {
    try {
        if (element && typeof element.focus === 'function') {
            element.focus();
            return true;
        }
        return false;
    } catch (error) {
        console.error('Error setting focus:', error);
        return false;
    }
}

// Check if element is currently focused
export function isFocused(element) {
    try {
        return document.activeElement === element;
    } catch (error) {
        console.error('Error checking focus state:', error);
        return false;
    }
}

// Get currently active element
export function getActiveElement() {
    try {
        return document.activeElement;
    } catch (error) {
        console.error('Error getting active element:', error);
        return null;
    }
}

// Get all focusable elements within a container
function getFocusableElements(container) {
    if (!container) return [];
    
    try {
        const elements = container.querySelectorAll(FOCUSABLE_SELECTORS);
        return Array.from(elements).filter(element => {
            // Additional checks for visibility and interactability
            const style = window.getComputedStyle(element);
            return (
                style.display !== 'none' &&
                style.visibility !== 'hidden' &&
                !element.hasAttribute('disabled') &&
                element.tabIndex !== -1
            );
        });
    } catch (error) {
        console.error('Error getting focusable elements:', error);
        return [];
    }
}

// Focus first focusable element in container
export function focusFirstElement(container) {
    try {
        const focusableElements = getFocusableElements(container);
        if (focusableElements.length > 0) {
            focusableElements[0].focus();
            return true;
        }
        return false;
    } catch (error) {
        console.error('Error focusing first element:', error);
        return false;
    }
}

// Focus last focusable element in container
export function focusLastElement(container) {
    try {
        const focusableElements = getFocusableElements(container);
        if (focusableElements.length > 0) {
            focusableElements[focusableElements.length - 1].focus();
            return true;
        }
        return false;
    } catch (error) {
        console.error('Error focusing last element:', error);
        return false;
    }
}

// Handle focus trap keyboard navigation
function handleFocusTrapKeydown(event) {
    if (!focusTrapData || event.key !== 'Tab') return;

    const { container, focusableElements } = focusTrapData;
    if (focusableElements.length === 0) return;

    const currentIndex = focusableElements.indexOf(document.activeElement);
    
    if (event.shiftKey) {
        // Shift + Tab: move to previous element
        if (currentIndex <= 0) {
            event.preventDefault();
            focusableElements[focusableElements.length - 1].focus();
        }
    } else {
        // Tab: move to next element
        if (currentIndex >= focusableElements.length - 1) {
            event.preventDefault();
            focusableElements[0].focus();
        }
    }
}

// Trap focus within a container
export function trapFocus(container) {
    try {
        if (!container) {
            console.error('Container element is required for focus trap');
            return false;
        }

        // Release existing trap first
        releaseFocusTrap();

        const focusableElements = getFocusableElements(container);
        
        if (focusableElements.length === 0) {
            console.warn('No focusable elements found in container for focus trap');
            return false;
        }

        focusTrapData = {
            container,
            focusableElements,
            previousActiveElement: document.activeElement
        };

        // Add keyboard event listener for tab trapping
        document.addEventListener('keydown', handleFocusTrapKeydown, true);

        // Focus first element
        focusableElements[0].focus();

        console.log('Focus trap activated');
        return true;
    } catch (error) {
        console.error('Error trapping focus:', error);
        return false;
    }
}

// Release focus trap
export function releaseFocusTrap() {
    try {
        if (focusTrapData) {
            // Remove event listener
            document.removeEventListener('keydown', handleFocusTrapKeydown, true);
            
            // Restore previous focus if element still exists
            if (focusTrapData.previousActiveElement && 
                document.contains(focusTrapData.previousActiveElement)) {
                focusTrapData.previousActiveElement.focus();
            }
            
            focusTrapData = null;
            console.log('Focus trap released');
        }
        return true;
    } catch (error) {
        console.error('Error releasing focus trap:', error);
        return false;
    }
}

// Initialize focus management
export function initialize() {
    try {
        console.log('Focus management initialized');
        
        // Clean up on page unload
        window.addEventListener('beforeunload', releaseFocusTrap);
        
        return true;
    } catch (error) {
        console.error('Error initializing focus management:', error);
        return false;
    }
}

// Cleanup function
export function dispose() {
    try {
        releaseFocusTrap();
        window.removeEventListener('beforeunload', releaseFocusTrap);
        console.log('Focus management disposed');
        return true;
    } catch (error) {
        console.error('Error disposing focus management:', error);
        return false;
    }
}

// Auto-initialize when module loads
initialize();