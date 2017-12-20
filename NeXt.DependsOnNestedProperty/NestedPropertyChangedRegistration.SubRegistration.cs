using System;
using System.ComponentModel;

namespace NeXt.DependsOnNestedProperty
{
    public sealed partial class NestedPropertyChangedRegistration
    {
        private class SubRegistration : RegistrationBase
        {
            public SubRegistration(string name, RegistrationBase next, Func<INotifyPropertyChanged, INotifyPropertyChanged> getter)
            {
                this.name = name;
                this.next = next;
                this.getter = getter;
            }

            private readonly string name;
            private readonly RegistrationBase next;

            private Func<INotifyPropertyChanged, INotifyPropertyChanged> getter;

            private void Bind(INotifyPropertyChanged newTarget)
            {
                next.Unregister();
                next.Register(newTarget);
            }
            
            private void PropertyChanged(object sender, PropertyChangedEventArgs e)
            {
                if (e.PropertyName != name) return;

                Bind(getter(Target));
            }

            protected override void DoRegister(INotifyPropertyChanged target)
            {
                target.PropertyChanged += PropertyChanged;
                next.Register(getter(target));
            }
            
            protected override void DoUnregister(INotifyPropertyChanged target)
            {
                next.Unregister();
                target.PropertyChanged -= PropertyChanged;
            }

            /// <inheritdoc />
            protected override void DoDispose()
            {
                getter = null;
            }
        }
    }
}
