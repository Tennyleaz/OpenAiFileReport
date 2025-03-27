using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static System.Net.Mime.MediaTypeNames;

namespace OpenAiFileReport;

internal class InputFileModel : INotifyPropertyChanged
{
    private string _fullPath;
    private string _fileName;
    private string _fileType;
    private string _content;
    private bool _isProcessing;
    private bool _isProcessed;
    private bool _isSelected;

    public string FullPath
    {
        get => _fullPath;
        set
        {
            if (_fullPath != value)
            {
                _fullPath = value;
                OnPropertyChanged(nameof(FullPath));
            }
        }
    }

    public string FileName
    {
        get => _fileName;
        set
        {
            if (_fileName != value)
            {
                _fileName = value;
                OnPropertyChanged(nameof(FileName));
            }
        }
    }

    public string FileType
    {
        get => _fileType;
        set
        {
            if (_fileType != value)
            {
                _fileType = value;
                OnPropertyChanged(nameof(FileType));
            }
        }
    }

    public string Content
    {
        get => _content;
        set
        {
            if (_content != value)
            {
                _content = value;
                OnPropertyChanged(nameof(Content));
            }
        }
    }

    public bool IsProcessing
    {
        get => _isProcessing;
        set
        {
            if (_isProcessing != value)
            {
                _isProcessing = value;
                OnPropertyChanged(nameof(IsProcessing));
                NotifyCanCheckChanged();
            }
        }
    }

    public bool IsProcessed
    {
        get => _isProcessed;
        set
        {
            if (_isProcessed != value)
            {
                _isProcessed = value;
                OnPropertyChanged(nameof(IsProcessed));
                NotifyCanCheckChanged();
            }
        }
    }

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (_isSelected != value)
            {
                _isSelected = value;
                OnPropertyChanged(nameof(IsSelected));
            }
        }
    }

    public bool CanCheck
    {
        get => IsProcessed && !IsProcessing;
    }

    // Notify UI when dependent properties change
    private void NotifyCanCheckChanged()
    {
        OnPropertyChanged(nameof(CanCheck));
    }

    public string PineconeId { get; set; }

    protected virtual void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    public event PropertyChangedEventHandler PropertyChanged;

    public override string ToString()
    {
        return FileName;
    }
}
