using System.Reflection;
using System.Runtime.CompilerServices;
using System.Linq.Expressions;

internal static class NativeControlTests
{
    private const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
    public static void Run()
    {
        Assembly assembly = Assembly.Load("DDMonitor");
        Type workspaceType = assembly.GetType("DDMonitor.RoomWorkspace")!;
        Type paneType = assembly.GetType("DDMonitor.RoomPane")!;
        object session = RuntimeHelpers.GetUninitializedObject(assembly.GetType("DDMonitor.BrowserSession")!);
        int checks = 0;
        void Check(bool result, string name) { if (!result) throw new Exception(name); checks++; }
        void Call(object target, string name, params object[] args) => target.GetType().GetMethod(name)!.Invoke(target, args);
        Delegate Noop(Type type) => Expression.Lambda(type, Expression.Empty(), type.GetMethod("Invoke")!.GetParameters().Select(p => Expression.Parameter(p.ParameterType))).Compile();
        Control Pane(string id)
        {
            ParameterInfo[] parameters = paneType.GetConstructors().Single().GetParameters();
            return (Control)Activator.CreateInstance(paneType, new object?[] { id, session, Noop(parameters[2].ParameterType), Noop(parameters[3].ParameterType), Noop(parameters[4].ParameterType) })!;
        }
        using var canvas = (Control)Activator.CreateInstance(workspaceType)!;
        canvas.Size = new Size(1000, 600);
        canvas.Padding = new Padding(2);
        using var left = Pane("1"); using var right = Pane("2");
        using var lowerLeft = Pane("3"); using var lowerRight = Pane("4");
        Control[] controls = [left, right, lowerLeft, lowerRight];
        canvas.Controls.AddRange(controls);
        void Arrange(int count)
        {
            Array panes = Array.CreateInstance(paneType, count);
            for (int i = 0; i < count; i++) panes.SetValue(controls[i], i);
            Call(canvas, "Arrange", panes, 2);
        }
        void NoOverlap(int count)
        {
            for (int i = 0; i < count; i++)
                for (int j = i + 1; j < count; j++)
                    Check(!controls[i].Bounds.IntersectsWith(controls[j].Bounds), $"No overlap between {i} and {j}");
        }
        Arrange(2);
        int originalWidth = left.Width, originalRightWidth = right.Width;
        Call(left, "ResizeWindow", new Size(left.Width + 100, left.Height));
        Check(left.Width == originalWidth + 100 && right.Width == originalRightWidth - 100, "Growing left room shrinks right room exactly");
        Check(right.Right == canvas.Width - canvas.Padding.Right, "Right room remains at workspace edge");
        NoOverlap(2);
        Call(right, "ResizeWindow", new Size(right.Width + 100, right.Height));
        Check(left.Width == originalWidth && right.Width == originalRightWidth, "Growing rightmost room uses its left shared boundary");
        Call(left, "ResizeWindow", new Size(left.Width + 10000, left.Height));
        Check(right.Width >= 180 * canvas.DeviceDpi / 96, "Oversized growth preserves neighboring controls");
        NoOverlap(2);
        Arrange(4);
        int lowerWidth = lowerLeft.Width, topHeight = left.Height, bottomHeight = lowerLeft.Height;
        Call(left, "ResizeWindow", new Size(left.Width + 60, left.Height + 50));
        Check(left.Height == topHeight + 50 && right.Height == left.Height && lowerLeft.Height == bottomHeight - 50, "Corner resize links both rows");
        Check(lowerLeft.Width == lowerWidth && lowerRight.Width == lowerWidth, "Horizontal resize leaves the other row unchanged");
        NoOverlap(4);
        Rectangle[] beforeFocus = controls.Select(p => p.Bounds).ToArray();
        Call(canvas, "FocusRoom", left);
        Call(canvas, "RestoreRooms");
        Check(controls.Select(p => p.Bounds).SequenceEqual(beforeFocus), "Full-page return restores edited layout exactly");
        Call(canvas, "FocusRoom", left);
        canvas.Size = new Size(1200, 700);
        Call(canvas, "RestoreRooms");
        Check(right.Right == 1198 && lowerRight.Bottom == 698 && left.Left == 2, "Main-window resize fills the new workspace");
        NoOverlap(4);
        Rectangle source = left.Bounds, destination = lowerRight.Bounds;
        object browser = paneType.GetField("browser", Hidden)!.GetValue(left)!;
        Call(canvas, "PreviewMove", left, new Point(destination.Left + destination.Width / 2, destination.Top + destination.Height / 2));
        Check(lowerRight.BackColor == (Color)assembly.GetType("DDMonitor.Ui")!.GetField("Accent")!.GetValue(null)!, "Move preview highlights destination");
        Call(canvas, "CommitMove", left);
        Check(left.Bounds == destination && lowerRight.Bounds == source, "Moving a title swaps room positions");
        Check(ReferenceEquals(browser, paneType.GetField("browser", Hidden)!.GetValue(left)), "Moving and resizing retain the browser control");
        NoOverlap(4);
        Arrange(3);
        Check(lowerLeft.Width == canvas.ClientSize.Width - canvas.Padding.Horizontal, "Incomplete last row fills the workspace");
        NoOverlap(3);

        using var library = (Control)Activator.CreateInstance(assembly.GetType("DDMonitor.RoomLibrary")!)!;
        library.Size = new Size(340, 260);
        ((List<string>)library.GetType().GetProperty("Items")!.GetValue(library)!).AddRange(new[] { "1", "2" });
        _ = library.Handle;
        int invalidations = 0;
        library.Invalidated += (_, _) => invalidations++;
        void MouseMove(int y) => library.GetType().GetMethod("OnMouseMove", Hidden)!.Invoke(library, new object[] { new MouseEventArgs(MouseButtons.None, 0, 10, y, 0) });
        MouseMove(10);
        Check(invalidations > 0, "Hover entry repaints immediately");
        int previous = invalidations;
        MouseMove(160);
        Check(invalidations > previous && (int)library.GetType().GetField("hovered", Hidden)!.GetValue(library)! == 1, "Changing hovered row repaints");
        previous = invalidations;
        library.GetType().GetMethod("OnMouseLeave", Hidden)!.Invoke(library, new object[] { EventArgs.Empty });
        Check(invalidations > previous && (int)library.GetType().GetField("hovered", Hidden)!.GetValue(library)! == -1, "Hover leave clears state and repaints");

        Type ui = assembly.GetType("DDMonitor.Ui")!;
        using var primary = (Button)ui.GetMethod("Button")!.Invoke(null, new object[] { "test", new EventHandler((_, _) => { }), true })!;
        double Luminance(Color color)
        {
            double Channel(byte value) { double c = value / 255d; return c <= .04045 ? c / 12.92 : Math.Pow((c + .055) / 1.055, 2.4); }
            return .2126 * Channel(color.R) + .7152 * Channel(color.G) + .0722 * Channel(color.B);
        }
        double Contrast(Color a, Color b) => (Math.Max(Luminance(a), Luminance(b)) + .05) / (Math.Min(Luminance(a), Luminance(b)) + .05);
        double normal = Contrast(primary.ForeColor, primary.BackColor), hover = Contrast(primary.ForeColor, primary.FlatAppearance.MouseOverBackColor);
        Check(normal >= 4.5 && hover >= 4.5, "Primary text remains readable on both backgrounds");
        Call(left, "SetListening", true);
        var sound = (Button)paneType.GetField("sound", Hidden)!.GetValue(left)!;
        Check(Contrast(sound.ForeColor, sound.BackColor) >= 4.5 && Contrast(sound.ForeColor, sound.FlatAppearance.MouseOverBackColor) >= 4.5, "Active audio button uses readable text");
        Call(left, "SetListening", false);
        Check(sound.ForeColor == (Color)ui.GetField("Text")!.GetValue(null)!, "Muted audio button restores regular text");
        Console.WriteLine($"PASS: {checks} native-control regression checks; primary contrast {normal:F2}:1, hover {hover:F2}:1");
    }
}
