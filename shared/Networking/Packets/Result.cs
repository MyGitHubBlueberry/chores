namespace Shared.Networking;

public enum ServiceError
{
    None,
    NotFound,
    Forbidden,
    InvalidInput,
    DatabaseError,
    Conflict
}

public class Result
{
    public bool IsSuccess { get; set; }
    public ServiceError Error { get; set; }
    public string? ErrorMessage { get; set; }

    public Result() {}

    protected Result(bool isSuccess, ServiceError error, string? errorMessage)
    {
        IsSuccess = isSuccess;
        Error = error;
        ErrorMessage = errorMessage;
    }

    public static Result Success() 
        => new(true, ServiceError.None, null);
    public static Result Fail(ServiceError error, string message) 
        => new(false, error, message);
    public static Result NotFound(string message = "Entity not found") 
        => Fail(ServiceError.NotFound, message);
    public static Result Forbidden(string message = "Insuffitient privileges") 
        => Fail(ServiceError.Forbidden, message);
}

public class Result<T> : Result
{
    public T? Value { get; set; }

    public Result() {}

    protected Result(Result result, T? value)
        : base(result.IsSuccess, result.Error, result.ErrorMessage)
    {
        Value = value;
    }

    public static Result<T> Success(T value)
        => new(Result.Success(), value);
    private static new void Success() {}  //hides success of the base class
    public static new Result<T> Fail(ServiceError error, string message) 
        => new(Result.Fail(error, message), default);
    public static new Result<T> NotFound(string message = "Entity not found") 
        => Fail(ServiceError.NotFound, message);
    public static new Result<T> Forbidden(string message = "Insuffitient privileges") 
        => Fail(ServiceError.Forbidden, message);
    public static Result<T> FromSucessfulResult(Result result, T value)
        => new Result<T>(result, value);
    public static Result<T> FromFailedResult(Result result)
        => new Result<T>(result, default);
}
