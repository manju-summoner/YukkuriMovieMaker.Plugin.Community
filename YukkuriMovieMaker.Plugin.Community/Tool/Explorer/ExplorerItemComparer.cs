using System;
using System.Collections.Generic;
using System.IO;
using YukkuriMovieMaker.Commons;

namespace YukkuriMovieMaker.Plugin.Community.Tool.Explorer
{
    internal class ExplorerItemComparer : IComparer<IExplorerItemViewModel>
    {
        static readonly NaturalComparer NaturalComparer = new();

        readonly ExplorerSortKey key;
        readonly ExplorerSortOrder order;

        public ExplorerItemComparer(ExplorerSortKey key, ExplorerSortOrder order)
        {
            this.key = key;
            this.order = order;
        }

        public int Compare(IExplorerItemViewModel? x, IExplorerItemViewModel? y)
        {
            if (ReferenceEquals(x, y)) return 0;
            if (x is null) return -1;
            if (y is null) return 1;

            // Folders first like Windows Explorer
            bool xIsDir = x is ExplorerDirectoryItemViewModel;
            bool yIsDir = y is ExplorerDirectoryItemViewModel;
            if (xIsDir && !yIsDir) return -1;
            if (!xIsDir && yIsDir) return 1;

            int result = 0;
            switch (key)
            {
                case ExplorerSortKey.Extension:
                    result = CompareExtension(x.Path, y.Path);
                    break;
                case ExplorerSortKey.LastWriteTime:
                    result = CompareLastWriteTime(x.Path, y.Path);
                    break;
                case ExplorerSortKey.Name:
                default:
                    result = CompareName(x.Name, y.Name);
                    break;
            }
            if (order == ExplorerSortOrder.Descending)
                result = -result;
            return result;
        }

        static int CompareName(string a, string b)
        {
            // Case-insensitive natural sort
            return NaturalComparer.Compare(a, b);
        }

        static int CompareExtension(string aPath, string bPath)
        {
            // Directories have no extension.
            var aExt = GetExtensionForSort(aPath);
            var bExt = GetExtensionForSort(bPath);

            int cmp = NaturalComparer.Compare(aExt, bExt);
            if (cmp != 0) return cmp;

            // Tiebreaker by name natural sort
            return CompareName(Path.GetFileName(aPath), Path.GetFileName(bPath));
        }

        static string GetExtensionForSort(string path)
        {
            try
            {
                if (File.Exists(path))
                {
                    var ext = Path.GetExtension(path) ?? string.Empty;
                    return ext.StartsWith(".", StringComparison.Ordinal) ? ext[1..] : ext;
                }
            }
            catch { }
            return string.Empty;
        }

        static int CompareLastWriteTime(string aPath, string bPath)
        {
            DateTime a = GetLastWriteTime(aPath);
            DateTime b = GetLastWriteTime(bPath);
            int cmp = a.CompareTo(b);
            if (cmp != 0) return cmp;
            // Tiebreaker by name natural sort
            return CompareName(Path.GetFileName(aPath), Path.GetFileName(bPath));
        }

        static DateTime GetLastWriteTime(string path)
        {
            try
            {
                if (File.Exists(path)) return new FileInfo(path).LastWriteTime;
                if (Directory.Exists(path)) return new DirectoryInfo(path).LastWriteTime;
            }
            catch { }
            return DateTime.MinValue;
        }
    }
}
