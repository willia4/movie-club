using System.Net;
using zinfandel_movie_club.Exceptions;

namespace zinfandel_movie_club;


public static class ExceptionExtensions
{
    public static Exceptions.HttpException ToInternalServerError(this Exception ex)
    {
        return ex switch
        {
            Exceptions.HttpException { StatusCode: HttpStatusCode.InternalServerError } alreadyThere => alreadyThere,
            _ => ex.ToInternalServerError("An internal error occurred while processing the request")
        };
    }
    
    public static Exceptions.HttpException ToInternalServerError(this Exception ex, string message)
    {
        return ex switch
        {
            Exceptions.HttpException almost => new HttpException(message, HttpStatusCode.InternalServerError, almost)
            {
                InternalMessage = almost.InternalMessage ?? ""
            },
            _ => new HttpException(message, HttpStatusCode.InternalServerError, ex)
        };
    }
}