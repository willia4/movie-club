namespace zinfandel_movie_club;

public abstract record Result<T, E>
{
    public abstract bool IsError { get;  }
    public abstract bool IsSuccess { get; }
    public bool IsOk => IsSuccess;

    public static Result<T, E> Ok(T value) => new OkResult<T, E>(Value: value);
    public static Result<T, E> Error(E error) => new ErrorResult<T, E>(Err: error);
    public abstract U Match<U>(
        Func<T, U> onSuccess,
        Func<E, U> onError);

    public abstract Result<U, E> Bind<U>(Func<T, Result<U, E>> f);
    public abstract Result<T, UE> BindError<UE>(Func<E, Result<T, UE>> f);
    
    public Result<U, E> Map<U>(Func<T, U> f) => Bind(v => new OkResult<U, E>(f(v)));

    public Result<T, UE> MapError<UE>(Func<E, UE> f) => BindError(e => new ErrorResult<T, UE>(f(e)));
    
    public abstract void ThrowIfError();
    public abstract T Unwrap();
    public T Get() => Unwrap();
    
    public T? UnwrapOrDefault() => IsSuccess ? Unwrap() : default;

    public T ValueOrThrow()
    {
        ThrowIfError();
        return Unwrap();
    }
}

public record ErrorResult<T, E>(E Err) : Result<T, E>
{
    public override bool IsError => true;

    public override bool IsSuccess => false;

    public override U Match<U>(Func<T, U> onSuccess, Func<E, U> onError) =>
        onError(Err);

    public override Result<U, E> Bind<U>(Func<T, Result<U, E>> f) => Result<U, E>.Error(Err);
    public override Result<T, UE> BindError<UE>(Func<E, Result<T, UE>> f) => f(Err);

    
    public override void ThrowIfError()
    {
        throw Err switch
        {
            Exception ex => new ExceptionResultException<T, E>(this, ex),
            _ => new ErrorResultException<T, E>(this)
        };
    }

    public override T Unwrap() => throw new ResultNotSuccessException<T, E>(this);
}

public record OkResult<T, E>(T Value) : Result<T, E>
{
    public override bool IsError => false;

    public override bool IsSuccess => true;

    public override U Match<U>(Func<T, U> onSuccess, Func<E, U> onError) =>
        onSuccess(Value);

    public override Result<U, E> Bind<U>(Func<T, Result<U, E>> f) => f(Value);
    public override Result<T, UE> BindError<UE>(Func<E, Result<T, UE>> f) => Result<T, UE>.Ok(Value);

    
    public override void ThrowIfError()
    {
    }

    public override T Unwrap() => Value;
}

public class ResultNotSuccessException<T, E> : Exception
{
    public E Error { get; init;  }

    public ResultNotSuccessException(ErrorResult<T, E> res) : base($"Tried to access value of an error result")
    {
        Error = res.Err;
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

public class ExceptionResultException<T, E> : Exception
{
    public E Error { get; init; }
    public ExceptionResultException(ErrorResult<T, E> res, Exception ex) : base(ex.Message, ex)
    {
        Error = res.Err;
    }
}
