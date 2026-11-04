using System.Collections.Generic;

public static class RuntimeSimulationUtilities {
    public static T AtCyclic<T>(this IList<T> list, int index) {
        if (list == null || list.Count == 0) return default;
        var normalizedIndex = ((index % list.Count) + list.Count) % list.Count;
        return list[normalizedIndex];
    }
}
