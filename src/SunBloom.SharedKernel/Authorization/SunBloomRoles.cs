namespace SunBloom.SharedKernel.Authorization;

/// <summary>
/// Role and policy names, shared so modules need not depend on Identity to authorize.
/// </summary>
/// <remarks>
/// ADR-0011 declined the full ASP.NET Core Identity role stack, so roles are a plain
/// string list on the user with policies built from these names. Lives in SharedKernel
/// for the same reason as <c>ICurrentUser</c>: authorization is cross-cutting, and
/// making Catalog depend on Identity to name a role would be exactly the coupling
/// ADR-0001 exists to prevent.
/// </remarks>
public static class SunBloomRoles
{
    /// <summary>Authors and reviews catalog content. Required for every admin write endpoint.</summary>
    public const string ContentAdmin = "ContentAdmin";
}

/// <summary>Authorization policy names.</summary>
public static class SunBloomPolicies
{
    public const string ContentAdmin = "content-admin";
}
