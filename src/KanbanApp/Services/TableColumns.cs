namespace KanbanApp.Services;

// How a table's width is shared between its columns, and what dragging the line between two of them does. The
// widths are weights (only their proportions matter); the editor turns them into star widths.
public static class TableColumns
{
    // Moves the line before column `boundary` (1 = between the first and second) by deltaPx. The two columns beside it
    // trade width, so the table stays as wide as it was; neither goes narrower than minPx.
    // lastIsFree: the last weight is the empty space after the table's last column, which may shrink to nothing.
    public static double[] Resize(IReadOnlyList<double> weights, int boundary, double deltaPx, double pxPerUnit, double minPx, bool lastIsFree = false)
    {
        var result = weights.ToArray();
        if (boundary < 1 || boundary >= result.Length || pxPerUnit <= 0) return result;

        var minUnits = minPx / pxPerUnit;
        var minRight = lastIsFree && boundary == result.Length - 1 ? 0 : minUnits;
        var pair = result[boundary - 1] + result[boundary];
        if (pair <= minUnits + minRight) return result; // no room to move either

        var left = Math.Clamp(result[boundary - 1] + deltaPx / pxPerUnit, minUnits, pair - minRight);
        result[boundary - 1] = left;
        result[boundary] = pair - left;
        return result;
    }

    // Weights scaled so the average column is 1, which a column added later (weight 1) then matches.
    public static double[] Normalise(IReadOnlyList<double> weights)
    {
        var sum = weights.Sum();
        return sum <= 0 ? weights.Select(_ => 1.0).ToArray() : weights.Select(w => w * weights.Count / sum).ToArray();
    }
}
