using System.Net;

namespace zinfandel_movie_club.Exceptions;

public class DebugDisplayProperty : Attribute
{
}

public class HttpException : Exception
{
    [DebugDisplayProperty]
    public HttpStatusCode StatusCode { get; init; }
    [DebugDisplayProperty]
    public string InternalMessage { get; set; } = "";
    
    public HttpException(string message, HttpStatusCode statusCode) : base(message)
    {
        StatusCode = statusCode;
    }

    public HttpException(string message, HttpStatusCode statusCode, Exception inner) : base(message, inner)
    {
        StatusCode = statusCode;
    }

    public string Title
    {
        get
        {
            var s = StatusCode.ToString("G");
            return string.Concat(s.Select(c => char.IsUpper(c) ? $" {c}" : $"{c}")).TrimStart();
        }
    }
}

public class UnauthorizedException : HttpException
{
    public UnauthorizedException() : base("Unauthorized", HttpStatusCode.Unauthorized) {}
    public UnauthorizedException(string message) : base(message, HttpStatusCode.Unauthorized) {}
    public UnauthorizedException(Exception inner) : base("Unauthorized", HttpStatusCode.Unauthorized, inner) {}
    public UnauthorizedException(string message, Exception inner) : base(message, HttpStatusCode.Unauthorized, inner) {}
}

public class NotFoundException : HttpException
{
    public NotFoundException() : base("Not Found", HttpStatusCode.NotFound) {}
    public NotFoundException(string message) : base(message, HttpStatusCode.NotFound) {}
    public NotFoundException(Exception inner) : base("Not Found", HttpStatusCode.NotFound, inner) {}
    public NotFoundException(string message, Exception inner) : base(message, HttpStatusCode.NotFound, inner) {}
}

public class BadRequestException : HttpException
{
    public BadRequestException() : base("Bad Request", HttpStatusCode.BadRequest) {}
    public BadRequestException(string message) : base(message, HttpStatusCode.BadRequest) {}
    public BadRequestException(Exception inner) : base("Bad Request", HttpStatusCode.BadRequest, inner) {}
    public BadRequestException(string message, Exception inner) : base(message, HttpStatusCode.BadRequest, inner) {}
}

public class BadRequestParameterException : HttpException
{
    [DebugDisplayProperty]
    public string Parameter { get; init; } = "";
    public BadRequestParameterException() : base("Bad Request Parameter", HttpStatusCode.BadRequest) {}

    public BadRequestParameterException(string parameter) : base("Bad Request Parameter", HttpStatusCode.BadRequest)
    {
        Parameter = parameter;
    }
    public BadRequestParameterException(Exception inner) : base("Bad Request Parameter", HttpStatusCode.BadRequest, inner) {}

    public BadRequestParameterException(string parameter, Exception inner) : base("Bad Request Parameter", HttpStatusCode.BadRequest, inner)
    {
        Parameter = parameter;
    }

    public BadRequestParameterException(string parameter, string message) : base(message, HttpStatusCode.BadRequest)
    {
        Parameter = parameter;
    }

    public BadRequestParameterException(string parameter, string message, Exception inner) : base(message, HttpStatusCode.BadRequest, inner)
    {
        Parameter = parameter;
    }
}