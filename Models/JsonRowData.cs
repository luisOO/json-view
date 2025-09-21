using System.ComponentModel;

namespace JsonViewer.Models
{
    /// <summary>
    /// JSON行数据模型 - 用于高性能视图
    /// </summary>
    public class JsonRowData : INotifyPropertyChanged
    {
        private string _name = string.Empty;
        private string _value = string.Empty;
        private string _type = string.Empty;
        private int _level;
        private bool _hasChildren;
        private string _path = string.Empty;
        
        public string Name
        {
            get => _name;
            set
            {
                _name = value;
                OnPropertyChanged(nameof(Name));
            }
        }
        
        public string Value
        {
            get => _value;
            set
            {
                _value = value;
                OnPropertyChanged(nameof(Value));
            }
        }
        
        public string Type
        {
            get => _type;
            set
            {
                _type = value;
                OnPropertyChanged(nameof(Type));
            }
        }
        
        public int Level
        {
            get => _level;
            set
            {
                _level = value;
                OnPropertyChanged(nameof(Level));
            }
        }
        
        public bool HasChildren
        {
            get => _hasChildren;
            set
            {
                _hasChildren = value;
                OnPropertyChanged(nameof(HasChildren));
            }
        }
        
        public string Path
        {
            get => _path;
            set
            {
                _path = value;
                OnPropertyChanged(nameof(Path));
            }
        }
        
        public event PropertyChangedEventHandler? PropertyChanged;
        
        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}