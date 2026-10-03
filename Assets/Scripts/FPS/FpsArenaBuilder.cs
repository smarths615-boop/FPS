using UnityEngine;

public static class FpsArenaBuilder
{
    public enum Tone { Floor, Wall, Trim, Blue, Red, Cover, Dark, Moss, Gold }

    private static readonly Color[] Colors =
    {
        new Color(0.46f, 0.47f, 0.48f),
        new Color(0.30f, 0.34f, 0.39f),
        new Color(0.44f, 0.49f, 0.54f),
        new Color(0.14f, 0.30f, 0.66f),
        new Color(0.65f, 0.18f, 0.24f),
        new Color(0.37f, 0.36f, 0.33f),
        new Color(0.16f, 0.19f, 0.23f),
        new Color(0.27f, 0.39f, 0.25f),
        new Color(0.72f, 0.58f, 0.35f)
    };

    public static Color GetColor(Tone tone) => Colors[(int)tone];

    public static GameObject Build(Transform parent, Material[] materials = null)
    {
        GameObject root = new GameObject("Blue Red Symmetric Arena");
        if (parent != null) root.transform.SetParent(parent, false);

        Material[] palette = materials ?? CreateRuntimePalette();
        Block(root.transform, "Main stone floor", new Vector3(0f, -0.36f, 0f), new Vector3(66f, 0.7f, 38f), palette, Tone.Floor);
        Block(root.transform, "Blue spawn floor", new Vector3(-25f, 0.012f, 0f), new Vector3(15f, 0.035f, 29f), palette, Tone.Blue);
        Block(root.transform, "Red spawn floor", new Vector3(25f, 0.012f, 0f), new Vector3(15f, 0.035f, 29f), palette, Tone.Red);
        Block(root.transform, "Central court", new Vector3(0f, 0.022f, 0f), new Vector3(36f, 0.045f, 29f), palette, Tone.Floor);

        // The arena is mirrored across the central x = 0 plane.
        Block(root.transform, "North perimeter", new Vector3(0f, 1.5f, 18.5f), new Vector3(66f, 3f, 1f), palette, Tone.Wall);
        Block(root.transform, "South perimeter", new Vector3(0f, 1.5f, -18.5f), new Vector3(66f, 3f, 1f), palette, Tone.Wall);
        Block(root.transform, "Blue end wall", new Vector3(-32.5f, 1.5f, 0f), new Vector3(1f, 3f, 38f), palette, Tone.Wall);
        Block(root.transform, "Red end wall", new Vector3(32.5f, 1.5f, 0f), new Vector3(1f, 3f, 38f), palette, Tone.Wall);

        for (int side = -1; side <= 1; side += 2)
        {
            string team = side < 0 ? "Blue" : "Red";
            Tone teamTone = side < 0 ? Tone.Blue : Tone.Red;
            Block(root.transform, team + " spawn marker", new Vector3(side * 26f, 0.055f, 0f), new Vector3(5.5f, 0.02f, 0.32f), palette, Tone.Gold);
            Block(root.transform, team + " inner gate north", new Vector3(side * 18f, 1.7f, 10.5f), new Vector3(1.1f, 3.4f, 15f), palette, Tone.Wall);
            Block(root.transform, team + " inner gate south", new Vector3(side * 18f, 1.7f, -10.5f), new Vector3(1.1f, 3.4f, 15f), palette, Tone.Wall);
            Block(root.transform, team + " spawn ledge north", new Vector3(side * 25f, 0.72f, 14.8f), new Vector3(12f, 1.45f, 1f), palette, Tone.Trim);
            Block(root.transform, team + " spawn ledge south", new Vector3(side * 25f, 0.72f, -14.8f), new Vector3(12f, 1.45f, 1f), palette, Tone.Trim);

            for (int lane = -1; lane <= 1; lane += 2)
            {
                string label = team + (lane < 0 ? " South" : " North");
                Block(root.transform, label + " tower", new Vector3(side * 23f, 2.1f, lane * 11.8f), new Vector3(5.2f, 4.2f, 4.5f), palette, Tone.Wall);
                Block(root.transform, label + " tower roof", new Vector3(side * 23f, 4.25f, lane * 11.8f), new Vector3(5.8f, 0.4f, 5.1f), palette, teamTone);
                Block(root.transform, label + " low wall", new Vector3(side * 12.4f, 0.65f, lane * 12.7f), new Vector3(6.5f, 1.3f, 1.2f), palette, Tone.Trim);
                Crate(root.transform, label + " crate A", new Vector3(side * 10.5f, 0.75f, lane * 5.5f), 1.5f, palette);
                Crate(root.transform, label + " crate B", new Vector3(side * 12.1f, 0.75f, lane * 5.5f), 1.5f, palette);
                Crate(root.transform, label + " crate top", new Vector3(side * 11.3f, 2.25f, lane * 5.5f), 1.5f, palette);
                Block(root.transform, label + " forward cover", new Vector3(side * 6.8f, 0.9f, lane * 2.7f), new Vector3(1.35f, 1.8f, 3.3f), palette, Tone.Cover);
                Moss(root.transform, new Vector3(side * 30f, 3.3f, lane * 16f), palette);
            }

            Block(root.transform, team + " raised step", new Vector3(side * 20f, 0.25f, 0f), new Vector3(2.6f, 0.5f, 7.5f), palette, Tone.Trim);
            Block(root.transform, team + " central crate", new Vector3(side * 12.5f, 0.75f, 0f), new Vector3(1.5f, 1.5f, 1.5f), palette, Tone.Cover);
        }

        Block(root.transform, "Central objective plinth", new Vector3(0f, 0.35f, 0f), new Vector3(8f, 0.7f, 8f), palette, Tone.Trim);
        Block(root.transform, "Central monument", new Vector3(0f, 2.25f, 0f), new Vector3(5.4f, 3.2f, 5.4f), palette, Tone.Wall);
        Block(root.transform, "Central monument cap", new Vector3(0f, 4f, 0f), new Vector3(6f, 0.35f, 6f), palette, Tone.Dark);
        for (int lane = -1; lane <= 1; lane += 2)
        {
            Block(root.transform, lane < 0 ? "South center barrier" : "North center barrier", new Vector3(0f, 0.85f, lane * 10.5f), new Vector3(8.5f, 1.7f, 1.1f), palette, Tone.Wall);
            Block(root.transform, lane < 0 ? "South center gate" : "North center gate", new Vector3(0f, 2.2f, lane * 17f), new Vector3(4.5f, 4.4f, 2f), palette, Tone.Wall);
        }

        return root;
    }

    private static Material[] CreateRuntimePalette()
    {
        Material[] palette = new Material[Colors.Length];
        Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        for (int i = 0; i < palette.Length; i++)
        {
            palette[i] = new Material(shader);
            palette[i].name = ((Tone)i) + " Arena Material";
            palette[i].color = Colors[i];
        }
        return palette;
    }

    private static void Block(Transform parent, string name, Vector3 position, Vector3 scale, Material[] palette, Tone tone)
    {
        GameObject block = GameObject.CreatePrimitive(PrimitiveType.Cube);
        block.name = name;
        block.transform.SetParent(parent, false);
        block.transform.localPosition = position;
        block.transform.localScale = scale;
        block.GetComponent<Renderer>().sharedMaterial = palette[(int)tone];
    }

    private static void Crate(Transform parent, string name, Vector3 position, float size, Material[] palette)
    {
        Block(parent, name, position, Vector3.one * size, palette, Tone.Cover);
        Block(parent, name + " lid", position + Vector3.up * (size * 0.48f), new Vector3(size * 1.06f, 0.1f, size * 1.06f), palette, Tone.Trim);
    }

    private static void Moss(Transform parent, Vector3 position, Material[] palette)
    {
        Block(parent, "Low poly foliage", position, new Vector3(1.6f, 0.9f, 1.1f), palette, Tone.Moss);
        Block(parent, "Low poly foliage tip", position + new Vector3(0.3f, 0.6f, 0.2f), new Vector3(0.9f, 0.7f, 0.9f), palette, Tone.Moss);
    }
}
