using System;
using System.Collections.Generic;

namespace NoLock.Social.Core.OCR.Models
{
    /// <summary>
    /// Represents a request to submit a document for OCR processing
    /// </summary>
    public class OCRSubmissionRequest
    {
        /// <summary>
        /// The image data as a base64 encoded string
        /// </summary>
        public string ImageData { get; set; }

        /// <summary>
        /// The type of document being submitted for OCR
        /// </summary>
        public DocumentType DocumentType { get; set; }

        /// <summary>
        /// Additional metadata associated with the submission
        /// </summary>
        public Dictionary<string, string> Metadata { get; set; } = new Dictionary<string, string>();

        /// <summary>
        /// Client-provided request ID for tracking purposes
        /// </summary>
        public string ClientRequestId { get; set; } = Guid.NewGuid().ToString();
    }

    /// <summary>
    /// Enumeration of supported document types for OCR processing
    /// </summary>
    public enum DocumentType
    {
        /// <summary>
        /// General document type
        /// </summary>
        General = 0,

        /// <summary>
        /// Invoice or billing document
        /// </summary>
        Invoice = 1,

        /// <summary>
        /// Receipt from purchase
        /// </summary>
        Receipt = 2,

        /// <summary>
        /// Identity document (ID card, passport, etc.)
        /// </summary>
        IdentityDocument = 3,

        /// <summary>
        /// Business card
        /// </summary>
        BusinessCard = 4,

        /// <summary>
        /// Form or application
        /// </summary>
        Form = 5,

        /// <summary>
        /// Contract or legal document
        /// </summary>
        Contract = 6
    }
}