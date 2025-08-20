namespace NoLock.Social.Core.Common.Constants
{
    /// <summary>
    /// HTTP status code constants for consistent error handling and classification.
    /// </summary>
    public static class HttpStatusConstants
    {
        /// <summary>
        /// Client error status codes (4xx).
        /// </summary>
        public static class ClientError
        {
            /// <summary>
            /// Bad Request - The server cannot process the request due to client error.
            /// </summary>
            public const int BadRequest = 400;

            /// <summary>
            /// Unauthorized - Authentication is required and has failed or not been provided.
            /// </summary>
            public const int Unauthorized = 401;

            /// <summary>
            /// Forbidden - The server understood the request but refuses to authorize it.
            /// </summary>
            public const int Forbidden = 403;

            /// <summary>
            /// Not Found - The requested resource could not be found.
            /// </summary>
            public const int NotFound = 404;

            /// <summary>
            /// Method Not Allowed - The request method is not supported for the resource.
            /// </summary>
            public const int MethodNotAllowed = 405;

            /// <summary>
            /// Not Acceptable - The server cannot produce a response matching the acceptable values.
            /// </summary>
            public const int NotAcceptable = 406;

            /// <summary>
            /// Request Timeout - The server timed out waiting for the request.
            /// </summary>
            public const int RequestTimeout = 408;

            /// <summary>
            /// Conflict - The request conflicts with the current state of the server.
            /// </summary>
            public const int Conflict = 409;

            /// <summary>
            /// Gone - The requested resource is no longer available.
            /// </summary>
            public const int Gone = 410;

            /// <summary>
            /// Unprocessable Entity - The request was well-formed but contains semantic errors.
            /// </summary>
            public const int UnprocessableEntity = 422;

            /// <summary>
            /// Too Many Requests - The user has sent too many requests in a given amount of time.
            /// </summary>
            public const int TooManyRequests = 429;
        }

        /// <summary>
        /// Server error status codes (5xx).
        /// </summary>
        public static class ServerError
        {
            /// <summary>
            /// Internal Server Error - A generic error occurred on the server.
            /// </summary>
            public const int InternalServerError = 500;

            /// <summary>
            /// Not Implemented - The server does not support the functionality required.
            /// </summary>
            public const int NotImplemented = 501;

            /// <summary>
            /// Bad Gateway - The server received an invalid response from an upstream server.
            /// </summary>
            public const int BadGateway = 502;

            /// <summary>
            /// Service Unavailable - The server is currently unavailable (overloaded or down).
            /// </summary>
            public const int ServiceUnavailable = 503;

            /// <summary>
            /// Gateway Timeout - The server did not receive a timely response from an upstream server.
            /// </summary>
            public const int GatewayTimeout = 504;
        }

        /// <summary>
        /// String representations of common HTTP status codes for message parsing.
        /// </summary>
        public static class StatusStrings
        {
            public const string BadRequest = "400";
            public const string Unauthorized = "401";
            public const string Forbidden = "403";
            public const string NotFound = "404";
            public const string RequestTimeout = "408";
            public const string TooManyRequests = "429";
            public const string InternalServerError = "500";
            public const string BadGateway = "502";
            public const string ServiceUnavailable = "503";
            public const string GatewayTimeout = "504";
        }
    }
}