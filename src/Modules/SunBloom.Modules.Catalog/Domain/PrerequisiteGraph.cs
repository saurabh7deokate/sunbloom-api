namespace SunBloom.Modules.Catalog.Domain;

/// <summary>
/// Acyclicity checks over prerequisite edges.
/// </summary>
/// <remarks>
/// An edge <c>from → to</c> means <em>from must be learned before to</em>.
/// <para>
/// These edges must form a DAG. A cycle makes sequencing impossible: the recommendation
/// engine walks prerequisites to decide what is unblocked, and a cycle means nothing in
/// it is ever reachable — every skill in the loop waits forever on another. PostgreSQL
/// cannot express this declaratively, so it is enforced on write.
/// </para>
/// <para>
/// Pure and side-effect free by design: this is the one piece of Catalog logic that must
/// be exhaustively testable, and coupling it to a database would make that awkward.
/// Traversal is iterative rather than recursive — an AI-generated graph can be deep, and
/// a stack overflow here would take down the process.
/// </para>
/// </remarks>
internal static class PrerequisiteGraph
{
    /// <summary>
    /// Would adding <paramref name="from" /> → <paramref name="to" /> create a cycle?
    /// </summary>
    /// <param name="edges">Existing prerequisite edges, keyed by source skill.</param>
    public static bool WouldCreateCycle(
        IReadOnlyDictionary<Guid, IReadOnlyList<Guid>> edges,
        Guid from,
        Guid to)
    {
        ArgumentNullException.ThrowIfNull(edges);

        // A self-edge is the degenerate cycle.
        if (from == to)
        {
            return true;
        }

        // Adding from → to closes a loop only if `to` can already reach `from`.
        return PathExists(edges, start: to, target: from);
    }

    /// <summary>Is <paramref name="target" /> reachable from <paramref name="start" />?</summary>
    public static bool PathExists(
        IReadOnlyDictionary<Guid, IReadOnlyList<Guid>> edges,
        Guid start,
        Guid target)
    {
        ArgumentNullException.ThrowIfNull(edges);

        var visited = new HashSet<Guid>();
        var pending = new Stack<Guid>();
        pending.Push(start);

        while (pending.Count > 0)
        {
            var current = pending.Pop();

            if (current == target)
            {
                return true;
            }

            if (!visited.Add(current) || !edges.TryGetValue(current, out var next))
            {
                continue;
            }

            foreach (var neighbour in next)
            {
                if (!visited.Contains(neighbour))
                {
                    pending.Push(neighbour);
                }
            }
        }

        return false;
    }

    /// <summary>
    /// Finds any existing cycle, returning the skills involved. Empty when the graph is
    /// acyclic. Used to validate seed data and imported content in bulk, where checking
    /// one edge at a time would not catch a set that is collectively cyclic.
    /// </summary>
    public static IReadOnlyList<Guid> FindAnyCycle(IReadOnlyDictionary<Guid, IReadOnlyList<Guid>> edges)
    {
        ArgumentNullException.ThrowIfNull(edges);

        var state = new Dictionary<Guid, VisitState>();
        var path = new List<Guid>();

        foreach (var node in edges.Keys)
        {
            var cycle = Walk(edges, node, state, path);

            if (cycle.Count > 0)
            {
                return cycle;
            }
        }

        return [];
    }

    /// <summary>
    /// Iterative depth-first walk colouring nodes white/grey/black. Reaching a grey node
    /// means it is still on the current path, which is precisely a back edge.
    /// </summary>
    private static List<Guid> Walk(
        IReadOnlyDictionary<Guid, IReadOnlyList<Guid>> edges,
        Guid start,
        Dictionary<Guid, VisitState> state,
        List<Guid> path)
    {
        if (state.GetValueOrDefault(start) == VisitState.Done)
        {
            return [];
        }

        var pending = new Stack<(Guid Node, bool Exiting)>();
        pending.Push((start, false));

        while (pending.Count > 0)
        {
            var (node, exiting) = pending.Pop();

            if (exiting)
            {
                state[node] = VisitState.Done;
                path.RemoveAt(path.Count - 1);
                continue;
            }

            var current = state.GetValueOrDefault(node);

            if (current == VisitState.Done)
            {
                continue;
            }

            if (current == VisitState.InProgress)
            {
                // Back edge: everything from this node onward in `path` is the cycle.
                var index = path.IndexOf(node);
                return [.. path[index..], node];
            }

            state[node] = VisitState.InProgress;
            path.Add(node);
            pending.Push((node, true));

            if (edges.TryGetValue(node, out var next))
            {
                foreach (var neighbour in next)
                {
                    pending.Push((neighbour, false));
                }
            }
        }

        return [];
    }

    private enum VisitState
    {
        Unvisited = 0,
        InProgress,
        Done,
    }
}
