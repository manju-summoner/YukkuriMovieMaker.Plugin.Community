using Microsoft.Xaml.Behaviors;
using System.Collections;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows.Controls;
using System.Windows.Threading;

namespace YukkuriMovieMaker.Plugin.Community.Tool.Explorer
{
    internal class ScrollSelectedItemIntoViewBehavior : Behavior<ListBox>
    {
        DependencyPropertyDescriptor? itemsSourcePropertyDescriptor;
        INotifyCollectionChanged? itemsSourceCollection;
        readonly HashSet<IExplorerSelectableItem> subscribedItems = [];

        protected override void OnAttached()
        {
            base.OnAttached();
            AssociatedObject.SelectionChanged += AssociatedObject_SelectionChanged;
            itemsSourcePropertyDescriptor = DependencyPropertyDescriptor.FromProperty(ItemsControl.ItemsSourceProperty, typeof(ListBox));
            itemsSourcePropertyDescriptor?.AddValueChanged(AssociatedObject, AssociatedObject_ItemsSourceChanged);
            SubscribeItemsSource();
        }

        protected override void OnDetaching()
        {
            base.OnDetaching();
            AssociatedObject.SelectionChanged -= AssociatedObject_SelectionChanged;
            itemsSourcePropertyDescriptor?.RemoveValueChanged(AssociatedObject, AssociatedObject_ItemsSourceChanged);
            itemsSourcePropertyDescriptor = null;
            UnsubscribeItemsSource();
        }

        private void AssociatedObject_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var item = e.AddedItems.Count > 0 ? e.AddedItems[^1] : AssociatedObject.SelectedItem;
            if (item is null)
                return;

            ScrollIntoView(item);
        }

        private void AssociatedObject_ItemsSourceChanged(object? sender, EventArgs e)
        {
            UnsubscribeItemsSource();
            SubscribeItemsSource();
        }

        private void SubscribeItemsSource()
        {
            itemsSourceCollection = AssociatedObject.ItemsSource as INotifyCollectionChanged;
            if (itemsSourceCollection is not null)
                itemsSourceCollection.CollectionChanged += ItemsSourceCollection_CollectionChanged;

            foreach (var item in GetItems())
                SubscribeItem(item);
        }

        private void UnsubscribeItemsSource()
        {
            if (itemsSourceCollection is not null)
            {
                itemsSourceCollection.CollectionChanged -= ItemsSourceCollection_CollectionChanged;
                itemsSourceCollection = null;
            }

            UnsubscribeItems();
        }

        private void ItemsSourceCollection_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.OldItems is not null)
            {
                foreach (var item in e.OldItems)
                    UnsubscribeItem(item);
            }

            if (e.Action == NotifyCollectionChangedAction.Reset)
            {
                UnsubscribeItems();
                foreach (var item in GetItems())
                    SubscribeItem(item);
            }

            if (e.NewItems is not null)
            {
                foreach (var item in e.NewItems)
                    SubscribeItem(item);
            }
        }

        private void SubscribeItem(object? item)
        {
            if (item is not IExplorerSelectableItem selectableItem)
                return;

            if (!subscribedItems.Add(selectableItem))
                return;

            selectableItem.PropertyChanged += Item_PropertyChanged;
            if (selectableItem.IsSelected)
                ScrollIntoView(selectableItem);
        }

        private void UnsubscribeItem(object? item)
        {
            if (item is IExplorerSelectableItem selectableItem && subscribedItems.Remove(selectableItem))
                selectableItem.PropertyChanged -= Item_PropertyChanged;
        }

        private void UnsubscribeItems()
        {
            foreach (var item in subscribedItems)
                item.PropertyChanged -= Item_PropertyChanged;

            subscribedItems.Clear();
        }

        private void Item_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (!string.IsNullOrEmpty(e.PropertyName) && e.PropertyName != nameof(IExplorerSelectableItem.IsSelected))
                return;

            if (sender is IExplorerSelectableItem { IsSelected: true } item)
                ScrollIntoView(item);
        }

        private IEnumerable GetItems() => AssociatedObject.ItemsSource as IEnumerable ?? AssociatedObject.Items;

        private void ScrollIntoView(object item)
        {
            AssociatedObject.Dispatcher.InvokeAsync(() =>
            {
                if (AssociatedObject.Items.Contains(item))
                    AssociatedObject.ScrollIntoView(item);
            }, DispatcherPriority.Loaded);
        }
    }
}
