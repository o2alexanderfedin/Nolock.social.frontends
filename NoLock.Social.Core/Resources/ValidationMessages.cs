namespace NoLock.Social.Core.Resources
{
    /// <summary>
    /// Centralized validation messages to reduce string duplication.
    /// These can be replaced with resource files for localization support.
    /// </summary>
    public static class ValidationMessages
    {
        // Session-related messages
        public const string SessionIdRequired = "Session ID cannot be null or empty";
        public const string SessionNotFound = "Session '{0}' not found";
        public const string SessionAlreadyExists = "Session '{0}' already exists";
        
        // Operation-related messages
        public const string OperationIdRequired = "Operation ID cannot be null or empty";
        public const string OperationNotFound = "Operation '{0}' not found";
        
        // Document-related messages
        public const string DocumentIdRequired = "Document ID cannot be null or empty";
        public const string DocumentNotFound = "Document '{0}' not found";
        public const string DocumentTypeRequired = "Document type must be specified";
        
        // Image-related messages
        public const string ImageIdRequired = "Image ID cannot be null or empty";
        public const string ImageDataRequired = "Image data cannot be null or empty";
        
        // Field validation messages
        public const string FieldNameRequired = "Field name cannot be null or empty";
        public const string FieldValueInvalid = "Field '{0}' has invalid value: {1}";
        
        // Storage-related messages
        public const string StorageInitializationFailed = "Failed to initialize storage: {0}";
        public const string StorageOperationFailed = "Storage operation '{0}' failed: {1}";
        
        // OCR-related messages
        public const string OCRProcessingFailed = "OCR processing failed for document '{0}': {1}";
        public const string OCRServiceUnavailable = "OCR service is currently unavailable";
        
        // Camera-related messages
        public const string CameraPermissionDenied = "Camera permission denied. Please enable camera access";
        public const string CameraInitializationFailed = "Failed to initialize camera: {0}";
        public const string CameraNotAvailable = "No camera device available";
    }
}