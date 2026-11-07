namespace TrueMoon.Aether.Ecs;

internal static class SystemGraph
{
    internal static int[] Order(SystemPlan[] plans)
    {
        var byId = new Dictionary<string, int>(StringComparer.Ordinal);
        for (var i = 0; i < plans.Length; i++)
            if (!byId.TryAdd(plans[i].Id, i)) throw new ArgumentException($"Duplicate system ID: {plans[i].Id}.");
        var successors = new List<int>[plans.Length];
        for (var i = 0; i < successors.Length; i++) successors[i] = [];
        var degrees = new int[plans.Length]; var edges = new HashSet<(int, int)>();
        void Edge(int from, int to)
        {
            if (plans[from].Phase > plans[to].Phase) throw new ArgumentException("A system dependency contradicts phase order.");
            if (edges.Add((from, to))) { successors[from].Add(to); degrees[to]++; }
        }
        int Find(string id) => byId.TryGetValue(id, out var index) ? index : throw new ArgumentException($"Missing system dependency: {id}.");
        for (var i = 0; i < plans.Length; i++)
        {
            foreach (var id in plans[i].After) Edge(Find(id), i);
            foreach (var id in plans[i].Before) Edge(i, Find(id));
        }
        var ready = new PriorityQueue<int, (int Phase, int Index)>();
        for (var i = 0; i < plans.Length; i++) if (degrees[i] == 0) ready.Enqueue(i, ((int)plans[i].Phase, i));
        var order = new int[plans.Length]; var count = 0;
        while (ready.TryDequeue(out var index, out _))
        {
            order[count++] = index;
            foreach (var next in successors[index]) if (--degrees[next] == 0) ready.Enqueue(next, ((int)plans[next].Phase, next));
        }
        if (count != plans.Length) throw new ArgumentException("The system dependency graph contains a cycle.");
        return order;
    }
}
