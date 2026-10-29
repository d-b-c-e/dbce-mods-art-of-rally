using System.Collections.Generic;
using Dbce.TripleScreen;
using UnityEngine;

namespace ArtOfSimRally.Mod.Triple;

/// <summary>
/// Redraws flat source renders onto curved panels: a grid over each panel samples its source texture at the UV the
/// eye ray through that point meets (<see cref="CurvedPanelWarp.SourceUv"/>). Drawn with Unity's always-included copy
/// shader, so no shader asset ships; screen-space overlay UI draws after all cameras and stays unwarped.
/// </summary>
internal sealed class CurvedPanelPresenter : MonoBehaviour
{
    internal const int Columns = 64, Rows = 36;
    private const string ShaderName = "Hidden/BlitCopy";
    private static Material s_material;
    private readonly List<Panel> _panels = new();

    private sealed class Panel
    {
        internal Texture Texture;
        internal Rect Area;
        internal Vector2[] Source;
    }

    internal Camera Camera { get; private set; }

    /// <summary>False when the copy shader is missing; the caller then stays flat.</summary>
    internal static bool Available => Shader.Find(ShaderName) != null;

    internal static CurvedPanelPresenter Create(string name, Transform parent, int display, float depth)
    {
        var host = new GameObject(name) { hideFlags = HideFlags.DontSave };
        host.transform.SetParent(parent, false);
        var camera = host.AddComponent<Camera>();
        camera.cullingMask = 0;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = Color.black;
        camera.targetDisplay = display;
        camera.depth = depth;
        camera.rect = new Rect(0f, 0f, 1f, 1f);
        camera.allowHDR = false;
        camera.allowMSAA = false;
        camera.useOcclusionCulling = false;
        var presenter = host.AddComponent<CurvedPanelPresenter>();
        presenter.Camera = camera;
        return presenter;
    }

    /// <summary>Adds a panel drawn into <paramref name="area"/> (fractions of this camera's viewport, 0,0 lower-left).</summary>
    internal void Add(Texture texture, Rect area, CurvedPanelWarp warp)
    {
        var source = new Vector2[(Columns + 1) * (Rows + 1)];
        for (var row = 0; row <= Rows; row++)
        for (var column = 0; column <= Columns; column++)
        {
            warp.SourceUv((double)column / Columns, (double)row / Rows, out var u, out var v);
            source[(row * (Columns + 1)) + column] = new Vector2((float)u, (float)v);
        }
        _panels.Add(new Panel { Texture = texture, Area = area, Source = source });
    }

    private void OnPostRender()
    {
        if (s_material == null)
        {
            var shader = Shader.Find(ShaderName);
            if (shader == null) return;
            s_material = new Material(shader) { hideFlags = HideFlags.DontSave };
        }
        GL.PushMatrix();
        GL.LoadOrtho();
        foreach (var panel in _panels)
        {
            if (panel.Texture == null) continue;
            s_material.mainTexture = panel.Texture;
            s_material.SetPass(0);
            GL.Begin(GL.QUADS);
            for (var row = 0; row < Rows; row++)
            for (var column = 0; column < Columns; column++)
            {
                Corner(panel, column, row);
                Corner(panel, column + 1, row);
                Corner(panel, column + 1, row + 1);
                Corner(panel, column, row + 1);
            }
            GL.End();
        }
        GL.PopMatrix();
    }

    private static void Corner(Panel panel, int column, int row)
    {
        var source = panel.Source[(row * (Columns + 1)) + column];
        GL.TexCoord2(source.x, source.y);
        GL.Vertex3(panel.Area.x + (panel.Area.width * column / Columns), panel.Area.y + (panel.Area.height * row / Rows), 0f);
    }
}
