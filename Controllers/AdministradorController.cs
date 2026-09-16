using control_asistencia.Data;
using control_asistencia.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace control_asistencia.Controllers
{
    [Authorize(Roles = "ADMIN")]
    [ResponseCache(Location = ResponseCacheLocation.None, NoStore = true)]
    public class AdministradorController : Controller
    {
        private readonly ApplicationDbContext _context;

        public AdministradorController(ApplicationDbContext context)
        {
            _context = context;
        }

        // =======================================================
        // 1. CARGA LA VISTA PRINCIPAL (Index)
        // =======================================================
        public async Task<IActionResult> Index()
        {
            Response.Headers["Cache-Control"] = "no-cache, no-store, must-revalidate";
            Response.Headers["Pragma"] = "no-cache";
            Response.Headers["Expires"] = "0";

            ViewBag.MesSeleccionado = DateTime.Now.Month;
            ViewBag.FechaExacta = DateTime.Now.ToString("yyyy-MM-dd");

            // Buscar Personal que NO tiene un registro en la tabla Usuarios
            var personalSinCuenta = await _context.Personal
                .Where(p => !_context.Usuarios.Any(u => u.IdPersonal == p.Id))
                .OrderBy(p => p.Nombre)
                .ToListAsync();

            ViewBag.PersonalSinCuenta = personalSinCuenta;

            return View();
        }

        // =======================================================
        // 2. ENDPOINT: MONITOREO DINÁMICO (JSON)
        // =======================================================
        [HttpGet]
        public async Task<IActionResult> ObtenerMonitoreoDinamico(string buscarUsuario, string tipoFiltro, int? mes, string fechaExacta, string estadoFiltro)
        {
            var query = _context.Asistencia
                .Include(a => a.Usuario)
                    .ThenInclude(u => u.Personal)
                .AsQueryable();

            // Lógica de Fechas (Mes vs Exacta)
            if (tipoFiltro == "exacta" && !string.IsNullOrEmpty(fechaExacta))
            {
                if (DateTime.TryParse(fechaExacta, out DateTime dateExact))
                {
                    query = query.Where(a => a.CreateAt.Date == dateExact.Date);
                }
            }
            else
            {
                int mesActual = mes ?? DateTime.Now.Month;
                int anioActual = DateTime.Now.Year;
                query = query.Where(a => a.CreateAt.Month == mesActual && a.CreateAt.Year == anioActual);
            }

            // Filtro por Incidencia
            if (!string.IsNullOrEmpty(estadoFiltro))
            {
                if (estadoFiltro == "ATRASADO")
                    query = query.Where(a => a.EstadoEntrada == "ATRASADO");
                else if (estadoFiltro == "ANTICIPADO")
                    query = query.Where(a => a.EstadoSalida == "ANTICIPADO");
                else if (estadoFiltro == "INASISTENCIA")
                    query = query.Where(a => a.EstadoEntrada == "NO_MARCADA" || a.EstadoEntrada == "INASISTENCIA");
            }

            // Búsqueda de Texto Abierto
            if (!string.IsNullOrEmpty(buscarUsuario))
            {
                query = query.Where(a => a.Usuario.Personal.Nombre.Contains(buscarUsuario) ||
                                         a.Usuario.Personal.Apellido.Contains(buscarUsuario) ||
                                         a.Usuario.Personal.Rut.Contains(buscarUsuario));
            }

            var lista = await query.OrderByDescending(a => a.CreateAt).ToListAsync();

            var resultados = lista.Select(item => {
                TimeSpan horasTrabajadas = item.HoraSalidaReal.Subtract(item.HoraEntradaReal);
                string textoHoras = item.HoraSalidaReal.TotalSeconds > 0
                                    ? $"{(int)horasTrabajadas.TotalHours:D2}:{horasTrabajadas.Minutes:D2} hrs"
                                    : "--:--";

                return new
                {
                    colaborador = $"{item.Usuario.Personal.Nombre} {item.Usuario.Personal.Apellido}",
                    fecha = item.CreateAt.ToShortDateString(),
                    entrada = item.HoraEntradaReal.ToString(@"hh\:mm\:ss"),
                    salida = item.HoraSalidaReal.ToString(@"hh\:mm\:ss"),
                    horasTrabajadas = textoHoras,
                    estadoEntrada = item.EstadoEntrada,
                    estadoSalida = item.EstadoSalida
                };
            });

            return Json(resultados);
        }

        // =======================================================
        // 3. ENDPOINT: BÚSQUEDA DINÁMICA DE USUARIOS (JSON)
        // =======================================================
        [HttpGet]
        public async Task<IActionResult> ObtenerUsuariosDinamico(string termino)
        {
            var query = _context.Usuarios
                .Include(u => u.Personal)
                .Where(u => u.Estado == true) // Solo activos
                .AsQueryable();

            if (!string.IsNullOrEmpty(termino))
            {
                query = query.Where(u => u.Personal.Nombre.Contains(termino) ||
                                         u.Personal.Apellido.Contains(termino) ||
                                         u.Personal.Rut.Contains(termino));
            }

            var lista = await query.OrderBy(u => u.Personal.Nombre).ToListAsync();

            var resultados = lista.Select(u => new {
                id = u.Id,
                rut = u.Personal.Rut,
                nombreCompleto = $"{u.Personal.Nombre} {u.Personal.Apellido}",
                correo = u.Personal.Correo,
                celular = u.Personal.Celular
            });

            return Json(resultados);
        }

        // =======================================================
        // 4. CREAR USUARIO (POST)
        // =======================================================
        [HttpPost]
        public async Task<IActionResult> CrearUsuario(Usuarios nuevoUsuario)
        {
            // Ignoramos validaciones de navegación que puedan interferir
            ModelState.Remove("Personal");

            if (ModelState.IsValid)
            {
                nuevoUsuario.CreateAt = DateTime.Now;
                _context.Usuarios.Add(nuevoUsuario);
                await _context.SaveChangesAsync();
            }

            return RedirectToAction("Index");
        }

        // =======================================================
        // 5. EDITAR USUARIO (GET Y POST)
        // =======================================================
        [HttpGet]
        public async Task<IActionResult> EditarUsuarios(int id)
        {
            var usuario = await _context.Usuarios
                .Include(u => u.Personal)
                .FirstOrDefaultAsync(u => u.Id == id);

            if (usuario == null) return RedirectToAction("Index");

            return View(usuario);
        }

        [HttpPost]
        public async Task<IActionResult> EditarUsuarios(Usuarios modelo)
        {
            var usuarioDb = await _context.Usuarios
                .Include(u => u.Personal)
                .FirstOrDefaultAsync(u => u.Id == modelo.Id);

            if (usuarioDb != null)
            {
                // 1. Actualizamos datos de la tabla Personal
                usuarioDb.Personal.Rut = modelo.Personal.Rut;
                usuarioDb.Personal.Nombre = modelo.Personal.Nombre;
                usuarioDb.Personal.Apellido = modelo.Personal.Apellido;
                usuarioDb.Personal.Correo = modelo.Personal.Correo;
                usuarioDb.Personal.Celular = modelo.Personal.Celular;
                usuarioDb.Personal.Direccion = modelo.Personal.Direccion;
                usuarioDb.Personal.IdRol = modelo.Personal.IdRol;

                // 2. Actualizamos datos de la tabla Usuarios
                usuarioDb.Status = modelo.Status;
                usuarioDb.Estado = modelo.Estado;

                // 3. Solo actualizamos la contraseña si se escribió una nueva
                if (!string.IsNullOrEmpty(modelo.Password))
                {
                    usuarioDb.Password = modelo.Password;
                }

                await _context.SaveChangesAsync();
            }

            return RedirectToAction("Index");
        }

        // =======================================================
        // 6. ELIMINAR USUARIO (Desactivación Lógica)
        // =======================================================
        [HttpGet]
        public async Task<IActionResult> EliminarUsuario(int id)
        {
            var usuario = await _context.Usuarios.FindAsync(id);
            if (usuario != null)
            {
                // En lugar de borrar (y romper las tablas relacionadas de asistencia),
                // lo desactivamos lógicamente (Soft Delete).
                usuario.Estado = false;
                await _context.SaveChangesAsync();
            }

            return RedirectToAction("Index");
        }

        // =======================================================
        // 7. GESTIONAR ASISTENCIA INDIVIDUAL (GET)
        // =======================================================
        [HttpGet]
        public async Task<IActionResult> GestionarAsistencia(int id)
        {
            var usuario = await _context.Usuarios
                .Include(u => u.Personal)
                .FirstOrDefaultAsync(u => u.Id == id);

            if (usuario == null) return RedirectToAction("Index");

            var historialAsistencia = await _context.Asistencia
                .Where(a => a.IdUsuario == id)
                .OrderByDescending(a => a.CreateAt)
                .ToListAsync();

            ViewBag.Historial = historialAsistencia;

            return View(usuario);
        }






        // =======================================================
        // ENDPOINT: LOGS DINÁMICOS CON FILTROS (JSON)
        // =======================================================
        [HttpGet]
        public async Task<IActionResult> ObtenerLogsDinamico(string termino, string filtroRespuesta)
        {
            var query = _context.Log
                .Include(l => l.AdministradorRevisor) // Apuntamos a la navegación correcta
                    .ThenInclude(u => u.Personal)
                .Include(l => l.Solicitudes)
                .Include(l => l.LogModificaciones)
                .AsQueryable();

            // Filtro por texto (Administrador, Respuesta o ID de Solicitud)
            if (!string.IsNullOrEmpty(termino))
            {
                termino = termino.Trim();
                query = query.Where(l => l.Respuesta.Contains(termino) ||
                                         l.AdministradorRevisor.Personal.Nombre.Contains(termino) ||
                                         l.AdministradorRevisor.Personal.Apellido.Contains(termino) ||
                                         l.IdSolicitudes.ToString().Contains(termino));
            }

            // Filtro por ComboBox de respuesta
            if (!string.IsNullOrEmpty(filtroRespuesta))
            {
                query = query.Where(l => l.Respuesta.Contains(filtroRespuesta));
            }

            var lista = await query.OrderByDescending(l => l.CreateAt).ToListAsync();

            var resultados = lista.Select(l => new {
                idLog = l.Id,
                idSolicitud = l.IdSolicitudes,
                revisadoPor = l.AdministradorRevisor?.Personal != null ? $"{l.AdministradorRevisor.Personal.Nombre} {l.AdministradorRevisor.Personal.Apellido}" : "Desconocido",
                respuesta = l.Respuesta ?? "Sin respuesta",
                fecha = l.CreateAt.ToString("dd/MM/yyyy HH:mm"),
                modificaciones = l.LogModificaciones.Select(m => new {
                    tabla = m.Tabla,
                    columna = m.Columna,
                    antiguo = m.ValorAntiguo,
                    nuevo = m.ValorNuevo
                })
            });

            return Json(resultados);
        }


    }
}