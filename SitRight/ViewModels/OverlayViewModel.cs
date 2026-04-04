using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using SitRight.Models;

namespace SitRight.ViewModels;

public class OverlayViewModel : INotifyPropertyChanged
{
    private double _maskOpacity;
    private string _maskColor = "#FFFFFF";
    private double _edgeOpacity;
    private int _severityLevel;
    private bool _isVisible;

    public event PropertyChangedEventHandler? PropertyChanged;

    public double MaskOpacity
    {
        get => _maskOpacity;
        set => SetProperty(ref _maskOpacity, value);
    }

    public string MaskColor
    {
        get => _maskColor;
        set => SetProperty(ref _maskColor, value);
    }

    public double EdgeOpacity
    {
        get => _edgeOpacity;
        set => SetProperty(ref _edgeOpacity, value);
    }

    public int SeverityLevel
    {
        get => _severityLevel;
        set => SetProperty(ref _severityLevel, value);
    }

    public bool IsVisible
    {
        get => _isVisible;
        set => SetProperty(ref _isVisible, value);
    }

    public void UpdateFrom(OverlayState state)
    {
        MaskOpacity = state.MaskOpacity;
        MaskColor = state.MaskColor;
        EdgeOpacity = state.EdgeOpacity;
        SeverityLevel = state.SeverityLevel;
        IsVisible = state.MaskOpacity > 0.01;
    }

    protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
            return false;

        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        return true;
    }
}
