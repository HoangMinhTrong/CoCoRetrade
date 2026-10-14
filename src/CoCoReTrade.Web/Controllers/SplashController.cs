using Microsoft.AspNetCore.Mvc;

namespace CoCoReTrade.Web.Controllers;

public sealed class SplashController : Controller
{
    [HttpGet("/Splash")]
    public IActionResult Index()
    {
        ViewBag.Next = User.Identity?.IsAuthenticated == true
            ? Url.Action("Index", "Home") : Url.Action("Login", "Account");
        return View();
    }
}
