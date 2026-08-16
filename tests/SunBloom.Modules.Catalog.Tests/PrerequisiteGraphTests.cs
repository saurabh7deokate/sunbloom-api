using SunBloom.Modules.Catalog.Domain;

namespace SunBloom.Modules.Catalog.Tests;

/// <summary>
/// Prerequisite edges must form a DAG. A cycle means every skill in the loop waits
/// forever on another, so nothing in it is ever recommended — and the failure is silent.
/// </summary>
public class PrerequisiteGraphTests
{
    // Readable, stable identifiers. Guid ordering is irrelevant to the algorithm.
    private static readonly Guid A = new("00000000-0000-0000-0000-00000000000a");
    private static readonly Guid B = new("00000000-0000-0000-0000-00000000000b");
    private static readonly Guid C = new("00000000-0000-0000-0000-00000000000c");
    private static readonly Guid D = new("00000000-0000-0000-0000-00000000000d");
    private static readonly Guid E = new("00000000-0000-0000-0000-00000000000e");

    [Fact]
    public void Self_edge_is_a_cycle()
    {
        Assert.True(PrerequisiteGraph.WouldCreateCycle(Graph(), A, A));
    }

    [Fact]
    public void Edge_into_an_empty_graph_is_fine()
    {
        Assert.False(PrerequisiteGraph.WouldCreateCycle(Graph(), A, B));
    }

    [Fact]
    public void Reverse_of_an_existing_edge_is_a_cycle()
    {
        // A -> B already exists, so B -> A closes a two-node loop.
        Assert.True(PrerequisiteGraph.WouldCreateCycle(Graph((A, B)), B, A));
    }

    [Fact]
    public void Edge_closing_a_long_chain_is_a_cycle()
    {
        // A -> B -> C -> D, so D -> A closes it.
        Assert.True(PrerequisiteGraph.WouldCreateCycle(Graph((A, B), (B, C), (C, D)), D, A));
    }

    [Fact]
    public void Edge_partway_back_along_a_chain_is_a_cycle()
    {
        // A -> B -> C -> D, so D -> B closes a loop that excludes A.
        Assert.True(PrerequisiteGraph.WouldCreateCycle(Graph((A, B), (B, C), (C, D)), D, B));
    }

    [Fact]
    public void Shortcut_edge_along_a_chain_is_not_a_cycle()
    {
        // A -> B -> C, plus A -> C. A diamond is still acyclic; rejecting this would
        // wrongly forbid a skill having two independent prerequisites.
        Assert.False(PrerequisiteGraph.WouldCreateCycle(Graph((A, B), (B, C)), A, C));
    }

    [Fact]
    public void Converging_paths_are_not_a_cycle()
    {
        // A -> C and B -> C: two prerequisites for the same skill.
        Assert.False(PrerequisiteGraph.WouldCreateCycle(Graph((A, C)), B, C));
    }

    [Fact]
    public void Edge_between_disconnected_components_is_not_a_cycle()
    {
        Assert.False(PrerequisiteGraph.WouldCreateCycle(Graph((A, B), (C, D)), B, C));
    }

    [Fact]
    public void Deep_chain_does_not_overflow_the_stack()
    {
        // Traversal is iterative precisely so an AI-generated deep graph cannot take
        // down the process.
        var ids = Enumerable.Range(0, 20_000).Select(_ => Guid.CreateVersion7()).ToArray();
        var edges = new Dictionary<Guid, IReadOnlyList<Guid>>();

        for (var i = 0; i < ids.Length - 1; i++)
        {
            edges[ids[i]] = [ids[i + 1]];
        }

        Assert.True(PrerequisiteGraph.WouldCreateCycle(edges, ids[^1], ids[0]));
        Assert.False(PrerequisiteGraph.WouldCreateCycle(edges, ids[0], ids[^1]));
    }

    [Fact]
    public void FindAnyCycle_returns_empty_for_an_acyclic_graph()
    {
        Assert.Empty(PrerequisiteGraph.FindAnyCycle(Graph((A, B), (B, C), (A, C), (D, C))));
    }

    [Fact]
    public void FindAnyCycle_reports_the_skills_in_the_loop()
    {
        // B -> C -> D -> B, with A feeding in from outside the loop.
        var cycle = PrerequisiteGraph.FindAnyCycle(Graph((A, B), (B, C), (C, D), (D, B)));

        Assert.NotEmpty(cycle);
        Assert.Contains(B, cycle);
        Assert.Contains(C, cycle);
        Assert.Contains(D, cycle);
        Assert.DoesNotContain(A, cycle);
    }

    [Fact]
    public void FindAnyCycle_detects_a_loop_reachable_only_through_a_later_root()
    {
        // The walk must not stop after exhausting the first root's component.
        Assert.NotEmpty(PrerequisiteGraph.FindAnyCycle(Graph((A, B), (C, D), (D, E), (E, C))));
    }

    [Fact]
    public void PathExists_follows_transitive_edges()
    {
        var graph = Graph((A, B), (B, C), (C, D));

        Assert.True(PrerequisiteGraph.PathExists(graph, A, D));
        Assert.False(PrerequisiteGraph.PathExists(graph, D, A));
    }

    private static Dictionary<Guid, IReadOnlyList<Guid>> Graph(params (Guid From, Guid To)[] edges) =>
        edges
            .GroupBy(edge => edge.From)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<Guid>)[.. group.Select(edge => edge.To)]);
}
