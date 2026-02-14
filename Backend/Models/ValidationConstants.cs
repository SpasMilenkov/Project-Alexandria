namespace Models;

public static class ValidationConstants
{
    public static class StringLengths
    {
        public const int ShortString = 50;
        public const int MediumString = 255;
        public const int LongString = 1000;
        public const int ExtraLongString = 2000;
        public const int UserId = 450; // ASP.NET Identity standard
    }

    public static class FileConstants
    {
        public const long MinFileSize = 0;
        public const long MaxFileSize = long.MaxValue;
    }

    public static class PaginationConstants
    {
        public const int MaxPageSize = 100;
    }

    public static class ErrorMessages
    {
        public const string FileSizeRange = "File size must be non-negative";
        public const string Required = "This field is required";
    }
}