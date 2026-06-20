using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using UnityEditor.AssetImporters;
using UnityEngine;

namespace Riten.Native.Cursors.Editor.Importers
{
    internal abstract class XCursorThemeImporterBase : ScriptedImporter
    {
        [SerializeField] bool _useTargetSize = true;
        [SerializeField] Vector2Int _targetSize = new(32, 32);

        private static readonly CursorRole[] Roles =
        {
            new("default", (pack, cursor) => pack.@default = cursor,
                "left_ptr", "arrow", "default", "top_left_arrow"),
            new("pointer", (pack, cursor) => pack.pointer = cursor,
                "pointer", "hand2", "pointing_hand", "hand", "link"),
            new("ibeam", (pack, cursor) => pack.ibeam = cursor,
                "xterm", "text", "ibeam"),
            new("wait", (pack, cursor) => pack.wait = cursor,
                "wait", "watch", "clock"),
            new("cross", (pack, cursor) => pack.cross = cursor,
                "crosshair", "cross", "tcross", "cross_reverse"),
            new("grab", (pack, cursor) => pack.grab = cursor,
                "grab", "openhand", "hand1"),
            new("grabbing", (pack, cursor) => pack.grabbing = cursor,
                "grabbing", "closedhand", "dragging"),
            new("denied", (pack, cursor) => pack.denied = cursor,
                "not-allowed", "forbidden", "crossed_circle", "crossed-circle", "no-drop", "dnd-no-drop"),
            new("move", (pack, cursor) => pack.move = cursor,
                "move", "all-scroll", "fleur", "size_all"),
            new("resize_horizontal", (pack, cursor) => pack.resizeHorizontal = cursor,
                "ew-resize", "h_double_arrow", "sb_h_double_arrow", "size_hor", "size-hor", "col-resize"),
            new("resize_vertical", (pack, cursor) => pack.resizeVertical = cursor,
                "ns-resize", "v_double_arrow", "sb_v_double_arrow", "size_ver", "size-ver", "row-resize"),
            new("resize_diagonal_1", (pack, cursor) => pack.resizeDiagonal1 = cursor,
                "nwse-resize", "size_fdiag", "size-fdiag", "top_left_corner", "bottom_right_corner"),
            new("resize_diagonal_2", (pack, cursor) => pack.resizeDiagonal2 = cursor,
                "nesw-resize", "size_bdiag", "size-bdiag", "top_right_corner", "bottom_left_corner")
        };

        public override void OnImportAsset(AssetImportContext ctx)
        {
            using var stream = File.OpenRead(ctx.assetPath);
            using var archive = new ZipArchive(stream, ZipArchiveMode.Read);

            var cursorEntries = FindCursorEntries(archive);

            if (cursorEntries.Count == 0)
            {
                Debug.LogError($"No Xcursor theme files found in archive: {ctx.assetPath}");
                return;
            }

            var pack = ScriptableObject.CreateInstance<CursorPack>();
            pack.name = Path.GetFileNameWithoutExtension(ctx.assetPath);

            var imported = 0;
            var cache = new Dictionary<string, VirtualCursorBase>(StringComparer.Ordinal);

            foreach (var role in Roles)
            {
                if (!TryFindEntry(cursorEntries, role.aliases, out var entry))
                    continue;

                if (!cache.TryGetValue(entry.FullName, out var cursor))
                {
                    cursor = ImportCursor(ctx, role.assetName, entry);

                    if (!cursor)
                        continue;

                    cache.Add(entry.FullName, cursor);
                }

                role.assign(pack, cursor);
                imported++;
            }

            if (imported == 0)
            {
                Debug.LogError($"Could not map any Xcursor theme files to Native Cursor slots: {ctx.assetPath}");
                return;
            }

            ctx.AddObjectToAsset("cursor_pack", pack);
            ctx.SetMainObject(pack);
        }

        private VirtualCursorBase ImportCursor(AssetImportContext ctx, string assetName, ZipArchiveEntry entry)
        {
            using var entryStream = entry.Open();

            if (!XCursorParser.TryLoadStream(entryStream, out var images))
            {
                Debug.LogWarning($"Skipping invalid Xcursor file in theme archive: {entry.FullName}");
                return null;
            }

            var selectedImages = XCursorParser.SelectImages(images, _useTargetSize, _targetSize);

            if (selectedImages.Count == 0)
                return null;

            var cursors = XCursorParser.ToCursorResults(selectedImages);

            if (XCursorParser.IsAnimatedGroup(selectedImages))
                return AddAnimatedCursor(ctx, assetName, cursors, XCursorParser.CalculateFramesPerSecond(selectedImages));

            return AddStaticCursor(ctx, assetName, cursors[0]);
        }

        private static VirtualCursor AddStaticCursor(AssetImportContext ctx, string assetName, CURSOR_RESULT cursorData)
        {
            var cursor = ScriptableObject.CreateInstance<VirtualCursor>();
            cursor.name = assetName;
            cursor.texture = cursorData.texture;
            cursor.texture.name = $"{assetName}_texture";
            cursor.hotspot = cursorData.hotspot;
            cursor.isMask = cursorData.isMask;
            cursor.backgroundColor = cursorData.backgroundColor;
            cursor.foregroundColor = cursorData.foregroundColor;

            ctx.AddObjectToAsset($"{assetName}_texture", cursor.texture, cursor.texture);
            ctx.AddObjectToAsset(assetName, cursor, cursor.texture);
            return cursor;
        }

        private static AnimatedVirtualCursor AddAnimatedCursor(AssetImportContext ctx, string assetName,
            IReadOnlyList<CURSOR_RESULT> frames, int fps)
        {
            var animatedCursor = ScriptableObject.CreateInstance<AnimatedVirtualCursor>();
            animatedCursor.name = assetName;
            animatedCursor.frames = new VirtualCursor[frames.Count];
            animatedCursor.fps = Mathf.Max(1, fps);

            for (var i = 0; i < frames.Count; ++i)
            {
                var frame = frames[i];
                var cursor = ScriptableObject.CreateInstance<VirtualCursor>();
                cursor.name = $"{assetName}_frame_{i}";
                cursor.texture = frame.texture;
                cursor.texture.name = $"{assetName}_frame_{i}_texture";
                cursor.isMask = frame.isMask;
                cursor.hotspot = frame.hotspot;
                cursor.backgroundColor = frame.backgroundColor;
                cursor.foregroundColor = frame.foregroundColor;

                ctx.AddObjectToAsset($"{assetName}_frame_{i}_texture", cursor.texture, cursor.texture);
                ctx.AddObjectToAsset($"{assetName}_frame_{i}", cursor, cursor.texture);
                animatedCursor.frames[i] = cursor;
            }

            ctx.AddObjectToAsset(assetName, animatedCursor, animatedCursor.frames[0].texture);
            return animatedCursor;
        }

        private static Dictionary<string, ZipArchiveEntry> FindCursorEntries(ZipArchive archive)
        {
            var entries = new Dictionary<string, ZipArchiveEntry>(StringComparer.OrdinalIgnoreCase);

            foreach (var entry in archive.Entries)
            {
                var normalizedPath = entry.FullName.Replace('\\', '/');
                var cursorsIndex = normalizedPath.IndexOf("/cursors/", StringComparison.OrdinalIgnoreCase);

                if (cursorsIndex < 0 || entry.Length == 0)
                    continue;

                var cursorName = normalizedPath[(cursorsIndex + "/cursors/".Length)..];

                if (string.IsNullOrEmpty(cursorName) || cursorName.Contains("/"))
                    continue;

                entries[cursorName] = entry;
            }

            return entries;
        }

        private static bool TryFindEntry(IReadOnlyDictionary<string, ZipArchiveEntry> entries,
            IReadOnlyList<string> aliases, out ZipArchiveEntry entry)
        {
            foreach (var alias in aliases)
            {
                if (entries.TryGetValue(alias, out entry))
                    return true;
            }

            entry = null;
            return false;
        }

        private readonly struct CursorRole
        {
            public readonly string assetName;
            public readonly Action<CursorPack, VirtualCursorBase> assign;
            public readonly string[] aliases;

            public CursorRole(string assetName, Action<CursorPack, VirtualCursorBase> assign, params string[] aliases)
            {
                this.assetName = assetName;
                this.assign = assign;
                this.aliases = aliases;
            }
        }
    }

    [ScriptedImporter(1, "xctheme")]
    internal sealed class XCursorThemeImporter : XCursorThemeImporterBase
    {
    }

    [ScriptedImporter(1, "xcursortheme")]
    internal sealed class XCursorThemeLongImporter : XCursorThemeImporterBase
    {
    }
}
