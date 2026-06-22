using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Media.Media3D;
using CarForge.Core.Rendering;

namespace CarForge.App.Views;

/// <summary>
/// Viewport 3D com controle de texturas em árvore: grupo por .ytd (liga/desliga
/// o pacote) e textura individual dentro. Depende só de IVehicleModelLoader.
/// </summary>
public partial class Viewer3DControl : UserControl
{
    public static IVehicleModelLoader Loader { get; set; } = new NullVehicleModelLoader();

    private static readonly Material Fallback =
        new DiffuseMaterial(new SolidColorBrush(Color.FromRgb(0x6E, 0x8A, 0xB8)));

    private sealed class PartVisual
    {
        public required GeometryModel3D Model { get; init; }
        public Material? Textured { get; init; }
        public string? TexName { get; init; }
    }

    private readonly List<PartVisual> _visuals = new();
    private readonly ObservableCollection<TextureGroup> _groups = new();
    private ModelVisual3D? _container;

    public Viewer3DControl()
    {
        InitializeComponent();
        TexList.ItemsSource = _groups;
    }

    public static readonly DependencyProperty YftPathProperty = DependencyProperty.Register(
        nameof(YftPath), typeof(string), typeof(Viewer3DControl),
        new PropertyMetadata(null, OnYftPathChanged));

    public string? YftPath
    {
        get => (string?)GetValue(YftPathProperty);
        set => SetValue(YftPathProperty, value);
    }

    private static void OnYftPathChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) =>
        ((Viewer3DControl)d).Reload();

    private void Reload()
    {
        if (_container is not null) { Viewport.Children.Remove(_container); _container = null; }
        _visuals.Clear();
        foreach (var g in _groups)
            foreach (var t in g.Items) t.PropertyChanged -= OnToggleChanged;
        _groups.Clear();

        if (string.IsNullOrWhiteSpace(YftPath)) return;

        ModelGeometry? geo;
        try { geo = Loader.Load(YftPath); }
        catch { return; }
        if (geo is null || geo.Parts.Count == 0) return;

        var group = new Model3DGroup();
        // origem -> (nome textura -> toggle)
        var bySource = new Dictionary<string, Dictionary<string, TextureToggle>>(StringComparer.OrdinalIgnoreCase);

        foreach (var part in geo.Parts)
        {
            var mesh = BuildMesh(part);
            if (mesh is null) continue;

            var textured = part.HasTexture && part.Uvs is { Length: > 0 } ? BuildTexture(part) : null;
            var model = new GeometryModel3D(mesh, Fallback) { BackMaterial = Fallback };
            group.Children.Add(model);
            _visuals.Add(new PartVisual { Model = model, Textured = textured, TexName = part.TextureName });

            if (textured is not null && part.TextureName is { Length: > 0 })
            {
                var src = part.TextureSource ?? "outros";
                if (!bySource.TryGetValue(src, out var map))
                    bySource[src] = map = new Dictionary<string, TextureToggle>(StringComparer.OrdinalIgnoreCase);
                if (!map.ContainsKey(part.TextureName))
                {
                    var toggle = new TextureToggle { Name = part.TextureName, IsEnabled = true };
                    toggle.PropertyChanged += OnToggleChanged;
                    map[part.TextureName] = toggle;
                }
            }
        }
        if (group.Children.Count == 0) return;

        foreach (var (src, map) in bySource.OrderBy(k => k.Key))
        {
            var tg = new TextureGroup { Source = src, IsEnabled = true };
            foreach (var t in map.Values.OrderBy(t => t.Name)) tg.Items.Add(t);
            _groups.Add(tg);
        }

        _container = new ModelVisual3D { Content = group };
        Viewport.Children.Add(_container);
        ApplyMaterials();
        // enquadra DEPOIS do layout, senão a câmera fica colada no modelo
        Dispatcher.BeginInvoke(new Action(() =>
        {
            try
            {
                Viewport.ZoomExtents(0);
                // o ZoomExtents deixa margem; aproxima ~40% pra o carro ocupar mais o quadro
                if (Viewport.Camera is ProjectionCamera cam)
                {
                    cam.Position += cam.LookDirection * 0.4;
                    cam.LookDirection *= 0.6;
                }
            }
            catch { }
        }), System.Windows.Threading.DispatcherPriority.Loaded);
    }

    private void OnToggleChanged(object? sender, PropertyChangedEventArgs e) => ApplyMaterials();
    private void OnMasterChanged(object sender, RoutedEventArgs e) => ApplyMaterials();

    private void OnGroupToggled(object sender, RoutedEventArgs e)
    {
        if (((FrameworkElement)sender).DataContext is not TextureGroup g) return;
        foreach (var t in g.Items) t.IsEnabled = g.IsEnabled; // dispara ApplyMaterials via toggle
        ApplyMaterials();
    }

    private void ApplyMaterials()
    {
        bool master = MasterTex.IsChecked == true;
        var enabled = _groups.SelectMany(g => g.Items).Where(t => t.IsEnabled)
                             .Select(t => t.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var v in _visuals)
        {
            bool useTex = master && v.Textured is not null &&
                          v.TexName is { Length: > 0 } && enabled.Contains(v.TexName);
            var mat = useTex ? v.Textured! : Fallback;
            v.Model.Material = mat;
            v.Model.BackMaterial = mat;
        }
    }

    private static MeshGeometry3D? BuildMesh(ModelPart part)
    {
        if (part.Positions.Length < 9) return null;
        var mesh = new MeshGeometry3D();
        for (int i = 0; i + 2 < part.Positions.Length; i += 3)
            mesh.Positions.Add(new Point3D(part.Positions[i], part.Positions[i + 1], part.Positions[i + 2]));
        foreach (var idx in part.Indices) mesh.TriangleIndices.Add(idx);
        if (part.Uvs is { Length: > 0 })
            for (int i = 0; i + 1 < part.Uvs.Length; i += 2)
                mesh.TextureCoordinates.Add(new Point(part.Uvs[i], part.Uvs[i + 1]));
        return mesh;
    }

    private static Material? BuildTexture(ModelPart part)
    {
        try
        {
            var bmp = BitmapSource.Create(part.TextureWidth, part.TextureHeight, 96, 96,
                PixelFormats.Bgra32, null, part.TextureBgra!, part.TextureWidth * 4);
            bmp.Freeze();
            var brush = new ImageBrush(bmp) { ViewportUnits = BrushMappingMode.Absolute };
            var mat = new DiffuseMaterial(brush);
            mat.Freeze();
            return mat;
        }
        catch { return null; }
    }
}

/// <summary>Grupo de texturas de um mesmo .ytd.</summary>
public sealed class TextureGroup
{
    public required string Source { get; init; }
    public bool IsEnabled { get; set; } = true;
    public ObservableCollection<TextureToggle> Items { get; } = new();
}

/// <summary>Item da lista de texturas: nome + ligado/desligado.</summary>
public sealed class TextureToggle : INotifyPropertyChanged
{
    public required string Name { get; init; }

    private bool _isEnabled = true;
    public bool IsEnabled
    {
        get => _isEnabled;
        set { if (_isEnabled != value) { _isEnabled = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsEnabled))); } }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
}
