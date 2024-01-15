namespace zinfandel_movie_club;

public abstract record Result<T, E>
{
    public abstract bool IsError { get; }
    public abstract bool IsSuccess { get; }
    public bool IsOk => IsSuccess;

    public static Result<T, E> Ok(T value) => new OkResult<T, E>(Value: value);
    public static Result<T, E> Error(E error) => new ErrorResult<T, E>(Err: error);

    public abstract U Match<U>(
        Func<T, U> onSuccess,
        Func<E, U> onError);

    public abstract U Match<U>(Func<T, U> onSuccess);

    public abstract Result<U, E> Bind<U>(Func<T, Result<U, E>> f);
    public abstract Result<T, UE> BindError<UE>(Func<E, Result<T, UE>> f);

    public Result<U, E> Map<U>(Func<T, U> f) => Bind(v => new OkResult<U, E>(f(v)));

    public Result<T, UE> MapError<UE>(Func<E, UE> f) => BindError(e => new ErrorResult<T, UE>(f(e)));

    public abstract Result<T, E> ThrowIfError();
    public abstract T Unwrap();
    public T Get() => Unwrap();

    public abstract E UnwrapError();
    
    public T ValueOrThrow() => Match(x => x);

    public abstract Task<Result<U, E>> MapAsync<U>(Func<T, Task<U>> f);
    public abstract Task<Result<U, E>> BindAsync<U>(Func<T, Task<Result<U, E>>> f);

    public abstract Result<T, Exception> AsExceptionErrorType();

    public abstract Result<U, E> ChangeResultTypeIfError<U>();
    public abstract Result<T, U> ChangeErrorTypeIfSuccess<U>();
    
    public T ValueOrDefault(T def) => Match(v => v, _ => def);
    public E ErrorOrDefault(E def) => Match(_ => def, e => e);
}


public record ErrorResult<T, E>(E Err) : Result<T, E>
{
    public override bool IsError => true;

    public override bool IsSuccess => false;

    public override U Match<U>(Func<T, U> onSuccess, Func<E, U> onError) =>
        onError(Err);

    public override U Match<U>(Func<T, U> onSuccess) => throw AsException();
    
    public override Result<U, E> Bind<U>(Func<T, Result<U, E>> f) => Result<U, E>.Error(Err);
    public override Result<T, UE> BindError<UE>(Func<E, Result<T, UE>> f) => f(Err);

    private Exception AsException() => Err switch
    {
        Exception ex => new ExceptionResultException<T, E>(this, ex),
        _ => new ErrorResultException<T, E>(this)
    };
    
    public override Result<T, E> ThrowIfError() => throw AsException();

    public override T Unwrap() => throw new ResultNotSuccessException<T, E>(this);
    public override E UnwrapError() => Err;

    public override Task<Result<U, E>> MapAsync<U>(Func<T, Task<U>> f) => Task.FromResult<Result<U, E>>(Result<U, E>.Error(Err));
    public override Task<Result<U, E>> BindAsync<U>(Func<T, Task<Result<U, E>>> f) => Task.FromResult<Result<U, E>>(Result<U, E>.Error(Err));

    public override Result<T, Exception> AsExceptionErrorType() =>
        Err switch
        {
            Exception ex => Result<T, Exception>.Error(ex),
            _ => Result<T, Exception>.Error(AsException())
        };

    public override Result<U, E> ChangeResultTypeIfError<U>() => Result<U, E>.Error(this.Err);
    public override Result<T, U> ChangeErrorTypeIfSuccess<U>() => throw new ResultNotSuccessException<T, E>(this);
}

public record OkResult<T, E>(T Value) : Result<T, E>
{
    public override bool IsError => false;

    public override bool IsSuccess => true;

    public override U Match<U>(Func<T, U> onSuccess, Func<E, U> onError) =>
        onSuccess(Value);

    public override U Match<U>(Func<T, U> onSuccess) => onSuccess(Value);

    public override Result<U, E> Bind<U>(Func<T, Result<U, E>> f) => f(Value);
    public override Result<T, UE> BindError<UE>(Func<E, Result<T, UE>> f) => Result<T, UE>.Ok(Value);


    public override Result<T, E> ThrowIfError() => this;

    public override T Unwrap() => Value;
    public override E UnwrapError() => throw new ResultNotErrorException<T, E>(this);
    
    public override async Task<Result<U, E>> MapAsync<U>(Func<T, Task<U>> f)
    {
        var r = await f(Value);
        return Result<U, E>.Ok(r);
    }

    public override async Task<Result<U, E>> BindAsync<U>(Func<T, Task<Result<U, E>>> f)
    {
        return await f(Value);
    }

    public override Result<T, Exception> AsExceptionErrorType() => Result<T, Exception>.Ok(Value);

    public override Result<U, E> ChangeResultTypeIfError<U>() => throw new ResultNotErrorException<T, E>(this);
    public override Result<T, U> ChangeErrorTypeIfSuccess<U>() => Result<T, U>.Ok(Value);
}

public class ResultNotSuccessException<T, E> : Exception
{
    public E Error { get; init;  }

    public ResultNotSuccessException(ErrorResult<T, E> res) : base($"Tried to access value of an error result")
    {
        Error = res.Err;
    }
    
}

public class ResultNotErrorException<T, E> : Exception
{
    public T SuccessValue { get; init; }

    public ResultNotErrorException(OkResult<T, E> res) : base($"Tried to access error of an OK result")
    {
        SuccessValue = res.Value;
    }
}

public class ErrorResultException<T, E> : Exception
{
    public E Error { get; init;  }

    public ErrorResultException(ErrorResult<T, E> res) : base(res.Err!.ToString())
    {
        Error = res.Err;
    }
    
    public ErrorResultException(string message, ErrorResult<T, E> res) : base(message)
    {
        Error = res.Err;
    }
}

public class ExceptionResultException : Exception
{
    protected ExceptionResultException(string message, Exception inner) : base(message, inner)
    {
        
    }
}

public class ExceptionResultException<T, E> : ExceptionResultException
{
    public E Error { get; init; }
    public ExceptionResultException(ErrorResult<T, E> res, Exception ex) : base(ex.Message, ex)
    {
        Error = res.Err;
    }
}

public static class TaskResultExtensions
{
    public static async Task<Result<U, E>> MapAsync<T, E, U>(this Task<Result<T, E>> t, Func<T, Task<U>> f)
    {
        var r = await t;
        return r.IsError ? Result<U, E>.Error(r.UnwrapError()) : Result<U, E>.Ok(await f(r.Unwrap()));
    }
    
    public static async Task<Result<U, E>> MapAsync<T, E, U>(this Task<Result<T, E>> t, Func<T, U> f)
    {
        var r = await t;
        return r.IsError ? Result<U, E>.Error(r.UnwrapError()) : Result<U, E>.Ok(f(r.Unwrap()));
    }

    public static async Task<Result<U, E>> BindAsync<T, E, U>(this Task<Result<T, E>> t, Func<T, Result<U, E>> f)
    {
        var r = await t;
        if (r.IsError)
        {
            return Result<U, E>.Error(r.UnwrapError());
        }

        return f(r.Unwrap());
    }
    
    public static async Task<Result<U, E>> BindAsync<T, E, U>(this Task<Result<T, E>> t, Func<T, Task<Result<U, E>>> f)
    {
        var r = await t;
        if (r.IsError)
        {
            return Result<U, E>.Error(r.UnwrapError());
        }

        return await f(r.Unwrap());
    }
}

public static class ResultExtensions
{
    public static IEnumerable<T> SuccessValues<T, E>(this IEnumerable<Result<T, E>> results)
    {
        return results.Where(r => r.IsSuccess).Select(r => r.Unwrap());
    }

    public static IEnumerable<E> ErrorValues<T, E>(this IEnumerable<Result<T, E>> results)
    {
        return results.Where(r => r.IsError).Select(r => r.UnwrapError());
    }
}