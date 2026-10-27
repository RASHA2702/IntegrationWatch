namespace IntegrationWatch.Core.Exceptions;

public class IntegrationWatchException : Exception
{
    public IntegrationWatchException(string message) : base(message) { }
    public IntegrationWatchException(string message, Exception inner) : base(message, inner) { }
}

public class EndpointValidationException : IntegrationWatchException
{
    public string FieldName { get; }
    public EndpointValidationException(string fieldName, string message) : base(message)
    {
        FieldName = fieldName;
    }
}

public class ResourceNotFoundException : IntegrationWatchException
{
    public ResourceNotFoundException(string resourceName, object key)
        : base($"{resourceName} with key '{key}' was not found.") { }
}
