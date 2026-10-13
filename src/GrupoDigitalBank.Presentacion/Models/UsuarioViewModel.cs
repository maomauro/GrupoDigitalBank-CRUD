using System.ComponentModel.DataAnnotations;
using GrupoDigitalBank.Presentacion.Servicios;

namespace GrupoDigitalBank.Presentacion.Models;

public class UsuarioViewModel
{
    public int Id { get; set; }

    [Required(ErrorMessage = "El nombre es obligatorio.")]
    [StringLength(100, ErrorMessage = "El nombre no puede superar 100 caracteres.")]
    [Display(Name = "Nombre")]
    public string Nombre { get; set; } = string.Empty;

    [Required(ErrorMessage = "La fecha de nacimiento es obligatoria.")]
    [DataType(DataType.Date)]
    [DisplayFormat(DataFormatString = "{0:yyyy-MM-dd}", ApplyFormatInEditMode = true)]
    [Display(Name = "Fecha de nacimiento")]
    public DateTime? FechaNacimiento { get; set; }

    [Required(ErrorMessage = "El sexo es obligatorio.")]
    [RegularExpression("^[MF]$", ErrorMessage = "Seleccione un sexo valido.")]
    [Display(Name = "Sexo")]
    public string? Sexo { get; set; }

    public DateTime? FechaRegistro { get; set; }

    public int Edad { get; set; }

    public string SexoTexto => Sexo == "M" ? "Masculino" : Sexo == "F" ? "Femenino" : "";

    public UsuarioDto ADto() => new()
    {
        Id = Id,
        Nombre = Nombre.Trim(),
        FechaNacimiento = FechaNacimiento ?? default,
        Sexo = Sexo ?? string.Empty
    };

    public static UsuarioViewModel DesdeDto(UsuarioDto d) => new()
    {
        Id = d.Id,
        Nombre = d.Nombre,
        FechaNacimiento = d.FechaNacimiento,
        Sexo = d.Sexo,
        FechaRegistro = d.FechaRegistro == default ? null : d.FechaRegistro,
        Edad = d.Edad
    };
}
