using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using EcoVidaGlobal.Data;
using EcoVidaGlobal.Models;
using System.Globalization;
using ClosedXML.Excel;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace EcoVidaGlobal.Controllers
{
    public class ContabilidadController : Controller
    {
        private readonly ApplicationDbContext _context;

        public ContabilidadController(ApplicationDbContext context)
        {
            _context = context;
        }

        private bool EsContador()
        {
            int? rolId = HttpContext.Session.GetInt32("UsuarioRolID");
            // Permite acceso a Módulo Contable (RolID = 4) y Administrador (RolID = 1)
            return rolId.HasValue && (rolId.Value == 4 || rolId.Value == 1);
        }

        private IActionResult RedirigirSinAcceso()
        {
            return RedirectToAction("Login", "Cuenta", new { mensaje = "Acceso restringido únicamente para el Módulo Contable." });
        }

        // 1. Dashboard Financiero
        // 1. Dashboard Financiero
        public async Task<IActionResult> Dashboard()
        {
            if (!EsContador())
                return RedirigirSinAcceso();

            // Solo contamos como ingresos los pedidos cuyo pago
            // figura como pagado.
            var pedidosPagados = _context.Pedidos
                .AsNoTracking()
                .Where(p => p.EstadoPago == "Pagado");

            decimal totalIngresos =
                await pedidosPagados.SumAsync(p => (decimal?)p.Total) ?? 0m;

            // Gastos que realmente están registrados en contabilidad.
            decimal totalEgresos =
                await _context.Gastos
                    .AsNoTracking()
                    .SumAsync(g => (decimal?)g.Monto) ?? 0m;

            // Diferencia provisional entre ingresos cobrados y gastos registrados.
            decimal balanceOperativo = totalIngresos - totalEgresos;

            // Recaudación real agrupada por método de pago.
            var ingresosPorMetodo = await pedidosPagados
                .GroupBy(p => p.MetodoPago)
                .Select(grupo => new
                {
                    Metodo = grupo.Key,
                    Monto = grupo.Sum(p => p.Total)
                })
                .OrderByDescending(grupo => grupo.Monto)
                .ToListAsync();

            ViewBag.TotalIngresos = totalIngresos;
            ViewBag.EgresosEstimados = totalEgresos;
            ViewBag.UtilidadNeta = balanceOperativo;

            // No contamos facturas: Pedido no identifica por sí solo
            // si se emitió un comprobante fiscal.
            ViewBag.TotalFacturas = await pedidosPagados.CountAsync();

            ViewBag.IngresosPorMetodoPago = ingresosPorMetodo;

            return View();
        }

        // 2. Registro de Ventas e Ingresos
        public async Task<IActionResult> Ventas()
        {
            if (!EsContador()) return RedirigirSinAcceso();

            var ventas = await _context.Pedidos
                .Include(p => p.Usuario)
                .OrderByDescending(p => p.FechaPedido)
                .ToListAsync();

            return View(ventas);
        }

        // 3. Control de Egresos
        // 3. Control de Egresos y solicitudes de reposición

        public async Task<IActionResult> Egresos()
        {
            if (!EsContador())
                return RedirigirSinAcceso();

            // Solicitudes que todavía esperan una decisión contable.
            var solicitudes = await _context.SolicitudesReposicion
                .Include(s => s.Producto)
                .Where(s => s.Estado == "Pendiente")
                .OrderBy(s => s.FechaSolicitud)
                .ToListAsync();

            // Gastos que ya están registrados en la base de datos.
            var gastos = await _context.Gastos
                .AsNoTracking()
                .OrderByDescending(g => g.FechaRegistro)
                .ToListAsync();

            // La vista conserva su modelo de solicitudes.
            ViewBag.Gastos = gastos;

            return View(solicitudes);
        }

        //
        //RegistrarGasto


        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RegistrarGasto(Gasto gasto)
        {
            if (!EsContador())
                return RedirigirSinAcceso();

            // El usuario no debe decidir la fecha de registro.
            gasto.FechaRegistro = DateTime.Now;


            var montoTexto = Request.Form["Monto"].ToString();

            if (decimal.TryParse(
                montoTexto,
                NumberStyles.AllowLeadingSign |
                NumberStyles.AllowDecimalPoint |
                NumberStyles.AllowLeadingWhite |
                NumberStyles.AllowTrailingWhite,
                CultureInfo.InvariantCulture,
                out var montoCorrecto))
            {
                gasto.Monto = montoCorrecto;

                // Quitamos la validación anterior del monto,
                // que utilizó la conversión cultural incorrecta.
                ModelState.Remove(nameof(gasto.Monto));

                // Validamos el valor convertido correctamente.
                if (montoCorrecto < 0.01m || montoCorrecto > 999999999m)
                {
                    ModelState.AddModelError(
                        nameof(gasto.Monto),
                        "El monto debe estar entre 0.01 y 999999999."
                    );
                }
            }
            else
            {
                ModelState.AddModelError(
                    nameof(gasto.Monto),
                    "El monto ingresado no tiene un formato válido."
                );
            }


            if (!ModelState.IsValid)
            {
                TempData["Error"] =
                    "No se pudo registrar el gasto. Revisa el concepto, la categoría y el monto.";
                return RedirectToAction(nameof(Egresos));
            }

            _context.Gastos.Add(gasto);
            await _context.SaveChangesAsync();

            TempData["Mensaje"] =
                "El gasto se registró correctamente.";

            return RedirectToAction(nameof(Egresos));
        }




        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AprobarSolicitud(int solicitudId)
        {
            if (!EsContador())
                return RedirigirSinAcceso();

            var usuarioId = HttpContext.Session.GetInt32("UsuarioID");

            if (usuarioId == null)
                return RedirigirSinAcceso();

            var solicitud = await _context.SolicitudesReposicion
                .FirstOrDefaultAsync(s => s.SolicitudID == solicitudId);

            if (solicitud == null || solicitud.Estado != "Pendiente")
            {
                TempData["Error"] =
                    "La solicitud no existe o ya fue procesada.";

                return RedirectToAction(nameof(Egresos));
            }

            solicitud.Estado = "Aprobada";
            solicitud.UsuarioApruebaID = usuarioId.Value;
            solicitud.FechaDecision = DateTime.Now;

            await _context.SaveChangesAsync();

            TempData["Mensaje"] =
                $"Solicitud #{solicitud.SolicitudID} aprobada correctamente.";

            return RedirectToAction(nameof(Egresos));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RechazarSolicitud(int solicitudId)
        {
            if (!EsContador())
                return RedirigirSinAcceso();

            var usuarioId = HttpContext.Session.GetInt32("UsuarioID");

            if (usuarioId == null)
                return RedirigirSinAcceso();

            var solicitud = await _context.SolicitudesReposicion
                .FirstOrDefaultAsync(s => s.SolicitudID == solicitudId);

            if (solicitud == null || solicitud.Estado != "Pendiente")
            {
                TempData["Error"] =
                    "La solicitud no existe o ya fue procesada.";

                return RedirectToAction(nameof(Egresos));
            }

            solicitud.Estado = "Rechazada";
            solicitud.UsuarioApruebaID = usuarioId.Value;
            solicitud.FechaDecision = DateTime.Now;

            await _context.SaveChangesAsync();

            TempData["Mensaje"] =
                $"Solicitud #{solicitud.SolicitudID} rechazada.";

            return RedirectToAction(nameof(Egresos));
        }



        // 4. EcoBank y Conciliación
        public async Task<IActionResult> EcoBank()
        {
            if (!EsContador()) return RedirigirSinAcceso();

            decimal totalIngresos = await _context.Pedidos.SumAsync(p => (decimal?)p.Total) ?? 0m;
            ViewBag.SaldoCaja = totalIngresos;
            ViewBag.BancoBcp = Math.Round(totalIngresos * 0.60m, 2);
            ViewBag.BancoBva = Math.Round(totalIngresos * 0.40m, 2);

            return View();
        }

        // 5. Reportes Financieros
        public IActionResult Reportes()
        {
            if (!EsContador()) return RedirigirSinAcceso();
            return View();
        }



        //Exportar Libro Diario

        [HttpGet]
        public async Task<IActionResult> ExportarLibroDiario()
        {
            if (!EsContador())
                return RedirigirSinAcceso();

            // Obtener ventas pagadas.
            var pedidos = await _context.Pedidos
                .AsNoTracking()
                .Where(p => p.EstadoPago == "Pagado")
                .Select(p => new
                {
                    p.PedidoID,
                    p.FechaPedido,
                    p.FechaPago,
                    p.Total,
                    p.MetodoPago
                })
                .ToListAsync();

            // Obtener gastos registrados.
            var gastos = await _context.Gastos
                .AsNoTracking()
                .Select(g => new
                {
                    g.GastoID,
                    g.FechaRegistro,
                    g.Concepto,
                    g.Categoria,
                    g.Monto
                })
                .ToListAsync();

            // Unificar los movimientos para ordenarlos cronológicamente.
            var movimientos = new List<(
                DateTime Fecha,
                string Tipo,
                string Concepto,
                string Categoria,
                decimal Ingreso,
                decimal Egreso)>();

            foreach (var pedido in pedidos)
            {
                movimientos.Add((
                    pedido.FechaPago ?? pedido.FechaPedido,
                    "Ingreso",
                    $"Pedido #{pedido.PedidoID}",
                    pedido.MetodoPago,
                    pedido.Total,
                    0m
                ));
            }

            foreach (var gasto in gastos)
            {
                movimientos.Add((
                    gasto.FechaRegistro,
                    "Egreso",
                    gasto.Concepto,
                    gasto.Categoria,
                    0m,
                    gasto.Monto
                ));
            }

            movimientos = movimientos
                .OrderBy(m => m.Fecha)
                .ThenBy(m => m.Tipo)
                .ToList();

            using var libro = new XLWorkbook();
            var hoja = libro.Worksheets.Add("Libro Diario");

            hoja.Cell(1, 1).Value = "LIBRO DIARIO - ECOVIDAGLOBAL";
            hoja.Range(1, 1, 1, 7).Merge();
            hoja.Cell(1, 1).Style.Font.Bold = true;
            hoja.Cell(1, 1).Style.Font.FontSize = 16;

            string[] encabezados =
            {
        "Fecha",
        "Tipo",
        "Concepto",
        "Categoría / Método de pago",
        "Ingreso (S/)",
        "Egreso (S/)",
        "Saldo acumulado (S/)"
    };

            for (int i = 0; i < encabezados.Length; i++)
            {
                hoja.Cell(3, i + 1).Value = encabezados[i];
            }

            var filaEncabezado = hoja.Range(3, 1, 3, 7);
            filaEncabezado.Style.Font.Bold = true;
            filaEncabezado.Style.Fill.BackgroundColor =
                XLColor.LightGray;

            decimal saldo = 0m;
            int fila = 4;

            foreach (var movimiento in movimientos)
            {
                saldo += movimiento.Ingreso - movimiento.Egreso;

                hoja.Cell(fila, 1).Value = movimiento.Fecha;
                hoja.Cell(fila, 1).Style.DateFormat.Format = "dd/MM/yyyy";

                hoja.Cell(fila, 2).Value = movimiento.Tipo;
                hoja.Cell(fila, 3).Value = movimiento.Concepto;
                hoja.Cell(fila, 4).Value = movimiento.Categoria;
                hoja.Cell(fila, 5).Value = movimiento.Ingreso;
                hoja.Cell(fila, 6).Value = movimiento.Egreso;
                hoja.Cell(fila, 7).Value = saldo;

                fila++;
            }

            if (fila > 4)
            {
                hoja.Range(4, 5, fila - 1, 7)
                    .Style.NumberFormat.Format = "#,##0.00";

                hoja.Range(3, 1, fila - 1, 7)
                    .SetAutoFilter();
            }

            hoja.Columns().AdjustToContents();

            using var memoria = new MemoryStream();
            libro.SaveAs(memoria);

            return File(
                memoria.ToArray(),
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                $"LibroDiario_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx"
            );
        }


        //Exportar PDF

        [HttpGet]
        public async Task<IActionResult> ExportarEstadoResultadosPdf()
        {
            if (!EsContador())
                return RedirigirSinAcceso();

            QuestPDF.Settings.License = LicenseType.Community;

            var cultura = CultureInfo.GetCultureInfo("es-PE");

            // Obtener pedidos pagados.
            var pedidos = await _context.Pedidos
                .AsNoTracking()
                .Where(p => p.EstadoPago == "Pagado")
                .Select(p => new
                {
                    p.PedidoID,
                    p.FechaPago,
                    p.FechaPedido,
                    p.Total,
                    p.MetodoPago
                })
                .ToListAsync();

            // Obtener gastos registrados.
            var gastos = await _context.Gastos
                .AsNoTracking()
                .Select(g => new
                {
                    g.GastoID,
                    g.FechaRegistro,
                    g.Concepto,
                    g.Categoria,
                    g.Monto
                })
                .ToListAsync();

            decimal totalIngresos = pedidos.Sum(p => p.Total);
            decimal totalEgresos = gastos.Sum(g => g.Monto);
            decimal resultado = totalIngresos - totalEgresos;

            // Unificar movimientos para mostrar el detalle.
            var movimientos = new List<(
                DateTime Fecha,
                string Tipo,
                string Concepto,
                string Categoria,
                decimal Ingreso,
                decimal Egreso)>();

            foreach (var pedido in pedidos)
            {
                movimientos.Add((
                    pedido.FechaPago ?? pedido.FechaPedido,
                    "Ingreso",
                    $"Pedido #{pedido.PedidoID}",
                    pedido.MetodoPago,
                    pedido.Total,
                    0m
                ));
            }

            foreach (var gasto in gastos)
            {
                movimientos.Add((
                    gasto.FechaRegistro,
                    "Egreso",
                    gasto.Concepto,
                    gasto.Categoria,
                    0m,
                    gasto.Monto
                ));
            }

            movimientos = movimientos
                .OrderBy(m => m.Fecha)
                .ThenBy(m => m.Tipo)
                .ToList();

            // Formato reutilizable para las celdas de las tablas.
            static IContainer Celda(IContainer contenedor)
            {
                return contenedor
                    .BorderBottom(1)
                    .BorderColor(Colors.Grey.Lighten2)
                    .PaddingVertical(5)
                    .PaddingHorizontal(4);
            }

            var documento = Document.Create(contenedor =>
            {
                contenedor.Page(pagina =>
                {
                    pagina.Size(PageSizes.A4);
                    pagina.Margin(30);
                    pagina.DefaultTextStyle(estilo => estilo.FontSize(9));

                    pagina.Header()
                        .Column(c =>
                        {
                            c.Item()
                                .Text("ECOVIDAGLOBAL")
                                .FontSize(10)
                                .Bold()
                                .FontColor(Colors.Green.Darken2);

                            c.Item()
                                .PaddingTop(5)
                                .Text("ESTADO DE RESULTADOS SIMPLIFICADO")
                                .FontSize(18)
                                .Bold();

                            c.Item()
                                .PaddingTop(4)
                                .Text(
                                    $"Periodo: histórico completo hasta " +
                                    $"{DateTime.Now.ToString("dd/MM/yyyy", cultura)}"
                                )
                                .FontSize(9)
                                .FontColor(Colors.Grey.Darken2);

                            c.Item()
                                .PaddingTop(8)
                                .LineHorizontal(1)
                                .LineColor(Colors.Grey.Lighten1);
                        });

                    pagina.Content()
                        .PaddingVertical(15)
                        .Column(columna =>
                        {
                            columna.Spacing(12);

                            columna.Item()
                                .Text("Resumen financiero")
                                .FontSize(13)
                                .Bold();

                            columna.Item().Table(tabla =>
                            {
                                tabla.ColumnsDefinition(columnas =>
                                {
                                    columnas.RelativeColumn(3);
                                    columnas.RelativeColumn(2);
                                });

                                void FilaResumen(string concepto, string importe)
                                {
                                    tabla.Cell()
                                        .Element(Celda)
                                        .Text(concepto);

                                    tabla.Cell()
                                        .Element(Celda)
                                        .AlignRight()
                                        .Text(importe);
                                }

                                FilaResumen(
                                    "Ingresos por pedidos pagados",
                                    $"S/ {totalIngresos.ToString("N2", cultura)}");

                                FilaResumen(
                                    "Gastos registrados",
                                    $"S/ {totalEgresos.ToString("N2", cultura)}");

                                tabla.Cell()
                                    .Background(Colors.Grey.Lighten3)
                                    .Padding(6)
                                    .Text("Resultado simplificado")
                                    .Bold();

                                tabla.Cell()
                                    .Background(Colors.Grey.Lighten3)
                                    .Padding(6)
                                    .AlignRight()
                                    .Text($"S/ {resultado.ToString("N2", cultura)}")
                                    .Bold();
                            });

                            columna.Item()
                                .PaddingTop(5)
                                .Text("Detalle de movimientos")
                                .FontSize(13)
                                .Bold();

                            columna.Item().Table(tabla =>
                            {
                                tabla.ColumnsDefinition(columnas =>
                                {
                                    columnas.ConstantColumn(55);
                                    columnas.ConstantColumn(43);
                                    columnas.RelativeColumn(2);
                                    columnas.RelativeColumn(1.5f);
                                    columnas.RelativeColumn(1);
                                    columnas.RelativeColumn(1);
                                });

                                tabla.Header(encabezado =>
                                {
                                    void Titulo(string texto)
                                    {
                                        encabezado.Cell()
                                            .Background(Colors.Green.Darken2)
                                            .Padding(4)
                                            .Text(texto)
                                            .FontColor(Colors.White)
                                            .Bold()
                                            .FontSize(8);
                                    }

                                    Titulo("Fecha");
                                    Titulo("Tipo");
                                    Titulo("Concepto");
                                    Titulo("Categoría / Método");
                                    Titulo("Ingreso S/");
                                    Titulo("Egreso S/");
                                });

                                foreach (var movimiento in movimientos)
                                {
                                    tabla.Cell()
                                        .Element(Celda)
                                        .Text(movimiento.Fecha.ToString("dd/MM/yy", cultura));

                                    tabla.Cell()
                                        .Element(Celda)
                                        .Text(movimiento.Tipo);

                                    tabla.Cell()
                                        .Element(Celda)
                                        .Text(movimiento.Concepto);

                                    tabla.Cell()
                                        .Element(Celda)
                                        .Text(movimiento.Categoria);

                                    tabla.Cell()
                                        .Element(Celda)
                                        .AlignRight()
                                        .Text(movimiento.Ingreso.ToString("N2", cultura));

                                    tabla.Cell()
                                        .Element(Celda)
                                        .AlignRight()
                                        .Text(movimiento.Egreso.ToString("N2", cultura));
                                }

                                if (movimientos.Count == 0)
                                {
                                    tabla.Cell()
                                        .ColumnSpan(6)
                                        .Element(Celda)
                                        .Text("No hay movimientos registrados.");
                                }
                            });

                            columna.Item()
                                .PaddingTop(5)
                                .Text(
                                    "Nota: este reporte considera pedidos pagados y gastos " +
                                    "registrados en el sistema. El resultado es una resta " +
                                    "simplificada; no incluye otros ajustes contables, " +
                                    "impuestos ni costos no registrados."
                                )
                                .FontSize(8)
                                .FontColor(Colors.Grey.Darken2);
                        });

                    pagina.Footer()
                        .AlignCenter()
                        .Text(texto =>
                        {
                            texto.Span("Generado el ");
                            texto.Span(DateTime.Now.ToString("dd/MM/yyyy HH:mm", cultura));
                            texto.Span(" | Página ");
                            texto.CurrentPageNumber();
                            texto.Span(" de ");
                            texto.TotalPages();
                        });
                });
            });

            byte[] pdf = documento.GeneratePdf();

            return File(
                pdf,
                "application/pdf",
                $"EstadoResultados_{DateTime.Now:yyyyMMdd_HHmmss}.pdf"
            );
        }




    }
}