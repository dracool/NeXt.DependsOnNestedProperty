using System;
using System.ComponentModel;

namespace NeXt.DependsOnNestedProperty
{
    public sealed partial class NestedPropertyChangedRegistration
    {
        private abstract class RegistrationBase : IDisposable
        {
            protected abstract void DoRegister(INotifyPropertyChanged target);
            protected abstract void DoUnregister(INotifyPropertyChanged target);

            protected INotifyPropertyChanged Target { get; private set; }

            public void Register(INotifyPropertyChanged target)
            {
                if (target == null) return;
                Target = target;
                DoRegister(this.Target);
            }

            public void Unregister()
            {
                if (Target == null) return;

                DoUnregister(Target);
                Target = null;
            }

            protected virtual void DoDispose() { }

            public void Dispose()
            {
                Unregister();
            }
        }
    }
}
