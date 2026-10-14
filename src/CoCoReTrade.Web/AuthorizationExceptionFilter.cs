using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace CoCoReTrade.Web;

public sealed class AuthorizationExceptionFilter : IExceptionFilter
{
    public void OnException(ExceptionContext context)
    {
        if (context.Exception is not UnauthorizedAccessException) return;
        context.Result = context.HttpContext.User.Identity?.IsAuthenticated == true
            ? new ForbidResult() : new UnauthorizedResult();
        context.ExceptionHandled = true;
    }
}
