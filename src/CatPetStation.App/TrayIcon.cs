using System.Diagnostics;
using System.Drawing;
using System.Windows;
using WinForms = System.Windows.Forms;
using MessageBox = System.Windows.MessageBox;
using MessageBoxButton = System.Windows.MessageBoxButton;
using MessageBoxImage = System.Windows.MessageBoxImage;
using Point = System.Drawing.Point;

namespace CatPetStation.App;

/// <summary>
/// The system tray icon and its menu — the app's only chrome. The icon is
/// drawn in code (a little orange cat face) so the repo needs no binary
/// icon assets.
/// </summary>
public sealed class TrayIcon : IDisposable
{
    private readonly WinForms.NotifyIcon _icon;
    private readonly PetHost _host;
    private Icon? _catIcon;

    public TrayIcon(PetHost host)
    {
        _host = host;
        _catIcon = DrawCatIcon();
        _icon = new WinForms.NotifyIcon
        {
            Icon = _catIcon,
            Text = "CatPetStation",
            Visible = true,
            ContextMenuStrip = new WinForms.ContextMenuStrip(),
        };
        _icon.ContextMenuStrip.Opening += (_, _) => RebuildMenu();
        RebuildMenu();
    }

    private void RebuildMenu()
    {
        var menu = _icon.ContextMenuStrip!;
        menu.Items.Clear();

        var add = new WinForms.ToolStripMenuItem("Add pet");
        foreach (var pack in _host.AvailablePacks)
        {
            var packItem = new WinForms.ToolStripMenuItem(pack.Definition.Name);
            packItem.Click += (_, _) => _host.Spawn(pack);
            add.DropDownItems.Add(packItem);
        }
        if (add.DropDownItems.Count == 0)
            add.DropDownItems.Add(new WinForms.ToolStripMenuItem("(no packs installed)") { Enabled = false });
        menu.Items.Add(add);

        menu.Items.Add(MakeItem("Import pet pack (.zip)…", _host.ImportPackInteractive));
        menu.Items.Add(MakeItem("Open packs folder", OpenPacksFolder));

        var size = new WinForms.ToolStripMenuItem("Pet size");
        foreach (var (label, scale) in new[] { ("Small", 0.5), ("Cozy", 0.75), ("Normal", 1.0), ("Chonk", 1.5) })
        {
            var item = new WinForms.ToolStripMenuItem(label) { Checked = Math.Abs(_host.Settings.Scale - scale) < 0.01 };
            var s = scale;
            item.Click += (_, _) => _host.SetScale(s);
            size.DropDownItems.Add(item);
        }
        menu.Items.Add(size);

        var nap = new WinForms.ToolStripMenuItem("Everyone nap") { Checked = _host.Settings.AllAsleep };
        nap.Click += (_, _) => _host.SetAllSleeping(!_host.Settings.AllAsleep);
        menu.Items.Add(nap);

        menu.Items.Add(MakeItem("Remove all pets", _host.RemoveAll));
        menu.Items.Add(new WinForms.ToolStripSeparator());
        menu.Items.Add(MakeItem("About CatPetStation…", ShowAbout));
        menu.Items.Add(MakeItem("Exit", () => System.Windows.Application.Current.Shutdown()));
    }

    private static WinForms.ToolStripMenuItem MakeItem(string text, Action onClick)
    {
        var item = new WinForms.ToolStripMenuItem(text);
        item.Click += (_, _) => onClick();
        return item;
    }

    private static void OpenPacksFolder() =>
        Process.Start(new ProcessStartInfo
        {
            FileName = AppSettings.PacksDirectory,
            UseShellExecute = true,
        });

    private void ShowAbout() =>
        MessageBox.Show(
            $"""
            CatPetStation {typeof(TrayIcon).Assembly.GetName().Version?.ToString(3)}
            Open-source desktop pets that are safe by design.

            • MIT licensed — free forever
            • No network access, no telemetry, no accounts
            • Pet packs are pure data: images and JSON, never code

            Active pets: {_host.ActivePetCount}
            Packs folder: {AppSettings.PacksDirectory}
            """,
            "About CatPetStation", MessageBoxButton.OK, MessageBoxImage.Information);

    /// <summary>A 32x32 orange cat face: two triangular ears over a round head.</summary>
    private static Icon DrawCatIcon()
    {
        using var bmp = new Bitmap(32, 32);
        using (var g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            var outline = Color.FromArgb(0x4A, 0x31, 0x23);
            var coat = Color.FromArgb(0xF4, 0x9E, 0x4C);
            using var coatBrush = new SolidBrush(coat);
            using var outlinePen = new Pen(outline, 2);

            g.FillPolygon(coatBrush, [new Point(3, 16), new Point(6, 1), new Point(14, 9)]);
            g.FillPolygon(coatBrush, [new Point(29, 16), new Point(26, 1), new Point(18, 9)]);
            g.DrawPolygon(outlinePen, [new Point(3, 16), new Point(6, 1), new Point(14, 9)]);
            g.DrawPolygon(outlinePen, [new Point(29, 16), new Point(26, 1), new Point(18, 9)]);
            g.FillEllipse(coatBrush, 3, 7, 26, 24);
            g.DrawEllipse(outlinePen, 3, 7, 26, 24);

            using var eye = new SolidBrush(Color.FromArgb(0x26, 0x21, 0x1E));
            g.FillEllipse(eye, 10, 16, 4, 5);
            g.FillEllipse(eye, 18, 16, 4, 5);
            using var nose = new SolidBrush(Color.FromArgb(0xF2, 0xA5, 0xAE));
            g.FillPolygon(nose, [new Point(14, 24), new Point(18, 24), new Point(16, 27)]);
        }
        return Icon.FromHandle(bmp.GetHicon());
    }

    public void Dispose()
    {
        _icon.Visible = false;
        _icon.Dispose();
        _catIcon?.Dispose();
        _catIcon = null;
    }
}
