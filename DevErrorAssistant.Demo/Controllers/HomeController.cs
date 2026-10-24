using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using DevErrorAssistant.Demo.Models;

namespace DevErrorAssistant.Demo.Controllers;

public class HomeController : Controller
{
    private readonly ILogger<HomeController> _logger;

    public HomeController(ILogger<HomeController> logger)
    {
        _logger = logger;
    }

    public IActionResult Index()
    {
        return View();
    }

    public IActionResult Privacy()
    {
        return View();
    }

    /// <summary>
    /// Deliberately throws an unhandled exception to test and demonstrate DevErrorAssistant.
    /// </summary>
    public IActionResult ThrowError()
    {
        _logger.LogInformation("Deliberately throwing an unhandled InvalidOperationException to test DevErrorAssistant.");
        throw new InvalidOperationException("This is a deliberate test exception thrown from HomeController.ThrowError() to verify the DevErrorAssistant error page.");
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
