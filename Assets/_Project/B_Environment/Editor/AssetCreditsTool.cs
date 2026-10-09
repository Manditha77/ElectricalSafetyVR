using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Tools > Workshop > Credits > 1. Scan Third-Party Assets
//   Finds every outside file the build scenes use (models, textures, HDRIs, audio, fonts) and writes
//     Assets/_Project/B_Environment/asset-licences.md   (the licence sheet - fill the [ ] cells here)
//     CREDITS.md                                         (repo root, made from the sheet)
//   Poly Haven files are recognised by their names (slug_diff_1k.jpg, slug_1k.fbx, slug_4k.hdr)
//   and get their page link + CC0 filled in. Re-running keeps everything you typed.
// Tools > Workshop > Credits > 2. Build In-World Credits Board
//   Reads asset-licences.md and puts a CREDITS board on the wall beside the results board
//   (the brief asks for credits inside the build). Re-run after editing the sheet.
public static class AssetCreditsTool
{
    const string SheetPath = "Assets/_Project/B_Environment/asset-licences.md";
    const string BoardName = "CreditsBoard";

    static readonly string[] ModelExt = { ".fbx", ".obj", ".blend", ".gltf", ".glb", ".dae", ".3ds", ".max", ".ma", ".mb" };
    static readonly string[] TexExt = { ".png", ".jpg", ".jpeg", ".tga", ".tif", ".tiff", ".exr", ".hdr", ".psd", ".bmp", ".gif" };
    static readonly string[] AudioExt = { ".wav", ".mp3", ".ogg", ".aif", ".aiff", ".flac" };
    static readonly string[] FontExt = { ".ttf", ".otf" };

    static readonly Regex PhMap = new Regex(@"^(?<slug>[a-z0-9_]+?)_(diff|diffuse|col|color|albedo|basecolor|nor_gl|nor_dx|nor|normal|rough|roughness|arm|ao|disp|displacement|spec|metal|metallic|mask|bump|alpha|opacity|translucent|emission)(_(?<res>\d+k))?$", RegexOptions.IgnoreCase);
    static readonly Regex PhRes = new Regex(@"^(?<slug>[a-z0-9_]+?)_(?<res>\d+k)$", RegexOptions.IgnoreCase);

    class Row
    {
        public string asset, type, source, author, licence, where, files;
        public string Key => asset.Trim().ToLowerInvariant();
    }

    class Group
    {
        public string name, source, author, licence, note;
        public readonly SortedSet<string> types = new SortedSet<string>();
        public readonly List<string> files = new List<string>();
        public readonly SortedSet<string> usedBy = new SortedSet<string>();
    }

    // ================================================================== 1. scan
    [MenuItem("Tools/Workshop/Credits/1. Scan Third-Party Assets")]
    static void Scan()
    {
        // scenes that go into the build (+ the open one)
        var scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToList();
        var open = EditorSceneManager.GetActiveScene().path;
        if (!string.IsNullOrEmpty(open) && !scenes.Contains(open)) scenes.Add(open);
        if (scenes.Count == 0) { EditorUtility.DisplayDialog("Credits", "Save the scene first (no scenes in Build Profiles and the open scene is unsaved).", "OK"); return; }

        var deps = AssetDatabase.GetDependencies(scenes.ToArray(), true);
        var used = new HashSet<string>(deps.Where(IsCreditable));

        // which material / prefab uses each file
        var users = new Dictionary<string, SortedSet<string>>();
        foreach (var d in deps)
        {
            if (!(d.EndsWith(".mat") || d.EndsWith(".prefab"))) continue;
            foreach (var x in AssetDatabase.GetDependencies(d, false))
            {
                if (!used.Contains(x)) continue;
                if (!users.TryGetValue(x, out var set)) users[x] = set = new SortedSet<string>();
                set.Add(Path.GetFileNameWithoutExtension(d));
            }
        }

        var groups = new Dictionary<string, Group>();
        foreach (var p in used.OrderBy(x => x)) Classify(p, groups, users);

        // files in the project that the build does not use
        var unused = AssetDatabase.GetAllAssetPaths()
            .Where(p => p.StartsWith("Assets/") && IsCreditable(p) && !used.Contains(p) && !p.Contains("/Editor/")
                        && !p.StartsWith("Assets/Samples/") && !p.StartsWith("Assets/TextMesh Pro/"))
            .OrderBy(p => p).ToList();

        // keep what the user already typed
        var old = ReadSheet(out _).ToDictionary(r => r.Key, r => r);
        var rows = new List<Row>();
        foreach (var g in groups.Values.OrderBy(g => Order(g)).ThenBy(g => g.name))
        {
            var r = new Row
            {
                asset = g.name,
                type = string.Join(", ", g.types),
                source = g.source,
                author = g.author,
                licence = g.licence,
                where = g.usedBy.Count == 0 ? "scene" : string.Join(", ", g.usedBy.Take(3)) + (g.usedBy.Count > 3 ? " +" + (g.usedBy.Count - 3) : ""),
                files = string.Join("<br>", g.files.Take(3).Select(f => "`" + f + "`")) + (g.files.Count > 3 ? "<br>+" + (g.files.Count - 3) + " more" : "") + (string.IsNullOrEmpty(g.note) ? "" : "<br>" + g.note)
            };
            if (old.TryGetValue(r.Key, out var o))
            {
                if (Filled(o.source)) r.source = o.source;
                if (Filled(o.author)) r.author = o.author;
                if (Filled(o.licence)) r.licence = o.licence;
            }
            rows.Add(r);
        }

        WriteSheet(rows, unused, used.Any(p => AudioExt.Contains(Path.GetExtension(p).ToLowerInvariant())));
        WriteCredits(rows);
        AssetDatabase.ImportAsset(SheetPath);

        int todo = rows.Count(r => !Filled(r.source) || !Filled(r.author) || !Filled(r.licence));
        string msg = "Found " + rows.Count + " outside asset group(s) used by the build" +
                     "\n(" + groups.Values.Count(g => g.licence.StartsWith("CC0")) + " from Poly Haven), and " + unused.Count + " unused file(s).\n\n" +
                     "Written:\n  " + SheetPath + "\n  CREDITS.md (repo root)\n\n" +
                     (todo > 0 ? todo + " row(s) still have [ ] cells to fill. Fill them in asset-licences.md, then run Scan again (your edits are kept)." : "All rows are filled.");
        Debug.Log("[Credits] " + msg);
        EditorUtility.DisplayDialog("Credits scan", msg, "OK");
        Selection.activeObject = AssetDatabase.LoadAssetAtPath<TextAsset>(SheetPath);
    }

    static bool IsCreditable(string p)
    {
        string e = Path.GetExtension(p).ToLowerInvariant();
        if (ModelExt.Contains(e) || TexExt.Contains(e) || AudioExt.Contains(e) || FontExt.Contains(e)) return true;
        if (e == ".asset" && AssetDatabase.GetMainAssetTypeAtPath(p) == typeof(TMP_FontAsset)) return true;
        return false;
    }

    static string Kind(string p)
    {
        string e = Path.GetExtension(p).ToLowerInvariant();
        if (ModelExt.Contains(e)) return "model";
        if (e == ".hdr" || e == ".exr") return "HDRI";
        if (TexExt.Contains(e)) return "texture";
        if (AudioExt.Contains(e)) return "audio";
        return "font";
    }

    static void Classify(string p, Dictionary<string, Group> groups, Dictionary<string, SortedSet<string>> users)
    {
        string file = Path.GetFileNameWithoutExtension(p);
        string kind = Kind(p);
        string key; Group g;

        // ---- Poly Haven (CC0) ----
        var m = PhMap.Match(file);
        var r = PhRes.Match(file);
        bool phFolder = p.ToLowerInvariant().Contains("polyhaven") || p.ToLowerInvariant().Contains("poly haven");
        string slug = null;
        if (m.Success && (m.Groups["res"].Success || phFolder)) slug = m.Groups["slug"].Value;
        else if (r.Success && (kind != "texture" || phFolder)) slug = r.Groups["slug"].Value;
        else if (phFolder) slug = file;

        if (slug != null)
        {
            slug = slug.ToLowerInvariant();
            key = "ph:" + slug;
            g = Get(groups, key, () => new Group
            {
                name = Pretty(slug) + " (Poly Haven)",
                source = "https://polyhaven.com/a/" + slug,
                author = "[author on the Poly Haven page]",
                licence = "CC0 1.0"
            });
        }
        else if (p.StartsWith("Packages/"))
        {
            string pkg = p.Split('/')[1];
            key = "pkg:" + pkg;
            g = Get(groups, key, () => new Group
            {
                name = "Unity package " + pkg,
                source = "Unity Package Manager",
                author = "Unity Technologies",
                licence = "Unity Companion License"
            });
        }
        else if (p.Contains("LiberationSans"))
        {
            key = "font:liberation";
            g = Get(groups, key, () => new Group
            {
                name = "Liberation Sans font (TextMesh Pro default)",
                source = "https://github.com/liberationfonts/liberation-fonts",
                author = "Liberation Fonts project (Red Hat)",
                licence = "SIL Open Font License 1.1"
            });
        }
        else if (p.StartsWith("Assets/TextMesh Pro/"))
        {
            key = "tmp";
            g = Get(groups, key, () => new Group
            {
                name = "TextMesh Pro Essential Resources",
                source = "Unity Package Manager (com.unity.ugui)",
                author = "Unity Technologies",
                licence = "Unity Companion License"
            });
        }
        else if (p.StartsWith("Assets/Samples/"))
        {
            var parts = p.Split('/');
            string pkg = parts.Length > 2 ? parts[2] : "Samples";
            key = "sample:" + pkg;
            g = Get(groups, key, () => new Group
            {
                name = pkg + " samples",
                source = "Unity Package Manager (package samples)",
                author = "Unity Technologies",
                licence = "Unity Companion License"
            });
        }
        else
        {
            var parts = p.Split('/');
            int tp = Array.FindIndex(parts, s => s.Equals("ThirdParty", StringComparison.OrdinalIgnoreCase));
            string folder = null, folderPath = null;
            if (tp >= 0 && tp + 1 < parts.Length - 1) { folder = parts[tp + 1]; folderPath = string.Join("/", parts.Take(tp + 2)); }
            else if (parts.Length > 2 && parts[1] != "_Project") { folder = parts[1]; folderPath = "Assets/" + parts[1]; }

            if (folder != null)
            {
                key = "pack:" + folderPath;
                string lic = FindLicenceFile(folderPath);
                g = Get(groups, key, () => new Group
                {
                    name = folder,
                    source = "[link: Asset Store / Kenney / Quaternius / Sketchfab page]",
                    author = "[author / publisher]",
                    licence = "[CC0 / CC BY 4.0 / Standard Unity Asset Store EULA]",
                    note = lic != null ? "licence file: `" + lic + "`" : ""
                });
            }
            else
            {
                key = "file:" + p;
                g = Get(groups, key, () => new Group
                {
                    name = Path.GetFileName(p),
                    source = "[team-made? or download link]",
                    author = "[author, or 'Team']",
                    licence = "[licence, or 'Team-made']"
                });
            }
        }

        g.types.Add(kind);
        g.files.Add(p);
        if (users.TryGetValue(p, out var u)) foreach (var x in u) g.usedBy.Add(x);
    }

    static Group Get(Dictionary<string, Group> d, string key, Func<Group> make)
    {
        if (!d.TryGetValue(key, out var g)) d[key] = g = make();
        return g;
    }

    static int Order(Group g)
    {
        if (g.licence.StartsWith("CC0")) return 0;
        if (g.source.StartsWith("[")) return 1;
        if (g.licence.StartsWith("SIL")) return 2;
        return 3;
    }

    static string FindLicenceFile(string folder)
    {
        if (!AssetDatabase.IsValidFolder(folder)) return null;
        foreach (var guid in AssetDatabase.FindAssets("", new[] { folder }))
        {
            var p = AssetDatabase.GUIDToAssetPath(guid);
            var n = Path.GetFileName(p).ToLowerInvariant();
            if (n.Contains("licen") || n.Contains("readme") || n.Contains("credit")) return p;
        }
        return null;
    }

    static string Pretty(string slug)
    {
        var words = slug.Split('_').Where(w => w.Length > 0)
            .Select(w => char.IsDigit(w[0]) ? w : char.ToUpperInvariant(w[0]) + w.Substring(1));
        return string.Join(" ", words);
    }

    static bool Filled(string s) => !string.IsNullOrWhiteSpace(s) && !s.Trim().StartsWith("[");

    static string Cell(string s) => (s ?? "").Replace("|", "/").Replace("\n", " ").Trim();

    // ================================================================== files
    static void WriteSheet(List<Row> rows, List<string> unused, bool hasAudioFiles)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Asset licences");
        sb.AppendLine();
        sb.AppendLine("Every outside asset used by the build. Scanned " + DateTime.Now.ToString("yyyy-MM-dd HH:mm") + " with *Tools > Workshop > Credits > 1. Scan Third-Party Assets*.");
        sb.AppendLine("Fill every `[ ]` cell from the asset's own page, then scan again: typed Source / Author / Licence cells are kept.");
        sb.AppendLine("CREDITS.md (repo root) and the in-game CREDITS board are made from this table.");
        sb.AppendLine();
        sb.AppendLine("## Used in the build");
        sb.AppendLine();
        sb.AppendLine("| Asset | Type | Source | Author | Licence | Where it is used | Files |");
        sb.AppendLine("| --- | --- | --- | --- | --- | --- | --- |");
        foreach (var r in rows)
            sb.AppendLine("| " + Cell(r.asset) + " | " + Cell(r.type) + " | " + Cell(r.source) + " | " + Cell(r.author) + " | " + Cell(r.licence) + " | " + Cell(r.where) + " | " + Cell(r.files) + " |");
        sb.AppendLine();
        sb.AppendLine("## Made by the team (no licence needed)");
        sb.AppendLine();
        sb.AppendLine("- Workshop room, hazards, spill kit, tool trolley, signs, boards and the Safe Isolation poster: built in Unity from primitives and TextMeshPro text.");
        if (!hasAudioFiles) sb.AppendLine("- All sound effects: generated in code at run time (no audio files).");
        sb.AppendLine("- Materials created in Unity (`Mat_*`) unless listed above.");
        sb.AppendLine();
        sb.AppendLine("## In the project but not used by the build");
        sb.AppendLine();
        if (unused.Count == 0) sb.AppendLine("None.");
        else
        {
            sb.AppendLine("These are not shipped, so they need no credit in the game. Delete them, or credit them here if they stay in the repo.");
            sb.AppendLine();
            foreach (var p in unused.Take(200)) sb.AppendLine("- `" + p + "`");
            if (unused.Count > 200) sb.AppendLine("- ... +" + (unused.Count - 200) + " more");
        }
        File.WriteAllText(SheetPath, sb.ToString());
    }

    static void WriteCredits(List<Row> rows)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Credits");
        sb.AppendLine();
        sb.AppendLine("**Electrical Workshop Safety Training Simulator** - INTE 42312 group project.");
        sb.AppendLine();
        sb.AppendLine("## Third-party assets");
        sb.AppendLine();
        foreach (var r in rows)
        {
            string link = r.source.StartsWith("http") ? " - <" + r.source + ">" : (Filled(r.source) ? " - " + r.source : "");
            sb.AppendLine("- **" + r.asset + "** by " + r.author + " - " + r.licence + link);
        }
        sb.AppendLine();
        sb.AppendLine("Poly Haven assets are CC0 (public domain); credit is not required but is given here.");
        sb.AppendLine();
        sb.AppendLine("## Platform");
        sb.AppendLine();
        sb.AppendLine("- Unity 6 (URP), XR Interaction Toolkit 3 (Starter Assets, XR Device Simulator), OpenXR - Unity Technologies.");
        sb.AppendLine();
        sb.AppendLine("## Made by the team");
        sb.AppendLine();
        sb.AppendLine("- Workshop environment, hazards, props, signs, boards and all sound effects (generated in code).");
        sb.AppendLine();
        sb.AppendLine("## AI assistance");
        sb.AppendLine();
        sb.AppendLine("- Claude (Anthropic): build guide, script drafting and debugging. Details in the presentation's AI disclosure.");
        sb.AppendLine();
        sb.AppendLine("Full licence table: `" + SheetPath + "`.");
        File.WriteAllText(Path.Combine(Directory.GetParent(Application.dataPath).FullName, "CREDITS.md"), sb.ToString());
    }

    static List<Row> ReadSheet(out bool exists)
    {
        var rows = new List<Row>();
        exists = File.Exists(SheetPath);
        if (!exists) return rows;
        bool inUsed = false;
        foreach (var line in File.ReadAllLines(SheetPath))
        {
            if (line.StartsWith("## ")) { inUsed = line.StartsWith("## Used"); continue; }
            if (!inUsed || !line.StartsWith("|") || line.Contains("| ---")) continue;
            var c = line.Trim().Trim('|').Split('|').Select(s => s.Trim()).ToArray();
            if (c.Length < 6 || c[0] == "Asset") continue;
            rows.Add(new Row { asset = c[0], type = c[1], source = c[2], author = c[3], licence = c[4], where = c[5], files = c.Length > 6 ? c[6] : "" });
        }
        return rows;
    }

    // ================================================================== 2. in-world board
    [MenuItem("Tools/Workshop/Credits/2. Build In-World Credits Board")]
    static void BuildBoard()
    {
        var rows = ReadSheet(out bool exists);
        if (!exists) { EditorUtility.DisplayDialog("Credits board", "Run '1. Scan Third-Party Assets' first.", "OK"); return; }

        var old = GameObject.Find(BoardName);
        Vector3 pos; Quaternion rot;
        if (old != null) { pos = old.transform.position; rot = old.transform.rotation; Undo.DestroyObjectImmediate(old); }
        else Place(out pos, out rot);

        var sb = new StringBuilder();
        sb.Append("<size=150%><b>CREDITS & LICENCES</b></size>\n");
        sb.Append("<color=#9AA0A8>Electrical Workshop Safety Training Simulator - INTE 42312</color>\n\n");
        foreach (var r in rows)
        {
            string src = r.source.StartsWith("http") ? r.source.Replace("https://", "") : (Filled(r.source) ? r.source : "");
            sb.Append("<b>" + r.asset + "</b>  " + (Filled(r.author) ? r.author : "") + "  <color=#F2B705>" + (Filled(r.licence) ? r.licence : "") + "</color>");
            if (src.Length > 0) sb.Append("  <color=#9AA0A8>" + src + "</color>");
            sb.Append("\n");
        }
        sb.Append("\nUnity 6, XR Interaction Toolkit 3, OpenXR - Unity Technologies\n");
        sb.Append("Workshop, props, signs and all sound effects made by the team.\n");
        sb.Append("AI assistance: Claude (Anthropic).");

        var board = new GameObject(BoardName);
        Undo.RegisterCreatedObjectUndo(board, "Credits board");
        board.transform.SetPositionAndRotation(pos, rot);

        const float W = 1.6f, H = 1.1f;
        var plate = GameObject.CreatePrimitive(PrimitiveType.Cube);
        UnityEngine.Object.DestroyImmediate(plate.GetComponent<Collider>());
        plate.name = "Plate";
        plate.transform.SetParent(board.transform, false);
        plate.transform.localPosition = new Vector3(0, 0, 0.02f);
        plate.transform.localScale = new Vector3(W + 0.08f, H + 0.08f, 0.02f);
        plate.GetComponent<Renderer>().sharedMaterial = PlateMat();

        var tg = new GameObject("Text");
        tg.transform.SetParent(board.transform, false);
        var t = tg.AddComponent<TextMeshPro>();
        t.rectTransform.sizeDelta = new Vector2(W, H);
        t.enableAutoSizing = true; t.fontSizeMin = 0.12f; t.fontSizeMax = 0.6f;
        t.textWrappingMode = TextWrappingModes.Normal;
        t.alignment = TextAlignmentOptions.TopLeft;
        t.color = new Color(0.95f, 0.95f, 0.93f);
        t.text = sb.ToString();

        EditorSceneManager.MarkSceneDirty(board.scene);
        Selection.activeGameObject = board;
        EditorUtility.DisplayDialog("Credits board", "CREDITS board built with " + rows.Count + " asset row(s).\nMove it if needed (it is a normal object), then save the scene.", "OK");
    }

    // beside the results board, same facing; else in front of the camera in the Scene view
    static void Place(out Vector3 pos, out Quaternion rot)
    {
        TMP_Text anchor = null;
        foreach (var mb in UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (mb != null && mb.GetType().Name == "TrainingResultsBoard") { anchor = mb.GetComponentInChildren<TMP_Text>(true); if (anchor != null) break; }
        if (anchor != null)
        {
            rot = anchor.transform.rotation;
            pos = anchor.transform.position + anchor.transform.right * 2.0f;
            pos.y = Mathf.Max(1.5f, pos.y);
            return;
        }
        var cam = SceneView.lastActiveSceneView != null ? SceneView.lastActiveSceneView.camera.transform : null;
        if (cam != null)
        {
            pos = cam.position + cam.forward * 2.5f;
            rot = Quaternion.LookRotation(Vector3.ProjectOnPlane(cam.forward, Vector3.up).normalized, Vector3.up);
        }
        else { pos = new Vector3(0, 1.5f, 0); rot = Quaternion.identity; }
    }

    static Material PlateMat()
    {
        const string path = "Assets/_Project/B_Environment/Materials/Mat_CreditsPlate.mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m != null) return m;
        if (!AssetDatabase.IsValidFolder("Assets/_Project/B_Environment/Materials")) AssetDatabase.CreateFolder("Assets/_Project/B_Environment", "Materials");
        m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        m.SetColor("_BaseColor", new Color(0.08f, 0.09f, 0.11f));
        m.SetFloat("_Smoothness", 0.2f);
        AssetDatabase.CreateAsset(m, path);
        return m;
    }
}
