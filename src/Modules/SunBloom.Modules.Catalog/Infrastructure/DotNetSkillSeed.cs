using SunBloom.Modules.Catalog.Domain;

namespace SunBloom.Modules.Catalog.Infrastructure;

/// <summary>
/// Hand-authored .NET Backend Developer skill graph — the seed for sub-slice 1.3.
/// </summary>
/// <remarks>
/// Deliberately hand-written, not generated. Sub-slice 1.5 brings the generator and
/// review workflow; this exists so the scoring and gap-ranking work in 1.6–1.8 has a
/// real graph to run against, in a domain the owner can personally judge.
/// <para>
/// Note the prerequisites that cross containment branches — <c>csharp-async</c> gates
/// <c>aspnetcore-performance</c>, <c>sql-indexing</c> sits under Data but gates work
/// elsewhere. These are exactly the edges a tree cannot express and are why typed
/// relationships exist alongside the parent hierarchy (ADR-0004).
/// </para>
/// </remarks>
internal static class DotNetSkillSeed
{
    public const string RootSlug = "dotnet-backend";

    public sealed record SeedSkill(string Slug, string Name, SkillKind Kind, string? ParentSlug, string Description);

    /// <summary>An edge meaning <c>FromSlug</c> must be learned before <c>ToSlug</c>.</summary>
    public sealed record SeedEdge(string FromSlug, string ToSlug, SkillRelationshipType Type);

    public static IReadOnlyList<SeedSkill> Skills { get; } =
    [
        new(RootSlug, ".NET Backend Development", SkillKind.Area, null,
            "Building server-side applications and APIs on .NET."),

        // ---- C# ----------------------------------------------------------------
        new("csharp", "C#", SkillKind.Technology, RootSlug,
            "The language: syntax, type system, and runtime behaviour."),
        new("csharp-oop", "Object-Oriented Programming", SkillKind.Concept, "csharp",
            "Encapsulation, inheritance, polymorphism, and when composition beats all three."),
        new("csharp-generics", "Generics", SkillKind.Concept, "csharp",
            "Type parameters, constraints, variance."),
        new("csharp-linq", "LINQ", SkillKind.Concept, "csharp",
            "Query composition, deferred execution, and the cost of materializing too early."),
        new("csharp-async", "Async and Await", SkillKind.Concept, "csharp",
            "The task model, synchronization context, cancellation, and deadlock avoidance."),
        new("csharp-collections", "Collections", SkillKind.Concept, "csharp",
            "Choosing the right structure and knowing its complexity characteristics."),
        new("csharp-memory", "Memory Management", SkillKind.Concept, "csharp",
            "Garbage collection, allocation cost, spans, and diagnosing leaks."),

        // ---- ASP.NET Core ------------------------------------------------------
        new("aspnetcore", "ASP.NET Core", SkillKind.Technology, RootSlug,
            "The web framework: hosting, pipeline, and endpoint model."),
        new("aspnetcore-webapi", "Web APIs", SkillKind.Concept, "aspnetcore",
            "Routing, model binding, content negotiation, and REST design."),
        new("aspnetcore-di", "Dependency Injection", SkillKind.Concept, "aspnetcore",
            "Service lifetimes, captive dependencies, and composition roots."),
        new("aspnetcore-middleware", "Middleware", SkillKind.Concept, "aspnetcore",
            "The request pipeline and why ordering changes behaviour."),
        new("aspnetcore-authn", "Authentication", SkillKind.Concept, "aspnetcore",
            "Schemes, tokens, cookies, and validating credentials."),
        new("aspnetcore-authz", "Authorization", SkillKind.Concept, "aspnetcore",
            "Policies, requirements, and resource-based authorization."),
        new("aspnetcore-performance", "Performance", SkillKind.Practice, "aspnetcore",
            "Profiling, allocation reduction, caching, and async throughput."),

        // ---- Data --------------------------------------------------------------
        new("data", "Data", SkillKind.Area, RootSlug,
            "Storing and querying data reliably."),
        new("sql", "SQL", SkillKind.Technology, "data",
            "Joins, aggregation, window functions, and reading a query plan."),
        new("postgresql", "PostgreSQL", SkillKind.Technology, "data",
            "Its type system, extensions, and operational behaviour."),
        new("efcore", "Entity Framework Core", SkillKind.Technology, "data",
            "Change tracking, query translation, and avoiding N+1."),
        new("sql-indexing", "Indexing", SkillKind.Concept, "data",
            "Index selection, covering indexes, and why an index can be ignored."),
        new("sql-transactions", "Transactions", SkillKind.Concept, "data",
            "Isolation levels, locking, deadlocks, and concurrency control."),

        // ---- Architecture ------------------------------------------------------
        new("architecture", "Architecture", SkillKind.Area, RootSlug,
            "Structuring systems so they stay changeable."),
        new("solid", "SOLID Principles", SkillKind.Concept, "architecture",
            "Five design principles, and the judgement to know when they do not apply."),
        new("design-patterns", "Design Patterns", SkillKind.Concept, "architecture",
            "Recognising recurring structures without forcing them."),
        new("clean-architecture", "Clean Architecture", SkillKind.Concept, "architecture",
            "Dependency direction, boundaries, and keeping the domain isolated."),
        new("system-design", "System Design", SkillKind.Concept, "architecture",
            "Scaling, partitioning, consistency trade-offs, and failure modes."),

        // ---- Testing -----------------------------------------------------------
        new("testing", "Testing", SkillKind.Area, RootSlug,
            "Proving behaviour, not producing coverage."),
        new("unit-testing", "Unit Testing", SkillKind.Practice, "testing",
            "Isolating behaviour and writing tests that fail for one reason."),
        new("integration-testing", "Integration Testing", SkillKind.Practice, "testing",
            "Exercising real collaborators, including the database."),
        new("test-doubles", "Test Doubles", SkillKind.Concept, "testing",
            "Stubs, mocks, and fakes — and the cost of over-mocking."),

        // ---- Operations --------------------------------------------------------
        new("operations", "Operations", SkillKind.Area, RootSlug,
            "Running what you build."),
        new("docker", "Docker", SkillKind.Technology, "operations",
            "Images, layers, networking, and container lifecycle."),
        new("cicd", "CI/CD", SkillKind.Practice, "operations",
            "Automated build, test, and deployment pipelines."),
        new("observability", "Observability", SkillKind.Practice, "operations",
            "Structured logs, metrics, and distributed tracing."),
        new("azure", "Azure", SkillKind.Technology, "operations",
            "Hosting, managed data services, identity, and cost awareness."),
    ];

    public static IReadOnlyList<SeedEdge> Edges { get; } =
    [
        // Language foundations gate design thinking.
        new("csharp-oop", "solid", SkillRelationshipType.Prerequisite),
        new("csharp-oop", "design-patterns", SkillRelationshipType.Prerequisite),
        new("csharp-generics", "csharp-collections", SkillRelationshipType.Prerequisite),

        // Async gates anything about throughput. Crosses from C# into ASP.NET Core.
        new("csharp-async", "aspnetcore-performance", SkillRelationshipType.Prerequisite),
        new("csharp-memory", "aspnetcore-performance", SkillRelationshipType.Prerequisite),

        // Framework ordering.
        new("aspnetcore-middleware", "aspnetcore-authn", SkillRelationshipType.Prerequisite),
        new("aspnetcore-authn", "aspnetcore-authz", SkillRelationshipType.Prerequisite),
        new("aspnetcore-di", "clean-architecture", SkillRelationshipType.Prerequisite),
        new("aspnetcore-webapi", "aspnetcore-performance", SkillRelationshipType.Prerequisite),

        // Data ordering. SQL underpins everything else in the branch.
        new("sql", "sql-indexing", SkillRelationshipType.Prerequisite),
        new("sql", "sql-transactions", SkillRelationshipType.Prerequisite),
        new("sql", "efcore", SkillRelationshipType.Prerequisite),
        new("sql", "postgresql", SkillRelationshipType.Prerequisite),
        new("csharp-linq", "efcore", SkillRelationshipType.Prerequisite),
        new("sql-indexing", "aspnetcore-performance", SkillRelationshipType.Prerequisite),

        // Architecture ordering.
        new("solid", "clean-architecture", SkillRelationshipType.Prerequisite),
        new("design-patterns", "clean-architecture", SkillRelationshipType.Prerequisite),
        new("clean-architecture", "system-design", SkillRelationshipType.Prerequisite),
        new("sql-transactions", "system-design", SkillRelationshipType.Prerequisite),

        // Testing ordering.
        new("unit-testing", "integration-testing", SkillRelationshipType.Prerequisite),
        new("test-doubles", "unit-testing", SkillRelationshipType.Prerequisite),
        new("efcore", "integration-testing", SkillRelationshipType.Prerequisite),

        // Operations ordering.
        new("docker", "cicd", SkillRelationshipType.Prerequisite),
        new("docker", "azure", SkillRelationshipType.Prerequisite),
        new("unit-testing", "cicd", SkillRelationshipType.Prerequisite),

        // Non-prerequisite associations.
        new("postgresql", "sql", SkillRelationshipType.Related),
        new("observability", "aspnetcore-performance", SkillRelationshipType.Related),
        new("unit-testing", "test-doubles", SkillRelationshipType.Related),
    ];
}
