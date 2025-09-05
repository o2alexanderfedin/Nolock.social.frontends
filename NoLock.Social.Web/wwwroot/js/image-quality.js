// Image Quality Analysis Module
// Provides blur detection, lighting analysis, and edge detection for document scanning

window.imageQuality = {
    
    /**
     * Detects blur in an image using Laplacian edge detection
     * @param {ImageData|HTMLCanvasElement|string} imageData - Image data, canvas element, or base64 string
     * @returns {Promise<{blurScore: number, confidence: number, isSharp: boolean}>}
     */
    async detectBlur(imageData) {
        try {
            const canvas = await this._getCanvasFromInput(imageData);
            const ctx = canvas.getContext('2d');
            const imgData = ctx.getImageData(0, 0, canvas.width, canvas.height);
            
            // Convert to grayscale and apply Laplacian filter
            const grayscale = this._convertToGrayscale(imgData);
            const laplacianVariance = this._calculateLaplacianVariance(grayscale, canvas.width, canvas.height);
            
            // Normalize blur score (higher = sharper)
            const blurScore = Math.min(laplacianVariance / 100, 1.0);
            const isSharp = blurScore > 0.3; // Threshold for acceptable sharpness
            const confidence = Math.min(blurScore * 2, 1.0); // Confidence in assessment
            
            return {
                blurScore: Math.round(blurScore * 100) / 100,
                threshold: Math.round(confidence * 100) / 100,
                isSharp: isSharp
            };
            
        } catch (error) {
            console.error('Blur detection failed:', error);
            throw new Error(`Blur detection failed: ${error.message}`);
        }
    },
    
    /**
     * Assesses lighting quality of an image
     * @param {ImageData|HTMLCanvasElement|string} imageData - Image data, canvas element, or base64 string
     * @returns {Promise<{lightingScore: number, brightness: number, contrast: number, isWellLit: boolean}>}
     */
    async assessLighting(imageData) {
        try {
            const canvas = await this._getCanvasFromInput(imageData);
            const ctx = canvas.getContext('2d');
            const imgData = ctx.getImageData(0, 0, canvas.width, canvas.height);
            
            // Analyze brightness and contrast
            const stats = this._calculateImageStats(imgData);
            const histogramAnalysis = this._analyzeHistogram(imgData);
            
            // Calculate lighting score based on multiple factors
            const brightnessScore = this._scoreBrightness(stats.brightness);
            const contrastScore = this._scoreContrast(stats.contrast);
            const distributionScore = histogramAnalysis.distributionScore;
            
            // Weighted combination of factors
            const lightingScore = (brightnessScore * 0.4 + contrastScore * 0.4 + distributionScore * 0.2);
            const isWellLit = lightingScore > 0.6; // Threshold for acceptable lighting
            
            return {
                lightingScore: Math.round(lightingScore * 100) / 100,
                brightness: Math.round(stats.brightness * 100) / 100,
                contrast: Math.round(stats.contrast * 100) / 100,
                isWellLit: isWellLit
            };
            
        } catch (error) {
            console.error('Lighting assessment failed:', error);
            throw new Error(`Lighting assessment failed: ${error.message}`);
        }
    },
    
    /**
     * Detects edges in an image for document boundary analysis
     * @param {ImageData|HTMLCanvasElement|string} imageData - Image data, canvas element, or base64 string
     * @returns {Promise<{edgeScore: number, edgeCount: number, hasRectangle: boolean, confidence: number}>}
     */
    async detectEdges(imageData) {
        try {
            const canvas = await this._getCanvasFromInput(imageData);
            const ctx = canvas.getContext('2d');
            const imgData = ctx.getImageData(0, 0, canvas.width, canvas.height);
            
            // Convert to grayscale for edge detection
            const grayscale = this._convertToGrayscale(imgData);
            const width = canvas.width;
            const height = canvas.height;
            
            // Apply Sobel edge detection
            const edges = this._applySobelEdgeDetection(grayscale, width, height);
            
            // Analyze edge characteristics
            const edgeDensity = this._calculateEdgeDensity(edges, width, height);
            const hasRectangle = this._detectRectangularShape(edges, width, height);
            const cornerSharpness = this._analyzeCornerSharpness(edges, width, height);
            
            // Calculate overall edge score
            const densityScore = Math.min(edgeDensity * 2, 1.0); // Higher density = better
            const rectangleScore = hasRectangle ? 1.0 : 0.3; // Documents should have rectangular boundaries
            const sharpnessScore = cornerSharpness;
            
            // Weighted combination for document quality
            const edgeScore = (densityScore * 0.4 + rectangleScore * 0.4 + sharpnessScore * 0.2);
            
            return {
                edgeScore: Math.round(edgeScore * 100) / 100,
                edgeCount: Math.round(edgeDensity * 100) / 100,
                hasRectangle: hasRectangle,
                confidence: Math.round(cornerSharpness * 100) / 100
            };
            
        } catch (error) {
            console.error('Edge detection failed:', error);
            throw new Error(`Edge detection failed: ${error.message}`);
        }
    },
    
    /**
     * Converts various input types to canvas element
     * @private
     */
    async _getCanvasFromInput(input) {
        if (input instanceof HTMLCanvasElement) {
            return input;
        }
        
        if (input instanceof ImageData) {
            const canvas = document.createElement('canvas');
            canvas.width = input.width;
            canvas.height = input.height;
            const ctx = canvas.getContext('2d');
            ctx.putImageData(input, 0, 0);
            return canvas;
        }
        
        if (typeof input === 'string') {
            // Assume base64 image data
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
                img.onerror = () => reject(new Error('Failed to load image from base64'));
                img.src = input.startsWith('data:') ? input : `data:image/jpeg;base64,${input}`;
            });
        }
        
        throw new Error('Invalid input type. Expected ImageData, Canvas, or base64 string.');
    },
    
    /**
     * Converts ImageData to grayscale array
     * @private
     */
    _convertToGrayscale(imageData) {
        const data = imageData.data;
        const grayscale = new Array(data.length / 4);
        
        for (let i = 0; i < data.length; i += 4) {
            // Standard grayscale conversion formula
            const gray = 0.299 * data[i] + 0.587 * data[i + 1] + 0.114 * data[i + 2];
            grayscale[i / 4] = gray;
        }
        
        return grayscale;
    },
    
    /**
     * Calculates Laplacian variance for blur detection
     * @private
     */
    _calculateLaplacianVariance(grayscale, width, height) {
        // Laplacian kernel for edge detection
        const kernel = [
            0, -1, 0,
            -1, 4, -1,
            0, -1, 0
        ];
        
        let sum = 0;
        let sumSquared = 0;
        let count = 0;
        
        // Apply Laplacian filter (skip borders)
        for (let y = 1; y < height - 1; y++) {
            for (let x = 1; x < width - 1; x++) {
                let laplacian = 0;
                
                // Apply 3x3 kernel
                for (let ky = -1; ky <= 1; ky++) {
                    for (let kx = -1; kx <= 1; kx++) {
                        const pixelIndex = (y + ky) * width + (x + kx);
                        const kernelIndex = (ky + 1) * 3 + (kx + 1);
                        laplacian += grayscale[pixelIndex] * kernel[kernelIndex];
                    }
                }
                
                sum += laplacian;
                sumSquared += laplacian * laplacian;
                count++;
            }
        }
        
        // Calculate variance (measure of edge strength)
        const mean = sum / count;
        const variance = (sumSquared / count) - (mean * mean);
        
        return Math.sqrt(variance);
    },
    
    /**
     * Calculates basic image statistics (brightness and contrast)
     * @private
     */
    _calculateImageStats(imageData) {
        const data = imageData.data;
        let sumR = 0, sumG = 0, sumB = 0;
        let sumSquaredR = 0, sumSquaredG = 0, sumSquaredB = 0;
        const pixelCount = data.length / 4;
        
        for (let i = 0; i < data.length; i += 4) {
            const r = data[i];
            const g = data[i + 1];
            const b = data[i + 2];
            
            sumR += r;
            sumG += g;
            sumB += b;
            
            sumSquaredR += r * r;
            sumSquaredG += g * g;
            sumSquaredB += b * b;
        }
        
        // Calculate mean brightness (0-1 scale)
        const meanR = sumR / pixelCount;
        const meanG = sumG / pixelCount;
        const meanB = sumB / pixelCount;
        const brightness = (meanR + meanG + meanB) / (3 * 255);
        
        // Calculate contrast using standard deviation
        const varR = (sumSquaredR / pixelCount) - (meanR * meanR);
        const varG = (sumSquaredG / pixelCount) - (meanG * meanG);
        const varB = (sumSquaredB / pixelCount) - (meanB * meanB);
        const contrast = (Math.sqrt(varR) + Math.sqrt(varG) + Math.sqrt(varB)) / (3 * 255);
        
        return { brightness, contrast };
    },
    
    /**
     * Analyzes histogram distribution for lighting quality
     * @private
     */
    _analyzeHistogram(imageData) {
        const data = imageData.data;
        const histogram = new Array(256).fill(0);
        const pixelCount = data.length / 4;
        
        // Build luminance histogram
        for (let i = 0; i < data.length; i += 4) {
            const luminance = Math.round(0.299 * data[i] + 0.587 * data[i + 1] + 0.114 * data[i + 2]);
            histogram[luminance]++;
        }
        
        // Analyze distribution characteristics
        let darkPixels = 0;
        let brightPixels = 0;
        let midPixels = 0;
        
        for (let i = 0; i < 256; i++) {
            if (i < 85) darkPixels += histogram[i];
            else if (i > 170) brightPixels += histogram[i];
            else midPixels += histogram[i];
        }
        
        const darkRatio = darkPixels / pixelCount;
        const brightRatio = brightPixels / pixelCount;
        const midRatio = midPixels / pixelCount;
        
        // Good distribution has balanced mid-tones with some darks and lights
        const distributionScore = Math.max(0, 1 - Math.abs(0.6 - midRatio) - Math.max(0, darkRatio - 0.3) - Math.max(0, brightRatio - 0.3));
        
        return { distributionScore, darkRatio, brightRatio, midRatio };
    },
    
    /**
     * Scores brightness quality (0-1 scale)
     * @private
     */
    _scoreBrightness(brightness) {
        // Optimal brightness is around 0.4-0.7 range
        if (brightness >= 0.4 && brightness <= 0.7) {
            return 1.0;
        } else if (brightness >= 0.2 && brightness <= 0.8) {
            // Acceptable range with reduced score
            const distance = Math.min(Math.abs(brightness - 0.4), Math.abs(brightness - 0.7));
            return Math.max(0, 1 - distance * 2);
        } else {
            // Too dark or too bright
            return Math.max(0, 1 - Math.abs(brightness - 0.55) * 3);
        }
    },
    
    /**
     * Scores contrast quality (0-1 scale)
     * @private
     */
    _scoreContrast(contrast) {
        // Good contrast is typically above 0.15 but not oversaturated
        if (contrast >= 0.15 && contrast <= 0.4) {
            return 1.0;
        } else if (contrast >= 0.1 && contrast <= 0.6) {
            // Acceptable range
            return Math.max(0, 1 - Math.abs(contrast - 0.25) * 2);
        } else {
            // Too low or too high contrast
            return Math.max(0, 1 - Math.abs(contrast - 0.25) * 4);
        }
    },
    
    /**
     * Applies Sobel edge detection algorithm
     * @private
     */
    _applySobelEdgeDetection(grayscale, width, height) {
        // Sobel kernels for horizontal and vertical edge detection
        const sobelX = [-1, 0, 1, -2, 0, 2, -1, 0, 1];
        const sobelY = [-1, -2, -1, 0, 0, 0, 1, 2, 1];
        
        const edges = new Array(width * height);
        
        // Apply Sobel operator (skip borders)
        for (let y = 1; y < height - 1; y++) {
            for (let x = 1; x < width - 1; x++) {
                let gx = 0;
                let gy = 0;
                
                // Apply 3x3 kernels
                for (let ky = -1; ky <= 1; ky++) {
                    for (let kx = -1; kx <= 1; kx++) {
                        const pixelIndex = (y + ky) * width + (x + kx);
                        const kernelIndex = (ky + 1) * 3 + (kx + 1);
                        const pixel = grayscale[pixelIndex];
                        
                        gx += pixel * sobelX[kernelIndex];
                        gy += pixel * sobelY[kernelIndex];
                    }
                }
                
                // Calculate edge magnitude
                const magnitude = Math.sqrt(gx * gx + gy * gy);
                edges[y * width + x] = magnitude;
            }
        }
        
        return edges;
    },
    
    /**
     * Calculates edge density for quality assessment
     * @private
     */
    _calculateEdgeDensity(edges, width, height) {
        let strongEdges = 0;
        let totalPixels = 0;
        const threshold = 50; // Edge strength threshold
        
        for (let i = 0; i < edges.length; i++) {
            if (edges[i] !== undefined) {
                totalPixels++;
                if (edges[i] > threshold) {
                    strongEdges++;
                }
            }
        }
        
        return totalPixels > 0 ? strongEdges / totalPixels : 0;
    },
    
    /**
     * Detects rectangular shapes in edge data
     * @private
     */
    _detectRectangularShape(edges, width, height) {
        const threshold = 40;
        let horizontalLines = 0;
        let verticalLines = 0;
        
        // Check for horizontal lines (top and bottom edges)
        for (let y of [Math.floor(height * 0.1), Math.floor(height * 0.9)]) {
            let edgeCount = 0;
            for (let x = 0; x < width; x++) {
                if (edges[y * width + x] > threshold) {
                    edgeCount++;
                }
            }
            if (edgeCount > width * 0.3) { // At least 30% of row has edges
                horizontalLines++;
            }
        }
        
        // Check for vertical lines (left and right edges)
        for (let x of [Math.floor(width * 0.1), Math.floor(width * 0.9)]) {
            let edgeCount = 0;
            for (let y = 0; y < height; y++) {
                if (edges[y * width + x] > threshold) {
                    edgeCount++;
                }
            }
            if (edgeCount > height * 0.3) { // At least 30% of column has edges
                verticalLines++;
            }
        }
        
        // Document should have at least 2 horizontal and 2 vertical edge lines
        return horizontalLines >= 1 && verticalLines >= 1;
    },
    
    /**
     * Analyzes corner sharpness for document quality
     * @private
     */
    _analyzeCornerSharpness(edges, width, height) {
        const cornerRegions = [
            { x: 0, y: 0, w: Math.floor(width * 0.2), h: Math.floor(height * 0.2) }, // Top-left
            { x: Math.floor(width * 0.8), y: 0, w: Math.floor(width * 0.2), h: Math.floor(height * 0.2) }, // Top-right
            { x: 0, y: Math.floor(height * 0.8), w: Math.floor(width * 0.2), h: Math.floor(height * 0.2) }, // Bottom-left
            { x: Math.floor(width * 0.8), y: Math.floor(height * 0.8), w: Math.floor(width * 0.2), h: Math.floor(height * 0.2) } // Bottom-right
        ];
        
        let totalSharpness = 0;
        let validCorners = 0;
        
        for (const region of cornerRegions) {
            let maxEdge = 0;
            let edgeCount = 0;
            
            for (let y = region.y; y < Math.min(region.y + region.h, height); y++) {
                for (let x = region.x; x < Math.min(region.x + region.w, width); x++) {
                    const edge = edges[y * width + x];
                    if (edge !== undefined) {
                        maxEdge = Math.max(maxEdge, edge);
                        if (edge > 30) edgeCount++;
                    }
                }
            }
            
            if (maxEdge > 0) {
                // Normalize sharpness based on edge strength and density in corner
                const density = edgeCount / (region.w * region.h);
                const sharpness = Math.min(maxEdge / 100, 1.0) * Math.min(density * 10, 1.0);
                totalSharpness += sharpness;
                validCorners++;
            }
        }
        
        return validCorners > 0 ? totalSharpness / validCorners : 0;
    }
};