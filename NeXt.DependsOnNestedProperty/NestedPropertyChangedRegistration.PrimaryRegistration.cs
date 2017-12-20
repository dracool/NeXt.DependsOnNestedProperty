using System;
using System.ComponentModel;

namespace NeXt.DependsOnNestedProperty
{
    public sealed partial class NestedPropertyChangedRegistration
    {
        private class PrimaryRegistration : RegistrationBase
        {
            public PrimaryRegistration(Action<string> invokePropertyChanged, string name, string propertyName)
            {
                this.invokePropertyChanged = invokePropertyChanged;
                this.name = name;
                this.propertyName = propertyName;
            }

            private Action<string> invokePropertyChanged;
            private readonly string name;
            private readonly string propertyName;

            private void PropertyChanged(object sender, PropertyChangedEventArgs e)
            {
                if (e.PropertyName != propertyName) return;
                invokePropertyChanged(name);
            }

            /// <inheritdoc />
            protected override void DoRegister(INotifyPropertyChanged target)
            {
                target.PropertyChanged += PropertyChanged;
                invokePropertyChanged(name);
            }

            /// <inheritdoc />
            protected override void DoUnregister(INotifyPropertyChanged target)
            {
                target.PropertyChanged -= PropertyChanged;
            }

            /// <inheritdoc />
            protected override void DoDispose()
            {
                invokePropertyChanged = null;
            }
        }
    }
}
