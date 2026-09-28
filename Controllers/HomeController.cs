using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;
using TSC.Models;

namespace TSC.Controllers
{
    public class HomeController : Controller
    {
        // Everyone lands here after signing in, so send each role to the only place it can use.
        public IActionResult Index()
        {
            if (User.IsInRole("Admin"))
                return RedirectToAction("Index", "Dashboard", new { area = "Admin" });

            if (User.IsInRole("Teacher"))
                return RedirectToAction("Index", "Home", new { area = "Teacher" });

            if (User.IsInRole("Student"))
                return RedirectToAction("Index", "Home", new { area = "Student" });

            return View();
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
