namespace DDMonitor;

// Tiled rooms share boundaries while retaining their existing browser controls.
internal sealed class RoomWorkspace : Panel
{
    private IReadOnlyList<RoomPane> rooms = Array.Empty<RoomPane>();
    private List<RoomPane> order = [];
    private int columns = 1;
    private double[] rowWeights = [];
    private double[][] columnWeights = [];
    private RoomPane? focused;
    private RoomPane? moveTarget;
    public bool FreeLayout { get; private set; }
    public bool CanAdjust => focused == null;
    public event EventHandler? LayoutEdited;
    public RoomWorkspace() { DoubleBuffered = true; }
    private int Gap => Math.Max(2, 2 * DeviceDpi / 96);
    private Rectangle Area => new(Padding.Left, Padding.Top,
        Math.Max(1, ClientSize.Width - Padding.Horizontal), Math.Max(1, ClientSize.Height - Padding.Vertical));

    public void Arrange(IReadOnlyList<RoomPane> panes, int columnCount)
    {
        CancelMove();
        rooms = panes;
        order = panes.ToList();
        columns = Math.Max(1, columnCount);
        rowWeights = [];
        columnWeights = [];
        FreeLayout = false;
        ArrangeRooms();
    }

    public void ArrangeRooms()
    {
        if (focused != null || ClientSize.Width <= 0 || ClientSize.Height <= 0) return;
        if (order.Count != rooms.Count || order.Any(room => !rooms.Contains(room))) order = rooms.ToList();
        if (order.Count == 0) return;
        int rows = (int)Math.Ceiling(order.Count / (double)columns);
        if (rowWeights.Length != rows || columnWeights.Length != rows ||
            Enumerable.Range(0, rows).Any(row => columnWeights[row].Length != Math.Min(columns, order.Count - row * columns)))
        {
            rowWeights = Enumerable.Repeat(1d, rows).ToArray();
            columnWeights = Enumerable.Range(0, rows).Select(row => Enumerable.Repeat(1d, Math.Min(columns, order.Count - row * columns)).ToArray()).ToArray();
        }
        Rectangle area = Area;
        int[] heights = AllocateTracks(area.Height, rowWeights, Gap);
        int top = area.Top;
        SuspendLayout();
        for (int row = 0; row < rows; row++)
        {
            int[] widths = AllocateTracks(area.Width, columnWeights[row], Gap);
            int left = area.Left;
            for (int column = 0; column < widths.Length; column++)
            {
                order[row * columns + column].Bounds = new Rectangle(left, top, widths[column], heights[row]);
                left += widths[column] + Gap;
            }
            top += heights[row] + Gap;
        }
        ResumeLayout();
    }

    internal static int[] AllocateTracks(int extent, double[] weights, int gap)
    {
        int remaining = Math.Max(0, extent - gap * (weights.Length - 1) - weights.Length);
        double total = weights.Sum(), cumulative = 0;
        int previous = 0;
        var sizes = new int[weights.Length];
        for (int i = 0; i < weights.Length; i++)
        {
            cumulative += weights[i];
            int end = i == weights.Length - 1 ? remaining : (int)Math.Round(remaining * cumulative / total);
            sizes[i] = 1 + end - previous;
            previous = end;
        }
        return sizes;
    }

    internal static int[] ResizePair(int[] sizes, int boundary, int delta, int minimum)
    {
        int[] result = (int[])sizes.Clone();
        if (boundary < 0 || boundary >= sizes.Length - 1) return result;
        delta = Math.Clamp(delta, Math.Min(minimum, sizes[boundary]) - sizes[boundary], sizes[boundary + 1] - Math.Min(minimum, sizes[boundary + 1]));
        result[boundary] += delta;
        result[boundary + 1] -= delta;
        return result;
    }

    public void Adjust(RoomPane pane, Rectangle requested)
    {
        if (!CanAdjust) return;
        int index = order.IndexOf(pane);
        if (index < 0) return;
        Rectangle original = pane.Bounds;
        if (requested.Size == original.Size)
        {
            PreviewMove(pane, new Point(requested.Left + requested.Width / 2, requested.Top + requested.Height / 2));
            CommitMove(pane);
            return;
        }
        int row = index / columns, column = index % columns;
        int[] widths = order.Skip(row * columns).Take(columnWeights[row].Length).Select(room => room.Width).ToArray();
        int[] heights = Enumerable.Range(0, rowWeights.Length).Select(r => order[r * columns].Height).ToArray();
        int horizontalBoundary = requested.Left != original.Left ? column - 1 : column;
        int horizontalDelta = requested.Left != original.Left ? requested.Left - original.Left : requested.Right - original.Right;
        int verticalBoundary = requested.Top != original.Top ? row - 1 : row;
        int verticalDelta = requested.Top != original.Top ? requested.Top - original.Top : requested.Bottom - original.Bottom;
        int[] nextWidths = ResizePair(widths, horizontalBoundary, horizontalDelta, 180 * DeviceDpi / 96);
        int[] nextHeights = ResizePair(heights, verticalBoundary, verticalDelta, 140 * DeviceDpi / 96);
        if (widths.SequenceEqual(nextWidths) && heights.SequenceEqual(nextHeights)) return;
        // Subtract the reserved pixel used by AllocateTracks so the same extent is reproduced exactly.
        columnWeights[row] = nextWidths.Select(size => (double)Math.Max(1, size - 1)).ToArray();
        rowWeights = nextHeights.Select(size => (double)Math.Max(1, size - 1)).ToArray();
        ArrangeRooms();
        MarkEdited();
    }

    public void ResizeRoom(RoomPane pane, Size size)
    {
        int index = order.IndexOf(pane);
        if (index < 0 || !CanAdjust) return;
        int row = index / columns, column = index % columns;
        Rectangle requested = new(pane.Location, size);
        if (column == columnWeights[row].Length - 1) requested.X = pane.Right - size.Width;
        if (row == rowWeights.Length - 1) requested.Y = pane.Bottom - size.Height;
        Adjust(pane, requested);
    }

    public void PreviewMove(RoomPane pane, Point center)
    {
        RoomPane? target = order.FirstOrDefault(room => room != pane && room.Bounds.Contains(center));
        if (target == moveTarget) return;
        CancelMove();
        moveTarget = target;
        if (moveTarget != null) moveTarget.BackColor = Ui.Accent;
    }

    public void CommitMove(RoomPane pane)
    {
        int source = order.IndexOf(pane), target = moveTarget == null ? -1 : order.IndexOf(moveTarget);
        CancelMove();
        if (!CanAdjust || source < 0 || target < 0) return;
        (order[source], order[target]) = (order[target], order[source]);
        ArrangeRooms();
        MarkEdited();
    }

    public void CancelMove()
    {
        if (moveTarget != null && !moveTarget.IsDisposed) moveTarget.BackColor = Ui.Surface;
        moveTarget = null;
    }

    private void MarkEdited()
    {
        bool firstEdit = !FreeLayout;
        FreeLayout = true;
        if (firstEdit) LayoutEdited?.Invoke(this, EventArgs.Empty);
    }

    public void FocusRoom(RoomPane pane)
    {
        CancelMove();
        focused = pane;
        foreach (RoomPane room in rooms) room.Visible = room == pane;
        pane.Bounds = Area;
        pane.BringToFront();
    }

    public void RestoreRooms()
    {
        if (focused == null) return;
        focused = null;
        foreach (RoomPane room in rooms) room.Visible = true;
        ArrangeRooms();
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        if (focused != null) focused.Bounds = Area;
        else ArrangeRooms();
    }
}
