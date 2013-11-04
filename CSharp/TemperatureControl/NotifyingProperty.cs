using System;
using System.ComponentModel;

namespace DataLogging
{
    class NotifyingProperty 
    {
        private string _property;
        public string Prop
        {
            get { return _property; }
            set
            {
                _property = value;
                PropertyChanged();
            }
        }
        public Action PropertyChanged;
        

        
    }
}
