using System.Diagnostics;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace zinfandel_movie_club.Pages;

[ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
[IgnoreAntiforgeryToken]
public class ErrorModel : PageModel
{
    public const string DefaultErrorMessage = "An internal error has occurred.";

    public string ErrorTitle { get; set; } = "";
    public string ErrorText { get; set; } = "";
    public Dictionary<string, string> ErrorProperties = new Dictionary<string, string>();

    public string? ErrorTypeName { get; set; } = null;
    public string? StackTrace { get; set; } = null;
    
    public string? RequestId { get; set; }

    public bool ShowRequestId => !string.IsNullOrEmpty(RequestId);

    private readonly ILogger<ErrorModel> _logger;
    private readonly IWebHostEnvironment _environment;
    
    public ErrorModel(ILogger<ErrorModel> logger, IWebHostEnvironment environment)
    {
        _logger = logger;
        _environment = environment;
    }

    private IActionResult Process()
    {
        static string msgOrDefault(string m) => string.IsNullOrWhiteSpace(m) ? DefaultErrorMessage : m.Trim();
        
        RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier;
        var exceptionFeature = HttpContext.Features.Get<IExceptionHandlerPathFeature>();
        var exception = exceptionFeature?.Error;

        if (exception is ExceptionResultException { InnerException: {} inner })
        {
            exception = inner;
        }
            
        (var statusCode, ErrorText, ErrorTitle) = exception switch
        {
            Exceptions.HttpException { Message: var msg, StatusCode: var exStatusCode, Title: var exTitle } => 
                ((int)exStatusCode, msgOrDefault(msg), $"{(int)exStatusCode} - {exTitle}"),
            _ => (500, DefaultErrorMessage, $"500 - Internal Server Error")
        };

        var errorProperties = new Dictionary<string, string>();
        if (exception is not null)
        {
            errorProperties =
                    exception.GetType().GetProperties().Where(p =>
                    {
                        var attrs = p.GetCustomAttributes(false);
                        return (attrs.Any(a => a is Exceptions.DebugDisplayProperty));
                    })
                    .ToDictionary((p => p.Name), (p => p.GetValue(exception) switch
                    {
                        null => "[null]",
                        object v => v.ToString() ?? "[null string]"
                    }));

            if (_environment.IsDevelopment())
            {
                ErrorProperties = errorProperties;
                ErrorTypeName = exception.GetType().Name;
                
                if (exception.StackTrace is string stackTrace)
                {
                    StackTrace = stackTrace;
                }
            }
        }

        var propertiesString = string.Join(" ", errorProperties.Select(kvp => $"{kvp.Key}=\"{kvp.Value}\""));
        
        _logger.LogError(exception, "Request Id: {requestId}, Message: {message}, Path: {path}, Properties: {properties}", RequestId, exception?.Message, exceptionFeature?.Path, propertiesString);
        
        if (string.Equals(Request.Method, "GET", StringComparison.OrdinalIgnoreCase))
        {
            return new PageResult
            {
                StatusCode = statusCode
            };
        }

        return new ContentResult()
        {
            ContentType = "text/plain",
            StatusCode = statusCode,
            Content = ErrorText
        };
    }

    public IActionResult OnGet() => Process();
    public IActionResult OnPost() => Process();
    public IActionResult OnPut() => Process();
}

