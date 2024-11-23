using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Automation.Peers;

namespace SqueezeIt
{
    public class FileGridItem : INotifyPropertyChanged
    {
        static int _itemNumber = 0;
        public int ItemNumber { get; private set; }
        private string _result;
        private float? _newFileSize { get; set; }
        private string _newDimensions { get; set; }
        private float _reduction { get; set; }

        public string FileName { get; set; }
        public string FilePath { get; set; }
        public float FileSize { get; set; }
        public string ImageDimensions { get; set; }
        public float? NewFileSize
        {
            get => this._newFileSize;
            set
            {
                this._newFileSize = value;
                OnPropertyChanged();
            }
        }

        public string NewDimensions
        { 
            get => this._newDimensions;
            set
            {
                this._newDimensions = value;
                OnPropertyChanged();
            }
        }

        public float Reduction
        { 
            get => this._reduction;
            set
            {
                this._reduction = value;
                OnPropertyChanged();
            }    
        }

        public string Result {
            get => this._result;
            set
            {
                this._result = value;
                OnPropertyChanged();
            }
        }
        public int Width { get; set; }
        public int Height { get; set; }

        public bool Processed { get; set; }
        public bool Rotated { get; set; }

        public override string ToString()
        {
            return ItemNumber.ToString() + "-" + FileName;
        }

        public event PropertyChangedEventHandler PropertyChanged;
        
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
        public FileGridItem()
        {
            ItemNumber = ++_itemNumber;
        }
    }
}
