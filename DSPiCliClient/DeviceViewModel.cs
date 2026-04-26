using System;
using System.Collections.Generic;
using CommunityToolkit.Mvvm.ComponentModel;

namespace DSPiCliClient;

public partial class DeviceViewModel : ObservableObject
{
    public class PresetItem
    {
        public string Slot { get; set; } = "";
        public string Name { get; set; } = "";
        public override string ToString() => Name;
    }

    [ObservableProperty] private List<PresetItem> _presetList = new();
    [ObservableProperty] private PresetItem? _selectedPreset;
  
    public DeviceViewModel()
    {
        
    }
}