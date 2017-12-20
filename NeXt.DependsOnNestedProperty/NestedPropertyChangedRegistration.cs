using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

namespace NeXt.DependsOnNestedProperty
{
    /// <summary>
    /// automatically manages nested property change dependencies by subscribing to all propertychanged events on the path
    /// </summary>
    public sealed partial class NestedPropertyChangedRegistration : IDisposable
    {
        /// <summary>
        /// Create a new registration for all nested dependant properties on the <paramref name="target"/> object
        /// </summary>
        /// <typeparam name="T">type of the target</typeparam>
        /// <param name="target">the target to register on</param>
        /// <param name="onPropertyChanged">an action to call when a registered properties dependency changes</param>
        /// <returns>the registration object</returns>
        public static NestedPropertyChangedRegistration Create<T>(T target, Action<string> onPropertyChanged)
            where T : class, INotifyPropertyChanged
        {
            return new NestedPropertyChangedRegistration(typeof(T), target, onPropertyChanged);
        }
        
        private Action<string> InvokePropertyChanged { get; set; }
        
        private readonly ConcurrentDictionary<string, ConcurrentBag<RegistrationBase>> registrations;

        private void Add(string name, RegistrationBase registration)
        {
            registrations.GetOrAdd(name, k => new ConcurrentBag<RegistrationBase>()).Add(registration);
        }


        private NestedPropertyChangedRegistration(Type targetType, INotifyPropertyChanged target, Action<string> onPropertyChanged)
        {
            InvokePropertyChanged = onPropertyChanged;
            registrations = new ConcurrentDictionary<string, ConcurrentBag<RegistrationBase>>();

            Register(targetType, target);
        }

        private void Register(Type targetType, INotifyPropertyChanged target)
        {
            var properties = targetType.GetRuntimeProperties()
                .Where(p => p.IsDefined(typeof(DependsOnNestedAttribute)));
            
            foreach (var property in properties)
            {
                foreach (var attribute in property.GetCustomAttributes<DependsOnNestedAttribute>())
                {
                    RegisterProperty(targetType, property.Name, attribute.Path, target);
                }
            }
        }

        private void RegisterProperty(Type targetType, string name, IReadOnlyList<string> path, INotifyPropertyChanged target)
        {
            var stack = new Stack<(Type t, PropertyInfo p)>();

            var t = targetType;

            //create stack from path attribute (need to traverse in reversed order to build registration)
            for (var index = 0; index < path.Count - 1; index++)
            {
                if (!typeof(INotifyPropertyChanged).IsAssignableFrom(t)) throw new InvalidOperationException($"Type of \"{path[index - 1]}\" does not implement INotifyPropertyChanged");

                var property = t.GetProperty(path[index], BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public);

                if (property == null) throw new InvalidOperationException($"Path item was not valid: \"{path[index]}\"");

                stack.Push((t, property));
                t = property.PropertyType;
            }

            //build the primary registration for innermost property

            var prop = t.GetProperty(path[path.Count - 1], BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public);
            if (prop == null) throw new InvalidOperationException($"Path item was not valid: \"{path[path.Count - 1]}\"");

            RegistrationBase current = new PrimaryRegistration(InvokePropertyChanged, name, prop.Name);

            while (stack.Count > 0)
            {
                var c = stack.Pop();
                var getter = CreateOpenGetter(c.t, c.p);
                current = new SubRegistration(c.p.Name, current, getter);
            }

            Add(name, current);

            current.Register(target);
        }
        
        /// <summary>
        /// Creates an open delegate for the getter of the given property (on the given type)
        /// </summary>
        /// <param name="t"></param>
        /// <param name="property"></param>
        /// <returns></returns>
        private static Func<INotifyPropertyChanged, INotifyPropertyChanged> CreateOpenGetter(Type t, PropertyInfo property)
        {
            //the delegates target parameter: "INotifyPropertyChanged instance"
            var instanceParameter = Expression.Parameter(typeof(INotifyPropertyChanged), "instance");

            //the lambdas method call: ((t)instanceParameter).GetProperty()
            var call = Expression.Call(
                Expression.Convert(instanceParameter, t),
                property.GetGetMethod(true)
            );
            
            //the lambda putting things together: (INotifyPropertyChanged instance) => (INotifyPropertyChanged) ((t)instance).GetProperty()
            var lambda = Expression.Lambda<Func<INotifyPropertyChanged, INotifyPropertyChanged>>(
                Expression.Convert(call, typeof(INotifyPropertyChanged)),
                instanceParameter
            );

            return lambda.Compile();
        }

        /// <inheritdoc />
        public void Dispose()
        {
            foreach (var keyValuePair in registrations)
            {
                foreach (var registration in keyValuePair.Value)
                {
                    registration.Dispose();
                }
            }

            InvokePropertyChanged = null;
        }
    }
}
