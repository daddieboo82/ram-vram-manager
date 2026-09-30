using System.Drawing.Drawing2D;

namespace RamVramManager;

public sealed class MainForm : Form
{
    private readonly MemoryPool _pool = new();
    private readonly Timer _timer = new() { Interval = 1000 };
    private readonly TrackBar _allocation = new() { Minimum = 0, Maximum = 16384, TickFrequency = 1024, LargeChange = 1024, SmallChange = 256 };
    private readonly Label _targetValue = new();
    private readonly Label _poolValue = new();
    private readonly Label _ramValue = new();
    private readonly Label _gpuValue = new();
    private readonly Label _sharedValue = new();
    private readonly Label _status = new();
    private readonly CheckBox _auto = new() { Text = "Automatic pressure management", AutoSize = true };
    private readonly NumericUpDown _reserve = new() { Minimum = 0, Maximum = 16384, Increment = 256, Value = 4096, Width = 110 };
    private long _totalRam;

    public MainForm()
    {
        Text = "RAM → Graphics Pool Manager";
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(760, 560);
        BackColor = Color.FromArgb(18, 20, 25);
        ForeColor = Color.White;
        Font = new Font("Segoe UI", 10);

        var header = new Panel { Dock = DockStyle.Top, Height = 86, Padding = new Padding(24, 16, 24, 8) };
        header.Paint += (_, e) => using (var pen = new Pen(Color.FromArgb(55, 60, 70))) e.Graphics.DrawLine(pen, 24, 85, header.Width - 24, 85);
        var title = new Label { Text = "RAM → Graphics Pool", Font = new Font("Segoe UI", 22, FontStyle.Bold), AutoSize = true, Location = new Point(24, 12) };
        var sub = new Label { Text = "Reserve system RAM for graphics-resource caching and workload staging", ForeColor = Color.Silver, AutoSize = true, Location = new Point(27, 49) };
        header.Controls.AddRange([title, sub]);

        var body = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(24), ColumnCount = 2, RowCount = 5 };
        body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 55));
        body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 45));
        body.RowStyles.Add(new RowStyle(SizeType.Absolute, 105));
        body.RowStyles.Add(new RowStyle(SizeType.Absolute, 100));
        body.RowStyles.Add(new RowStyle(SizeType.Absolute, 100));
        body.RowStyles.Add(new RowStyle(SizeType.Absolute, 100));
        body.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var allocBox = Card("POOL TARGET");
        _allocation.Dock = DockStyle.Fill;
        _allocation.Margin = new Padding(10, 4, 10, 4);
        _allocation.ValueChanged += (_, _) => _targetValue.Text = $"{_allocation.Value:N0} MB";
        _targetValue.Text = "0 MB";
        _targetValue.Font = new Font("Segoe UI", 14, FontStyle.Bold);
        _targetValue.AutoSize = true;
        allocBox.Controls.Add(_allocation);
        allocBox.Controls.Add(_targetValue);
        body.Controls.Add(allocBox, 0, 0);

        var apply = new Button { Text = "APPLY POOL", Dock = DockStyle.Fill, Margin = new Padding(12, 18, 0, 18), BackColor = Color.FromArgb(55, 120, 220), ForeColor = Color.White, FlatStyle = FlatStyle.Flat };
        apply.FlatAppearance.BorderSize = 0;
        apply.Click += (_, _) => ApplyTarget();
        body.Controls.Add(apply, 1, 0);

        body.Controls.Add(MetricCard("ALLOCATED POOL", _poolValue, "MB"), 0, 1);
        body.Controls.Add(MetricCard("SYSTEM RAM AVAILABLE", _ramValue, ""), 1, 1);
        body.Controls.Add(MetricCard("GPU", _gpuValue, ""), 0, 2);
        body.Controls.Add(MetricCard("GPU SHARED MEMORY", _sharedValue, ""), 1, 2);

        var autoCard = Card("AUTOMATION");
        _auto.Location = new Point(14, 34);
        _auto.CheckedChanged += (_, _) => _status.Text = _auto.Checked ? "Auto mode enabled" : "Manual mode";
        autoCard.Controls.Add(_auto);
        var reserveLabel = new Label { Text = "Keep available RAM at least", AutoSize = true, Location = new Point(14, 64), ForeColor = Color.Silver };
        _reserve.Location = new Point(210, 59);
        autoCard.Controls.Add(reserveLabel);
        autoCard.Controls.Add(_reserve);
        body.Controls.Add(autoCard, 0, 3);

        var note = Card("WHAT THIS DOES");
        var info = new Label { Dock = DockStyle.Fill, Padding = new Padding(14, 28, 14, 8), Text = "This app commits a RAM-backed pool that graphics applications can use as a secondary resource/cache tier. It does NOT convert system RAM into physical GPU VRAM. Dedicated VRAM remains the fastest GPU memory.", ForeColor = Color.Silver, AutoSize = false };
        note.Controls.Add(info);
        body.Controls.Add(note, 1, 3);

        _status.Text = "Ready";
        _status.ForeColor = Color.Silver;
        _status.Dock = DockStyle.Bottom;
        _status.Height = 34;
        _status.Padding = new Padding(24, 5, 24, 0);

        Controls.Add(body);
        Controls.Add(header);
        Controls.Add(_status);

        Load += (_, _) => RefreshStats();
        _timer.Tick += (_, _) => Tick();
        _timer.Start();
        FormClosed += (_, _) => { _timer.Stop(); _pool.Dispose(); };
    }

    private Control Card(string caption)
    {
        var p = new Panel { Dock = DockStyle.Fill, BackColor = Color.FromArgb(27, 30, 37), Margin = new Padding(0, 0, 12, 10), Padding = new Padding(14) };
        var l = new Label { Text = caption, Dock = DockStyle.Top, Height = 25, ForeColor = Color.FromArgb(150, 160, 175), Font = new Font("Segoe UI", 8, FontStyle.Bold) };
        p.Controls.Add(l);
        return p;
    }

    private Control MetricCard(string caption, Label value, string suffix)
    {
        var p = (Panel)Card(caption);
        value.Dock = DockStyle.Fill;
        value.TextAlign = ContentAlignment.MiddleLeft;
        value.Font = new Font("Segoe UI", 16, FontStyle.Bold);
        p.Controls.Add(value);
        return p;
    }

    private void ApplyTarget()
    {
        try
        {
            var target = (long)_allocation.Value * 1024 * 1024;
            _pool.SetTarget(target);
            _status.Text = $"Pool committed: {_pool.AllocatedBytes / 1024 / 1024:N0} MB";
            RefreshStats();
        }
        catch (Exception ex)
        {
            _status.Text = "Allocation failed: " + ex.Message;
        }
    }

    private void Tick()
    {
        RefreshStats();
        if (_auto.Checked && _totalRam > 0)
        {
            var targetMb = Math.Min((long)_allocation.Value, Math.Max(0, (_totalRam / 1024 / 1024) - (long)_reserve.Value));
            if (targetMb != _allocation.Value) _allocation.Value = (int)targetMb;
            if (_pool.AllocatedBytes / 1024 / 1024 != targetMb) ApplyTarget();
        }
    }

    private void RefreshStats()
    {
        try
        {
            var s = SystemStatsReader.Read();
            _totalRam = s.TotalRam;
            _poolValue.Text = $"{_pool.AllocatedBytes / 1024 / 1024:N0} MB";
            _ramValue.Text = s.AvailableRam > 0 ? FormatBytes(s.AvailableRam) : "Unavailable";
            _gpuValue.Text = s.GpuName;
            _sharedValue.Text = s.SharedGpuMemory > 0 ? FormatBytes(s.SharedGpuMemory) : "Unavailable";
        }
        catch { }
    }

    private static string FormatBytes(long bytes)
    {
        string[] units = ["B", "KB", "MB", "GB", "TB"];
        double v = bytes;
        var i = 0;
        while (v >= 1024 && i < units.Length - 1) { v /= 1024; i++; }
        return $"{v:0.0} {units[i]}";
    }
}
