using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Editor.Control;

/// <summary>
///     NumberPortをWPFバインディングで使用するためのラッパー
/// </summary>
public class NumberPortWrapper : ContentControl
{
    public static readonly DependencyProperty ValueProperty =
        DependencyProperty.Register(
            nameof(Value),
            typeof(object),
            typeof(NumberPortWrapper),
            new FrameworkPropertyMetadata(
                null,
                FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                OnValueChanged));

    public static readonly DependencyProperty MinProperty =
        DependencyProperty.Register(nameof(Min), typeof(float), typeof(NumberPortWrapper),
            new PropertyMetadata(float.NaN, OnConfigChanged));

    public static readonly DependencyProperty MaxProperty =
        DependencyProperty.Register(nameof(Max), typeof(float), typeof(NumberPortWrapper),
            new PropertyMetadata(float.NaN, OnConfigChanged));

    public static readonly DependencyProperty DigitsProperty =
        DependencyProperty.Register(nameof(Digits), typeof(int), typeof(NumberPortWrapper),
            new PropertyMetadata(2, OnConfigChanged));

    public static readonly DependencyProperty UnitProperty =
        DependencyProperty.Register(nameof(Unit), typeof(string), typeof(NumberPortWrapper),
            new PropertyMetadata("", OnConfigChanged));

    public static readonly DependencyProperty DefaultProperty =
        DependencyProperty.Register(nameof(Default), typeof(float), typeof(NumberPortWrapper),
            new PropertyMetadata(0f));

    private NumberPort? _numberPort;

    public NumberPortWrapper()
    {
        Loaded += OnLoaded;
    }

    public object? Value
    {
        get => GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    public float Min
    {
        get => (float)GetValue(MinProperty);
        set => SetValue(MinProperty, value);
    }

    public float Max
    {
        get => (float)GetValue(MaxProperty);
        set => SetValue(MaxProperty, value);
    }

    public int Digits
    {
        get => (int)GetValue(DigitsProperty);
        set => SetValue(DigitsProperty, value);
    }

    public string Unit
    {
        get => (string)GetValue(UnitProperty);
        set => SetValue(UnitProperty, value);
    }

    public float Default
    {
        get => (float)GetValue(DefaultProperty);
        set => SetValue(DefaultProperty, value);
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (_numberPort == null) InitializeNumberPort();
    }

    private void InitializeNumberPort()
    {
        var currentValue = Value is float f ? f : Default;

        _numberPort = new NumberPort(
            Default,
            currentValue,
            Min,
            Max,
            Digits,
            Unit
        );

        _numberPort.PropertyChanged += OnNumberPortValueChanged;

        Content = _numberPort;
    }

    private void OnNumberPortValueChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(NumberPort.Value) && _numberPort != null)
            SetValue(ValueProperty, _numberPort.Value);
    }

    private static void OnValueChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is NumberPortWrapper { _numberPort: not null } wrapper)
            wrapper._numberPort.UpdateValueSilently(e.NewValue);
    }

    private static void OnConfigChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is NumberPortWrapper { _numberPort: not null } wrapper)
            wrapper._numberPort.ChangeSetting(
                wrapper.Min,
                wrapper.Max,
                wrapper.Digits,
                wrapper.Unit
            );
    }
}