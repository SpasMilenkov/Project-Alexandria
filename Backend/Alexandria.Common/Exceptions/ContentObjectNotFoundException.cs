namespace Alexandria.Common.Exceptions;

public class ContentObjectNotFoundException(Guid id)
    : Exception($"Content object for file version with Id '{id}' was not found.");