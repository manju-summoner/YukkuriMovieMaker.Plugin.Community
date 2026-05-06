using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.ComponentModel;
using System.Linq;
using YukkuriMovieMaker.Commons;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Audio.Equalizer2
{
    internal class BandsEditorViewModel : Bindable, IDisposable
    {
        readonly INotifyPropertyChanged item;
        readonly ItemProperty[] properties;

        List<Equalizer2Band> bands = [];

        public event EventHandler? BeginEdit;
        public event EventHandler? EndEdit;

        public List<Equalizer2Band> Bands { get => bands; set => Set(ref bands, value); }

        public int SelectedIndex
        {
            get => selectedIndex;
            set
            {
                if (!Set(ref selectedIndex, value))
                    return;
                RaiseCommandCanExecuteChanged();
            }
        }
        int selectedIndex = 0;

        public ActionCommand AddCommand { get; }
        public ActionCommand RemoveCommand { get; }
        public ActionCommand MoveUpCommand { get; }
        public ActionCommand MoveDownCommand { get; }
        public ActionCommand SortByFrequencyCommand { get; }

        public BandsEditorViewModel(ItemProperty[] properties)
        {
            this.properties = properties;

            item = (INotifyPropertyChanged)properties[0].PropertyOwner;
            item.PropertyChanged += Item_PropertyChanged;

            AddCommand = new ActionCommand(
                _ => true,
                _ =>
                {
                    var tmpSelectedIndex = SelectedIndex;
                    BeginEdit?.Invoke(this, EventArgs.Empty);
                    var list = Bands.ToList();
                    list.Insert(tmpSelectedIndex + 1, new Equalizer2Band(FilterType.PeakingEQ, 1000, 0, 1.41));
                    foreach (var property in properties)
                        property.SetValue(list.Select(x => new Equalizer2Band(x)).ToImmutableList());
                    EndEdit?.Invoke(this, EventArgs.Empty);
                    SelectedIndex = tmpSelectedIndex + 1;
                });

            RemoveCommand = new ActionCommand(
                _ => bands.Count > 1,
                _ =>
                {
                    var tmpSelectedIndex = SelectedIndex;
                    BeginEdit?.Invoke(this, EventArgs.Empty);
                    var list = Bands.ToList();
                    list.RemoveAt(SelectedIndex);
                    foreach (var property in properties)
                        property.SetValue(list.Select(x => new Equalizer2Band(x)).ToImmutableList());
                    EndEdit?.Invoke(this, EventArgs.Empty);
                    SelectedIndex = Math.Min(tmpSelectedIndex, list.Count - 1);
                });

            MoveUpCommand = new ActionCommand(
                _ => SelectedIndex > 0,
                _ =>
                {
                    var tmpSelectedIndex = SelectedIndex;
                    BeginEdit?.Invoke(this, EventArgs.Empty);
                    var list = Bands.ToList();
                    var band = list[tmpSelectedIndex];
                    list.RemoveAt(tmpSelectedIndex);
                    list.Insert(tmpSelectedIndex - 1, band);
                    foreach (var property in properties)
                        property.SetValue(list.Select(x => new Equalizer2Band(x)).ToImmutableList());
                    EndEdit?.Invoke(this, EventArgs.Empty);
                    SelectedIndex = tmpSelectedIndex - 1;
                });

            MoveDownCommand = new ActionCommand(
                _ => SelectedIndex < bands.Count - 1,
                _ =>
                {
                    var tmpSelectedIndex = SelectedIndex;
                    BeginEdit?.Invoke(this, EventArgs.Empty);
                    var list = Bands.ToList();
                    var band = list[tmpSelectedIndex];
                    list.RemoveAt(tmpSelectedIndex);
                    list.Insert(tmpSelectedIndex + 1, band);
                    foreach (var property in properties)
                        property.SetValue(list.Select(x => new Equalizer2Band(x)).ToImmutableList());
                    EndEdit?.Invoke(this, EventArgs.Empty);
                    SelectedIndex = tmpSelectedIndex + 1;
                });

            SortByFrequencyCommand = new ActionCommand(
                _ => bands.Count > 1,
                _ =>
                {
                    var selectedBand = SelectedIndex >= 0 && SelectedIndex < Bands.Count ? Bands[SelectedIndex] : null;
                    BeginEdit?.Invoke(this, EventArgs.Empty);
                    var sorted = Bands
                        .OrderByDescending(x => x.Frequency.Values[0].Value)
                        .Select(x => new Equalizer2Band(x))
                        .ToImmutableList();
                    foreach (var property in properties)
                        property.SetValue(sorted);
                    EndEdit?.Invoke(this, EventArgs.Empty);
                    if (selectedBand is not null)
                    {
                        var newIndex = sorted.FindIndex(x =>
                            x.FilterType == selectedBand.FilterType &&
                            x.Frequency.Values[0].Value == selectedBand.Frequency.Values[0].Value &&
                            x.Gain.Values[0].Value == selectedBand.Gain.Values[0].Value);
                        SelectedIndex = newIndex >= 0 ? newIndex : 0;
                    }
                });

            UpdateBands();
        }

        private void Item_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == properties[0].PropertyInfo.Name)
                UpdateBands();
        }

        void UpdateBands()
        {
            var values = properties[0].GetValue<ImmutableList<Equalizer2Band>>() ?? [];
            if (!Bands.SequenceEqual(values))
                Bands = [.. values];
            RaiseCommandCanExecuteChanged();
        }

        void RaiseCommandCanExecuteChanged()
        {
            AddCommand.RaiseCanExecuteChanged();
            RemoveCommand.RaiseCanExecuteChanged();
            MoveUpCommand.RaiseCanExecuteChanged();
            MoveDownCommand.RaiseCanExecuteChanged();
            SortByFrequencyCommand.RaiseCanExecuteChanged();
        }

        public void CopyToOtherItems()
        {
            var otherProperties = properties.Skip(1);
            foreach (var property in otherProperties)
                property.SetValue(Bands.Select(x => new Equalizer2Band(x)).ToImmutableList());
        }

        public void Dispose()
        {
            item.PropertyChanged -= Item_PropertyChanged;
        }
    }
}
