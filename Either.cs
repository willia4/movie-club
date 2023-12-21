namespace zinfandel_movie_club;

public abstract class Either<LeftT, RightT>
{
    public abstract LeftT Left { get; }
    public abstract RightT Right { get;  }
    
    public abstract bool IsLeft { get;  }
    public abstract bool IsRight { get; }

    public abstract T Match<T>(Func<LeftT, T> ifLeft, Func<RightT, T> ifRight);
    public abstract Task<T> MatchAsync<T>(Func<LeftT, Task<T>> ifLeft, Func<RightT, Task<T>> ifRight);

    public static Either<LeftT, RightT> OfLeft(LeftT v) => new EitherLeft<LeftT, RightT>(v);
    public static Either<LeftT, RightT> OfRight(RightT v) => new EitherRight<LeftT, RightT>(v);
}

public class EitherLeft<LeftT, RightT> : Either<LeftT, RightT>
{
    public EitherLeft(LeftT v)
    {
        Left = v;
    }

    public override LeftT Left { get; }

    public override RightT Right => throw new EitherIsRightException();

    public override bool IsLeft => true;

    public override bool IsRight => false;

    public override T Match<T>(Func<LeftT, T> ifLeft, Func<RightT, T> ifRight) => ifLeft(Left);

    public override Task<T> MatchAsync<T>(Func<LeftT, Task<T>> ifLeft, Func<RightT, Task<T>> ifRight) => ifLeft(Left);
}

public class EitherRight<LeftT, RightT> : Either<LeftT, RightT>
{
    public EitherRight(RightT v)
    {
        Right = v;
    }

    public override LeftT Left => throw new EitherIsLeftException();

    public override RightT Right { get; }

    public override bool IsLeft => false;

    public override bool IsRight => true;

    public override T Match<T>(Func<LeftT, T> ifLeft, Func<RightT, T> ifRight) => ifRight(Right);

    public override Task<T> MatchAsync<T>(Func<LeftT, Task<T>> ifLeft, Func<RightT, Task<T>> ifRight) => ifRight(Right);
}

public class EitherIsRightException : Exception
{
    public EitherIsRightException() : base("This either can only be accessed from the right")
    {
        
    }
}

public class EitherIsLeftException : Exception
{
    public EitherIsLeftException() : base("This either can only be accessed from the left")
    {
        
    }
}