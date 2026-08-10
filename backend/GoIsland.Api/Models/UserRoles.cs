namespace GoIsland.Api.Models;

public static class UserRoles
{
    public const string Tourist = "Tourist";
    public const string Host = "Host";

    // Administrar no es una forma de participar en la plataforma: es un permiso que se suma al
    // rol. Vive en User.IsAdmin y solo se emite como claim para que las autorizaciones sigan
    // expresandose con roles, asi que un administrador tambien puede ser turista o anfitrion.
    public const string Admin = "Admin";

    public static readonly IReadOnlySet<string> All = new HashSet<string>
    {
        Tourist,
        Host
    };

    public static readonly IReadOnlySet<string> PublicRegistration = new HashSet<string>
    {
        Tourist
    };
}
