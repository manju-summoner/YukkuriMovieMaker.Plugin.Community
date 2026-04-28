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
                    result = CompareExtension(x, y);
                    break;
                case ExplorerSortKey.LastWriteTime:
                    result = CompareLastWriteTime(x, y);
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

        static int CompareExtension(IExplorerItemViewModel a, IExplorerItemViewModel b)
        {
            int cmp = NaturalComparer.Compare(a.Extension, b.Extension);
            if (cmp != 0) return cmp;

            // Tiebreaker by name natural sort
            return CompareName(a.Name, b.Name);
        }

        static int CompareLastWriteTime(IExplorerItemViewModel a, IExplorerItemViewModel b)
        {
            int cmp = a.LastWriteTime.CompareTo(b.LastWriteTime);
            if (cmp != 0) return cmp;
            // Tiebreaker by name natural sort
            return CompareName(a.Name, b.Name);
        }
    }
}
