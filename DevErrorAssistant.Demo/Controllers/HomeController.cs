
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
    /// Deliberately throws an unhandled exception to test DevErrorAssistant.
    /// </summary>
    public IActionResult ThrowError()
    {
        _logger.LogInformation(
            "Deliberately throwing an InvalidOperationException for testing.");

        throw new InvalidOperationException(
            "This is a deliberate test exception thrown from HomeController.ThrowError() to verify the DevErrorAssistant error page.");
    }

    /// <summary>
    /// Simulates a record lookup that returns no matching record.
    /// </summary>
    public IActionResult ThrowRecordNotFound()
    {
        _logger.LogInformation(
            "Testing a record lookup with no matching result.");

        var customers = new List<DemoCustomer>();

        // Single() throws InvalidOperationException when no match exists.
        var customer = customers.Single(c => c.Id == 42);

        return Ok(customer);
    }

    /// <summary>
    /// Simulates a required value being null during validation.
    /// </summary>
    public IActionResult ThrowNullRequiredValue()
    {
        _logger.LogInformation(
            "Testing a missing required value.");

        string? customerName = null;

        ArgumentNullException.ThrowIfNull(customerName);

        return Ok(customerName);
    }

    /// <summary>
    /// Simulates a database write failure.
    /// This does not execute a real database operation.
    /// </summary>
    public IActionResult ThrowDatabaseWriteFailure()
    {
        _logger.LogInformation(
            "Testing a simulated database write failure.");

        throw new InvalidOperationException(
            "Simulated database write failure: a required column 'CustomerName' cannot accept NULL.");
    }

    /// <summary>
    /// Simulates invalid input while parsing a record ID.
    /// </summary>
    public IActionResult ThrowInvalidRecordId()
    {
        _logger.LogInformation(
            "Testing an invalid record ID.");

        var recordId = "customer-abc";

        var id = int.Parse(recordId);

        return Ok(id);
    }

    /// <summary>
    /// Simulates accessing a property on a null object.
    /// </summary>
    public IActionResult ThrowNullReference()
    {
        _logger.LogInformation(
            "Testing a null object access.");

        DemoCustomer? customer = null;

        return Content(customer!.Name);
    }

    [ResponseCache(
        Duration = 0,
        Location = ResponseCacheLocation.None,
        NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel
        {
            RequestId = Activity.Current?.Id
                ?? HttpContext.TraceIdentifier
        });
    }

    /// <summary>
    /// Simple demo model used by the exception test actions.
    /// </summary>
    public sealed class DemoCustomer
    {
        public int Id { get; set; }

        public string Name { get; set; } = string.Empty;
    }
}
