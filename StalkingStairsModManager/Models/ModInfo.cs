using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace StalkingStairsModManager.Models
{
    public class ModInfo : INotifyPropertyChanged
    {
        private string? _name;
        private string? _author;
        private string? _version;
        private string? _downloadUrl;
        private string? _description;
        private bool _enabled;
        private string? _gitPath;
        private long? _releaseId;
        private string? _group;

        public string name
        {
            get => _name ?? string.Empty;
            set
            {
                if (SetProperty(ref _name, value))
                {
                    // name change may affect computed properties
                    OnPropertyChanged(nameof(DisplayName));
                    OnPropertyChanged(nameof(isToggleable));
                }
            }
        }

        public string author
        {
            get => _author ?? string.Empty;
            set => SetProperty(ref _author, value);
        }

        public string version
        {
            get => _version ?? string.Empty;
            set
            {
                if (SetProperty(ref _version, value))
                {
                    OnPropertyChanged(nameof(DisplayName));
                }
            }
        }

        public string downloadUrl
        {
            get => _downloadUrl ?? string.Empty;
            set => SetProperty(ref _downloadUrl, value);
        }

        public string description
        {
            get => _description ?? string.Empty;
            set => SetProperty(ref _description, value);
        }

        public bool enabled
        {
            get => _enabled;
            set => SetProperty(ref _enabled, value);
        }

        public string gitPath
        {
            get => _gitPath ?? string.Empty;
            set => SetProperty(ref _gitPath, value);
        }

        public long? releaseId
        {
            get => _releaseId;
            set => SetProperty(ref _releaseId, value);
        }

        // group (used for UI grouping)
        public string group
        {
            get => _group ?? string.Empty;
            set => SetProperty(ref _group, value);
        }

        // readonly display used by the UI: "Name - Version"
        public string DisplayName => string.IsNullOrWhiteSpace(version) ? name : $"{name} - {version}";

        // computed: BepInEx is forced and not toggleable
        public bool isToggleable => !string.Equals(name, "BepInEx", StringComparison.OrdinalIgnoreCase);

        public event PropertyChangedEventHandler? PropertyChanged;

        protected bool SetProperty<T>(ref T? field, T? value, [CallerMemberName] string? propertyName = null)
        {
            if (EqualityComparer<T?>.Default.Equals(field, value)) return false;
            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }

        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}