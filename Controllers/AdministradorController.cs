using Microsoft.AspNetCore.Mvc;

namespace control_asistencia.Controllers
{
    public class AdministradorController : Controller
    {
        public IActionResult Index()

        {

            // Cabeceras para evitar que el navegador guarde la página en caché
            Response.Headers["Cache-Control"] = "no-cache, no-store, must-revalidate";
            Response.Headers["Pragma"] = "no-cache";
            Response.Headers["Expires"] = "0";

            return View();


           
        }





    }
}
