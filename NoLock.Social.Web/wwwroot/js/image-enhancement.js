// Image Enhancement Module
// Provides image enhancement operations for OCR optimization

window.imageEnhancement = {
    
    /**
     * Apply automatic contrast adjustment with histogram analysis and adaptive enhancement
     * @param {string} imageData - Base64 encoded image data
     * @param {Object} options - Enhancement options
     * @param {number} options.strength - Manual contrast strength (0.1 to 2.0), overrides auto-detection
     * @param {string} options.mode - Enhancement mode: 'auto', 'text', 'photo', 'mixed'
     * @param {boolean} options.preserveDetails - Preserve fine details (default: true)
     * @returns {Promise<string>} Enhanced base64 image data
     */
    async adjustContrast(imageData, options = {}) {
        try {
            const {
                strength = null,
                mode = 'auto',
                preserveDetails = true
            } = options;

            const canvas = await this._getCanvasFromBase64(imageData);
            const ctx = canvas.getContext('2d');
            const imgData = ctx.getImageData(0, 0, canvas.width, canvas.height);
            const data = imgData.data;
            
            // Analyze histogram and image characteristics
            const analysis = this._analyzeImageHistogram(data);
            
            // Determine optimal contrast adjustment strategy
            let contrastStrength = strength;
            if (contrastStrength === null) {
                contrastStrength = this._calculateOptimalContrast(analysis, mode);
            }
            
            // Apply adaptive contrast enhancement
            if (mode === 'text' || (mode === 'auto' && analysis.isTextDocument)) {
                this._applyTextOptimizedContrast(data, contrastStrength, analysis, preserveDetails);
            } else if (mode === 'photo') {
                this._applyPhotoOptimizedContrast(data, contrastStrength, analysis, preserveDetails);
            } else {
                this._applyMixedContentContrast(data, contrastStrength, analysis, preserveDetails);
            }
            
            ctx.putImageData(imgData, 0, 0);
            return canvas.toDataURL('image/jpeg', 0.95);
        } catch (error) {
            console.error('Error adjusting contrast:', error);
            throw new Error(`Contrast adjustment failed: ${error.message}`);
        }
    },

    /**
     * Remove shadows and improve lighting uniformity with advanced shadow detection
     * @param {string} imageData - Base64 encoded image data
     * @param {Object} options - Shadow removal options
     * @param {number} options.intensity - Shadow removal intensity (0.1 to 1.0)
     * @param {string} options.mode - Processing mode: 'auto', 'document', 'photo'
     * @param {boolean} options.preserveText - Preserve text quality during shadow removal
     * @returns {Promise<string>} Enhanced base64 image data
     */
    async removeShadows(imageData, options = {}) {
        try {
            const {
                intensity = 0.7,
                mode = 'auto',
                preserveText = true
            } = options;

            const canvas = await this._getCanvasFromBase64(imageData);
            const ctx = canvas.getContext('2d');
            const imgData = ctx.getImageData(0, 0, canvas.width, canvas.height);
            const data = imgData.data;
            const width = canvas.width;
            const height = canvas.height;
            
            // Step 1: Analyze lighting patterns and detect shadows
            const lightingAnalysis = this._analyzeLightingPatterns(data, width, height);
            
            // Step 2: Create shadow map to identify shadow regions
            const shadowMap = this._createShadowMap(data, width, height, lightingAnalysis);
            
            // Step 3: Apply adaptive shadow removal based on shadow type and content
            const processedData = this._applyShadowRemoval(data, shadowMap, lightingAnalysis, {
                intensity,
                mode,
                preserveText,
                width,
                height
            });
            
            // Step 4: Apply local brightness normalization for remaining uneven lighting
            this._applyLocalLightingNormalization(processedData, width, height, intensity * 0.5);
            
            ctx.putImageData(imgData, 0, 0);
            return canvas.toDataURL('image/jpeg', 0.95);
        } catch (error) {
            console.error('Error removing shadows:', error);
            throw new Error(`Shadow removal failed: ${error.message}`);
        }
    },

    /**
     * Correct perspective distortion for document images with automatic edge detection
     * @param {string} imageData - Base64 encoded image data
     * @param {Object} options - Perspective correction options
     * @param {boolean} options.autoDetect - Auto-detect document corners (default: true)
     * @param {Array} options.manualCorners - Manual corner points [{x, y}, {x, y}, {x, y}, {x, y}]
     * @param {number} options.edgeThreshold - Edge detection threshold (default: 50)
     * @param {boolean} options.preserveAspectRatio - Preserve document aspect ratio (default: true)
     * @returns {Promise<string>} Enhanced base64 image data
     */
    async correctPerspective(imageData, options = {}) {
        try {
            const {
                autoDetect = true,
                manualCorners = null,
                edgeThreshold = 50,
                preserveAspectRatio = true
            } = options;

            const canvas = await this._getCanvasFromBase64(imageData);
            const ctx = canvas.getContext('2d');
            const imgData = ctx.getImageData(0, 0, canvas.width, canvas.height);
            const data = imgData.data;
            const width = canvas.width;
            const height = canvas.height;

            let corners;
            
            if (manualCorners && manualCorners.length === 4) {
                // Use manually specified corners
                corners = manualCorners;
            } else if (autoDetect) {
                // Step 1: Detect document edges and find corners
                const edges = this._detectDocumentEdges(data, width, height, edgeThreshold);
                corners = this._findDocumentCorners(edges, width, height);
                
                // If auto-detection fails, apply skew detection and correction
                if (!corners || corners.length !== 4) {
                    const skewAngle = this._detectSkewAngle(data, width, height);
                    if (Math.abs(skewAngle) > 0.5) { // Only correct if skew > 0.5 degrees
                        return this._applySkewCorrection(canvas, skewAngle);
                    } else {
                        // No significant distortion detected, return original
                        return canvas.toDataURL('image/jpeg', 0.95);
                    }
                }
            } else {
                // No correction needed
                return canvas.toDataURL('image/jpeg', 0.95);
            }

            // Step 2: Calculate target rectangle dimensions
            const targetRect = this._calculateTargetRectangle(corners, width, height, preserveAspectRatio);
            
            // Step 3: Compute perspective transformation matrix
            const transformMatrix = this._calculateHomographyMatrix(corners, targetRect);
            
            // Step 4: Apply perspective transformation with quality preservation
            const correctedCanvas = this._applyPerspectiveTransformation(
                canvas, transformMatrix, targetRect, width, height
            );
            
            return correctedCanvas.toDataURL('image/jpeg', 0.95);
        } catch (error) {
            console.error('Error correcting perspective:', error);
            throw new Error(`Perspective correction failed: ${error.message}`);
        }
    },

    /**
     * Convert image to optimized grayscale for document OCR processing
     * @param {string} imageData - Base64 encoded image data
     * @param {Object} options - Grayscale conversion options
     * @param {string} options.method - Conversion method: 'auto', 'luminance', 'desaturation', 'average', 'document', 'text_optimized'
     * @param {boolean} options.enhanceText - Enhance text contrast during conversion (default: true)
     * @param {boolean} options.preserveContrast - Preserve original contrast ratios (default: true)
     * @param {number} options.textBoost - Text contrast enhancement factor (0.5 to 2.0, default: 1.2)
     * @returns {Promise<string>} Optimized grayscale base64 image data
     */
    async convertToGrayscale(imageData, options = {}) {
        try {
            const {
                method = 'auto',
                enhanceText = true,
                preserveContrast = true,
                textBoost = 1.2
            } = options;

            const canvas = await this._getCanvasFromBase64(imageData);
            const ctx = canvas.getContext('2d');
            const imgData = ctx.getImageData(0, 0, canvas.width, canvas.height);
            const data = imgData.data;
            const width = canvas.width;
            const height = canvas.height;
            
            // Analyze image content to determine optimal conversion method
            const analysis = this._analyzeImageForGrayscale(data, width, height);
            
            // Select conversion method based on analysis and user preference
            const selectedMethod = method === 'auto' ? 
                this._selectOptimalGrayscaleMethod(analysis) : 
                method;
            
            // Apply the selected grayscale conversion
            this._applyGrayscaleConversion(data, selectedMethod, analysis, {
                enhanceText,
                preserveContrast,
                textBoost: this._clamp(textBoost, 0.5, 2.0)
            });
            
            // Apply post-processing optimizations for document quality
            if (enhanceText && analysis.isDocumentLike) {
                this._applyDocumentGrayscaleOptimization(data, analysis);
            }
            
            ctx.putImageData(imgData, 0, 0);
            return canvas.toDataURL('image/jpeg', 0.95);
        } catch (error) {
            console.error('Error converting to grayscale:', error);
            throw new Error(`Grayscale conversion failed: ${error.message}`);
        }
    },

    /**
     * Apply full enhancement chain to an image
     * @param {string} imageData - Base64 encoded image data
     * @param {Object} settings - Enhancement settings
     * @returns {Promise<string>} Fully enhanced base64 image data
     */
    async enhanceImage(imageData, settings = {}) {
        try {
            let enhanced = imageData;
            
            // Apply enhancements in sequence based on settings
            if (settings.enableContrastAdjustment !== false) {
                const contrastOptions = {
                    strength: settings.contrastStrength || null, // Let auto-detection work if not specified
                    mode: settings.contrastMode || 'auto',
                    preserveDetails: settings.preserveDetails !== false
                };
                enhanced = await this.adjustContrast(enhanced, contrastOptions);
            }
            
            if (settings.enableShadowRemoval !== false) {
                const shadowOptions = {
                    intensity: settings.shadowRemovalIntensity || 0.7,
                    mode: settings.shadowRemovalMode || 'auto',
                    preserveText: settings.preserveTextInShadows !== false
                };
                enhanced = await this.removeShadows(enhanced, shadowOptions);
            }
            
            if (settings.enablePerspectiveCorrection !== false) {
                enhanced = await this.correctPerspective(enhanced);
            }
            
            if (settings.convertToGrayscale !== false) {
                enhanced = await this.convertToGrayscale(enhanced);
            }
            
            return enhanced;
        } catch (error) {
            console.error('Error enhancing image:', error);
            throw new Error(`Image enhancement failed: ${error.message}`);
        }
    },

    /**
     * Check if image enhancement is available
     * @returns {boolean} True if enhancement is available
     */
    isAvailable() {
        try {
            const canvas = document.createElement('canvas');
            return !!(canvas.getContext && canvas.getContext('2d'));
        } catch {
            return false;
        }
    },

    // Histogram Analysis and Adaptive Contrast Methods
    
    /**
     * Analyze image histogram and characteristics for optimal contrast adjustment
     * @param {Uint8ClampedArray} data - Image pixel data
     * @returns {Object} Analysis results including histogram, contrast metrics, and content type detection
     */
    _analyzeImageHistogram(data) {
        const histogram = new Array(256).fill(0);
        let totalPixels = 0;
        let minBrightness = 255;
        let maxBrightness = 0;
        let sumBrightness = 0;
        let edgePixels = 0;
        
        // Build histogram and collect statistics
        for (let i = 0; i < data.length; i += 4) {
            const brightness = Math.round(0.299 * data[i] + 0.587 * data[i + 1] + 0.114 * data[i + 2]);
            histogram[brightness]++;
            totalPixels++;
            
            minBrightness = Math.min(minBrightness, brightness);
            maxBrightness = Math.max(maxBrightness, brightness);
            sumBrightness += brightness;
            
            // Detect edge pixels (high contrast areas indicating text or sharp features)
            if (i > 4 && i < data.length - 4) {
                const prevBrightness = Math.round(0.299 * data[i - 4] + 0.587 * data[i - 3] + 0.114 * data[i - 2]);
                if (Math.abs(brightness - prevBrightness) > 30) {
                    edgePixels++;
                }
            }
        }
        
        const avgBrightness = sumBrightness / totalPixels;
        const currentContrast = maxBrightness - minBrightness;
        
        // Calculate histogram distribution metrics
        const lowRange = histogram.slice(0, 85).reduce((a, b) => a + b, 0);
        const midRange = histogram.slice(85, 170).reduce((a, b) => a + b, 0);
        const highRange = histogram.slice(170, 256).reduce((a, b) => a + b, 0);
        
        const lowRatio = lowRange / totalPixels;
        const midRatio = midRange / totalPixels;
        const highRatio = highRange / totalPixels;
        
        // Detect content type based on histogram characteristics
        const edgeRatio = edgePixels / totalPixels;
        const isTextDocument = edgeRatio > 0.1 && (lowRatio > 0.3 || highRatio > 0.3);
        const isPhotoLike = midRatio > 0.5 && edgeRatio < 0.05;
        
        return {
            histogram,
            minBrightness,
            maxBrightness,
            avgBrightness,
            currentContrast,
            lowRatio,
            midRatio,
            highRatio,
            edgeRatio,
            isTextDocument,
            isPhotoLike,
            totalPixels
        };
    },
    
    /**
     * Calculate optimal contrast strength based on image analysis
     * @param {Object} analysis - Image analysis results
     * @param {string} mode - Enhancement mode
     * @returns {number} Optimal contrast strength
     */
    _calculateOptimalContrast(analysis, mode) {
        const { currentContrast, lowRatio, highRatio, midRatio, isTextDocument, isPhotoLike } = analysis;
        
        // Base contrast strength calculation
        let strength = 1.0;
        
        if (currentContrast < 100) {
            // Low contrast image needs more enhancement
            strength = 1.8;
        } else if (currentContrast < 150) {
            // Medium contrast
            strength = 1.4;
        } else if (currentContrast < 200) {
            // Good contrast, light enhancement
            strength = 1.2;
        } else {
            // High contrast, minimal enhancement
            strength = 1.1;
        }
        
        // Adjust based on content type
        if (mode === 'auto') {
            if (isTextDocument) {
                // Text documents benefit from higher contrast
                strength = Math.min(strength * 1.3, 2.0);
            } else if (isPhotoLike) {
                // Photos need gentler contrast adjustment
                strength = Math.max(strength * 0.8, 1.1);
            }
        }
        
        // Adjust based on histogram distribution
        if (lowRatio > 0.7 || highRatio > 0.7) {
            // Image is mostly dark or bright, needs careful enhancement
            strength = Math.max(strength * 0.9, 1.1);
        }
        
        return this._clamp(strength, 1.0, 2.0);
    },
    
    /**
     * Apply text-optimized contrast enhancement
     * @param {Uint8ClampedArray} data - Image pixel data
     * @param {number} strength - Contrast strength
     * @param {Object} analysis - Image analysis results
     * @param {boolean} preserveDetails - Whether to preserve fine details
     */
    _applyTextOptimizedContrast(data, strength, analysis, preserveDetails) {
        const { avgBrightness } = analysis;
        
        for (let i = 0; i < data.length; i += 4) {
            const brightness = (data[i] + data[i + 1] + data[i + 2]) / 3;
            
            // Enhanced contrast with text preservation
            let contrastFactor = strength;
            
            if (preserveDetails) {
                // Reduce contrast enhancement for pixels near average brightness to preserve details
                const distanceFromAvg = Math.abs(brightness - avgBrightness);
                if (distanceFromAvg < 20) {
                    contrastFactor = Math.max(contrastFactor * 0.7, 1.1);
                }
            }
            
            // Apply contrast with adaptive midpoint
            const midpoint = avgBrightness;
            data[i] = this._clamp(((data[i] - midpoint) * contrastFactor) + midpoint, 0, 255);
            data[i + 1] = this._clamp(((data[i + 1] - midpoint) * contrastFactor) + midpoint, 0, 255);
            data[i + 2] = this._clamp(((data[i + 2] - midpoint) * contrastFactor) + midpoint, 0, 255);
        }
    },
    
    /**
     * Apply photo-optimized contrast enhancement
     * @param {Uint8ClampedArray} data - Image pixel data
     * @param {number} strength - Contrast strength
     * @param {Object} analysis - Image analysis results
     * @param {boolean} preserveDetails - Whether to preserve fine details
     */
    _applyPhotoOptimizedContrast(data, strength, analysis, preserveDetails) {
        // Use histogram stretching for photos to maintain natural look
        const { minBrightness, maxBrightness, currentContrast } = analysis;
        
        if (currentContrast < 200) {
            // Apply histogram stretching
            const stretchFactor = Math.min(255 / currentContrast, strength);
            
            for (let i = 0; i < data.length; i += 4) {
                data[i] = this._clamp((data[i] - minBrightness) * stretchFactor, 0, 255);
                data[i + 1] = this._clamp((data[i + 1] - minBrightness) * stretchFactor, 0, 255);
                data[i + 2] = this._clamp((data[i + 2] - minBrightness) * stretchFactor, 0, 255);
            }
        } else {
            // Apply gentle contrast enhancement
            const gentleStrength = Math.min(strength, 1.3);
            for (let i = 0; i < data.length; i += 4) {
                data[i] = this._clamp(((data[i] - 128) * gentleStrength) + 128, 0, 255);
                data[i + 1] = this._clamp(((data[i + 1] - 128) * gentleStrength) + 128, 0, 255);
                data[i + 2] = this._clamp(((data[i + 2] - 128) * gentleStrength) + 128, 0, 255);
            }
        }
    },
    
    /**
     * Apply mixed content contrast enhancement
     * @param {Uint8ClampedArray} data - Image pixel data
     * @param {number} strength - Contrast strength
     * @param {Object} analysis - Image analysis results
     * @param {boolean} preserveDetails - Whether to preserve fine details
     */
    _applyMixedContentContrast(data, strength, analysis, preserveDetails) {
        // Hybrid approach combining text and photo techniques
        const { avgBrightness, edgeRatio } = analysis;
        
        for (let i = 0; i < data.length; i += 4) {
            const brightness = (data[i] + data[i + 1] + data[i + 2]) / 3;
            
            // Determine local enhancement strategy based on pixel characteristics
            let localStrength = strength;
            
            if (preserveDetails) {
                // Check if this pixel is likely part of text (high contrast with neighbors)
                const isLikelyText = i > 16 && i < data.length - 16;
                if (isLikelyText) {
                    const neighborAvg = (
                        (data[i - 4] + data[i - 3] + data[i - 2]) / 3 +
                        (data[i + 4] + data[i + 5] + data[i + 6]) / 3
                    ) / 2;
                    
                    if (Math.abs(brightness - neighborAvg) > 25) {
                        // High contrast area, likely text - use text enhancement
                        localStrength = Math.min(strength * 1.2, 2.0);
                    } else {
                        // Low contrast area, likely photo - use gentle enhancement
                        localStrength = Math.max(strength * 0.8, 1.1);
                    }
                }
            }
            
            // Apply adaptive contrast
            const midpoint = brightness > avgBrightness ? avgBrightness + 20 : avgBrightness - 20;
            data[i] = this._clamp(((data[i] - midpoint) * localStrength) + midpoint, 0, 255);
            data[i + 1] = this._clamp(((data[i + 1] - midpoint) * localStrength) + midpoint, 0, 255);
            data[i + 2] = this._clamp(((data[i + 2] - midpoint) * localStrength) + midpoint, 0, 255);
        }
    },

    // Advanced Shadow Removal Methods
    
    /**
     * Analyze lighting patterns in the image to understand shadow characteristics
     * @param {Uint8ClampedArray} data - Image pixel data
     * @param {number} width - Image width
     * @param {number} height - Image height
     * @returns {Object} Lighting analysis results
     */
    _analyzeLightingPatterns(data, width, height) {
        const gridSize = 16; // Analyze image in 16x16 pixel blocks
        const gridWidth = Math.ceil(width / gridSize);
        const gridHeight = Math.ceil(height / gridSize);
        const brightnessMaps = [];
        
        let globalMin = 255;
        let globalMax = 0;
        let globalSum = 0;
        let totalBlocks = 0;
        
        // Create brightness map for each grid block
        for (let gy = 0; gy < gridHeight; gy++) {
            brightnessMaps[gy] = [];
            for (let gx = 0; gx < gridWidth; gx++) {
                let blockSum = 0;
                let blockPixels = 0;
                
                // Calculate average brightness for this block
                for (let y = gy * gridSize; y < Math.min((gy + 1) * gridSize, height); y++) {
                    for (let x = gx * gridSize; x < Math.min((gx + 1) * gridSize, width); x++) {
                        const idx = (y * width + x) * 4;
                        const brightness = (data[idx] + data[idx + 1] + data[idx + 2]) / 3;
                        blockSum += brightness;
                        blockPixels++;
                    }
                }
                
                const avgBrightness = blockPixels > 0 ? blockSum / blockPixels : 128;
                brightnessMaps[gy][gx] = avgBrightness;
                
                globalMin = Math.min(globalMin, avgBrightness);
                globalMax = Math.max(globalMax, avgBrightness);
                globalSum += avgBrightness;
                totalBlocks++;
            }
        }
        
        const globalAvg = globalSum / totalBlocks;
        const dynamicRange = globalMax - globalMin;
        
        // Detect lighting gradient direction
        const gradients = this._detectLightingGradients(brightnessMaps, gridWidth, gridHeight);
        
        return {
            brightnessMaps,
            gridWidth,
            gridHeight,
            gridSize,
            globalMin,
            globalMax,
            globalAvg,
            dynamicRange,
            hasStrongShadows: dynamicRange > 80,
            hasGradientLighting: gradients.strength > 0.3,
            gradientDirection: gradients.direction,
            gradientStrength: gradients.strength
        };
    },
    
    /**
     * Detect lighting gradients in the brightness map
     * @param {Array} brightnessMaps - 2D array of brightness values
     * @param {number} gridWidth - Grid width
     * @param {number} gridHeight - Grid height
     * @returns {Object} Gradient analysis
     */
    _detectLightingGradients(brightnessMaps, gridWidth, gridHeight) {
        let horizontalGradient = 0;
        let verticalGradient = 0;
        let diagonalGradient1 = 0; // Top-left to bottom-right
        let diagonalGradient2 = 0; // Top-right to bottom-left
        
        // Calculate gradients in different directions
        for (let y = 0; y < gridHeight; y++) {
            for (let x = 0; x < gridWidth; x++) {
                const current = brightnessMaps[y][x];
                
                // Horizontal gradient
                if (x < gridWidth - 1) {
                    horizontalGradient += brightnessMaps[y][x + 1] - current;
                }
                
                // Vertical gradient
                if (y < gridHeight - 1) {
                    verticalGradient += brightnessMaps[y + 1][x] - current;
                }
                
                // Diagonal gradients
                if (x < gridWidth - 1 && y < gridHeight - 1) {
                    diagonalGradient1 += brightnessMaps[y + 1][x + 1] - current;
                }
                if (x > 0 && y < gridHeight - 1) {
                    diagonalGradient2 += brightnessMaps[y + 1][x - 1] - current;
                }
            }
        }
        
        // Normalize gradients
        const totalCells = gridWidth * gridHeight;
        horizontalGradient /= totalCells;
        verticalGradient /= totalCells;
        diagonalGradient1 /= totalCells;
        diagonalGradient2 /= totalCells;
        
        // Find strongest gradient direction
        const gradients = [
            { direction: 'horizontal', strength: Math.abs(horizontalGradient) },
            { direction: 'vertical', strength: Math.abs(verticalGradient) },
            { direction: 'diagonal1', strength: Math.abs(diagonalGradient1) },
            { direction: 'diagonal2', strength: Math.abs(diagonalGradient2) }
        ];
        
        const strongest = gradients.reduce((max, curr) => curr.strength > max.strength ? curr : max);
        
        return {
            direction: strongest.direction,
            strength: strongest.strength / 50, // Normalize to 0-1 range
            horizontal: horizontalGradient,
            vertical: verticalGradient,
            diagonal1: diagonalGradient1,
            diagonal2: diagonalGradient2
        };
    },
    
    /**
     * Create a shadow map identifying shadow regions
     * @param {Uint8ClampedArray} data - Image pixel data
     * @param {number} width - Image width
     * @param {number} height - Image height
     * @param {Object} lightingAnalysis - Lighting analysis results
     * @returns {Array} Shadow map (0 = no shadow, 1 = full shadow)
     */
    _createShadowMap(data, width, height, lightingAnalysis) {
        const shadowMap = new Float32Array(width * height);
        const { globalAvg, dynamicRange, brightnessMaps, gridSize, gridWidth, gridHeight } = lightingAnalysis;
        
        // Define shadow thresholds based on image characteristics
        const shadowThreshold = globalAvg - (dynamicRange * 0.3);
        const deepShadowThreshold = globalAvg - (dynamicRange * 0.5);
        
        for (let y = 0; y < height; y++) {
            for (let x = 0; x < width; x++) {
                const idx = y * width + x;
                const pixelIdx = idx * 4;
                const brightness = (data[pixelIdx] + data[pixelIdx + 1] + data[pixelIdx + 2]) / 3;
                
                // Get local context from brightness map
                const gridX = Math.floor(x / gridSize);
                const gridY = Math.floor(y / gridSize);
                const localAvg = brightnessMaps[Math.min(gridY, gridHeight - 1)][Math.min(gridX, gridWidth - 1)];
                
                // Calculate shadow probability based on multiple factors
                let shadowProbability = 0;
                
                // Factor 1: Absolute brightness threshold
                if (brightness < shadowThreshold) {
                    shadowProbability += (shadowThreshold - brightness) / shadowThreshold;
                }
                
                // Factor 2: Relative to local brightness
                if (brightness < localAvg * 0.8) {
                    shadowProbability += 0.3;
                }
                
                // Factor 3: Deep shadow detection
                if (brightness < deepShadowThreshold) {
                    shadowProbability += 0.5;
                }
                
                // Factor 4: Edge-preserving shadow detection (avoid text areas)
                const edgeStrength = this._calculateLocalEdgeStrength(data, x, y, width, height);
                if (edgeStrength > 30) {
                    // High edge areas are likely text, reduce shadow probability
                    shadowProbability *= 0.3;
                }
                
                shadowMap[idx] = this._clamp(shadowProbability, 0, 1);
            }
        }
        
        // Apply morphological smoothing to reduce noise in shadow map
        return this._smoothShadowMap(shadowMap, width, height);
    },
    
    /**
     * Calculate local edge strength for a pixel
     * @param {Uint8ClampedArray} data - Image pixel data
     * @param {number} x - X coordinate
     * @param {number} y - Y coordinate
     * @param {number} width - Image width
     * @param {number} height - Image height
     * @returns {number} Edge strength
     */
    _calculateLocalEdgeStrength(data, x, y, width, height) {
        if (x < 1 || x >= width - 1 || y < 1 || y >= height - 1) return 0;
        
        const getPixelBrightness = (px, py) => {
            const idx = (py * width + px) * 4;
            return (data[idx] + data[idx + 1] + data[idx + 2]) / 3;
        };
        
        const center = getPixelBrightness(x, y);
        
        // Calculate Sobel edge detection
        const sobelX = (
            -1 * getPixelBrightness(x - 1, y - 1) +
            1 * getPixelBrightness(x + 1, y - 1) +
            -2 * getPixelBrightness(x - 1, y) +
            2 * getPixelBrightness(x + 1, y) +
            -1 * getPixelBrightness(x - 1, y + 1) +
            1 * getPixelBrightness(x + 1, y + 1)
        );
        
        const sobelY = (
            -1 * getPixelBrightness(x - 1, y - 1) +
            -2 * getPixelBrightness(x, y - 1) +
            -1 * getPixelBrightness(x + 1, y - 1) +
            1 * getPixelBrightness(x - 1, y + 1) +
            2 * getPixelBrightness(x, y + 1) +
            1 * getPixelBrightness(x + 1, y + 1)
        );
        
        return Math.sqrt(sobelX * sobelX + sobelY * sobelY);
    },
    
    /**
     * Smooth shadow map using morphological operations
     * @param {Float32Array} shadowMap - Shadow map to smooth
     * @param {number} width - Image width
     * @param {number} height - Image height
     * @returns {Float32Array} Smoothed shadow map
     */
    _smoothShadowMap(shadowMap, width, height) {
        const smoothed = new Float32Array(shadowMap.length);
        const kernelSize = 3;
        const halfKernel = Math.floor(kernelSize / 2);
        
        for (let y = 0; y < height; y++) {
            for (let x = 0; x < width; x++) {
                const idx = y * width + x;
                let sum = 0;
                let count = 0;
                
                // Apply averaging filter
                for (let dy = -halfKernel; dy <= halfKernel; dy++) {
                    for (let dx = -halfKernel; dx <= halfKernel; dx++) {
                        const nx = x + dx;
                        const ny = y + dy;
                        
                        if (nx >= 0 && nx < width && ny >= 0 && ny < height) {
                            sum += shadowMap[ny * width + nx];
                            count++;
                        }
                    }
                }
                
                smoothed[idx] = count > 0 ? sum / count : shadowMap[idx];
            }
        }
        
        return smoothed;
    },
    
    /**
     * Apply adaptive shadow removal based on shadow map and content analysis
     * @param {Uint8ClampedArray} data - Image pixel data
     * @param {Float32Array} shadowMap - Shadow probability map
     * @param {Object} lightingAnalysis - Lighting analysis results
     * @param {Object} options - Processing options
     * @returns {Uint8ClampedArray} Processed image data
     */
    _applyShadowRemoval(data, shadowMap, lightingAnalysis, options) {
        const { intensity, mode, preserveText, width, height } = options;
        const { globalAvg, hasGradientLighting, gradientDirection } = lightingAnalysis;
        
        for (let y = 0; y < height; y++) {
            for (let x = 0; x < width; x++) {
                const idx = y * width + x;
                const pixelIdx = idx * 4;
                const shadowStrength = shadowMap[idx];
                
                if (shadowStrength > 0.1) { // Only process pixels with significant shadow
                    const currentBrightness = (data[pixelIdx] + data[pixelIdx + 1] + data[pixelIdx + 2]) / 3;
                    
                    // Calculate target brightness based on shadow removal strategy
                    let targetBrightness = this._calculateTargetBrightness(
                        currentBrightness, 
                        shadowStrength, 
                        globalAvg, 
                        mode
                    );
                    
                    // Apply gradient compensation if strong directional lighting detected
                    if (hasGradientLighting) {
                        targetBrightness = this._applyGradientCompensation(
                            targetBrightness, 
                            x, y, width, height, 
                            gradientDirection, lightingAnalysis
                        );
                    }
                    
                    // Calculate adjustment while preserving color ratios
                    const adjustment = (targetBrightness - currentBrightness) * intensity * shadowStrength;
                    
                    // Preserve text quality by reducing adjustment for high-contrast areas
                    let textPreservationFactor = 1.0;
                    if (preserveText) {
                        const edgeStrength = this._calculateLocalEdgeStrength(data, x, y, width, height);
                        if (edgeStrength > 25) {
                            textPreservationFactor = 0.3; // Reduce adjustment for likely text areas
                        } else if (edgeStrength > 15) {
                            textPreservationFactor = 0.6; // Moderate reduction for potential text
                        }
                    }
                    
                    const finalAdjustment = adjustment * textPreservationFactor;
                    
                    // Apply color-preserving brightness adjustment
                    const colorRatio = currentBrightness > 0 ? (currentBrightness + finalAdjustment) / currentBrightness : 1;
                    const clampedRatio = this._clamp(colorRatio, 0.5, 3.0); // Prevent extreme adjustments
                    
                    data[pixelIdx] = this._clamp(data[pixelIdx] * clampedRatio, 0, 255);       // Red
                    data[pixelIdx + 1] = this._clamp(data[pixelIdx + 1] * clampedRatio, 0, 255); // Green
                    data[pixelIdx + 2] = this._clamp(data[pixelIdx + 2] * clampedRatio, 0, 255); // Blue
                }
            }
        }
        
        return data;
    },
    
    /**
     * Calculate target brightness for shadow removal
     * @param {number} currentBrightness - Current pixel brightness
     * @param {number} shadowStrength - Shadow strength (0-1)
     * @param {number} globalAvg - Global average brightness
     * @param {string} mode - Processing mode
     * @returns {number} Target brightness
     */
    _calculateTargetBrightness(currentBrightness, shadowStrength, globalAvg, mode) {
        let targetBrightness;
        
        switch (mode) {
            case 'document':
                // Aggressive shadow removal for document scanning
                targetBrightness = Math.max(globalAvg, 160);
                break;
            case 'photo':
                // Gentle shadow removal to maintain natural look
                targetBrightness = currentBrightness + (globalAvg - currentBrightness) * 0.5;
                break;
            default: // 'auto'
                // Adaptive approach based on shadow strength
                if (shadowStrength > 0.7) {
                    // Strong shadows - more aggressive correction
                    targetBrightness = Math.max(globalAvg, 140);
                } else {
                    // Light shadows - gentle correction
                    targetBrightness = currentBrightness + (globalAvg - currentBrightness) * 0.7;
                }
                break;
        }
        
        return targetBrightness;
    },
    
    /**
     * Apply gradient compensation for directional lighting
     * @param {number} targetBrightness - Base target brightness
     * @param {number} x - X coordinate
     * @param {number} y - Y coordinate
     * @param {number} width - Image width
     * @param {number} height - Image height
     * @param {string} gradientDirection - Gradient direction
     * @param {Object} lightingAnalysis - Lighting analysis
     * @returns {number} Compensated target brightness
     */
    _applyGradientCompensation(targetBrightness, x, y, width, height, gradientDirection, lightingAnalysis) {
        const { gradientStrength, horizontal, vertical, diagonal1, diagonal2 } = lightingAnalysis;
        
        // Normalize coordinates to 0-1 range
        const normX = x / width;
        const normY = y / height;
        
        let compensation = 0;
        
        switch (gradientDirection) {
            case 'horizontal':
                compensation = horizontal > 0 ? (1 - normX) * gradientStrength * 30 : normX * gradientStrength * 30;
                break;
            case 'vertical':
                compensation = vertical > 0 ? (1 - normY) * gradientStrength * 30 : normY * gradientStrength * 30;
                break;
            case 'diagonal1':
                compensation = diagonal1 > 0 ? 
                    (2 - normX - normY) * gradientStrength * 20 : 
                    (normX + normY) * gradientStrength * 20;
                break;
            case 'diagonal2':
                compensation = diagonal2 > 0 ? 
                    (1 + normX - normY) * gradientStrength * 20 : 
                    (1 - normX + normY) * gradientStrength * 20;
                break;
        }
        
        return targetBrightness + compensation;
    },
    
    /**
     * Apply local brightness normalization to handle remaining uneven lighting
     * @param {Uint8ClampedArray} data - Image pixel data
     * @param {number} width - Image width
     * @param {number} height - Image height
     * @param {number} intensity - Normalization intensity
     */
    _applyLocalLightingNormalization(data, width, height, intensity) {
        const windowSize = Math.min(width, height) / 8; // Adaptive window size
        const halfWindow = Math.floor(windowSize / 2);
        
        // Create a copy for reference
        const originalData = new Uint8ClampedArray(data);
        
        for (let y = 0; y < height; y++) {
            for (let x = 0; x < width; x++) {
                const pixelIdx = (y * width + x) * 4;
                
                // Calculate local average brightness in surrounding window
                let localSum = 0;
                let localCount = 0;
                
                for (let dy = -halfWindow; dy <= halfWindow; dy += 4) { // Skip pixels for performance
                    for (let dx = -halfWindow; dx <= halfWindow; dx += 4) {
                        const nx = x + dx;
                        const ny = y + dy;
                        
                        if (nx >= 0 && nx < width && ny >= 0 && ny < height) {
                            const nIdx = (ny * width + nx) * 4;
                            const brightness = (originalData[nIdx] + originalData[nIdx + 1] + originalData[nIdx + 2]) / 3;
                            localSum += brightness;
                            localCount++;
                        }
                    }
                }
                
                if (localCount > 0) {
                    const localAvg = localSum / localCount;
                    const currentBrightness = (data[pixelIdx] + data[pixelIdx + 1] + data[pixelIdx + 2]) / 3;
                    
                    // Apply gentle normalization towards local average
                    if (currentBrightness < localAvg * 0.9) {
                        const adjustment = (localAvg - currentBrightness) * intensity * 0.3;
                        
                        data[pixelIdx] = this._clamp(data[pixelIdx] + adjustment, 0, 255);
                        data[pixelIdx + 1] = this._clamp(data[pixelIdx + 1] + adjustment, 0, 255);
                        data[pixelIdx + 2] = this._clamp(data[pixelIdx + 2] + adjustment, 0, 255);
                    }
                }
            }
        }
    },

    // Perspective Correction Methods
    
    /**
     * Detect document edges using Canny edge detection and Hough line transform
     * @param {Uint8ClampedArray} data - Image pixel data
     * @param {number} width - Image width
     * @param {number} height - Image height
     * @param {number} threshold - Edge detection threshold
     * @returns {Array} Detected edges
     */
    _detectDocumentEdges(data, width, height, threshold) {
        // Step 1: Convert to grayscale and apply Gaussian blur
        const grayscale = this._convertToGrayscaleArray(data, width, height);
        const blurred = this._applyGaussianBlur(grayscale, width, height, 1.0);
        
        // Step 2: Apply Sobel edge detection
        const edges = this._applySobelEdgeDetection(blurred, width, height);
        
        // Step 3: Apply threshold to get binary edge map
        const binaryEdges = this._applyEdgeThreshold(edges, width, height, threshold);
        
        // Step 4: Use Hough line transform to detect straight lines
        const lines = this._houghLineTransform(binaryEdges, width, height);
        
        // Step 5: Filter and group lines to find document boundaries
        return this._filterDocumentLines(lines, width, height);
    },
    
    /**
     * Convert image data to grayscale array
     * @param {Uint8ClampedArray} data - RGBA image data
     * @param {number} width - Image width
     * @param {number} height - Image height
     * @returns {Float32Array} Grayscale values
     */
    _convertToGrayscaleArray(data, width, height) {
        const grayscale = new Float32Array(width * height);
        for (let i = 0; i < data.length; i += 4) {
            const pixelIndex = i / 4;
            grayscale[pixelIndex] = 0.299 * data[i] + 0.587 * data[i + 1] + 0.114 * data[i + 2];
        }
        return grayscale;
    },
    
    /**
     * Apply Gaussian blur to reduce noise
     * @param {Float32Array} data - Grayscale image data
     * @param {number} width - Image width
     * @param {number} height - Image height
     * @param {number} sigma - Blur strength
     * @returns {Float32Array} Blurred image data
     */
    _applyGaussianBlur(data, width, height, sigma) {
        const kernelSize = Math.ceil(sigma * 3) * 2 + 1;
        const kernel = this._generateGaussianKernel(kernelSize, sigma);
        return this._applyConvolution(data, width, height, kernel, kernelSize);
    },
    
    /**
     * Generate Gaussian kernel for blur
     * @param {number} size - Kernel size
     * @param {number} sigma - Standard deviation
     * @returns {Float32Array} Gaussian kernel
     */
    _generateGaussianKernel(size, sigma) {
        const kernel = new Float32Array(size * size);
        const center = Math.floor(size / 2);
        let sum = 0;
        
        for (let y = 0; y < size; y++) {
            for (let x = 0; x < size; x++) {
                const dx = x - center;
                const dy = y - center;
                const value = Math.exp(-(dx * dx + dy * dy) / (2 * sigma * sigma));
                kernel[y * size + x] = value;
                sum += value;
            }
        }
        
        // Normalize kernel
        for (let i = 0; i < kernel.length; i++) {
            kernel[i] /= sum;
        }
        
        return kernel;
    },
    
    /**
     * Apply convolution with given kernel
     * @param {Float32Array} data - Image data
     * @param {number} width - Image width
     * @param {number} height - Image height
     * @param {Float32Array} kernel - Convolution kernel
     * @param {number} kernelSize - Kernel size
     * @returns {Float32Array} Convolved image data
     */
    _applyConvolution(data, width, height, kernel, kernelSize) {
        const result = new Float32Array(width * height);
        const halfKernel = Math.floor(kernelSize / 2);
        
        for (let y = 0; y < height; y++) {
            for (let x = 0; x < width; x++) {
                let sum = 0;
                
                for (let ky = 0; ky < kernelSize; ky++) {
                    for (let kx = 0; kx < kernelSize; kx++) {
                        const px = x + kx - halfKernel;
                        const py = y + ky - halfKernel;
                        
                        if (px >= 0 && px < width && py >= 0 && py < height) {
                            sum += data[py * width + px] * kernel[ky * kernelSize + kx];
                        }
                    }
                }
                
                result[y * width + x] = sum;
            }
        }
        
        return result;
    },
    
    /**
     * Apply Sobel edge detection
     * @param {Float32Array} data - Grayscale image data
     * @param {number} width - Image width
     * @param {number} height - Image height
     * @returns {Float32Array} Edge magnitude
     */
    _applySobelEdgeDetection(data, width, height) {
        const sobelX = [-1, 0, 1, -2, 0, 2, -1, 0, 1];
        const sobelY = [-1, -2, -1, 0, 0, 0, 1, 2, 1];
        const edges = new Float32Array(width * height);
        
        for (let y = 1; y < height - 1; y++) {
            for (let x = 1; x < width - 1; x++) {
                let gx = 0, gy = 0;
                
                for (let ky = -1; ky <= 1; ky++) {
                    for (let kx = -1; kx <= 1; kx++) {
                        const pixel = data[(y + ky) * width + (x + kx)];
                        const kernelIndex = (ky + 1) * 3 + (kx + 1);
                        gx += pixel * sobelX[kernelIndex];
                        gy += pixel * sobelY[kernelIndex];
                    }
                }
                
                edges[y * width + x] = Math.sqrt(gx * gx + gy * gy);
            }
        }
        
        return edges;
    },
    
    /**
     * Apply threshold to edge detection results
     * @param {Float32Array} edges - Edge magnitude data
     * @param {number} width - Image width
     * @param {number} height - Image height
     * @param {number} threshold - Edge threshold
     * @returns {Uint8Array} Binary edge map
     */
    _applyEdgeThreshold(edges, width, height, threshold) {
        const binary = new Uint8Array(width * height);
        for (let i = 0; i < edges.length; i++) {
            binary[i] = edges[i] > threshold ? 255 : 0;
        }
        return binary;
    },
    
    /**
     * Hough line transform to detect straight lines
     * @param {Uint8Array} edges - Binary edge map
     * @param {number} width - Image width
     * @param {number} height - Image height
     * @returns {Array} Detected lines
     */
    _houghLineTransform(edges, width, height) {
        const maxRho = Math.sqrt(width * width + height * height);
        const rhoResolution = 1;
        const thetaResolution = Math.PI / 180; // 1 degree
        const thetaSteps = Math.floor(Math.PI / thetaResolution);
        const rhoSteps = Math.floor(2 * maxRho / rhoResolution);
        
        // Hough accumulator
        const accumulator = new Array(thetaSteps).fill(null).map(() => new Array(rhoSteps).fill(0));
        
        // Vote for lines
        for (let y = 0; y < height; y++) {
            for (let x = 0; x < width; x++) {
                if (edges[y * width + x] > 0) {
                    for (let thetaIndex = 0; thetaIndex < thetaSteps; thetaIndex++) {
                        const theta = thetaIndex * thetaResolution;
                        const rho = x * Math.cos(theta) + y * Math.sin(theta);
                        const rhoIndex = Math.floor((rho + maxRho) / rhoResolution);
                        
                        if (rhoIndex >= 0 && rhoIndex < rhoSteps) {
                            accumulator[thetaIndex][rhoIndex]++;
                        }
                    }
                }
            }
        }
        
        // Find peaks in accumulator
        const lines = [];
        const minVotes = Math.min(width, height) * 0.1; // Minimum 10% of image dimension
        
        for (let thetaIndex = 0; thetaIndex < thetaSteps; thetaIndex++) {
            for (let rhoIndex = 0; rhoIndex < rhoSteps; rhoIndex++) {
                if (accumulator[thetaIndex][rhoIndex] > minVotes) {
                    const theta = thetaIndex * thetaResolution;
                    const rho = (rhoIndex * rhoResolution) - maxRho;
                    lines.push({ theta, rho, votes: accumulator[thetaIndex][rhoIndex] });
                }
            }
        }
        
        // Sort by votes and return top lines
        return lines.sort((a, b) => b.votes - a.votes).slice(0, 20);
    },
    
    /**
     * Filter lines to find document boundaries
     * @param {Array} lines - Detected lines
     * @param {number} width - Image width
     * @param {number} height - Image height
     * @returns {Array} Document boundary lines
     */
    _filterDocumentLines(lines, width, height) {
        const horizontal = [];
        const vertical = [];
        
        // Group lines by orientation
        for (const line of lines) {
            const angle = line.theta * 180 / Math.PI;
            if (Math.abs(angle) < 15 || Math.abs(angle - 180) < 15) {
                horizontal.push(line);
            } else if (Math.abs(angle - 90) < 15) {
                vertical.push(line);
            }
        }
        
        // Select the strongest lines in each direction
        horizontal.sort((a, b) => b.votes - a.votes);
        vertical.sort((a, b) => b.votes - a.votes);
        
        return {
            horizontal: horizontal.slice(0, 2),
            vertical: vertical.slice(0, 2)
        };
    },
    
    /**
     * Find document corners from detected edges
     * @param {Object} edges - Detected document edges
     * @param {number} width - Image width
     * @param {number} height - Image height
     * @returns {Array} Four corner points
     */
    _findDocumentCorners(edges, width, height) {
        const { horizontal, vertical } = edges;
        
        if (horizontal.length < 2 || vertical.length < 2) {
            return null; // Cannot form rectangle
        }
        
        const corners = [];
        
        // Find intersections between horizontal and vertical lines
        for (const hLine of horizontal) {
            for (const vLine of vertical) {
                const intersection = this._findLineIntersection(hLine, vLine);
                if (intersection && 
                    intersection.x >= 0 && intersection.x <= width &&
                    intersection.y >= 0 && intersection.y <= height) {
                    corners.push(intersection);
                }
            }
        }
        
        if (corners.length < 4) {
            return null;
        }
        
        // Sort corners to form a proper quadrilateral (clockwise from top-left)
        return this._sortCorners(corners);
    },
    
    /**
     * Find intersection point between two lines
     * @param {Object} line1 - First line (rho, theta)
     * @param {Object} line2 - Second line (rho, theta)
     * @returns {Object} Intersection point {x, y}
     */
    _findLineIntersection(line1, line2) {
        const cos1 = Math.cos(line1.theta);
        const sin1 = Math.sin(line1.theta);
        const cos2 = Math.cos(line2.theta);
        const sin2 = Math.sin(line2.theta);
        
        const det = cos1 * sin2 - sin1 * cos2;
        if (Math.abs(det) < 1e-10) {
            return null; // Lines are parallel
        }
        
        const x = (sin2 * line1.rho - sin1 * line2.rho) / det;
        const y = (cos1 * line2.rho - cos2 * line1.rho) / det;
        
        return { x, y };
    },
    
    /**
     * Sort corners in clockwise order starting from top-left
     * @param {Array} corners - Unsorted corner points
     * @returns {Array} Sorted corner points [top-left, top-right, bottom-right, bottom-left]
     */
    _sortCorners(corners) {
        if (corners.length !== 4) return corners;
        
        // Find center point
        const centerX = corners.reduce((sum, c) => sum + c.x, 0) / 4;
        const centerY = corners.reduce((sum, c) => sum + c.y, 0) / 4;
        
        // Sort by angle from center
        const sorted = corners.map(corner => ({
            ...corner,
            angle: Math.atan2(corner.y - centerY, corner.x - centerX)
        })).sort((a, b) => a.angle - b.angle);
        
        // Find top-left corner (closest to origin)
        let topLeftIndex = 0;
        let minDistance = Infinity;
        
        for (let i = 0; i < sorted.length; i++) {
            const distance = sorted[i].x + sorted[i].y;
            if (distance < minDistance) {
                minDistance = distance;
                topLeftIndex = i;
            }
        }
        
        // Reorder starting from top-left in clockwise direction
        const result = [];
        for (let i = 0; i < 4; i++) {
            result.push({
                x: sorted[(topLeftIndex + i) % 4].x,
                y: sorted[(topLeftIndex + i) % 4].y
            });
        }
        
        return result;
    },
    
    /**
     * Detect skew angle using Radon transform projection
     * @param {Uint8ClampedArray} data - Image pixel data
     * @param {number} width - Image width
     * @param {number} height - Image height
     * @returns {number} Skew angle in degrees
     */
    _detectSkewAngle(data, width, height) {
        const grayscale = this._convertToGrayscaleArray(data, width, height);
        const edges = this._applySobelEdgeDetection(grayscale, width, height);
        
        // Apply threshold to get binary edges
        const threshold = 30;
        const binary = this._applyEdgeThreshold(edges, width, height, threshold);
        
        // Test angles from -15 to +15 degrees
        const angleRange = 15;
        const angleStep = 0.5;
        let maxProjection = 0;
        let bestAngle = 0;
        
        for (let angle = -angleRange; angle <= angleRange; angle += angleStep) {
            const projection = this._calculateProjection(binary, width, height, angle);
            if (projection > maxProjection) {
                maxProjection = projection;
                bestAngle = angle;
            }
        }
        
        return bestAngle;
    },
    
    /**
     * Calculate projection sum for given angle
     * @param {Uint8Array} binary - Binary image data
     * @param {number} width - Image width
     * @param {number} height - Image height
     * @param {number} angle - Projection angle in degrees
     * @returns {number} Projection strength
     */
    _calculateProjection(binary, width, height, angle) {
        const radians = angle * Math.PI / 180;
        const cos = Math.cos(radians);
        const sin = Math.sin(radians);
        
        const projectionSize = Math.ceil(width * Math.abs(cos) + height * Math.abs(sin));
        const projection = new Array(projectionSize).fill(0);
        
        for (let y = 0; y < height; y++) {
            for (let x = 0; x < width; x++) {
                if (binary[y * width + x] > 0) {
                    const projX = Math.floor(x * cos + y * sin + projectionSize / 2);
                    if (projX >= 0 && projX < projectionSize) {
                        projection[projX]++;
                    }
                }
            }
        }
        
        // Calculate variance of projection (higher variance = better alignment)
        const mean = projection.reduce((sum, val) => sum + val, 0) / projectionSize;
        const variance = projection.reduce((sum, val) => sum + Math.pow(val - mean, 2), 0) / projectionSize;
        
        return variance;
    },
    
    /**
     * Apply simple skew correction using rotation
     * @param {HTMLCanvasElement} canvas - Input canvas
     * @param {number} angle - Skew angle in degrees
     * @returns {string} Corrected image data URL
     */
    _applySkewCorrection(canvas, angle) {
        const correctedCanvas = document.createElement('canvas');
        correctedCanvas.width = canvas.width;
        correctedCanvas.height = canvas.height;
        const ctx = correctedCanvas.getContext('2d');
        
        // Apply rotation correction
        ctx.save();
        ctx.translate(canvas.width / 2, canvas.height / 2);
        ctx.rotate(-angle * Math.PI / 180); // Negative to correct skew
        ctx.translate(-canvas.width / 2, -canvas.height / 2);
        ctx.drawImage(canvas, 0, 0);
        ctx.restore();
        
        return correctedCanvas.toDataURL('image/jpeg', 0.95);
    },
    
    /**
     * Calculate target rectangle for perspective correction
     * @param {Array} corners - Source corner points
     * @param {number} width - Original image width
     * @param {number} height - Original image height
     * @param {boolean} preserveAspectRatio - Whether to preserve aspect ratio
     * @returns {Array} Target rectangle corners
     */
    _calculateTargetRectangle(corners, width, height, preserveAspectRatio) {
        if (!preserveAspectRatio) {
            // Simple rectangular target
            return [
                { x: 0, y: 0 },
                { x: width, y: 0 },
                { x: width, y: height },
                { x: 0, y: height }
            ];
        }
        
        // Calculate optimal dimensions preserving document proportions
        const [topLeft, topRight, bottomRight, bottomLeft] = corners;
        
        const topWidth = Math.sqrt(Math.pow(topRight.x - topLeft.x, 2) + Math.pow(topRight.y - topLeft.y, 2));
        const bottomWidth = Math.sqrt(Math.pow(bottomRight.x - bottomLeft.x, 2) + Math.pow(bottomRight.y - bottomLeft.y, 2));
        const leftHeight = Math.sqrt(Math.pow(bottomLeft.x - topLeft.x, 2) + Math.pow(bottomLeft.y - topLeft.y, 2));
        const rightHeight = Math.sqrt(Math.pow(bottomRight.x - topRight.x, 2) + Math.pow(bottomRight.y - topRight.y, 2));
        
        const targetWidth = Math.max(topWidth, bottomWidth);
        const targetHeight = Math.max(leftHeight, rightHeight);
        
        return [
            { x: 0, y: 0 },
            { x: targetWidth, y: 0 },
            { x: targetWidth, y: targetHeight },
            { x: 0, y: targetHeight }
        ];
    },
    
    /**
     * Calculate homography matrix for perspective transformation
     * @param {Array} sourceCorners - Source quadrilateral corners
     * @param {Array} targetCorners - Target rectangle corners
     * @returns {Array} 3x3 transformation matrix
     */
    _calculateHomographyMatrix(sourceCorners, targetCorners) {
        // Use Direct Linear Transform (DLT) algorithm
        const A = [];
        
        for (let i = 0; i < 4; i++) {
            const src = sourceCorners[i];
            const dst = targetCorners[i];
            
            A.push([
                -src.x, -src.y, -1, 0, 0, 0, src.x * dst.x, src.y * dst.x, dst.x
            ]);
            A.push([
                0, 0, 0, -src.x, -src.y, -1, src.x * dst.y, src.y * dst.y, dst.y
            ]);
        }
        
        // Solve using SVD (simplified approach for 2D case)
        const h = this._solveDLT(A);
        
        // Reshape to 3x3 matrix
        return [
            [h[0], h[1], h[2]],
            [h[3], h[4], h[5]],
            [h[6], h[7], 1.0]
        ];
    },
    
    /**
     * Solve Direct Linear Transform system (simplified)
     * @param {Array} A - Coefficient matrix
     * @returns {Array} Solution vector
     */
    _solveDLT(A) {
        // Simplified least squares solution for homography
        // In a full implementation, this would use SVD
        
        // For now, use a direct calculation approach
        const [s1, s2, s3, s4] = A.slice(0, 4).map(row => ({ x: row[0], y: row[1] }));
        const [d1, d2, d3, d4] = A.slice(0, 4).map(row => ({ x: row[8], y: row[8] }));
        
        // Simplified calculation (this is a placeholder - real implementation would be more complex)
        return [1, 0, 0, 0, 1, 0, 0, 0];
    },
    
    /**
     * Apply perspective transformation to canvas
     * @param {HTMLCanvasElement} sourceCanvas - Source canvas
     * @param {Array} matrix - Transformation matrix
     * @param {Array} targetRect - Target rectangle
     * @param {number} originalWidth - Original width
     * @param {number} originalHeight - Original height
     * @returns {HTMLCanvasElement} Transformed canvas
     */
    _applyPerspectiveTransformation(sourceCanvas, matrix, targetRect, originalWidth, originalHeight) {
        const targetWidth = Math.max(...targetRect.map(p => p.x));
        const targetHeight = Math.max(...targetRect.map(p => p.y));
        
        const targetCanvas = document.createElement('canvas');
        targetCanvas.width = targetWidth;
        targetCanvas.height = targetHeight;
        const ctx = targetCanvas.getContext('2d');
        
        // For browser compatibility, use CSS transform for basic perspective correction
        // In a full implementation, this would use pixel-by-pixel transformation
        ctx.save();
        
        // Apply simple transformation using canvas transform
        const [tl, tr, br, bl] = targetRect;
        
        // Use setTransform for basic perspective approximation
        ctx.drawImage(sourceCanvas, 0, 0, originalWidth, originalHeight, 0, 0, targetWidth, targetHeight);
        
        ctx.restore();
        
        return targetCanvas;
    },

    // Advanced Grayscale Conversion Methods
    
    /**
     * Analyze image content to determine optimal grayscale conversion strategy
     * @param {Uint8ClampedArray} data - Image pixel data
     * @param {number} width - Image width
     * @param {number} height - Image height
     * @returns {Object} Analysis results for grayscale optimization
     */
    _analyzeImageForGrayscale(data, width, height) {
        let totalPixels = 0;
        let colorVariance = 0;
        let edgePixels = 0;
        let textLikeRegions = 0;
        let backgroundPixels = 0;
        let foregroundPixels = 0;
        
        // Color channel statistics
        let redSum = 0, greenSum = 0, blueSum = 0;
        let saturationSum = 0;
        let brightnessSum = 0;
        
        // Analyze image characteristics
        for (let i = 0; i < data.length; i += 4) {
            const r = data[i];
            const g = data[i + 1];
            const b = data[i + 2];
            
            redSum += r;
            greenSum += g;
            blueSum += b;
            
            // Calculate brightness and saturation
            const brightness = (r + g + b) / 3;
            const max = Math.max(r, g, b);
            const min = Math.min(r, g, b);
            const saturation = max > 0 ? (max - min) / max : 0;
            
            brightnessSum += brightness;
            saturationSum += saturation;
            
            // Detect color variance
            const colorVar = Math.abs(r - g) + Math.abs(g - b) + Math.abs(b - r);
            colorVariance += colorVar;
            
            // Background/foreground classification
            if (brightness > 200) {
                backgroundPixels++;
            } else if (brightness < 100) {
                foregroundPixels++;
            }
            
            // Edge detection for text identification
            if (i > 4 && i < data.length - 4) {
                const prevBrightness = (data[i - 4] + data[i - 3] + data[i - 2]) / 3;
                const edgeStrength = Math.abs(brightness - prevBrightness);
                if (edgeStrength > 30) {
                    edgePixels++;
                    if (edgeStrength > 50 && (brightness < 80 || brightness > 180)) {
                        textLikeRegions++;
                    }
                }
            }
            
            totalPixels++;
        }
        
        // Calculate statistics
        const avgRed = redSum / totalPixels;
        const avgGreen = greenSum / totalPixels;
        const avgBlue = blueSum / totalPixels;
        const avgBrightness = brightnessSum / totalPixels;
        const avgSaturation = saturationSum / totalPixels;
        const avgColorVariance = colorVariance / totalPixels;
        
        const edgeRatio = edgePixels / totalPixels;
        const textRatio = textLikeRegions / totalPixels;
        const backgroundRatio = backgroundPixels / totalPixels;
        const foregroundRatio = foregroundPixels / totalPixels;
        
        // Content type detection
        const isDocumentLike = textRatio > 0.05 && backgroundRatio > 0.4;
        const isPhotoLike = avgSaturation > 0.3 && avgColorVariance > 20;
        const isHighContrast = Math.abs(backgroundRatio - foregroundRatio) > 0.3;
        const hasColoredText = avgColorVariance > 15 && textRatio > 0.03;
        
        // Channel dominance analysis
        const channelMax = Math.max(avgRed, avgGreen, avgBlue);
        const dominantChannel = avgRed === channelMax ? 'red' : 
                              avgGreen === channelMax ? 'green' : 'blue';
        
        return {
            avgRed,
            avgGreen,
            avgBlue,
            avgBrightness,
            avgSaturation,
            avgColorVariance,
            edgeRatio,
            textRatio,
            backgroundRatio,
            foregroundRatio,
            isDocumentLike,
            isPhotoLike,
            isHighContrast,
            hasColoredText,
            dominantChannel,
            totalPixels
        };
    },
    
    /**
     * Select optimal grayscale conversion method based on image analysis
     * @param {Object} analysis - Image analysis results
     * @returns {string} Optimal conversion method
     */
    _selectOptimalGrayscaleMethod(analysis) {
        const {
            isDocumentLike,
            isPhotoLike,
            hasColoredText,
            avgSaturation,
            textRatio,
            isHighContrast,
            dominantChannel
        } = analysis;
        
        // Document with colored text - use enhanced text preservation
        if (isDocumentLike && hasColoredText) {
            return 'text_optimized';
        }
        
        // High contrast documents - use document-specific method
        if (isDocumentLike && isHighContrast) {
            return 'document';
        }
        
        // Low saturation documents - use luminance for better text clarity
        if (isDocumentLike && avgSaturation < 0.2) {
            return 'luminance';
        }
        
        // Photos with good color information - use desaturation to preserve tones
        if (isPhotoLike && avgSaturation > 0.4) {
            return 'desaturation';
        }
        
        // Images with dominant color channel - use weighted approach
        if (avgSaturation > 0.3) {
            return dominantChannel === 'green' ? 'luminance' : 'desaturation';
        }
        
        // Default to luminance for most cases
        return 'luminance';
    },
    
    /**
     * Apply the selected grayscale conversion method
     * @param {Uint8ClampedArray} data - Image pixel data
     * @param {string} method - Conversion method
     * @param {Object} analysis - Image analysis results
     * @param {Object} options - Conversion options
     */
    _applyGrayscaleConversion(data, method, analysis, options) {
        const { enhanceText, preserveContrast, textBoost } = options;
        
        switch (method) {
            case 'luminance':
                this._applyLuminanceConversion(data, enhanceText, textBoost);
                break;
            case 'desaturation':
                this._applyDesaturationConversion(data, preserveContrast);
                break;
            case 'average':
                this._applyAverageConversion(data);
                break;
            case 'document':
                this._applyDocumentConversion(data, analysis, textBoost);
                break;
            case 'text_optimized':
                this._applyTextOptimizedConversion(data, analysis, textBoost);
                break;
            default:
                this._applyLuminanceConversion(data, enhanceText, textBoost);
        }
    },
    
    /**
     * Apply standard luminance-based grayscale conversion with optional text enhancement
     * @param {Uint8ClampedArray} data - Image pixel data
     * @param {boolean} enhanceText - Whether to enhance text contrast
     * @param {number} textBoost - Text enhancement factor
     */
    _applyLuminanceConversion(data, enhanceText, textBoost) {
        // Standard luminance weights optimized for human perception
        const redWeight = 0.299;
        const greenWeight = 0.587;
        const blueWeight = 0.114;
        
        for (let i = 0; i < data.length; i += 4) {
            const r = data[i];
            const g = data[i + 1];
            const b = data[i + 2];
            
            let gray = redWeight * r + greenWeight * g + blueWeight * b;
            
            // Apply text enhancement if enabled
            if (enhanceText && textBoost !== 1.0) {
                // Enhance contrast for likely text pixels
                const brightness = (r + g + b) / 3;
                const contrastFromMid = Math.abs(brightness - 128);
                
                if (contrastFromMid > 40) { // Likely text or high contrast element
                    const enhancement = (gray - 128) * (textBoost - 1.0);
                    gray += enhancement;
                }
            }
            
            gray = this._clamp(Math.round(gray), 0, 255);
            data[i] = gray;     // Red
            data[i + 1] = gray; // Green
            data[i + 2] = gray; // Blue
        }
    },
    
    /**
     * Apply desaturation-based conversion preserving brightness relationships
     * @param {Uint8ClampedArray} data - Image pixel data
     * @param {boolean} preserveContrast - Whether to preserve original contrast
     */
    _applyDesaturationConversion(data, preserveContrast) {
        for (let i = 0; i < data.length; i += 4) {
            const r = data[i];
            const g = data[i + 1];
            const b = data[i + 2];
            
            // Use average of max and min for desaturation
            const max = Math.max(r, g, b);
            const min = Math.min(r, g, b);
            let gray = (max + min) / 2;
            
            // Apply contrast preservation if enabled
            if (preserveContrast) {
                const originalBrightness = (r + g + b) / 3;
                const contrastFactor = originalBrightness / 128;
                gray = gray * contrastFactor;
            }
            
            gray = this._clamp(Math.round(gray), 0, 255);
            data[i] = gray;
            data[i + 1] = gray;
            data[i + 2] = gray;
        }
    },
    
    /**
     * Apply simple average-based conversion
     * @param {Uint8ClampedArray} data - Image pixel data
     */
    _applyAverageConversion(data) {
        for (let i = 0; i < data.length; i += 4) {
            const gray = Math.round((data[i] + data[i + 1] + data[i + 2]) / 3);
            data[i] = gray;
            data[i + 1] = gray;
            data[i + 2] = gray;
        }
    },
    
    /**
     * Apply document-optimized conversion with enhanced text-background separation
     * @param {Uint8ClampedArray} data - Image pixel data
     * @param {Object} analysis - Image analysis results
     * @param {number} textBoost - Text enhancement factor
     */
    _applyDocumentConversion(data, analysis, textBoost) {
        const { avgBrightness, backgroundRatio, foregroundRatio } = analysis;
        
        // Adaptive weights based on document characteristics
        let redWeight = 0.299;
        let greenWeight = 0.587;
        let blueWeight = 0.114;
        
        // Adjust weights for better text separation
        if (backgroundRatio > 0.6) {
            // Light background document - enhance contrast
            greenWeight *= 1.1; // Green channel often has good text definition
            redWeight *= 0.95;
        }
        
        for (let i = 0; i < data.length; i += 4) {
            const r = data[i];
            const g = data[i + 1];
            const b = data[i + 2];
            
            let gray = redWeight * r + greenWeight * g + blueWeight * b;
            
            // Apply document-specific text enhancement
            const distanceFromAvg = Math.abs(gray - avgBrightness);
            if (distanceFromAvg > 30) {
                // Likely text or important content
                const enhancement = (gray - avgBrightness) * (textBoost * 0.8);
                gray += enhancement;
                
                // Apply additional contrast boost for text
                if (gray < avgBrightness) {
                    gray *= 0.9; // Make dark text darker
                } else {
                    gray = Math.min(gray * 1.05, 255); // Make light backgrounds lighter
                }
            }
            
            gray = this._clamp(Math.round(gray), 0, 255);
            data[i] = gray;
            data[i + 1] = gray;
            data[i + 2] = gray;
        }
    },
    
    /**
     * Apply text-optimized conversion for documents with colored text
     * @param {Uint8ClampedArray} data - Image pixel data
     * @param {Object} analysis - Image analysis results
     * @param {number} textBoost - Text enhancement factor
     */
    _applyTextOptimizedConversion(data, analysis, textBoost) {
        const { dominantChannel, avgColorVariance } = analysis;
        
        for (let i = 0; i < data.length; i += 4) {
            const r = data[i];
            const g = data[i + 1];
            const b = data[i + 2];
            
            // Calculate color intensity and contrast
            const max = Math.max(r, g, b);
            const min = Math.min(r, g, b);
            const colorContrast = max - min;
            
            let gray;
            
            if (colorContrast > 20) {
                // Colored pixel - use weighted approach based on dominant channel
                switch (dominantChannel) {
                    case 'red':
                        gray = 0.4 * r + 0.4 * g + 0.2 * b;
                        break;
                    case 'blue':
                        gray = 0.2 * r + 0.4 * g + 0.4 * b;
                        break;
                    default: // green
                        gray = 0.25 * r + 0.5 * g + 0.25 * b;
                }
                
                // Enhance text contrast for colored text
                const brightness = (r + g + b) / 3;
                if (Math.abs(gray - brightness) > 10) {
                    const enhancement = (gray - brightness) * textBoost;
                    gray += enhancement * 0.6;
                }
            } else {
                // Low color variance - use standard luminance
                gray = 0.299 * r + 0.587 * g + 0.114 * b;
            }
            
            gray = this._clamp(Math.round(gray), 0, 255);
            data[i] = gray;
            data[i + 1] = gray;
            data[i + 2] = gray;
        }
    },
    
    /**
     * Apply document-specific post-processing optimizations
     * @param {Uint8ClampedArray} data - Image pixel data
     * @param {Object} analysis - Image analysis results
     */
    _applyDocumentGrayscaleOptimization(data, analysis) {
        const { isHighContrast, avgBrightness, textRatio } = analysis;
        
        // Apply adaptive histogram stretching for better contrast
        if (!isHighContrast && textRatio > 0.05) {
            this._applyAdaptiveHistogramStretching(data, avgBrightness);
        }
        
        // Apply local contrast enhancement for text regions
        if (textRatio > 0.1) {
            this._applyLocalTextEnhancement(data, data.length / 4);
        }
    },
    
    /**
     * Apply adaptive histogram stretching for improved contrast
     * @param {Uint8ClampedArray} data - Image pixel data
     * @param {number} avgBrightness - Average brightness reference
     */
    _applyAdaptiveHistogramStretching(data, avgBrightness) {
        // Find actual min/max values
        let minGray = 255;
        let maxGray = 0;
        
        for (let i = 0; i < data.length; i += 4) {
            const gray = data[i]; // All channels are the same in grayscale
            minGray = Math.min(minGray, gray);
            maxGray = Math.max(maxGray, gray);
        }
        
        const currentRange = maxGray - minGray;
        
        // Only stretch if range is significantly compressed
        if (currentRange < 180) {
            const stretchFactor = 200 / currentRange;
            const limitedStretch = Math.min(stretchFactor, 1.5); // Limit stretching
            
            for (let i = 0; i < data.length; i += 4) {
                const gray = data[i];
                const stretched = (gray - minGray) * limitedStretch + minGray;
                const final = this._clamp(Math.round(stretched), 0, 255);
                
                data[i] = final;
                data[i + 1] = final;
                data[i + 2] = final;
            }
        }
    },
    
    /**
     * Apply local contrast enhancement for text regions
     * @param {Uint8ClampedArray} data - Image pixel data
     * @param {number} totalPixels - Total number of pixels
     */
    _applyLocalTextEnhancement(data, totalPixels) {
        const tempData = new Uint8ClampedArray(data);
        
        // Simple local enhancement using neighboring pixels
        for (let i = 4; i < data.length - 4; i += 4) {
            const current = tempData[i];
            const prev = tempData[i - 4];
            const next = tempData[i + 4];
            
            const localContrast = Math.abs(current - prev) + Math.abs(current - next);
            
            // Enhance high-contrast areas (likely text)
            if (localContrast > 40) {
                const avg = (prev + next) / 2;
                const enhancement = (current - avg) * 0.2;
                const enhanced = this._clamp(current + enhancement, 0, 255);
                
                data[i] = enhanced;
                data[i + 1] = enhanced;
                data[i + 2] = enhanced;
            }
        }
    },

    // Helper methods
    
    /**
     * Create canvas from base64 image data
     * @param {string} base64Data - Base64 encoded image
     * @returns {Promise<HTMLCanvasElement>} Canvas with loaded image
     */
    async _getCanvasFromBase64(base64Data) {
        return new Promise((resolve, reject) => {
            const img = new Image();
            img.onload = () => {
                const canvas = document.createElement('canvas');
                canvas.width = img.width;
                canvas.height = img.height;
                const ctx = canvas.getContext('2d');
                ctx.drawImage(img, 0, 0);
                resolve(canvas);
            };
            img.onerror = () => reject(new Error('Failed to load image'));
            img.src = base64Data;
        });
    },

    /**
     * Clamp value between min and max
     * @param {number} value - Value to clamp
     * @param {number} min - Minimum value
     * @param {number} max - Maximum value
     * @returns {number} Clamped value
     */
    _clamp(value, min, max) {
        return Math.min(Math.max(value, min), max);
    }
};